namespace CUIDAPP_API.DTOs.Ticket
{
    public class TicketMensajeDto
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public int AutorId { get; set; }
        public string AutorNombre { get; set; } = "";
        public bool EsAdmin { get; set; }
        public string Mensaje { get; set; } = "";
        public DateTime FechaCreacion { get; set; }
    }
}
