namespace CUIDAPP_API.DTOs.Cuidador
{
    public class SubirDocumentoDto
    {
        public int CuidadorId { get; set; }
        public required string TipoDocumento { get; set; }
        public required string UrlArchivo { get; set; }
    }
}
