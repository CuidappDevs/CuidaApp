using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using CUIDAPP_API.Interfaces.Email;

namespace CUIDAPP_API.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly EmailCredentials _credenciales;

        public EmailService(IConfiguration config)
        {
            _credenciales = config.GetSection("EmailCredentials").Get<EmailCredentials>()
                ?? throw new InvalidOperationException("Falta la sección 'EmailCredentials' en la configuración.");
        }

        public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(MailboxAddress.Parse(_credenciales.SmtpUser));

            var destinatarios = destinatario.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var d in destinatarios)
                mensaje.To.Add(MailboxAddress.Parse(d));

            mensaje.Subject = asunto;
            mensaje.Body = new BodyBuilder { HtmlBody = cuerpoHtml }.ToMessageBody();

            using var cliente = new SmtpClient();
            await cliente.ConnectAsync(_credenciales.SmtpHost, _credenciales.SmtpPort, SecureSocketOptions.StartTls);
            await cliente.AuthenticateAsync(_credenciales.SmtpUser, _credenciales.SmtpPass);
            await cliente.SendAsync(mensaje);
            await cliente.DisconnectAsync(true);
        }
    }
}
