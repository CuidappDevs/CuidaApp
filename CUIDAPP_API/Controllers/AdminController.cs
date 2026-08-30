using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Admin;
using CUIDAPP_API.Interfaces.Admin;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
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
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

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
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("cuidadores/{usuarioId}/suspender")]
        public async Task<IActionResult> SuspenderCuidador(int usuarioId, [FromBody] SuspenderCuidadorDto dto)
        {
            try
            {
                var success = await _adminService.SuspenderCuidadorAsync(usuarioId, dto);
                if (!success)
                    return BadRequest("No se pudo suspender. Verifica el ID.");

                return Ok(new { Message = "Cuenta suspendida" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("cuidadores/{usuarioId}/reactivar")]
        public async Task<IActionResult> ReactivarCuidador(int usuarioId, [FromBody] ReactivarCuidadorDto dto)
        {
            try
            {
                var success = await _adminService.ReactivarCuidadorAsync(usuarioId, dto);
                if (!success)
                    return BadRequest("No se pudo reactivar. Verifica el ID.");

                return Ok(new { Message = "Cuenta reactivada" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

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
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

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
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        // Suspender/reactivar y el historial de sanciones son genéricos (por UsuarioId,
        // sin importar el rol) — se reutiliza la misma lógica que ya usan los Care
        // Partners bajo una ruta equivalente para clientes.
        [HttpPut("clientes/{usuarioId}/suspender")]
        public async Task<IActionResult> SuspenderCliente(int usuarioId, [FromBody] SuspenderCuidadorDto dto)
            => await SuspenderCuidador(usuarioId, dto);

        [HttpPut("clientes/{usuarioId}/reactivar")]
        public async Task<IActionResult> ReactivarCliente(int usuarioId, [FromBody] ReactivarCuidadorDto dto)
            => await ReactivarCuidador(usuarioId, dto);

        [HttpGet("clientes/{usuarioId}/sanciones")]
        public async Task<IActionResult> ObtenerSancionesCliente(int usuarioId)
            => await ObtenerSanciones(usuarioId);

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
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("administradores/{usuarioId}/suspender")]
        public async Task<IActionResult> SuspenderAdmin(int usuarioId, [FromBody] SuspenderCuidadorDto dto)
            => await SuspenderCuidador(usuarioId, dto);

        [HttpPut("administradores/{usuarioId}/reactivar")]
        public async Task<IActionResult> ReactivarAdmin(int usuarioId, [FromBody] ReactivarCuidadorDto dto)
            => await ReactivarCuidador(usuarioId, dto);

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
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
