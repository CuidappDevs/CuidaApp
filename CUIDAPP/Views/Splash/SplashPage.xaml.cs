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

            var version = AppInfo.Current.VersionString;
            var build = AppInfo.Current.BuildString;
            LblVersion.Text = $"Versión {version} ({build})";

            await Task.WhenAll(
                LogoCircle.FadeTo(1, 550, Easing.CubicOut),
                LogoCircle.ScaleTo(1, 550, Easing.SpringOut)
            );

            await LblTagline.FadeTo(1, 400, Easing.CubicOut);
            await DotsPanel.FadeTo(1, 300, Easing.CubicOut);
            _ = LblVersion.FadeTo(1, 500, Easing.CubicOut);

            await AnimarPuntosCargandoAsync();

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
            await punto.ScaleTo(1.5, 200, Easing.CubicOut);
            await punto.ScaleTo(1, 200, Easing.CubicIn);
        }
    }
}
