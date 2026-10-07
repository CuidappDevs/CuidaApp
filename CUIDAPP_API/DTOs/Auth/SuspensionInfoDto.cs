namespace CUIDAPP_API.DTOs.Auth
{
    public record SuspensionInfoDto(string Motivo, string Tipo, DateTimeOffset? FechaFinUtc);
}
