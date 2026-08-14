using CUIDAPP.Services;

namespace CUIDAPP.Views.Auth
{
    public partial class ForgotPasswordPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private string _userEmail = "";
        private string _resetCode = "";

        public ForgotPasswordPage()
        {
            InitializeComponent();
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
                LblEmail.Text = $"Se envió un código a {_userEmail}";
            }
            finally
            {
                BtnEnviar.IsEnabled = true;
                BtnEnviar.Text = "Enviar código";
            }
        }

        private async void OnRestablecerClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EntryCode.Text) || EntryCode.Text.Length != 6)
            {
                await DisplayAlert("Error", "Ingresa un código de 6 dígitos.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(EntryNewPass.Text) || EntryNewPass.Text.Length < 6)
            {
                await DisplayAlert("Error", "La contraseña debe tener al menos 6 caracteres.", "OK");
                return;
            }
            if (EntryNewPass.Text != EntryConfirmPass.Text)
            {
                await DisplayAlert("Error", "Las contraseñas no coinciden.", "OK");
                return;
            }

            BtnRestablecer.IsEnabled = false;
            BtnRestablecer.Text = "Restableciendo...";

            try
            {
                var success = await _apiService.ResetPasswordAsync(_userEmail, EntryCode.Text, EntryNewPass.Text);

                if (success)
                {
                    await DisplayAlert("Éxito", "Tu contraseña ha sido actualizada.", "OK");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Error", "Código inválido o expirado.", "OK");
                }
            }
            finally
            {
                BtnRestablecer.IsEnabled = true;
                BtnRestablecer.Text = "Restablecer contraseña";
            }
        }
    }
}
