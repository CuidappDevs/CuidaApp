using CUIDAPP.Localization;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace CUIDAPP.Platforms.Android
{
    // Caja negra: mantiene vivo el proceso para poder grabar con la app en segundo plano Y muestra una
    // notificación VISIBLE mientras dura ("Grabación de seguridad activa"). Tipo de servicio "microphone":
    // en Android 11+ es obligatorio para usar el micrófono en segundo plano, y hace que el sistema muestre
    // el punto verde del micrófono. No es una grabación oculta: el usuario siempre la ve.
    [Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMicrophone)]
    public class GrabacionForegroundService : Service
    {
        private const int NotificacionId = 7;
        private const string CanalId = "cuidapp_grabacion";

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            CrearCanal();
            var notificacion = CrearNotificacion();
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                try { StartForeground(NotificacionId, notificacion, global::Android.Content.PM.ForegroundService.TypeMicrophone); }
                catch (Exception ex) { Console.WriteLine($"[Grabacion] StartForeground microphone: {ex.Message}"); StartForeground(NotificacionId, notificacion); }
            }
            else
            {
                StartForeground(NotificacionId, notificacion);
            }
            return StartCommandResult.Sticky;
        }

        private void CrearCanal()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;
            var manager = (NotificationManager)GetSystemService(NotificationService)!;
            if (manager.GetNotificationChannel(CanalId) == null)
            {
                var canal = new NotificationChannel(CanalId, Localizador.T("canal_grabacion"), NotificationImportance.Low)
                {
                    Description = Localizador.T("canal_grabacion_desc")
                };
                canal.SetShowBadge(false);
                manager.CreateNotificationChannel(canal);
            }
        }

        private Notification CrearNotificacion()
        {
            var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
            var abrir = PackageManager?.GetLaunchIntentForPackage(PackageName ?? "");
            var alTocar = abrir != null ? PendingIntent.GetActivity(this, 0, abrir, flags) : null;

            return new NotificationCompat.Builder(this, CanalId)
                .SetContentTitle(Localizador.T("grabacion_activa_titulo"))
                .SetContentText(Localizador.T("grabacion_activa_texto"))
                .SetSmallIcon(Resource.Drawable.ic_notificacion)
                .SetColor(global::Android.Graphics.Color.ParseColor("#1C4D96"))
                .SetColorized(true)
                .SetOngoing(true)
                .SetOnlyAlertOnce(true)
                .SetShowWhen(false)
                .SetCategory(NotificationCompat.CategoryService)
                .SetPriority(NotificationCompat.PriorityLow)
                .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
                .SetContentIntent(alTocar)
                .Build();
        }
    }
}
