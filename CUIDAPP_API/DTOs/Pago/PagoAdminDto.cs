namespace CUIDAPP_API.DTOs.Pago
{
    public class PagoAdminDto
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public int CuidadorId { get; set; }
        public string CuidadorNombre { get; set; } = "";
        public int ClienteId { get; set; }
        public string ClienteNombre { get; set; } = "";
        public string TipoServicio { get; set; } = "";
        public decimal Monto { get; set; }
        public int Estado { get; set; } // 1=Pendiente, 3=Autorizado, 2=Pagado
        public DateTime FechaCreacion { get; set; }
        public int? AutorizadoPorAdminId { get; set; }
        public string? AutorizadoPorNombre { get; set; }
        public DateTime? FechaAutorizacion { get; set; }
        public int? AprobadoPorAdminId { get; set; }
        public string? AprobadoPorNombre { get; set; }
        public DateTime? FechaPago { get; set; }
        public bool PagoDisputado { get; set; }
    }
}
