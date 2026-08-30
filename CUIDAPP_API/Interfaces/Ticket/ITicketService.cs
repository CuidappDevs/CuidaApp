using CUIDAPP_API.DTOs.Ticket;

namespace CUIDAPP_API.Interfaces.Ticket
{
    public interface ITicketService
    {
        Task<int> CrearTicketAsync(CrearTicketDto dto);
        Task<List<TicketDto>> ObtenerTicketsPorUsuarioAsync(int usuarioId);
        Task<List<TicketAdminDto>> ObtenerTicketsAdminAsync(int? estado);
        Task<TicketAdminDto?> ObtenerDetalleAsync(int ticketId);
        Task<List<TicketMensajeDto>> ObtenerMensajesAsync(int ticketId);
        Task<bool> AgregarMensajeAsync(int ticketId, CrearMensajeTicketDto dto);
        Task<bool> ActualizarEstadoAsync(int ticketId, ActualizarEstadoTicketDto dto);
    }
}
