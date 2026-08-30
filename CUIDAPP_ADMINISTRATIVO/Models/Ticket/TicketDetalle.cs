namespace CUIDAPP_ADMINISTRATIVO.Models.Ticket
{
    public class TicketDetalle
    {
        public TicketAdmin Ticket { get; set; } = new();
        public List<TicketMensaje> Mensajes { get; set; } = new();
    }
}
