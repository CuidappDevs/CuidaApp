using CUIDAPP.Models;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class NotificacionesPage : ContentPage
    {
        public NotificacionesPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            RenderizarLista();
            NotificacionHistorial.MarcarTodasLeidas();
        }

        private void RenderizarLista()
        {
            var notificaciones = NotificacionHistorial.Obtener();

            ListaNotificaciones.Clear();
            EstadoVacio.IsVisible = notificaciones.Count == 0;

            foreach (var n in notificaciones)
                ListaNotificaciones.Add(CrearTarjeta(n));
        }

        private View CrearTarjeta(Notificacion n)
        {
            var (colorFondo, icono, colorIcono) = n.Tipo switch
            {
                "mensaje" => (Color.FromArgb("#EFF6FF"), "💬", Color.FromArgb("#2563EB")),
                "solicitud" => (Color.FromArgb("#FEF3C7"), "📋", Color.FromArgb("#92400E")),
                "trabajo" => (Color.FromArgb("#DCFCE7"), "🔔", Color.FromArgb("#166534")),
                "geocerca" => (Color.FromArgb("#FEE2E2"), "⚠️", Color.FromArgb("#991B1B")),
                _ => (Color.FromArgb("#F3F4F6"), "🔔", Color.FromArgb("#374151"))
            };

            var iconoBorde = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = colorFondo,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                WidthRequest = 40,
                HeightRequest = 40,
                Content = new Label { Text = icono, FontSize = 16, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
            };

            var contenido = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label { Text = n.Titulo, FontSize = 14, FontFamily = n.Leida ? "OpenSansRegular" : "OpenSansSemibold", TextColor = Color.FromArgb("#111827") },
                    new Label { Text = n.Mensaje, FontSize = 13, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#6B7280") },
                    new Label { Text = n.Fecha.ToString("d MMM, h:mm tt"), FontSize = 11, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#9CA3AF") }
                }
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 12 };
            grid.Add(iconoBorde, 0, 0);
            grid.Add(contenido, 1, 0);

            var card = new Border
            {
                Stroke = Color.FromArgb("#E5E7EB"),
                StrokeThickness = 1,
                BackgroundColor = n.Leida ? Colors.White : Color.FromArgb("#F8FAFF"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(14, 12),
                Content = grid
            };

            if (n.TrabajoId.HasValue)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (s, e) => await IrAlTrabajoAsync(n.TrabajoId.Value);
                card.GestureRecognizers.Add(tap);
            }

            return card;
        }

        private async Task IrAlTrabajoAsync(int trabajoId)
        {
            var rolId = Preferences.Default.Get("RolId", 0);
            await Navigation.PopModalAsync();

            if (rolId == 3) // Cuidador
                await Shell.Current.GoToAsync("TrabajosPage");
            else // Cliente
                await Shell.Current.GoToAsync("MiServicioPage");
        }

        private async void OnCerrarTapped(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}
