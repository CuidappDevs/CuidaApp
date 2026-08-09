namespace CUIDAPP_API.DTOs.Trabajo
{
    public class ActualizarEstadoTrabajoDto
    {
        public int TrabajoId { get; set; }
        public int NuevoEstado { get; set; } // 2=Aceptado,3=EnProgreso,4=Completado,5=Cancelado,6=Rechazado
    }
}
