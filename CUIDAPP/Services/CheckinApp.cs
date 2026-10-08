using CUIDAPP.Localization;

namespace CUIDAPP.Services
{
    /// <summary>
    /// "¿Estás bien?" enviado desde el centro de mando del panel. Se guarda como pendiente: si la app está
    /// abierta se muestra al instante; si no, llega como notificación y se muestra al volver a la app.
    /// Si la persona no responde a tiempo, el servidor crea una alerta SOS.
    /// </summary>
    public static class CheckinApp
    {
        private const string Clave = "CheckinPendiente"; // "id|vence(ticks utc)|mensaje"
        private static bool _mostrando;

        public static void Recibir(int id, string mensaje, int minutos)
        {
            var vence = DateTime.UtcNow.AddMinutes(minutos <= 0 ? 5 : minutos);
            Preferences.Default.Set(Clave, $"{id}|{vence.Ticks}|{mensaje}");
            NotificacionHistorial.Agregar(Localizador.T("checkin_titulo"), mensaje, "aviso", null);

            if (App.EstaEnPrimerPlano)
                MainThread.BeginInvokeOnMainThread(async () => await MostrarPendienteAsync());
            else
                NativeNotifier.Mostrar(Localizador.T("checkin_titulo"), mensaje, $"checkin:{id}");
        }

        /// <summary>Muestra la pregunta si hay una pendiente y vigente. Se llama al volver a la app y desde la notificación.</summary>
        public static async Task MostrarPendienteAsync()
        {
            if (_mostrando || Shell.Current?.Navigation is not { } navegacion)
                return;
            var partes = Preferences.Default.Get(Clave, "").Split('|', 3);
            if (partes.Length < 3 || !int.TryParse(partes[0], out var id) || !long.TryParse(partes[1], out var ticks))
                return;
            if (new DateTime(ticks, DateTimeKind.Utc) <= DateTime.UtcNow)
            {
                Preferences.Default.Remove(Clave); // venció: el servidor ya creó el SOS
                return;
            }

            _mostrando = true;
            try
            {
                var pagina = new Views.Comun.CheckinPage(id, partes[2], new DateTime(ticks, DateTimeKind.Utc));
                await navegacion.PushModalAsync(pagina, true);
                await pagina.Respondida;
                Preferences.Default.Remove(Clave);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Checkin] {ex.Message}");
            }
            finally
            {
                _mostrando = false;
            }
        }
    }
}
