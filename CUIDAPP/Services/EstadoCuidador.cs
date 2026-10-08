namespace CUIDAPP.Services
{
    /// <summary>
    /// Disponibilidad del cuidador ("visible para clientes"), compartida entre el panel y la
    /// notificación persistente de Android: cambie donde cambie, los dos quedan iguales.
    /// </summary>
    public static class EstadoCuidador
    {
        private const string ClavePreferencia = "CuidadorDisponible";

        /// <summary>Se dispara (en el hilo principal) cuando la disponibilidad cambia.</summary>
        public static event Action<bool>? DisponibilidadCambiada;

        public static bool EsCuidador => Preferences.Default.Get("RolId", 0) == 3;

        public static bool Disponible => Preferences.Default.Get(ClavePreferencia, false);

        /// <summary>
        /// Cuenta aprobada por la administración (PerfilCuidador.EstadoAprobacion = 2). Con la cuenta pendiente
        /// puede entrar a su panel, pero no puede ponerse visible para los clientes.
        /// </summary>
        public static bool CuentaAprobada => Preferences.Default.Get("CuentaAprobada", false);

        /// <summary>
        /// Horario automático activo: el servidor lo pone visible / oculto según su horario y el
        /// interruptor manual (panel y notificación) queda bloqueado.
        /// </summary>
        public static bool HorarioAutomatico => Preferences.Default.Get("HorarioAutomatico", false);

        public static void EstablecerHorarioAutomatico(bool activo)
        {
            if (HorarioAutomatico == activo) return;
            Preferences.Default.Set("HorarioAutomatico", activo);
            ConexionServiceManager.Actualizar();
        }

        public static void EstablecerCuentaAprobada(bool aprobada)
        {
            if (CuentaAprobada == aprobada) return;
            Preferences.Default.Set("CuentaAprobada", aprobada);
            ConexionServiceManager.Actualizar();
        }

        /// <summary>Registra un valor ya confirmado por el servidor (al cargar el perfil o tras el switch del panel).</summary>
        public static void Establecer(bool disponible)
        {
            var cambio = Disponible != disponible;
            Preferences.Default.Set(ClavePreferencia, disponible);
            ConexionServiceManager.Actualizar();
            if (cambio)
                MainThread.BeginInvokeOnMainThread(() => DisponibilidadCambiada?.Invoke(disponible));
        }

        /// <summary>Cambia la disponibilidad en el servidor (botón de la notificación). False si falla.</summary>
        public static async Task<bool> CambiarAsync(bool disponible)
        {
            var cuidadorId = Preferences.Default.Get("UserId", 0);
            if (cuidadorId == 0 || HorarioAutomatico || (disponible && !CuentaAprobada))
                return false;

            var ok = await new ApiService().ActualizarDisponibilidadAsync(cuidadorId, disponible);
            if (ok)
                Establecer(disponible);
            return ok;
        }
    }
}
