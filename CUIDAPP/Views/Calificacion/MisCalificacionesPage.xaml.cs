using CUIDAPP.Localization;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Calificacion
{
    public partial class MisCalificacionesPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public MisCalificacionesPage()
        {
            InitializeComponent();
            // Borde a borde: el encabezado empieza debajo de la barra de estado.
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
        }

        // Entrada: el contenido aparece subiendo con fade.
        private void Revelar(VisualElement v)
        {
            v.Opacity = 0;
            v.TranslationY = 16;
            _ = v.FadeTo(1, 300, Easing.CubicOut);
            _ = v.TranslateTo(0, 0, 360, Easing.CubicOut);
        }


        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            await CargarCalificacionesAsync();
        }

        private async Task CargarCalificacionesAsync()
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            ListaCalificaciones.IsVisible = false;
            PanelVacio.IsVisible = false;
            PanelResumen.IsVisible = false;

            var usuarioId = Preferences.Default.Get("UserId", 0);
            if (usuarioId == 0)
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                PanelVacio.IsVisible = true;
                return;
            }

            var calificaciones = await _apiService.ObtenerCalificacionesDeUsuarioAsync(usuarioId);

            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;

            if (calificaciones.Count == 0)
            {
                PanelVacio.IsVisible = true;
                Revelar(PanelVacio);
                return;
            }

            var promedio = calificaciones.Average(c => c.Puntuacion);
            LblPromedio.Text = promedio.ToString("0.0");
            LblTotal.Text = calificaciones.Count == 1 ? Localizador.T("basado_en_1_calificacion") : Localizador.F("basado_en_n_calificaciones", calificaciones.Count);
            PanelResumen.IsVisible = true;
            Revelar(PanelResumen);

            ListaCalificaciones.ItemsSource = calificaciones;
            ListaCalificaciones.IsVisible = true;
            Revelar(ListaCalificaciones);
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
