using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.DTOs.SOS;
using CUIDAPP_API.Interfaces.SOS;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SOSController : ControllerBase
    {
        private readonly ISOSAlertService _sosService;

        public SOSController(ISOSAlertService sosService)
        {
            _sosService = sosService;
        }

        [HttpPost]
        public async Task<IActionResult> CrearAlerta([FromBody] CrearSOSAlertaDto dto)
        {
            try
            {
                var alerta = await _sosService.CrearAlertaAsync(dto);
                if (alerta == null)
                    return BadRequest(new { Message = "No se pudo crear la alerta SOS" });

                return Ok(new { Message = "Alerta SOS enviada", Alerta = alerta });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        /// <summary>La app del cuidador la llama sola cuando detecta impacto + inmovilidad y no hay confirmación en 30 s.</summary>
        [HttpPost("dead-man-triggered")]
        public async Task<IActionResult> DeadManTriggered([FromBody] DeadManTriggeredDto dto)
        {
            try
            {
                var (alerta, error) = await _sosService.CrearAlertaDeadManAsync(dto);
                if (alerta == null)
                    return BadRequest(new { Message = error ?? "No se pudo crear la alerta automática" });

                return Ok(new { Message = "Alerta automática enviada", Alerta = alerta });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [Authorize(Policy = PoliticasAdmin.Admin)]
        [AuditarAdmin]
        [HttpGet("pendientes")]
        public async Task<IActionResult> ObtenerAlertasPendientes()
        {
            try
            {
                var alertas = await _sosService.ObtenerAlertasPendientesAsync();
                return Ok(alertas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [Authorize(Policy = PoliticasAdmin.Admin)]
        [HttpGet("historial")]
        public async Task<IActionResult> ObtenerHistorial([FromQuery] int top = 100)
        {
            try
            {
                return Ok(await _sosService.ObtenerHistorialAsync(Math.Clamp(top, 1, 500)));
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [Authorize(Policy = PoliticasAdmin.Admin)]
        [AuditarAdmin]
        [HttpGet("{id}")]
        public async Task<IActionResult> ObtenerAlertaPorId(int id)
        {
            try
            {
                var alerta = await _sosService.ObtenerAlertaPorIdAsync(id);
                if (alerta == null)
                    return NotFound(new { Message = "Alerta no encontrada" });

                return Ok(alerta);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [Authorize(Policy = PoliticasAdmin.Admin)]
        [AuditarAdmin]
        [HttpPut("{id}/atender")]
        public async Task<IActionResult> AtenderAlerta(int id, [FromBody] AtenderSOSDto dto)
        {
            try
            {
                var success = await _sosService.AtenderAlertaAsync(id, dto);
                if (!success)
                    return BadRequest(new { Message = "Alerta no encontrada o ya fue atendida" });

                return Ok(new { Message = "Alerta atendida" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [Authorize(Policy = PoliticasAdmin.Admin)]
        [AuditarAdmin]
        [HttpPut("{id}/descartar")]
        public async Task<IActionResult> DescartarAlerta(int id)
        {
            try
            {
                var success = await _sosService.DescartarAlertaAsync(id);
                if (!success)
                    return BadRequest(new { Message = "Alerta no encontrada o ya fue procesada" });

                return Ok(new { Message = "Alerta descartada" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
