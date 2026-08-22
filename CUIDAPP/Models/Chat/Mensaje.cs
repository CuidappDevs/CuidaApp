namespace CUIDAPP.Models.Chat
{
    public class Conversacion
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public DateTime FechaCreacion { get; set; }
        public bool Activa { get; set; }
    }

    public class Mensaje
    {
        public long Id { get; set; }
        public int ConversacionId { get; set; }
        public int RemitenteId { get; set; }
        public string Contenido { get; set; } = "";
        public DateTime FechaEnvio { get; set; }
        public bool Leido { get; set; }
        public string Tipo { get; set; } = "texto"; // texto | imagen | audio
        public string? UrlArchivo { get; set; }
        public int? DuracionSegundos { get; set; }
    }
}
