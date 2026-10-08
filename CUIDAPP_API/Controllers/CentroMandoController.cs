using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.Services;
using CUIDAPP_API.Services.Admin;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Controllers
{
    /// <summary>Centro de mando del panel: ficha de una persona en el mapa, estelas, recorridos, demanda, alertas y acciones.</summary>
    [Route("api/admin/mapa")]
    [ApiController]
    [Authorize(Policy = PoliticasAdmin.Admin)]
    [AuditarAdmin]
    public class CentroMandoController : ControllerBase
    {
        private readonly IAdminOperacionesService _db;
        private readonly CentroMandoService _mando;
        private readonly ITrabajoNotifier _notifier;

        public CentroMandoController(IAdminOperacionesService db, CentroMandoService mando, ITrabajoNotifier notifier)
        {
            _db = db;
            _mando = mando;
            _notifier = notifier;
        }

        private int AdminId => PoliticasAdmin.AdminId(User);

        public class NotificarDto
        {
            [Required, MinLength(1), MaxLength(200)]
            public List<int> UsuarioIds { get; set; } = new();
            [Required, StringLength(120, MinimumLength = 2)]
            public string Titulo { get; set; } = "";
            [Required, StringLength(500, MinimumLength = 2)]
            public string Mensaje { get; set; } = "";
        }

        public class CheckinDto
        {
            public int UsuarioId { get; set; }
            /// <summary>Minutos para responder antes de crear un SOS.</summary>
            [Range(2, 60)]
            public int Minutos { get; set; } = 5;
            [StringLength(500)]
            public string? Mensaje { get; set; }
        }

        [HttpGet("ficha/{usuarioId:int}")]
        public async Task<IActionResult> Ficha(int usuarioId)
        {
            var r = await _db.ConsultarAsync("sp_AdminFichaMapa", ("@UsuarioId", usuarioId), ("@Hoy", HoraLocalRD.Ahora.Date));
            if (r[0].Count == 0)
                return NotFound(new ApiErrorDto("NO_ENCONTRADO", "La persona no existe."));
            return Ok(new { Persona = r[0][0], Servicio = r[1].FirstOrDefault(), Avisos = r[2] });
        }

        [HttpGet("estelas")]
        public async Task<IActionResult> Estelas([FromQuery] int minutos = 20)
            => Ok((await _db.ConsultarAsync("sp_AdminEstelas", ("@Desde", HoraLocalRD.Ahora.AddMinutes(-Math.Clamp(minutos, 5, 240)))))[0]);

        [HttpGet("recorrido")]
        public async Task<IActionResult> Recorrido([FromQuery] int? trabajoId, [FromQuery] int? cuidadorId, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        {
            if (trabajoId == null && cuidadorId == null)
                return BadRequest(new ApiErrorDto("FALTAN_DATOS", "Indica un servicio o un Care Partner."));
            var h = hasta ?? HoraLocalRD.Ahora;
            var d = desde ?? h.AddHours(-8);
            return Ok((await _db.ConsultarAsync("sp_AdminRecorrido", ("@TrabajoId", trabajoId), ("@CuidadorId", cuidadorId), ("@Desde", d), ("@Hasta", h)))[0]);
        }

        [HttpGet("demanda")]
        public async Task<IActionResult> Demanda([FromQuery] int dias = 60)
            => Ok((await _db.ConsultarAsync("sp_AdminDemanda", ("@Desde", HoraLocalRD.Ahora.Date.AddDays(-Math.Clamp(dias, 7, 365)))))[0]);

        [HttpGet("alertas")]
        public async Task<IActionResult> Alertas()
            => Ok((await _db.ConsultarAsync("sp_AdminAlertasMapa", ("@Ahora", HoraLocalRD.Ahora)))[0]);

        /// <summary>Notificación directa al teléfono de una o varias personas (también desde un área dibujada en el mapa).</summary>
        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPost("notificar")]
        public async Task<IActionResult> Notificar([FromBody] NotificarDto dto)
        {
            var enviados = 0;
            foreach (var id in dto.UsuarioIds.Distinct())
                if (await _mando.EnviarAsync(AdminId, id, "mensaje", dto.Titulo.Trim(), dto.Mensaje.Trim()) > 0)
                    enviados++;
            return Ok(new { Message = enviados == 1 ? "Notificación enviada." : $"Notificación enviada a {enviados} personas.", Enviados = enviados });
        }

        /// <summary>"¿Estás bien?": la persona responde en su teléfono; si pide ayuda o no responde a tiempo se crea un SOS.</summary>
        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPost("checkin")]
        public async Task<IActionResult> Checkin([FromBody] CheckinDto dto)
        {
            var mensaje = string.IsNullOrWhiteSpace(dto.Mensaje) ? "El equipo de CuidApp quiere saber si estás bien. Responde por favor." : dto.Mensaje.Trim();
            var id = await _mando.EnviarAsync(AdminId, dto.UsuarioId, "checkin", "¿Estás bien?", mensaje, dto.Minutos);
            return id > 0
                ? Ok(new { Message = $"Pregunta enviada. Si no responde en {dto.Minutos} min se creará una alerta SOS.", Id = id })
                : NotFound(new ApiErrorDto("NO_ENCONTRADO", "La persona no existe."));
        }

        /// <summary>Oculta a un Care Partner de los clientes de inmediato (y apaga su horario automático).</summary>
        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPost("ocultar/{usuarioId:int}")]
        public async Task<IActionResult> OcultarCuidador(int usuarioId)
        {
            var fila = await _db.EjecutarAsync("sp_AdminOcultarCuidador", ("@CuidadorId", usuarioId));
            if (fila?["Resultado"]?.ToString() != "OK")
                return NotFound(new ApiErrorDto("NO_ENCONTRADO", "El Care Partner no existe."));
            await _notifier.NotificarGlobalAsync("DisponibilidadCambio", new { CuidadorId = usuarioId, Disponible = false });
            await _mando.EnviarAsync(AdminId, usuarioId, "mensaje", "Te ocultamos de los clientes",
                "El equipo de CuidApp te ocultó temporalmente. Escríbenos desde Ayuda si tienes dudas.");
            return Ok(new { Message = "Care Partner oculto. Se le avisó en su teléfono." });
        }
    }
}
