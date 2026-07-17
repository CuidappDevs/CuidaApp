namespace CUIDAPP_API.DTOs.Admin
{
    public class ActualizarEstadoCuidadorDto
    {
        public int CuidadorId { get; set; }
        public int NuevoEstado { get; set; } // 2=Aprobado, 3=Rechazado
        public string? Observaciones { get; set; }
    }
}
