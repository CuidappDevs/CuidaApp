namespace CUIDAPP.Models.Calificacion
{
    public class Calificacion
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public int CalificadorId { get; set; }
        public int CalificadoId { get; set; }
        public int Puntuacion { get; set; }
        public string? Comentario { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
