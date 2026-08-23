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

            await ProponerCalificacionPendienteAsync(clienteId);
        }

        private async Task ProponerCalificacionPendienteAsync(int clienteId)
        {
            var servicios = await _apiService.ObtenerTrabajosActivosPorClienteAsync(clienteId);
            var pendiente = servicios.FirstOrDefault(t => t.Estado == 4);
            if (pendiente == null)
                return;

            var calificar = await DisplayAlert(
                "¡Servicio completado!",
                $"{pendiente.CuidadorNombre} terminó tu servicio y necesita tu calificación.",
                "Calificar ahora", "Después");

            if (!calificar)
                return;

            var parametros = new Dictionary<string, object>
            {
                { "TrabajoId", pendiente.Id },
                { "CalificadoId", pendiente.CuidadorId },
                { "CalificadoNombre", pendiente.CuidadorNombre },
                { "RutaSalida", "//ClienteDashboardPage" }
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

        private async void OnCerrarSesionTapped(object sender, EventArgs e)
        {
            Preferences.Default.Clear();
            await RealtimeService.DesconectarAsync();
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}
