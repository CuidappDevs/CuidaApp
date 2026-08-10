namespace CUIDAPP_API.DTOs.Trabajo
{
    public class CrearTrabajoDto
    {
        public int ClienteId { get; set; }
        public int CuidadorId { get; set; }
        public required string TipoServicio { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public string? Direccion { get; set; }
        public decimal Tarifa { get; set; }
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
    }
}
