using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CUIDAPP_API.Interfaces.Admin;
namespace CUIDAPP_API.Controllers;
[ApiController, Route("api/seguimiento")]
public class SeguimientoController(IAdminOperacionesService db, IConfiguration config, CUIDAPP_API.Interfaces.SOS.ISOSAlertService sos) : ControllerBase
{
    static bool TokenValido(string token) => token.Length == 43 && token.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');

    /// <summary>
    /// SOS desde la página pública de seguimiento: el familiar con el enlace avisa al centro de mando de CuidaApp.
    /// Queda como alerta del cliente que compartió el enlace, con la última ubicación conocida. Un aviso pendiente
    /// cada 10 minutos por servicio (evita repetirlo sin querer).
    /// </summary>
    [AllowAnonymous, EnableRateLimiting("seguimiento"), HttpPost("{token}/sos")]
    public async Task<IActionResult> Sos(string token)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TokenValido(token)) return NotFound();
        var fila = await db.EjecutarAsync("sp_SeguimientoSOS", ("@TokenHash", SHA256.HashData(Encoding.UTF8.GetBytes(token))));
        switch (fila?["Resultado"]?.ToString())
        {
            case "OK": break;
            case "YA_ENVIADA": return Ok(new { motivo = "YA_ENVIADA", message = "Ya avisamos al equipo de CuidaApp. Te contactarán en breve." });
            default: return NotFound(new { motivo = "NO_DISPONIBLE", message = "El seguimiento ya no está disponible." });
        }
        var alerta = await sos.CrearAlertaSeguimientoAsync(new CUIDAPP_API.DTOs.SOS.CrearSOSAlertaDto
        {
            TrabajoId = Convert.ToInt32(fila["TrabajoId"]),
            UsuarioId = Convert.ToInt32(fila["ClienteId"]),
            TipoUsuario = "Cliente",
            Latitud = Convert.ToDouble(fila["Latitud"]),
            Longitud = Convert.ToDouble(fila["Longitud"]),
            Motivo = "SOS enviado por un familiar desde el enlace de seguimiento compartido."
        });
        return alerta == null
            ? StatusCode(500, new { motivo = "ERROR", message = "No se pudo enviar el aviso. Llama al 9-1-1 si es una emergencia." })
            : Ok(new { motivo = "OK", message = "Avisamos al equipo de CuidaApp. Te contactarán en breve." });
    }

    int? Identidad => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) && User.Identity?.IsAuthenticated == true ? id : null;
    [HttpPost("trabajos/{trabajoId:int}/{clienteId:int}")]
    public async Task<IActionResult> Crear(int trabajoId, int clienteId)
    {
        if (Identidad != clienteId) return Unauthorized();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
        var row = await db.EjecutarAsync("sp_SeguimientoCrear", ("@TrabajoId",trabajoId),("@ClienteId",clienteId),("@TokenHash",SHA256.HashData(Encoding.UTF8.GetBytes(token))));
        return row == null ? NotFound() : Ok(new { Url = config["SeguimientoPublico:UrlBase"]!.TrimEnd('/') + "/seguimiento/" + token });
    }
    [HttpGet("trabajos/{trabajoId:int}/{clienteId:int}")]
    public async Task<IActionResult> Listar(int trabajoId,int clienteId) => Identidad != clienteId ? Unauthorized() : Ok((await db.ConsultarAsync("sp_SeguimientoListar",("@TrabajoId",trabajoId),("@ClienteId",clienteId)))[0]);
    [HttpPost("{id:int}/revocar/{clienteId:int}")]
    public async Task<IActionResult> Revocar(int id,int clienteId) => Identidad != clienteId ? Unauthorized() : (await db.EjecutarAsync("sp_SeguimientoRevocar",("@Id",id),("@ClienteId",clienteId))) == null ? NotFound() : NoContent();
    [AllowAnonymous, EnableRateLimiting("seguimiento"), HttpGet("{token}")]
    public async Task<IActionResult> Publico(string token)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (token.Length != 43 || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c!='-' && c!='_')) return NotFound();
        var rows=await db.ConsultarAsync("sp_SeguimientoPublico",("@TokenHash",SHA256.HashData(Encoding.UTF8.GetBytes(token))));
        if (rows[0].Count==0) return NotFound();
        var servicio = rows[0][0];
        if (servicio["FotoUrl"] is string foto && foto.StartsWith("/uploads/")) servicio["FotoUrl"] = $"{Request.Scheme}://{Request.Host}{Request.PathBase}{foto}";
        return Ok(new { Servicio=servicio, Tareas=rows[1], Actividades=rows[2] });
    }
}
