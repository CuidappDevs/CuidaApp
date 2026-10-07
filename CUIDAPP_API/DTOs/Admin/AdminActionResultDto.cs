namespace CUIDAPP_API.DTOs.Admin
{
    // Resultado de negocio de suspender/reactivar. Code es el código estable de dominio.
    public record AdminActionResult(
        string Code,
        string Message,
        int UsuarioId,
        int? SancionId,
        bool? IsActive,
        string? EstadoSancion,
        DateTimeOffset? FechaFinUtc);
}
