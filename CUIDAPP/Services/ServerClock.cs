namespace CUIDAPP.Services
{
    // Fuente de verdad para "qué hora/fecha es" en toda la app. El reloj del dispositivo
    // puede estar mal configurado (visto en producción: un día adelantado), y como las
    // validaciones críticas (PIN de inicio/salida) comparan contra la hora del servidor
    // SQL, usar DateTime.Now/Today directamente generaba bloqueos que parecían "sin razón".
    //
    // Se sincroniza una vez (offset entre el reloj del servidor y el del dispositivo) y
    // desde ahí Now/Today se calculan localmente sumando ese offset — no hay que llamar
    // a la API cada vez que se necesita la hora.
    public static class ServerClock
    {
        private static TimeSpan? _offset;

        public static DateTime Now => _offset.HasValue ? DateTime.Now + _offset.Value : DateTime.Now;
        public static DateTime Today => Now.Date;
        public static bool EstaSincronizado => _offset.HasValue;

        public static async Task SincronizarAsync()
        {
            try
            {
                var apiService = new ApiService();
                var horaServidor = await apiService.ObtenerHoraServidorAsync();
                if (horaServidor.HasValue)
                    _offset = horaServidor.Value - DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sincronizando reloj del servidor: {ex.Message}");
            }
        }
    }
}
