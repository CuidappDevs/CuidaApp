namespace CUIDAPP.Models.Auth
{
    public class RegisterCaregiverRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoUrl { get; set; }
        public string Especialidad { get; set; } = string.Empty;
        public decimal TarifaHora { get; set; }
        public string? Bio { get; set; }
        public string? MetodoCobro { get; set; }
        public string? CedulaUrl { get; set; }
        public string? CartaAntecedentesUrl { get; set; }
    }
}
