namespace CUIDAPP_API.DTOs.Cliente
{
    public class PerfilClienteDto
    {
        public int Id { get; set; }
        public required string Email { get; set; }
        public required string NombreCompleto { get; set; }
        public string? FotoUrl { get; set; }
        public string? DireccionPrincipal { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
    }
}
