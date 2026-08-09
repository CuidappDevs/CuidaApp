using CUIDAPP.Models.Cuidador;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Dinero
{
    public partial class DineroPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public DineroPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarDatos();
        }

        private async Task CargarDatos()
        {
            var cuidadorId = Preferences.Default.Get("UserId", 0);
            if (cuidadorId == 0)
                return;

            LoadingIndicator.IsRunning = true;

            var gananciasTask = _apiService.ObtenerGananciasAsync(cuidadorId);
            var pagosTask = _apiService.ObtenerPagosAsync(cuidadorId);
            await Task.WhenAll(gananciasTask, pagosTask);

            LoadingIndicator.IsRunning = false;

            var ganancias = gananciasTask.Result;
            LblTotalCobrado.Text = $"RD$ {(ganancias?.TotalCobrado ?? 0):N2}";
            LblPendiente.Text = $"RD$ {(ganancias?.PendientePorCobrar ?? 0):N2}";

            RenderizarPagos(pagosTask.Result);
        }

        private void RenderizarPagos(List<Pago> pagos)
        {
            ListaPagos.Clear();
            LblSinPagos.IsVisible = pagos.Count == 0;

            foreach (var pago in pagos)
            {
                ListaPagos.Add(CrearTarjetaPago(pago));
            }
        }

        private static View CrearTarjetaPago(Pago pago)
        {
            var esPagado = pago.Estado == 2;
            var colorMonto = esPagado ? Color.FromArgb("#059669") : Color.FromArgb("#D97706");
            var textoEstado = esPagado ? "Pagado" : "Pendiente";
            var fecha = esPagado && pago.FechaPago.HasValue ? pago.FechaPago.Value : pago.FechaCreacion;

            var icono = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb("#F3E8FF"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                WidthRequest = 45,
                HeightRequest = 45,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 15, 0),
                Content = new Label
                {
                    Text = esPagado ? "$" : "…",
                    FontSize = 18,
                    FontFamily = "OpenSansSemibold",
                    TextColor = Color.FromArgb("#5A31F4"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            var info = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = pago.TipoServicio, FontSize = 15, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#111827") },
                    new Label { Text = $"{pago.ClienteNombre} • {fecha:d MMM}", FontSize = 12, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#6B7280") }
                }
            };

            var montoStack = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = $"+ RD$ {pago.Monto:N2}", FontSize = 15, FontFamily = "OpenSansSemibold", TextColor = colorMonto, HorizontalTextAlignment = TextAlignment.End },
                    new Label { Text = textoEstado, FontSize = 11, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#9CA3AF"), HorizontalTextAlignment = TextAlignment.End }
                }
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            grid.Add(icono, 0, 0);
            grid.Add(info, 1, 0);
            grid.Add(montoStack, 2, 0);

            return new Border
            {
                Stroke = Color.FromArgb("#E5E7EB"),
                StrokeThickness = 1,
                BackgroundColor = Colors.White,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(15),
                Content = grid
            };
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnInicioTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorDashboardPage");
        }

        private async void OnTrabajosTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("TrabajosPage");
        }

        private async void OnPerfilTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorPerfilPage");
        }
    }
}
