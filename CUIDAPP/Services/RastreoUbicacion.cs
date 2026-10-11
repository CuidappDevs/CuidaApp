namespace CUIDAPP.Services
{
    /// <summary>
    /// Envía la ubicación del Care Partner (y la batería) cada 20 s mientras esté visible o con un servicio
    /// en curso, aunque la app esté en segundo plano (la notificación fija mantiene vivo el proceso).
    /// Así su punto se mueve en el mapa de los clientes y en el centro de mando del panel.
    /// Nunca pide permisos: si no hay permiso de ubicación, no envía nada.
    /// </summary>
    public static class RastreoUbicacion
    {
        private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(20);
        private static Timer? _timer;
        private static int _enCurso;

        public static void Iniciar()
        {
            if (_timer != null || Preferences.Default.Get("RolId", 0) != 3)
                return;
            _timer = new Timer(_ => _ = EnviarAsync(), null, TimeSpan.FromSeconds(3), Intervalo);
        }

        public static void Detener()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private const string ClaveAceptados = "ServiciosAceptadosInicio";

        /// <summary>Guarda las horas de inicio de los servicios aceptados (lo llama Recordatorios al sincronizar).</summary>
        public static void GuardarServiciosAceptados(IEnumerable<DateTime> inicios)
            => Preferences.Default.Set(ClaveAceptados, string.Join(",", inicios.Select(i => i.Ticks)));

        // Entre 2 h antes y 3 h después de la hora de un servicio aceptado, aunque esté oculto: así el
        // servidor puede avisarle al cliente cuando llega.
        private static bool VaEnCamino()
        {
            var ahora = DateTime.Now;
            foreach (var parte in Preferences.Default.Get(ClaveAceptados, "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (long.TryParse(parte, out var ticks))
                {
                    var inicio = new DateTime(ticks);
                    if (ahora >= inicio.AddHours(-2) && ahora <= inicio.AddHours(3))
                        return true;
                }
            return false;
        }

        /// <summary>Envía la ubicación ahora mismo (p. ej. al ponerse visible).</summary>
        public static Task EnviarAhoraAsync() => EnviarAsync();

        private static async Task EnviarAsync()
        {
            // Evita dos envíos encimados si el GPS tarda.
            if (Interlocked.Exchange(ref _enCurso, 1) == 1)
                return;
            try
            {
                var cuidadorId = Preferences.Default.Get("UserId", 0);
                if (cuidadorId == 0 || Preferences.Default.Get("RolId", 0) != 3)
                    return;
                if (!EstadoCuidador.Disponible && !DeadManService.Activo && !VaEnCamino())
                    return;

                var permiso = await MainThread.InvokeOnMainThreadAsync(() => Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>());
                if (permiso != PermissionStatus.Granted)
                    return;

                var ubicacion = await MainThread.InvokeOnMainThreadAsync(() =>
                    Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10))));
                if (ubicacion == null)
                    return;

                int? bateria = null;
                try
                {
                    var nivel = Battery.Default.ChargeLevel;
                    if (nivel >= 0) bateria = (int)Math.Round(nivel * 100);
                }
                catch { /* sin acceso a la batería */ }

                await new ApiService().ActualizarUbicacionCuidadorAsync(cuidadorId, ubicacion.Latitude, ubicacion.Longitude, bateria);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Rastreo] No se pudo enviar la ubicación: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _enCurso, 0);
            }
        }
    }
}
