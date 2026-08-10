using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.Interfaces.Busqueda;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BusquedaController : ControllerBase
    {
        private readonly IBusquedaService _busquedaService;

        public BusquedaController(IBusquedaService busquedaService)
        {
            _busquedaService = busquedaService;
        }

        [HttpGet("servicios-cercanos")]
        public async Task<IActionResult> ObtenerServiciosCercanos([FromQuery] decimal lat, [FromQuery] decimal lng, [FromQuery] decimal radioKm = 15)
        {
            try
            {
                var servicios = await _busquedaService.ObtenerServiciosCercanosAsync(lat, lng, radioKm);
                return Ok(servicios);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("cuidadores-mapa")]
        public async Task<IActionResult> ObtenerCuidadoresCercanosMapa([FromQuery] decimal lat, [FromQuery] decimal lng, [FromQuery] decimal radioKm = 15)
        {
            try
            {
                var cuidadores = await _busquedaService.ObtenerCuidadoresCercanosMapaAsync(lat, lng, radioKm);
                return Ok(cuidadores);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("cuidadores")]
        public async Task<IActionResult> ObtenerCuidadoresPorServicio([FromQuery] string especialidad, [FromQuery] decimal lat, [FromQuery] decimal lng, [FromQuery] decimal radioKm = 15)
        {
            try
            {
                var cuidadores = await _busquedaService.ObtenerCuidadoresPorServicioAsync(especialidad, lat, lng, radioKm);
                return Ok(cuidadores);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
