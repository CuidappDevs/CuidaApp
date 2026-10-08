using CUIDAPP_API.DTOs.Cuidador;

namespace CUIDAPP_API.Interfaces.Cuidador
{
    public interface ICuidadorService
    {
        Task<int> SubirDocumentoAsync(SubirDocumentoDto dto);
        /// <summary>Reemplaza un documento rechazado (vuelve a pendiente). False si no es del cuidador o no está rechazado.</summary>
        Task<bool> ReemplazarDocumentoAsync(int documentoId, ReemplazarDocumentoDto dto);
        Task<EstadoVerificacionDto> ObtenerEstadoVerificacionAsync(int cuidadorId);
        Task<PerfilCuidadorDto?> ObtenerPerfilAsync(int cuidadorId);
        Task<bool> ActualizarDisponibilidadAsync(ActualizarDisponibilidadDto dto);
        Task<HorarioCuidadorDto?> ObtenerHorarioAsync(int cuidadorId);
        /// <summary>Devuelve "OK" o un código: NO_ENCONTRADO, FRANJA_INVALIDA, FRANJAS_SOLAPADAS, SIN_FRANJAS.</summary>
        Task<string> GuardarHorarioAsync(int cuidadorId, HorarioCuidadorDto dto);
        Task<bool> ActualizarUbicacionAsync(ActualizarUbicacionDto dto);
        Task<GananciasDto> ObtenerGananciasAsync(int cuidadorId);
        Task<IEnumerable<PagoDto>> ObtenerPagosAsync(int cuidadorId);
        Task<ContactoEmergenciaDto?> ObtenerContactoEmergenciaAsync(int cuidadorId);
        Task<bool> GuardarContactoEmergenciaAsync(int cuidadorId, ContactoEmergenciaDto dto);
    }
}
