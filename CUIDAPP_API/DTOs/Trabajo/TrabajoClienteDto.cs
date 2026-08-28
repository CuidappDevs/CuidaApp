namespace CUIDAPP_API.DTOs.Trabajo
{
    public class TrabajoClienteDto
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public int CuidadorId { get; set; }
        public required string CuidadorNombre { get; set; }
        public string? CuidadorFotoUrl { get; set; }
        public required string TipoServicio { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public string? Direccion { get; set; }
        public int Estado { get; set; } // 1=Pendiente,2=Aceptado,3=EnProgreso,4=Completado,5=Cancelado,6=Rechazado,7=EsperandoConfirmacionCliente
        public decimal Tarifa { get; set; }
        public string? Notas { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string? PinInicio { get; set; }
        public string? PinFin { get; set; }
        public DateTime? FechaInicioReal { get; set; }
        public string? JustificacionFinalizacion { get; set; }
        public bool RechazadoPorCliente { get; set; }
    }
}
