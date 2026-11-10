using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.DTOs.Contrato;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Services;
using CUIDAPP_API.Services.Contrato;

namespace CUIDAPP_API.Controllers
{
    /// <summary>
    /// Contratos fijos desde la app (cliente y Care Partner): permiso para contratar, domicilios,
    /// propuesta / negociación, firma, ausencias, pausa y terminación, y descarga del PDF.
    /// La usa la app, por eso NO lleva token (igual que el resto de endpoints de la app). Los errores
    /// devuelven { code, message }: la app traduce el code con la clave contrato_err_{code}.
    /// </summary>
    [Route("api/contratos")]
    [ApiController]
    public class ContratoController : ControllerBase
    {
        private readonly IAdminOperacionesService _db;
        private readonly ContratoService _contratos;
        private readonly ILogger<ContratoController> _logger;

        public ContratoController(IAdminOperacionesService db, ContratoService contratos, ILogger<ContratoController> logger)
        {
            _db = db;
            _contratos = contratos;
            _logger = logger;
        }

        private static DateTime Ahora => HoraLocalRD.Ahora;

        private IActionResult Error(string codigo) => codigo switch
        {
            "NO_ENCONTRADO" => NotFound(new ApiErrorDto(codigo, "No se encontró el contrato o el registro.")),
            "ESTADO_INVALIDO" or "NO_ES_SU_TURNO" or "YA_FIRMADO" or "YA_SOLICITADO" or "YA_APROBADO" or "DUPLICADO"
                or "CHOQUE_HORARIO" or "YA_MARCADA" or "EN_CURSO" or "ESPERA" => Conflict(new ApiErrorDto(codigo, codigo)),
            "PERMISO_REQUERIDO" or "DOMICILIO_NO_VALIDADO" or "CUIDADOR_NO_ACEPTA" => StatusCode(403, new ApiErrorDto(codigo, codigo)),
            _ => BadRequest(new ApiErrorDto(codigo, codigo))
        };

        private async Task<IActionResult> Leer(string operacion, Func<Task<object>> accion)
        {
            try { return Ok(await accion()); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en {Operacion}", operacion);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        // ------------------------------------------------------------------ permiso y domicilios (cliente)

        [HttpGet("permiso/{clienteId:int}")]
        public Task<IActionResult> Permiso(int clienteId) => Leer("permiso contrato", async () =>
        {
            var r = await _db.ConsultarAsync("sp_ContratoPermisoObtener", ("@ClienteId", clienteId));
            return new { Permiso = r[0].FirstOrDefault(), Domicilios = r[1] };
        });

        [HttpPost("permiso")]
        public async Task<IActionResult> SolicitarPermiso([FromBody] SolicitarPermisoContratoDto dto)
        {
            var fila = await _db.EjecutarAsync("sp_ContratoPermisoSolicitar", ("@ClienteId", dto.ClienteId), ("@Motivo", dto.Motivo.Trim()),
                ("@CedulaUrl", dto.CedulaUrl), ("@Referencia", dto.Referencia?.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            return r == "OK" ? Ok(new { Message = "OK" }) : Error(r);
        }

        [HttpPost("domicilio")]
        public async Task<IActionResult> SolicitarDomicilio([FromBody] SolicitarDomicilioContratoDto dto)
        {
            var fila = await _db.EjecutarAsync("sp_ContratoDomicilioSolicitar", ("@ClienteId", dto.ClienteId), ("@UbicacionId", dto.UbicacionId),
                ("@ComprobanteUrl", dto.ComprobanteUrl), ("@QuienVive", dto.QuienVive.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            return r == "OK" ? Ok(new { Message = "OK" }) : Error(r);
        }

        // ------------------------------------------------------------------ Care Partner

        [HttpGet("cuidador/{cuidadorId:int}/config")]
        public Task<IActionResult> ConfigCuidador(int cuidadorId) => Leer("config contratos", async () =>
            (object?)(await _db.EjecutarAsync("sp_ContratoCuidadorConfig", ("@CuidadorId", cuidadorId), ("@Acepta", null))) ?? new { AceptaContratos = false });

        [HttpPut("cuidador/{cuidadorId:int}/config")]
        public Task<IActionResult> GuardarConfigCuidador(int cuidadorId, [FromBody] CuidadorAceptaContratosDto dto) => Leer("guardar config contratos", async () =>
            (object?)(await _db.EjecutarAsync("sp_ContratoCuidadorConfig", ("@CuidadorId", cuidadorId), ("@Acepta", dto.Acepta))) ?? new { AceptaContratos = dto.Acepta });

        // ------------------------------------------------------------------ propuesta y negociación

        /// <summary>Datos para la pantalla de proponer: permiso del cliente, si el cuidador acepta y domicilios aprobados.</summary>
        [HttpGet("puede-proponer")]
        public Task<IActionResult> PuedeProponer([FromQuery] int clienteId, [FromQuery] int cuidadorId) => Leer("puede proponer", async () =>
        {
            var r = await _db.ConsultarAsync("sp_ContratoPuedeProponer", ("@ClienteId", clienteId), ("@CuidadorId", cuidadorId));
            return new { Estado = r[0].FirstOrDefault(), Domicilios = r[1] };
        });

        [HttpPost]
        public async Task<IActionResult> Proponer([FromBody] ProponerContratoDto dto)
        {
            var horario = ContratoService.HorarioTexto(dto.Horario);
            if (string.IsNullOrEmpty(horario)) return Error("HORARIO_INVALIDO");

            var fila = await _db.EjecutarAsync("sp_ContratoProponer",
                ("@ClienteId", dto.ClienteId), ("@CuidadorId", dto.CuidadorId), ("@UbicacionId", dto.UbicacionId),
                ("@TipoServicio", dto.TipoServicio.Trim()), ("@TarifaHora", dto.TarifaHora), ("@MetodoPago", dto.MetodoPago),
                ("@FechaInicio", dto.FechaInicio.Date), ("@FechaFin", dto.FechaFin?.Date), ("@Horario", horario),
                ("@Nota", string.IsNullOrWhiteSpace(dto.Nota) ? null : dto.Nota.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);

            var id = Convert.ToInt32(fila!["ContratoId"]);
            await _contratos.NotificarAsync(Convert.ToInt32(fila["NotificarA"]), id, 1);
            return Ok(new { ContratoId = id });
        }

        [HttpPost("{id:int}/responder")]
        public async Task<IActionResult> Responder(int id, [FromBody] ResponderContratoDto dto)
        {
            var accion = dto.Accion.Trim().ToLowerInvariant();
            string? horario = null;
            if (accion == "contraproponer")
            {
                horario = ContratoService.HorarioTexto(dto.Horario);
                if (string.IsNullOrEmpty(horario)) return Error("HORARIO_INVALIDO");
            }

            var fila = await _db.EjecutarAsync("sp_ContratoResponder", ("@ContratoId", id), ("@UsuarioId", dto.UsuarioId), ("@Accion", accion),
                ("@TarifaHora", dto.TarifaHora), ("@FechaInicio", dto.FechaInicio?.Date), ("@Horario", horario),
                ("@Nota", string.IsNullOrWhiteSpace(dto.Nota) ? null : dto.Nota.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);

            await _contratos.NotificarAsync(Convert.ToInt32(fila!["NotificarA"]), id, fila["Estado"]);
            return Ok(new { Estado = fila["Estado"] });
        }

        // ------------------------------------------------------------------ consultas

        [HttpGet("usuario/{usuarioId:int}")]
        public Task<IActionResult> PorUsuario(int usuarioId) => Leer("contratos usuario", async () =>
            (await _db.ConsultarAsync("sp_ContratosPorUsuario", ("@UsuarioId", usuarioId)))[0]);

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detalle(int id, [FromQuery] int usuarioId)
        {
            try
            {
                var r = await _db.ConsultarAsync("sp_ContratoDetalle", ("@ContratoId", id), ("@UsuarioId", usuarioId));
                var c = r[0].FirstOrDefault();
                if (c == null) return Error("NO_ENCONTRADO");
                // La app no necesita el trazo ni la IP: solo quién firmó y cuándo.
                var firmas = r[4].Select(f => new { UsuarioId = f["UsuarioId"], Nombre = f["NombreCompleto"], Fecha = f["Fecha"] });
                return Ok(new { Contrato = c, Horario = r[1], Propuestas = r[2], Ausencias = r[3], Firmas = firmas, Documentos = r[5], Trabajos = r[6] });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo el contrato {Id}", id);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        // ------------------------------------------------------------------ firma

        [HttpPost("{id:int}/codigo")]
        public async Task<IActionResult> Codigo(int id, [FromBody] CodigoFirmaDto dto)
        {
            try
            {
                var (r, correo) = await _contratos.EnviarCodigoAsync(id, dto.UsuarioId);
                return r == "OK" ? Ok(new { Correo = correo }) : Error(r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el código del contrato {Id}", id);
                return StatusCode(500, new ApiErrorDto("CORREO_ERROR", "CORREO_ERROR"));
            }
        }

        [HttpPost("{id:int}/firmar")]
        public async Task<IActionResult> Firmar(int id, [FromBody] FirmarContratoDto dto)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var (r, activado) = await _contratos.FirmarAsync(id, dto, ip);
                return r == "OK" ? Ok(new { Activado = activado }) : Error(r);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error firmando el contrato {Id}", id);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        // ------------------------------------------------------------------ contrato activo

        [HttpPost("{id:int}/ausencias")]
        public async Task<IActionResult> Ausencia(int id, [FromBody] AusenciaContratoDto dto)
        {
            var fila = await _db.EjecutarAsync("sp_ContratoAusencia", ("@ContratoId", id), ("@UsuarioId", dto.UsuarioId),
                ("@Fecha", dto.Fecha.Date), ("@Motivo", string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);
            await _contratos.NotificarAsync(Convert.ToInt32(fila!["NotificarA"]), id, "ausencia");
            return Ok(new { Message = "OK" });
        }

        [HttpDelete("ausencias/{ausenciaId:int}")]
        public async Task<IActionResult> QuitarAusencia(int ausenciaId, [FromQuery] int usuarioId)
        {
            var fila = await _db.EjecutarAsync("sp_ContratoQuitarAusencia", ("@AusenciaId", ausenciaId), ("@UsuarioId", usuarioId), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);
            if (fila!["NotificarA"] is int otro) await _contratos.NotificarAsync(otro, 0, "ausencia");
            await _contratos.GenerarTrabajosAsync();
            return Ok(new { Message = "OK" });
        }

        [HttpPost("{id:int}/estado")]
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] EstadoContratoDto dto)
        {
            var fila = await _db.EjecutarAsync("sp_ContratoCambiarEstado", ("@ContratoId", id), ("@UsuarioId", dto.UsuarioId),
                ("@Accion", dto.Accion.Trim().ToLowerInvariant()), ("@Motivo", dto.Motivo?.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);
            await _contratos.NotificarAsync(Convert.ToInt32(fila!["NotificarA"]), id, fila["Estado"]);
            if (dto.Accion.Equals("reanudar", StringComparison.OrdinalIgnoreCase))
                await _contratos.GenerarTrabajosAsync(id);
            return Ok(new { Estado = fila["Estado"], FechaFin = fila["FechaFin"] });
        }

        // ------------------------------------------------------------------ PDF

        /// <summary>
        /// Enlace temporal (10 min) al PDF más reciente del contrato. Solo para una de las partes.
        /// La URL que devuelve es relativa a /api/.
        /// </summary>
        [HttpGet("{id:int}/enlace-pdf")]
        public async Task<IActionResult> EnlacePdf(int id, [FromQuery] int usuarioId)
        {
            var r = await _db.ConsultarAsync("sp_ContratoDetalle", ("@ContratoId", id), ("@UsuarioId", usuarioId));
            if (r[0].Count == 0) return Error("NO_ENCONTRADO");
            var doc = r[5].FirstOrDefault();
            if (doc == null) return Error("SIN_DOCUMENTO");
            var docId = Convert.ToInt32(doc["Id"]);
            var (expira, firma) = _contratos.CrearEnlace(docId);
            return Ok(new { Url = $"contratos/documento/{docId}?exp={expira}&sig={firma}" });
        }

        [HttpGet("documento/{documentoId:int}")]
        public async Task<IActionResult> Documento(int documentoId, [FromQuery] long exp, [FromQuery] string? sig)
        {
            if (!_contratos.EnlaceValido(documentoId, exp, sig))
                return StatusCode(403, new ApiErrorDto("ENLACE_VENCIDO", "El enlace venció. Vuelve a abrir el contrato."));
            var (doc, bytes, _) = await _contratos.LeerDocumentoAsync(documentoId);
            if (doc == null || bytes == null) return NotFound(new ApiErrorDto("NO_ENCONTRADO", "Documento no disponible."));
            return File(bytes, "application/pdf", $"Contrato-CF-{Convert.ToInt32(doc["ContratoId"]):D5}.pdf");
        }
    }
}
