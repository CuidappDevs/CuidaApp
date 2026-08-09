namespace CUIDAPP.Models.Cliente
{
    public class PerfilCliente
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoUrl { get; set; }
        public string? DireccionPrincipal { get; set; }
        public string? ContactoEmergenciaNombre { get; set; }
        public string? ContactoEmergenciaTelefono { get; set; }
    }
}
