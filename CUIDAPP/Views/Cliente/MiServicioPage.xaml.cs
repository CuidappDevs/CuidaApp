using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class MiServicioPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public MiServicioPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            RealtimeService.TrabajoActualizado += OnTrabajoActualizadoTiempoReal;
            await CargarServicios();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
        }

        private async void OnTrabajoActualizadoTiempoReal(int trabajoId, int estado)
        {
            await CargarServicios();
        }

        private async Task CargarServicios()
        {
            var clienteId = Preferences.Default.Get("UserId", 0);
            if (clienteId == 0)
                return;

            LoadingIndicator.IsRunning = true;
            ListaServicios.Clear();
            ContenedorVacio.IsVisible = false;

            var servicios = await _apiService.ObtenerTrabajosActivosPorClienteAsync(clienteId);

            LoadingIndicator.IsRunning = false;

            if (servicios.Count == 0)
            {
                ContenedorVacio.IsVisible = true;
                return;
            }

            // Con un solo servicio activo, saltamos directo al detalle (misma experiencia de antes).
            // Usamos "../DetalleServicioClientePage" (en vez de solo el nombre de ruta) para que
            // esta página se reemplace en la pila de navegación, no se apile debajo: si no,
            // el botón "atrás" del detalle regresa aquí y este OnAppearing vuelve a saltar
            // adelante de inmediato, dando la sensación de un loop/bug al usuario.
            if (servicios.Count == 1)
            {
                await Shell.Current.GoToAsync($"../DetalleServicioClientePage", new Dictionary<string, object> { { "TrabajoId", servicios[0].Id } });
                return;
            }

            foreach (var servicio in servicios)
                ListaServicios.Add(CrearTarjetaServicio(servicio));
        }

        private View CrearTarjetaServicio(TrabajoCliente t)
        {
            var (colorFondo, colorTexto, texto) = t.Estado switch
            {
                1 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), "Esperando respuesta"),
                2 => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#1E40AF"), "Aceptado"),
                3 => (Color.FromArgb("#EDE9FE"), Color.FromArgb("#5B21B6"), "En progreso"),
                4 => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#166534"), "Completado, ¡califica!"),
                _ => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), "En curso")
            };

            var badge = new Border
            {
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                BackgroundColor = colorFondo,
                Padding = new Thickness(10, 4),
                HorizontalOptions = LayoutOptions.Start,
                Content = new Label { Text = texto, FontSize = 11, FontFamily = "OpenSansSemibold", TextColor = colorTexto }
            };

            var foto = new Border
            {
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 27 },
                BackgroundColor = Color.FromArgb("#E5E7EB"),
                WidthRequest = 54,
                HeightRequest = 54,
                Margin = new Thickness(0, 0, 14, 0),
                Content = new Image
                {
                    Aspect = Aspect.AspectFill,
                    Source = string.IsNullOrWhiteSpace(t.CuidadorFotoUrl) ? null : $"{ApiService.ServerOrigin}{t.CuidadorFotoUrl}"
                }
            };

            var contenido = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    badge,
                    new Label { Text = t.CuidadorNombre, FontSize = 16, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#111827") },
                    new Label { Text = t.TipoServicio, FontSize = 13, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#6B7280") }
                }
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
            grid.Add(foto, 0, 0);
            grid.Add(contenido, 1, 0);

            var card = new Border
            {
                Stroke = Color.FromArgb("#E5E7EB"),
                StrokeThickness = 1,
                BackgroundColor = Colors.White,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(16),
                Content = grid
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) => await Shell.Current.GoToAsync("DetalleServicioClientePage", new Dictionary<string, object> { { "TrabajoId", t.Id } });
            card.GestureRecognizers.Add(tap);

            return card;
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
