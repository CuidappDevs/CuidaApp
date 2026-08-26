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
        }

        private void OnActividadAgregadaGlobal(int trabajoId, string descripcion, DateTime fechaHora)
        {
            // Solo le interesa al cliente (el cuidador es quien reporta, no necesita
            // que le avisen de su propio reporte).
            var rolId = Preferences.Default.Get("RolId", 0);
            if (rolId == 3)
                return;

            NotificacionHistorial.Agregar("Tu cuidador reportó una actividad", descripcion, "trabajo", trabajoId);

            if (EstaEnPrimerPlano)
                GlobalNotifier.MostrarBanner("Actividad reportada", descripcion);
            else
                NativeNotifier.Mostrar("Tu cuidador reportó una actividad", descripcion);
        }

        private void OnAlertaGeocercaGlobal(int trabajoId, double distanciaMetros)
        {
            var texto = $"Se alejó {distanciaMetros:N0} m del domicilio donde se realiza el servicio.";
            NotificacionHistorial.Agregar("⚠️ Tu cuidador se alejó del sitio", texto, "geocerca", trabajoId);

            if (!EstaEnPrimerPlano)
                NativeNotifier.Mostrar("⚠️ Tu cuidador se alejó del sitio", texto);
        }

        private void OnMensajeNuevoGlobal(Mensaje mensaje)
        {
            var miUsuarioId = Preferences.Default.Get("UserId", 0);
            if (mensaje.RemitenteId == miUsuarioId)
                return;

            var texto = mensaje.Tipo switch
            {
                "imagen" => "📷 Te enviaron una foto",
                "audio" => "🎤 Te enviaron una nota de voz",
                _ => mensaje.Contenido
            };

            NotificacionHistorial.Agregar("Nuevo mensaje", texto, "mensaje");

            // Si ya tiene esa misma conversación abierta, ChatPage se encarga de pintarlo
            // en vivo — no hace falta ni banner ni notificación del sistema encima.
            if (mensaje.ConversacionId == ChatPage.ConversacionAbiertaId)
                return;

            if (EstaEnPrimerPlano)
                GlobalNotifier.MostrarBanner("Nuevo mensaje", texto);
            else
                NativeNotifier.Mostrar("Nuevo mensaje", texto);
        }

        private void OnNuevaSolicitudGlobal(int trabajoId, int clienteId)
        {
            var miUsuarioId = Preferences.Default.Get("UserId", 0);
            if (miUsuarioId == 0)
                return;

            const string texto = "Un cliente solicitó tus servicios. Revisa los detalles.";
            NotificacionHistorial.Agregar("Nueva solicitud de servicio", texto, "solicitud", trabajoId);

            if (EstaEnPrimerPlano)
                GlobalNotifier.MostrarBanner("Nueva solicitud", texto);
            else
                NativeNotifier.Mostrar("Nueva solicitud de servicio", texto);
        }

        private void OnTrabajoActualizadoGlobal(int trabajoId, int estado)
        {
            var texto = estado switch
            {
                2 => "Tu solicitud fue aceptada.",
                3 => "El servicio ya está en progreso.",
                4 => "¡El servicio fue completado!",
                5 => "El servicio fue cancelado.",
                6 => "Tu solicitud fue rechazada.",
                7 => "Tu cuidador indicó que el servicio terminó. Confírmalo.",
                _ => "Hay una actualización en tu servicio."
            };

            NotificacionHistorial.Agregar("Actualización de servicio", texto, "trabajo", trabajoId);

            if (!EstaEnPrimerPlano)
                NativeNotifier.Mostrar("Actualización de servicio", texto);
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
