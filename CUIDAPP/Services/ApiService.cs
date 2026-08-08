using System.Net.Http.Json;
using CUIDAPP.Models.Auth;

namespace CUIDAPP.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        
        // API alojada en el servidor remoto IIS (con / al final obligatoria para HttpClient)
        private const string BaseUrl = "http://192.169.179.217/api/";

        public ApiService()
        {
            // Omitir validación de certificados SSL para desarrollo local
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };
            
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl)
            };
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("auth/login", request);
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<AuthResponse>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en login: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> RegisterClienteAsync(RegisterClientRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("auth/register/cliente", request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registrando cliente: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RegisterCuidadorAsync(RegisterCaregiverRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("auth/register/cuidador", request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registrando cuidador: {ex.Message}");
                return false;
            }
        }
    }
}
