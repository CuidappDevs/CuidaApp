using CUIDAPP.Localization;
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
            BarraEstado.Blanca();
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
            LblTotalCobrado.Text = Localizador.F("rd_3", (ganancias?.TotalCobrado ?? 0));
            LblPendiente.Text = Localizador.F("rd_3", (ganancias?.PendientePorCobrar ?? 0));

            RenderizarPagos(pagosTask.Result);
        }

        private void RenderizarPagos(List<Pago> pagos)
        {
            ListaPagos.Clear();
            LblSinPagos.IsVisible = pagos.Count == 0;

            var indice = 0;
            foreach (var pago in pagos)
            {
                var tarjeta = CrearTarjetaPago(pago);
                tarjeta.Opacity = 0;
                tarjeta.TranslationY = 12;
                ListaPagos.Add(tarjeta);
                var retraso = Math.Min(indice++, 8) * 45;
                _ = Task.Delay(retraso).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
                {
                    tarjeta.FadeTo(1, 260, Easing.CubicOut);
                    tarjeta.TranslateTo(0, 0, 320, Easing.CubicOut);
                }));
            }
        }

        private static View CrearTarjetaPago(Pago pago)
        {
            var esPagado = pago.Estado == 2;
            var colorMonto = esPagado ? Tema.C("ColorSuccess") : Tema.C("ColorWarning");
            var textoEstado = esPagado ? Localizador.T("pagado") : Localizador.T("pendiente");
            var fecha = esPagado && pago.FechaPago.HasValue ? pago.FechaPago.Value : pago.FechaCreacion;

            // Pagado: check verde. Pendiente: reloj ámbar.
            var icono = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = esPagado ? Tema.C("ColorSuccessSoft") : Tema.C("ColorWarningSoft"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                WidthRequest = 46,
                HeightRequest = 46,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 14, 0),
                Content = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(esPagado
                        ? "M9 16.17L4.83 12L3.41 13.41L9 19L21 7L19.59 5.59L9 16.17Z"
                        : "M11.99 2C6.47 2 2 6.48 2 12C2 17.52 6.47 22 11.99 22C17.52 22 22 17.52 22 12C22 6.48 17.52 2 11.99 2ZM12 20C7.58 20 4 16.42 4 12C4 7.58 7.58 4 12 4C16.42 4 20 7.58 20 12C20 16.42 16.42 20 12 20ZM12.5 7H11V13L16.25 16.15L17 14.92L12.5 12.25V7Z")!,
                    Fill = esPagado ? Tema.C("ColorSuccess") : Color.FromArgb("#E09A1A"),
                    Aspect = Stretch.Uniform, WidthRequest = 20, HeightRequest = 20,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                }
            };

            var info = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = Localizador.D(pago.TipoServicio), FontSize = 15, FontFamily = "OpenSansSemibold", TextColor = (Color)Application.Current!.Resources["ColorTextStrong"] },
                    new Label { Text = $"{pago.ClienteNombre} • {fecha:d MMM}", FontSize = 12, FontFamily = "OpenSansRegular", TextColor = (Color)Application.Current!.Resources["ColorTextMuted"] }
                }
            };

            var montoStack = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = Localizador.F("rd_4", pago.Total), FontSize = 15, FontFamily = "OpenSansSemibold", TextColor = colorMonto, HorizontalTextAlignment = TextAlignment.End },
                    new Label { Text = textoEstado, FontSize = 11, FontFamily = "OpenSansRegular", TextColor = (Color)Application.Current!.Resources["ColorTextMuted"], HorizontalTextAlignment = TextAlignment.End }
                }
            };

            if (pago.Propina > 0)
            {
                montoStack.Children.Insert(1, new Label
                {
                    Text = Localizador.F("incluye_propina", pago.Propina),
                    FontSize = 11,
                    FontFamily = "OpenSansSemibold",
                    TextColor = Tema.C("ColorSuccess"),
                    HorizontalTextAlignment = TextAlignment.End
                });
            }

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            grid.Add(icono, 0, 0);
            grid.Add(info, 1, 0);
            grid.Add(montoStack, 2, 0);

            return new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Tema.C("ColorSurface"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                Padding = new Thickness(14),
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
