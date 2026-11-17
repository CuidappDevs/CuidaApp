namespace CUIDAPP_ADMINISTRATIVO.Models.Panel
{
    /// <summary>Deuda de un cliente por un servicio en efectivo pagado incompleto (api/AdminDeudas).</summary>
    public class DeudaClienteFila
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public DateTime? FechaServicio { get; set; }
        public string? NombreCuidador { get; set; }
        public int CuidadorId { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal MontoRecibido { get; set; }
        public decimal Monto { get; set; }
        /// <summary>1 pendiente, 2 pagada, 3 en revisión, 4 condonada/anulada.</summary>
        public int Estado { get; set; }
        public string? ComprobanteUrl { get; set; }
        public string? NotaAdmin { get; set; }
        public string? NotaCliente { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaResolucion { get; set; }
        public DateTime? FechaLimite { get; set; }
        public bool Vencida { get; set; }
        public int ClienteId { get; set; }
        public string? NombreCliente { get; set; }
        public string? CorreoCliente { get; set; }
    }

    /// <summary>Contrato firmado guardado cifrado en la bóveda (api/admin/boveda). Sin datos personales.</summary>
    public class DocumentoBoveda
    {
        public int DocumentoId { get; set; }
        public int ContratoId { get; set; }
        public DateTime? FechaFirma { get; set; }
        public DateTime? FechaBoveda { get; set; }
        public List<int> UsuariosIdEliminados { get; set; } = new();
        public long? TamanoBytes { get; set; }
        public string? Sha256 { get; set; }
    }

    /// <summary>Solicitud de eliminación de cuenta (api/AdminEliminaciones).</summary>
    public class EliminacionCuentaFila
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public int Rol { get; set; }
        public string? NombreCompleto { get; set; }
        public string? Email { get; set; }
        public string? Motivo { get; set; }
        /// <summary>1 programada, 2 cancelada, 3 ejecutada (anonimizada).</summary>
        public int Estado { get; set; }
        public DateTime FechaSolicitud { get; set; }
        public DateTime FechaEjecucion { get; set; }
        public DateTime? FechaCancelacion { get; set; }
        public string? CanceladaPor { get; set; }
        public string? Origen { get; set; }
        public string? NotaAdmin { get; set; }
    }
}
