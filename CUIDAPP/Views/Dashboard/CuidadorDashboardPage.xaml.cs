using CUIDAPP.Localization;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Dashboard
{
    public partial class CuidadorDashboardPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private int cuidadorId;
        private bool disponibleActual;
        private bool suprimirEventoToggle;
        private bool estaVisible;
        private bool pollingUbicacionIniciado;

        public CuidadorDashboardPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            estaVisible = true;
            cuidadorId = Preferences.Default.Get("UserId", 0);

            if (cuidadorId == 0)
            {
                await Shell.Current.GoToAsync("//MainPage");
                return;
            }

            _ = RealtimeService.ConectarAsync(cuidadorId);

            // Evita suscripciones duplicadas si OnAppearing se dispara más de una vez
            // sin un OnDisappearing intermedio (puede pasar con navegación "//").
            RealtimeService.NuevaSolicitud -= OnNuevaSolicitudTiempoReal;
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
            RealtimeService.NuevaSolicitud += OnNuevaSolicitudTiempoReal;
            RealtimeService.TrabajoActualizado += OnTrabajoActualizadoTiempoReal;

            await CargarDashboard();
            _ = ActualizarUbicacionActualAsync();
            IniciarPollingUbicacionSiHaceFalta();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            estaVisible = false;
            RealtimeService.NuevaSolicitud -= OnNuevaSolicitudTiempoReal;
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
        }

        private async void OnNuevaSolicitudTiempoReal(int trabajoId, int clienteId)
        {
            await CargarDashboard();
        }

        private async void OnTrabajoActualizadoTiempoReal(int trabajoId, int estado)
        {
            await CargarDashboard();
        }

        private void IniciarPollingUbicacionSiHaceFalta()
        {
            if (pollingUbicacionIniciado)
                return;
            pollingUbicacionIniciado = true;

            // Mientras el cuidador esté "Disponible" y con esta pantalla abierta, subimos su
            // GPS periódicamente para que el punto verde en el mapa del cliente refleje su
            // posición real en vez de quedar congelado en la dirección de registro.
            Dispatcher.StartTimer(TimeSpan.FromSeconds(30), () =>
            {
                if (!estaVisible)
                    return false;

                if (disponibleActual)
                    _ = ActualizarUbicacionActualAsync();

                return true;
            });
        }

        private async Task ActualizarUbicacionActualAsync()
        {
            if (!disponibleActual)
                return;

            var ubicacion = await LocationService.ObtenerUbicacionActualAsync();
            if (ubicacion == null)
                return;

            await _apiService.ActualizarUbicacionCuidadorAsync(cuidadorId, ubicacion.Latitude, ubicacion.Longitude);
        }

        private async Task CargarDashboard()
        {
            try
            {
                await CargarDashboardInterno();
            }
            catch (Exception ex)
            {
                // Nunca dejar que una excepción no atrapada aquí reviente la app: esto
                // corre dentro de un OnAppearing "async void", donde una excepción sin
                // capturar mata el proceso en Android en vez de solo mostrar un error.
                Console.WriteLine($"[CuidadorDashboardPage] Error cargando dashboard: {ex}");
                await DisplayAlert(Localizador.T("error"), Localizador.T("no_se_pudo_cargar_tu"), Localizador.T("ok"));
            }
        }

        private async Task CargarDashboardInterno()
        {
            var perfilTask = _apiService.ObtenerPerfilCuidadorAsync(cuidadorId);
            var gananciasTask = _apiService.ObtenerGananciasAsync(cuidadorId);
            var proximoTrabajoTask = _apiService.ObtenerProximoTrabajoAsync(cuidadorId);
            var trabajosTask = _apiService.ObtenerTrabajosAsync(cuidadorId);
            var ratingTask = _apiService.ObtenerPromedioCalificacionAsync(cuidadorId);

            await Task.WhenAll(perfilTask, gananciasTask, proximoTrabajoTask, trabajosTask, ratingTask);

            var perfil = perfilTask.Result;
            var ganancias = gananciasTask.Result;
            var proximoTrabajo = proximoTrabajoTask.Result;
            var pendientes = trabajosTask.Result.Count(t => t.Estado == 1);
            var rating = ratingTask.Result;

            if (rating != null && rating.Total > 0)
            {
                LblRating.Text = $"{rating.Promedio:N1} ({rating.Total})";
                ContenedorRating.IsVisible = true;
            }
            else
            {
                ContenedorRating.IsVisible = false;
            }

            BadgeNotificaciones.IsVisible = pendientes > 0;
            LblBadgeNotificaciones.Text = pendientes > 9 ? "9+" : pendientes.ToString();

            if (perfil != null)
            {
                var primerNombre = perfil.NombreCompleto.Split(' ').FirstOrDefault() ?? perfil.NombreCompleto;
                LblSaludo.Text = Localizador.F("hola_2", primerNombre);

                if (!string.IsNullOrWhiteSpace(perfil.FotoUrl))
                    ImgFotoPerfil.Source = $"{ApiService.ServerOrigin}{perfil.FotoUrl}";

                disponibleActual = perfil.Disponible;
                ActualizarUiDisponibilidad();
            }

            LblGanadoHoy.Text = Localizador.F("rd_2", (ganancias?.GanadoHoy ?? 0));
            LblPendienteCobrar.Text = Localizador.F("rd_2", (ganancias?.PendientePorCobrar ?? 0));

            if (proximoTrabajo != null)
            {
                CardProximoTrabajo.IsVisible = true;
                LblSinTrabajos.IsVisible = false;

                LblProximoHorario.Text = $"{FormatearHora(proximoTrabajo.HoraInicio)} - {FormatearHora(proximoTrabajo.HoraFin)}";
                LblProximoServicio.Text = Localizador.D(proximoTrabajo.TipoServicio);
                LblProximoDireccion.Text = string.IsNullOrWhiteSpace(proximoTrabajo.Direccion) ? Localizador.T("sin_direccion") : proximoTrabajo.Direccion;
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
            suprimirEventoToggle = true;
            SwitchDisponible.IsToggled = disponibleActual;
            suprimirEventoToggle = false;

            if (disponibleActual)
            {
                CardDisponibilidad.BackgroundColor = Color.FromArgb("#ECFDF5");
                IconoDisponibleFondo.BackgroundColor = Color.FromArgb("#10B981");
                IconoDisponible.Fill = Colors.White;
                LblDisponible.Text = Localizador.T("disponible_ahora");
                LblDisponible.TextColor = Color.FromArgb("#065F46");
                LblDisponibleSubtitulo.Text = Localizador.T("los_clientes_pueden_verte_y");
            }
            else
            {
                CardDisponibilidad.BackgroundColor = Color.FromArgb("#F3F4F6");
                IconoDisponibleFondo.BackgroundColor = Color.FromArgb("#E5E7EB");
                IconoDisponible.Fill = Color.FromArgb("#9CA3AF");
                LblDisponible.Text = Localizador.T("no_disponible");
                LblDisponible.TextColor = Color.FromArgb("#374151");
                LblDisponibleSubtitulo.Text = Localizador.T("estas_desconectado_los_clientes_no");
            }
        }

        private void OnDisponibilidadTapped(object sender, EventArgs e)
        {
            // Alterna el Switch; la lógica real corre en OnDisponibilidadToggled.
            SwitchDisponible.IsToggled = !SwitchDisponible.IsToggled;
        }

        private async void OnDisponibilidadToggled(object sender, ToggledEventArgs e)
        {
            if (suprimirEventoToggle)
                return;

            var nuevoValor = e.Value;
            SwitchDisponible.IsEnabled = false;
            LblDisponibleSubtitulo.Text = Localizador.T("actualizando");

            var success = await _apiService.ActualizarDisponibilidadAsync(cuidadorId, nuevoValor);
            SwitchDisponible.IsEnabled = true;

            if (success)
            {
                disponibleActual = nuevoValor;
                ActualizarUiDisponibilidad();

                if (disponibleActual)
                    _ = ActualizarUbicacionActualAsync();
            }
            else
            {
                await DisplayAlert(Localizador.T("error"), Localizador.T("no_se_pudo_actualizar_tu"), Localizador.T("ok"));
                // Revertir visualmente sin volver a llamar a la API.
                suprimirEventoToggle = true;
                SwitchDisponible.IsToggled = disponibleActual;
                suprimirEventoToggle = false;
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
            await Navigation.PushModalAsync(new Views.Cliente.NotificacionesPage());
        }

        private async void OnCerrarSesionTapped(object sender, EventArgs e)
        {
            var confirmar = await DisplayAlert(Localizador.T("cerrar_sesion"), Localizador.T("estas_seguro_de_que_deseas"), Localizador.T("si"), Localizador.T("cancelar"));
            if (!confirmar)
                return;

            Preferences.Default.Clear();
            await RealtimeService.DesconectarAsync();
            ConexionServiceManager.Detener();
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}
