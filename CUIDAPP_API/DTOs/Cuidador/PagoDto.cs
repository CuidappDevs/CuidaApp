namespace CUIDAPP_API.DTOs.Cuidador
{
    public class PagoDto
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public required string TipoServicio { get; set; }
        public required string ClienteNombre { get; set; }
        public decimal Monto { get; set; }
        public decimal Propina { get; set; }
        public int Estado { get; set; } // 1=Pendiente, 3=Autorizado, 2=Pagado, 4=Cobrado en efectivo
        public DateTime? FechaPago { get; set; }
        public DateTime FechaCreacion { get; set; }
        public decimal Comision { get; set; }
        public decimal DescuentoSaldo { get; set; }
        public string? MetodoPago { get; set; }
        /// <summary>Lo que recibe: tarifa − comisión + propina − descuento (en efectivo: lo que cobró en mano).</summary>
        public decimal Neto { get; set; }
    }
}
