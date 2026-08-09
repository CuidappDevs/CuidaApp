using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class ClientePerfilPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public ClientePerfilPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var clienteId = Preferences.Default.Get("UserId", 0);
            if (clienteId == 0)
                return;

            var perfil = await _apiService.ObtenerPerfilClienteAsync(clienteId);
            if (perfil == null)
                return;

            LblNombre.Text = perfil.NombreCompleto;
            LblEmail.Text = perfil.Email;
            LblDireccion.Text = string.IsNullOrWhiteSpace(perfil.DireccionPrincipal) ? "Sin dirección registrada" : perfil.DireccionPrincipal;

            LblContacto.Text = string.IsNullOrWhiteSpace(perfil.ContactoEmergenciaNombre)
                ? "Sin contacto registrado"
                : $"{perfil.ContactoEmergenciaNombre} · {perfil.ContactoEmergenciaTelefono}";

            if (!string.IsNullOrWhiteSpace(perfil.FotoUrl))
                ImgFoto.Source = $"{ApiService.ServerOrigin}{perfil.FotoUrl}";
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnCerrarSesionTapped(object sender, EventArgs e)
        {
            Preferences.Default.Clear();
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}
