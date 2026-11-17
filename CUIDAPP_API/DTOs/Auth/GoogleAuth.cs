namespace CUIDAPP_API.DTOs.Auth;

public record GoogleIdentidad(string Sub, string Email, bool EmailVerified, string? Nombre, string? FotoUrl);
public record GoogleLoginDto(string IdToken);
public class GoogleAuthException(string motivo, int statusCode, string message, GoogleIdentidad? identidad = null) : Exception(message)
{
    public string Motivo { get; } = motivo;
    public int StatusCode { get; } = statusCode;
    public GoogleIdentidad? Identidad { get; } = identidad;
}
