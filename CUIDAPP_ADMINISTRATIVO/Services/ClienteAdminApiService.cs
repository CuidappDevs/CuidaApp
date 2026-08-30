using System.Net.Http.Json;
using CUIDAPP_ADMINISTRATIVO.Models.Cliente;
using CUIDAPP_ADMINISTRATIVO.Models.Cuidador;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    public class ClienteAdminApiService
    {
        private readonly HttpClient _httpClient;

        public ClienteAdminApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public string ServerOrigin => _httpClient.BaseAddress is { } uri
            ? $"{uri.Scheme}://{uri.Authority}"
            : "";

        public async Task<List<ClienteAdmin>> ObtenerClientesAsync(bool? activo)
        {
            try
            {
                var url = activo.HasValue ? $"admin/clientes?activo={activo}" : "admin/clientes";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<ClienteAdmin>();

                return await response.Content.ReadFromJsonAsync<List<ClienteAdmin>>() ?? new List<ClienteAdmin>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo clientes: {ex.Message}");
                return new List<ClienteAdmin>();
            }
        }

        public async Task<ClienteAdmin?> ObtenerDetalleAsync(int usuarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"admin/clientes/{usuarioId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<ClienteAdmin>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo detalle de cliente: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ActualizarInfoAsync(int usuarioId, string nombreCompleto, string? direccion, string? contactoNombre, string? contactoTelefono)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"admin/clientes/{usuarioId}/info", new
                {
                    NombreCompleto = nombreCompleto,
                    DireccionPrincipal = direccion,
                    ContactoEmergenciaNombre = contactoNombre,
                    ContactoEmergenciaTelefono = contactoTelefono
                });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando información: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SuspenderAsync(int usuarioId, int adminId, string motivo)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"admin/clientes/{usuarioId}/suspender", new { AdminId = adminId, Motivo = motivo });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error suspendiendo cuenta: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReactivarAsync(int usuarioId, int adminId)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"admin/clientes/{usuarioId}/reactivar", new { AdminId = adminId });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reactivando cuenta: {ex.Message}");
                return false;
            }
        }

        public async Task<List<SancionCuidador>> ObtenerSancionesAsync(int usuarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"admin/clientes/{usuarioId}/sanciones");
                if (!response.IsSuccessStatusCode)
                    return new List<SancionCuidador>();

                return await response.Content.ReadFromJsonAsync<List<SancionCuidador>>() ?? new List<SancionCuidador>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo sanciones: {ex.Message}");
                return new List<SancionCuidador>();
            }
        }
    }
}
