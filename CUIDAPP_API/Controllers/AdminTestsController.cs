using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.DTOs.TestPsicologico;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.Services;

namespace CUIDAPP_API.Controllers
{
    /// <summary>
    /// Tests psicológicos del registro: el panel los crea, los vincula a tipos de servicio y revisa los intentos.
    /// </summary>
    [Route("api/admin/tests")]
    [ApiController]
    [Authorize(Policy = PoliticasAdmin.Admin)]
    [AuditarAdmin]
    public class AdminTestsController : ControllerBase
    {
        private readonly IAdminOperacionesService _svc;
        private readonly ILogger<AdminTestsController> _logger;

        public AdminTestsController(IAdminOperacionesService svc, ILogger<AdminTestsController> logger)
        {
            _svc = svc;
            _logger = logger;
        }

        private async Task<IActionResult> Leer(string operacion, Func<Task<object>> accion)
        {
            try { return Ok(await accion()); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en {Operacion}", operacion);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        [HttpGet]
        public Task<IActionResult> Tests() => Leer("Tests", async () =>
        {
            var r = await _svc.ConsultarAsync("sp_AdminTests");
            return new { Tests = r[0], Tipos = r[1] };
        });

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detalle(int id)
        {
            try
            {
                var r = await _svc.ConsultarAsync("sp_AdminTestDetalle", ("@Id", id));
                var test = r[0].FirstOrDefault();
                if (test == null)
                    return NotFound(new ApiErrorDto("NO_ENCONTRADO", "No se encontró el test."));

                var opciones = r[3];
                return Ok(new
                {
                    Test = test,
                    Tipos = r[1].Select(f => Convert.ToInt32(f["TipoTrabajoId"])).ToList(),
                    Preguntas = r[2].Select(p => new
                    {
                        Texto = p["Texto"],
                        EsCritica = p["EsCritica"],
                        Opciones = opciones.Where(o => Equals(o["PreguntaId"], p["Id"]))
                            .Select(o => new { Texto = o["Texto"], EsCorrecta = o["EsCorrecta"], EsRedFlag = o["EsRedFlag"] })
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en TestDetalle");
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        // Reglas que el SP no comprueba: cada pregunta con 2+ opciones y una sola correcta que no sea red flag.
        private static string? Validar(TestAdminDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre)) return "El test necesita un nombre.";
            if (dto.MinutosLimite is < 1 or > 180) return "El tiempo debe estar entre 1 y 180 minutos.";
            if (dto.PorcentajeAprobacion is <= 0 or > 100) return "El porcentaje de aprobación debe estar entre 1 y 100.";
            if (dto.PorcentajeRevision < 0 || dto.PorcentajeRevision > dto.PorcentajeAprobacion)
                return "El porcentaje de revisión manual no puede ser mayor que el de aprobación.";
            if (dto.DiasBloqueo is < 1 or > 3650) return "Los días de bloqueo deben estar entre 1 y 3650.";
            if (dto.Activo && dto.Preguntas.Count == 0) return "Un test activo necesita al menos una pregunta.";

            for (int i = 0; i < dto.Preguntas.Count; i++)
            {
                var p = dto.Preguntas[i];
                var n = i + 1;
                if (string.IsNullOrWhiteSpace(p.Texto)) return $"La pregunta {n} no tiene texto.";
                var opciones = p.Opciones.Where(o => !string.IsNullOrWhiteSpace(o.Texto)).ToList();
                if (opciones.Count < 2) return $"La pregunta {n} necesita al menos 2 opciones.";
                if (opciones.Count(o => o.EsCorrecta) != 1) return $"La pregunta {n} debe tener exactamente una opción correcta.";
                if (opciones.Any(o => o.EsCorrecta && o.EsRedFlag)) return $"En la pregunta {n}, la opción correcta no puede ser red flag.";
            }
            return null;
        }

        [Authorize(Policy = PoliticasAdmin.Verificacion)]
        [HttpPost]
        public async Task<IActionResult> Guardar([FromBody] TestAdminDto dto)
        {
            var error = Validar(dto);
            if (error != null)
                return BadRequest(new ApiErrorDto("VALIDACION", error));

            var preguntas = dto.Preguntas.Select(p => new
            {
                texto = p.Texto.Trim(),
                esCritica = p.EsCritica,
                opciones = p.Opciones.Where(o => !string.IsNullOrWhiteSpace(o.Texto))
                    .Select(o => new { texto = o.Texto.Trim(), esCorrecta = o.EsCorrecta, esRedFlag = o.EsRedFlag })
            });

            try
            {
                var fila = await _svc.EjecutarAsync("sp_AdminGuardarTest", ("@Id", dto.Id), ("@Nombre", dto.Nombre.Trim()),
                    ("@Descripcion", string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim()),
                    ("@MinutosLimite", dto.MinutosLimite), ("@PorcentajeAprobacion", dto.PorcentajeAprobacion),
                    ("@PorcentajeRevision", dto.PorcentajeRevision), ("@DiasBloqueo", dto.DiasBloqueo), ("@Activo", dto.Activo),
                    ("@Tipos", JsonSerializer.Serialize(dto.Tipos.Distinct())), ("@Preguntas", JsonSerializer.Serialize(preguntas)),
                    ("@Ahora", HoraLocalRD.Ahora));

                return fila?["Resultado"]?.ToString() switch
                {
                    "OK" => Ok(new { Message = "Test guardado.", Id = fila["Id"] }),
                    "NO_ENCONTRADO" => NotFound(new ApiErrorDto("NO_ENCONTRADO", "No se encontró el test.")),
                    "TIPO_OCUPADO" => Conflict(new ApiErrorDto("TIPO_OCUPADO", $"Cada servicio solo puede tener un test: {fila["Detalle"]}.")),
                    _ => StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "No se pudo guardar el test."))
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GuardarTest");
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        [HttpGet("intentos")]
        public Task<IActionResult> Intentos([FromQuery] int? resultado, [FromQuery] string? buscar) => Leer("TestIntentos", async () =>
            (await _svc.ConsultarAsync("sp_AdminTestIntentos", ("@Resultado", resultado),
                ("@Buscar", string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim())))[0]);

        [HttpGet("intentos/{id:int}")]
        public async Task<IActionResult> Intento(int id)
        {
            try
            {
                var r = await _svc.ConsultarAsync("sp_AdminTestIntentoDetalle", ("@Id", id));
                var intento = r[0].FirstOrDefault();
                if (intento == null)
                    return NotFound(new ApiErrorDto("NO_ENCONTRADO", "No se encontró el intento."));
                return Ok(new { Intento = intento, Respuestas = r[1] });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en TestIntento");
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        [Authorize(Policy = PoliticasAdmin.Verificacion)]
        [HttpPost("intentos/{id:int}/anular")]
        public async Task<IActionResult> Anular(int id)
        {
            var fila = await _svc.EjecutarAsync("sp_AdminAnularTestIntento", ("@Id", id), ("@AdminId", PoliticasAdmin.AdminId(User)), ("@Ahora", HoraLocalRD.Ahora));
            return fila?["Resultado"]?.ToString() == "OK"
                ? Ok(new { Message = "Intento anulado: ya no bloquea a esta persona." })
                : NotFound(new ApiErrorDto("NO_ENCONTRADO", "El intento no existe o ya estaba anulado."));
        }

        // Último resultado de cada Care Partner registrada (para la cola de Verificación).
        [HttpGet("resultados-usuarios")]
        public Task<IActionResult> ResultadosUsuarios() => Leer("TestResultadosUsuarios", async () =>
            (await _svc.ConsultarAsync("sp_AdminTestResultadosUsuarios"))[0]);
    }
}
