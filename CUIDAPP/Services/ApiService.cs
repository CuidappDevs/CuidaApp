using System.Net.Http.Json;
using CUIDAPP.Models.Auth;
using CUIDAPP.Models.Busqueda;
using CUIDAPP.Models.Cliente;
using CUIDAPP.Models.Cuidador;
using CUIDAPP.Models.Trabajo;
using CUIDAPP.Models.Upload;

namespace CUIDAPP.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        // La URL cambia sola según la configuración de compilación: en Debug apunta a la
        // API local de desarrollo, en Release al servidor de producción. Así no hay que
        // tocar este archivo al publicar — solo compilar en modo Release.
        //
        // En Debug: el emulador de Android tiene su propia red virtual donde "localhost"
        // apunta al propio emulador, no a la PC. 10.0.2.2 es el alias especial que el
        // emulador usa para llegar a la máquina host. En Windows/iOS/dispositivo físico
        // se usa la IP real de la PC.
        //
        // IMPORTANTE (local): la API debe correr con el perfil "http" (puerto 5258), no
        // "IIS Express" (puerto 44352) — en Visual Studio, cambia el perfil en el
        // desplegable junto al botón de Iniciar.
        private static readonly string BaseUrl =
#if DEBUG
#if ANDROID
            "http://10.0.2.2:5258/api/";
#else
            "http://localhost:5258/api/";
#endif
#else
            "http://192.169.179.217/api/";
#endif

        // Origen del servidor (sin /api/) para resolver rutas relativas de archivos, ej. "/uploads/...".
        public static string ServerOrigin => BaseUrl.Substring(0, BaseUrl.IndexOf("/api/", StringComparison.Ordinal));

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

        public async Task<string?> UploadFileAsync(string localFilePath, string carpeta)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var bytes = await File.ReadAllBytesAsync(localFilePath);
                var fileContent = new ByteArrayContent(bytes);
                content.Add(fileContent, "file", Path.GetFileName(localFilePath));
                content.Add(new StringContent(carpeta), "carpeta");

                var response = await _httpClient.PostAsync("upload", content);
                if (!response.IsSuccessStatusCode)
                    return null;

                var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
                return result?.Url;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error subiendo archivo: {ex.Message}");
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

        public async Task<EstadoVerificacion?> ObtenerEstadoVerificacionAsync(int cuidadorId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"cuidador/estado-verificacion/{cuidadorId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<EstadoVerificacion>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo estado de verificación: {ex.Message}");
                return null;
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

        public async Task<PerfilCuidador?> ObtenerPerfilCuidadorAsync(int cuidadorId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"cuidador/perfil/{cuidadorId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<PerfilCuidador>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo perfil: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ActualizarDisponibilidadAsync(int cuidadorId, bool disponible)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("cuidador/disponibilidad", new { CuidadorId = cuidadorId, Disponible = disponible });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando disponibilidad: {ex.Message}");
                return false;
            }
        }

        public async Task<Ganancias?> ObtenerGananciasAsync(int cuidadorId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"cuidador/ganancias/{cuidadorId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<Ganancias>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo ganancias: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Pago>> ObtenerPagosAsync(int cuidadorId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"cuidador/pagos/{cuidadorId}");
                if (!response.IsSuccessStatusCode)
                    return new List<Pago>();

                return await response.Content.ReadFromJsonAsync<List<Pago>>() ?? new List<Pago>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo pagos: {ex.Message}");
                return new List<Pago>();
            }
        }

        public async Task<Trabajo?> ObtenerProximoTrabajoAsync(int cuidadorId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"trabajo/proximo/{cuidadorId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<Trabajo>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo próximo trabajo: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Trabajo>> ObtenerTrabajosAsync(int cuidadorId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"trabajo/cuidador/{cuidadorId}");
                if (!response.IsSuccessStatusCode)
                    return new List<Trabajo>();

                return await response.Content.ReadFromJsonAsync<List<Trabajo>>() ?? new List<Trabajo>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo trabajos: {ex.Message}");
                return new List<Trabajo>();
            }
        }

        public async Task<List<ServicioCercano>> ObtenerServiciosCercanosAsync(double lat, double lng, double radioKm = 15)
        {
            try
            {
                var url = $"busqueda/servicios-cercanos?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lng={lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}&radioKm={radioKm.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<ServicioCercano>();

                return await response.Content.ReadFromJsonAsync<List<ServicioCercano>>() ?? new List<ServicioCercano>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo servicios cercanos: {ex.Message}");
                return new List<ServicioCercano>();
            }
        }

        public async Task<List<CuidadorCercano>> ObtenerCuidadoresPorServicioAsync(string especialidad, double lat, double lng, double radioKm = 15)
        {
            try
            {
                var especialidadCodificada = Uri.EscapeDataString(especialidad);
                var url = $"busqueda/cuidadores?especialidad={especialidadCodificada}&lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lng={lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}&radioKm={radioKm.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<CuidadorCercano>();

                return await response.Content.ReadFromJsonAsync<List<CuidadorCercano>>() ?? new List<CuidadorCercano>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo cuidadores por servicio: {ex.Message}");
                return new List<CuidadorCercano>();
            }
        }

        public async Task<PerfilCliente?> ObtenerPerfilClienteAsync(int clienteId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"cliente/perfil/{clienteId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<PerfilCliente>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo perfil de cliente: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> CrearTrabajoAsync(CrearTrabajoRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("trabajo", request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creando trabajo: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ActualizarEstadoTrabajoAsync(int trabajoId, int nuevoEstado)
        {
            try
            {
                var request = new ActualizarEstadoTrabajoRequest { TrabajoId = trabajoId, NuevoEstado = nuevoEstado };
                var response = await _httpClient.PutAsJsonAsync("trabajo/estado", request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando estado de trabajo: {ex.Message}");
                return false;
            }
        }
    }
}
