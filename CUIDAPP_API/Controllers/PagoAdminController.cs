using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Pago;
using CUIDAPP_API.Interfaces.Pago;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PagoAdminController : ControllerBase
    {
        private readonly IPagoAdminService _pagoAdminService;

        public PagoAdminController(IPagoAdminService pagoAdminService)
        {
            _pagoAdminService = pagoAdminService;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPagos([FromQuery] int? estado)
        {
            try
            {
                var pagos = await _pagoAdminService.ObtenerPagosAsync(estado);
                return Ok(pagos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost("{pagoId}/autorizar")]
        public async Task<IActionResult> AutorizarPago(int pagoId, [FromBody] AccionPagoDto dto)
        {
            try
            {
                var success = await _pagoAdminService.AutorizarPagoAsync(pagoId, dto.AdminId);
                if (!success)
                    return BadRequest("El pago no está en estado Pendiente o no existe.");

                return Ok(new { Message = "Pago autorizado" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost("{pagoId}/aprobar")]
        public async Task<IActionResult> AprobarPago(int pagoId, [FromBody] AccionPagoDto dto)
        {
            try
            {
                var success = await _pagoAdminService.AprobarPagoAsync(pagoId, dto.AdminId);
                if (!success)
                    return BadRequest("El pago no está en estado Autorizado o no existe.");

                return Ok(new { Message = "Pago aprobado" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
