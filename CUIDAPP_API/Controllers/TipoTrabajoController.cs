using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.Interfaces.TipoTrabajo;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TipoTrabajoController : ControllerBase
    {
        private readonly ITipoTrabajoService _tipoTrabajoService;

        public TipoTrabajoController(ITipoTrabajoService tipoTrabajoService)
        {
            _tipoTrabajoService = tipoTrabajoService;
        }

        // Tipos de trabajo para el registro de cuidadores (tabla TiposTrabajos).
        // Devuelve activos e inactivos: la app muestra los inactivos como "No disponible".
        [HttpGet]
        public async Task<IActionResult> ObtenerTodos()
        {
            try
            {
                return Ok(await _tipoTrabajoService.ObtenerTodosAsync());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
