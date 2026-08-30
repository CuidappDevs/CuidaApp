namespace CUIDAPP_ADMINISTRATIVO.Models.Cuidador
{
    public class CuidadorAdmin
    {
        public int UsuarioId { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string Email { get; set; } = "";
        public string? FotoUrl { get; set; }
        public DateTime FechaCreacion { get; set; }
        public bool IsActive { get; set; }
        public string? Especialidad { get; set; }
        public decimal TarifaHora { get; set; }
        public int EstadoAprobacion { get; set; } // 1=Pendiente, 2=Aprobado, 3=Rechazado
        public bool Disponible { get; set; }
        public string? Bio { get; set; }
        public string? MetodoCobro { get; set; }
        public decimal? PromedioCalificacion { get; set; }
        public int TotalCalificaciones { get; set; }
        public int TrabajosCompletados { get; set; }

        public string EstadoTexto => EstadoAprobacion switch
        {
            1 => "Pendiente",
            2 => "Verificado",
            3 => "Rechazado",
            _ => "Desconocido"
        };

        public string CuentaTexto => IsActive ? "Activa" : "Suspendida";
    }
}
