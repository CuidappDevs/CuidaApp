namespace CUIDAPP_API.DTOs.Auth
{
    public class RegisterCaregiverDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.MaxLength(20)]
        public string? TerminosVersion { get; set; }
        public string? GoogleIdToken { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoUrl { get; set; }
        public int? EstatusMigratorioId { get; set; }
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
        /// <summary>Token del test psicológico aprobado (obligatorio si el tipo de trabajo tiene test activo).</summary>
        public Guid? TestToken { get; set; }
        /// <summary>Código de "Invitar a mi cuidadora" (opcional). Si viene, debe estar vigente.</summary>
        public string? CodigoInvitacion { get; set; }
    }

    /// <summary>El código de invitación no existe, venció o ya se usó.</summary>
    public class InvitacionInvalidaException : Exception
    {
        public InvitacionInvalidaException() : base("El código de invitación no es válido o ya fue utilizado. No se creó la cuenta; vuelve a registrarte con una invitación vigente.") { }
    }

    /// <summary>El tipo de trabajo elegido pide test psicológico y no hay un intento aprobado de esta persona.</summary>
    public class TestRequeridoException : Exception
    {
        public TestRequeridoException() : base("Falta aprobar el test psicológico.") { }
    }

    public class DocumentoExtraDto
    {
        public string TipoDocumento { get; set; } = string.Empty;
        public string UrlArchivo { get; set; } = string.Empty;
    }
}
