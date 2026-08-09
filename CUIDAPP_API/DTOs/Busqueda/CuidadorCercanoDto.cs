namespace CUIDAPP_API.DTOs.Busqueda
{
    public class CuidadorCercanoDto
    {
        public int Id { get; set; }
        public required string NombreCompleto { get; set; }
        public string? FotoUrl { get; set; }
        public required string Especialidad { get; set; }
        public decimal TarifaHora { get; set; }
        public string? Bio { get; set; }
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
        public double DistanciaKm { get; set; }
    }
}
