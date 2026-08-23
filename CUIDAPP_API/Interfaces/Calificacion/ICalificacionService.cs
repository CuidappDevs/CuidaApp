using CUIDAPP_API.DTOs.Calificacion;

namespace CUIDAPP_API.Interfaces.Calificacion
{
    public interface ICalificacionService
    {
        Task<bool> CrearCalificacionAsync(CrearCalificacionDto dto);
        Task<CalificacionPromedioDto> ObtenerPromedioAsync(int usuarioId);
        Task<bool> ExisteCalificacionAsync(int trabajoId, int calificadorId);
        Task<CalificacionDto?> ObtenerCalificacionDeTrabajoAsync(int trabajoId, int calificadorId);
    }
}
