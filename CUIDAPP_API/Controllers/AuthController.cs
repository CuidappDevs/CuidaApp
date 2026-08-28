using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.Interfaces.Auth;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginDto)
        {
            try
            {
                var result = await _authService.LoginAsync(loginDto);
                if (result == null)
                    return Unauthorized("Credenciales inválidas o cuenta inactiva.");

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                return StatusCode(500, $"Error al registrar: {ex.Message}");
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
                return StatusCode(500, $"Error al registrar: {ex.Message}");
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
                return StatusCode(500, $"Error interno: {ex.Message}");
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
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }
    }
}
