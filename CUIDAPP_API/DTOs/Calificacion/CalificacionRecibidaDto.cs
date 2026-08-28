namespace CUIDAPP_API.DTOs.Calificacion
{
    public class CalificacionRecibidaDto
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public int CalificadorId { get; set; }
        public string CalificadorNombre { get; set; } = "";
        public string? CalificadorFotoUrl { get; set; }
        public int Puntuacion { get; set; }
        public string? Comentario { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string? TipoServicio { get; set; }
    }
}
