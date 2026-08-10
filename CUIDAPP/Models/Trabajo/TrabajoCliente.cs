namespace CUIDAPP.Models.Trabajo
{
    public class TrabajoCliente
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public int CuidadorId { get; set; }
        public string CuidadorNombre { get; set; } = string.Empty;
        public string? CuidadorFotoUrl { get; set; }
        public string TipoServicio { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public string? Direccion { get; set; }
        public int Estado { get; set; } // 1=Pendiente,2=Aceptado,3=EnProgreso,4=Completado,5=Cancelado,6=Rechazado
        public decimal Tarifa { get; set; }
        public string? Notas { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string? PinInicio { get; set; }
    }
}
