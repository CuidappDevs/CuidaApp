namespace CUIDAPP_API.DTOs.Auth
{
    public class AuthResponseDto
    {
        public required string Token { get; set; }
        public required string Email { get; set; }
        public string? NombreCompleto { get; set; }
        public string? FotoUrl { get; set; }
        public int UserId { get; set; }
        public int RolId { get; set; }
        public int? EstadoAprobacion { get; set; } // Solo aplica a Cuidador: 1=Pendiente, 2=Aprobado, 3=Rechazado
    }
}
