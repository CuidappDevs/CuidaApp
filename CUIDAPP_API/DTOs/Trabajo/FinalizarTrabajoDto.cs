namespace CUIDAPP_API.DTOs.Trabajo
{
    public class FinalizarTrabajoDto
    {
        public int TrabajoId { get; set; }
        public required string Pin { get; set; }
        public string? Justificacion { get; set; }
    }

    public class ConfirmarFinalizacionDto
    {
        public int TrabajoId { get; set; }
        public int ClienteId { get; set; }
        public bool Confirmado { get; set; }
    }

    public class ForzarFinalizacionDto
    {
        public int TrabajoId { get; set; }
        public int CuidadorId { get; set; }
    }
}
