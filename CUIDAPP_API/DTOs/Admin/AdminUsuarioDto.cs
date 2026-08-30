namespace CUIDAPP_API.DTOs.Admin
{
    public class AdminUsuarioDto
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime FechaCreacion { get; set; }
        public bool IsActive { get; set; }
    }
}
