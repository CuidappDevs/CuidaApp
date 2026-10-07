using CUIDAPP.Localization;
using CUIDAPP.Models.Ticket;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Soporte
{
    [QueryProperty(nameof(TicketId), "TicketId")]
    public partial class DetalleReportePage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public int TicketId { get; set; }

        public DetalleReportePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Blanca();
            await CargarDetalleAsync();
        }

        private async Task CargarDetalleAsync()
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            ScrollMensajes.IsVisible = false;

            var detalle = await _apiService.ObtenerDetalleTicketAsync(TicketId);

            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;

            if (detalle == null)
                return;

            LblAsunto.Text = detalle.Ticket.Asunto;
            LblEstado.Text = detalle.Ticket.EstadoTexto;
            ChipEstado.IsVisible = !string.IsNullOrWhiteSpace(detalle.Ticket.EstadoTexto);

            var miUsuarioId = Preferences.Default.Get("UserId", 0);
            PanelMensajes.Children.Clear();
            foreach (var mensaje in detalle.Mensajes)
                PanelMensajes.Children.Add(CrearBurbuja(mensaje, esMio: mensaje.AutorId == miUsuarioId && !mensaje.EsAdmin));

            ScrollMensajes.IsVisible = true;
            Dispatcher.Dispatch(async () => await ScrollMensajes.ScrollToAsync(0, PanelMensajes.Height, false));
        }

        private static View CrearBurbuja(TicketMensaje mensaje, bool esMio)
        {
            Color R(string k) => (Color)Application.Current!.Resources[k];

            // Burbuja con "cola" del lado de quien escribe
            var burbuja = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = esMio ? R("ColorPrimary") : Tema.C("ColorSurface"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                {
                    CornerRadius = esMio ? new CornerRadius(20, 20, 20, 6) : new CornerRadius(20, 20, 6, 20)
                },
                Padding = new Thickness(14, 10),
                MaximumWidthRequest = 270,
                Shadow = esMio ? null! : new Shadow { Brush = Tema.C("ColorShadow"), Offset = new Point(0, 2), Radius = 6, Opacity = 0.05f }
            };

            var contenido = new VerticalStackLayout { Spacing = 4 };

            if (mensaje.EsAdmin)
            {
                contenido.Children.Add(new Label
                {
                    Text = Localizador.T("soporte_cuidapp"),
                    FontFamily = "OpenSansSemibold",
                    FontSize = 11,
                    TextColor = R("ColorPrimary")
                });
            }

            contenido.Children.Add(new Label
            {
                Text = mensaje.Mensaje,
                FontFamily = "OpenSansRegular",
                FontSize = 14,
                TextColor = esMio ? Colors.White : R("ColorTextStrong")
            });

            contenido.Children.Add(new Label
            {
                Text = mensaje.FechaCreacion.ToString("dd MMM, HH:mm"),
                FontFamily = "OpenSansRegular",
                FontSize = 10,
                TextColor = esMio ? Color.FromArgb("#C9D9F0") : R("ColorTextMuted"),
                HorizontalOptions = LayoutOptions.End
            });

            burbuja.Content = contenido;

            if (esMio)
            {
                burbuja.HorizontalOptions = LayoutOptions.End;
                return burbuja;
            }

            // Respuestas del equipo de soporte: con el ícono de auriculares al lado
            var fila = new Grid { ColumnSpacing = 8, HorizontalOptions = LayoutOptions.Start, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) } };
            if (mensaje.EsAdmin)
            {
                fila.Add(new Border
                {
                    Stroke = Colors.Transparent,
                    BackgroundColor = Tema.C("ColorPrimarySoft"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    WidthRequest = 30,
                    HeightRequest = 30,
                    VerticalOptions = LayoutOptions.End,
                    Content = new Microsoft.Maui.Controls.Shapes.Path
                    {
                        Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString("M12 1C7.03 1 3 5.03 3 10V17C3 18.66 4.34 20 6 20H9V12H5V10C5 6.13 8.13 3 12 3C15.87 3 19 6.13 19 10V12H15V20H19V21H12V23H18C19.66 23 21 21.66 21 20V10C21 5.03 16.97 1 12 1Z")!,
                        Fill = R("ColorPrimary"), Aspect = Stretch.Uniform, WidthRequest = 14, HeightRequest = 14,
                        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                    }
                }, 0);
            }
            fila.Add(burbuja, 1);
            return fila;
        }

        private void OnMensajeFocused(object? sender, FocusEventArgs e)
        {
            ContenedorEntry.Stroke = (Color)Application.Current!.Resources["ColorPrimary"];
            ContenedorEntry.BackgroundColor = Tema.C("ColorSurface");
        }

        private void OnMensajeUnfocused(object? sender, FocusEventArgs e)
        {
            ContenedorEntry.Stroke = Colors.Transparent;
            ContenedorEntry.BackgroundColor = (Color)Application.Current!.Resources["ColorBackground"];
        }

        private async void OnEnviarMensajeTapped(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EntryMensaje.Text))
                return;

            var usuarioId = Preferences.Default.Get("UserId", 0);
            var texto = EntryMensaje.Text.Trim();
            EntryMensaje.Text = "";
            _ = BtnEnviarCirculo.ScaleTo(0.88, 80, Easing.CubicOut).ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(() => BtnEnviarCirculo.ScaleTo(1, 160, Easing.CubicOut)));

            var success = await _apiService.AgregarMensajeTicketAsync(TicketId, usuarioId, texto);
            if (success)
                await CargarDetalleAsync();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
