using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.Interfaces.Auth;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        // Nunca expone ex.Message ni texto SQL; el detalle va al log estructurado.
        private IActionResult ErrorInterno(Exception ex, string operacion)
        {
            _logger.LogError(ex, "Error interno en {Operacion}", operacion);
            return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
        }

        public static IActionResult TraducirLogin(LoginResult result) => result.Status switch
        {
            LoginStatus.Success => new OkObjectResult(result.AuthResponse),
            LoginStatus.AccountDeleting => new ObjectResult(new {
                motivo = "CUENTA_EN_ELIMINACION", fechaEjecucion = result.FechaEjecucion, usuarioId = result.UsuarioId
            }) { StatusCode = 403 },
            LoginStatus.AccountSuspended => new ObjectResult(new ApiErrorDto(
                "ACCOUNT_SUSPENDED", "Tu cuenta está suspendida.", null, result.Suspension)) { StatusCode = 423 },
            LoginStatus.AccountInactive => new ObjectResult(new ApiErrorDto(
                "ACCOUNT_INACTIVE", "Tu cuenta no está activa.")) { StatusCode = 423 },
            _ => new ObjectResult(new ApiErrorDto(
                "INVALID_CREDENTIALS", "Credenciales inválidas.")) { StatusCode = 401 }
        };

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginDto)
        {
            if (loginDto == null || string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrEmpty(loginDto.Password))
            {
                return BadRequest(new ApiErrorDto("VALIDATION_ERROR", "Hay datos inválidos en la solicitud.",
                    new Dictionary<string, string[]> { ["credenciales"] = new[] { "Correo y contraseña son obligatorios." } }));
            }

            try
            {
                return TraducirLogin(await _authService.LoginAsync(loginDto));
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "Login");
            }
        }

        [HttpPost("google")]
        public async Task<IActionResult> Google([FromBody] GoogleLoginDto dto)
        {
            try { return TraducirLogin(await _authService.LoginGoogleAsync(dto?.IdToken ?? "")); }
            catch (TerminosRegistroException ex) { return StatusCode(ex.StatusCode, new { motivo = ex.Motivo, message = ex.Message }); }
            catch (GoogleAuthException ex) { return ErrorGoogle(ex); }
            catch (Exception ex) { return ErrorInterno(ex, "Google"); }
        }

        private IActionResult ErrorGoogle(GoogleAuthException ex) => new ObjectResult(ex.Identidad == null
            ? (object)new { motivo = ex.Motivo, message = ex.Message }
            : new { motivo = ex.Motivo, email = ex.Identidad.Email, nombre = ex.Identidad.Nombre, fotoUrl = ex.Identidad.FotoUrl })
            { StatusCode = ex.StatusCode };

        public record RenovarTokenDto(string Token);

        /// <summary>La app renueva su token al abrirse, para no tener que iniciar sesión otra vez.</summary>
        [HttpPost("renovar-token")]
        public IActionResult RenovarToken([FromBody] RenovarTokenDto dto)
        {
            var nuevo = _authService.RenovarToken(dto?.Token ?? "");
            if (nuevo == null)
                return Unauthorized(new ApiErrorDto("TOKEN_INVALIDO", "No se pudo renovar la sesión."));
            return Ok(new { Token = nuevo });
        }

        [HttpPost("register/cliente")]
        public async Task<IActionResult> RegisterClient([FromBody] RegisterClientDto registerDto)
        {
            try
            {
                var newUserId = await _authService.RegisterClientAsync(registerDto);
                return Ok(new { Message = "Cliente registrado con éxito", UserId = newUserId });
            }
            catch (TerminosRegistroException ex) { return StatusCode(ex.StatusCode, new { motivo = ex.Motivo, message = ex.Message }); }
            catch (GoogleAuthException ex) { return ErrorGoogle(ex); }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "Registro");
            }
        }

        [HttpPost("register/cuidador")]
        public async Task<IActionResult> RegisterCaregiver([FromBody] RegisterCaregiverDto registerDto)
        {
            try
            {
                var newUserId = await _authService.RegisterCaregiverAsync(registerDto);
                return Ok(new { Message = "Cuidador registrado con éxito", UserId = newUserId });
            }
            catch (MigratorioException ex)
            {
                if (ex.Code == "ESTATUS_MIGRATORIO_INVALIDO") return BadRequest(new { motivo = ex.Code, message = "Ese estatus migratorio no está disponible." });
                if (ex.Code == "ESTATUS_MIGRATORIO_REQUERIDO") return BadRequest(new { code = ex.Code });
                return BadRequest(new { code = ex.Code, faltantes = ex.Faltantes });
            }
            catch (TestRequeridoException ex)
            {
                return Conflict(new ApiErrorDto("TEST_REQUERIDO", ex.Message));
            }
            catch (InvitacionInvalidaException ex)
            {
                return Conflict(new ApiErrorDto("INVITACION_INVALIDA", ex.Message));
            }
            catch (TerminosRegistroException ex) { return StatusCode(ex.StatusCode, new { motivo = ex.Motivo, message = ex.Message }); }
            catch (GoogleAuthException ex) { return ErrorGoogle(ex); }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "Registro");
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            try
            {
                var (success, resetToken, message) = await _authService.ForgotPasswordAsync(dto);
                if (!success)
                    return BadRequest(new { Message = message });

                return Ok(new { Message = "Código enviado", ResetToken = resetToken, Code = message });
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "Auth");
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            try
            {
                var (success, message) = await _authService.ResetPasswordAsync(dto);
                if (!success)
                    return BadRequest(new { Message = message });

                return Ok(new { Message = message });
            }
            catch (Exception ex)
            {
                return ErrorInterno(ex, "Auth");
            }
        }
    }
}
