namespace CUIDAPP_API.DTOs.Auth
{
    public enum LoginStatus
    {
        Success,
        InvalidCredentials,
        AccountSuspended,
        AccountInactive,
        AccountDeleting
    }

    public record LoginResult(
        LoginStatus Status,
        AuthResponseDto? AuthResponse = null,
        SuspensionInfoDto? Suspension = null,
        DateTime? FechaEjecucion = null,
        int? UsuarioId = null);
}
