namespace CUIDAPP.Models.Cliente
{
    public class UbicacionCliente
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public string Nombre { get; set; } = "";
        public string Direccion { get; set; } = "";
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
        public bool EsPredeterminada { get; set; }
    }
}
