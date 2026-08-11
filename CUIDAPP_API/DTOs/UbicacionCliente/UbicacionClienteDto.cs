namespace CUIDAPP_API.DTOs.UbicacionCliente
{
    public class UbicacionClienteDto
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public required string Nombre { get; set; }
        public required string Direccion { get; set; }
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
        public bool EsPredeterminada { get; set; }
    }
}
