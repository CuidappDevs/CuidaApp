using System.Globalization;
using System.Net;

namespace CUIDAPP_API.Services.Email
{
    /// <summary>
    /// Diseño común de los correos de CuidApp (recibos, bienvenida): encabezado con logo y franja azul,
    /// secciones, avisos, tarjetas y pie oscuro. Los íconos son PNG servidos desde wwwroot/email
    /// (Gmail/Outlook no muestran SVG) y el HTML usa tablas y estilos en línea, que es lo único que
    /// respetan los clientes de correo.
    /// </summary>
    public abstract class PlantillaCorreo
    {
        protected readonly string _urlPublica;
        protected readonly EmailService _email;
        protected static readonly CultureInfo Es = new("es-DO");

        // Paleta CuidApp
        protected const string Azul = "#1C4D96", AzulOscuro = "#0A2F41", Texto = "#0A2F41", Suave = "#4B5563",
                               Fondo = "#F5F8FC", Borde = "#D9E2EC", Verde = "#2E7D32", VerdeSuave = "#E3F4E8", Gris = "#94A3B8";

        protected PlantillaCorreo(IConfiguration config)
        {
            _email = new EmailService(config);
            _urlPublica = (config["UrlPublica"] ?? "http://192.169.179.217").TrimEnd('/');
        }

        /// <summary>Envía sin lanzar: un fallo de correo nunca debe afectar la operación que lo originó.</summary>
        protected async Task EnviarSeguroAsync(string destino, string asunto, string html)
        {
            try { await _email.EnviarAsync(destino, asunto, html); }
            catch (Exception ex) { Console.WriteLine($"[Correo] Error enviando a {destino}: {ex.Message}"); }
        }

        protected string Encabezado(DateTime fecha, string titulo, string subtitulo, string iconoHero) => $"""
            <tr><td style="padding:24px 32px;background:#FFFFFF">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
                <td style="vertical-align:middle">
                  <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                    <td style="padding-right:10px"><img src="{_urlPublica}/email/logo.png" width="34" height="32" alt="CuidApp" style="display:block;border:0"></td>
                    <td style="font:700 22px Arial,Helvetica,sans-serif;color:{Azul};letter-spacing:-0.3px">CuidApp</td>
                  </tr></table>
                </td>
                <td align="right" style="font:12px Arial,Helvetica,sans-serif;color:{Suave};line-height:18px">{FechaCorta(fecha)}<br>{Hora(fecha)}</td>
              </tr></table>
            </td></tr>
            <tr><td style="background:{Azul};padding:30px 32px">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
                <td style="vertical-align:middle;padding-right:16px">
                  <div style="font:700 26px/32px Arial,Helvetica,sans-serif;color:#FFFFFF">{titulo}</div>
                  <div style="font:15px/22px Arial,Helvetica,sans-serif;color:#D9E6F7;padding-top:10px">{subtitulo}</div>
                </td>
                <td width="100" align="right" style="vertical-align:middle">{Icono(iconoHero, 100)}</td>
              </tr></table>
            </td></tr>
            """;

        protected string Icono(string nombre, int px) =>
            $"""<img src="{_urlPublica}/email/{nombre}.png" width="{px}" height="{px}" alt="" style="display:block;border:0;width:{px}px;height:{px}px">""";


        protected static string Seccion(string filas, string fondo = "#FFFFFF") => $"""
            <tr><td style="padding:24px 32px;background:{fondo};border-bottom:1px solid {Borde}">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0">{filas}</table>
            </td></tr>
            """;

        protected static string Titulo(string texto) =>
            $"""<tr><td colspan="2" style="font:700 18px Arial,Helvetica,sans-serif;color:{Texto};padding-bottom:14px">{texto}</td></tr>""";

        protected static string FilaTotal(string etiqueta, string monto, string color = Texto) => $"""
            <tr>
              <td style="font:700 22px Arial,Helvetica,sans-serif;color:{Texto};padding-bottom:6px">{etiqueta}</td>
              <td align="right" style="font:700 26px Arial,Helvetica,sans-serif;color:{color};padding-bottom:6px;white-space:nowrap">{monto}</td>
            </tr>
            """;

        protected string FilaMonto(string etiqueta, string monto, string? icono = null) => $"""
            <tr>
              <td style="padding:8px 0;font:14px Arial,Helvetica,sans-serif;color:{Texto}">
                <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                  {(icono != null ? $"<td style=\"padding-right:10px\">{Icono(icono, 24)}</td>" : "")}
                  <td style="font:14px Arial,Helvetica,sans-serif;color:{Texto}">{etiqueta}</td>
                </tr></table>
              </td>
              <td align="right" style="padding:8px 0;font:14px Arial,Helvetica,sans-serif;color:{Texto};white-space:nowrap">{monto}</td>
            </tr>
            """;

        protected static string Separador() =>
            $"""<tr><td colspan="2" style="padding:8px 0"><div style="border-top:1px solid {Borde};height:1px;line-height:1px">&nbsp;</div></td></tr>""";

        protected string Detalle(string icono, string titulo, string valor) => $"""
            <tr><td colspan="2" style="padding:7px 0">
              <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                <td style="vertical-align:top;padding-right:12px">{Icono(icono, 36)}</td>
                <td style="vertical-align:middle">
                  <div style="font:12px Arial,Helvetica,sans-serif;color:{Suave}">{titulo}</div>
                  <div style="font:600 14px/20px Arial,Helvetica,sans-serif;color:{Texto}">{valor}</div>
                </td>
              </tr></table>
            </td></tr>
            """;

        protected string Aviso(string icono, string titulo, string texto) => $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#FFF4DC;border-radius:12px"><tr>
              <td width="40" style="padding:12px 0 12px 14px;vertical-align:top">{Icono(icono, 28)}</td>
              <td style="padding:12px 14px">
                <div style="font:700 13px Arial,Helvetica,sans-serif;color:#8A4B00">{titulo}</div>
                <div style="font:13px/19px Arial,Helvetica,sans-serif;color:#8A4B00;padding-top:2px">{texto}</div>
              </td>
            </tr></table>
            """;

        protected string Tarjetas(params (string Icono, string Titulo, string Texto)[] tarjetas)
        {
            var celdas = string.Join("<td width=\"12\"></td>", tarjetas.Select(t => $"""
                <td width="50%" style="vertical-align:top;background:#FFFFFF;border:1px solid {Borde};border-radius:14px;padding:16px">
                  {Icono(t.Icono, 32)}
                  <div style="font:700 14px Arial,Helvetica,sans-serif;color:{Texto};padding-top:10px">{t.Titulo}</div>
                  <div style="font:13px/19px Arial,Helvetica,sans-serif;color:{Suave};padding-top:4px">{t.Texto}</div>
                </td>
                """));
            return $"""<tr><td colspan="2"><table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>{celdas}</tr></table></td></tr>""";
        }

        protected static string Pie(string motivo) => $"""
            <tr><td style="background:{AzulOscuro};padding:26px 32px">
              <div style="font:700 18px Arial,Helvetica,sans-serif;color:#FFFFFF">CuidApp</div>
              <div style="font:13px Arial,Helvetica,sans-serif;color:#C9D9F0;padding-top:4px">Cuidado en quien puedes confiar</div>
              <div style="font:12px/18px Arial,Helvetica,sans-serif;color:#8FA6BF;padding-top:16px">{motivo}<br>Santo Domingo, República Dominicana</div>
            </td></tr>
            """;

        protected static string Envolver(string titulo, string filas) => $"""
            <!DOCTYPE html>
            <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{WebUtility.HtmlEncode(titulo)}</title></head>
            <body style="margin:0;padding:0;background:#E9EEF4">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#E9EEF4"><tr><td align="center" style="padding:24px 12px">
                <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;background:#FFFFFF;border-radius:18px;overflow:hidden">
                  {filas}
                </table>
              </td></tr></table>
            </body></html>
            """;

        // ------------------------------------------------------------------ formato

        protected static string E(string s) => WebUtility.HtmlEncode(s);
        protected static string PrimerNombre(string n) => string.IsNullOrWhiteSpace(n) ? "" : n.Trim().Split(' ')[0];
        protected static string Dinero(decimal m) => "RD$ " + m.ToString("N2", Es);
        protected static string FechaCorta(DateTime f) => f.ToString("dd/MM/yyyy", Es);
        protected static string FechaLarga(DateTime f)
        {
            var t = f.ToString("dddd d 'de' MMMM", Es);
            return char.ToUpper(t[0], Es) + t[1..];
        }
        protected static string Hora(DateTime? f) => f.HasValue ? f.Value.ToString("h:mm tt", Es) : "";
        protected static string HoraSpan(TimeSpan t) => DateTime.Today.Add(t).ToString("h:mm tt", Es);

    }
}
