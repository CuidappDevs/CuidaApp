namespace CUIDAPP.Models.Auth
{
    public class RegisterCaregiverRequest
    {
        /// <summary>Versión de los Términos y Condiciones aceptada en el registro.</summary>
        public string? TerminosVersion { get; set; }

        /// <summary>Registro con "Continuar con Google": el servidor valida el token y no exige contraseña.</summary>
        public string? GoogleIdToken { get; set; }

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
        /// <summary>Token del test psicológico aprobado (si el tipo de trabajo lo pide).</summary>
        public Guid? TestToken { get; set; }
        /// <summary>Código de "Invitar a mi cuidadora" (opcional).</summary>
        public string? CodigoInvitacion { get; set; }
        /// <summary>Estatus migratorio (obligatorio si es extranjera); sus documentos van en DocumentosExtra.</summary>
        public int? EstatusMigratorioId { get; set; }
    }

    public class DocumentoExtraDto
    {
        public string TipoDocumento { get; set; } = string.Empty;
        public string UrlArchivo { get; set; } = string.Empty;
    }
}
