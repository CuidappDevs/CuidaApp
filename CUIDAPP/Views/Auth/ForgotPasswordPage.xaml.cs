using CUIDAPP.Services;

namespace CUIDAPP.Views.Auth
{
    public partial class ForgotPasswordPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private string _userEmail = "";
        private string _resetCode = "";
        private Entry[] _pins;
        private CancellationTokenSource _timerCts;

        public ForgotPasswordPage()
        {
            InitializeComponent();
            _pins = new[] { Pin1, Pin2, Pin3, Pin4, Pin5, Pin6 };
            SetupPinHandlers();
        }

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
            await Shell.Current.GoToAsync("..");
        }

        private async void OnEnviarClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EntryEmail.Text))
            {
                await DisplayAlert("Error", "Ingresa tu correo electrónico.", "OK");
                return;
            }

            BtnEnviar.IsEnabled = false;
            BtnEnviar.Text = "Enviando...";

            try
            {
                _userEmail = EntryEmail.Text.Trim();
                var result = await _apiService.ForgotPasswordAsync(_userEmail);

                if (result == null)
                {
                    await DisplayAlert("Error", "No se pudo conectar con el servidor.", "OK");
                    return;
                }

                _resetCode = result.Code;
                await DisplayAlert("Código de recuperación", $"Tu código es: {_resetCode}\n\n(En producción esto se enviaría por email)", "OK");

                Step1.IsVisible = false;
                Step2.IsVisible = true;
                TopTitle.Text = "Verificar código";
                LblEmail.Text = $"Se envió un código a {_userEmail}";
                
                StartTimer();
                _pins[0].Focus();
            }
            finally
            {
                BtnEnviar.IsEnabled = true;
                BtnEnviar.Text = "Enviar código";
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
                        LblTimer.Text = $"Reenviar código en {i}s";
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
            BtnEnviar.Text = "Enviando...";

            try
            {
                _userEmail = EntryEmail.Text.Trim();
                var result = await _apiService.ForgotPasswordAsync(_userEmail);

                if (result == null)
                {
                    await DisplayAlert("Error", "No se pudo conectar con el servidor.", "OK");
                    return;
                }

                _resetCode = result.Code;
                await DisplayAlert("Código de recuperación", $"Tu código es: {_resetCode}\n\n(En producción esto se enviaría por email)", "OK");

                StartTimer();
                _pins[0].Focus();
            }
            finally
            {
                BtnEnviar.IsEnabled = true;
                BtnEnviar.Text = "Enviar código";
            }
        }

        private async void OnVerificarClicked(object sender, EventArgs e)
        {
            var code = GetCode();
            if (code.Length != 6)
            {
                await DisplayAlert("Error", "Ingresa el código de 6 dígitos.", "OK");
                return;
            }

            BtnVerificar.IsEnabled = false;
            BtnVerificar.Text = "Verificando...";

            try
            {
                // Navigate to ResetPasswordPage with email and code
                await Shell.Current.GoToAsync($"ResetPasswordPage?email={Uri.EscapeDataString(_userEmail)}&code={code}");
            }
            finally
            {
                BtnVerificar.IsEnabled = true;
                BtnVerificar.Text = "Verificar código";
            }
        }
    }
}
