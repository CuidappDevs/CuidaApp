using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Email;
using CUIDAPP_API.Interfaces.Email;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;

        public EmailController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpPost("enviar")]
        public async Task<IActionResult> Enviar([FromBody] EnviarEmailDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Destinatario) || string.IsNullOrWhiteSpace(dto.Asunto) || string.IsNullOrWhiteSpace(dto.CuerpoHtml))
                return BadRequest("Destinatario, asunto y cuerpo son obligatorios.");

            try
            {
                await _emailService.EnviarAsync(dto.Destinatario, dto.Asunto, dto.CuerpoHtml);
                return Ok(new { Message = "Correo enviado" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
