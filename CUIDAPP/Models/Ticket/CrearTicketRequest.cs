namespace CUIDAPP.Models.Ticket
{
    public class CrearTicketRequest
    {
        public int UsuarioId { get; set; }
        public string Asunto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public int? TrabajoId { get; set; }
    }
}
