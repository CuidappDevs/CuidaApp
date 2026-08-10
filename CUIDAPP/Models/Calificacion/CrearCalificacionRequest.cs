namespace CUIDAPP.Models.Calificacion
{
    public class CrearCalificacionRequest
    {
        public int TrabajoId { get; set; }
        public int CalificadorId { get; set; }
        public int CalificadoId { get; set; }
        public int Puntuacion { get; set; }
        public string? Comentario { get; set; }
    }
}
