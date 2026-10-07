using CUIDAPP_API.DTOs.Auth;

namespace CUIDAPP_API.DTOs.Common
{
    public record ApiErrorDto(
        string Code,
        string Message,
        IDictionary<string, string[]>? Errors = null,
        SuspensionInfoDto? Suspension = null);
}
