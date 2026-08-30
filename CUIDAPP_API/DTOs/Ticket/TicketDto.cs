namespace CUIDAPP_API.DTOs.Ticket
{
    public class TicketDto
    {
        public int Id { get; set; }
        public string Asunto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public int? TrabajoId { get; set; }
        public int Estado { get; set; } // 1=Abierto, 2=En proceso, 3=Resuelto
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaActualizacion { get; set; }
    }
}
