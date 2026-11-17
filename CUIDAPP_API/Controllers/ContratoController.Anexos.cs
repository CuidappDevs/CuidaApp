using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Contrato;
using CUIDAPP_API.Services.Contrato;
namespace CUIDAPP_API.Controllers;
public partial class ContratoController
{
 [HttpGet("{id:int}/anexos")]
 public Task<IActionResult> Anexos(int id,[FromQuery] int usuarioId) => ValidarUsuario(usuarioId) is {} e ? Task.FromResult(e) : Leer("anexos",async()=> (await _db.ConsultarAsync("sp_ContratoAnexos",("@ContratoId",id),("@UsuarioId",usuarioId)))[0]);
 [HttpPost("{id:int}/anexos")]
 public async Task<IActionResult> ProponerAnexo(int id,ProponerAnexoDto dto)
 {
  if(ValidarUsuario(dto.UsuarioId) is {} e) return e;
  if(dto.TarifaHora==null && dto.Horario==null) return Error("DATOS_INCOMPLETOS");
  var horario=ContratoService.HorarioTexto(dto.Horario);
  if(dto.Horario!=null && string.IsNullOrEmpty(horario)) return Error("HORARIO_INVALIDO");
  var f=await _db.EjecutarAsync("sp_ContratoAnexoProponer",("@ContratoId",id),("@UsuarioId",dto.UsuarioId),("@TarifaHora",dto.TarifaHora),("@Horario",horario),("@FechaInicio",dto.FechaInicio.Date),("@Ahora",Ahora));
  var r=f?["Resultado"]?.ToString()??"ERROR"; if(r!="OK") return Error(r);
  await _contratos.NotificarAsync(Convert.ToInt32(f!["NotificarA"]),id,"anexo"); return Ok(f);
 }
 [HttpPost("anexos/{anexoId:int}/responder")]
 public async Task<IActionResult> ResponderAnexo(int anexoId,ResponderContratoDto dto)
 {
  if(ValidarUsuario(dto.UsuarioId) is {} e) return e;
  var f=await _db.EjecutarAsync("sp_ContratoAnexoResponder",("@AnexoId",anexoId),("@UsuarioId",dto.UsuarioId),("@Accion",dto.Accion),("@Ahora",Ahora));
  var r=f?["Resultado"]?.ToString()??"ERROR"; if(r!="OK") return Error(r);
  await _contratos.NotificarAsync(Convert.ToInt32(f!["NotificarA"]),Convert.ToInt32(f["ContratoId"]),"anexo"); return Ok(f);
 }
 [HttpPost("anexos/{anexoId:int}/codigo")]
 public async Task<IActionResult> CodigoAnexo(int anexoId,CodigoFirmaDto dto)
 {
  if(ValidarUsuario(dto.UsuarioId) is {} e) return e;
  var (r,correo)=await _contratos.EnviarCodigoAnexoAsync(anexoId,dto.UsuarioId);
  return r=="OK"?Ok(new {Correo=correo}):Error(r);
 }
 [HttpPost("anexos/{anexoId:int}/firmar")]
 public async Task<IActionResult> FirmarAnexo(int anexoId,FirmarContratoDto dto)
 {
  if(ValidarUsuario(dto.UsuarioId) is {} e) return e;
  var (r,activo)=await _contratos.FirmarAnexoAsync(anexoId,dto,HttpContext.Connection.RemoteIpAddress?.ToString());
  return r=="OK"?Ok(new {Activado=activo}):Error(r);
 }
}
