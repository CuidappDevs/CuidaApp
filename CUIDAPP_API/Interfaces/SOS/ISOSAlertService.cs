using CUIDAPP_API.DTOs.SOS;

namespace CUIDAPP_API.Interfaces.SOS
{
    public interface ISOSAlertService
    {
        Task<SOSAlertaDto?> CrearAlertaAsync(CrearSOSAlertaDto dto);
        Task<IEnumerable<SOSAlertaDto>> ObtenerAlertasPendientesAsync();
        Task<SOSAlertaDto?> ObtenerAlertaPorIdAsync(int id);
        Task<bool> AtenderAlertaAsync(int id, AtenderSOSDto dto);
        Task<bool> DescartarAlertaAsync(int id);
    }
}
