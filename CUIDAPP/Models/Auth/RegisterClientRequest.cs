namespace CUIDAPP.Models.Auth
{
    public class RegisterClientRequest
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
        public string? DireccionPrincipal { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
    }
}
