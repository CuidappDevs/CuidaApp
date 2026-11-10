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

        public Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml)
            => EnviarInternoAsync(destinatario, asunto, cuerpoHtml, null);

        public Task EnviarConAdjuntoAsync(string destinatario, string asunto, string cuerpoHtml, string nombreArchivo, byte[] contenido, string tipoMime)
            => EnviarInternoAsync(destinatario, asunto, cuerpoHtml, (nombreArchivo, contenido, tipoMime));

        private async Task EnviarInternoAsync(string destinatario, string asunto, string cuerpoHtml, (string Nombre, byte[] Contenido, string Tipo)? adjunto)
        {
            var mensaje = new MimeMessage();
            mensaje.From.Add(MailboxAddress.Parse(_credenciales.SmtpUser));

            var destinatarios = destinatario.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var d in destinatarios)
                mensaje.To.Add(MailboxAddress.Parse(d));

            mensaje.Subject = asunto;
            var cuerpo = new BodyBuilder { HtmlBody = cuerpoHtml };
            if (adjunto is { } a)
                cuerpo.Attachments.Add(a.Nombre, a.Contenido, ContentType.Parse(a.Tipo));
            mensaje.Body = cuerpo.ToMessageBody();

            using var cliente = new SmtpClient();
            await cliente.ConnectAsync(_credenciales.SmtpHost, _credenciales.SmtpPort, SecureSocketOptions.StartTls);
            await cliente.AuthenticateAsync(_credenciales.SmtpUser, _credenciales.SmtpPass);
            await cliente.SendAsync(mensaje);
            await cliente.DisconnectAsync(true);
        }
    }
}
