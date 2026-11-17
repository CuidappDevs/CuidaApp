using CUIDAPP_API.DTOs.Cuenta;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.Interfaces.Cuenta;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CUIDAPP_API.Controllers;

[ApiController, Route("api/EliminarCuenta"), EnableRateLimiting("eliminar-cuenta"), CUIDAPP_API.Seguridad.CuentaValidacion]
public class EliminarCuentaController(IEliminarCuentaService cuentas) : ControllerBase
{
    [HttpGet("revisar/{usuarioId:int:min(1)}")]
    public async Task<IActionResult> Revisar(int usuarioId)
    {
        var r=await cuentas.RevisarAsync(usuarioId);
        if(r[0].Count==0) return CuentaRespuesta.Error(this,new Fila { ["Resultado"]="NO_ENCONTRADO" });
        return Ok(new { puedeEliminar=!r[1].Any(b=>Convert.ToBoolean(b["Bloquea"])),
            bloqueos=r[1].Select(VistaBloqueo), solicitud=r[2].FirstOrDefault() is { } s ? new { fechaEjecucion=s["FechaEjecucion"] } : null });
    }
    private static object VistaBloqueo(Fila b)=>new { codigo=b["Codigo"],detalle=b["Detalle"],referenciaId=b["ReferenciaId"],bloquea=b["Bloquea"] };

    private async Task<IActionResult?> Bloqueos(int usuarioId)
    {
        var r=await cuentas.RevisarAsync(usuarioId);
        if(r[0].Count==0) return CuentaRespuesta.Error(this,new Fila { ["Resultado"]="NO_ENCONTRADO" });
        return r[1].Any(b=>Convert.ToBoolean(b["Bloquea"]))
            ? Conflict(new { motivo="BLOQUEOS_PENDIENTES",message="Debes resolver los asuntos pendientes antes de eliminar la cuenta.",bloqueos=r[1].Select(VistaBloqueo) }) : null;
    }

    [HttpPost("codigo")]
    public Task<IActionResult> Codigo(UsuarioEliminacionDto dto)=>Enviar(dto.UsuarioId,"app");

    private async Task<IActionResult> Enviar(int usuarioId,string origen)
    {
        if(await Bloqueos(usuarioId) is { } error) return error;
        var r=await cuentas.EnviarCodigoAsync(usuarioId,origen);
        if(r?["Resultado"]?.ToString()=="BLOQUEOS_PENDIENTES" && await Bloqueos(usuarioId) is { } bloqueo) return bloqueo;
        return r?["Resultado"]?.ToString()=="OK" ? Ok(new { message="Código enviado al correo de la cuenta.",expiraEnMinutos=10 }) : CuentaRespuesta.Error(this,r);
    }

    [HttpPost("confirmar")]
    public Task<IActionResult> Confirmar(ConfirmarEliminacionDto dto)=>ConfirmarUsuario(dto.UsuarioId,dto.Codigo,dto.Motivo,"app");

    private async Task<IActionResult> ConfirmarUsuario(int usuarioId,string codigo,string? motivo,string origen)
    {
        if(await Bloqueos(usuarioId) is { } error) return error;
        var r=await cuentas.ConfirmarAsync(usuarioId,codigo,motivo,origen);
        if(r?["Resultado"]?.ToString()=="BLOQUEOS_PENDIENTES" && await Bloqueos(usuarioId) is { } bloqueo) return bloqueo;
        return r?["Resultado"]?.ToString()=="OK" ? Ok(new { fechaEjecucion=r["FechaEjecucion"] }) : CuentaRespuesta.Error(this,r);
    }

    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar(CancelarEliminacionDto dto)
    {
        var id=await cuentas.ValidarCredencialesAsync(dto.Correo,dto.Contrasena);
        if(id==null || (dto.UsuarioId.HasValue && dto.UsuarioId!=id)) return CredencialesInvalidas();
        var r=await cuentas.CancelarAsync(id,null,"usuario",null);
        return r?["Resultado"]?.ToString()=="OK" ? Ok(new { ok=true }) : CuentaRespuesta.Error(this,r);
    }

    [HttpPost("web")]
    public async Task<IActionResult> Web(CredencialesEliminacionDto dto)
    {
        var id=await cuentas.ValidarCredencialesAsync(dto.Correo,dto.Contrasena);
        return id==null ? CredencialesInvalidas() : await Enviar(id.Value,"web");
    }

    [HttpPost("web/confirmar")]
    public async Task<IActionResult> WebConfirmar(ConfirmarWebEliminacionDto dto)
    {
        var id=await cuentas.ValidarCredencialesAsync(dto.Correo,dto.Contrasena);
        return id==null ? CredencialesInvalidas() : await ConfirmarUsuario(id.Value,dto.Codigo,dto.Motivo,"web");
    }
    private IActionResult CredencialesInvalidas()=>Unauthorized(new { motivo="CREDENCIALES_INVALIDAS",message="Correo o contraseña incorrectos." });
}
