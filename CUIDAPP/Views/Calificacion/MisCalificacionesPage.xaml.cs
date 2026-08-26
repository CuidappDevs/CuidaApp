using CUIDAPP.Services;

namespace CUIDAPP.Views.Calificacion
{
    public partial class MisCalificacionesPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public MisCalificacionesPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
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
                return;
            }

            var promedio = calificaciones.Average(c => c.Puntuacion);
            LblPromedio.Text = promedio.ToString("0.0");
            LblTotal.Text = $"Basado en {calificaciones.Count} calificación{(calificaciones.Count == 1 ? "" : "es")}";
            PanelResumen.IsVisible = true;

            ListaCalificaciones.ItemsSource = calificaciones;
            ListaCalificaciones.IsVisible = true;
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
