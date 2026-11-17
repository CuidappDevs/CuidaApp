using CUIDAPP_API.DTOs.AdminOperaciones;

namespace CUIDAPP_API.Interfaces.Cuenta;

public interface IEliminarCuentaService
{
    Task<List<List<Fila>>> RevisarAsync(int usuarioId);
    Task<int?> ValidarCredencialesAsync(string correo, string contrasena);
    Task<Fila?> EnviarCodigoAsync(int usuarioId, string origen);
    Task<Fila?> ConfirmarAsync(int usuarioId, string codigo, string? motivo, string origen);
    Task<Fila?> CancelarAsync(int? usuarioId, int? solicitudId, string por, string? nota);
}
