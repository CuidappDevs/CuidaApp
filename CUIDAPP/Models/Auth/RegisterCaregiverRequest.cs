namespace CUIDAPP.Models.Auth
{
    public class RegisterCaregiverRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoUrl { get; set; }
        public int? NacionalidadId { get; set; }
        public string? DocumentoIdentidad { get; set; }
        public string? Telefono { get; set; }
        /// <summary>Documentos opcionales (certificado médico, cursos…) ya subidos; van a DocumentosCuidador.</summary>
        public List<DocumentoExtraDto>? DocumentosExtra { get; set; }
        public string Especialidad { get; set; } = string.Empty;
        public decimal TarifaHora { get; set; }
        public string? Bio { get; set; }
        public string? MetodoCobro { get; set; }
        public string? CedulaUrl { get; set; }
        public string? CartaAntecedentesUrl { get; set; }
    }

    public class DocumentoExtraDto
    {
        public string TipoDocumento { get; set; } = string.Empty;
        public string UrlArchivo { get; set; } = string.Empty;
    }
}
