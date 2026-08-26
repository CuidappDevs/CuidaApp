namespace CUIDAPP.Services
{
    public static class ConexionServiceManager
    {
        public static void Iniciar()
        {
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

        public static void Detener()
        {
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
