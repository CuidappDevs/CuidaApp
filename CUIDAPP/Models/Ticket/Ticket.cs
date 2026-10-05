using CUIDAPP.Localization;
namespace CUIDAPP.Models.Ticket
{
    public class Ticket
    {
        public int Id { get; set; }
        public string Asunto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public int? TrabajoId { get; set; }
        public int Estado { get; set; } // 1=Abierto, 2=En proceso, 3=Resuelto
        public DateTime FechaCreacion { get; set; }
        public DateTime FechaActualizacion { get; set; }

        public string EstadoTexto => Estado switch
        {
            1 => Localizador.T("ticket_abierto"),
            2 => Localizador.T("ticket_en_proceso"),
            3 => Localizador.T("ticket_resuelto"),
            _ => Localizador.T("desconocido")
        };
    }
}
