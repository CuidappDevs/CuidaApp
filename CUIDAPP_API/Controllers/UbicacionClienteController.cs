using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.UbicacionCliente;
using CUIDAPP_API.Interfaces.UbicacionCliente;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UbicacionClienteController : ControllerBase
    {
        private readonly IUbicacionClienteService _ubicacionClienteService;

        public UbicacionClienteController(IUbicacionClienteService ubicacionClienteService)
        {
            _ubicacionClienteService = ubicacionClienteService;
        }

        [HttpGet("cliente/{clienteId}")]
        public async Task<IActionResult> ObtenerUbicaciones(int clienteId)
        {
            try
            {
                var ubicaciones = await _ubicacionClienteService.ObtenerUbicacionesAsync(clienteId);
                return Ok(ubicaciones);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> CrearUbicacion([FromBody] CrearUbicacionClienteDto dto)
        {
            try
            {
                var id = await _ubicacionClienteService.CrearUbicacionAsync(dto);
                return Ok(new { Id = id });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut]
        public async Task<IActionResult> ActualizarUbicacion([FromBody] ActualizarUbicacionClienteDto dto)
        {
            try
            {
                var success = await _ubicacionClienteService.ActualizarUbicacionAsync(dto);
                if (success)
                    return Ok(new { Message = "Ubicación actualizada" });
                return BadRequest("No se pudo actualizar la ubicación.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpDelete("{id}/cliente/{clienteId}")]
        public async Task<IActionResult> EliminarUbicacion(int id, int clienteId)
        {
            try
            {
                var success = await _ubicacionClienteService.EliminarUbicacionAsync(new EliminarUbicacionClienteDto { Id = id, ClienteId = clienteId });
                if (success)
                    return Ok(new { Message = "Ubicación eliminada" });
                return BadRequest("No se pudo eliminar la ubicación.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
