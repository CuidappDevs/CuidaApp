namespace CUIDAPP_API.DTOs.Trabajo
{
    public class TareaTrabajoDto
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public required string Descripcion { get; set; }
        public int Orden { get; set; }
        public bool Completada { get; set; }
        public DateTime? FechaCompletada { get; set; }
    }
}
