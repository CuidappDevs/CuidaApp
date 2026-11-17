using Microsoft.AspNetCore.Mvc;
namespace CUIDAPP_API.Controllers;
public partial class ContratoController
{
 [HttpGet("resumen-dashboard/{usuarioId:int}")]
 public Task<IActionResult> ResumenDashboard(int usuarioId) => ValidarUsuario(usuarioId) is {} error ? Task.FromResult(error) : Leer("resumen dashboard",async()=> (await _db.ConsultarAsync("sp_ContratosResumenDashboard",("@UsuarioId",usuarioId),("@Hoy",Ahora.Date)))[0]);
}
