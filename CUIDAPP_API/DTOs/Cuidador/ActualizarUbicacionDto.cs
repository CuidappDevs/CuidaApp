namespace CUIDAPP_API.DTOs.Cuidador
{
    public class ActualizarUbicacionDto
    {
        public int CuidadorId { get; set; }
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
    }
}
