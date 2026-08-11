using CUIDAPP_API.DTOs.Trabajo;

namespace CUIDAPP_API.Interfaces.Trabajo
{
    public interface ITrabajoService
    {
        Task<int> CrearTrabajoAsync(CrearTrabajoDto dto);
        Task<IEnumerable<TrabajoDto>> ObtenerTrabajosPorCuidadorAsync(int cuidadorId);
        Task<TrabajoDto?> ObtenerProximoTrabajoAsync(int cuidadorId);
        Task<bool> ActualizarEstadoTrabajoAsync(ActualizarEstadoTrabajoDto dto);
        Task<TrabajoClienteDto?> ObtenerTrabajoActivoPorClienteAsync(int clienteId);
        Task<(bool Success, string Motivo)> IniciarTrabajoAsync(IniciarTrabajoDto dto);
        Task<IEnumerable<MotivoCancelacionDto>> ObtenerMotivosCancelacionAsync();
        Task<bool> CancelarTrabajoCuidadorAsync(CancelarTrabajoDto dto);
    }
}
