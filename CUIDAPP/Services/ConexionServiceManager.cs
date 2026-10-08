namespace CUIDAPP.Services
{
    public static class ConexionServiceManager
    {
        public static void Iniciar()
        {
            RastreoUbicacion.Iniciar();
#if ANDROID
            try
            {
                var contexto = global::Android.App.Application.Context;
                var intent = new global::Android.Content.Intent(contexto, typeof(Platforms.Android.ConexionForegroundService));

                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                    contexto.StartForegroundService(intent);
                else
                    contexto.StartService(intent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error iniciando servicio de conexión: {ex.Message}");
            }
#endif
        }

        /// <summary>Vuelve a pintar la notificación persistente (p. ej. cambió la disponibilidad del cuidador).</summary>
        public static void Actualizar()
        {
#if ANDROID
            if (Preferences.Default.Get("UserId", 0) == 0)
                return;
            try
            {
                var contexto = global::Android.App.Application.Context;
                var intent = new global::Android.Content.Intent(contexto, typeof(Platforms.Android.ConexionForegroundService))
                    .SetAction(Platforms.Android.ConexionForegroundService.AccionActualizar);

                if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
                    contexto.StartForegroundService(intent);
                else
                    contexto.StartService(intent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando la notificación del servicio: {ex.Message}");
            }
#endif
        }

        public static void Detener()
        {
            RastreoUbicacion.Detener();
#if ANDROID
            try
            {
                var contexto = global::Android.App.Application.Context;
                var intent = new global::Android.Content.Intent(contexto, typeof(Platforms.Android.ConexionForegroundService));
                contexto.StopService(intent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deteniendo servicio de conexión: {ex.Message}");
            }
#endif
        }
    }
}
