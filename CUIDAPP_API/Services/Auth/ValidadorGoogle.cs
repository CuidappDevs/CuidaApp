using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.Interfaces.Auth;
using Google.Apis.Auth;
namespace CUIDAPP_API.Services.Auth;

public class ValidadorGoogle(IConfiguration config) : IValidadorGoogle
{
    public async Task<GoogleIdentidad> ValidarAsync(string idToken)
    {
        var clientId = config["Google:ClientIdWeb"];
        if (string.IsNullOrWhiteSpace(clientId)) throw new InvalidOperationException("Falta Google:ClientIdWeb.");
        try
        {
            if (string.IsNullOrWhiteSpace(idToken)) throw new InvalidJwtException("Token vacío.");
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { clientId } });
            if (string.IsNullOrWhiteSpace(payload.Subject) || payload.Subject.Length > 64 || string.IsNullOrWhiteSpace(payload.Email))
                throw new InvalidJwtException("Identidad incompleta.");
            return new(payload.Subject, payload.Email, payload.EmailVerified, payload.Name, payload.Picture);
        }
        catch (InvalidJwtException)
        {
            throw new GoogleAuthException("GOOGLE_TOKEN_INVALIDO", 401, "El token de Google no es válido o ha expirado.");
        }
    }
}
