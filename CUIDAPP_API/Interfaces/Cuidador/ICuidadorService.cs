using CUIDAPP_API.DTOs.Cuidador;

namespace CUIDAPP_API.Interfaces.Cuidador
{
    public interface ICuidadorService
    {
        Task<int> SubirDocumentoAsync(SubirDocumentoDto dto);
        Task<EstadoVerificacionDto> ObtenerEstadoVerificacionAsync(int cuidadorId);
        Task<PerfilCuidadorDto?> ObtenerPerfilAsync(int cuidadorId);
        Task<bool> ActualizarDisponibilidadAsync(ActualizarDisponibilidadDto dto);
        Task<bool> ActualizarUbicacionAsync(ActualizarUbicacionDto dto);
        Task<GananciasDto> ObtenerGananciasAsync(int cuidadorId);
        Task<IEnumerable<PagoDto>> ObtenerPagosAsync(int cuidadorId);
    }
}
