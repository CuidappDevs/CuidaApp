namespace CUIDAPP_API.DTOs.Busqueda
{
    public class CuidadorMapaDto
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Especialidad { get; set; } = string.Empty;
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
        public double DistanciaKm { get; set; }
    }
}
