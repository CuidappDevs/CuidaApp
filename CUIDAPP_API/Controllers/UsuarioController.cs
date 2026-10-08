using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.Interfaces.Usuario;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuarioController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;
        private readonly Services.Admin.CentroMandoService _centroMando;

        public UsuarioController(IUsuarioService usuarioService, Services.Admin.CentroMandoService centroMando)
        {
            _usuarioService = usuarioService;
            _centroMando = centroMando;
        }

        public class RespuestaCheckinDto
        {
            public bool EstaBien { get; set; }
        }

        /// <summary>Respuesta al "¿Estás bien?" del centro de mando. Si pide ayuda se crea una alerta SOS.</summary>
        [HttpPost("{usuarioId}/checkin/{avisoId}")]
        public async Task<IActionResult> ResponderCheckin(int usuarioId, int avisoId, [FromBody] RespuestaCheckinDto dto)
        {
            try
            {
                return await _centroMando.ResponderAsync(avisoId, usuarioId, dto.EstaBien)
                    ? Ok(new { message = "Respuesta enviada" })
                    : Conflict(new { message = "Esta pregunta ya fue respondida o venció." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
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
