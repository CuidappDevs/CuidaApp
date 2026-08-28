namespace CUIDAPP_API.DTOs.Email
{
    public class EnviarEmailDto
    {
        public string Destinatario { get; set; } = "";
        public string Asunto { get; set; } = "";
        public string CuerpoHtml { get; set; } = "";
    }
}
