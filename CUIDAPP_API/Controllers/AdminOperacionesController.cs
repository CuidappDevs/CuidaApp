using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.Services;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Controllers
{
    /// <summary>
    /// Operación diaria del panel administrativo. Todo exige un JWT de administrador; las acciones
    /// que cambian datos quedan en la bitácora (AuditoriaAdmin).
    /// </summary>
    [Route("api/admin")]
    [ApiController]
    [Authorize(Policy = PoliticasAdmin.Admin)]
    [AuditarAdmin]
    public class AdminOperacionesController : ControllerBase
    {
        private readonly IAdminOperacionesService _svc;
        private readonly ITrabajoNotifier _notifier;
        private readonly ILogger<AdminOperacionesController> _logger;

        public AdminOperacionesController(IAdminOperacionesService svc, ITrabajoNotifier notifier, ILogger<AdminOperacionesController> logger)
        {
            _svc = svc;
            _notifier = notifier;
            _logger = logger;
        }

        private int AdminId => PoliticasAdmin.AdminId(User);

        private async Task<IActionResult> Leer(string operacion, Func<Task<object>> accion)
        {
            try { return Ok(await accion()); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en {Operacion}", operacion);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        // Convierte el "Resultado" de un SP de escritura en la respuesta HTTP.
        private IActionResult Resultado(Fila? fila, string ok = "Cambios guardados.")
        {
            var codigo = fila?["Resultado"]?.ToString() ?? "ERROR";
            return codigo switch
            {
                "OK" => Ok(new { Message = ok }),
                "NO_ENCONTRADO" => NotFound(new ApiErrorDto(codigo, "No se encontró el registro.")),
                "DUPLICADO" => Conflict(new ApiErrorDto(codigo, "Ya existe un registro con ese nombre.")),
                "ESTADO_INVALIDO" => Conflict(new ApiErrorDto(codigo, "El servicio ya no está en un estado que permita esta acción.")),
                "ULTIMO_SUPERADMIN" => Conflict(new ApiErrorDto(codigo, "Debe quedar al menos un superadministrador activo.")),
                "NIVEL_INVALIDO" => BadRequest(new ApiErrorDto(codigo, "Nivel no válido.")),
                _ => StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "No se pudo completar la acción."))
            };
        }

        private static DateOnly HoyRD => DateOnly.FromDateTime(HoraLocalRD.Ahora);

        // ---------------- Dashboard
        [HttpGet("dashboard")]
        public Task<IActionResult> Dashboard() => Leer("Dashboard", async () =>
        {
            var r = await _svc.ConsultarAsync("sp_AdminDashboard", ("@Hoy", HoyRD.ToDateTime(TimeOnly.MinValue)));
            return new { Indicadores = r[0].FirstOrDefault(), Serie = r[1], Actividad = r[2], Verificacion = r[3] };
        });

        // ---------------- Servicios
        [HttpGet("servicios")]
        public Task<IActionResult> Servicios([FromQuery] int? estado, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
            [FromQuery] string? buscar, [FromQuery] int pagina = 1, [FromQuery] int tamano = 20) => Leer("Servicios", async () =>
        {
            var r = await _svc.ConsultarAsync("sp_AdminTrabajos", ("@Estado", estado), ("@Desde", desde?.Date), ("@Hasta", hasta?.Date),
                ("@Buscar", string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim()), ("@Pagina", Math.Max(1, pagina)), ("@Tamano", Math.Clamp(tamano, 5, 100)));
            var filas = r[0];
            return new { Total = filas.Count > 0 ? Convert.ToInt32(filas[0]["TotalFilas"]) : 0, Filas = filas };
        });

        [HttpGet("servicios/{id:int}")]
        public async Task<IActionResult> Servicio(int id)
        {
            try
            {
                var r = await _svc.ConsultarAsync("sp_AdminTrabajoDetalle", ("@TrabajoId", id));
                if (r[0].Count == 0)
                    return NotFound(new ApiErrorDto("NO_ENCONTRADO", "El servicio no existe."));
                return Ok(new { Servicio = r[0][0], Tareas = r[1], Actividades = r[2], Pagos = r[3], Calificaciones = r[4], Mensajes = r[5], Sos = r[6], Tickets = r[7] });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en detalle de servicio {Id}", id);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPut("servicios/{id:int}/cancelar")]
        public async Task<IActionResult> CancelarServicio(int id, [FromBody] MotivoAdminDto dto)
        {
            var fila = await _svc.EjecutarAsync("sp_AdminCancelarTrabajo", ("@TrabajoId", id), ("@Motivo", dto.Motivo.Trim()));
            await AvisarParticipantes(fila, id, 5);
            return Resultado(fila, "Servicio cancelado.");
        }

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPut("servicios/{id:int}/completar")]
        public async Task<IActionResult> CompletarServicio(int id, [FromBody] MotivoAdminDto dto)
        {
            var fila = await _svc.EjecutarAsync("sp_AdminCompletarTrabajo", ("@TrabajoId", id), ("@Justificacion", dto.Motivo.Trim()), ("@Fecha", HoraLocalRD.Ahora));
            await AvisarParticipantes(fila, id, 4);
            return Resultado(fila, "Servicio completado. Se creó el pago pendiente.");
        }

        private async Task AvisarParticipantes(Fila? fila, int trabajoId, int estado)
        {
            if (fila?["Resultado"]?.ToString() != "OK")
                return;
            foreach (var clave in new[] { "ClienteId", "CuidadorId" })
                if (fila[clave] is int usuarioId)
                    await _notifier.NotificarAsync(usuarioId, "TrabajoActualizado", new { TrabajoId = trabajoId, Estado = estado });
        }

        // ---------------- Verificación, mapa, calificaciones, finanzas
        [HttpGet("verificacion")]
        public Task<IActionResult> Verificacion() => Leer("Verificacion", async () => (await _svc.ConsultarAsync("sp_AdminColaVerificacion"))[0]);

        [HttpGet("mapa")]
        public Task<IActionResult> Mapa() => Leer("Mapa", async () =>
        {
            var r = await _svc.ConsultarAsync("sp_AdminMapa");
            return new { Cuidadores = r[0], Servicios = r[1], Sos = r[2] };
        });

        [HttpGet("calificaciones")]
        public Task<IActionResult> Calificaciones([FromQuery] int? max) => Leer("Calificaciones", async () =>
        {
            var r = await _svc.ConsultarAsync("sp_AdminCalificaciones", ("@MaxPuntuacion", max));
            return new { Calificaciones = r[0], Resumen = r[1] };
        });

        [Authorize(Policy = PoliticasAdmin.Finanzas)]
        [HttpGet("finanzas")]
        public Task<IActionResult> Finanzas([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta) => Leer("Finanzas", async () =>
        {
            var h = hasta?.Date ?? HoyRD.ToDateTime(TimeOnly.MinValue);
            var d = desde?.Date ?? h.AddDays(-29);
            var r = await _svc.ConsultarAsync("sp_AdminFinanzas", ("@Desde", d), ("@Hasta", h));
            return new { Desde = d, Hasta = h, Totales = r[0].FirstOrDefault(), PorCuidador = r[1], Serie = r[2] };
        });

        // ---------------- Auditoría y administradores
        [Authorize(Policy = PoliticasAdmin.SuperAdmin)]
        [HttpGet("auditoria")]
        public Task<IActionResult> Auditoria([FromQuery] int? adminId, [FromQuery] string? buscar, [FromQuery] int pagina = 1, [FromQuery] int tamano = 25)
            => Leer("Auditoria", async () =>
            {
                var filas = (await _svc.ConsultarAsync("sp_AdminObtenerAuditoria", ("@AdminId", adminId),
                    ("@Buscar", string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim()), ("@Pagina", Math.Max(1, pagina)), ("@Tamano", Math.Clamp(tamano, 5, 100))))[0];
                return new { Total = filas.Count > 0 ? Convert.ToInt32(filas[0]["TotalFilas"]) : 0, Filas = filas };
            });

        [Authorize(Policy = PoliticasAdmin.SuperAdmin)]
        [HttpPut("administradores/{usuarioId:int}/nivel")]
        public async Task<IActionResult> CambiarNivelAdmin(int usuarioId, [FromBody] NivelAdminDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminCambiarNivel", ("@UsuarioId", usuarioId), ("@Nivel", dto.Nivel)), "Nivel actualizado. Aplica en su próximo inicio de sesión.");

        // ---------------- Avisos masivos
        [HttpGet("avisos")]
        public Task<IActionResult> Avisos() => Leer("Avisos", async () => (await _svc.ConsultarAsync("sp_AdminObtenerAvisos"))[0]);

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPost("avisos")]
        public async Task<IActionResult> EnviarAviso([FromBody] AvisoMasivoDto dto)
        {
            if (dto.Destino == 1)
                return BadRequest(new ApiErrorDto("DESTINO_INVALIDO", "Destino no válido."));

            await Difundir(dto.Destino, dto.Idioma, dto.Titulo.Trim(), dto.Mensaje.Trim());
            return Ok(new { Message = "Aviso enviado." });
        }

        /// <summary>Vuelve a enviar un aviso del historial (queda como un envío nuevo a nombre de quien lo reenvía).</summary>
        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPost("avisos/{id:int}/reenviar")]
        public async Task<IActionResult> ReenviarAviso(int id)
        {
            var aviso = await _svc.EjecutarAsync("sp_AdminObtenerAviso", ("@Id", id));
            if (aviso == null)
                return NotFound(new ApiErrorDto("NO_ENCONTRADO", "El aviso ya no existe."));

            await Difundir(Convert.ToInt32(aviso["Destino"]), aviso["Idioma"] as string, aviso["Titulo"]!.ToString()!, aviso["Mensaje"]!.ToString()!);
            return Ok(new { Message = "Aviso reenviado." });
        }

        /// <summary>Quita un aviso del historial. No lo borra de los teléfonos que ya lo recibieron.</summary>
        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpDelete("avisos/{id:int}")]
        public async Task<IActionResult> EliminarAviso(int id)
            => Resultado(await _svc.EjecutarAsync("sp_AdminEliminarAviso", ("@Id", id)), "Aviso eliminado del historial.");

        // Guarda el envío y lo emite a todas las apps conectadas; cada app decide si es para su rol e idioma.
        private async Task Difundir(int destino, string? idioma, string titulo, string mensaje)
        {
            var fila = await _svc.EjecutarAsync("sp_AdminRegistrarAviso", ("@AdminId", AdminId), ("@Destino", destino),
                ("@Titulo", titulo), ("@Mensaje", mensaje), ("@Fecha", HoraLocalRD.Ahora), ("@Idioma", idioma));
            await _notifier.NotificarGlobalAsync("AvisoGeneral", new { Id = fila?["Id"], Destino = destino, Idioma = idioma, Titulo = titulo, Mensaje = mensaje });
        }

        // ---------------- Catálogos
        [HttpGet("catalogos/tipos-servicio")]
        public Task<IActionResult> TiposServicio() => Leer("TiposServicio", async () => (await _svc.ConsultarAsync("sp_ObtenerTiposTrabajos"))[0]);

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPost("catalogos/tipos-servicio")]
        public async Task<IActionResult> GuardarTipoTrabajo([FromBody] TipoTrabajoAdminDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminGuardarTipoTrabajo", ("@Id", dto.Id), ("@Nombre", dto.Nombre.Trim()),
                ("@Descripcion", dto.Descripcion?.Trim()), ("@Icono", dto.Icono?.Trim()), ("@Activo", dto.Activo)));

        [HttpGet("catalogos/motivos-cancelacion")]
        public Task<IActionResult> MotivosCancelacion() => Leer("Motivos", async () => (await _svc.ConsultarAsync("sp_AdminObtenerMotivosCancelacion"))[0]);

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPost("catalogos/motivos-cancelacion")]
        public async Task<IActionResult> GuardarMotivoCancelacion([FromBody] MotivoCancelacionAdminDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminGuardarMotivoCancelacion", ("@Id", dto.Id), ("@Descripcion", dto.Descripcion.Trim()),
                ("@Activo", dto.Activo), ("@OrdenVisual", dto.OrdenVisual)));

        [HttpGet("catalogos/nacionalidades")]
        public Task<IActionResult> Nacionalidades() => Leer("Nacionalidades", async () => (await _svc.ConsultarAsync("sp_AdminObtenerNacionalidades"))[0]);

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPost("catalogos/nacionalidades")]
        public async Task<IActionResult> GuardarNacionalidad([FromBody] NacionalidadAdminDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminGuardarNacionalidad", ("@Id", dto.Id), ("@Nombre", dto.Nombre.Trim()),
                ("@Pais", dto.Pais.Trim()), ("@CodigoIso", dto.CodigoIso.Trim().ToUpperInvariant()), ("@Activo", dto.Activo)));

        [HttpGet("catalogos/estatus-migratorio")]
        public Task<IActionResult> EstatusMigratorio() => Leer("EstatusMigratorio", async () =>
        {
            var r = await _svc.ConsultarAsync("sp_AdminObtenerEstatusMigratorio");
            return new { Estatus = r[0], Requisitos = r[1] };
        });

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPost("catalogos/estatus-migratorio")]
        public async Task<IActionResult> GuardarEstatusMigratorio([FromBody] EstatusMigratorioAdminDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminGuardarEstatusMigratorio", ("@Id", dto.Id), ("@Nombre", dto.Nombre.Trim()), ("@Activo", dto.Activo)));

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPost("catalogos/requisitos")]
        public async Task<IActionResult> AgregarRequisito([FromBody] RequisitoAdminDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminAgregarRequisito", ("@EstatusMigratorioId", dto.EstatusMigratorioId), ("@TipoDocumento", dto.TipoDocumento.Trim())));

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpDelete("catalogos/requisitos/{id:int}")]
        public async Task<IActionResult> QuitarRequisito(int id)
            => Resultado(await _svc.EjecutarAsync("sp_AdminQuitarRequisito", ("@Id", id)));
    }
}
