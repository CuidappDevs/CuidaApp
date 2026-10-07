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

        [HttpPost("register/cliente")]
        public async Task<IActionResult> RegisterClient([FromBody] RegisterClientDto registerDto)
        {
            try
            {
                var newUserId = await _authService.RegisterClientAsync(registerDto);
                return Ok(new { Message = "Cliente registrado con éxito", UserId = newUserId });
            }
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
