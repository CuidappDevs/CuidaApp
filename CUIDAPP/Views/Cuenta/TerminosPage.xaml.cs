using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cuenta
{
    /// <summary>Términos y Condiciones para volver a leerlos (o escucharlos) desde el perfil.</summary>
    public partial class TerminosPage : ContentPage
    {
        private readonly ReproductorTerminos lector = new() { Margin = new Thickness(20, 6, 20, 0) };
        private List<Border> partes = new();

        public TerminosPage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            LblTitulo.Text = Localizador.T("tyc_titulo");
            LblSubtitulo.Text = Localizador.F("tyc_version", Terminos.Version);
            Contenido.Spacing = 26;

            // Fijo en el encabezado: los controles quedan a mano mientras la página sigue la lectura.
            ContenidoEncabezado.Add(lector);
            lector.ParteCambiada += i => MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (i < 0 || i >= partes.Count) return;
                Terminos.Resaltar(partes, i);
                await Desplazamiento.ScrollToAsync(partes[i], ScrollToPosition.Start, true);
            });
            lector.Detenido += () => MainThread.BeginInvokeOnMainThread(() => Terminos.Resaltar(partes, -1));
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (Contenido.Children.Count > 0) return;
            await Task.Yield(); // primero se pinta el encabezado; el documento es largo
            partes = Terminos.Construir(Contenido);
            Contenido.Add(new ContentView { HeightRequest = 24 + BarraEstado.AltoInferior() });
            Cargando.IsVisible = Cargando.IsRunning = false;
            Desplazamiento.IsVisible = true;
            await lector.PrepararAsync(Terminos.Segmentos());
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            lector.Detener();
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
