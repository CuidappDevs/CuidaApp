namespace CUIDAPP_API.DTOs.Auth
{
    public enum LoginStatus
    {
        Success,
        InvalidCredentials,
        AccountSuspended,
        AccountInactive
    }

    public record LoginResult(
        LoginStatus Status,
        AuthResponseDto? AuthResponse = null,
        SuspensionInfoDto? Suspension = null);
}
