namespace CUIDAPP.Models.Cuidador
{
    public class DocumentoEstado
    {
        public int Id { get; set; }
        public string TipoDocumento { get; set; } = string.Empty;
        public string UrlArchivo { get; set; } = string.Empty;
        public int Estado { get; set; } // 1=Pendiente, 2=Aprobado, 3=Rechazado
        public string? ObservacionesAdmin { get; set; }
        public DateTime FechaSubida { get; set; }
    }
}
