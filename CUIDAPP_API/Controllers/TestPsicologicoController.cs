using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.DTOs.TestPsicologico;
using CUIDAPP_API.Interfaces.TestPsicologico;

namespace CUIDAPP_API.Controllers
{
    /// <summary>
    /// Test psicológico del registro de Care Partners. Sin [Authorize]: se usa antes de que exista la cuenta.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TestPsicologicoController : ControllerBase
    {
        private readonly ITestPsicologicoService _service;
        private readonly ILogger<TestPsicologicoController> _logger;

        public TestPsicologicoController(ITestPsicologicoService service, ILogger<TestPsicologicoController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private IActionResult ErrorInterno(Exception ex, string operacion)
        {
            _logger.LogError(ex, "Error interno en {Operacion}", operacion);
            return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
        }

        // ¿El tipo de trabajo elegido pide test? ¿La persona puede hacerlo?
        [HttpGet("estado")]
        public async Task<IActionResult> Estado([FromQuery] int tipoTrabajoId, [FromQuery] string? documento, [FromQuery] string? telefono)
        {
            try
            {
                return Ok(await _service.EstadoAsync(tipoTrabajoId, documento, telefono));
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "TestEstado");
            }
        }

        [HttpPost("iniciar")]
        public async Task<IActionResult> Iniciar([FromBody] IniciarTestDto dto)
        {
            try
            {
                var (test, estado) = await _service.IniciarAsync(dto);
                if (test == null)
                    return Conflict(estado); // el candidato no está libre (bloqueado, descartado, ya aprobado o sin test)
                return Ok(test);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "TestIniciar");
            }
        }

        [HttpPost("finalizar")]
        public async Task<IActionResult> Finalizar([FromBody] FinalizarTestDto dto)
        {
            try
            {
                var resultado = await _service.FinalizarAsync(dto);
                if (resultado == null)
                    return NotFound(new ApiErrorDto("NO_ENCONTRADO", "El test no existe o ya se terminó."));
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "TestFinalizar");
            }
        }
    }
}
