namespace CUIDAPP_API.DTOs.Cuidador
{
    public class ReemplazarDocumentoDto
    {
        public int CuidadorId { get; set; }
        public required string UrlArchivo { get; set; }
    }
}
