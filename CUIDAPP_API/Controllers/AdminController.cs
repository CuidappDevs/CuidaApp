using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.DTOs.Admin;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.Interfaces.Admin;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = PoliticasAdmin.Admin)]
    [AuditarAdmin]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        // Nunca expone ex.Message, stack ni texto SQL; el detalle va al log estructurado.
        private IActionResult ErrorInterno(Exception ex, string operacion, int? usuarioId = null, int? adminId = null)
        {
            _logger.LogError(ex, "Error interno en {Operacion}. UsuarioId={UsuarioId} AdminId={AdminId}", operacion, usuarioId, adminId);
            return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
        }

        private IActionResult ErrorValidacion(Dictionary<string, string[]> errores)
            => BadRequest(new ApiErrorDto("VALIDATION_ERROR", "Hay datos inválidos en la solicitud.", errores));

        public static IActionResult TraducirResultado(AdminActionResult r)
        {
            ObjectResult Error(int status, string code) => new(new ApiErrorDto(code, r.Message)) { StatusCode = status };

            return r.Code switch
            {
                "APPLIED" or "ALREADY_COMPLETED" => new OkObjectResult(r),
                "INVALID_REASON" => Error(400, "INVALID_REASON"),
                "INVALID_END_DATE" => Error(400, "INVALID_END_DATE"),
                "ADMIN_ROLE_REQUIRED" => Error(403, "ADMIN_ROLE_REQUIRED"),
                "USER_NOT_FOUND" => Error(404, "USER_NOT_FOUND"),
                "ALREADY_SUSPENDED" => Error(409, "ALREADY_SUSPENDED"),
                "ALREADY_ACTIVE" => Error(409, "ALREADY_ACTIVE"),
                "STATE_CONFLICT" => Error(409, "SANCTION_STATE_CONFLICT"),
                _ => Error(500, "INTERNAL_ERROR")
            };
        }

        [HttpGet("cuidadores-pendientes")]
        public async Task<IActionResult> ObtenerCuidadoresPendientes()
        {
            try
            {
                var cuidadores = await _adminService.ObtenerCuidadoresPendientesAsync();
                return Ok(cuidadores);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [HttpGet("cuidadores")]
        public async Task<IActionResult> ObtenerCuidadores([FromQuery] int? estado)
        {
            try
            {
                var cuidadores = await _adminService.ObtenerCuidadoresAdminAsync(estado);
                return Ok(cuidadores);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [HttpGet("cuidadores/{usuarioId}")]
        public async Task<IActionResult> ObtenerCuidadorDetalle(int usuarioId)
        {
            try
            {
                var cuidador = await _adminService.ObtenerCuidadorAdminDetalleAsync(usuarioId);
                if (cuidador == null)
                    return NotFound();

                return Ok(cuidador);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [HttpGet("documentos/{cuidadorId}")]
        public async Task<IActionResult> ObtenerDocumentosPorCuidador(int cuidadorId)
        {
            try
            {
                var documentos = await _adminService.ObtenerDocumentosPorCuidadorAsync(cuidadorId);
                return Ok(documentos);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPut("aprobar-cuidador")]
        public async Task<IActionResult> ActualizarEstadoCuidador([FromBody] ActualizarEstadoCuidadorDto dto)
        {
            try
            {
                var success = await _adminService.ActualizarEstadoCuidadorAsync(dto);
                if (success)
                    return Ok(new { Message = "Estado actualizado correctamente" });
                return BadRequest("No se pudo actualizar el estado. Verifica el ID.");
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPut("cuidadores/{usuarioId}/suspender")]
        public async Task<IActionResult> SuspenderCuidador(int usuarioId, [FromBody] SuspenderCuidadorDto dto)
        {
            var errores = dto.Validar(usuarioId, DateTimeOffset.UtcNow);
            if (errores.Count > 0)
                return ErrorValidacion(errores);

            try
            {
                var resultado = await _adminService.SuspenderUsuarioAsync(usuarioId, dto);
                return TraducirResultado(resultado);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "SuspenderUsuario", usuarioId, dto.AdminId);
            }
        }

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPut("cuidadores/{usuarioId}/reactivar")]
        public async Task<IActionResult> ReactivarCuidador(int usuarioId, [FromBody] ReactivarCuidadorDto dto)
        {
            var errores = dto.Validar(usuarioId);
            if (errores.Count > 0)
                return ErrorValidacion(errores);

            try
            {
                var resultado = await _adminService.ReactivarUsuarioAsync(usuarioId, dto);
                return TraducirResultado(resultado);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "ReactivarUsuario", usuarioId, dto.AdminId);
            }
        }

        [HttpGet("cuidadores/{usuarioId}/sanciones")]
        public async Task<IActionResult> ObtenerSanciones(int usuarioId)
        {
            try
            {
                var sanciones = await _adminService.ObtenerSancionesAsync(usuarioId);
                return Ok(sanciones);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPut("cuidadores/{usuarioId}/info")]
        public async Task<IActionResult> ActualizarInfoCuidador(int usuarioId, [FromBody] ActualizarInfoCuidadorDto dto)
        {
            try
            {
                var success = await _adminService.ActualizarInfoCuidadorAsync(usuarioId, dto);
                if (!success)
                    return BadRequest("No se pudo actualizar. Verifica el ID.");

                return Ok(new { Message = "Información actualizada" });
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [HttpGet("clientes")]
        public async Task<IActionResult> ObtenerClientes([FromQuery] bool? activo)
        {
            try
            {
                var clientes = await _adminService.ObtenerClientesAdminAsync(activo);
                return Ok(clientes);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [HttpGet("clientes/{usuarioId}")]
        public async Task<IActionResult> ObtenerClienteDetalle(int usuarioId)
        {
            try
            {
                var cliente = await _adminService.ObtenerClienteAdminDetalleAsync(usuarioId);
                if (cliente == null)
                    return NotFound();

                return Ok(cliente);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPut("clientes/{usuarioId}/info")]
        public async Task<IActionResult> ActualizarInfoCliente(int usuarioId, [FromBody] ActualizarInfoClienteDto dto)
        {
            try
            {
                var success = await _adminService.ActualizarInfoClienteAsync(usuarioId, dto);
                if (!success)
                    return BadRequest("No se pudo actualizar. Verifica el ID.");

                return Ok(new { Message = "Información actualizada" });
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        // Suspender/reactivar y el historial de sanciones son genéricos (por UsuarioId,
        // sin importar el rol) — se reutiliza la misma lógica que ya usan los Care
        // Partners bajo una ruta equivalente para clientes.
        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPut("clientes/{usuarioId}/suspender")]
        public async Task<IActionResult> SuspenderCliente(int usuarioId, [FromBody] SuspenderCuidadorDto dto)
            => await SuspenderCuidador(usuarioId, dto);

        [Authorize(Policy = PoliticasAdmin.Operaciones)]
        [HttpPut("clientes/{usuarioId}/reactivar")]
        public async Task<IActionResult> ReactivarCliente(int usuarioId, [FromBody] ReactivarCuidadorDto dto)
            => await ReactivarCuidador(usuarioId, dto);

        [HttpGet("clientes/{usuarioId}/sanciones")]
        public async Task<IActionResult> ObtenerSancionesCliente(int usuarioId)
            => await ObtenerSanciones(usuarioId);

        [Authorize(Policy = PoliticasAdmin.SuperAdmin)]
        [HttpPost("administradores")]
        public async Task<IActionResult> CrearAdmin([FromBody] CrearAdminDto dto)
        {
            try
            {
                var (nuevoId, motivo) = await _adminService.CrearAdminAsync(dto);
                if (motivo == "EMAIL_DUPLICADO")
                    return BadRequest("Ya existe una cuenta con ese correo.");
                if (motivo != "OK")
                    return StatusCode(500, "No se pudo crear la cuenta.");

                return Ok(new { UsuarioId = nuevoId });
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [HttpGet("administradores")]
        public async Task<IActionResult> ObtenerAdmins()
        {
            try
            {
                var admins = await _adminService.ObtenerAdminsAsync();
                return Ok(admins);
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }

        [Authorize(Policy = PoliticasAdmin.SuperAdmin)]
        [HttpPut("administradores/{usuarioId}/suspender")]
        public async Task<IActionResult> SuspenderAdmin(int usuarioId, [FromBody] SuspenderCuidadorDto dto)
            => await SuspenderCuidador(usuarioId, dto);

        [Authorize(Policy = PoliticasAdmin.SuperAdmin)]
        [HttpPut("administradores/{usuarioId}/reactivar")]
        public async Task<IActionResult> ReactivarAdmin(int usuarioId, [FromBody] ReactivarCuidadorDto dto)
            => await ReactivarCuidador(usuarioId, dto);

        [Authorize(Policy = PoliticasAdmin.Finanzas)]
        [HttpPut("marcar-pago-pagado/{pagoId}")]
        public async Task<IActionResult> MarcarPagoComoPagado(int pagoId)
        {
            try
            {
                var success = await _adminService.MarcarPagoComoPagadoAsync(pagoId);
                if (success)
                    return Ok(new { Message = "Pago marcado como pagado" });
                return BadRequest("No se pudo actualizar el pago. Verifica el ID.");
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "AdminOperacion");
            }
        }
    }
}
