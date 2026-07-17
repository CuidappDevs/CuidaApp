using CUIDAPP_API.DTOs.Auth;

namespace CUIDAPP_API.Interfaces.Auth
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginDto);
        Task<int> RegisterClientAsync(RegisterClientDto registerDto);
        Task<int> RegisterCaregiverAsync(RegisterCaregiverDto registerDto);
    }
}
