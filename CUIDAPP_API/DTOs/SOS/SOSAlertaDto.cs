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
