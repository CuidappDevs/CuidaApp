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

        [HttpPut("iniciar")]
        public async Task<IActionResult> IniciarTrabajo([FromBody] IniciarTrabajoDto dto)
        {
            try
            {
                var success = await _trabajoService.IniciarTrabajoAsync(dto);
                if (success)
                    return Ok(new { Message = "Trabajo iniciado correctamente" });
                return BadRequest("PIN incorrecto o el trabajo no está en estado Aceptado.");
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
