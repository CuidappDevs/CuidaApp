namespace CUIDAPP.Models.Cuidador
{
    public class Pago
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public string TipoServicio { get; set; } = string.Empty;
        public string ClienteNombre { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public decimal Propina { get; set; }
        public int Estado { get; set; } // 1=Pendiente, 3=Autorizado, 2=Pagado, 4=Cobrado en efectivo
        public decimal Comision { get; set; }
        public decimal DescuentoSaldo { get; set; }
        public string? MetodoPago { get; set; }
        public decimal Neto { get; set; }
        /// <summary>Lo que recibe (neto de comisión); en efectivo, lo que cobró en mano.</summary>
        public decimal Total => Neto > 0 ? Neto : Monto + Propina;
        public bool EsCobrado => Estado is 2 or 4;
        public DateTime? FechaPago { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
