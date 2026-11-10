namespace CUIDAPP_API.Interfaces.Email
{
    public interface IEmailService
    {
        Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml);

        /// <summary>Igual que EnviarAsync, con un archivo adjunto (por ejemplo el PDF de un contrato).</summary>
        Task EnviarConAdjuntoAsync(string destinatario, string asunto, string cuerpoHtml, string nombreArchivo, byte[] contenido, string tipoMime);
    }
}
