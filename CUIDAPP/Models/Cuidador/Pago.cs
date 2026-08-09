namespace CUIDAPP.Models.Cuidador
{
    public class Pago
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public string TipoServicio { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public int Estado { get; set; } // 1=Pendiente, 2=Pagado
        public DateTime? FechaPago { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
