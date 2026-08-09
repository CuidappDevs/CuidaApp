using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Trabajos
{
    public partial class TrabajosPage : ContentPage
    {
        private enum Tab { Nuevos, Aceptados, Historial }

        private readonly ApiService _apiService = new ApiService();
        private List<Trabajo> todosLosTrabajos = new();
        private Tab tabActual = Tab.Nuevos;

        public TrabajosPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarTrabajos();
        }

        private async Task CargarTrabajos()
        {
            var cuidadorId = Preferences.Default.Get("UserId", 0);
            if (cuidadorId == 0)
                return;

            LoadingIndicator.IsRunning = true;
            ListaTrabajos.IsVisible = false;

            todosLosTrabajos = await _apiService.ObtenerTrabajosAsync(cuidadorId);

            LoadingIndicator.IsRunning = false;
            ListaTrabajos.IsVisible = true;

            RenderizarTab();
        }

        private void OnTabNuevosTapped(object sender, EventArgs e) => CambiarTab(Tab.Nuevos);
        private void OnTabAceptadosTapped(object sender, EventArgs e) => CambiarTab(Tab.Aceptados);
        private void OnTabHistorialTapped(object sender, EventArgs e) => CambiarTab(Tab.Historial);

        private void CambiarTab(Tab nuevaTab)
        {
            tabActual = nuevaTab;

            var activo = Color.FromArgb("#4024A2");
            var inactivo = Color.FromArgb("#6B7280");

            LblTabNuevos.TextColor = nuevaTab == Tab.Nuevos ? activo : inactivo;
            LblTabNuevos.FontFamily = nuevaTab == Tab.Nuevos ? "OpenSansSemibold" : "OpenSansRegular";
            IndicadorTabNuevos.Color = nuevaTab == Tab.Nuevos ? activo : Colors.Transparent;

            LblTabAceptados.TextColor = nuevaTab == Tab.Aceptados ? activo : inactivo;
            LblTabAceptados.FontFamily = nuevaTab == Tab.Aceptados ? "OpenSansSemibold" : "OpenSansRegular";
            IndicadorTabAceptados.Color = nuevaTab == Tab.Aceptados ? activo : Colors.Transparent;

            LblTabHistorial.TextColor = nuevaTab == Tab.Historial ? activo : inactivo;
            LblTabHistorial.FontFamily = nuevaTab == Tab.Historial ? "OpenSansSemibold" : "OpenSansRegular";
            IndicadorTabHistorial.Color = nuevaTab == Tab.Historial ? activo : Colors.Transparent;

            RenderizarTab();
        }

        private void RenderizarTab()
        {
            ListaTrabajos.Clear();

            var filtrados = tabActual switch
            {
                Tab.Nuevos => todosLosTrabajos.Where(t => t.Estado == 1),
                Tab.Aceptados => todosLosTrabajos.Where(t => t.Estado == 2 || t.Estado == 3),
                Tab.Historial => todosLosTrabajos.Where(t => t.Estado == 4 || t.Estado == 5 || t.Estado == 6),
                _ => Enumerable.Empty<Trabajo>()
            };

            var lista = filtrados.OrderBy(t => t.Fecha).ThenBy(t => t.HoraInicio).ToList();

            LblSinTrabajosTab.IsVisible = lista.Count == 0;

            foreach (var trabajo in lista)
            {
                ListaTrabajos.Add(CrearTarjetaTrabajo(trabajo));
            }
        }

        private View CrearTarjetaTrabajo(Trabajo trabajo)
        {
            var (colorFondo, colorTexto, textoEstado) = trabajo.Estado switch
            {
                1 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), "Pendiente"),
                2 => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#1E40AF"), "Aceptado"),
                3 => (Color.FromArgb("#EDE9FE"), Color.FromArgb("#5B21B6"), "En progreso"),
                4 => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#166534"), "Completado"),
                5 => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), "Cancelado"),
                6 => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#991B1B"), "Rechazado"),
                _ => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), "Desconocido")
            };

            var badge = new Border
            {
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                BackgroundColor = colorFondo,
                Padding = new Thickness(10, 4),
                HorizontalOptions = LayoutOptions.Start,
                Content = new Label { Text = textoEstado, FontSize = 11, FontFamily = "OpenSansSemibold", TextColor = colorTexto }
            };

            var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
            headerGrid.Add(badge, 0, 0);
            headerGrid.Add(new Label
            {
                Text = trabajo.Fecha.ToString("d MMM"),
                FontSize = 12,
                FontFamily = "OpenSansSemibold",
                TextColor = Color.FromArgb("#374151"),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);

            var contenido = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    headerGrid,
                    new Label { Text = trabajo.TipoServicio, FontSize = 18, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#111827") },
                    new Label { Text = trabajo.ClienteNombre, FontSize = 14, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#4B5563") }
                }
            };

            var footerGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, Margin = new Thickness(0, 5, 0, 0) };
            footerGrid.Add(new Label
            {
                Text = $"{FormatearHora(trabajo.HoraInicio)} - {FormatearHora(trabajo.HoraFin)}",
                FontSize = 14,
                FontFamily = "OpenSansRegular",
                TextColor = Color.FromArgb("#4B5563"),
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            footerGrid.Add(new Label
            {
                Text = $"RD${trabajo.Tarifa:N0}",
                FontSize = 16,
                FontFamily = "OpenSansSemibold",
                TextColor = Color.FromArgb("#111827"),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);
            contenido.Children.Add(footerGrid);

            var card = new Border
            {
                Stroke = Color.FromArgb("#E5E7EB"),
                StrokeThickness = 1,
                BackgroundColor = Colors.White,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(20),
                Content = contenido
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) => await AbrirDetalle(trabajo);
            card.GestureRecognizers.Add(tap);

            return card;
        }

        private static string FormatearHora(TimeSpan hora)
        {
            return DateTime.Today.Add(hora).ToString("h:mm tt");
        }

        private async Task AbrirDetalle(Trabajo trabajo)
        {
            var parametros = new Dictionary<string, object> { { "Trabajo", trabajo } };
            await Shell.Current.GoToAsync("DetalleTrabajoPage", parametros);
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnInicioTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorDashboardPage");
        }

        private async void OnPerfilTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorPerfilPage");
        }

        private async void OnDineroTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("DineroPage");
        }
    }
}
