using CUIDAPP_API.DTOs.Auth;

namespace CUIDAPP_API.Interfaces.Auth
{
    public interface IAuthService
    {
        Task<LoginResult> LoginAsync(LoginRequestDto loginDto);
        Task<LoginResult> LoginGoogleAsync(string idToken);
        string? RenovarToken(string token);
        Task<int> RegisterClientAsync(RegisterClientDto registerDto);
        Task<int> RegisterCaregiverAsync(RegisterCaregiverDto registerDto);
        Task<(bool Success, Guid ResetToken, string Message)> ForgotPasswordAsync(ForgotPasswordDto dto);
        Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto dto);
    }
}
