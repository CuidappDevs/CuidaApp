using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.Interfaces.Usuario;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuarioController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;

        public UsuarioController(IUsuarioService usuarioService)
        {
            _usuarioService = usuarioService;
        }

        // Bienvenida animada de la app: una vez por cuenta.
        [HttpGet("{usuarioId}/bienvenida")]
        public async Task<IActionResult> ObtenerBienvenida(int usuarioId)
        {
            try
            {
                var vista = await _usuarioService.ObtenerBienvenidaVistaAsync(usuarioId);
                return vista == null ? NotFound(new { message = "Usuario no encontrado" }) : Ok(new { BienvenidaVista = vista.Value });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPut("{usuarioId}/bienvenida")]
        public async Task<IActionResult> MarcarBienvenida(int usuarioId)
        {
            try
            {
                return await _usuarioService.MarcarBienvenidaVistaAsync(usuarioId)
                    ? Ok(new { Message = "Bienvenida marcada como vista" })
                    : NotFound(new { message = "Usuario no encontrado" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
