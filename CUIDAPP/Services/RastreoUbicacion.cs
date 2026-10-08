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
                if (!EstadoCuidador.Disponible && !DeadManService.Activo)
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
