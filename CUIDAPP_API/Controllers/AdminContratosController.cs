using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.DTOs.Contrato;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.Services;
using CUIDAPP_API.Services.Admin;
using CUIDAPP_API.Services.Contrato;

namespace CUIDAPP_API.Controllers
{
    /// <summary>
    /// Contratos fijos en el panel: solicitudes de permiso para contratar, domicilios a validar,
    /// lista y detalle de contratos, y PDF firmado (descarga y verificación de integridad).
    /// La descarga es POST a propósito: así queda en la auditoría como cualquier acción del panel.
    /// </summary>
    [Route("api/admin/contratos")]
    [ApiController]
    [Authorize(Policy = PoliticasAdmin.Operaciones)]
    [AuditarAdmin]
    public class AdminContratosController : ControllerBase
    {
        private readonly IAdminOperacionesService _db;
        private readonly ContratoService _contratos;
        private readonly CentroMandoService _avisos;
        private readonly ILogger<AdminContratosController> _logger;

        public AdminContratosController(IAdminOperacionesService db, ContratoService contratos, CentroMandoService avisos, ILogger<AdminContratosController> logger)
        {
            _db = db;
            _contratos = contratos;
            _avisos = avisos;
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

        private IActionResult Error(string codigo) => codigo switch
        {
            "NO_ENCONTRADO" => NotFound(new ApiErrorDto(codigo, "No se encontró el registro.")),
            "ESTADO_INVALIDO" => Conflict(new ApiErrorDto(codigo, "La solicitud ya no está en un estado que permita esta acción.")),
            "COMENTARIO_REQUERIDO" => BadRequest(new ApiErrorDto(codigo, "Escribe el motivo (mínimo 5 caracteres): el cliente lo verá.")),
            _ => BadRequest(new ApiErrorDto(codigo, "No se pudo completar la acción."))
        };

        [HttpGet("solicitudes")]
        public Task<IActionResult> Solicitudes([FromQuery] int? estado) => Leer("solicitudes contrato", async () =>
            (await _db.ConsultarAsync("sp_AdminContratoSolicitudes", ("@Estado", estado)))[0]);

        [HttpPost("solicitudes/{clienteId:int}/revisar")]
        public async Task<IActionResult> RevisarPermisoContratacion(int clienteId, [FromBody] RevisionContratoDto dto)
        {
            var accion = dto.Accion.Trim().ToLowerInvariant();
            var fila = await _db.EjecutarAsync("sp_AdminContratoRevisarPermiso", ("@ClienteId", clienteId), ("@Accion", accion),
                ("@Comentario", dto.Comentario?.Trim()), ("@AdminId", AdminId), ("@Ahora", HoraLocalRD.Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);

            var (titulo, mensaje) = accion switch
            {
                "aprobar" => ("Ya puedes contratar fijo", "La administración aprobó tu solicitud. Valida el domicilio donde quieres el servicio y propón un contrato desde el perfil del Care Partner."),
                "rechazar" => ("Solicitud para contratar no aprobada", dto.Comentario?.Trim() ?? ""),
                _ => ("Permiso para contratar retirado", (dto.Comentario?.Trim() ?? "") + " Tus contratos fijos activos quedaron en pausa.")
            };
            await Avisar(clienteId, titulo, mensaje);
            await _contratos.NotificarAsync(clienteId, 0, "permiso");

            foreach (var id in (fila!["CuidadoresAfectados"]?.ToString() ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(id, out var cuidadorId))
                {
                    await Avisar(cuidadorId, "Contrato fijo en pausa", "Uno de tus contratos fijos quedó en pausa por decisión de la administración. Las próximas jornadas se cancelaron.");
                    await _contratos.NotificarAsync(cuidadorId, 0, 5);
                }
            return Ok(new { Message = "Listo." });
        }

        [HttpGet("domicilios")]
        public Task<IActionResult> Domicilios([FromQuery] int? estado) => Leer("domicilios contrato", async () =>
            (await _db.ConsultarAsync("sp_AdminContratoDomicilios", ("@Estado", estado)))[0]);

        [HttpPost("domicilios/{id:int}/revisar")]
        public async Task<IActionResult> RevisarDomicilioContratacion(int id, [FromBody] RevisionContratoDto dto)
        {
            var accion = dto.Accion.Trim().ToLowerInvariant();
            var fila = await _db.EjecutarAsync("sp_AdminContratoRevisarDomicilio", ("@DomicilioId", id), ("@Accion", accion),
                ("@Comentario", dto.Comentario?.Trim()), ("@AdminId", AdminId), ("@Ahora", HoraLocalRD.Ahora));
            var r = fila?["Resultado"]?.ToString() ?? "ERROR";
            if (r != "OK") return Error(r);

            var clienteId = Convert.ToInt32(fila!["ClienteId"]);
            if (accion == "aprobar")
                await Avisar(clienteId, "Domicilio validado", "Ya puedes contratar Care Partners fijos en ese domicilio.");
            else
                await Avisar(clienteId, "Domicilio no validado", dto.Comentario?.Trim() ?? "");
            await _contratos.NotificarAsync(clienteId, 0, "domicilio");
            return Ok(new { Message = "Listo." });
        }

        [HttpGet]
        public Task<IActionResult> Lista([FromQuery] int? estado) => Leer("contratos", async () =>
            (await _db.ConsultarAsync("sp_AdminContratos", ("@Estado", estado)))[0]);

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detalle(int id)
        {
            try
            {
                var r = await _db.ConsultarAsync("sp_ContratoDetalle", ("@ContratoId", id), ("@UsuarioId", null));
                if (r[0].Count == 0) return Error("NO_ENCONTRADO");
                return Ok(new { Contrato = r[0][0], Horario = r[1], Propuestas = r[2], Ausencias = r[3], Firmas = r[4], Documentos = r[5], Trabajos = r[6] });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leyendo el contrato {Id}", id);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        [HttpPost("documentos/{documentoId:int}/descargar")]
        public async Task<IActionResult> DescargarDocumentoContrato(int documentoId)
        {
            var (doc, bytes, _) = await _contratos.LeerDocumentoAsync(documentoId);
            if (doc == null || bytes == null) return Error("NO_ENCONTRADO");
            return File(bytes, "application/pdf", $"Contrato-CF-{Convert.ToInt32(doc["ContratoId"]):D5}-v{doc["Version"]}.pdf");
        }

        [HttpPost("documentos/{documentoId:int}/verificar")]
        public async Task<IActionResult> VerificarDocumentoContrato(int documentoId)
        {
            var (doc, bytes, integro) = await _contratos.LeerDocumentoAsync(documentoId);
            if (doc == null) return Error("NO_ENCONTRADO");
            return Ok(new { Existe = bytes != null, Integro = integro, Hash = doc["Hash"] });
        }

        private async Task Avisar(int usuarioId, string titulo, string mensaje)
        {
            try { await _avisos.EnviarAsync(AdminId, usuarioId, "mensaje", titulo, mensaje); }
            catch (Exception ex) { _logger.LogWarning(ex, "No se pudo avisar al usuario {Id}", usuarioId); }
        }
    }
}
