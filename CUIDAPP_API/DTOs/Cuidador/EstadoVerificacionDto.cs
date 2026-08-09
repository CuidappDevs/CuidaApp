namespace CUIDAPP_API.DTOs.Cuidador
{
    public class EstadoVerificacionDto
    {
        public int EstadoAprobacion { get; set; } // 1=Pendiente, 2=Aprobado, 3=Rechazado
        public List<DocumentoEstadoDto> Documentos { get; set; } = new();
    }
}
