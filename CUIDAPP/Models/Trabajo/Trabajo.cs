namespace CUIDAPP.Models.Trabajo
{
    public class Trabajo
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public string ClienteNombre { get; set; } = string.Empty;
        public string? ClienteFotoUrl { get; set; }
        public string TipoServicio { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public string? Direccion { get; set; }
        public int Estado { get; set; } // 1=Pendiente,2=Aceptado,3=EnProgreso,4=Completado,5=Cancelado,6=Rechazado
        public decimal Tarifa { get; set; }
        public string? Notas { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
