using CUIDAPP.Models.Busqueda;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    [QueryProperty(nameof(Especialidad), "Especialidad")]
    [QueryProperty(nameof(Latitud), "Latitud")]
    [QueryProperty(nameof(Longitud), "Longitud")]
    public partial class CuidadoresPorServicioPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private bool estaVisible;
        private bool primeraCarga = true;

        public string Especialidad { get; set; } = string.Empty;
        public double Latitud { get; set; }
        public double Longitud { get; set; }

        public CuidadoresPorServicioPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            LblTituloServicio.Text = Especialidad;
            estaVisible = true;

            await CargarCuidadores();

            // Mientras el cliente esté viendo esta lista, la refrescamos periódicamente
            // para reflejar en "tiempo real" cuando un cuidador se conecta/desconecta.
            Dispatcher.StartTimer(TimeSpan.FromSeconds(8), () =>
            {
                if (!estaVisible)
                    return false;

                _ = CargarCuidadores();
                return true;
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            estaVisible = false;
        }

        private async Task CargarCuidadores()
        {
            // Solo mostramos el spinner grande en la primera carga; los refrescos
            // automáticos posteriores actualizan la lista en silencio, sin parpadeos.
            if (primeraCarga)
            {
                LoadingIndicator.IsRunning = true;
                primeraCarga = false;
            }

            var cuidadores = await _apiService.ObtenerCuidadoresPorServicioAsync(Especialidad, Latitud, Longitud);

            LoadingIndicator.IsRunning = false;
            ListaCuidadores.Clear();
            LblSinCuidadores.IsVisible = cuidadores.Count == 0;

            foreach (var cuidador in cuidadores)
            {
                ListaCuidadores.Add(CrearTarjetaCuidador(cuidador));
            }
        }

        private View CrearTarjetaCuidador(CuidadorCercano cuidador)
        {
            var foto = new Border
            {
                Stroke = Color.FromArgb("#E5E7EB"),
                StrokeThickness = 1,
                BackgroundColor = Color.FromArgb("#F3F4F6"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 30 },
                WidthRequest = 60,
                HeightRequest = 60,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 15, 0)
            };

            if (!string.IsNullOrWhiteSpace(cuidador.FotoUrl))
            {
                foto.Content = new Image { Source = $"{ApiService.ServerOrigin}{cuidador.FotoUrl}", Aspect = Aspect.AspectFill };
            }

            var info = new VerticalStackLayout
            {
                Spacing = 3,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = cuidador.NombreCompleto, FontSize = 16, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#111827") },
                    new Label { Text = $"A {cuidador.DistanciaKm:N1} km de ti", FontSize = 12, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#6B7280") },
                    new Label { Text = $"RD${cuidador.TarifaHora:N0} / hora", FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#5A31F4") }
                }
            };

            var chevron = new Label
            {
                Text = "›",
                FontSize = 24,
                TextColor = Color.FromArgb("#9CA3AF"),
                VerticalOptions = LayoutOptions.Center
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            grid.Add(foto, 0, 0);
            grid.Add(info, 1, 0);
            grid.Add(chevron, 2, 0);

            var card = new Border
            {
                Stroke = Color.FromArgb("#E5E7EB"),
                StrokeThickness = 1,
                BackgroundColor = Colors.White,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(15),
                Content = grid
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) => await AbrirDetalle(cuidador);
            card.GestureRecognizers.Add(tap);

            return card;
        }

        private async Task AbrirDetalle(CuidadorCercano cuidador)
        {
            try
            {
                var parametros = new Dictionary<string, object> { { "Cuidador", cuidador } };
                await Shell.Current.GoToAsync("CuidadorDetallePage", parametros);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error al abrir el perfil", ex.ToString(), "OK");
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
