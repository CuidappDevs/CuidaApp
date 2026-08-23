using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Calificacion;
using CUIDAPP_API.Interfaces.Calificacion;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CalificacionController : ControllerBase
    {
        private readonly ICalificacionService _calificacionService;

        public CalificacionController(ICalificacionService calificacionService)
        {
            _calificacionService = calificacionService;
        }

        [HttpPost]
        public async Task<IActionResult> CrearCalificacion([FromBody] CrearCalificacionDto dto)
        {
            try
            {
                var success = await _calificacionService.CrearCalificacionAsync(dto);
                if (success)
                    return Ok(new { Message = "Calificación registrada" });
                return BadRequest("Ya calificaste este trabajo.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("usuario/{usuarioId}/promedio")]
        public async Task<IActionResult> ObtenerPromedio(int usuarioId)
        {
            try
            {
                var promedio = await _calificacionService.ObtenerPromedioAsync(usuarioId);
                return Ok(promedio);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("trabajo/{trabajoId}/existe")]
        public async Task<IActionResult> ExisteCalificacion(int trabajoId, [FromQuery] int calificadorId)
        {
            try
            {
                var existe = await _calificacionService.ExisteCalificacionAsync(trabajoId, calificadorId);
                return Ok(new { Existe = existe });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("trabajo/{trabajoId}")]
        public async Task<IActionResult> ObtenerCalificacionDeTrabajo(int trabajoId, [FromQuery] int calificadorId)
        {
            try
            {
                var calificacion = await _calificacionService.ObtenerCalificacionDeTrabajoAsync(trabajoId, calificadorId);
                if (calificacion == null)
                    return NotFound();
                return Ok(calificacion);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
