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

        private void OnMensajeNuevoGlobal(Mensaje mensaje)
        {
            var miUsuarioId = Preferences.Default.Get("UserId", 0);
            if (mensaje.RemitenteId == miUsuarioId)
                return;

            var texto = mensaje.Tipo switch
            {
                "imagen" => Localizador.T("notif_foto"),
                "audio" => Localizador.T("notif_audio"),
                _ => mensaje.Contenido
            };

            NotificacionHistorial.Agregar(Localizador.T("nuevo_mensaje"), texto, "mensaje");

            // Si ya tiene esa misma conversación abierta, ChatPage se encarga de pintarlo
            // en vivo — no hace falta ni banner ni notificación del sistema encima.
            if (mensaje.ConversacionId == ChatPage.ConversacionAbiertaId)
                return;

            if (EstaEnPrimerPlano)
                GlobalNotifier.MostrarBanner(Localizador.T("nuevo_mensaje"), texto);
            else
                NativeNotifier.Mostrar(Localizador.T("nuevo_mensaje"), texto);
        }

        private void OnNuevaSolicitudGlobal(int trabajoId, int clienteId)
        {
            var miUsuarioId = Preferences.Default.Get("UserId", 0);
            if (miUsuarioId == 0)
                return;

            var texto = Localizador.T("un_cliente_solicito_tus_servicios");
            NotificacionHistorial.Agregar(Localizador.T("nueva_solicitud_de_servicio"), texto, "solicitud", trabajoId);

            if (EstaEnPrimerPlano)
                GlobalNotifier.MostrarBanner(Localizador.T("nueva_solicitud"), texto);
            else
                NativeNotifier.Mostrar(Localizador.T("nueva_solicitud_de_servicio"), texto);
        }

        private void OnTrabajoActualizadoGlobal(int trabajoId, int estado)
        {
            var texto = estado switch
            {
                2 => Localizador.T("est_aceptada"),
                3 => Localizador.T("est_en_progreso"),
                4 => Localizador.T("est_completado"),
                5 => Localizador.T("est_cancelado"),
                6 => Localizador.T("est_rechazada"),
                7 => Localizador.T("est_cuidador_termino"),
                _ => Localizador.T("est_actualizacion")
            };

            NotificacionHistorial.Agregar(Localizador.T("actualizacion_de_servicio"), texto, "trabajo", trabajoId);

            if (!EstaEnPrimerPlano)
                NativeNotifier.Mostrar(Localizador.T("actualizacion_de_servicio"), texto);
            // En primer plano no mostramos banner aquí: cada pantalla de detalle ya se
            // refresca sola y mostrar un banner encima sería redundante con eso.
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window();
            window.Page = new SplashPage(() => window.Page = new AppShell());

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
            };

            window.Stopped += (s, e) => EstaEnPrimerPlano = false;

            return window;
        }
    }
}
