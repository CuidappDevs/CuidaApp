namespace CUIDAPP_API.DTOs.Auth
{
    public class RegisterClientDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.MaxLength(20)]
        public string? TerminosVersion { get; set; }
        public string? GoogleIdToken { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoUrl { get; set; }
        public int? NacionalidadId { get; set; }
        public string? DocumentoIdentidad { get; set; }
        public string? Telefono { get; set; }
        public string? DireccionPrincipal { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
    }
}
