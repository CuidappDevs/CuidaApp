using CUIDAPP.Localization;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CUIDAPP.Helpers
{
    /// <summary>Avisos de la deuda del cliente: tarjeta en su inicio y alerta cuando el servidor bloquea una solicitud.</summary>
    public static class DeudaAviso
    {
        /// <summary>Muestra (o quita) la tarjeta "Tienes un saldo pendiente" en el inicio del cliente.</summary>
        public static async Task CargarBannerAsync(VerticalStackLayout contenedor)
        {
            var clienteId = Preferences.Default.Get("UserId", 0);
            if (clienteId == 0) return;
            var resumen = await new ApiService().ObtenerDeudasClienteAsync(clienteId);

            contenedor.Clear();
            contenedor.IsVisible = resumen is { Bloqueado: true };
            if (resumen is not { Bloqueado: true }) return;

            var icono = new Border
            {
                WidthRequest = 40, HeightRequest = 40, StrokeThickness = 0, BackgroundColor = Tema.C("ColorDanger"),
                StrokeShape = new RoundRectangle { CornerRadius = 12 }, VerticalOptions = LayoutOptions.Center,
                Content = new Label { Text = "!", FontSize = 20, FontFamily = "OpenSansSemibold", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
            };
            var textos = new VerticalStackLayout
            {
                Spacing = 2, VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = Localizador.F("deuda_banner_titulo", Localizador.F("rd_monto", resumen.TotalPendiente)), FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong") },
                    new Label { Text = Localizador.T("deuda_banner_texto"), FontSize = 12, FontFamily = "OpenSansRegular", TextColor = Tema.C("ColorTextMuted") }
                }
            };
            var flecha = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M10 6L8.59 7.41L13.17 12L8.59 16.59L10 18L16 12L10 6Z")!,
                Fill = Tema.C("ColorDanger"), Aspect = Stretch.Uniform, WidthRequest = 18, HeightRequest = 18, VerticalOptions = LayoutOptions.Center
            };
            var fila = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
            fila.Add(icono, 0);
            fila.Add(textos, 1);
            fila.Add(flecha, 2);

            var tarjeta = new Border
            {
                Stroke = Tema.C("ColorDanger"), StrokeThickness = 1, BackgroundColor = Tema.C("ColorDangerSoft"),
                StrokeShape = new RoundRectangle { CornerRadius = 18 }, Padding = new Thickness(14), Content = fila
            };
            SemanticProperties.SetDescription(tarjeta, Localizador.T("deuda_ver"));
            tarjeta.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    await tarjeta.ScaleTo(0.98, 80, Easing.CubicOut);
                    _ = tarjeta.ScaleTo(1, 140, Easing.CubicOut);
                    await Shell.Current.GoToAsync("DeudasClientePage");
                })
            });
            contenedor.Add(tarjeta);
        }

        /// <summary>El servidor rechazó la solicitud por deuda: explica y ofrece ir a pagarla.</summary>
        public static async Task OfrecerAsync()
        {
            if (await Alerta.MostrarAsync(Localizador.T("deuda_bloqueo_titulo"), Localizador.T("deuda_bloqueo_texto"),
                    Localizador.T("deuda_ver"), Localizador.T("cerrar"), TipoAlerta.Advertencia))
                await Shell.Current.GoToAsync("DeudasClientePage");
        }
    }
}
