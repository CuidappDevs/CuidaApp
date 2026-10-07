using CUIDAPP.Localization;
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
                LblSubtitulo.Text = string.IsNullOrWhiteSpace(_calificadoNombre) ? "" : Localizador.F("califica_a", _calificadoNombre);
            }
        }
        private string _calificadoNombre = "";

        public CalificarPage()
        {
            InitializeComponent();
            estrellas = new[] { Estrella1, Estrella2, Estrella3, Estrella4, Estrella5 };

            // Borde a borde: el encabezado empieza debajo de la barra de estado y el contenido
            // termina con espacio para la barra de gestos.
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();

            InsigniaEstrella.Scale = 0.85;
            InsigniaEstrella.Opacity = 0;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            try
            {
                _ = InsigniaEstrella.FadeTo(1, 300, Easing.CubicOut);
                await InsigniaEstrella.ScaleTo(1.06, 260, Easing.CubicOut);
                await InsigniaEstrella.ScaleTo(1, 160, Easing.CubicInOut);
            }
            finally
            {
                InsigniaEstrella.Opacity = 1;
                InsigniaEstrella.Scale = 1;
            }
        }

        private void OnComentarioFocused(object? sender, FocusEventArgs e) => BordeComentario.Stroke = (Color)Application.Current!.Resources["ColorPrimary"];

        private void OnComentarioUnfocused(object? sender, FocusEventArgs e) => BordeComentario.Stroke = Colors.Transparent;

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
                estrellas[i].TextColor = activa ? Color.FromArgb("#E09A1A") : Color.FromArgb("#D1D5DB");

                // Las estrellas activas rebotan una tras otra
                if (activa)
                {
                    var estrella = estrellas[i];
                    var retraso = i * 45;
                    _ = Task.Delay(retraso).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await estrella.ScaleTo(1.3, 110, Easing.CubicOut);
                        await estrella.ScaleTo(1, 160, Easing.CubicInOut);
                    }));
                }
            }

            // Qué significa la puntuación
            LblSignificado.Text = Localizador.T($"estrellas_{puntuacion}");
            LblSignificado.TextColor = Color.FromArgb("#E09A1A");
            LblSignificado.Opacity = 0;
            _ = LblSignificado.FadeTo(1, 220, Easing.CubicOut);

            BtnEnviar.IsEnabled = true;
            _ = BtnEnviar.FadeTo(1, 200, Easing.CubicOut);
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
            BtnEnviar.Text = Localizador.T("enviando");

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
                Recordatorios.CancelarCalificacion(TrabajoId);

            if (success)
            {
                await Alerta.MostrarAsync(Localizador.T("gracias"), Localizador.T("tu_calificacion_fue_enviada"), Localizador.T("ok"));
                try
                {
                    await Shell.Current.GoToAsync(string.IsNullOrWhiteSpace(RutaSalida) ? ".." : RutaSalida);
                }
                catch (Exception ex)
                {
                    // Una ruta de salida inválida no debe tumbar la app (async void): volvemos atrás.
                    Console.WriteLine($"Error navegando tras calificar: {ex.Message}");
                    await Shell.Current.GoToAsync("..");
                }
            }
            else
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_enviar_la"), Localizador.T("ok"));
                BtnEnviar.Text = Localizador.T("enviar_calificacion");
                BtnEnviar.IsEnabled = true;
            }
        }
    }
}
