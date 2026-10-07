using Android.Content;
using CUIDAPP.Services;

namespace CUIDAPP.Platforms.Android
{
    // Recibe las alarmas de Recordatorios (aunque la app esté cerrada) y muestra la notificación.
    [BroadcastReceiver(Enabled = true, Exported = false)]
    public class RecordatorioReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            var titulo = intent?.GetStringExtra("titulo");
            var texto = intent?.GetStringExtra("texto");
            if (string.IsNullOrWhiteSpace(titulo))
                return;

            NativeNotifier.Mostrar(titulo, texto ?? "", intent?.GetStringExtra(NotificacionDestino.ClaveExtra));
        }
    }
}
