namespace CUIDAPP_API.DTOs.Auth
{
    public class RegisterClientDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoUrl { get; set; }
        public string? DireccionPrincipal { get; set; }
        public string? ContactoEmergencia { get; set; }
    }
}
