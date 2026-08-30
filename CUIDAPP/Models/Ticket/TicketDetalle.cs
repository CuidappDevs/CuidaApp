namespace CUIDAPP.Models.Ticket
{
    public class TicketDetalle
    {
        public Ticket Ticket { get; set; } = new();
        public List<TicketMensaje> Mensajes { get; set; } = new();
    }
}
