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
        Task<IEnumerable<TrabajoClienteDto>> ObtenerTrabajosActivosPorClienteAsync(int clienteId);
        Task<TrabajoClienteDto?> ObtenerTrabajoPorIdAsync(int trabajoId);
        Task<(bool Success, string Motivo)> IniciarTrabajoAsync(IniciarTrabajoDto dto);
        Task<(bool Success, string Motivo)> FinalizarTrabajoAsync(FinalizarTrabajoDto dto);
        Task<(bool Success, string Motivo)> ConfirmarFinalizacionAsync(int trabajoId, int clienteId, bool confirmado);
        Task<(bool Success, string Motivo)> ForzarFinalizacionAsync(int trabajoId, int cuidadorId);
        Task<IEnumerable<MotivoCancelacionDto>> ObtenerMotivosCancelacionAsync();
        Task<bool> CancelarTrabajoCuidadorAsync(CancelarTrabajoDto dto);
        Task<IEnumerable<ActividadTrabajoDto>> ObtenerActividadesAsync(int trabajoId);
        Task<ActividadTrabajoDto> AgregarActividadAsync(AgregarActividadTrabajoDto dto);
        Task AlertarGeocercaAsync(int trabajoId, double distanciaMetros);
    }
}
