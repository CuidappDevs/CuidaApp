namespace CUIDAPP_API.DTOs.Cuidador
{
    public class PerfilCuidadorDto
    {
        public int Id { get; set; }
        public required string Email { get; set; }
        public required string NombreCompleto { get; set; }
        public string? FotoUrl { get; set; }
        public string? Especialidad { get; set; }
        public decimal TarifaHora { get; set; }
        public string? Bio { get; set; }
        public string? MetodoCobro { get; set; }
        public int EstadoAprobacion { get; set; }
        public bool Disponible { get; set; }
    }
}
