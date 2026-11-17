using System.Security.Cryptography;
using CUIDAPP_API.DTOs.Contrato;
using CUIDAPP_API.DTOs.AdminOperaciones;
namespace CUIDAPP_API.Services.Contrato;
public partial class ContratoService
{
        public async Task<(string Resultado, string? CorreoOculto)> EnviarCodigoAnexoAsync(int contratoId, int usuarioId)
        {
            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var fila = await _db.EjecutarAsync("sp_AnexoCodigoGenerar",
                ("@AnexoId", contratoId), ("@UsuarioId", usuarioId),
                ("@CodigoHash", HashCodigo(contratoId, usuarioId, codigo)), ("@Expira", HoraLocalRD.Ahora.AddMinutes(10)), ("@Ahora", HoraLocalRD.Ahora));
            var resultado = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (resultado != "OK")
                return (resultado, null);

            var correo = fila!["Email"]?.ToString() ?? "";
            var nombre = fila["NombreCompleto"]?.ToString() ?? "";
            var html = CorreoSimple($"Tu código para firmar el anexo",
                $"Hola {E(PrimerNombre(nombre))}, este es tu código para firmar el anexo AF-{contratoId:D5} en CuidApp:",
                $"<div style=\"font:700 34px Arial,Helvetica,sans-serif;letter-spacing:8px;color:#1C4D96;padding:18px 0\">{codigo}</div>" +
                "<div style=\"font:13px Arial,Helvetica,sans-serif;color:#4B5563\">Vence en 10 minutos. Si no estás firmando un contrato, ignora este correo y avisa a soporte.</div>");
            await _email.EnviarAsync(correo, "Código para firmar tu contrato · CuidApp", html);
            return ("OK", OcultarCorreo(correo));
        }

        /// <summary>Registra la firma. Si ya firmaron los dos, genera el PDF y activa el contrato.</summary>
        public async Task<(string Resultado, bool Activado)> FirmarAnexoAsync(int contratoId, FirmarContratoDto dto, string? ip)
        {
            var svg = ArmarSvgFirma(dto.Trazos, dto.Ancho, dto.Alto);
            if (svg == null)
                return ("FIRMA_VACIA", false);

            var fila = await _db.EjecutarAsync("sp_AnexoFirmar",
                ("@AnexoId", contratoId), ("@UsuarioId", dto.UsuarioId),
                ("@CodigoHash", HashCodigo(contratoId, dto.UsuarioId, dto.Codigo)), ("@FirmaSvg", svg), ("@Ip", ip),
                ("@Latitud", dto.Latitud.HasValue ? Math.Round((decimal)dto.Latitud.Value, 6) : null),
                ("@Longitud", dto.Longitud.HasValue ? Math.Round((decimal)dto.Longitud.Value, 6) : null),
                ("@Ahora", HoraLocalRD.Ahora));
            var resultado = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (resultado != "OK")
                return (resultado, false);

            if (fila!["FirmasCompletas"] is true)
            {
                // La firma ya quedó guardada: si el PDF o la activación fallan, no se le devuelve error a
                // quien firmó; ActivarPendientesAsync lo reintenta.
                try
                {
                    return ("OK", await ActivarAnexoAsync(contratoId));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Las dos partes firmaron el contrato {Id} pero no se pudo activar", contratoId);
                    return ("OK", false);
                }
            }

            if (fila["NotificarA"] is int otro)
                await NotificarAnexoAsync(contratoId, otro);
            return ("OK", false);
        }


 private async Task NotificarAnexoAsync(int id,int usuario)
 {
  var a=(await _db.ConsultarAsync("sp_AnexoDetalle",("@AnexoId",id)))[0].FirstOrDefault();
  if(a!=null) await NotificarAsync(usuario,Convert.ToInt32(a["ContratoId"]),"anexo");
 }
 public async Task ActivarAnexosPendientesAsync()
 {
  foreach(var a in (await _db.ConsultarAsync("sp_AnexosPendientesActivar"))[0])
   try { await ActivarAnexoAsync(Convert.ToInt32(a["Id"])); } catch(Exception e) { _logger.LogError(e,"No se pudo activar anexo {Id}",a["Id"]); }
 }
 private async Task<bool> ActivarAnexoAsync(int id)
 {
  var r=await _db.ConsultarAsync("sp_AnexoDetalle",("@AnexoId",id)); var a=r[0].FirstOrDefault(); if(a==null)return false;
  var contrato=Convert.ToInt32(a["ContratoId"]);
  var detalle=await _db.ConsultarAsync("sp_ContratoDetalle",("@ContratoId",contrato),("@UsuarioId",null));
  var c=detalle[0][0]; c["TarifaHora"]=a["TarifaHora"]; c["FechaInicio"]=a["FechaInicio"];
  c["AnexoId"]=id;
  var horario=new List<Fila>();
  foreach(var f in a["Horario"]!.ToString()!.Split(';')) {var v=f.Split('|');horario.Add(new Fila { ["DiaSemana"]=int.Parse(v[0]),["HoraInicio"]=v[1],["HoraFin"]=v[2] });}
  var pdf=GenerarPdf(c,horario,r[1]);var hash=Convert.ToHexString(SHA256.HashData(pdf));
  var relativa=Path.Combine("contratos",contrato.ToString(),$"Anexo-{id}-{Guid.NewGuid():N}.pdf");
  var completa=Path.Combine(_raiz,relativa);Directory.CreateDirectory(Path.GetDirectoryName(completa)!);await File.WriteAllBytesAsync(completa,pdf);
  var fdb=await _db.EjecutarAsync("sp_AnexoActivar",("@AnexoId",id),("@Ruta",relativa),("@Hash",hash),("@Ahora",HoraLocalRD.Ahora));
  if(fdb?["Resultado"]?.ToString()!="OK") {File.Delete(completa);return false;}
  try { await GenerarTrabajosAsync(contrato); }
  catch(Exception ex) { _logger.LogWarning(ex,"Se reintentar? la generaci?n de jornadas del anexo {Id}",id); }
  await NotificarAsync(Convert.ToInt32(a["ClienteId"]),contrato,"anexo");await NotificarAsync(Convert.ToInt32(a["CuidadorId"]),contrato,"anexo");return true;
 }
}
