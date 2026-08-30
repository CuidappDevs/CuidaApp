namespace CUIDAPP_API.DTOs.Admin
{
    public class ActualizarInfoClienteDto
    {
        public string NombreCompleto { get; set; } = "";
        public string? DireccionPrincipal { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
    }
}
