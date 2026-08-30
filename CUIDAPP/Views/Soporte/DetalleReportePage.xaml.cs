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

            var miUsuarioId = Preferences.Default.Get("UserId", 0);
            PanelMensajes.Children.Clear();
            foreach (var mensaje in detalle.Mensajes)
                PanelMensajes.Children.Add(CrearBurbuja(mensaje, esMio: mensaje.AutorId == miUsuarioId && !mensaje.EsAdmin));

            ScrollMensajes.IsVisible = true;
        }

        private static View CrearBurbuja(TicketMensaje mensaje, bool esMio)
        {
            var burbuja = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = esMio ? Color.FromArgb("#2563EB") : (mensaje.EsAdmin ? Color.FromArgb("#EFF6FF") : Colors.White),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(14, 10),
                HorizontalOptions = esMio ? LayoutOptions.End : LayoutOptions.Start,
                MaximumWidthRequest = 280
            };

            var contenido = new VerticalStackLayout { Spacing = 3 };

            if (mensaje.EsAdmin)
            {
                contenido.Children.Add(new Label
                {
                    Text = "Soporte Cuidapp",
                    FontFamily = "OpenSansSemibold",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#2563EB")
                });
            }

            contenido.Children.Add(new Label
            {
                Text = mensaje.Mensaje,
                FontFamily = "OpenSansRegular",
                FontSize = 14,
                TextColor = esMio ? Colors.White : Color.FromArgb("#111827")
            });

            contenido.Children.Add(new Label
            {
                Text = mensaje.FechaCreacion.ToString("dd MMM, HH:mm"),
                FontFamily = "OpenSansRegular",
                FontSize = 10,
                TextColor = esMio ? Color.FromArgb("#DBEAFE") : Color.FromArgb("#9CA3AF")
            });

            burbuja.Content = contenido;
            return burbuja;
        }

        private async void OnEnviarMensajeTapped(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EntryMensaje.Text))
                return;

            var usuarioId = Preferences.Default.Get("UserId", 0);
            var texto = EntryMensaje.Text.Trim();
            EntryMensaje.Text = "";

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
