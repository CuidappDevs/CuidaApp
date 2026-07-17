namespace CUIDAPP_API.DTOs.Admin
{
    public class CuidadorPendienteDto
    {
        public int UsuarioId { get; set; }
        public required string Email { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string? Especialidad { get; set; }
        public decimal TarifaHora { get; set; }
        public int EstadoAprobacion { get; set; }
    }
}
