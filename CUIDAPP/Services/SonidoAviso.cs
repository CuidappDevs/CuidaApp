namespace CUIDAPP.Services
{
    /// <summary>Sonido propio de CuidApp para avisos y para mensajes de chat.</summary>
    public enum TipoSonido { Aviso, Mensaje }

    /// <summary>
    /// Sonidos de notificación de CuidApp (Platforms/Android/Resources/raw/cuidapp_aviso.wav y cuidapp_mensaje.wav):
    /// - Aviso: "chime" de dos notas (solicitudes, estado del servicio, recordatorios, pagos…).
    /// - Mensaje: "pop" corto (chat).
    /// Las notificaciones nativas los usan a través de su canal; el banner con la app abierta los
    /// reproduce aquí. Se usa el canal de notificaciones del sistema, así que respeta el modo silencio.
    /// </summary>
    public static class SonidoAviso
    {
        private static DateTime _ultimo = DateTime.MinValue;

        public static void Reproducir(TipoSonido tipo)
        {
#if ANDROID
            try
            {
                // Evita que varios avisos seguidos se encimen en un ruido.
                if ((DateTime.Now - _ultimo).TotalMilliseconds < 700)
                    return;
                _ultimo = DateTime.Now;

                var contexto = Android.App.Application.Context;
                var audio = (Android.Media.AudioManager?)contexto.GetSystemService(Android.Content.Context.AudioService);
                if (audio != null && audio.RingerMode != Android.Media.RingerMode.Normal)
                    return; // silencio o vibración: no suena

                var tono = Android.Media.RingtoneManager.GetRingtone(contexto, Uri(tipo));
                if (tono == null)
                    return;
                tono.AudioAttributes = new Android.Media.AudioAttributes.Builder()
                    .SetUsage(Android.Media.AudioUsageKind.Notification)!
                    .SetContentType(Android.Media.AudioContentType.Sonification)!
                    .Build();
                tono.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sonido] {ex.Message}");
            }
#endif
        }

#if ANDROID
        public static Android.Net.Uri Uri(TipoSonido tipo)
        {
            var contexto = Android.App.Application.Context;
            var recurso = tipo == TipoSonido.Mensaje ? CUIDAPP.Resource.Raw.cuidapp_mensaje : CUIDAPP.Resource.Raw.cuidapp_aviso;
            return Android.Net.Uri.Parse($"android.resource://{contexto.PackageName}/{recurso}")!;
        }
#endif
    }
}
