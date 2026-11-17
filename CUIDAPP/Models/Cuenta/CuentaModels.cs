namespace CUIDAPP.Models.Cuenta
{
    /// <summary>Deuda del cliente por un servicio en efectivo que no pagó completo (api/DeudaCliente).</summary>
    public class DeudaCliente
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public DateTime? FechaServicio { get; set; }
        public string? NombreCuidador { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal MontoRecibido { get; set; }
        public decimal Monto { get; set; }
        /// <summary>1 pendiente, 2 pagada, 3 en revisión (el cliente objetó), 4 condonada.</summary>
        public int Estado { get; set; }
        public string? ComprobanteUrl { get; set; }
        public string? NotaAdmin { get; set; }
        public DateTime FechaCreacion { get; set; }
        /// <summary>Hasta cuándo debe pagarlo (24 h desde el servicio). El bloqueo es inmediato; el plazo es para el seguimiento.</summary>
        public DateTime? FechaLimite { get; set; }
        public bool Vencida { get; set; }

        public bool Abierta => Estado is 1 or 3;
    }

    public class ResumenDeudasCliente
    {
        public decimal TotalPendiente { get; set; }
        public bool Bloqueado { get; set; }
        public List<DeudaCliente> Deudas { get; set; } = new();
    }

    /// <summary>Algo que impide (o conviene revisar antes de) eliminar la cuenta.</summary>
    public class BloqueoEliminacion
    {
        public string Codigo { get; set; } = "";
        public string? Detalle { get; set; }
        public int? ReferenciaId { get; set; }
        public bool Bloquea { get; set; }
    }

    public class SolicitudEliminacion
    {
        public DateTime FechaEjecucion { get; set; }
    }

    public class RevisionEliminacion
    {
        public bool PuedeEliminar { get; set; }
        public List<BloqueoEliminacion> Bloqueos { get; set; } = new();
        public SolicitudEliminacion? Solicitud { get; set; }
    }
}
