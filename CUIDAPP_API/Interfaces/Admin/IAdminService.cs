using CUIDAPP_API.DTOs.Admin;

namespace CUIDAPP_API.Interfaces.Admin
{
    public interface IAdminService
    {
        Task<IEnumerable<CuidadorPendienteDto>> ObtenerCuidadoresPendientesAsync();
        Task<IEnumerable<CuidadorAdminDto>> ObtenerCuidadoresAdminAsync(int? estado);
        Task<CuidadorAdminDto?> ObtenerCuidadorAdminDetalleAsync(int usuarioId);
        Task<IEnumerable<DocumentoDto>> ObtenerDocumentosPorCuidadorAsync(int cuidadorId);
        Task<bool> ActualizarEstadoCuidadorAsync(ActualizarEstadoCuidadorDto dto);
        Task<bool> MarcarPagoComoPagadoAsync(int pagoId);
        Task<bool> SuspenderCuidadorAsync(int usuarioId, SuspenderCuidadorDto dto);
        Task<bool> ReactivarCuidadorAsync(int usuarioId, ReactivarCuidadorDto dto);
        Task<IEnumerable<SancionCuidadorDto>> ObtenerSancionesAsync(int usuarioId);
        Task<bool> ActualizarInfoCuidadorAsync(int usuarioId, ActualizarInfoCuidadorDto dto);

        Task<IEnumerable<ClienteAdminDto>> ObtenerClientesAdminAsync(bool? activo);
        Task<ClienteAdminDto?> ObtenerClienteAdminDetalleAsync(int usuarioId);
        Task<bool> ActualizarInfoClienteAsync(int usuarioId, ActualizarInfoClienteDto dto);

        Task<(int NuevoId, string Motivo)> CrearAdminAsync(CrearAdminDto dto);
        Task<IEnumerable<AdminUsuarioDto>> ObtenerAdminsAsync();
    }
}
