using CUIDAPP_API.DTOs.Cuenta;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.Services;
using CUIDAPP_API.Services.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CUIDAPP_API.Controllers;

[ApiController,Route("api/AdminDeudas"),AuditarAdmin,Authorize(Policy=PoliticasAdmin.Admin),CuentaValidacion]
public class AdminDeudasController(IAdminOperacionesService db,ITrabajoNotifier notifier,
    ILogger<AdminDeudasController>? logger=null) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista([FromQuery] byte? estado,[FromQuery] int? clienteId)
        => Ok((await db.ConsultarAsync("sp_DeudasClienteLeer",("@Estado",estado),("@ClienteId",clienteId)))[0]);

    [HttpPost("{id:int:min(1)}/confirmar-pago"),Authorize(Policy=PoliticasAdmin.Finanzas)]
    public Task<IActionResult> ConfirmarPago(int id,NotaCuentaDto dto)=>Accion(id,"confirmar-pago",dto.Nota);
    [HttpPost("{id:int:min(1)}/condonar"),Authorize(Policy=PoliticasAdmin.Finanzas)]
    public Task<IActionResult> Condonar(int id,NotaCuentaDto dto)=>Accion(id,"condonar",dto.Nota);
    [HttpPost("{id:int:min(1)}/ajustar"),Authorize(Policy=PoliticasAdmin.Finanzas)]
    public Task<IActionResult> Ajustar(int id,AjustarDeudaDto dto)
        => CUIDAPP_API.Services.Trabajo.TrabajoService.MontoRecibidoValido(dto.Monto)
            ? Accion(id,"ajustar",dto.Nota,dto.Monto)
            : Task.FromResult(CuentaRespuesta.Error(this,new CUIDAPP_API.DTOs.AdminOperaciones.Fila { ["Resultado"]="MONTO_INVALIDO" }));
    [HttpPost("{id:int:min(1)}/reabrir"),Authorize(Policy=PoliticasAdmin.Finanzas)]
    public Task<IActionResult> Reabrir(int id,NotaCuentaDto dto)=>Accion(id,"reabrir",dto.Nota);

    private async Task<IActionResult> Accion(int id,string accion,string? nota,decimal? monto=null)
    {
        var r=await db.EjecutarAsync("sp_DeudaClienteAccion",("@Id",id),("@Accion",accion),("@Nota",nota?.Trim()),
            ("@Monto",monto),("@AdminId",PoliticasAdmin.AdminId(User)),("@Ahora",HoraLocalRD.Ahora));
        if(r?["Resultado"]?.ToString()!="OK") return CuentaRespuesta.Error(this,r);
        try { await notifier.NotificarAsync(Convert.ToInt32(r["ClienteId"]),"CuentaActualizada",new { tipo="deuda_cliente" }); }
        catch(Exception ex) { logger?.LogWarning(ex,"La acción financiera de deuda {Id} se guardó pero no se pudo notificar",id); }
        return Ok(new { ok=true });
    }
}
