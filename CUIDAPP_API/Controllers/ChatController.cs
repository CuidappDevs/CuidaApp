using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Chat;
using CUIDAPP_API.Interfaces.Chat;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpPost("conversacion/{trabajoId}")]
        public async Task<IActionResult> ObtenerOCrearConversacion(int trabajoId)
        {
            try
            {
                var conversacion = await _chatService.ObtenerOCrearConversacionAsync(trabajoId);
                return Ok(conversacion);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("conversacion/{conversacionId}/mensajes")]
        public async Task<IActionResult> ObtenerMensajes(int conversacionId)
        {
            try
            {
                var mensajes = await _chatService.ObtenerMensajesAsync(conversacionId);
                return Ok(mensajes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost("mensaje")]
        public async Task<IActionResult> EnviarMensaje([FromBody] EnviarMensajeDto dto)
        {
            try
            {
                var mensaje = await _chatService.EnviarMensajeAsync(dto);
                return Ok(mensaje);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("leido")]
        public async Task<IActionResult> MarcarLeidos([FromBody] MarcarLeidosDto dto)
        {
            try
            {
                await _chatService.MarcarLeidosAsync(dto);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("no-leidos/{usuarioId}")]
        public async Task<IActionResult> ContarNoLeidos(int usuarioId)
        {
            try
            {
                var resultado = await _chatService.ContarNoLeidosAsync(usuarioId);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
