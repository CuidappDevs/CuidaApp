namespace CUIDAPP_API.DTOs.Admin
{
    public class DocumentoDto
    {
        public int Id { get; set; }
        public required string TipoDocumento { get; set; }
        public required string UrlArchivo { get; set; }
        public int Estado { get; set; }
        public string? ObservacionesAdmin { get; set; }
        public DateTime FechaSubida { get; set; }
    }
}
