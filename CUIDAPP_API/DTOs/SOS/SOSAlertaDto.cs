namespace CUIDAPP_API.DTOs.SOS
{
    public class SOSAlertaDto
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public int UsuarioId { get; set; }
        public required string TipoUsuario { get; set; }
        public required string NombreUsuario { get; set; }
        public string? EmailUsuario { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public string? Motivo { get; set; }
        public required string Estado { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaAtencion { get; set; }
        public string? AtendidoPor { get; set; }
        /// <summary>"Manual" (botón SOS) o "Automatica" (caída detectada sin confirmación).</summary>
        public string Origen { get; set; } = "Manual";
        public string? ContactoNombre { get; set; }
        public string? ContactoTelefono { get; set; }
        public string? ContactoEmail { get; set; }
    }

    /// <summary>Alerta disparada por la app cuando detecta impacto + inmovilidad y el cuidador no confirma en 30 s.</summary>
    public class DeadManTriggeredDto
    {
        public int TrabajoId { get; set; }
        public int UsuarioId { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        /// <summary>Fuerza máxima del impacto detectado, en g.</summary>
        public double? ImpactoG { get; set; }
        /// <summary>Segundos de inmovilidad medidos antes de pedir confirmación.</summary>
        public int? SegundosInmovil { get; set; }
    }

    public class CrearSOSAlertaDto
    {
        public int TrabajoId { get; set; }
        public int UsuarioId { get; set; }
        public required string TipoUsuario { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public string? Motivo { get; set; }
    }

    public class AtenderSOSDto
    {
        public required string AtendidoPor { get; set; }
    }
}
