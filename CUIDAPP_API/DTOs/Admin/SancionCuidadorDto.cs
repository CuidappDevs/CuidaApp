namespace CUIDAPP_API.DTOs.Admin
{
    public class SancionCuidadorDto
    {
        public int Id { get; set; }
        public string Accion { get; set; } = "";
        public string? Motivo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string AdminNombre { get; set; } = "";
    }
}
