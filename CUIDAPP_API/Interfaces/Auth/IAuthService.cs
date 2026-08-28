using CUIDAPP_API.DTOs.Auth;

namespace CUIDAPP_API.Interfaces.Auth
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginDto);
        Task<int> RegisterClientAsync(RegisterClientDto registerDto);
        Task<int> RegisterCaregiverAsync(RegisterCaregiverDto registerDto);
        Task<(bool Success, Guid ResetToken, string Message)> ForgotPasswordAsync(ForgotPasswordDto dto);
        Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto dto);
    }
}
