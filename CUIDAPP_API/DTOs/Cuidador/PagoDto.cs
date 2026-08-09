namespace CUIDAPP_API.DTOs.Cuidador
{
    public class PagoDto
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public required string TipoServicio { get; set; }
        public required string ClienteNombre { get; set; }
        public decimal Monto { get; set; }
        public int Estado { get; set; } // 1=Pendiente, 2=Pagado
        public DateTime? FechaPago { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
