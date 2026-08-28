namespace CUIDAPP_API.DTOs.Chat
{
    public class ConversacionDto
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public DateTime FechaCreacion { get; set; }
        public bool Activa { get; set; }
    }

    public class MensajeDto
    {
        public long Id { get; set; }
        public int ConversacionId { get; set; }
        public int RemitenteId { get; set; }
        public required string Contenido { get; set; }
        public DateTime FechaEnvio { get; set; }
        public bool Leido { get; set; }
        public string Tipo { get; set; } = "texto"; // texto | imagen | audio
        public string? UrlArchivo { get; set; }
        public int? DuracionSegundos { get; set; }
    }

    public class EnviarMensajeDto
    {
        public int ConversacionId { get; set; }
        public int RemitenteId { get; set; }
        public required string Contenido { get; set; }
        public string Tipo { get; set; } = "texto";
        public string? UrlArchivo { get; set; }
        public int? DuracionSegundos { get; set; }
    }

    public class MarcarLeidosDto
    {
        public int ConversacionId { get; set; }
        public int UsuarioId { get; set; }
    }

    public class NoLeidosDto
    {
        public int TrabajoId { get; set; }
        public int ConversacionId { get; set; }
        public int NoLeidos { get; set; }
    }
}
