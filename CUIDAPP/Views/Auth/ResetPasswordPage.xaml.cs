using CUIDAPP.Services;

namespace CUIDAPP.Views.Auth
{
    [QueryProperty(nameof(UserEmail), "email")]
    [QueryProperty(nameof(ResetCode), "code")]
    public partial class ResetPasswordPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        
        public string UserEmail { get; set; } = "";
        public string ResetCode { get; set; } = "";

        public ResetPasswordPage()
        {
            InitializeComponent();
            EntryNewPass.TextChanged += OnPasswordChanged;
            EntryConfirmPass.TextChanged += OnPasswordChanged;
        }

        private void OnPasswordChanged(object sender, TextChangedEventArgs e)
        {
            var newPass = EntryNewPass.Text ?? "";
            var confirmPass = EntryConfirmPass.Text ?? "";

            if (string.IsNullOrEmpty(confirmPass))
            {
                LblMatch.IsVisible = false;
                return;
            }

            LblMatch.IsVisible = true;
            if (newPass == confirmPass)
            {
                LblMatch.Text = "✓ Las contraseñas coinciden";
                LblMatch.TextColor = Colors.Green;
            }
            else
            {
                LblMatch.Text = "✗ Las contraseñas no coinciden";
                LblMatch.TextColor = Colors.Red;
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private void OnToggleNewPassVisibility(object sender, EventArgs e)
        {
            EntryNewPass.IsPassword = !EntryNewPass.IsPassword;
        }

        private void OnToggleConfirmPassVisibility(object sender, EventArgs e)
        {
            EntryConfirmPass.IsPassword = !EntryConfirmPass.IsPassword;
        }

        private async void OnRestablecerClicked(object sender, EventArgs e)
        {
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
                var success = await _apiService.ResetPasswordAsync(UserEmail, ResetCode, EntryNewPass.Text);

                if (success)
                {
                    await DisplayAlert("Éxito", "Tu contraseña ha sido actualizada correctamente.", "OK");
                    await Shell.Current.GoToAsync("//MainPage");
                }
                else
                {
                    await DisplayAlert("Error", "Código inválido o expirado. Intenta solicitar uno nuevo.", "OK");
                    await Shell.Current.GoToAsync("//MainPage");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Ocurrió un error: {ex.Message}", "OK");
            }
            finally
            {
                BtnRestablecer.IsEnabled = true;
                BtnRestablecer.Text = "Restablecer contraseña";
            }
        }
    }
}
