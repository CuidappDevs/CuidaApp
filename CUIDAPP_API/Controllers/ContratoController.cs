using System.Security.Claims;
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
    /// La app envía su JWT y se valida manualmente la identidad. Los errores
    /// devuelven { code, message }: la app traduce el code con la clave contrato_err_{code}.
    /// </summary>
    [Route("api/contratos")]
    [ApiController]
    public partial class ContratoController : ControllerBase
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

        private IActionResult? ValidarUsuario(int usuarioId)
        {
            if (User.Identity?.IsAuthenticated != true)
                return Unauthorized(new ApiErrorDto("AUTH_REQUIRED", "AUTH_REQUIRED"));
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (!int.TryParse(claim, out var id) || id != usuarioId)
                return StatusCode(403, new ApiErrorDto("IDENTIDAD_INVALIDA", "IDENTIDAD_INVALIDA"));
            return null;
        }

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
        public Task<IActionResult> Permiso(int clienteId) => ValidarUsuario(clienteId) is { } error ? Task.FromResult(error) : Leer("permiso contrato", async () =>
        {
            var r = await _db.ConsultarAsync("sp_ContratoPermisoObtener", ("@ClienteId", clienteId));
            return new { Permiso = r[0].FirstOrDefault(), Domicilios = r[1] };
        });

        [HttpPost("permiso")]
        public async Task<IActionResult> SolicitarPermiso([FromBody] SolicitarPermisoContratoDto dto)
        {
            if (ValidarUsuario(dto.ClienteId) is { } error) return error;
            var fila = await _db.EjecutarAsync("sp_ContratoPermisoSolicitar", ("@ClienteId", dto.ClienteId), ("@Motivo", dto.Motivo.Trim()),
                ("@CedulaUrl", dto.CedulaUrl), ("@Referencia", dto.Referencia?.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            return r == "OK" ? Ok(new { Message = "OK" }) : Error(r);
        }

        [HttpPost("domicilio")]
        public async Task<IActionResult> SolicitarDomicilio([FromBody] SolicitarDomicilioContratoDto dto)
        {
            if (ValidarUsuario(dto.ClienteId) is { } error) return error;
            var fila = await _db.EjecutarAsync("sp_ContratoDomicilioSolicitar", ("@ClienteId", dto.ClienteId), ("@UbicacionId", dto.UbicacionId),
                ("@ComprobanteUrl", dto.ComprobanteUrl), ("@QuienVive", dto.QuienVive.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            return r == "OK" ? Ok(new { Message = "OK" }) : Error(r);
        }

        // ------------------------------------------------------------------ Care Partner

        [HttpGet("cuidador/{cuidadorId:int}/config")]
        public Task<IActionResult> ConfigCuidador(int cuidadorId) => ValidarUsuario(cuidadorId) is { } error ? Task.FromResult(error) : Leer("config contratos", async () =>
            (object?)(await _db.EjecutarAsync("sp_ContratoCuidadorConfig", ("@CuidadorId", cuidadorId), ("@Acepta", null))) ?? new { AceptaContratos = false });

        [HttpPut("cuidador/{cuidadorId:int}/config")]
        public Task<IActionResult> GuardarConfigCuidador(int cuidadorId, [FromBody] CuidadorAceptaContratosDto dto) => ValidarUsuario(cuidadorId) is { } error ? Task.FromResult(error) : Leer("guardar config contratos", async () =>
            (object?)(await _db.EjecutarAsync("sp_ContratoCuidadorConfig", ("@CuidadorId", cuidadorId), ("@Acepta", dto.Acepta))) ?? new { AceptaContratos = dto.Acepta });

        // ------------------------------------------------------------------ propuesta y negociación

        /// <summary>Datos para la pantalla de proponer: permiso del cliente, si el cuidador acepta y domicilios aprobados.</summary>
        [HttpGet("puede-proponer")]
        public Task<IActionResult> PuedeProponer([FromQuery] int clienteId, [FromQuery] int cuidadorId) => ValidarUsuario(clienteId) is { } error ? Task.FromResult(error) : Leer("puede proponer", async () =>
        {
            var r = await _db.ConsultarAsync("sp_ContratoPuedeProponer", ("@ClienteId", clienteId), ("@CuidadorId", cuidadorId));
            return new { Estado = r[0].FirstOrDefault(), Domicilios = r[1] };
        });

        [HttpPost]
        public async Task<IActionResult> Proponer([FromBody] ProponerContratoDto dto)
        {
            if (ValidarUsuario(dto.ClienteId) is { } error) return error;
            if (!TareasValidas(dto.Tareas)) return Error("TAREAS_INVALIDAS");
            var horario = ContratoService.HorarioTexto(dto.Horario);
            if (string.IsNullOrEmpty(horario)) return Error("HORARIO_INVALIDO");

            var fila = await _db.EjecutarAsync("sp_ContratoProponer",
                ("@ClienteId", dto.ClienteId), ("@CuidadorId", dto.CuidadorId), ("@UbicacionId", dto.UbicacionId),
                ("@TipoServicio", dto.TipoServicio.Trim()), ("@TarifaHora", dto.TarifaHora), ("@MetodoPago", dto.MetodoPago),
                ("@FechaInicio", dto.FechaInicio.Date), ("@FechaFin", dto.FechaFin?.Date), ("@Horario", horario), ("@Dormida", dto.Dormida), ("@Tareas", SerializarTareas(dto.Tareas)),
                ("@Nota", string.IsNullOrWhiteSpace(dto.Nota) ? null : dto.Nota.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);

            var id = Convert.ToInt32(fila!["ContratoId"]);
            if (dto.TrabajaDesde is DateTime desde && desde.Date <= Ahora.Date)
                await _db.EjecutarAsync("sp_ContratoTrabajaDesde", ("@ContratoId", id), ("@ClienteId", dto.ClienteId), ("@TrabajaDesde", desde.Date));
            await _contratos.NotificarAsync(Convert.ToInt32(fila["NotificarA"]), id, 1);
            return Ok(new { ContratoId = id });
        }

        // ------------------------------------------------------------------ invitar a mi cuidadora

        // Sin 0/O, 1/I/L para que se pueda dictar por teléfono.
        private const string AlfabetoCodigo = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        [HttpPost("invitaciones")]
        public async Task<IActionResult> CrearInvitacion([FromBody] CrearInvitacionDto dto)
        {
            if (ValidarUsuario(dto.ClienteId) is { } error) return error;
            if (string.IsNullOrWhiteSpace(dto.Telefono) && string.IsNullOrWhiteSpace(dto.Email))
                return Error("CONTACTO_REQUERIDO");
            try
            {
                for (var intento = 0; intento < 5; intento++)
                {
                    var codigo = System.Security.Cryptography.RandomNumberGenerator.GetString(AlfabetoCodigo, 8);
                    var fila = await _db.EjecutarAsync("sp_InvitacionCrear", ("@ClienteId", dto.ClienteId), ("@Codigo", codigo),
                        ("@Nombre", dto.Nombre.Trim()), ("@Telefono", dto.Telefono?.Trim()), ("@Email", dto.Email?.Trim()),
                        ("@TrabajaDesde", dto.TrabajaDesde?.Date), ("@Ahora", Ahora));
                    var r = fila?["Resultado"]?.ToString() ?? "ERROR";
                    if (r == "REINTENTAR") continue;
                    return r == "OK" ? Ok(new { Id = fila!["Id"], Codigo = codigo, Expira = fila["Expira"] }) : Error(r);
                }
                return Error("ERROR");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando la invitación del cliente {Id}", dto.ClienteId);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        [HttpGet("invitaciones/cliente/{clienteId:int}")]
        public Task<IActionResult> Invitaciones(int clienteId) => ValidarUsuario(clienteId) is { } error ? Task.FromResult(error) : Leer("invitaciones", async () =>
            (await _db.ConsultarAsync("sp_InvitacionesPorCliente", ("@ClienteId", clienteId), ("@Ahora", Ahora)))[0]);

        [HttpPost("invitaciones/{id:int}/cancelar")]
        public async Task<IActionResult> CancelarInvitacion(int id, [FromBody] CancelarInvitacionDto dto)
        {
            if (ValidarUsuario(dto.ClienteId) is { } error) return error;
            var fila = await _db.EjecutarAsync("sp_InvitacionCancelar", ("@Id", id), ("@ClienteId", dto.ClienteId));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            return r == "OK" ? Ok(new { Message = "OK" }) : Error(r);
        }

        /// <summary>Para el registro de la cuidadora: valida el código y devuelve quién la invitó.</summary>
        [HttpGet("invitaciones/codigo/{codigo}")]
        public async Task<IActionResult> InvitacionPorCodigo(string codigo)
        {
            var fila = await _db.EjecutarAsync("sp_InvitacionObtener", ("@Codigo", codigo.Trim().ToUpperInvariant()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "INVITACION_INVALIDA";
            if (r != "OK") return Error(r == "USADA" || r == "VENCIDA" ? "INVITACION_" + r : "INVITACION_INVALIDA");
            return Ok(new { Nombre = fila!["Nombre"], Telefono = fila["Telefono"], Email = fila["Email"], ClienteNombre = fila["ClienteNombre"] });
        }

        [HttpPost("{id:int}/responder")]
        public async Task<IActionResult> Responder(int id, [FromBody] ResponderContratoDto dto)
        {
            if (ValidarUsuario(dto.UsuarioId) is { } error) return error;
            if (!TareasValidas(dto.Tareas)) return Error("TAREAS_INVALIDAS");
            var accion = dto.Accion.Trim().ToLowerInvariant();
            string? horario = null;
            if (accion == "contraproponer")
            {
                horario = ContratoService.HorarioTexto(dto.Horario);
                if (string.IsNullOrEmpty(horario)) return Error("HORARIO_INVALIDO");
            }

            var fila = await _db.EjecutarAsync("sp_ContratoResponder", ("@ContratoId", id), ("@UsuarioId", dto.UsuarioId), ("@Accion", accion),
                ("@TarifaHora", dto.TarifaHora), ("@FechaInicio", dto.FechaInicio?.Date), ("@Horario", horario), ("@Dormida", dto.Dormida), ("@Tareas", SerializarTareas(dto.Tareas)),
                ("@Nota", string.IsNullOrWhiteSpace(dto.Nota) ? null : dto.Nota.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);

            await _contratos.NotificarAsync(Convert.ToInt32(fila!["NotificarA"]), id, fila["Estado"]);
            return Ok(new { Estado = fila["Estado"] });
        }

        // ------------------------------------------------------------------ consultas

        [HttpGet("usuario/{usuarioId:int}")]
        public Task<IActionResult> PorUsuario(int usuarioId) => ValidarUsuario(usuarioId) is { } error ? Task.FromResult(error) : Leer("contratos usuario", async () =>
            (await _db.ConsultarAsync("sp_ContratosPorUsuario", ("@UsuarioId", usuarioId)))[0]);

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detalle(int id, [FromQuery] int usuarioId)
        {
            if (ValidarUsuario(usuarioId) is { } error) return error;
            try
            {
                var r = await _db.ConsultarAsync("sp_ContratoDetalle", ("@ContratoId", id), ("@UsuarioId", usuarioId));
                var c = r[0].FirstOrDefault();
                if (c == null) return Error("NO_ENCONTRADO");
                // La app no necesita el trazo ni la IP: solo quién firmó y cuándo.
                var firmas = r[4].Select(f => new { UsuarioId = f["UsuarioId"], Nombre = f["NombreCompleto"], Fecha = f["Fecha"] });
                return Ok(new { Contrato = c, Horario = r[1], Propuestas = r[2], Ausencias = r[3], Firmas = firmas, Documentos = r[5], Trabajos = r[6], Tareas = r[7] });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo el contrato {Id}", id);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        [HttpGet("{id:int}/resumen-mensual")]
        public Task<IActionResult> ResumenMensual(int id, [FromQuery] int usuarioId, [FromQuery] DateTime mes)
            => ValidarUsuario(usuarioId) is { } error ? Task.FromResult(error) : Leer("resumen mensual", async () =>
                (object?)(await _db.EjecutarAsync("sp_ContratoResumenMensual", ("@ContratoId", id),
                    ("@UsuarioId", usuarioId), ("@Mes", mes.Date), ("@Ahora", Ahora))) ?? new { });

        [HttpPost("{id:int}/renovar")]
        public async Task<IActionResult> Renovar(int id, [FromBody] RenovarContratoDto dto)
        {
            if (ValidarUsuario(dto.UsuarioId) is { } error) return error;
            var fila = await _db.EjecutarAsync("sp_ContratoRenovar", ("@ContratoId", id), ("@UsuarioId", dto.UsuarioId),
                ("@FechaFin", dto.FechaFin.Date), ("@Ahora", Ahora));
            var resultado = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (resultado != "OK") return Error(resultado);
            await _contratos.NotificarAsync(Convert.ToInt32(fila!["NotificarA"]), Convert.ToInt32(fila["ContratoId"]), "renovacion");
            return Ok(new { ContratoId = fila["ContratoId"] });
        }

        // ------------------------------------------------------------------ firma

        [HttpPost("{id:int}/codigo")]
        public async Task<IActionResult> Codigo(int id, [FromBody] CodigoFirmaDto dto)
        {
            if (ValidarUsuario(dto.UsuarioId) is { } error) return error;
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
            if (ValidarUsuario(dto.UsuarioId) is { } error) return error;
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
            if (ValidarUsuario(dto.UsuarioId) is { } error) return error;
            var fila = await _db.EjecutarAsync("sp_ContratoAusencia", ("@ContratoId", id), ("@UsuarioId", dto.UsuarioId),
                ("@Fecha", dto.Fecha.Date), ("@Motivo", string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim()), ("@Ahora", Ahora), ("@Emergencia", dto.Emergencia));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);
            // Si falta la titular y hay jornada ese día, el cliente recibe un solo aviso: el de elegir relevo.
            if (!await NotificarAusenciaRelevo(id, dto))
                await _contratos.NotificarAsync(Convert.ToInt32(fila!["NotificarA"]), id, "ausencia");
            return Ok(new { Message = "OK" });
        }

        [HttpDelete("ausencias/{ausenciaId:int}")]
        public async Task<IActionResult> QuitarAusencia(int ausenciaId, [FromQuery] int usuarioId)
        {
            if (ValidarUsuario(usuarioId) is { } error) return error;
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
            if (ValidarUsuario(dto.UsuarioId) is { } error) return error;
            var fila = await _db.EjecutarAsync("sp_ContratoCambiarEstado", ("@ContratoId", id), ("@UsuarioId", dto.UsuarioId),
                ("@Accion", dto.Accion.Trim().ToLowerInvariant()), ("@Motivo", dto.Motivo?.Trim()), ("@Ahora", Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);
            await _contratos.NotificarAsync(Convert.ToInt32(fila!["NotificarA"]), id, fila["Estado"]);
            if (dto.Accion.Equals("reanudar", StringComparison.OrdinalIgnoreCase))
                await _contratos.GenerarTrabajosAsync(id);
            return Ok(new { Estado = fila["Estado"], FechaFin = fila["FechaFin"] });
        }

        [HttpGet("{id:int}/turnos/{trabajoId:int}/reemplazos")]
        public async Task<IActionResult> Reemplazos(int id, int trabajoId, [FromQuery] int usuarioId,
            [FromServices] CUIDAPP_API.Interfaces.Busqueda.IBusquedaService busqueda)
        {
            if (ValidarUsuario(usuarioId) is { } error) return error;
            var t = await _db.EjecutarAsync("sp_ContratoTurnoReemplazo", ("@ContratoId", id), ("@TrabajoId", trabajoId),
                ("@UsuarioId", usuarioId), ("@Ahora", Ahora));
            if (t == null) return Error("NO_ENCONTRADO");
            return Ok(await Candidatos(id, trabajoId, usuarioId, busqueda));
        }

        [HttpPost("{id:int}/turnos/{trabajoId:int}/reemplazos/{cuidadorId:int}")]
        public async Task<IActionResult> Reemplazar(int id, int trabajoId, int cuidadorId, [FromBody] CodigoFirmaDto dto,
            [FromServices] CUIDAPP_API.Interfaces.Busqueda.IBusquedaService busqueda,
            [FromServices] CUIDAPP_API.Services.Realtime.ITrabajoNotifier notifier)
        {
            if (ValidarUsuario(dto.UsuarioId) is { } error) return error;
            var t = await _db.EjecutarAsync("sp_ContratoTurnoReemplazo", ("@ContratoId", id), ("@TrabajoId", trabajoId),
                ("@UsuarioId", dto.UsuarioId), ("@Ahora", Ahora));
            if (t == null) return Error("NO_ENCONTRADO");
            var candidatos = await busqueda.ObtenerCuidadoresPorServicioAsync(t["TipoServicio"]!.ToString()!,
                Convert.ToDecimal(t["Latitud"]), Convert.ToDecimal(t["Longitud"]), 15);
            var candidato = candidatos.FirstOrDefault(c => c.Id == cuidadorId && c.Id != Convert.ToInt32(t["TitularId"]) &&
                (t["MetodoPago"]?.ToString() != "efectivo" || c.PuedeAceptarEfectivo));
            if (candidato == null) return Error("CUIDADOR_NO_ACEPTA");
            var fila = await _db.EjecutarAsync("sp_ContratoReemplazar", ("@ContratoId", id), ("@TrabajoId", trabajoId),
                ("@UsuarioId", dto.UsuarioId), ("@CuidadorId", cuidadorId), ("@TarifaHora", candidato.TarifaHora), ("@Ahora", Ahora));
            var resultado = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (resultado != "OK") return Error(resultado);
            await notifier.NotificarAsync(cuidadorId, "NuevaSolicitud", new { TrabajoId = fila!["TrabajoId"], ClienteId = dto.UsuarioId });
            await _contratos.NotificarAsync(dto.UsuarioId, id, "reemplazo");
            return Ok(new { TrabajoId = fila!["TrabajoId"] });
        }

        [HttpGet("{id:int}/enlace-pdf")]
        public async Task<IActionResult> EnlacePdf(int id, [FromQuery] int usuarioId, [FromQuery] int? documentoId = null)
        {
            if (ValidarUsuario(usuarioId) is { } error) return error;
            var r = await _db.ConsultarAsync("sp_ContratoDetalle", ("@ContratoId", id), ("@UsuarioId", usuarioId));
            if (r[0].Count == 0) return Error("NO_ENCONTRADO");
            var doc = r[5].FirstOrDefault(f => documentoId == null || Convert.ToInt32(f["Id"]) == documentoId);
            if (doc == null) return Error("SIN_DOCUMENTO");
            var estadoDoc=await _db.EjecutarAsync("sp_ContratoDocumento",("@DocumentoId",doc["Id"]));
            if (estadoDoc != null && Convert.ToBoolean(estadoDoc.GetValueOrDefault("EnBoveda") ?? false))
                return StatusCode(410,new { motivo="EN_BOVEDA",message="Este contrato está en la bóveda de cumplimiento." });
            var docId = Convert.ToInt32(doc["Id"]);
            var (expira, firma) = _contratos.CrearEnlace(docId);
            return Ok(new { Url = $"contratos/documento/{docId}?exp={expira}&sig={firma}" });
        }

        [HttpGet("documento/{documentoId:int}")]
        public async Task<IActionResult> Documento(int documentoId, [FromQuery] long exp, [FromQuery] string? sig)
        {
            if (!_contratos.EnlaceValido(documentoId, exp, sig))
                return StatusCode(403, new ApiErrorDto("ENLACE_VENCIDO", "El enlace venció. Vuelve a abrir el contrato."));
            var (doc, bytes, integro) = await _contratos.LeerDocumentoAsync(documentoId);
            if (doc != null && Convert.ToBoolean(doc.GetValueOrDefault("EnBoveda") ?? false))
                return StatusCode(410,new { motivo="EN_BOVEDA",message="Este contrato está en la bóveda de cumplimiento." });
            if (doc == null || bytes == null) return NotFound(new ApiErrorDto("NO_ENCONTRADO", "Documento no disponible."));
            if (!integro) return Conflict(new ApiErrorDto("DOCUMENTO_ALTERADO", "DOCUMENTO_ALTERADO"));
            return File(bytes, "application/pdf", $"Contrato-CF-{Convert.ToInt32(doc["ContratoId"]):D5}.pdf");
        }
    }
}
