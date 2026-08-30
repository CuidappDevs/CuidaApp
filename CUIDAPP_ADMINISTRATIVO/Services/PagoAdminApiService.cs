using System.Net.Http.Json;
using CUIDAPP_ADMINISTRATIVO.Models.Pago;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    public class PagoAdminApiService
    {
        private readonly HttpClient _httpClient;

        public PagoAdminApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<PagoAdmin>> ObtenerPagosAsync(int? estado)
        {
            try
            {
                var url = estado.HasValue ? $"pagoadmin?estado={estado}" : "pagoadmin";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<PagoAdmin>();

                return await response.Content.ReadFromJsonAsync<List<PagoAdmin>>() ?? new List<PagoAdmin>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo pagos: {ex.Message}");
                return new List<PagoAdmin>();
            }
        }

        public async Task<bool> AutorizarPagoAsync(int pagoId, int adminId)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"pagoadmin/{pagoId}/autorizar", new { AdminId = adminId });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error autorizando pago: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AprobarPagoAsync(int pagoId, int adminId)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"pagoadmin/{pagoId}/aprobar", new { AdminId = adminId });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error aprobando pago: {ex.Message}");
                return false;
            }
        }
    }
}
