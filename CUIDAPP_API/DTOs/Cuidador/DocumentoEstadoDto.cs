namespace CUIDAPP_API.DTOs.Cuidador
{
    public class DocumentoEstadoDto
    {
        public int Id { get; set; }
        public required string TipoDocumento { get; set; }
        public required string UrlArchivo { get; set; }
        public int Estado { get; set; } // 1=Pendiente, 2=Aprobado, 3=Rechazado
        public string? ObservacionesAdmin { get; set; }
        public DateTime FechaSubida { get; set; }
    }
}
