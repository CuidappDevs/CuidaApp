namespace CUIDAPP_API.Interfaces.Email
{
    public interface IEmailService
    {
        Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml);
    }
}
