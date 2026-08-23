using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Trabajo;
using CUIDAPP_API.Interfaces.Trabajo;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrabajoController : ControllerBase
    {
        private readonly ITrabajoService _trabajoService;

        public TrabajoController(ITrabajoService trabajoService)
        {
            _trabajoService = trabajoService;
        }

        [HttpPost]
        public async Task<IActionResult> CrearTrabajo([FromBody] CrearTrabajoDto dto)
        {
            try
            {
                var trabajoId = await _trabajoService.CrearTrabajoAsync(dto);
                return Ok(new { Message = "Trabajo creado con éxito", TrabajoId = trabajoId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("cuidador/{cuidadorId}")]
        public async Task<IActionResult> ObtenerTrabajosPorCuidador(int cuidadorId)
        {
            try
            {
                var trabajos = await _trabajoService.ObtenerTrabajosPorCuidadorAsync(cuidadorId);
                return Ok(trabajos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("proximo/{cuidadorId}")]
        public async Task<IActionResult> ObtenerProximoTrabajo(int cuidadorId)
        {
            try
            {
                var trabajo = await _trabajoService.ObtenerProximoTrabajoAsync(cuidadorId);
                return Ok(trabajo);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("cliente/{clienteId}/activo")]
        public async Task<IActionResult> ObtenerTrabajoActivoPorCliente(int clienteId)
        {
            try
            {
                var trabajo = await _trabajoService.ObtenerTrabajoActivoPorClienteAsync(clienteId);
                return Ok(trabajo);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("cliente/{clienteId}/activos")]
        public async Task<IActionResult> ObtenerTrabajosActivosPorCliente(int clienteId)
        {
            try
            {
                var trabajos = await _trabajoService.ObtenerTrabajosActivosPorClienteAsync(clienteId);
                return Ok(trabajos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("{trabajoId}")]
        public async Task<IActionResult> ObtenerTrabajoPorId(int trabajoId)
        {
            try
            {
                var trabajo = await _trabajoService.ObtenerTrabajoPorIdAsync(trabajoId);
                if (trabajo == null)
                    return NotFound();
                return Ok(trabajo);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("iniciar")]
        public async Task<IActionResult> IniciarTrabajo([FromBody] IniciarTrabajoDto dto)
        {
            try
            {
                var (success, motivo) = await _trabajoService.IniciarTrabajoAsync(dto);
                if (success)
                    return Ok(new { Message = "Trabajo iniciado correctamente" });

                var mensaje = motivo switch
                {
                    "TRABAJO_NO_ENCONTRADO" => "El trabajo no existe.",
                    "ESTADO_INVALIDO" => "Este trabajo ya no está en estado Aceptado.",
                    "FECHA_FUTURA" => "Aún no es la fecha programada para este trabajo.",
                    "PIN_INCORRECTO" => "El código PIN es incorrecto.",
                    _ => "No se pudo iniciar el trabajo."
                };
                return BadRequest(new { Motivo = motivo, Message = mensaje });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("finalizar")]
        public async Task<IActionResult> FinalizarTrabajo([FromBody] FinalizarTrabajoDto dto)
        {
            try
            {
                var (success, motivo) = await _trabajoService.FinalizarTrabajoAsync(dto);
                if (success)
                    return Ok(new { Message = "Trabajo finalizado correctamente" });

                var mensaje = motivo switch
                {
                    "TRABAJO_NO_ENCONTRADO" => "El trabajo no existe.",
                    "ESTADO_INVALIDO" => "Este trabajo no está En progreso.",
                    "PIN_INCORRECTO" => "El código PIN de salida es incorrecto.",
                    _ => "No se pudo finalizar el trabajo."
                };
                return BadRequest(new { Motivo = motivo, Message = mensaje });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("confirmar-finalizacion")]
        public async Task<IActionResult> ConfirmarFinalizacion([FromBody] ConfirmarFinalizacionDto dto)
        {
            try
            {
                var (success, motivo) = await _trabajoService.ConfirmarFinalizacionAsync(dto.TrabajoId, dto.ClienteId, dto.Confirmado);
                if (success)
                    return Ok(new { Message = "Confirmación registrada" });

                var mensaje = motivo switch
                {
                    "TRABAJO_NO_ENCONTRADO" => "El trabajo no existe o no te pertenece.",
                    "ESTADO_INVALIDO" => "Este trabajo no está esperando confirmación.",
                    _ => "No se pudo registrar la confirmación."
                };
                return BadRequest(new { Motivo = motivo, Message = mensaje });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("forzar-finalizacion")]
        public async Task<IActionResult> ForzarFinalizacion([FromBody] ForzarFinalizacionDto dto)
        {
            try
            {
                var (success, motivo) = await _trabajoService.ForzarFinalizacionAsync(dto.TrabajoId, dto.CuidadorId);
                if (success)
                    return Ok(new { Message = "Trabajo marcado como completado sin pago" });

                var mensaje = motivo switch
                {
                    "TRABAJO_NO_ENCONTRADO" => "El trabajo no existe o no te pertenece.",
                    "ESTADO_INVALIDO" => "Este trabajo no fue rechazado previamente por el cliente.",
                    _ => "No se pudo forzar la finalización."
                };
                return BadRequest(new { Motivo = motivo, Message = mensaje });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("{trabajoId}/actividades")]
        public async Task<IActionResult> ObtenerActividades(int trabajoId)
        {
            try
            {
                var actividades = await _trabajoService.ObtenerActividadesAsync(trabajoId);
                return Ok(actividades);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost("alerta-geocerca")]
        public async Task<IActionResult> AlertarGeocerca([FromBody] AlertaGeocercaDto dto)
        {
            try
            {
                await _trabajoService.AlertarGeocercaAsync(dto.TrabajoId, dto.DistanciaMetros);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost("actividades")]
        public async Task<IActionResult> AgregarActividad([FromBody] AgregarActividadTrabajoDto dto)
        {
            try
            {
                var actividad = await _trabajoService.AgregarActividadAsync(dto);
                return Ok(actividad);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("motivos-cancelacion")]
        public async Task<IActionResult> ObtenerMotivosCancelacion()
        {
            try
            {
                var motivos = await _trabajoService.ObtenerMotivosCancelacionAsync();
                return Ok(motivos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("cancelar-cuidador")]
        public async Task<IActionResult> CancelarTrabajoCuidador([FromBody] CancelarTrabajoDto dto)
        {
            try
            {
                var success = await _trabajoService.CancelarTrabajoCuidadorAsync(dto);
                if (success)
                    return Ok(new { Message = "Trabajo cancelado correctamente" });
                return BadRequest("No se pudo cancelar. Verifica que el trabajo esté aceptado o en progreso.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("estado")]
        public async Task<IActionResult> ActualizarEstado([FromBody] ActualizarEstadoTrabajoDto dto)
        {
            try
            {
                var success = await _trabajoService.ActualizarEstadoTrabajoAsync(dto);
                if (success)
                    return Ok(new { Message = "Estado actualizado correctamente" });
                return BadRequest("No se pudo actualizar el estado. Verifica el ID.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
