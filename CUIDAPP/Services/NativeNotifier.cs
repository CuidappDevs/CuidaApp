using CUIDAPP.Localization;
namespace CUIDAPP.Services
{
    // Notificación del sistema operativo (aparece en la barra/bandeja de Android), para
    // cuando el usuario tiene la app en segundo plano y no está mirando la pantalla —
    // ahí el banner in-app (GlobalNotifier) no sirve porque no hay nada visible.
    public static class NativeNotifier
    {
        private static int _proximoId = 1000;

        public static void Mostrar(string titulo, string mensaje, string? destino = null)
        {
#if ANDROID
            try
            {
                var contexto = Android.App.Application.Context;
                const string canalId = "cuidapp_mensajes";

                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
                {
                    var manager = (Android.App.NotificationManager)contexto.GetSystemService(Android.Content.Context.NotificationService)!;
                    if (manager.GetNotificationChannel(canalId) == null)
                    {
                        var canal = new Android.App.NotificationChannel(canalId, Localizador.T("canal_mensajes_actividad"), Android.App.NotificationImportance.High)
                        {
                            Description = Localizador.T("notificaciones_de_chat_solicitudes_y")
                        };
                        manager.CreateNotificationChannel(canal);
                    }
                }

                var id = _proximoId++;
                var intent = contexto.PackageManager?.GetLaunchIntentForPackage(contexto.PackageName!);
                intent?.SetFlags(Android.Content.ActivityFlags.NewTask | Android.Content.ActivityFlags.SingleTop);
                // Al tocarla abre la pantalla correspondiente (NotificacionDestino). Cada notificación
                // usa su propio código para que no se pisen los destinos entre sí.
                if (!string.IsNullOrWhiteSpace(destino))
                    intent?.PutExtra(NotificacionDestino.ClaveExtra, destino);
                var pendingIntent = Android.App.PendingIntent.GetActivity(contexto, id, intent, Android.App.PendingIntentFlags.UpdateCurrent | Android.App.PendingIntentFlags.Immutable);

                var notificacion = new AndroidX.Core.App.NotificationCompat.Builder(contexto, canalId)
                    .SetContentTitle(titulo)
                    .SetContentText(mensaje)
                    .SetStyle(new AndroidX.Core.App.NotificationCompat.BigTextStyle().BigText(mensaje))
                    .SetSmallIcon(_iconoResId)
                    .SetColor(Android.Graphics.Color.ParseColor("#1C4D96"))
                    .SetAutoCancel(true)
                    .SetPriority((int)Android.App.NotificationPriority.High)
                    .SetContentIntent(pendingIntent)
                    .Build();

                AndroidX.Core.App.NotificationManagerCompat.From(contexto).Notify(id, notificacion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error mostrando notificación nativa: {ex.Message}");
            }
#endif
        }

#if ANDROID
        // Silueta blanca del logo: Android pinta como círculo genérico los íconos a color.
        private static int _iconoResId => CUIDAPP.Resource.Drawable.ic_notificacion;
#endif
    }
}
