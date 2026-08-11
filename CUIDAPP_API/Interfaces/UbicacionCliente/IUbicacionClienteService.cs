using CUIDAPP_API.DTOs.UbicacionCliente;

namespace CUIDAPP_API.Interfaces.UbicacionCliente
{
    public interface IUbicacionClienteService
    {
        Task<IEnumerable<UbicacionClienteDto>> ObtenerUbicacionesAsync(int clienteId);
        Task<int> CrearUbicacionAsync(CrearUbicacionClienteDto dto);
        Task<bool> ActualizarUbicacionAsync(ActualizarUbicacionClienteDto dto);
        Task<bool> EliminarUbicacionAsync(EliminarUbicacionClienteDto dto);
    }
}
