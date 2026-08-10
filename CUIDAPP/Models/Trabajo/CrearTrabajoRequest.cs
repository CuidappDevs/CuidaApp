namespace CUIDAPP.Models.Trabajo
{
    public class CrearTrabajoRequest
    {
        public int ClienteId { get; set; }
        public int CuidadorId { get; set; }
        public string TipoServicio { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public string? Direccion { get; set; }
        public decimal Tarifa { get; set; }
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
    }
}
