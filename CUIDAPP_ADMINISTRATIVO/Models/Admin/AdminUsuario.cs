namespace CUIDAPP_ADMINISTRATIVO.Models.Admin
{
    public class AdminUsuario
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime FechaCreacion { get; set; }
        public bool IsActive { get; set; }

        public string EstadoTexto => IsActive ? "Activo" : "Suspendido";
    }
}
