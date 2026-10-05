using CUIDAPP.Localization;
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
                LblMatch.Text = Localizador.T("las_contrasenas_coinciden");
                LblMatch.TextColor = Colors.Green;
            }
            else
            {
                LblMatch.Text = Localizador.T("las_contrasenas_no_coinciden_2");
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
                await DisplayAlert(Localizador.T("error"), Localizador.T("la_contrasena_debe_tener_al_2"), Localizador.T("ok"));
                return;
            }

            if (EntryNewPass.Text != EntryConfirmPass.Text)
            {
                await DisplayAlert(Localizador.T("error"), Localizador.T("las_contrasenas_no_coinciden"), Localizador.T("ok"));
                return;
            }

            BtnRestablecer.IsEnabled = false;
            BtnRestablecer.Text = Localizador.T("restableciendo");

            try
            {
                var success = await _apiService.ResetPasswordAsync(UserEmail, ResetCode, EntryNewPass.Text);

                if (success)
                {
                    await DisplayAlert(Localizador.T("exito"), Localizador.T("tu_contrasena_ha_sido_actualizada"), Localizador.T("ok"));
                    await Shell.Current.GoToAsync("//MainPage");
                }
                else
                {
                    await DisplayAlert(Localizador.T("error"), Localizador.T("codigo_invalido_o_expirado_intenta"), Localizador.T("ok"));
                    await Shell.Current.GoToAsync("//MainPage");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(Localizador.T("error"), Localizador.F("ocurrio_un_error", ex.Message), Localizador.T("ok"));
            }
            finally
            {
                BtnRestablecer.IsEnabled = true;
                BtnRestablecer.Text = Localizador.T("restablecer_contrasena");
            }
        }
    }
}
