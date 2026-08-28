namespace CUIDAPP.Models.Calificacion
{
    public class CalificacionRecibida
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

        public string EstrellasTexto => new string('★', Puntuacion) + new string('☆', 5 - Puntuacion);
        public bool TieneComentario => !string.IsNullOrWhiteSpace(Comentario);
        public string FechaTexto => FechaCreacion.ToString("dd MMM yyyy");
    }
}
