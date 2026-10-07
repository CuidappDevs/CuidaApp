using CUIDAPP.Localization;
using CUIDAPP.Models.Ticket;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Soporte
{
    public partial class NuevoReportePage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        // El servidor guarda la categoría en español; al usuario se la mostramos traducida.
        private static readonly string[] CategoriasValor = { "Cuenta", "Pago", "Servicio", "Chat", "Otro" };

        public NuevoReportePage()
        {
            InitializeComponent();
            PickerCategoria.ItemsSource = CategoriasValor.Select(Localizador.D).ToList();

            // Borde a borde: el encabezado empieza debajo de la barra de estado y el contenido
            // termina con espacio para la barra de gestos.
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();

            ConstruirCategorias();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
        }

        // Íconos de cada categoría (mismo orden que CategoriasValor)
        private static readonly string[] IconosCategoria =
        {
            "M12 12C14.21 12 16 10.21 16 8C16 5.79 14.21 4 12 4C9.79 4 8 5.79 8 8C8 10.21 9.79 12 12 12ZM12 14C9.33 14 4 15.34 4 18V20H20V18C20 15.34 14.67 14 12 14Z",
            "M20 4H4C2.89 4 2.01 4.89 2.01 6L2 18C2 19.11 2.89 20 4 20H20C21.11 20 22 19.11 22 18V6C22 4.89 21.11 4 20 4ZM20 18H4V12H20V18ZM20 8H4V6H20V8Z",
            "M10 20V14H14V20H19V12H22L12 3L2 12H5V20H10Z",
            "M20 2H4C2.9 2 2 2.9 2 4V22L6 18H20C21.1 18 22 17.1 22 16V4C22 2.9 21.1 2 20 2Z",
            "M6 10C4.9 10 4 10.9 4 12C4 13.1 4.9 14 6 14C7.1 14 8 13.1 8 12C8 10.9 7.1 10 6 10ZM18 10C16.9 10 16 10.9 16 12C16 13.1 16.9 14 18 14C19.1 14 20 13.1 20 12C20 10.9 19.1 10 18 10ZM12 10C10.9 10 10 10.9 10 12C10 13.1 10.9 14 12 14C13.1 14 14 13.1 14 12C14 10.9 13.1 10 12 10Z"
        };

        private readonly List<(Border Tarjeta, Border Caja, Microsoft.Maui.Controls.Shapes.Path Icono, Label Texto)> tarjetasCategoria = new();

        // Categorías como tarjetas con ícono; al tocar una se selecciona en el Picker (oculto).
        private void ConstruirCategorias()
        {
            ListaCategorias.Clear();
            tarjetasCategoria.Clear();
            for (var i = 0; i < CategoriasValor.Length; i++)
            {
                var indice = i;
                var icono = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(IconosCategoria[i])!,
                    Aspect = Stretch.Uniform, WidthRequest = 16, HeightRequest = 16,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                };
                var caja = new Border
                {
                    Stroke = Colors.Transparent,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    WidthRequest = 30, HeightRequest = 30,
                    VerticalOptions = LayoutOptions.Center,
                    Content = icono
                };
                var texto = new Label { Text = Localizador.D(CategoriasValor[i]), FontSize = 14, FontFamily = "OpenSansSemibold", VerticalOptions = LayoutOptions.Center };
                var tarjeta = new Border
                {
                    StrokeThickness = 1.5,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
                    Padding = new Thickness(6, 6, 14, 6),
                    Margin = new Thickness(0, 0, 8, 8),
                    Content = new HorizontalStackLayout { Spacing = 8, Children = { caja, texto } }
                };
                tarjeta.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                    {
                        PickerCategoria.SelectedIndex = indice;
                        PintarCategorias();
                        await tarjeta.ScaleTo(0.94, 80, Easing.CubicOut);
                        _ = tarjeta.ScaleTo(1, 180, Easing.CubicOut);
                    })
                });
                tarjetasCategoria.Add((tarjeta, caja, icono, texto));
                ListaCategorias.Add(tarjeta);
            }
            PintarCategorias();
        }

        private void PintarCategorias()
        {
            Color R(string k) => (Color)Application.Current!.Resources[k];
            for (var i = 0; i < tarjetasCategoria.Count; i++)
            {
                var (tarjeta, caja, icono, texto) = tarjetasCategoria[i];
                var activa = PickerCategoria.SelectedIndex == i;
                tarjeta.BackgroundColor = activa ? R("ColorPrimary") : Colors.White;
                tarjeta.Stroke = activa ? R("ColorPrimary") : Colors.Transparent;
                caja.BackgroundColor = activa ? Color.FromArgb("#33FFFFFF") : Color.FromArgb("#EAF1FB");
                icono.Fill = activa ? Colors.White : R("ColorPrimary");
                texto.TextColor = activa ? Colors.White : R("ColorTextStrong");
            }
        }

        private static Border? BordeDe(object? sender)
        {
            var e = (sender as Element)?.Parent;
            while (e != null && e is not Border) e = e.Parent;
            return e as Border;
        }

        private void OnCampoFocused(object? sender, FocusEventArgs e)
        {
            if (BordeDe(sender) is Border b) b.Stroke = (Color)Application.Current!.Resources["ColorPrimary"];
        }

        private void OnCampoUnfocused(object? sender, FocusEventArgs e)
        {
            if (BordeDe(sender) is Border b) b.Stroke = Colors.Transparent;
        }

        // Estado de envío: botón gris azulado con spinner en lugar del texto.
        private void MostrarEnviando(bool enviando)
        {
            BtnEnviar.IsEnabled = !enviando;
            BtnEnviar.Text = enviando ? "" : Localizador.T("enviar_reporte");
            BtnEnviar.BackgroundColor = enviando ? Color.FromArgb("#7F97BC") : (Color)Application.Current!.Resources["ColorPrimary"];
            SpinnerEnviar.IsVisible = SpinnerEnviar.IsRunning = enviando;
            SemanticProperties.SetDescription(SpinnerEnviar, Localizador.T("enviando"));
        }

        private async void OnEnviarClicked(object sender, EventArgs e)
        {
            if (PickerCategoria.SelectedIndex < 0)
            {
                await Alerta.MostrarAsync(Localizador.T("falta_informacion"), Localizador.T("selecciona_una_categoria_2"), Localizador.T("ok"));
                return;
            }

            if (string.IsNullOrWhiteSpace(EntryAsunto.Text) || string.IsNullOrWhiteSpace(EditorDescripcion.Text))
            {
                await Alerta.MostrarAsync(Localizador.T("falta_informacion"), Localizador.T("completa_el_asunto_y_la"), Localizador.T("ok"));
                return;
            }

            var categoria = CategoriasValor[PickerCategoria.SelectedIndex];
            var usuarioId = Preferences.Default.Get("UserId", 0);
            if (usuarioId == 0)
                return;

            MostrarEnviando(true);

            var request = new CrearTicketRequest
            {
                UsuarioId = usuarioId,
                Categoria = categoria,
                Asunto = EntryAsunto.Text.Trim(),
                Descripcion = EditorDescripcion.Text.Trim()
            };

            var ticketId = await _apiService.CrearTicketAsync(request);

            if (ticketId != null)
            {
                await Alerta.MostrarAsync(Localizador.T("reporte_enviado"), Localizador.T("nuestro_equipo_lo_revisara_pronto"), Localizador.T("ok"));
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_enviar_el"), Localizador.T("ok"));
                MostrarEnviando(false);
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
