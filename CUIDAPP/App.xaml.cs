using CUIDAPP.Localization;
using Microsoft.Extensions.DependencyInjection;
using CUIDAPP.Models.Chat;
using CUIDAPP.Services;
using CUIDAPP.Views.Chat;
using CUIDAPP.Views.Splash;

namespace CUIDAPP
{
    public partial class App : Application
    {
        // Mientras la app esté al frente, un mensaje/evento nuevo se muestra como banner
        // in-app (GlobalNotifier). En segundo plano no hay nada visible que animar, así
        // que ahí se dispara una notificación real del sistema operativo (NativeNotifier).
        public static bool EstaEnPrimerPlano { get; private set; } = true;

        public App()
        {
            InitializeComponent();

            // La app está diseñada solo para tema claro (fondos blancos, textos oscuros
            // explícitos). Sin esto, en un dispositivo con modo oscuro activado, controles
            // nativos como Entry/Editor usan su color de texto por defecto del sistema
            // (blanco), que sobre nuestros fondos blancos se vuelve invisible.
            UserAppTheme = AppTheme.Light;

            // Red de seguridad: si algo revienta sin try/catch en un handler async void,
            // lo dejamos registrado en el Output de Visual Studio en vez de que la app crashee
            // silenciosamente sin dejar rastro del motivo real.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                System.Diagnostics.Debug.WriteLine($"[UnhandledException] {e.ExceptionObject}");

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] {e.Exception}");
                e.SetObserved();
            };

            // Suscripciones únicas para toda la vida de la app: avisan de eventos sin
            // importar en qué pantalla esté el usuario (o si la app está en segundo plano).
            RealtimeService.MensajeNuevo += OnMensajeNuevoGlobal;
            RealtimeService.NuevaSolicitud += OnNuevaSolicitudGlobal;
            RealtimeService.TrabajoActualizado += OnTrabajoActualizadoGlobal;
            RealtimeService.ActividadAgregada += OnActividadAgregadaGlobal;
            RealtimeService.AlertaGeocerca += OnAlertaGeocercaGlobal;
            RealtimeService.TareaCompletada += OnTareaCompletadaGlobal;
            RealtimeService.PropinaRecibida += OnPropinaRecibidaGlobal;
            RealtimeService.CuentaActualizada += estado => AvisosApp.CuentaActualizada(estado);
            RealtimeService.PagoAprobado += (trabajoId, monto) => AvisosApp.PagoAprobado(trabajoId, monto);
            RealtimeService.TicketActualizado += (ticketId, respuesta, estado, asunto) => AvisosApp.TicketActualizado(ticketId, respuesta, estado, asunto);
        }

        private void OnPropinaRecibidaGlobal(int trabajoId, decimal monto)
        {
            // Solo el cuidador recibe este evento.
            if (Preferences.Default.Get("RolId", 0) != 3)
                return;

            var titulo = Localizador.T("notif_propina_titulo");
            var texto = Localizador.F("notif_propina_texto", monto);
            NotificacionHistorial.Agregar(titulo, texto, "trabajo", trabajoId);

            if (EstaEnPrimerPlano)
                GlobalNotifier.MostrarBanner(titulo, texto);
            else
                NativeNotifier.Mostrar(titulo, texto);
        }

        private void OnTareaCompletadaGlobal(int trabajoId, int tareaId, string descripcion)
        {
            // Solo el cliente recibe este evento; el cuidador es quien la marca.
            if (Preferences.Default.Get("RolId", 0) == 3)
                return;

            var texto = Localizador.F("tarea_completada_detalle", descripcion);
            NotificacionHistorial.Agregar(Localizador.T("tu_cuidador_completo_una_tarea"), texto, "trabajo", trabajoId);

            if (EstaEnPrimerPlano)
                GlobalNotifier.MostrarBanner(Localizador.T("tarea_completada"), texto);
            else
                NativeNotifier.Mostrar(Localizador.T("tu_cuidador_completo_una_tarea"), texto);
        }

        private void OnActividadAgregadaGlobal(int trabajoId, string descripcion, DateTime fechaHora)
        {
            // Solo le interesa al cliente (el cuidador es quien reporta, no necesita
            // que le avisen de su propio reporte).
            var rolId = Preferences.Default.Get("RolId", 0);
            if (rolId == 3)
                return;

            NotificacionHistorial.Agregar(Localizador.T("tu_cuidador_reporto_una_actividad"), descripcion, "trabajo", trabajoId);

            if (EstaEnPrimerPlano)
                GlobalNotifier.MostrarBanner(Localizador.T("actividad_reportada"), descripcion);
            else
                NativeNotifier.Mostrar(Localizador.T("tu_cuidador_reporto_una_actividad"), descripcion);
        }

        private void OnAlertaGeocercaGlobal(int trabajoId, double distanciaMetros)
        {
            var texto = Localizador.F("alejo_m_domicilio_servicio", distanciaMetros);
            NotificacionHistorial.Agregar(Localizador.T("tu_cuidador_se_alejo_del"), texto, "geocerca", trabajoId);

            if (!EstaEnPrimerPlano)
                NativeNotifier.Mostrar(Localizador.T("tu_cuidador_se_alejo_del"), texto);
        }

        private void OnMensajeNuevoGlobal(Mensaje mensaje) => _ = AvisosApp.MensajeNuevoAsync(mensaje);

        private void OnNuevaSolicitudGlobal(int trabajoId, int clienteId) => _ = AvisosApp.NuevaSolicitudAsync(trabajoId);

        private void OnTrabajoActualizadoGlobal(int trabajoId, int estado)
        {
            // Cuidador: el monitoreo de caídas solo corre mientras haya un servicio En Progreso.
            if (Preferences.Default.Get("RolId", 0) == 3)
                _ = DeadManService.SincronizarAsync(Preferences.Default.Get("UserId", 0));

            _ = AvisosApp.TrabajoActualizadoAsync(trabajoId, estado);
        }

        /// <summary>
        /// Tras el splash: si hay una sesión guardada (y hay internet) va directo al panel del
        /// usuario; si no, muestra el login. "Cerrar sesión" borra la sesión guardada.
        /// </summary>
        private static async Task AbrirAppAsync(Window window)
        {
            var ruta = Helpers.SesionGuardada.RutaDeInicio();
            CUIDAPP.MainPage.OcultarAlAbrir = ruta != null; // evita que el login se vea un instante

            var shell = new AppShell();
            window.Page = shell;

            if (ruta == null)
                return;

            try
            {
                Helpers.SesionGuardada.IniciarServicios();
                await shell.GoToAsync(ruta, animate: false);
                await NotificacionDestino.ProcesarPendienteAsync();
            }
            catch (Exception ex)
            {
                // Si algo falla al restaurar la sesión, se queda en el login.
                Console.WriteLine($"[App] No se pudo abrir la sesión guardada: {ex}");
            }
            finally
            {
                CUIDAPP.MainPage.OcultarAlAbrir = false;
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window();
            window.Page = new SplashPage(() => _ = AbrirAppAsync(window));

            window.Activated += (s, e) => EstaEnPrimerPlano = true;
            window.Deactivated += (s, e) => EstaEnPrimerPlano = false;

            // Si el sistema operativo suspendió la app (pantalla apagada, cambio de app,
            // etc.), el socket de SignalR puede haber muerto sin que ConectarAsync se
            // vuelva a llamar desde ninguna pantalla. Al volver al primer plano, forzamos
            // una reconexión (ConectarAsync ya es un no-op si sigue viva).
            window.Resumed += (s, e) =>
            {
                EstaEnPrimerPlano = true;
                var usuarioId = Preferences.Default.Get("UserId", 0);
                if (usuarioId != 0)
                    _ = RealtimeService.ConectarAsync(usuarioId);
                DeadManService.MostrarSiPendiente();
            };

            window.Stopped += (s, e) => EstaEnPrimerPlano = false;

            return window;
        }
    }
}
