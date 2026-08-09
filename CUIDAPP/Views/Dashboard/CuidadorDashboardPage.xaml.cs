using CUIDAPP.Services;

namespace CUIDAPP.Views.Dashboard
{
    public partial class CuidadorDashboardPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private int cuidadorId;
        private bool disponibleActual;

        public CuidadorDashboardPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            cuidadorId = Preferences.Default.Get("UserId", 0);

            if (cuidadorId == 0)
            {
                await Shell.Current.GoToAsync("//MainPage");
                return;
            }

            await CargarDashboard();
        }

        private async Task CargarDashboard()
        {
            var perfilTask = _apiService.ObtenerPerfilCuidadorAsync(cuidadorId);
            var gananciasTask = _apiService.ObtenerGananciasAsync(cuidadorId);
            var proximoTrabajoTask = _apiService.ObtenerProximoTrabajoAsync(cuidadorId);
            var trabajosTask = _apiService.ObtenerTrabajosAsync(cuidadorId);

            await Task.WhenAll(perfilTask, gananciasTask, proximoTrabajoTask, trabajosTask);

            var perfil = perfilTask.Result;
            var ganancias = gananciasTask.Result;
            var proximoTrabajo = proximoTrabajoTask.Result;
            var pendientes = trabajosTask.Result.Count(t => t.Estado == 1);

            BadgeNotificaciones.IsVisible = pendientes > 0;
            LblBadgeNotificaciones.Text = pendientes > 9 ? "9+" : pendientes.ToString();

            if (perfil != null)
            {
                var primerNombre = perfil.NombreCompleto.Split(' ').FirstOrDefault() ?? perfil.NombreCompleto;
                LblSaludo.Text = $"Hola, {primerNombre}";

                if (!string.IsNullOrWhiteSpace(perfil.FotoUrl))
                    ImgFotoPerfil.Source = $"{ApiService.ServerOrigin}{perfil.FotoUrl}";

                disponibleActual = perfil.Disponible;
                ActualizarUiDisponibilidad();
            }

            LblGanadoHoy.Text = $"RD${(ganancias?.GanadoHoy ?? 0):N0}";

            if (proximoTrabajo != null)
            {
                CardProximoTrabajo.IsVisible = true;
                LblSinTrabajos.IsVisible = false;

                LblProximoHorario.Text = $"{FormatearHora(proximoTrabajo.HoraInicio)} - {FormatearHora(proximoTrabajo.HoraFin)}";
                LblProximoServicio.Text = proximoTrabajo.TipoServicio;
                LblProximoDireccion.Text = string.IsNullOrWhiteSpace(proximoTrabajo.Direccion) ? "Sin dirección" : proximoTrabajo.Direccion;
            }
            else
            {
                CardProximoTrabajo.IsVisible = false;
                LblSinTrabajos.IsVisible = true;
            }
        }

        private static string FormatearHora(TimeSpan hora)
        {
            return DateTime.Today.Add(hora).ToString("h:mm tt");
        }

        private void ActualizarUiDisponibilidad()
        {
            if (disponibleActual)
            {
                IndicadorDisponible.BackgroundColor = Color.FromArgb("#10B981");
                LblDisponible.Text = "Disponible ahora";
                LblDisponible.TextColor = Color.FromArgb("#059669");
            }
            else
            {
                IndicadorDisponible.BackgroundColor = Color.FromArgb("#9CA3AF");
                LblDisponible.Text = "No disponible";
                LblDisponible.TextColor = Color.FromArgb("#6B7280");
            }
        }

        private async void OnDisponibilidadTapped(object sender, EventArgs e)
        {
            var nuevoValor = !disponibleActual;
            var success = await _apiService.ActualizarDisponibilidadAsync(cuidadorId, nuevoValor);

            if (success)
            {
                disponibleActual = nuevoValor;
                ActualizarUiDisponibilidad();
            }
            else
            {
                await DisplayAlert("Error", "No se pudo actualizar tu disponibilidad. Intenta de nuevo.", "OK");
            }
        }

        private async void OnProximoTrabajoTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("TrabajosPage");
        }

        private async void OnPerfilTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorPerfilPage");
        }

        private async void OnTrabajosTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("TrabajosPage");
        }

        private async void OnDineroTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("DineroPage");
        }

        private async void OnNotificacionesTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("TrabajosPage");
        }

        private async void OnCerrarSesionTapped(object sender, EventArgs e)
        {
            var confirmar = await DisplayAlert("Cerrar sesión", "¿Estás seguro de que deseas cerrar sesión?", "Sí", "Cancelar");
            if (!confirmar)
                return;

            Preferences.Default.Clear();
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}
