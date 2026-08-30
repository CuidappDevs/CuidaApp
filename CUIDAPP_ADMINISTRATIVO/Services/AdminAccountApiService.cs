using System.Net.Http.Json;
using CUIDAPP_ADMINISTRATIVO.Models.Admin;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    public class CrearAdminResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class AdminAccountApiService
    {
        private readonly HttpClient _httpClient;

        public AdminAccountApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<AdminUsuario>> ObtenerAdminsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("admin/administradores");
                if (!response.IsSuccessStatusCode)
                    return new List<AdminUsuario>();

                return await response.Content.ReadFromJsonAsync<List<AdminUsuario>>() ?? new List<AdminUsuario>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo administradores: {ex.Message}");
                return new List<AdminUsuario>();
            }
        }

        public async Task<CrearAdminResult> CrearAdminAsync(string email, string password, string nombreCompleto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("admin/administradores", new { Email = email, Password = password, NombreCompleto = nombreCompleto });
                if (response.IsSuccessStatusCode)
                    return new CrearAdminResult { Success = true };

                var mensaje = await response.Content.ReadAsStringAsync();
                return new CrearAdminResult { Success = false, ErrorMessage = string.IsNullOrWhiteSpace(mensaje) ? "No se pudo crear la cuenta." : mensaje };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creando administrador: {ex.Message}");
                return new CrearAdminResult { Success = false, ErrorMessage = "No se pudo conectar con el servidor." };
            }
        }

        public async Task<bool> SuspenderAsync(int usuarioId, int adminId, string motivo)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"admin/administradores/{usuarioId}/suspender", new { AdminId = adminId, Motivo = motivo });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error suspendiendo administrador: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReactivarAsync(int usuarioId, int adminId)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"admin/administradores/{usuarioId}/reactivar", new { AdminId = adminId });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reactivando administrador: {ex.Message}");
                return false;
            }
        }
    }
}
