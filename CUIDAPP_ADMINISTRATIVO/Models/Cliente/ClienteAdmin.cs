namespace CUIDAPP_ADMINISTRATIVO.Models.Cliente
{
    public class ClienteAdmin
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string Email { get; set; } = "";
        public string? FotoUrl { get; set; }
        public DateTime FechaCreacion { get; set; }
        public bool IsActive { get; set; }
        public string? DireccionPrincipal { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
        public int TotalServiciosSolicitados { get; set; }
        public int ServiciosCompletados { get; set; }

        public string CuentaTexto => IsActive ? "Activa" : "Suspendida";
    }
}
