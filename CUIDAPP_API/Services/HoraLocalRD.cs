namespace CUIDAPP_API.Services
{
    // El servidor donde corre esta API (y el de SQL Server) tiene el sistema operativo
    // configurado con la zona horaria del Pacífico (UTC-07:00) en vez de la de República
    // Dominicana (UTC-04:00, sin horario de verano). DateTime.Now hereda ese error de
    // configuración del SO; DateTime.UtcNow, en cambio, siempre es correcto sin importar
    // cómo esté configurada la zona horaria local de la máquina. Por eso calculamos la
    // hora de RD explícitamente a partir de UTC en vez de usar DateTime.Now directo.
    public static class HoraLocalRD
    {
        private static readonly TimeSpan OffsetRD = TimeSpan.FromHours(-4);

        public static DateTime Ahora => DateTime.UtcNow + OffsetRD;
    }
}
