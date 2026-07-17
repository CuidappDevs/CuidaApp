using CUIDAPP_API.DTOs.Admin;

namespace CUIDAPP_API.Interfaces.Admin
{
    public interface IAdminService
    {
        Task<IEnumerable<CuidadorPendienteDto>> ObtenerCuidadoresPendientesAsync();
        Task<IEnumerable<DocumentoDto>> ObtenerDocumentosPorCuidadorAsync(int cuidadorId);
        Task<bool> ActualizarEstadoCuidadorAsync(ActualizarEstadoCuidadorDto dto);
    }
}
