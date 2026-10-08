using CUIDAPP.Localization;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Comun
{
    /// <summary>
    /// Pantalla completa del "¿Estás bien?" del centro de mando. No se puede cerrar con atrás: hay que
    /// responder (o esperar a que venza, y entonces el equipo recibe una alerta SOS).
    /// </summary>
    public partial class CheckinPage : ContentPage
    {
        private readonly int _id;
        private readonly DateTime _venceUtc;
        private readonly TaskCompletionSource _respondida = new();
        private bool _enviando;

        public Task Respondida => _respondida.Task;

        public CheckinPage(int id, string mensaje, DateTime venceUtc)
        {
            InitializeComponent();
            _id = id;
            _venceUtc = venceUtc;
            LblMensaje.Text = mensaje;
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();
            ActualizarRestante();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(400)); } catch { }

            // Dos anillos que se expanden y desvanecen, y el corazón que late.
            new Animation(t => { Anillo1.Scale = 0.65 + 0.5 * t; Anillo1.Opacity = 1 - t; }, 0, 1)
                .Commit(this, "Anillo1", length: 1600, easing: Easing.CubicOut, repeat: () => true);
            Dispatcher.StartTimer(TimeSpan.FromMilliseconds(800), () =>
            {
                new Animation(t => { Anillo2.Scale = 0.65 + 0.5 * t; Anillo2.Opacity = 1 - t; }, 0, 1)
                    .Commit(this, "Anillo2", length: 1600, easing: Easing.CubicOut, repeat: () => true);
                return false;
            });
            new Animation { { 0, 0.5, new Animation(v => Corazon.Scale = v, 1, 1.15) }, { 0.5, 1, new Animation(v => Corazon.Scale = v, 1.15, 1) } }
                .Commit(this, "Latido", length: 900, repeat: () => true);

            Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                ActualizarRestante();
                if (DateTime.UtcNow >= _venceUtc && !_respondida.Task.IsCompleted)
                {
                    _ = CerrarAsync();
                    return false;
                }
                return !_respondida.Task.IsCompleted;
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            this.AbortAnimation("Anillo1");
            this.AbortAnimation("Anillo2");
            this.AbortAnimation("Latido");
        }

        // Atrás no la cierra: tiene que responder.
        protected override bool OnBackButtonPressed() => true;

        private void ActualizarRestante()
        {
            var resta = _venceUtc - DateTime.UtcNow;
            if (resta < TimeSpan.Zero) resta = TimeSpan.Zero;
            LblRestante.Text = Localizador.F("checkin_restante", $"{(int)resta.TotalMinutes}:{resta.Seconds:00}");
        }

        private async void OnBienTapped(object sender, TappedEventArgs e) => await ResponderAsync(true, BtnBien);
        private async void OnAyudaTapped(object sender, TappedEventArgs e) => await ResponderAsync(false, BtnAyuda);

        private async Task ResponderAsync(bool estaBien, View boton)
        {
            if (_enviando) return;
            _enviando = true;
            await boton.ScaleTo(0.97, 80, Easing.CubicOut);
            _ = boton.ScaleTo(1, 140, Easing.CubicOut);
            Enviando.IsVisible = Enviando.IsRunning = true;

            var usuarioId = Preferences.Default.Get("UserId", 0);
            var ok = await new ApiService().ResponderCheckinAsync(usuarioId, _id, estaBien);
            Enviando.IsVisible = Enviando.IsRunning = false;

            if (!ok)
            {
                _enviando = false;
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("checkin_error"), Localizador.T("ok"));
                return;
            }

            await Alerta.MostrarAsync(Localizador.T(estaBien ? "checkin_gracias_titulo" : "checkin_ayuda_titulo"),
                Localizador.T(estaBien ? "checkin_gracias_texto" : "checkin_ayuda_texto"), Localizador.T("entendido"),
                estaBien ? TipoAlerta.Info : TipoAlerta.Advertencia);
            await CerrarAsync();
        }

        private async Task CerrarAsync()
        {
            _respondida.TrySetResult();
            if (Navigation.ModalStack.Contains(this))
                await Navigation.PopModalAsync(true);
        }
    }
}
