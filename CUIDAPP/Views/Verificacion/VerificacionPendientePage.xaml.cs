using CUIDAPP.Models.Cuidador;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Verificacion
{
    public partial class VerificacionPendientePage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public VerificacionPendientePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarEstado(mostrarErrorSiFalla: false);
        }

        private async void OnActualizarTapped(object sender, EventArgs e)
        {
            await CargarEstado(mostrarErrorSiFalla: true);
        }

        private async void OnCerrarSesionTapped(object sender, EventArgs e)
        {
            Preferences.Default.Clear();
            await Shell.Current.GoToAsync("//MainPage");
        }

        private async Task CargarEstado(bool mostrarErrorSiFalla)
        {
            var userId = Preferences.Default.Get("UserId", 0);
            if (userId == 0)
            {
                await Shell.Current.GoToAsync("//MainPage");
                return;
            }

            BtnActualizar.IsEnabled = false;
            BtnActualizar.Text = "Consultando...";

            var estado = await _apiService.ObtenerEstadoVerificacionAsync(userId);

            BtnActualizar.IsEnabled = true;
            BtnActualizar.Text = "Actualizar estado";

            if (estado == null)
            {
                if (mostrarErrorSiFalla)
                    await DisplayAlert("Error", "No se pudo consultar el estado. Verifica tu conexión.", "OK");
                return;
            }

            // Si ya fue aprobado, mostrar la felicitación y pasar al dashboard.
            if (estado.EstadoAprobacion == 2)
            {
                Preferences.Default.Set("EstadoAprobacion", 2);
                await MostrarExitoYContinuar();
                return;
            }

            RenderizarEstado(estado);
        }

        private async Task MostrarExitoYContinuar()
        {
            OverlayExito.IsVisible = true;
            await OverlayExito.FadeTo(1, 250);

            CirculoExito.Scale = 0.5;
            await CirculoExito.ScaleTo(1.1, 300, Easing.SpringOut);
            await CirculoExito.ScaleTo(1.0, 120);

            await Task.Delay(1400);

            await Shell.Current.GoToAsync("CuidadorDashboardPage");
        }

        private void RenderizarEstado(EstadoVerificacion estado)
        {
            bool rechazado = estado.EstadoAprobacion == 3;

            LblTituloEstado.Text = rechazado ? "Documentos rechazados" : "Documentos en revisión";
            LblDescripcionEstado.Text = rechazado
                ? "Uno o más documentos fueron rechazados. Revisa las observaciones y vuelve a subirlos desde soporte."
                : "Estamos verificando tus documentos. Te avisaremos cuando tu cuenta esté aprobada para empezar a trabajar.";

            IconEstadoGeneral.Fill = rechazado ? Color.FromArgb("#DC2626") : Color.FromArgb("#4024A2");

            ListaDocumentos.Clear();

            if (estado.Documentos.Count == 0)
            {
                LblSinDocumentos.IsVisible = true;
                return;
            }

            LblSinDocumentos.IsVisible = false;

            foreach (var doc in estado.Documentos)
            {
                ListaDocumentos.Add(CrearTarjetaDocumento(doc));
            }
        }

        private static View CrearTarjetaDocumento(DocumentoEstado doc)
        {
            var (colorFondo, colorTexto, textoEstado) = doc.Estado switch
            {
                2 => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#166534"), "Aprobado"),
                3 => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#991B1B"), "Rechazado"),
                _ => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), "Pendiente")
            };

            var nombreDocumento = doc.TipoDocumento switch
            {
                "Cedula" => "Cédula de Identidad",
                "CartaAntecedentes" => "Carta de Antecedentes Penales",
                _ => doc.TipoDocumento
            };

            var badge = new Border
            {
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                BackgroundColor = colorFondo,
                Padding = new Thickness(12, 6),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = textoEstado,
                    TextColor = colorTexto,
                    FontFamily = "OpenSansSemibold",
                    FontSize = 12,
                    LineHeight = 1,
                    VerticalOptions = LayoutOptions.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };

            var contenido = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = nombreDocumento, FontFamily = "OpenSansSemibold", FontSize = 15, TextColor = Color.FromArgb("#111827") },
                    new Label { Text = $"Subido el {doc.FechaSubida:dd/MM/yyyy}", FontFamily = "OpenSansRegular", FontSize = 12, TextColor = Color.FromArgb("#6B7280") }
                }
            };

            if (doc.Estado == 3 && !string.IsNullOrWhiteSpace(doc.ObservacionesAdmin))
            {
                contenido.Children.Add(new Label
                {
                    Text = $"Motivo: {doc.ObservacionesAdmin}",
                    FontFamily = "OpenSansRegular",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#991B1B"),
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            contenido.VerticalOptions = LayoutOptions.Center;
            grid.Add(contenido, 0, 0);
            grid.Add(badge, 1, 0);

            return new Border
            {
                Stroke = Color.FromArgb("#E5E7EB"),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                BackgroundColor = Colors.White,
                Padding = new Thickness(15),
                Content = grid
            };
        }
    }
}
