using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using Microsoft.Maui.Controls.Shapes;

namespace CUIDAPP.Views.Splash
{
    public partial class SplashPage : ContentPage
    {
        private readonly Action _alTerminar;

        public SplashPage(Action alTerminar)
        {
            InitializeComponent();
            _alTerminar = alTerminar;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();

            var version = AppInfo.Current.VersionString;
            var build = AppInfo.Current.BuildString;
            LblVersion.Text = Localizador.F("version", version, build);

            LblTagline.TranslationY = 8;

            await Task.WhenAll(
                LogoCircle.FadeTo(1, 450, Easing.CubicOut),
                LogoCircle.ScaleTo(1, 500, Easing.CubicOut)
            );

            await Task.WhenAll(
                LblTagline.FadeTo(1, 350, Easing.CubicOut),
                LblTagline.TranslateTo(0, 0, 350, Easing.CubicOut)
            );
            await DotsPanel.FadeTo(1, 250, Easing.CubicOut);
            _ = LblVersion.FadeTo(1, 500, Easing.CubicOut);

            await AnimarPuntosCargandoAsync();

            // Salida: el contenido se desvanece y queda solo el fondo azul,
            // que continúa en el encabezado del login.
            await Task.WhenAll(
                LogoCircle.FadeTo(0, 220, Easing.CubicOut),
                LogoCircle.ScaleTo(0.96, 220, Easing.CubicOut),
                LblTagline.FadeTo(0, 180, Easing.CubicOut),
                DotsPanel.FadeTo(0, 180, Easing.CubicOut),
                LblVersion.FadeTo(0, 180, Easing.CubicOut)
            );

            _alTerminar?.Invoke();
        }

        private async Task AnimarPuntosCargandoAsync()
        {
            var puntos = new[] { Dot1, Dot2, Dot3 };

            for (int ciclo = 0; ciclo < 3; ciclo++)
            {
                foreach (var punto in puntos)
                {
                    _ = PulsarPuntoAsync(punto);
                    await Task.Delay(150);
                }
                await Task.Delay(150);
            }
        }

        private static async Task PulsarPuntoAsync(Ellipse punto)
        {
            await Task.WhenAll(punto.ScaleTo(1.4, 200, Easing.CubicOut), punto.FadeTo(1, 200, Easing.CubicOut));
            await Task.WhenAll(punto.ScaleTo(1, 220, Easing.CubicInOut), punto.FadeTo(0.6, 220, Easing.CubicInOut));
        }
    }
}
