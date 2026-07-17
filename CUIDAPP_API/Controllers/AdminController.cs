using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Admin;
using CUIDAPP_API.Interfaces.Admin;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        [HttpGet("cuidadores-pendientes")]
        public async Task<IActionResult> ObtenerCuidadoresPendientes()
        {
            try
            {
                var cuidadores = await _adminService.ObtenerCuidadoresPendientesAsync();
                return Ok(cuidadores);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("documentos/{cuidadorId}")]
        public async Task<IActionResult> ObtenerDocumentosPorCuidador(int cuidadorId)
        {
            try
            {
                var documentos = await _adminService.ObtenerDocumentosPorCuidadorAsync(cuidadorId);
                return Ok(documentos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("aprobar-cuidador")]
        public async Task<IActionResult> ActualizarEstadoCuidador([FromBody] ActualizarEstadoCuidadorDto dto)
        {
            try
            {
                var success = await _adminService.ActualizarEstadoCuidadorAsync(dto);
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
