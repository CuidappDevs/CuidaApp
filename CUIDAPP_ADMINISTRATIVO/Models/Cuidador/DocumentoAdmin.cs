namespace CUIDAPP_ADMINISTRATIVO.Models.Cuidador
{
    public class DocumentoAdmin
    {
        public int Id { get; set; }
        public string TipoDocumento { get; set; } = "";
        public string UrlArchivo { get; set; } = "";
        public int Estado { get; set; }
        public string? ObservacionesAdmin { get; set; }
        public DateTime FechaSubida { get; set; }

        public string TipoTexto => TipoDocumento switch
        {
            "Cedula" => "Cédula de identidad",
            "CartaAntecedentes" => "Carta de antecedentes penales",
            _ => TipoDocumento
        };
    }
}
