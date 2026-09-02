using System.Net.Http.Json;
using CUIDAPP.Models.Auth;
using CUIDAPP.Models.Busqueda;
using CUIDAPP.Models.Calificacion;
using CUIDAPP.Models.Chat;
using CUIDAPP.Models.Cliente;
using CUIDAPP.Models.Cuidador;
using CUIDAPP.Models.Ticket;
using CUIDAPP.Models.Trabajo;
using CUIDAPP.Models.Upload;

namespace CUIDAPP.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        // TEMPORAL: apuntando el build Debug directo a producción para probar ahí en vez
        // de la API local, mientras se diagnostica el tiempo real / hora del servidor.
        // Para volver a apuntar a la API local de desarrollo, descomenta el bloque de
        // abajo y comenta esta línea (o viceversa).
        private static readonly string BaseUrl = "http://192.169.179.217/api/";

        // --- Configuración original (Debug = API local, Release = producción) ---
        // En Debug: el emulador de Android tiene su propia red virtual donde "localhost"
        // apunta al propio emulador, no a la PC. 10.0.2.2 es el alias especial que el
        // emulador usa para llegar a la máquina host. En Windows/iOS/dispositivo físico
        // se usa la IP real de la PC.
        //
        // IMPORTANTE (local): la API debe correr con el perfil "http" (puerto 5258), no
        // "IIS Express" — en Visual Studio, cambia el perfil en el desplegable junto al
        // botón de Iniciar, o mejor, corre `dotnet run --launch-profile http` en terminal.
        //
        // private static readonly string BaseUrl =
        //#if DEBUG
        //#if ANDROID
        //    "http://10.0.2.2:5258/api/";
        //#else
        //    "http://localhost:5258/api/";
        //#endif
        //#else
        //    "http://192.169.179.217/api/";
        //#endif

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
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(20)
            };
        }

        public async Task<DateTime?> ObtenerHoraServidorAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("sistema/hora");
                if (!response.IsSuccessStatusCode)
                    return null;

                var resultado = await response.Content.ReadFromJsonAsync<Dictionary<string, DateTime>>();
                return resultado != null && resultado.TryGetValue("horaServidor", out var hora) ? hora : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo hora del servidor: {ex.Message}");
                return null;
            }
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
            var (url, _) = await UploadFileConDiagnosticoAsync(localFilePath, carpeta);
            return url;
        }

        public async Task<(string? Url, string? Error)> UploadFileConDiagnosticoAsync(string localFilePath, string carpeta)
        {
            try
            {
                if (!File.Exists(localFilePath))
                    return (null, $"El archivo no existe en el dispositivo: {localFilePath}");

                using var content = new MultipartFormDataContent();
                var bytes = await File.ReadAllBytesAsync(localFilePath);
                var fileContent = new ByteArrayContent(bytes);
                content.Add(fileContent, "file", Path.GetFileName(localFilePath));
                content.Add(new StringContent(carpeta), "carpeta");

                var response = await _httpClient.PostAsync("upload", content);
                if (!response.IsSuccessStatusCode)
                {
                    var cuerpo = await response.Content.ReadAsStringAsync();
                    return (null, $"HTTP {(int)response.StatusCode}: {cuerpo}");
                }

                var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
                if (string.IsNullOrWhiteSpace(result?.Url))
                    return (null, "El servidor no devolvió una URL de archivo.");

                return (result.Url, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error subiendo archivo: {ex}");
                return (null, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        public async Task<(bool Success, int UserId)> RegisterClienteAsync(RegisterClientRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("auth/register/cliente", request);
                if (!response.IsSuccessStatusCode)
                    return (false, 0);

                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
                var userId = result != null && result.TryGetValue("userId", out var idObj) ? Convert.ToInt32(idObj.ToString()) : 0;
                return (true, userId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registrando cliente: {ex.Message}");
                return (false, 0);
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

        public async Task<List<UbicacionCliente>> ObtenerUbicacionesClienteAsync(int clienteId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"ubicacioncliente/cliente/{clienteId}");
                if (!response.IsSuccessStatusCode)
                    return new List<UbicacionCliente>();

                return await response.Content.ReadFromJsonAsync<List<UbicacionCliente>>() ?? new List<UbicacionCliente>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo ubicaciones del cliente: {ex.Message}");
                return new List<UbicacionCliente>();
            }
        }

        public async Task<(bool Success, int Id)> CrearUbicacionClienteAsync(int clienteId, string nombre, string direccion, decimal latitud, decimal longitud, bool esPredeterminada)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("ubicacioncliente", new { ClienteId = clienteId, Nombre = nombre, Direccion = direccion, Latitud = latitud, Longitud = longitud, EsPredeterminada = esPredeterminada });
                if (!response.IsSuccessStatusCode)
                    return (false, 0);

                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
                return (true, result != null && result.TryGetValue("id", out var id) ? id : 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creando ubicación: {ex.Message}");
                return (false, 0);
            }
        }

        public async Task<bool> ActualizarUbicacionClienteAsync(int id, int clienteId, string nombre, string direccion, decimal latitud, decimal longitud, bool esPredeterminada)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("ubicacioncliente", new { Id = id, ClienteId = clienteId, Nombre = nombre, Direccion = direccion, Latitud = latitud, Longitud = longitud, EsPredeterminada = esPredeterminada });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando ubicación: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EliminarUbicacionClienteAsync(int id, int clienteId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"ubicacioncliente/{id}/cliente/{clienteId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error eliminando ubicación: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ActualizarUbicacionCuidadorAsync(int cuidadorId, double latitud, double longitud)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("cuidador/ubicacion", new { CuidadorId = cuidadorId, Latitud = latitud, Longitud = longitud });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando ubicación del cuidador: {ex.Message}");
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

        public async Task<List<CuidadorMapa>> ObtenerCuidadoresCercanosMapaAsync(double lat, double lng, double radioKm = 15)
        {
            try
            {
                var url = $"busqueda/cuidadores-mapa?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lng={lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}&radioKm={radioKm.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new List<CuidadorMapa>();

                return await response.Content.ReadFromJsonAsync<List<CuidadorMapa>>() ?? new List<CuidadorMapa>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo cuidadores cercanos para el mapa: {ex.Message}");
                return new List<CuidadorMapa>();
            }
        }

        public async Task<bool> CrearCalificacionAsync(CrearCalificacionRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("calificacion", request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creando calificación: {ex.Message}");
                return false;
            }
        }

        public async Task<CalificacionPromedio?> ObtenerPromedioCalificacionAsync(int usuarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"calificacion/usuario/{usuarioId}/promedio");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<CalificacionPromedio>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo promedio de calificación: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ExisteCalificacionAsync(int trabajoId, int calificadorId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"calificacion/trabajo/{trabajoId}/existe?calificadorId={calificadorId}");
                if (!response.IsSuccessStatusCode)
                    return false;

                var resultado = await response.Content.ReadFromJsonAsync<ExisteCalificacionResponse>();
                return resultado?.Existe ?? false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verificando calificación: {ex.Message}");
                return false;
            }
        }

        private class ExisteCalificacionResponse
        {
            public bool Existe { get; set; }
        }

        public async Task<List<CalificacionRecibida>> ObtenerCalificacionesDeUsuarioAsync(int usuarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"calificacion/usuario/{usuarioId}");
                if (!response.IsSuccessStatusCode)
                    return new List<CalificacionRecibida>();

                return await response.Content.ReadFromJsonAsync<List<CalificacionRecibida>>() ?? new List<CalificacionRecibida>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo calificaciones del usuario: {ex.Message}");
                return new List<CalificacionRecibida>();
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

        public async Task<(bool Success, string? Error)> CrearTrabajoAsync(CrearTrabajoRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("trabajo", request);
                if (response.IsSuccessStatusCode)
                    return (true, null);

                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Error creando trabajo ({(int)response.StatusCode}): {error}");
                return (false, error);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creando trabajo: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<TrabajoCliente?> ObtenerTrabajoActivoPorClienteAsync(int clienteId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"trabajo/cliente/{clienteId}/activo");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<TrabajoCliente>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo trabajo activo del cliente: {ex.Message}");
                return null;
            }
        }

        public async Task<Conversacion?> ObtenerOCrearConversacionAsync(int trabajoId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"chat/conversacion/{trabajoId}", null);
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<Conversacion>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo conversación: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Mensaje>> ObtenerMensajesAsync(int conversacionId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"chat/conversacion/{conversacionId}/mensajes");
                if (!response.IsSuccessStatusCode)
                    return new List<Mensaje>();

                return await response.Content.ReadFromJsonAsync<List<Mensaje>>() ?? new List<Mensaje>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo mensajes: {ex.Message}");
                return new List<Mensaje>();
            }
        }

        public async Task<Mensaje?> EnviarMensajeAsync(int conversacionId, int remitenteId, string contenido, string tipo = "texto", string? urlArchivo = null, int? duracionSegundos = null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("chat/mensaje", new { ConversacionId = conversacionId, RemitenteId = remitenteId, Contenido = contenido, Tipo = tipo, UrlArchivo = urlArchivo, DuracionSegundos = duracionSegundos });
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<Mensaje>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando mensaje: {ex.Message}");
                return null;
            }
        }

        public async Task MarcarMensajesLeidosAsync(int conversacionId, int usuarioId)
        {
            try
            {
                await _httpClient.PutAsJsonAsync("chat/leido", new { ConversacionId = conversacionId, UsuarioId = usuarioId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marcando mensajes leídos: {ex.Message}");
            }
        }

        public async Task<List<TrabajoCliente>> ObtenerTrabajosActivosPorClienteAsync(int clienteId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"trabajo/cliente/{clienteId}/activos");
                if (!response.IsSuccessStatusCode)
                    return new List<TrabajoCliente>();

                return await response.Content.ReadFromJsonAsync<List<TrabajoCliente>>() ?? new List<TrabajoCliente>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo trabajos activos del cliente: {ex.Message}");
                return new List<TrabajoCliente>();
            }
        }

        public async Task<TrabajoCliente?> ObtenerTrabajoClientePorIdAsync(int trabajoId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"trabajo/{trabajoId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<TrabajoCliente>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo trabajo por id: {ex.Message}");
                return null;
            }
        }

        public async Task<(bool Success, string? Error)> IniciarTrabajoAsync(int trabajoId, string pin)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("trabajo/iniciar", new { TrabajoId = trabajoId, Pin = pin });
                if (response.IsSuccessStatusCode)
                    return (true, null);

                try
                {
                    var errorDto = await response.Content.ReadFromJsonAsync<IniciarTrabajoErrorDto>();
                    return (false, errorDto?.Message ?? "No se pudo iniciar el trabajo.");
                }
                catch
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return (false, string.IsNullOrWhiteSpace(error) ? "No se pudo iniciar el trabajo." : error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error iniciando trabajo: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> FinalizarTrabajoAsync(int trabajoId, string pin, string? justificacion = null)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("trabajo/finalizar", new { TrabajoId = trabajoId, Pin = pin, Justificacion = justificacion });
                if (response.IsSuccessStatusCode)
                    return (true, null);

                try
                {
                    var errorDto = await response.Content.ReadFromJsonAsync<IniciarTrabajoErrorDto>();
                    return (false, errorDto?.Message ?? "No se pudo finalizar el trabajo.");
                }
                catch
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return (false, string.IsNullOrWhiteSpace(error) ? "No se pudo finalizar el trabajo." : error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finalizando trabajo: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> ConfirmarFinalizacionAsync(int trabajoId, int clienteId, bool confirmado)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("trabajo/confirmar-finalizacion", new { TrabajoId = trabajoId, ClienteId = clienteId, Confirmado = confirmado });
                if (response.IsSuccessStatusCode)
                    return (true, null);

                var errorDto = await response.Content.ReadFromJsonAsync<IniciarTrabajoErrorDto>();
                return (false, errorDto?.Message ?? "No se pudo registrar tu respuesta.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error confirmando finalización: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? Error)> ForzarFinalizacionAsync(int trabajoId, int cuidadorId)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("trabajo/forzar-finalizacion", new { TrabajoId = trabajoId, CuidadorId = cuidadorId });
                if (response.IsSuccessStatusCode)
                    return (true, null);

                var errorDto = await response.Content.ReadFromJsonAsync<IniciarTrabajoErrorDto>();
                return (false, errorDto?.Message ?? "No se pudo forzar la finalización.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error forzando finalización: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<Calificacion?> ObtenerCalificacionDeTrabajoAsync(int trabajoId, int calificadorId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"calificacion/trabajo/{trabajoId}?calificadorId={calificadorId}");
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<Calificacion>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo calificación: {ex.Message}");
                return null;
            }
        }

        public async Task<List<ActividadTrabajo>> ObtenerActividadesAsync(int trabajoId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"trabajo/{trabajoId}/actividades");
                if (!response.IsSuccessStatusCode)
                    return new List<ActividadTrabajo>();

                return await response.Content.ReadFromJsonAsync<List<ActividadTrabajo>>() ?? new List<ActividadTrabajo>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo actividades: {ex.Message}");
                return new List<ActividadTrabajo>();
            }
        }

        public async Task AlertarGeocercaAsync(int trabajoId, double distanciaMetros)
        {
            try
            {
                await _httpClient.PostAsJsonAsync("trabajo/alerta-geocerca", new { TrabajoId = trabajoId, DistanciaMetros = distanciaMetros });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando alerta de geocerca: {ex.Message}");
            }
        }

        public async Task<bool> AgregarActividadAsync(int trabajoId, string descripcion)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("trabajo/actividades", new { TrabajoId = trabajoId, Descripcion = descripcion });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error agregando actividad: {ex.Message}");
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

        public async Task<List<MotivoCancelacion>> ObtenerMotivosCancelacionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("trabajo/motivos-cancelacion");
                if (!response.IsSuccessStatusCode)
                    return new List<MotivoCancelacion>();

                return await response.Content.ReadFromJsonAsync<List<MotivoCancelacion>>() ?? new List<MotivoCancelacion>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo motivos de cancelación: {ex.Message}");
                return new List<MotivoCancelacion>();
            }
        }

        public async Task<bool> CancelarTrabajoCuidadorAsync(int trabajoId, int motivoId, string? motivoTexto)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync("trabajo/cancelar-cuidador", new { TrabajoId = trabajoId, MotivoId = motivoId, MotivoTexto = motivoTexto });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cancelando trabajo: {ex.Message}");
                return false;
            }
        }

        public async Task<ForgotPasswordResponse?> ForgotPasswordAsync(string email)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("auth/forgot-password", new { Email = email });
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error olvidando contraseña: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ResetPasswordAsync(string email, string code, string newPassword)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("auth/reset-password", new { Email = email, Code = code, NewPassword = newPassword });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reseteando contraseña: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnviarEmailAsync(string destinatario, string asunto, string cuerpoHtml)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("email/enviar", new { Destinatario = destinatario, Asunto = asunto, CuerpoHtml = cuerpoHtml });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando email: {ex.Message}");
                return false;
            }
        }

        public async Task<int?> CrearTicketAsync(CrearTicketRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("ticket", request);
                if (!response.IsSuccessStatusCode)
                    return null;

                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
                return result != null && result.TryGetValue("ticketId", out var id) ? id : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creando ticket: {ex.Message}");
                return null;
            }
        }

        public async Task<List<Ticket>> ObtenerMisTicketsAsync(int usuarioId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"ticket/usuario/{usuarioId}");
                if (!response.IsSuccessStatusCode)
                    return new List<Ticket>();

                return await response.Content.ReadFromJsonAsync<List<Ticket>>() ?? new List<Ticket>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo tickets: {ex.Message}");
                return new List<Ticket>();
            }
        }

        public async Task<TicketDetalle?> ObtenerDetalleTicketAsync(int ticketId)
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

        public async Task<bool> AgregarMensajeTicketAsync(int ticketId, int autorId, string mensaje)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"ticket/{ticketId}/mensaje", new { AutorId = autorId, EsAdmin = false, Mensaje = mensaje });
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error agregando mensaje a ticket: {ex.Message}");
                return false;
            }
        }
    }
}
