namespace CUIDAPP.Services
{
    // Notificación del sistema operativo (aparece en la barra/bandeja de Android), para
    // cuando el usuario tiene la app en segundo plano y no está mirando la pantalla —
    // ahí el banner in-app (GlobalNotifier) no sirve porque no hay nada visible.
    public static class NativeNotifier
    {
        private static int _proximoId = 1000;

        public static void Mostrar(string titulo, string mensaje)
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
                        var canal = new Android.App.NotificationChannel(canalId, "Mensajes y actividad", Android.App.NotificationImportance.High)
                        {
                            Description = "Notificaciones de chat, solicitudes y actualizaciones de trabajos"
                        };
                        manager.CreateNotificationChannel(canal);
                    }
                }

                var intent = contexto.PackageManager?.GetLaunchIntentForPackage(contexto.PackageName!);
                intent?.SetFlags(Android.Content.ActivityFlags.NewTask | Android.Content.ActivityFlags.ClearTop);
                var pendingIntent = Android.App.PendingIntent.GetActivity(contexto, 0, intent, Android.App.PendingIntentFlags.UpdateCurrent | Android.App.PendingIntentFlags.Immutable);

                var notificacion = new AndroidX.Core.App.NotificationCompat.Builder(contexto, canalId)
                    .SetContentTitle(titulo)
                    .SetContentText(mensaje)
                    .SetSmallIcon(_iconoResId)
                    .SetAutoCancel(true)
                    .SetPriority((int)Android.App.NotificationPriority.High)
                    .SetContentIntent(pendingIntent)
                    .Build();

                AndroidX.Core.App.NotificationManagerCompat.From(contexto).Notify(_proximoId++, notificacion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error mostrando notificación nativa: {ex.Message}");
            }
#endif
        }

#if ANDROID
        private static int _iconoResId => Android.App.Application.Context.ApplicationInfo?.Icon ?? global::Android.Resource.Drawable.SymDefAppIcon;
#endif
    }
}
