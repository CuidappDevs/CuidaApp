using CUIDAPP_API.DTOs.Cuenta;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Services;
using CUIDAPP_API.Services.Realtime;
using Microsoft.AspNetCore.Mvc;

namespace CUIDAPP_API.Controllers;

[ApiController, Route("api/DeudaCliente"), CUIDAPP_API.Seguridad.CuentaValidacion]
public class DeudaClienteController(IAdminOperacionesService db, ITrabajoNotifier notifier,
    ILogger<DeudaClienteController>? logger=null) : ControllerBase
{
    // Proyección explícita: no entrega correo ni notas privadas del cliente en las lecturas de la app.
    private static object Vista(Fila d) => new { id=d["Id"], trabajoId=d["TrabajoId"], fechaServicio=d["FechaServicio"],
        nombreCuidador=d["NombreCuidador"], montoTotal=d["MontoTotal"], montoRecibido=d["MontoRecibido"],
        monto=d["Monto"], estado=d["Estado"], comprobanteUrl=d["ComprobanteUrl"], notaAdmin=d["NotaAdmin"], fechaCreacion=d["FechaCreacion"], fechaLimite=d.GetValueOrDefault("FechaLimite"), vencida=d.GetValueOrDefault("Vencida") ?? false };

    [HttpGet("cliente/{clienteId:int:min(1)}")]
    public async Task<IActionResult> Cliente(int clienteId)
    {
        var deudas=(await db.ConsultarAsync("sp_DeudasClienteLeer", ("@ClienteId", clienteId)))[0];
        var total=deudas.Where(d=>Convert.ToInt32(d["Estado"]) is 1 or 3).Sum(d=>Convert.ToDecimal(d["Monto"]));
        return Ok(new { totalPendiente=total, bloqueado=deudas.Any(d=>Convert.ToInt32(d["Estado"]) is 1 or 3), deudas=deudas.Select(Vista) });
    }

    [HttpGet("trabajo/{trabajoId:int:min(1)}")]
    public async Task<IActionResult> Trabajo(int trabajoId)
    {
        var deuda=await db.EjecutarAsync("sp_DeudasClienteLeer", ("@TrabajoId", trabajoId));
        return deuda == null ? NoContent() : Ok(Vista(deuda));
    }

    [HttpPost("{deudaId:int:min(1)}/comprobante")]
    public Task<IActionResult> Comprobante(int deudaId, ComprobanteDeudaDto dto)
        => Accion(deudaId,"comprobante",dto.ClienteId,dto.ComprobanteUrl,dto.Nota);

    [HttpPost("{deudaId:int:min(1)}/objetar")]
    public Task<IActionResult> Objetar(int deudaId, ObjetarDeudaDto dto)
        => Accion(deudaId,"objetar",dto.ClienteId,null,dto.Motivo);

    private async Task<IActionResult> Accion(int id,string accion,int clienteId,string? url,string? nota)
    {
        var r=await db.EjecutarAsync("sp_DeudaClienteAccion",("@Id",id),("@Accion",accion),("@ClienteId",clienteId),
            ("@ComprobanteUrl",url?.Trim()),("@Nota",nota?.Trim()),("@Ahora",HoraLocalRD.Ahora));
        if(r?["Resultado"]?.ToString()!="OK") return CuentaRespuesta.Error(this,r);
        try { await notifier.NotificarAsync(clienteId,"CuentaActualizada",new { tipo="deuda_cliente" }); }
        catch(Exception ex) { logger?.LogWarning(ex,"La deuda {Id} se actualizó pero no se pudo notificar",id); }
        return Ok(new { ok=true });
    }
}
