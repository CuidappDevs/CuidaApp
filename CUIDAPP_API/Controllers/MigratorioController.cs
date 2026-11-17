using System.ComponentModel.DataAnnotations;
using System.Net;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Interfaces.Email;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.Services;
using CUIDAPP_API.Services.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CUIDAPP_API.Controllers;

[ApiController]
[Route("api")]
public class MigratorioController(IAdminOperacionesService db, ITrabajoNotifier notifier, IEmailService email) : ControllerBase
{
    [HttpGet("nacionalidad/estatus-migratorio")]
    public async Task<IActionResult> Estatus()
    {
        var r = await db.ConsultarAsync("sp_PilotoEstatusPublicos");
        return Ok(r[0].Select(e => new { id = e["Id"], nombre = e["Nombre"], requisitos = r[1]
            .Where(d => Convert.ToInt32(d["EstatusMigratorioId"]) == Convert.ToInt32(e["Id"]))
            .Select(d => d["TipoDocumento"]) }));
    }

    [Authorize(Policy = PoliticasAdmin.Admin)]
    [HttpGet("admin/cuidadores/{usuarioId:int}/migratorio")]
    public async Task<IActionResult> Detalle(int usuarioId)
    {
        var r = await Leer(usuarioId);
        var c = r[0].FirstOrDefault();
        if (c == null) return NotFound(new ApiErrorDto("NO_ENCONTRADO", "Cuidador no encontrado."));
        return Ok(new { esExtranjero = c["EsExtranjero"], nacionalidad = c["Nacionalidad"], estatusId = c["EstatusId"],
            estatus = c["Estatus"], documentos = r[1].Select(d => new { tipo = d["Tipo"], estado = d["Estado"],
                vencimiento = d["Vencimiento"], url = d["Url"], presente = d["Presente"] }),
            faltantes = Faltantes(r), puedeAprobar = r[2].Count == 0 && (c["EstatusId"] == null || Convert.ToBoolean(c["EstatusActivo"])) });
    }

    public class PedirDto { [StringLength(1000)] public string? Mensaje { get; set; } }

    [Authorize(Policy = PoliticasAdmin.Verificacion)]
    [AuditarAdmin]
    [HttpPost("admin/cuidadores/{usuarioId:int}/pedir-migratorio")]
    public async Task<IActionResult> Pedir(int usuarioId, [FromBody] PedirDto dto)
    {
        var r = await Leer(usuarioId);
        var c = r[0].FirstOrDefault();
        if (c == null) return NotFound(new ApiErrorDto("NO_ENCONTRADO", "Cuidador no encontrado."));
        await db.EjecutarAsync("sp_PilotoPedirMigratorio", ("@UsuarioId", usuarioId), ("@Mensaje", dto.Mensaje?.Trim()));
        var faltantes = Faltantes(r);
        await notifier.NotificarAsync(usuarioId, "CuentaActualizada", new { tipo = "documentos_migratorios", faltantes });
        await email.EnviarAsync(c["Email"]!.ToString()!, "Documentos migratorios pendientes",
            $"<p>Sube tu comprobante migratorio y los documentos pendientes desde tu perfil: {WebUtility.HtmlEncode(string.Join(", ", faltantes))}.</p><p>{WebUtility.HtmlEncode(dto.Mensaje)}</p>");
        return Ok(new { ok = true });
    }

    private Task<List<List<Fila>>> Leer(int usuarioId) => db.ConsultarAsync("sp_PilotoMigratorio", ("@UsuarioId", usuarioId), ("@Hoy", HoraLocalRD.Ahora.Date));
    private static string[] Faltantes(List<List<Fila>> r) => r[2].Select(d => d["TipoDocumento"]!.ToString()!).ToArray();
}
