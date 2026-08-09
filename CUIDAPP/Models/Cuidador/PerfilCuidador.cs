namespace CUIDAPP.Models.Cuidador
{
    public class PerfilCuidador
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoUrl { get; set; }
        public string? Especialidad { get; set; }
        public decimal TarifaHora { get; set; }
        public string? Bio { get; set; }
        public string? MetodoCobro { get; set; }
        public int EstadoAprobacion { get; set; }
        public bool Disponible { get; set; }
    }
}
