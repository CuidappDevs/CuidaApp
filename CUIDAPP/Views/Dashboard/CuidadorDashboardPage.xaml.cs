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
            BarraEstado.Blanca();
            _ = AnimarEntradaAsync();
            estaVisible = true;
            cuidadorId = Preferences.Default.Get("UserId", 0);

            if (cuidadorId == 0)
            {
                await Shell.Current.GoToAsync("//MainPage");
                return;
            }

            _ = RealtimeService.ConectarAsync(cuidadorId);
            _ = DeadManService.SincronizarAsync(cuidadorId);

            // Evita suscripciones duplicadas si OnAppearing se dispara más de una vez
            // sin un OnDisappearing intermedio (puede pasar con navegación "//").
            RealtimeService.NuevaSolicitud -= OnNuevaSolicitudTiempoReal;
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
            RealtimeService.NuevaSolicitud += OnNuevaSolicitudTiempoReal;
            RealtimeService.TrabajoActualizado += OnTrabajoActualizadoTiempoReal;
            // La visibilidad también se cambia desde el botón de la notificación persistente.
            EstadoCuidador.DisponibilidadCambiada -= OnDisponibilidadCambiadaFuera;
            EstadoCuidador.DisponibilidadCambiada += OnDisponibilidadCambiadaFuera;

            await CargarDashboard();
            _ = ActualizarUbicacionActualAsync();
            IniciarPollingUbicacionSiHaceFalta();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            this.AbortAnimation("PulsoDisponible");
            estaVisible = false;
            RealtimeService.NuevaSolicitud -= OnNuevaSolicitudTiempoReal;
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
            EstadoCuidador.DisponibilidadCambiada -= OnDisponibilidadCambiadaFuera;
        }

        private void OnDisponibilidadCambiadaFuera(bool disponible)
        {
            if (disponible == disponibleActual)
                return;
            disponibleActual = disponible;
            ActualizarUiDisponibilidad();
            if (disponibleActual)
                _ = ActualizarUbicacionActualAsync();
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
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_cargar_tu"), Localizador.T("ok"));
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
                EstadoCuidador.Establecer(disponibleActual);
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
                CardDisponibilidad.BackgroundColor = Tema.C("ColorSuccessSoft");
                IconoDisponibleFondo.BackgroundColor = Tema.C("ColorSuccess");
                IconoDisponible.Fill = Colors.White;
                LblDisponible.Text = Localizador.T("disponible_ahora");
                IniciarPulsoDisponible();
                LblDisponible.TextColor = Tema.C("ColorSuccess");
                LblDisponibleSubtitulo.Text = Localizador.T("los_clientes_pueden_verte_y");
            }
            else
            {
                CardDisponibilidad.BackgroundColor = Tema.C("ColorSurface");
                IconoDisponibleFondo.BackgroundColor = Tema.C("ColorSubtle");
                IconoDisponible.Fill = Tema.C("ColorChevron");
                LblDisponible.Text = Localizador.T("no_disponible");
                DetenerPulsoDisponible();
                LblDisponible.TextColor = (Color)Application.Current!.Resources["ColorTextStrong"];
                LblDisponibleSubtitulo.Text = Localizador.T("estas_desconectado_los_clientes_no");
            }
        }

        // Onda verde alrededor del ícono mientras el cuidador está disponible
        private void IniciarPulsoDisponible()
        {
            if (this.AnimationIsRunning("PulsoDisponible"))
                return;
            new Animation(t =>
            {
                PulsoDisponible.Scale = 1 + 0.5 * t;
                PulsoDisponible.Opacity = 0.8 * (1 - t);
            }, 0, 1).Commit(this, "PulsoDisponible", length: 1700, easing: Easing.CubicOut, repeat: () => true);
        }

        private void DetenerPulsoDisponible()
        {
            this.AbortAnimation("PulsoDisponible");
            PulsoDisponible.Opacity = 0;
        }

        private bool entradaHecha;

        private async Task AnimarEntradaAsync()
        {
            if (entradaHecha) return;
            entradaHecha = true;
            var bloques = new VisualElement[] { BloqueEncabezado, CardDisponibilidad, CardGanancias, BloqueProximo, BloqueAccesos };
            try
            {
                foreach (var v in bloques) { v.Opacity = 0; v.TranslationY = 16; }
                foreach (var v in bloques)
                {
                    _ = v.FadeTo(1, 300, Easing.CubicOut);
                    _ = v.TranslateTo(0, 0, 360, Easing.CubicOut);
                    await Task.Delay(60);
                }
                // La campana timbra una vez
                foreach (var angulo in new double[] { 14, -12, 8, -6, 0 })
                    await IconoCampana.RotateTo(angulo, 70, Easing.CubicInOut);
            }
            finally
            {
                foreach (var v in bloques) { v.Opacity = 1; v.TranslationY = 0; }
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
                EstadoCuidador.Establecer(disponibleActual);
                ActualizarUiDisponibilidad();

                if (disponibleActual)
                    _ = ActualizarUbicacionActualAsync();
            }
            else
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_actualizar_tu"), Localizador.T("ok"));
                // Revertir visualmente sin volver a llamar a la API.
                suprimirEventoToggle = true;
                SwitchDisponible.IsToggled = disponibleActual;
                suprimirEventoToggle = false;
            }
        }

        private async void OnProximoTrabajoTapped(object sender, EventArgs e)
        {
            await CardProximoTrabajo.ScaleTo(0.96, 80, Easing.CubicOut);
            _ = CardProximoTrabajo.ScaleTo(1, 160, Easing.CubicOut);
            await Shell.Current.GoToAsync("TrabajosPage");
        }

        private async void OnPerfilTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorPerfilPage");
        }

        private async void OnTrabajosTapped(object sender, EventArgs e)
        {
            await AccesoTrabajos.ScaleTo(0.96, 80, Easing.CubicOut);
            _ = AccesoTrabajos.ScaleTo(1, 160, Easing.CubicOut);
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
            var confirmar = await Alerta.MostrarAsync(Localizador.T("cerrar_sesion"), Localizador.T("estas_seguro_de_que_deseas"), Localizador.T("si"), Localizador.T("cancelar"));
            if (!confirmar)
                return;

            DeadManService.Detener();
            Recordatorios.CancelarTodos();
            Preferences.Default.Clear();
            await RealtimeService.DesconectarAsync();
            ConexionServiceManager.Detener();
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}
