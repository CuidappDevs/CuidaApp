namespace CUIDAPP_API.DTOs.UbicacionCliente
{
    public class CrearUbicacionClienteDto
    {
        public int ClienteId { get; set; }
        public required string Nombre { get; set; }
        public required string Direccion { get; set; }
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
        public bool EsPredeterminada { get; set; }
    }

    public class ActualizarUbicacionClienteDto
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public required string Nombre { get; set; }
        public required string Direccion { get; set; }
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
        public bool EsPredeterminada { get; set; }
    }

    public class EliminarUbicacionClienteDto
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
    }
}
