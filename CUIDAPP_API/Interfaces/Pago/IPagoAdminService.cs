using CUIDAPP_API.DTOs.Pago;

namespace CUIDAPP_API.Interfaces.Pago
{
    public interface IPagoAdminService
    {
        Task<List<PagoAdminDto>> ObtenerPagosAsync(int? estado);
        Task<bool> AutorizarPagoAsync(int pagoId, int adminId);
        Task<bool> AprobarPagoAsync(int pagoId, int adminId);
    }
}
