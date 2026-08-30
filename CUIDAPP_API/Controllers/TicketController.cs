using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Ticket;
using CUIDAPP_API.Interfaces.Ticket;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketController : ControllerBase
    {
        private readonly ITicketService _ticketService;

        public TicketController(ITicketService ticketService)
        {
            _ticketService = ticketService;
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] CrearTicketDto dto)
        {
            try
            {
                var ticketId = await _ticketService.CrearTicketAsync(dto);
                return Ok(new { TicketId = ticketId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("usuario/{usuarioId}")]
        public async Task<IActionResult> ObtenerPorUsuario(int usuarioId)
        {
            try
            {
                var tickets = await _ticketService.ObtenerTicketsPorUsuarioAsync(usuarioId);
                return Ok(tickets);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTodos([FromQuery] int? estado)
        {
            try
            {
                var tickets = await _ticketService.ObtenerTicketsAdminAsync(estado);
                return Ok(tickets);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpGet("{ticketId}")]
        public async Task<IActionResult> ObtenerDetalle(int ticketId)
        {
            try
            {
                var ticket = await _ticketService.ObtenerDetalleAsync(ticketId);
                if (ticket == null)
                    return NotFound();

                var mensajes = await _ticketService.ObtenerMensajesAsync(ticketId);
                return Ok(new { Ticket = ticket, Mensajes = mensajes });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPost("{ticketId}/mensaje")]
        public async Task<IActionResult> AgregarMensaje(int ticketId, [FromBody] CrearMensajeTicketDto dto)
        {
            try
            {
                var success = await _ticketService.AgregarMensajeAsync(ticketId, dto);
                if (!success)
                    return BadRequest("El ticket no existe.");

                return Ok(new { Message = "Mensaje agregado" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("{ticketId}/estado")]
        public async Task<IActionResult> ActualizarEstado(int ticketId, [FromBody] ActualizarEstadoTicketDto dto)
        {
            try
            {
                var success = await _ticketService.ActualizarEstadoAsync(ticketId, dto);
                if (!success)
                    return BadRequest("El ticket no existe.");

                return Ok(new { Message = "Estado actualizado" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
