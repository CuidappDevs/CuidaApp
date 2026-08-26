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
    [Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
    public class ConexionForegroundService : Service
    {
        private const int NotificacionId = 1;
        private const string CanalId = "cuidapp_servicio";

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            var manager = (NotificationManager)GetSystemService(NotificationService)!;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O && manager.GetNotificationChannel(CanalId) == null)
            {
                var canal = new NotificationChannel(CanalId, "Conexión activa", NotificationImportance.Min)
                {
                    Description = "Mantiene la app conectada para recibir avisos al instante"
                };
                manager.CreateNotificationChannel(canal);
            }

            var notificacion = new NotificationCompat.Builder(this, CanalId)
                .SetContentTitle("CuidApp está activo")
                .SetContentText("Recibiendo mensajes y avisos en tiempo real")
                .SetSmallIcon(ApplicationInfo?.Icon ?? global::Android.Resource.Drawable.SymDefAppIcon)
                .SetOngoing(true)
                .SetPriority(NotificationCompat.PriorityMin)
                .Build();

            StartForeground(NotificacionId, notificacion);

            return StartCommandResult.Sticky;
        }
    }
}
