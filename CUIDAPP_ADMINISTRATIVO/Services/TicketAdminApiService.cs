using System.Net.Http.Json;
using CUIDAPP_ADMINISTRATIVO.Models.Ticket;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    public class TicketAdminApiService
    {
        private readonly HttpClient _httpClient;

        public TicketAdminApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<TicketAdmin>> ObtenerTicketsAsync(int? estado)
        {
            try
            {
                var url = estado.HasValue ? $"ticket?estado={estado}" : "ticket";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<TicketAdmin>();

                return await response.Content.ReadFromJsonAsync<List<TicketAdmin>>() ?? new List<TicketAdmin>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo tickets: {ex.Message}");
                return new List<TicketAdmin>();
            }
        }

        public async Task<TicketDetalle?> ObtenerDetalleAsync(int ticketId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"ticket/{ticketId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<TicketDetalle>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo detalle de ticket: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> AgregarMensajeAsync(int ticketId, int adminId, string mensaje)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"ticket/{ticketId}/mensaje", new { AutorId = adminId, EsAdmin = true, Mensaje = mensaje });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error agregando mensaje: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ActualizarEstadoAsync(int ticketId, int estado, int adminId)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"ticket/{ticketId}/estado", new { Estado = estado, AdminId = adminId });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando estado: {ex.Message}");
                return false;
            }
        }
    }
}
