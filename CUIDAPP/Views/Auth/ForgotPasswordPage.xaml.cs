using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Auth
{
    public partial class ForgotPasswordPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private string _userEmail = "";
        private Entry[] _pins;
        private CancellationTokenSource _timerCts;

        public ForgotPasswordPage()
        {
            InitializeComponent();
            _pins = new[] { Pin1, Pin2, Pin3, Pin4, Pin5, Pin6 };
            SetupPinHandlers();

            Hoja.SizeChanged += (_, _) => TransicionHoja.AjustarAlto(Encabezado, Hoja, Height, 240 + BarraEstado.Alto());
            TransicionHoja.Preparar(Hoja, Insignia, ContenidoHoja);
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            TransicionHoja.AjustarAlto(Encabezado, Hoja, height, 240 + BarraEstado.Alto());
        }

        private bool _saliendo;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            IniciarBucle();
            await TransicionHoja.EntrarAsync(Hoja, Insignia, ContenidoHoja);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            this.AbortAnimation("BucleInsignia");
            this.AbortAnimation("BucleCirculos");
        }

        // Atrás del sistema: misma transición que la flecha.
        protected override bool OnBackButtonPressed()
        {
            _ = VolverAsync();
            return true;
        }

        private async Task VolverAsync()
        {
            if (_saliendo) return;
            _saliendo = true;
            await TransicionHoja.SalirAsync(Hoja, Insignia, ContenidoHoja);
            await Shell.Current.GoToAsync("..", animate: false);
        }

        private void IniciarBucle()
        {
            new Animation(t => Insignia.TranslationY = -4 * Math.Sin(t * Math.PI * 2), 0, 1)
                .Commit(this, "BucleInsignia", length: 4000, easing: Easing.Linear, repeat: () => true);

            new Animation(t =>
            {
                var s = Math.Sin(t * Math.PI * 2);
                CirculoA.Scale = 1 + 0.06 * s;
                CirculoA.TranslationX = -6 * s;
                CirculoB.Scale = 1 - 0.05 * s;
                CirculoB.TranslationY = -8 * s;
            }, 0, 1).Commit(this, "BucleCirculos", length: 9000, easing: Easing.Linear, repeat: () => true);
        }

        // Paso 1 → paso 2: el correo sale hacia la izquierda y el código entra desde la derecha.
        private async Task MostrarPaso2Async()
        {
            await Task.WhenAll(Step1.FadeTo(0, 160, Easing.CubicOut), Step1.TranslateTo(-24, 0, 200, Easing.CubicOut));
            Step1.IsVisible = false;
            Step2.Opacity = 0;
            Step2.TranslationX = 24;
            Step2.IsVisible = true;
            await Task.WhenAll(Step2.FadeTo(1, 260, Easing.CubicOut), Step2.TranslateTo(0, 0, 300, Easing.CubicOut));
        }

        private void OnEntryFocused(object? sender, FocusEventArgs e)
        {
            if ((sender as Element)?.Parent?.Parent is Border borde)
            {
                borde.Stroke = (Color)Application.Current!.Resources["ColorPrimary"];
                borde.BackgroundColor = (Color)Application.Current!.Resources["ColorSurface"];
            }
        }

        private void OnEntryUnfocused(object? sender, FocusEventArgs e)
        {
            if ((sender as Element)?.Parent?.Parent is Border borde)
            {
                borde.Stroke = (Color)Application.Current!.Resources["ColorBorder"];
                borde.BackgroundColor = (Color)Application.Current!.Resources["ColorBackground"];
            }
        }

        private void OnBotonPresionado(object? sender, EventArgs e) => _ = (sender as VisualElement)?.ScaleTo(0.97, 100, Easing.CubicOut);

        private void OnBotonSoltado(object? sender, EventArgs e) => _ = (sender as VisualElement)?.ScaleTo(1, 160, Easing.CubicOut);

        private void SetupPinHandlers()
        {
            Pin1.TextChanged += (s, e) => OnPinChanged(s, e, 0);
            Pin2.TextChanged += (s, e) => OnPinChanged(s, e, 1);
            Pin3.TextChanged += (s, e) => OnPinChanged(s, e, 2);
            Pin4.TextChanged += (s, e) => OnPinChanged(s, e, 3);
            Pin5.TextChanged += (s, e) => OnPinChanged(s, e, 4);
            Pin6.TextChanged += (s, e) => OnPinChanged(s, e, 5);
        }

        private void OnPinChanged(object sender, TextChangedEventArgs e, int currentIndex)
        {
            var entry = (Entry)sender;
            
            // Only allow digits
            if (!string.IsNullOrEmpty(e.NewTextValue) && !char.IsDigit(e.NewTextValue[0]))
            {
                entry.Text = "";
                return;
            }

            if (!string.IsNullOrEmpty(e.NewTextValue) && currentIndex < 5)
            {
                _pins[currentIndex + 1].Focus();
            }
            else if (string.IsNullOrEmpty(e.NewTextValue) && currentIndex > 0)
            {
                _pins[currentIndex - 1].Focus();
            }
        }

        private string GetCode()
        {
            return $"{Pin1.Text}{Pin2.Text}{Pin3.Text}{Pin4.Text}{Pin5.Text}{Pin6.Text}";
        }

        private void ClearPins()
        {
            foreach (var pin in _pins)
                pin.Text = "";
            _pins[0].Focus();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await VolverAsync();
        }

        private async void OnEnviarClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EntryEmail.Text))
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("ingresa_tu_correo_electronico"), Localizador.T("ok"));
                return;
            }

            BtnEnviar.IsEnabled = false;
            BtnEnviar.Text = Localizador.T("enviando");

            try
            {
                _userEmail = EntryEmail.Text.Trim();
                var result = await _apiService.ForgotPasswordAsync(_userEmail);

                if (result == null)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_conectar_con"), Localizador.T("ok"));
                    return;
                }

                var asunto = Localizador.T("email_asunto_recuperacion");
                var cuerpoHtml = $"""
                    <div style="font-family: Arial, sans-serif; max-width: 400px; margin: 0 auto; padding: 20px;">
                        <h2 style="color: #1C4D96; text-align: center;">CuidaApp</h2>
                        <p>{Localizador.T("email_codigo_es")}</p>
                        <div style="background: #F5F8FC; border: 1px solid #D9E2EC; border-radius: 8px; padding: 15px; text-align: center; font-size: 28px; font-weight: bold; letter-spacing: 8px; color: #0A2F41;">{result.Code}</div>
                        <p style="color: #4B5563; font-size: 13px;">{Localizador.T("email_expira")}</p>
                    </div>
                    """;
                await _apiService.EnviarEmailAsync(_userEmail, asunto, cuerpoHtml);

                await Alerta.MostrarAsync(Localizador.T("codigo_enviado"), Localizador.F("revisa_tu_correo_electronico_para", _userEmail), Localizador.T("ok"));

                _ = MostrarPaso2Async();
                TopTitle.Text = Localizador.T("verificar_codigo");
                LblEmail.Text = Localizador.F("se_envio_un_codigo_a", _userEmail);
                
                StartTimer();
                _pins[0].Focus();
            }
            finally
            {
                BtnEnviar.IsEnabled = true;
                BtnEnviar.Text = Localizador.T("enviar_codigo");
            }
        }

        private void StartTimer()
        {
            _timerCts?.Cancel();
            _timerCts = new CancellationTokenSource();
            var token = _timerCts.Token;
            
            Task.Run(async () =>
            {
                for (int i = 60; i > 0; i--)
                {
                    if (token.IsCancellationRequested) break;
                    
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LblTimer.Text = Localizador.F("reenviar_codigo_en_s", i);
                        LblReenviar.Opacity = 0.5;
                        LblReenviar.GestureRecognizers.Clear();
                    });
                    
                    await Task.Delay(1000, token);
                }
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LblTimer.Text = "";
                    LblReenviar.Opacity = 1;
                    LblReenviar.GestureRecognizers.Clear();
                    LblReenviar.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => OnReenviarTapped(null, null)) });
                });
            }, token);
        }

        private async void OnReenviarTapped(object sender, EventArgs e)
        {
            ClearPins();
            
            if (string.IsNullOrWhiteSpace(EntryEmail.Text))
                return;

            BtnEnviar.IsEnabled = false;
            BtnEnviar.Text = Localizador.T("enviando");

            try
            {
                _userEmail = EntryEmail.Text.Trim();
                var result = await _apiService.ForgotPasswordAsync(_userEmail);

                if (result == null)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_conectar_con"), Localizador.T("ok"));
                    return;
                }

                var asunto = Localizador.T("email_asunto_recuperacion");
                var cuerpoHtml = $"""
                    <div style="font-family: Arial, sans-serif; max-width: 400px; margin: 0 auto; padding: 20px;">
                        <h2 style="color: #1C4D96; text-align: center;">CuidaApp</h2>
                        <p>{Localizador.T("email_codigo_es")}</p>
                        <div style="background: #F5F8FC; border: 1px solid #D9E2EC; border-radius: 8px; padding: 15px; text-align: center; font-size: 28px; font-weight: bold; letter-spacing: 8px; color: #0A2F41;">{result.Code}</div>
                        <p style="color: #4B5563; font-size: 13px;">{Localizador.T("email_expira")}</p>
                    </div>
                    """;
                await _apiService.EnviarEmailAsync(_userEmail, asunto, cuerpoHtml);

                await Alerta.MostrarAsync(Localizador.T("codigo_reenviado"), Localizador.F("se_envio_un_nuevo_codigo", _userEmail), Localizador.T("ok"));

                StartTimer();
                _pins[0].Focus();
            }
            finally
            {
                BtnEnviar.IsEnabled = true;
                BtnEnviar.Text = Localizador.T("enviar_codigo");
            }
        }

        private async void OnVerificarClicked(object sender, EventArgs e)
        {
            var code = GetCode();
            if (code.Length != 6)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("ingresa_el_codigo_de_6"), Localizador.T("ok"));
                return;
            }

            BtnVerificar.IsEnabled = false;
            BtnVerificar.Text = Localizador.T("verificando");

            try
            {
                // Navigate to ResetPasswordPage with email and code
                await Shell.Current.GoToAsync($"ResetPasswordPage?email={Uri.EscapeDataString(_userEmail)}&code={code}");
            }
            finally
            {
                BtnVerificar.IsEnabled = true;
                BtnVerificar.Text = Localizador.T("verificar_codigo");
            }
        }
    }
}
