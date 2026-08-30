namespace CUIDAPP_API.DTOs.Ticket
{
    public class CrearMensajeTicketDto
    {
        public int AutorId { get; set; }
        public bool EsAdmin { get; set; }
        public string Mensaje { get; set; } = "";
    }
}
