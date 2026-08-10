namespace CUIDAPP_API.DTOs.Trabajo
{
    public class CancelarTrabajoDto
    {
        public int TrabajoId { get; set; }
        public int MotivoId { get; set; }
        public string? MotivoTexto { get; set; }
    }
}
