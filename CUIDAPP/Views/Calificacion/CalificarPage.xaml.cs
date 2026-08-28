using CUIDAPP.Models.Calificacion;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Calificacion
{
    [QueryProperty(nameof(TrabajoId), "TrabajoId")]
    [QueryProperty(nameof(CalificadoId), "CalificadoId")]
    [QueryProperty(nameof(CalificadoNombre), "CalificadoNombre")]
    [QueryProperty(nameof(RutaSalida), "RutaSalida")]
    public partial class CalificarPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private readonly Label[] estrellas;
        private int puntuacionSeleccionada = 0;

        public int TrabajoId { get; set; }
        public int CalificadoId { get; set; }

        // A dónde navegar al terminar de calificar. Por defecto vuelve atrás ("..");
        // algunos flujos (calificar y salir directo al dashboard) pasan una ruta absoluta.
        public string? RutaSalida { get; set; }

        public string CalificadoNombre
        {
            get => _calificadoNombre;
            set
            {
                _calificadoNombre = value ?? "";
                LblSubtitulo.Text = string.IsNullOrWhiteSpace(_calificadoNombre) ? "" : $"Califica a {_calificadoNombre}";
            }
        }
        private string _calificadoNombre = "";

        public CalificarPage()
        {
            InitializeComponent();
            estrellas = new[] { Estrella1, Estrella2, Estrella3, Estrella4, Estrella5 };
        }

        private void OnEstrella1Tapped(object sender, EventArgs e) => SeleccionarPuntuacion(1);
        private void OnEstrella2Tapped(object sender, EventArgs e) => SeleccionarPuntuacion(2);
        private void OnEstrella3Tapped(object sender, EventArgs e) => SeleccionarPuntuacion(3);
        private void OnEstrella4Tapped(object sender, EventArgs e) => SeleccionarPuntuacion(4);
        private void OnEstrella5Tapped(object sender, EventArgs e) => SeleccionarPuntuacion(5);

        private void SeleccionarPuntuacion(int puntuacion)
        {
            puntuacionSeleccionada = puntuacion;

            for (int i = 0; i < estrellas.Length; i++)
            {
                var activa = i < puntuacion;
                estrellas[i].Text = activa ? "★" : "☆";
                estrellas[i].TextColor = activa ? Color.FromArgb("#F59E0B") : Color.FromArgb("#D1D5DB");
            }

            BtnEnviar.IsEnabled = true;
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnEnviarClicked(object sender, EventArgs e)
        {
            if (puntuacionSeleccionada == 0 || TrabajoId == 0 || CalificadoId == 0)
                return;

            var calificadorId = Preferences.Default.Get("UserId", 0);
            if (calificadorId == 0)
                return;

            BtnEnviar.IsEnabled = false;
            BtnEnviar.Text = "Enviando...";

            var request = new CrearCalificacionRequest
            {
                TrabajoId = TrabajoId,
                CalificadorId = calificadorId,
                CalificadoId = CalificadoId,
                Puntuacion = puntuacionSeleccionada,
                Comentario = string.IsNullOrWhiteSpace(EditorComentario.Text) ? null : EditorComentario.Text.Trim()
            };

            var success = await _apiService.CrearCalificacionAsync(request);

            if (success)
            {
                await DisplayAlert("¡Gracias!", "Tu calificación fue enviada.", "OK");
                await Shell.Current.GoToAsync(string.IsNullOrWhiteSpace(RutaSalida) ? ".." : RutaSalida);
            }
            else
            {
                await DisplayAlert("Error", "No se pudo enviar la calificación. Es posible que ya hayas calificado este trabajo.", "OK");
                BtnEnviar.Text = "Enviar calificación";
                BtnEnviar.IsEnabled = true;
            }
        }
    }
}
