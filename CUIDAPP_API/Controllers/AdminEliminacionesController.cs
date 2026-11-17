using CUIDAPP_API.DTOs.Cuenta;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Interfaces.Cuenta;
using CUIDAPP_API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CUIDAPP_API.Controllers;

[ApiController,Route("api/AdminEliminaciones"),AuditarAdmin,Authorize(Policy=PoliticasAdmin.Admin),CuentaValidacion]
public class AdminEliminacionesController(IAdminOperacionesService db,IEliminarCuentaService cuentas) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Lista([FromQuery] byte? estado)=>Ok((await db.ConsultarAsync("sp_AdminEliminaciones",("@Estado",estado)))[0]);
    [HttpGet("usuario/{usuarioId:int:min(1)}")]
    public async Task<IActionResult> Usuario(int usuarioId)=>Ok((await db.ConsultarAsync("sp_AdminEliminaciones",("@UsuarioId",usuarioId)))[0]);
    [HttpPost("{id:int:min(1)}/cancelar"),Authorize(Policy=PoliticasAdmin.Operaciones)]
    public async Task<IActionResult> Cancelar(int id,NotaCuentaDto dto)
    {
        if(string.IsNullOrWhiteSpace(dto.Nota)) return BadRequest(new { motivo="NOTA_REQUERIDA",message="Debes indicar una nota." });
        var r=await cuentas.CancelarAsync(null,id,"admin",dto.Nota);
        return r?["Resultado"]?.ToString()=="OK" ? Ok(new { ok=true }) : CuentaRespuesta.Error(this,r);
    }
}
