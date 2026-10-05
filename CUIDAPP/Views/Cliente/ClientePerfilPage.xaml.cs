using CUIDAPP.Localization;
using CUIDAPP.Models.Cliente;
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

        private PerfilCliente? _perfil;

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Localizador.Instancia.IdiomaCambiado -= AplicarPerfil;
        }

        // Textos armados por código: se vuelven a pintar al instante cuando cambia el idioma.
        private void AplicarPerfil()
        {
            if (_perfil == null)
                return;

            LblNombre.Text = _perfil.NombreCompleto;
            LblEmail.Text = _perfil.Email;
            LblDireccion.Text = string.IsNullOrWhiteSpace(_perfil.DireccionPrincipal) ? Localizador.T("sin_direccion_registrada") : _perfil.DireccionPrincipal;

            LblContacto.Text = string.IsNullOrWhiteSpace(_perfil.ContactoEmergenciaNombre)
                ? Localizador.T("sin_contacto_registrado")
                : $"{_perfil.ContactoEmergenciaNombre} · {_perfil.ContactoEmergenciaTelefono}";
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Localizador.Instancia.IdiomaCambiado -= AplicarPerfil;
            Localizador.Instancia.IdiomaCambiado += AplicarPerfil;

            var clienteId = Preferences.Default.Get("UserId", 0);
            if (clienteId == 0)
                return;

            var perfil = await _apiService.ObtenerPerfilClienteAsync(clienteId);
            if (perfil == null)
                return;

            _perfil = perfil;
            AplicarPerfil();

            if (!string.IsNullOrWhiteSpace(perfil.FotoUrl))
                ImgFoto.Source = $"{ApiService.ServerOrigin}{perfil.FotoUrl}";

            await ProponerCalificacionPendienteAsync(clienteId);
        }

        private async Task ProponerCalificacionPendienteAsync(int clienteId)
        {
            var servicios = await _apiService.ObtenerTrabajosActivosPorClienteAsync(clienteId);
            var pendiente = servicios.FirstOrDefault(t => t.Estado == 4);
            if (pendiente == null)
                return;

            var calificar = await DisplayAlert(
                Localizador.T("servicio_completado"),
                Localizador.F("termino_tu_servicio_y_necesita", pendiente.CuidadorNombre),
                Localizador.T("calificar_ahora"), Localizador.T("despues"));

            if (!calificar)
                return;

            var parametros = new Dictionary<string, object>
            {
                { "TrabajoId", pendiente.Id },
                { "CalificadoId", pendiente.CuidadorId },
                { "CalificadoNombre", pendiente.CuidadorNombre },
                { "RutaSalida", "//MainPage/ClienteDashboardPage" }
            };
            await Shell.Current.GoToAsync("CalificarPage", parametros);
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnMisUbicacionesTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("MisUbicacionesPage");
        }

        private async void OnMisCalificacionesTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("MisCalificacionesPage");
        }

        private async void OnSoporteTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("MisReportesPage");
        }

        private async void OnCerrarSesionTapped(object sender, EventArgs e)
        {
            Preferences.Default.Clear();
            await RealtimeService.DesconectarAsync();
            ConexionServiceManager.Detener();
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}
