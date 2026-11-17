using CUIDAPP_API.DTOs.Cuenta;
using System.Security.Cryptography;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.Services;
using CUIDAPP_API.Services.Cuenta;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CUIDAPP_API.Controllers;

[ApiController, Route("api/admin/boveda"), Authorize(Policy=PoliticasAdmin.Boveda), AuditarAdmin, CuentaValidacion]
public class AdminBovedaController(IAdminOperacionesService db,BovedaCumplimientoService boveda,
    ILogger<AdminBovedaController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista()
    {
        var filas=(await db.ConsultarAsync("sp_BovedaLista"))[0];
        foreach(var fila in filas)
            fila["UsuariosIdEliminados"]=(fila.GetValueOrDefault("UsuariosIdEliminados")?.ToString() ?? "")
                .Split(',',StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        return Ok(filas);
    }

    [HttpPost("{documentoId:int:min(1)}/abrir")]
    public async Task<IActionResult> Abrir(int documentoId,AbrirBovedaDto dto,CancellationToken ct)
    {
        if((dto.Motivo?.Trim().Length ?? 0)<10)
            return BadRequest(new {motivo="MOTIVO_INVALIDO",message="Indica un motivo de al menos 10 caracteres."});
        var doc=await db.EjecutarAsync("sp_ContratoDocumento",("@DocumentoId",documentoId));
        if(doc==null || !Convert.ToBoolean(doc.GetValueOrDefault("EnBoveda") ?? false))
            return NotFound(new {motivo="NO_ENCONTRADO",message="Documento no encontrado en la bóveda."});
        try
        {
            var pdf=await boveda.AbrirAsync(doc,ct);
            await db.EjecutarAsync("sp_BovedaAcceso",("@DocumentoId",documentoId),("@AdminId",PoliticasAdmin.AdminId(User)),
                ("@Ahora",HoraLocalRD.Ahora),("@Motivo",dto.Motivo!.Trim()),("@Ip",HttpContext.Connection.RemoteIpAddress?.ToString()));
            return File(pdf,"application/pdf",$"Contrato-{doc["ContratoId"]}-documento-{documentoId}.pdf");
        }
        catch(CryptographicException ex)
        {
            logger.LogError(ex,"Fallo de integridad del documento de bóveda {Id}",documentoId);
            return Conflict(new {motivo="INTEGRIDAD_FALLIDA",message="No se pudo verificar la integridad del documento."});
        }
        catch(Exception ex) when(ex is IOException or InvalidOperationException or FormatException)
        {
            logger.LogError(ex,"No se pudo abrir el documento de bóveda {Id}",documentoId);
            return StatusCode(503,new {motivo="BOVEDA_NO_DISPONIBLE",message="La bóveda no está disponible temporalmente."});
        }
    }
}
