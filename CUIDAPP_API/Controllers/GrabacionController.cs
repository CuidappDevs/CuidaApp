using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Services;

namespace CUIDAPP_API.Controllers
{
    /// <summary>
    /// Caja negra: la app del Care Partner consulta si la grabación está encendida, marca el servicio
    /// como grabado (al iniciar con el PIN) y registra cada segmento de audio ya subido a /uploads.
    /// La usa la app, por eso NO lleva token. El audio se sube por el endpoint /upload normal.
    /// </summary>
    [Route("api/grabacion")]
    [ApiController]
    public class GrabacionController : ControllerBase
    {
        private readonly IAdminOperacionesService _db;
        private readonly ILogger<GrabacionController> _logger;

        public GrabacionController(IAdminOperacionesService db, ILogger<GrabacionController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>¿La administración tiene encendida la grabación de seguridad?</summary>
        [HttpGet("activa")]
        public async Task<IActionResult> Activa()
        {
            try
            {
                var fila = (await _db.ConsultarAsync("sp_GrabacionSeguridadActiva"))[0].FirstOrDefault();
                return Ok(new { Activa = fila?["Activa"] is true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando grabación activa");
                return Ok(new { Activa = false });
            }
        }

        /// <summary>El servicio quedó con grabación consentida (se llama al iniciar con el PIN).</summary>
        [HttpPut("{trabajoId:int}/marcar")]
        public async Task<IActionResult> Marcar(int trabajoId, [FromQuery] int cuidadorId)
        {
            var fila = await _db.EjecutarAsync("sp_MarcarGrabacionServicio", ("@TrabajoId", trabajoId), ("@CuidadorId", cuidadorId));
            return fila?["Resultado"]?.ToString() == "OK" ? Ok(new { Message = "Listo." }) : NotFound(new { Message = "Servicio no válido." });
        }

        /// <summary>Registra un segmento de audio ya subido (url relativa de /uploads).</summary>
        [HttpPost("segmento")]
        public async Task<IActionResult> Segmento([FromBody] SegmentoGrabacionDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Url) || !dto.Url.StartsWith("/uploads/"))
                return BadRequest(new { Message = "URL no válida." });

            var fila = await _db.EjecutarAsync("sp_RegistrarSegmentoGrabacion",
                ("@TrabajoId", dto.TrabajoId), ("@CuidadorId", dto.CuidadorId), ("@UrlArchivo", dto.Url.Trim()),
                ("@DuracionSeg", dto.DuracionSeg), ("@Fecha", HoraLocalRD.Ahora));
            return fila?["Resultado"]?.ToString() == "OK" ? Ok(new { Message = "Listo." }) : NotFound(new { Message = "Servicio no válido." });
        }

        public class SegmentoGrabacionDto
        {
            public int TrabajoId { get; set; }
            public int CuidadorId { get; set; }
            public string Url { get; set; } = "";
            public int DuracionSeg { get; set; }
        }
    }
}
