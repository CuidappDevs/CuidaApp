using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.Interfaces.Cliente;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClienteController : ControllerBase
    {
        private readonly IClienteService _clienteService;

        public ClienteController(IClienteService clienteService)
        {
            _clienteService = clienteService;
        }

        [HttpGet("perfil/{clienteId}")]
        public async Task<IActionResult> ObtenerPerfil(int clienteId)
        {
            try
            {
                var perfil = await _clienteService.ObtenerPerfilAsync(clienteId);
                if (perfil == null)
                    return NotFound("Perfil no encontrado.");

                return Ok(perfil);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
