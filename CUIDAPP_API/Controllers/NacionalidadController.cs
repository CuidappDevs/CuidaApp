using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.Interfaces.Nacionalidad;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NacionalidadController : ControllerBase
    {
        private readonly INacionalidadService _nacionalidadService;

        public NacionalidadController(INacionalidadService nacionalidadService)
        {
            _nacionalidadService = nacionalidadService;
        }

        // Nacionalidades activas para el registro (tabla Nacionalidades).
        [HttpGet]
        public async Task<IActionResult> ObtenerActivas()
        {
            try
            {
                return Ok(await _nacionalidadService.ObtenerActivasAsync());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
