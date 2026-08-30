using System.Net.Http.Json;
using CUIDAPP_ADMINISTRATIVO.Models.Cuidador;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    public class CuidadorAdminApiService
    {
        private readonly HttpClient _httpClient;

        public CuidadorAdminApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Origen del servidor (sin /api/), para armar la URL completa de los documentos
        // (que la API devuelve como ruta relativa, ej. "/uploads/...").
        public string ServerOrigin => _httpClient.BaseAddress is { } uri
            ? $"{uri.Scheme}://{uri.Authority}"
            : "";

        public async Task<List<CuidadorAdmin>> ObtenerCuidadoresAsync(int? estado)
        {
            try
            {
                var url = estado.HasValue ? $"admin/cuidadores?estado={estado}" : "admin/cuidadores";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<CuidadorAdmin>();

                return await response.Content.ReadFromJsonAsync<List<CuidadorAdmin>>() ?? new List<CuidadorAdmin>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo cuidadores: {ex.Message}");
                return new List<CuidadorAdmin>();
            }
        }

        public async Task<CuidadorAdmin?> ObtenerDetalleAsync(int usuarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"admin/cuidadores/{usuarioId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<CuidadorAdmin>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo detalle de cuidador: {ex.Message}");
                return null;
            }
        }

        public async Task<List<DocumentoAdmin>> ObtenerDocumentosAsync(int cuidadorId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"admin/documentos/{cuidadorId}");
                if (!response.IsSuccessStatusCode)
                    return new List<DocumentoAdmin>();

                return await response.Content.ReadFromJsonAsync<List<DocumentoAdmin>>() ?? new List<DocumentoAdmin>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo documentos: {ex.Message}");
                return new List<DocumentoAdmin>();
            }
        }

        public async Task<bool> ActualizarEstadoAsync(int cuidadorId, int nuevoEstado, string? observaciones)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("admin/aprobar-cuidador", new { CuidadorId = cuidadorId, NuevoEstado = nuevoEstado, Observaciones = observaciones });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando estado del cuidador: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SuspenderAsync(int usuarioId, int adminId, string motivo)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"admin/cuidadores/{usuarioId}/suspender", new { AdminId = adminId, Motivo = motivo });
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
                var response = await _httpClient.PutAsJsonAsync($"admin/cuidadores/{usuarioId}/reactivar", new { AdminId = adminId });
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
                var response = await _httpClient.GetAsync($"admin/cuidadores/{usuarioId}/sanciones");
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

        public async Task<bool> ActualizarInfoAsync(int usuarioId, string nombreCompleto, string especialidad, decimal tarifaHora, string? bio, string? metodoCobro)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync($"admin/cuidadores/{usuarioId}/info", new
                {
                    NombreCompleto = nombreCompleto,
                    Especialidad = especialidad,
                    TarifaHora = tarifaHora,
                    Bio = bio,
                    MetodoCobro = metodoCobro
                });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando información: {ex.Message}");
                return false;
            }
        }
    }
}
