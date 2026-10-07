using CUIDAPP.Services;

namespace CUIDAPP.Helpers
{
    /// <summary>
    /// Sesión recordada: si el usuario ya inició sesión antes (y no la cerró), al abrir la app
    /// se va directo a su panel en lugar del login. Los datos los guarda el login en Preferences
    /// y "Cerrar sesión" los borra (Preferences.Clear).
    /// </summary>
    public static class SesionGuardada
    {
        /// <summary>
        /// Ruta del panel al que hay que ir al abrir la app, o null si hay que mostrar el login:
        /// no hay sesión guardada, el rol no tiene panel en la app, o no hay internet.
        /// </summary>
        public static string? RutaDeInicio()
        {
            var usuarioId = Preferences.Default.Get("UserId", 0);
            var token = Preferences.Default.Get("AuthToken", "");
            if (usuarioId == 0 || string.IsNullOrWhiteSpace(token))
                return null;

            // Sin internet no se puede trabajar con la cuenta: se muestra el login.
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                return null;

            return Preferences.Default.Get("RolId", 0) switch
            {
                3 => Preferences.Default.Get("EstadoAprobacion", 0) == 2 ? "CuidadorDashboardPage" : "VerificacionPendientePage",
                2 => "ClienteDashboardPage",
                _ => null
            };
        }

        /// <summary>Arranca los mismos servicios que arranca el login al entrar.</summary>
        public static void IniciarServicios()
        {
            var usuarioId = Preferences.Default.Get("UserId", 0);
            _ = RealtimeService.ConectarAsync(usuarioId);
            _ = ServerClock.SincronizarAsync();
            _ = Recordatorios.SincronizarServiciosAsync();
            ConexionServiceManager.Iniciar();
        }
    }
}
