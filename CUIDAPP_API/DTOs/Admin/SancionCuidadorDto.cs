namespace CUIDAPP_API.DTOs.Admin
{
    public class SancionCuidadorDto
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public int AdminId { get; set; }
        public string AdminNombre { get; set; } = "";
        public string Accion { get; set; } = "";
        public string Motivo { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Estado { get; set; } = "";
        public DateTimeOffset FechaInicioUtc { get; set; }
        public DateTimeOffset? FechaFinUtc { get; set; }
        public int? RevocadaPorAdminId { get; set; }
        public string? RevocadaPorAdminNombre { get; set; }
        public DateTimeOffset? FechaRevocacionUtc { get; set; }
        public bool EstaVigente { get; set; }
    }
}
