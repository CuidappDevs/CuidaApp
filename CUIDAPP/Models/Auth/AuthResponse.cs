namespace CUIDAPP.Models.Auth
{
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? NombreCompleto { get; set; }
        public string? FotoUrl { get; set; }
        public int UserId { get; set; }
        public int RolId { get; set; }
        public int? EstadoAprobacion { get; set; } // Solo Cuidador: 1=Pendiente, 2=Aprobado, 3=Rechazado
    }
}
