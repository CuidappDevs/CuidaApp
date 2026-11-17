using CUIDAPP_API.DTOs.Auth;
namespace CUIDAPP_API.Interfaces.Auth;
public interface IValidadorGoogle
{
    Task<GoogleIdentidad> ValidarAsync(string idToken);
}
