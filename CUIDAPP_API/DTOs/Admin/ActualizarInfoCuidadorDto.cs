namespace CUIDAPP_API.DTOs.Admin
{
    public class ActualizarInfoCuidadorDto
    {
        public string NombreCompleto { get; set; } = "";
        public string Especialidad { get; set; } = "";
        public decimal TarifaHora { get; set; }
        public string? Bio { get; set; }
        public string? MetodoCobro { get; set; }
    }
}
