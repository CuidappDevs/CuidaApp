using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.DTOs.Contrato;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Interfaces.Email;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.Services.Realtime;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CUIDAPP_API.Services.Contrato
{
    /// <summary>
    /// Contratos fijos: firma (código por correo + trazo), PDF del contrato firmado, almacenamiento
    /// privado con hash SHA-256, enlaces temporales de descarga para la app y avisos en tiempo real.
    /// Los PDF viven fuera de wwwroot (carpeta "AlmacenPrivado", por defecto {ContentRoot}/privado),
    /// así que nunca se sirven como archivos estáticos.
    /// </summary>
    public partial class ContratoService
    {
        public const string Evento = "ContratoActualizado";

        private readonly IAdminOperacionesService _db;
        private readonly IEmailService _email;
        private readonly ITrabajoNotifier _notifier;
        private readonly ILogger<ContratoService> _logger;
        private readonly string _raiz;
        private readonly byte[] _claveEnlaces;
        private static readonly CultureInfo Es = new("es-DO");

        static ContratoService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public ContratoService(IAdminOperacionesService db, IEmailService email, ITrabajoNotifier notifier,
            IConfiguration config, IWebHostEnvironment env, ILogger<ContratoService> logger)
        {
            _db = db;
            _email = email;
            _notifier = notifier;
            _logger = logger;
            _raiz = config["AlmacenPrivado"] is { Length: > 0 } r ? r : Path.Combine(env.ContentRootPath, "privado");
            _claveEnlaces = SHA256.HashData(Encoding.UTF8.GetBytes("contratos:" + PoliticasAdmin.ClaveJwt(config)));
        }

        // ------------------------------------------------------------------ utilidades

        /// <summary>Horario de la API → texto del SP ("1|08:00|14:00;3|08:00|14:00").</summary>
        public static string? HorarioTexto(IEnumerable<FranjaContratoDto>? franjas)
        {
            if (franjas == null) return null;
            var partes = new List<string>();
            foreach (var f in franjas)
            {
                if (!TimeSpan.TryParse(f.HoraInicio, CultureInfo.InvariantCulture, out var i) ||
                    !TimeSpan.TryParse(f.HoraFin, CultureInfo.InvariantCulture, out var fin))
                    return "";
                partes.Add($"{f.DiaSemana}|{i:hh\\:mm}|{fin:hh\\:mm}");
            }
            return string.Join(';', partes);
        }

        public Task NotificarAsync(int usuarioId, int contratoId, object? estado)
            => _notifier.NotificarAsync(usuarioId, Evento, new { ContratoId = contratoId, Estado = estado });

        private static string HashCodigo(int contratoId, int usuarioId, string codigo)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{contratoId}:{usuarioId}:{codigo.Trim()}")));

        // ------------------------------------------------------------------ firma

        /// <summary>Genera un código de 6 dígitos (10 min) y lo envía al correo del usuario.</summary>
        public async Task<(string Resultado, string? CorreoOculto)> EnviarCodigoAsync(int contratoId, int usuarioId)
        {
            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var fila = await _db.EjecutarAsync("sp_ContratoCodigoGenerar",
                ("@ContratoId", contratoId), ("@UsuarioId", usuarioId),
                ("@CodigoHash", HashCodigo(contratoId, usuarioId, codigo)), ("@Expira", HoraLocalRD.Ahora.AddMinutes(10)));
            var resultado = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (resultado != "OK")
                return (resultado, null);

            var correo = fila!["Email"]?.ToString() ?? "";
            var nombre = fila["NombreCompleto"]?.ToString() ?? "";
            var html = CorreoSimple($"Tu código para firmar el contrato",
                $"Hola {E(PrimerNombre(nombre))}, este es tu código para firmar el contrato fijo CF-{contratoId:D5} en CuidApp:",
                $"<div style=\"font:700 34px Arial,Helvetica,sans-serif;letter-spacing:8px;color:#1C4D96;padding:18px 0\">{codigo}</div>" +
                "<div style=\"font:13px Arial,Helvetica,sans-serif;color:#4B5563\">Vence en 10 minutos. Si no estás firmando un contrato, ignora este correo y avisa a soporte.</div>");
            await _email.EnviarAsync(correo, "Código para firmar tu contrato · CuidApp", html);
            return ("OK", OcultarCorreo(correo));
        }

        /// <summary>Registra la firma. Si ya firmaron los dos, genera el PDF y activa el contrato.</summary>
        public async Task<(string Resultado, bool Activado)> FirmarAsync(int contratoId, FirmarContratoDto dto, string? ip)
        {
            var svg = ArmarSvgFirma(dto.Trazos, dto.Ancho, dto.Alto);
            if (svg == null)
                return ("FIRMA_VACIA", false);

            var fila = await _db.EjecutarAsync("sp_ContratoFirmar",
                ("@ContratoId", contratoId), ("@UsuarioId", dto.UsuarioId),
                ("@CodigoHash", HashCodigo(contratoId, dto.UsuarioId, dto.Codigo)), ("@FirmaSvg", svg), ("@Ip", ip),
                ("@Latitud", dto.Latitud.HasValue ? Math.Round((decimal)dto.Latitud.Value, 6) : null),
                ("@Longitud", dto.Longitud.HasValue ? Math.Round((decimal)dto.Longitud.Value, 6) : null),
                ("@Ahora", HoraLocalRD.Ahora));
            var resultado = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (resultado != "OK")
                return (resultado, false);

            if (fila!["FirmasCompletas"] is true)
            {
                await ActivarAsync(contratoId);
                return ("OK", true);
            }

            if (fila["NotificarA"] is int otro)
                await NotificarAsync(otro, contratoId, 3);
            return ("OK", false);
        }

        [GeneratedRegex(@"^[MLml0-9.\s,\-]+$")]
        private static partial Regex TrazosValidos();

        /// <summary>Solo se aceptan comandos M/L con números: el SVG lo arma el servidor.</summary>
        private static string? ArmarSvgFirma(string trazos, double ancho, double alto)
        {
            trazos = trazos.Trim();
            if (!TrazosValidos().IsMatch(trazos) || trazos.Count(c => c is 'L' or 'l') < 5)
                return null;
            var a = ancho.ToString("0.#", CultureInfo.InvariantCulture);
            var h = alto.ToString("0.#", CultureInfo.InvariantCulture);
            return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {a} {h}\" width=\"{a}\" height=\"{h}\">" +
                   $"<path d=\"{trazos}\" fill=\"none\" stroke=\"#0A2F41\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>";
        }

        // ------------------------------------------------------------------ activación + PDF

        private async Task ActivarAsync(int contratoId)
        {
            var detalle = await _db.ConsultarAsync("sp_ContratoDetalle", ("@ContratoId", contratoId), ("@UsuarioId", null));
            var c = detalle[0].FirstOrDefault();
            if (c == null) return;

            var pdf = GenerarPdf(c, detalle[1], detalle[4]);
            var hash = Convert.ToHexString(SHA256.HashData(pdf));
            var ahora = HoraLocalRD.Ahora;
            var relativa = Path.Combine("contratos", ahora.Year.ToString(), contratoId.ToString(), $"CF-{contratoId:D5}-v1-{ahora:yyyyMMddHHmmss}.pdf");
            var completa = Path.Combine(_raiz, relativa);
            Directory.CreateDirectory(Path.GetDirectoryName(completa)!);
            await File.WriteAllBytesAsync(completa, pdf);

            var fila = await _db.EjecutarAsync("sp_ContratoActivar", ("@ContratoId", contratoId), ("@Ruta", relativa), ("@Hash", hash), ("@Ahora", ahora));
            if (fila?["Resultado"]?.ToString() != "OK")
            {
                _logger.LogWarning("No se pudo activar el contrato {Id}: {R}", contratoId, fila?["Resultado"]);
                return;
            }

            // Primeros servicios de la semana, sin esperar a la pasada horaria.
            try { await GenerarTrabajosAsync(contratoId); }
            catch (Exception ex) { _logger.LogWarning(ex, "No se generaron los trabajos del contrato {Id}", contratoId); }

            var clienteId = Convert.ToInt32(c["ClienteId"]);
            var cuidadorId = Convert.ToInt32(c["CuidadorId"]);
            await NotificarAsync(clienteId, contratoId, 4);
            await NotificarAsync(cuidadorId, contratoId, 4);

            var nombreArchivo = $"Contrato-CF-{contratoId:D5}.pdf";
            foreach (var (correo, nombre) in new[] { (c["ClienteEmail"]?.ToString(), c["ClienteNombre"]?.ToString()), (c["CuidadorEmail"]?.ToString(), c["CuidadorNombre"]?.ToString()) })
            {
                if (string.IsNullOrWhiteSpace(correo)) continue;
                try
                {
                    var html = CorreoSimple("Tu contrato fijo está firmado",
                        $"Hola {E(PrimerNombre(nombre ?? ""))}, las dos partes firmaron el contrato CF-{contratoId:D5}. Te adjuntamos la copia en PDF.",
                        $"<div style=\"font:13px/20px Arial,Helvetica,sans-serif;color:#4B5563\">Código de integridad (SHA-256):<br><span style=\"font-family:Consolas,monospace;font-size:11px;word-break:break-all\">{hash}</span><br><br>También puedes verlo en la app, en <b>Mis contratos</b>.</div>");
                    await _email.EnviarConAdjuntoAsync(correo, $"Contrato CF-{contratoId:D5} firmado · CuidApp", html, nombreArchivo, pdf, "application/pdf");
                }
                catch (Exception ex) { _logger.LogWarning(ex, "No se pudo enviar el PDF del contrato {Id} a {Correo}", contratoId, correo); }
            }
        }

        /// <summary>Genera los trabajos (de un contrato o de todos) y avisa a las dos partes.</summary>
        public async Task<int> GenerarTrabajosAsync(int? contratoId = null)
        {
            var filas = (await _db.ConsultarAsync("sp_ContratosGenerarTrabajos", ("@Ahora", HoraLocalRD.Ahora), ("@Dias", 7), ("@ContratoId", contratoId))).LastOrDefault() ?? new();
            foreach (var f in filas)
            {
                if (f["TrabajoId"] is not int trabajoId) continue;
                await _notifier.NotificarAsync(Convert.ToInt32(f["ClienteId"]), "TrabajoActualizado", new { TrabajoId = trabajoId, Estado = 2 });
                await _notifier.NotificarAsync(Convert.ToInt32(f["CuidadorId"]), "TrabajoActualizado", new { TrabajoId = trabajoId, Estado = 2 });
            }
            return filas.Count;
        }

        private static string Dia(int d) => Es.DateTimeFormat.GetDayName((DayOfWeek)d) is var n ? char.ToUpper(n[0]) + n[1..] : "";
        private static string Hora(object? v) => TimeSpan.TryParse(v?.ToString(), out var t) ? DateTime.Today.Add(t).ToString("h:mm tt", Es) : "";
        private static string Fecha(object? v) => v is DateTime d ? d.ToString("d 'de' MMMM 'de' yyyy", Es) : "—";
        private static string Txt(object? v) => v?.ToString() ?? "";
        private static string Dinero(decimal m) => "RD$ " + m.ToString("N2", Es);

        private static byte[] GenerarPdf(Fila c, List<Fila> horario, List<Fila> firmas)
        {
            var id = Convert.ToInt32(c["Id"]);
            var tarifa = Convert.ToDecimal(c["TarifaHora"]);
            var horasSemana = horario.Sum(h => TimeSpan.TryParse(Txt(h["HoraFin"]), out var f) && TimeSpan.TryParse(Txt(h["HoraInicio"]), out var i) ? (f - i).TotalHours : 0);
            var metodo = Txt(c["MetodoPago"]) switch { "efectivo" => "Efectivo", "transferencia" => "Transferencia", "tarjeta" => "Tarjeta", var m => m };
            const string azul = "#1C4D96", texto = "#0A2F41", suave = "#4B5563", borde = "#D9E2EC";

            return Document.Create(doc => doc.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(46);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(texto).LineHeight(1.35f));

                page.Header().PaddingBottom(14).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("CuidApp").FontSize(20).Bold().FontColor(azul);
                        col.Item().Text("Cuidado en quien puedes confiar").FontSize(9).FontColor(suave);
                    });
                    row.ConstantItem(170).AlignRight().Column(col =>
                    {
                        col.Item().AlignRight().Text($"Contrato CF-{id:D5}").Bold();
                        col.Item().AlignRight().Text($"Emitido {HoraLocalRD.Ahora.ToString("dd/MM/yyyy h:mm tt", Es)}").FontSize(8.5f).FontColor(suave);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Text("Acuerdo de servicio recurrente de cuidado").FontSize(16).Bold();
                    col.Item().Text(t =>
                    {
                        t.Span("Este acuerdo se celebra a través de la plataforma CuidApp entre las partes indicadas abajo, que aceptan las condiciones de este documento y lo firman electrónicamente desde la aplicación.").FontColor(suave);
                    });

                    void Seccion(string titulo, Action<ColumnDescriptor> contenido) => col.Item().Column(s =>
                    {
                        s.Spacing(4);
                        s.Item().BorderBottom(1).BorderColor(borde).PaddingBottom(3).Text(titulo).FontSize(11.5f).Bold().FontColor(azul);
                        contenido(s);
                    });
                    void Dato(ColumnDescriptor s, string etiqueta, string valor) => s.Item().Row(r =>
                    {
                        r.ConstantItem(140).Text(etiqueta).FontColor(suave);
                        r.RelativeItem().Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor);
                    });

                    Seccion("1. Partes", s =>
                    {
                        s.Item().Row(r =>
                        {
                            void Parte(RowDescriptor rd, string rol, string pre) => rd.RelativeItem().Column(p =>
                            {
                                p.Item().Text(rol).Bold();
                                p.Item().Text(Txt(c[pre + "Nombre"]));
                                p.Item().Text($"Documento: {Txt(c[pre + "Documento"])}").FontColor(suave);
                                p.Item().Text($"Teléfono: {Txt(c[pre + "Telefono"])}").FontColor(suave);
                                p.Item().Text($"Correo: {Txt(c[pre + "Email"])}").FontColor(suave);
                            });
                            Parte(r, "Cliente (contratante)", "Cliente");
                            r.ConstantItem(16);
                            Parte(r, "Care Partner (prestador)", "Cuidador");
                        });
                    });

                    Seccion("2. Domicilio del servicio", s =>
                    {
                        Dato(s, "Nombre", Txt(c["DomicilioNombre"]));
                        Dato(s, "Dirección", Txt(c["Direccion"]));
                        Dato(s, "Persona a cuidar", Txt(c["QuienVive"]));
                    });

                    Seccion("3. Servicio y horario", s =>
                    {
                        Dato(s, "Tipo de servicio", Txt(c["TipoServicio"]));
                        Dato(s, "Fecha de inicio", Fecha(c["FechaInicio"]));
                        Dato(s, "Fecha de fin", c["FechaFin"] is DateTime ? Fecha(c["FechaFin"]) : "Indefinida, hasta que una de las partes lo termine");
                        s.Item().PaddingTop(4).Table(t =>
                        {
                            t.ColumnsDefinition(cd => { cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(2); cd.RelativeColumn(1); });
                            t.Header(h =>
                            {
                                foreach (var e in new[] { "Día", "Desde", "Hasta", "Horas" })
                                    h.Cell().Background("#EEF3FA").Padding(5).Text(e).Bold().FontSize(9);
                            });
                            foreach (var f in horario)
                            {
                                var horas = TimeSpan.TryParse(Txt(f["HoraFin"]), out var fin) && TimeSpan.TryParse(Txt(f["HoraInicio"]), out var ini) ? (fin - ini).TotalHours : 0;
                                t.Cell().BorderBottom(0.5f).BorderColor(borde).Padding(5).Text(Dia(Convert.ToInt32(f["DiaSemana"])));
                                t.Cell().BorderBottom(0.5f).BorderColor(borde).Padding(5).Text(Hora(f["HoraInicio"]));
                                t.Cell().BorderBottom(0.5f).BorderColor(borde).Padding(5).Text(Hora(f["HoraFin"]));
                                t.Cell().BorderBottom(0.5f).BorderColor(borde).Padding(5).Text(horas.ToString("0.#", Es));
                            }
                        });
                    });

                    Seccion("4. Pago", s =>
                    {
                        Dato(s, "Tarifa por hora", Dinero(tarifa));
                        Dato(s, "Horas por semana", horasSemana.ToString("0.#", Es));
                        Dato(s, "Estimado semanal", Dinero(tarifa * (decimal)horasSemana));
                        Dato(s, "Método de pago", metodo);
                        s.Item().Text("Cada jornada se registra en CuidApp como un servicio y se paga según el flujo de pagos de la plataforma, con la comisión vigente de CuidApp. Las jornadas canceladas por ausencia no se cobran.").FontColor(suave).FontSize(9);
                    });

                    Seccion("5. Condiciones", s =>
                    {
                        var clausulas = new[]
                        {
                            "Naturaleza del acuerdo. El Care Partner presta el servicio de forma independiente a través de CuidApp. Este documento es un acuerdo de servicio recurrente y no crea una relación de empleo entre las partes ni con CuidApp.",
                            "Jornadas. CuidApp crea cada jornada según el horario acordado. Cada jornada usa las herramientas de seguridad de la plataforma: PIN de inicio y fin, ubicación durante el servicio, registro de actividades, alerta SOS y, si está activa, la grabación de seguridad consentida.",
                            "Ausencias. Cualquiera de las partes puede marcar en la aplicación los días en que no habrá servicio (vacaciones, enfermedad u otros). Esas jornadas no se generan ni se cobran.",
                            "Pausa y terminación. Las partes pueden pausar el acuerdo desde la aplicación. Para terminarlo se requiere un preaviso de " + Txt(c["DiasPreaviso"]) + " días, durante los cuales se mantienen las jornadas acordadas.",
                            "Cambios. Cualquier cambio de tarifa u horario debe acordarse por las dos partes dentro de CuidApp.",
                            "Confidencialidad. El Care Partner mantendrá en reserva la información personal y de salud de la persona cuidada y del hogar.",
                            "Conflictos. Cualquier reclamo se gestionará primero por el soporte de CuidApp, que podrá revisar la información registrada en la plataforma.",
                        };
                        for (var i = 0; i < clausulas.Length; i++)
                        {
                            var n = i + 1;
                            s.Item().Row(r => { r.ConstantItem(18).Text($"{n}.").FontColor(suave); r.RelativeItem().Text(clausulas[n - 1]).FontSize(9.5f); });
                        }
                        if (!string.IsNullOrWhiteSpace(Txt(c["Nota"])))
                            s.Item().PaddingTop(4).Background("#F5F8FC").Padding(8).Text(t => { t.Span("Nota de las partes: ").Bold(); t.Span(Txt(c["Nota"])); });
                    });

                    Seccion("6. Firmas", s =>
                    {
                        s.Item().Row(r =>
                        {
                            var lista = firmas.ToList();
                            void Firma(RowDescriptor rd, string rol, object? usuarioId)
                            {
                                var f = lista.FirstOrDefault(x => Convert.ToInt32(x["UsuarioId"]) == Convert.ToInt32(usuarioId));
                                rd.RelativeItem().Border(1).BorderColor(borde).Padding(10).Column(p =>
                                {
                                    p.Item().Text(rol).FontSize(9).FontColor(suave);
                                    if (f != null)
                                    {
                                        p.Item().Height(70).AlignCenter().Svg(Txt(f["FirmaSvg"])).FitArea();
                                        p.Item().BorderTop(1).BorderColor(texto).PaddingTop(4).Text(Txt(f["NombreCompleto"])).Bold();
                                        p.Item().Text($"Firmado: {(f["Fecha"] is DateTime fd ? fd.ToString("dd/MM/yyyy h:mm:ss tt", Es) : "")}").FontSize(8.5f).FontColor(suave);
                                        p.Item().Text($"Código verificado en: {Txt(f["Correo"])}").FontSize(8.5f).FontColor(suave);
                                        p.Item().Text($"IP: {Txt(f["Ip"])}" + (f["Latitud"] != null ? $" · GPS: {Convert.ToDecimal(f["Latitud"]).ToString(CultureInfo.InvariantCulture)}, {Convert.ToDecimal(f["Longitud"]).ToString(CultureInfo.InvariantCulture)}" : "")).FontSize(8.5f).FontColor(suave);
                                    }
                                });
                            }
                            Firma(r, "Cliente", c["ClienteId"]);
                            r.ConstantItem(14);
                            Firma(r, "Care Partner", c["CuidadorId"]);
                        });
                        s.Item().PaddingTop(4).Text("Firma electrónica simple: cada parte dibujó su firma en la aplicación después de confirmar un código de un solo uso enviado a su correo. CuidApp guarda el hash SHA-256 de este PDF para comprobar que no fue modificado.").FontSize(8.5f).FontColor(suave);
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor(suave));
                    t.Span($"CuidApp · Contrato CF-{id:D5} · Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
            })).GeneratePdf();
        }

        // ------------------------------------------------------------------ documentos

        /// <summary>Lee el PDF guardado y comprueba su hash.</summary>
        public async Task<(Fila? Doc, byte[]? Bytes, bool Integro)> LeerDocumentoAsync(int documentoId)
        {
            var doc = (await _db.ConsultarAsync("sp_ContratoDocumento", ("@DocumentoId", documentoId)))[0].FirstOrDefault();
            if (doc == null) return (null, null, false);
            var ruta = Path.GetFullPath(Path.Combine(_raiz, Txt(doc["Ruta"])));
            if (!ruta.StartsWith(Path.GetFullPath(_raiz), StringComparison.OrdinalIgnoreCase) || !File.Exists(ruta))
                return (doc, null, false);
            var bytes = await File.ReadAllBytesAsync(ruta);
            var integro = string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), Txt(doc["Hash"]), StringComparison.OrdinalIgnoreCase);
            return (doc, bytes, integro);
        }

        /// <summary>Firma "docId:expira" para un enlace de descarga válido 10 minutos.</summary>
        public (long Expira, string Firma) CrearEnlace(int documentoId)
        {
            var expira = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
            return (expira, FirmaEnlace(documentoId, expira));
        }

        public bool EnlaceValido(int documentoId, long expira, string? firma)
        {
            if (string.IsNullOrEmpty(firma) || DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expira) return false;
            var esperada = Encoding.ASCII.GetBytes(FirmaEnlace(documentoId, expira));
            var recibida = Encoding.ASCII.GetBytes(firma);
            return CryptographicOperations.FixedTimeEquals(esperada, recibida);
        }

        private string FirmaEnlace(int documentoId, long expira)
            => Convert.ToHexString(HMACSHA256.HashData(_claveEnlaces, Encoding.UTF8.GetBytes($"{documentoId}:{expira}"))).ToLowerInvariant();

        // ------------------------------------------------------------------ correo

        private static string E(string s) => WebUtility.HtmlEncode(s);
        private static string PrimerNombre(string n) => string.IsNullOrWhiteSpace(n) ? "" : n.Trim().Split(' ')[0];
        private static string OcultarCorreo(string correo)
        {
            var at = correo.IndexOf('@');
            return at <= 1 ? correo : correo[0] + new string('•', Math.Min(at - 1, 6)) + correo[at..];
        }

        private static string CorreoSimple(string titulo, string intro, string cuerpo) => $"""
            <!DOCTYPE html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{E(titulo)}</title></head>
            <body style="margin:0;padding:0;background:#E9EEF4">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#E9EEF4"><tr><td align="center" style="padding:24px 12px">
                <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;background:#FFFFFF;border-radius:18px;overflow:hidden">
                  <tr><td style="background:#1C4D96;padding:26px 32px;font:700 22px Arial,Helvetica,sans-serif;color:#FFFFFF">{E(titulo)}</td></tr>
                  <tr><td style="padding:26px 32px">
                    <div style="font:15px/22px Arial,Helvetica,sans-serif;color:#0A2F41">{intro}</div>
                    {cuerpo}
                  </td></tr>
                  <tr><td style="background:#0A2F41;padding:20px 32px;font:12px Arial,Helvetica,sans-serif;color:#8FA6BF">CuidApp · Santo Domingo, República Dominicana</td></tr>
                </table>
              </td></tr></table>
            </body></html>
            """;
    }
}
