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

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EntryEmail.Text) || string.IsNullOrWhiteSpace(EntryPassword.Text))
            {
                await DisplayAlert("Error", "Ingresa tu correo y contraseña.", "OK");
                return;
            }

            BtnLogin.IsEnabled = false;
            BtnLogin.Text = "Ingresando...";

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
                    await DisplayAlert("Error", "Credenciales inválidas o no se pudo conectar con el servidor.", "OK");
                    return;
                }

                Preferences.Default.Set("AuthToken", result.Token);
                Preferences.Default.Set("UserEmail", result.Email);
                Preferences.Default.Set("RolId", result.RolId);

                switch (result.RolId)
                {
                    case 3: // Cuidador
                        await Shell.Current.GoToAsync("CuidadorDashboardPage");
                        break;
                    case 2: // Cliente
                        await DisplayAlert("Bienvenido", "Inicio de sesión exitoso.", "OK");
                        break;
                    default: // Admin u otro rol
                        await DisplayAlert("Bienvenido", "Inicio de sesión exitoso.", "OK");
                        break;
                }
            }
            finally
            {
                BtnLogin.IsEnabled = true;
                BtnLogin.Text = "Iniciar sesión";
            }
        }
    }
}
