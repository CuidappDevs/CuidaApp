using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Cuidador;
using CUIDAPP_API.Interfaces.Cuidador;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CuidadorController : ControllerBase
    {
        private readonly ICuidadorService _cuidadorService;

        public CuidadorController(ICuidadorService cuidadorService)
        {
            _cuidadorService = cuidadorService;
        }

        [HttpPost("documentos")]
        public async Task<IActionResult> SubirDocumento([FromBody] SubirDocumentoDto dto)
        {
            try
            {
                var documentoId = await _cuidadorService.SubirDocumentoAsync(dto);
                return Ok(new { Message = "Documento subido con éxito", DocumentoId = documentoId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
