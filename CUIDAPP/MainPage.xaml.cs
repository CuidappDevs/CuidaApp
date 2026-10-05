using CUIDAPP.Localization;
using CUIDAPP.Models.Auth;
using CUIDAPP.Services;

namespace CUIDAPP
{
    public partial class MainPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnRegisterTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("RegistroPage");
        }

        private async void OnForgotPasswordTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("ForgotPasswordPage");
        }

        private void OnTogglePasswordVisibility(object sender, EventArgs e)
        {
            EntryPassword.IsPassword = !EntryPassword.IsPassword;
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EntryEmail.Text) || string.IsNullOrWhiteSpace(EntryPassword.Text))
            {
                await DisplayAlert(Localizador.T("error"), Localizador.T("ingresa_tu_correo_y_contrasena"), Localizador.T("ok"));
                return;
            }

            BtnLogin.IsEnabled = false;
            BtnLogin.Text = Localizador.T("ingresando");

            try
            {
                var request = new LoginRequest
                {
                    Email = EntryEmail.Text.Trim(),
                    Password = EntryPassword.Text
                };

                var result = await _apiService.LoginAsync(request);

                if (result == null)
                {
                    await DisplayAlert(Localizador.T("error"), Localizador.T("credenciales_invalidas_o_no_se"), Localizador.T("ok"));
                    return;
                }

                Preferences.Default.Set("AuthToken", result.Token);
                Preferences.Default.Set("UserEmail", result.Email);
                Preferences.Default.Set("UserNombre", result.NombreCompleto ?? "");
                Preferences.Default.Set("UserFotoUrl", result.FotoUrl ?? "");
                Preferences.Default.Set("UserId", result.UserId);
                Preferences.Default.Set("RolId", result.RolId);
                Preferences.Default.Set("EstadoAprobacion", result.EstadoAprobacion ?? 0);

                _ = RealtimeService.ConectarAsync(result.UserId);
                _ = ServerClock.SincronizarAsync();
                ConexionServiceManager.Iniciar();

                switch (result.RolId)
                {
                    case 3: // Cuidador
                        if (result.EstadoAprobacion == 2)
                            await Shell.Current.GoToAsync("CuidadorDashboardPage");
                        else
                            await Shell.Current.GoToAsync("VerificacionPendientePage");
                        break;
                    case 2: // Cliente
                        await Shell.Current.GoToAsync("ClienteDashboardPage");
                        break;
                    default: // Admin u otro rol
                        await DisplayAlert(Localizador.T("bienvenido"), Localizador.T("inicio_de_sesion_exitoso"), Localizador.T("ok"));
                        break;
                }
            }
            finally
            {
                BtnLogin.IsEnabled = true;
                BtnLogin.Text = Localizador.T("iniciar_sesion");
            }
        }
    }
}
