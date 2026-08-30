namespace CUIDAPP_API.DTOs.Ticket
{
    public class TicketAdminDto
    {
        public int Id { get; set; }
        public string Asunto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public int? TrabajoId { get; set; }
        public int Estado { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaActualizacion { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; } = "";
        public string UsuarioEmail { get; set; } = "";
        public int UsuarioRolId { get; set; }
        public int? AsignadoAdminId { get; set; }
        public string? AsignadoAdminNombre { get; set; }
        public int TotalMensajes { get; set; }
    }
}
