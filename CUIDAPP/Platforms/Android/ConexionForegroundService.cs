using CUIDAPP.Localization;
using CUIDAPP.Services;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace CUIDAPP.Platforms.Android
{
    // Mantiene el proceso vivo con prioridad más alta que una app en segundo plano normal,
    // para que el socket de SignalR sobreviva más tiempo antes de que el sistema operativo
    // lo mate (Doze / ahorro de batería). No es tan confiable como un push real (FCM), pero
    // reduce bastante la ventana en la que las notificaciones no llegan.
    //
    // Su notificación es persistente ("CuidApp está ejecutándose") y, para el cuidador, trae un
    // botón para hacerse visible u ocultarse a los clientes sin abrir la app.
    [Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
    public class ConexionForegroundService : Service
    {
        private const int NotificacionId = 1;
        private const string CanalId = "cuidapp_activa";
        private const string CanalViejoId = "cuidapp_servicio";

        public const string AccionActualizar = "cuidapp.ACTUALIZAR";
        private const string AccionCambiarVisibilidad = "cuidapp.CAMBIAR_VISIBILIDAD";
        // Desde Android 14 el usuario puede deslizar la notificación de un servicio en primer plano:
        // si la quita, se vuelve a mostrar mientras la app siga en ejecución.
        private const string AccionDescartada = "cuidapp.DESCARTADA";

        private bool cambiando;

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            CrearCanal();

            if (intent?.Action == AccionCambiarVisibilidad && !cambiando)
            {
                _ = CambiarVisibilidadAsync();
            }

            StartForeground(NotificacionId, CrearNotificacion());
            return StartCommandResult.Sticky;
        }

        private async Task CambiarVisibilidadAsync()
        {
            cambiando = true;
            Mostrar(CrearNotificacion());
            var ok = await EstadoCuidador.CambiarAsync(!EstadoCuidador.Disponible);
            cambiando = false;
            Mostrar(CrearNotificacion(error: !ok));
        }

        private void Mostrar(Notification notificacion)
        {
            var manager = (NotificationManager)GetSystemService(NotificationService)!;
            manager.Notify(NotificacionId, notificacion);
        }

        private void CrearCanal()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var manager = (NotificationManager)GetSystemService(NotificationService)!;
            if (manager.GetNotificationChannel(CanalViejoId) != null)
                manager.DeleteNotificationChannel(CanalViejoId);

            if (manager.GetNotificationChannel(CanalId) == null)
            {
                // Importancia baja: se ve en la barra y muestra el botón, pero sin sonido ni vibración.
                var canal = new NotificationChannel(CanalId, Localizador.T("canal_conexion_activa"), NotificationImportance.Low)
                {
                    Description = Localizador.T("mantiene_la_app_conectada_para")
                };
                canal.SetShowBadge(false);
                manager.CreateNotificationChannel(canal);
            }
        }

        private Notification CrearNotificacion(bool error = false)
        {
            var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;

            // Tocar la notificación abre la app.
            var abrir = PackageManager?.GetLaunchIntentForPackage(PackageName ?? "");
            var alTocar = abrir != null ? PendingIntent.GetActivity(this, 0, abrir, flags) : null;

            var alDescartar = PendingIntent.GetService(this, 2,
                new Intent(this, typeof(ConexionForegroundService)).SetAction(AccionDescartada), flags);

            var builder = new NotificationCompat.Builder(this, CanalId)
                .SetContentTitle(Localizador.T("cuidapp_esta_activo"))
                .SetContentText(Localizador.T("recibiendo_avisos_tiempo_real"))
                // Silueta blanca del logo (Resources/drawable/ic_notificacion.xml) y tarjeta en el azul de la marca.
                .SetSmallIcon(Resource.Drawable.ic_notificacion)
                .SetColor(global::Android.Graphics.Color.ParseColor("#1C4D96"))
                .SetColorized(true)
                .SetOngoing(true)
                .SetOnlyAlertOnce(true)
                .SetShowWhen(false)
                .SetCategory(NotificationCompat.CategoryService)
                .SetPriority(NotificationCompat.PriorityLow)
                .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
                .SetDeleteIntent(alDescartar);

            if (alTocar != null)
                builder.SetContentIntent(alTocar);

            if (EstadoCuidador.EsCuidador)
            {
                bool visible = EstadoCuidador.Disponible;
                bool aprobada = EstadoCuidador.CuentaAprobada;
                var estado = !aprobada ? Localizador.T("notif_perfil_en_validacion")
                           : cambiando ? Localizador.T("notif_actualizando_visibilidad")
                           : error ? Localizador.T("notif_error_visibilidad")
                           : Localizador.T(visible ? "notif_visible_clientes" : "notif_oculto_clientes");

                // El estado va solo en la cabecera (junto a "CuidApp"); el cuerpo queda con el lema.
                builder.SetSubText(estado)
                       .SetStyle(new NotificationCompat.BigTextStyle().BigText(Localizador.T("recibiendo_avisos_tiempo_real")));

                if (!cambiando && aprobada)
                {
                    var alCambiar = PendingIntent.GetService(this, 1,
                        new Intent(this, typeof(ConexionForegroundService)).SetAction(AccionCambiarVisibilidad), flags);
                    builder.AddAction(0, Localizador.T(visible ? "notif_ocultarme" : "notif_hacerme_visible"), alCambiar);
                }
            }

            return builder.Build()!;
        }
    }
}
