namespace CUIDAPP_ADMINISTRATIVO.Models.Auth
{
    public class AuthResponse
    {
        public string Token { get; set; } = "";
        public string Email { get; set; } = "";
        public string? NombreCompleto { get; set; }
        public string? FotoUrl { get; set; }
        public int UserId { get; set; }
        public int RolId { get; set; }
        public int? EstadoAprobacion { get; set; }
    }
}
