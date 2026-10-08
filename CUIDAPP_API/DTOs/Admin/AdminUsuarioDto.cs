namespace CUIDAPP_API.DTOs.Admin
{
    public class AdminUsuarioDto
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime FechaCreacion { get; set; }
        public bool IsActive { get; set; }
        /// <summary>1 Superadmin, 2 Operaciones/soporte, 3 Finanzas.</summary>
        public int NivelAdmin { get; set; } = 2;
    }
}
