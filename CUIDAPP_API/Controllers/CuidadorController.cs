using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Cuidador;
using CUIDAPP_API.Interfaces.Cuidador;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CuidadorController : ControllerBase
    {
        private readonly ICuidadorService _cuidadorService;

        public CuidadorController(ICuidadorService cuidadorService)
        {
            _cuidadorService = cuidadorService;
        }

        [HttpGet("estado-verificacion/{cuidadorId}")]
        public async Task<IActionResult> ObtenerEstadoVerificacion(int cuidadorId)
        {
            try
            {
                var estado = await _cuidadorService.ObtenerEstadoVerificacionAsync(cuidadorId);
                return Ok(estado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost("documentos")]
        public async Task<IActionResult> SubirDocumento([FromBody] SubirDocumentoDto dto)
        {
            try
            {
                var documentoId = await _cuidadorService.SubirDocumentoAsync(dto);
                return Ok(new { Message = "Documento subido con éxito", DocumentoId = documentoId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("perfil/{cuidadorId}")]
        public async Task<IActionResult> ObtenerPerfil(int cuidadorId)
        {
            try
            {
                var perfil = await _cuidadorService.ObtenerPerfilAsync(cuidadorId);
                if (perfil == null)
                    return NotFound("Perfil no encontrado.");

                return Ok(perfil);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("disponibilidad")]
        public async Task<IActionResult> ActualizarDisponibilidad([FromBody] ActualizarDisponibilidadDto dto)
        {
            try
            {
                var success = await _cuidadorService.ActualizarDisponibilidadAsync(dto);
                if (success)
                    return Ok(new { Message = "Disponibilidad actualizada" });
                return BadRequest("No se pudo actualizar la disponibilidad. Verifica el ID.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("ubicacion")]
        public async Task<IActionResult> ActualizarUbicacion([FromBody] ActualizarUbicacionDto dto)
        {
            try
            {
                var success = await _cuidadorService.ActualizarUbicacionAsync(dto);
                if (success)
                    return Ok(new { Message = "Ubicación actualizada" });
                return BadRequest("No se pudo actualizar la ubicación. Verifica el ID.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("ganancias/{cuidadorId}")]
        public async Task<IActionResult> ObtenerGanancias(int cuidadorId)
        {
            try
            {
                var ganancias = await _cuidadorService.ObtenerGananciasAsync(cuidadorId);
                return Ok(ganancias);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("pagos/{cuidadorId}")]
        public async Task<IActionResult> ObtenerPagos(int cuidadorId)
        {
            try
            {
                var pagos = await _cuidadorService.ObtenerPagosAsync(cuidadorId);
                return Ok(pagos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
