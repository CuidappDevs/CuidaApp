namespace CUIDAPP_ADMINISTRATIVO.Models.Pago
{
    public class PagoAdmin
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public int CuidadorId { get; set; }
        public string CuidadorNombre { get; set; } = "";
        public int ClienteId { get; set; }
        public string ClienteNombre { get; set; } = "";
        public string TipoServicio { get; set; } = "";
        public decimal Monto { get; set; }
        public decimal Propina { get; set; }
        public decimal Comision { get; set; }
        public decimal ComisionPorcentaje { get; set; }
        public decimal? NetoCuidador { get; set; }
        public decimal DescuentoSaldo { get; set; }
        /// <summary>Deuda actual del Care Partner por comisiones de servicios en efectivo.</summary>
        public decimal SaldoCuidador { get; set; }
        public bool EsEfectivo => Estado == 4;
        public string? MetodoPago { get; set; }
        /// <summary>Lo que recibe el Care Partner: tarifa - comisión + propina.</summary>
        public decimal Total => NetoCuidador ?? Monto + Propina;
        public int Estado { get; set; } // 1=Pendiente, 3=Autorizado, 2=Pagado, 4=Cobrado en efectivo
        public DateTime FechaCreacion { get; set; }
        public int? AutorizadoPorAdminId { get; set; }
        public string? AutorizadoPorNombre { get; set; }
        public DateTime? FechaAutorizacion { get; set; }
        public int? AprobadoPorAdminId { get; set; }
        public string? AprobadoPorNombre { get; set; }
        public DateTime? FechaPago { get; set; }
        public bool PagoDisputado { get; set; }

        public string EstadoTexto => Estado switch
        {
            1 => "Pendiente",
            3 => "Autorizado",
            2 => "Pagado",
            4 => "Cobrado en efectivo",
            _ => "Desconocido"
        };
    }
}
