using System.Net.Http.Json;
using CUIDAPP_ADMINISTRATIVO.Models.Auth;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    public class AdminLoginResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public AuthResponse? Data { get; set; }
    }

    public class AdminAuthService
    {
        private const int RolAdmin = 1;
        private readonly HttpClient _httpClient;

        public AdminAuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<AdminLoginResult> LoginAsync(string email, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("auth/login", new LoginRequest { Email = email, Password = password });

                if (!response.IsSuccessStatusCode)
                    return new AdminLoginResult { Success = false, ErrorMessage = "Correo o contraseña incorrectos." };

                var data = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (data == null)
                    return new AdminLoginResult { Success = false, ErrorMessage = "Respuesta inválida del servidor." };

                if (data.RolId != RolAdmin)
                    return new AdminLoginResult { Success = false, ErrorMessage = "No tienes permisos para acceder al panel administrativo." };

                return new AdminLoginResult { Success = true, Data = data };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en login administrativo: {ex.Message}");
                return new AdminLoginResult { Success = false, ErrorMessage = "No se pudo conectar con el servidor. Intenta de nuevo." };
            }
        }
    }
}
