using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using CUIDAPP_ADMINISTRATIVO.Models.Panel;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    /// <summary>Resultado de una acción del panel: si salió bien y el mensaje que devuelve la API.</summary>
    public record ResultadoAccion(bool Ok, string Mensaje, bool SinPermiso = false);

    /// <summary>
    /// Llamadas de la operación diaria (dashboard, servicios, verificación, mapa, calificaciones,
    /// finanzas, catálogos, auditoría, avisos y SOS). Todas van con el JWT del administrador.
    /// </summary>
    public class PanelApiService
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly HttpClient _http;

        public PanelApiService(HttpClient http, AuthenticationStateProvider estado)
        {
            _http = http;
            SesionAdmin.Adjuntar(_http, estado);
        }

        // Las fotos y documentos se sirven a través del panel (/archivo/...): la API está en http y el
        // navegador bloquea imágenes http dentro de una página https.
        public string ServerOrigin => "/archivo";

        /// <summary>Convierte una ruta "/uploads/..." en URL completa del servidor.</summary>
        public string? Archivo(string? ruta) => string.IsNullOrWhiteSpace(ruta) ? null : ruta.StartsWith("http") ? ruta : ServerOrigin + ruta;

        private async Task<T?> Get<T>(string url)
        {
            try
            {
                var r = await _http.GetAsync(url);
                if (!r.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Panel] GET {url} → {(int)r.StatusCode}");
                    return default;
                }
                return await r.Content.ReadFromJsonAsync<T>(Json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Panel] GET {url}: {ex.Message}");
                return default;
            }
        }

        private async Task<ResultadoAccion> Enviar(HttpMethod metodo, string url, object? cuerpo = null)
        {
            try
            {
                using var req = new HttpRequestMessage(metodo, url) { Content = cuerpo == null ? null : JsonContent.Create(cuerpo) };
                var r = await _http.SendAsync(req);
                if (r.StatusCode is HttpStatusCode.Forbidden)
                    return new(false, "Tu nivel de administrador no permite esta acción.", true);
                if (r.StatusCode is HttpStatusCode.Unauthorized)
                    return new(false, "Tu sesión venció. Vuelve a iniciar sesión.");

                var mensaje = r.IsSuccessStatusCode ? "Listo." : "No se pudo completar la acción.";
                try
                {
                    var json = await r.Content.ReadFromJsonAsync<JsonElement>(Json);
                    if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                        mensaje = m.GetString()!;
                }
                catch { /* respuesta sin cuerpo JSON */ }
                return new(r.IsSuccessStatusCode, mensaje);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Panel] {metodo} {url}: {ex.Message}");
                return new(false, "No se pudo conectar con el servidor.");
            }
        }

        // ---------- Lecturas
        public Task<DashboardPanel?> DashboardAsync() => Get<DashboardPanel>("admin/dashboard");

        public Task<Pagina<ServicioFila>?> ServiciosAsync(int? estado, DateTime? desde, DateTime? hasta, string? buscar, int pagina, int tamano)
        {
            var q = new List<string> { $"pagina={pagina}", $"tamano={tamano}" };
            if (estado.HasValue) q.Add($"estado={estado}");
            if (desde.HasValue) q.Add($"desde={desde:yyyy-MM-dd}");
            if (hasta.HasValue) q.Add($"hasta={hasta:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(buscar)) q.Add($"buscar={Uri.EscapeDataString(buscar.Trim())}");
            return Get<Pagina<ServicioFila>>("admin/servicios?" + string.Join("&", q));
        }

        public Task<DetalleServicio?> ServicioAsync(int id) => Get<DetalleServicio>($"admin/servicios/{id}");
        public Task<List<VerificacionFila>?> VerificacionAsync() => Get<List<VerificacionFila>>("admin/verificacion");
        public Task<MapaPanel?> MapaAsync() => Get<MapaPanel>("admin/mapa");
        public Task<CalificacionesPanel?> CalificacionesAsync(int? max) => Get<CalificacionesPanel>(max.HasValue ? $"admin/calificaciones?max={max}" : "admin/calificaciones");
        public Task<FinanzasPanel?> FinanzasAsync(DateTime desde, DateTime hasta) => Get<FinanzasPanel>($"admin/finanzas?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}");

        public Task<Pagina<AuditoriaFila>?> AuditoriaAsync(string? buscar, int pagina, int tamano)
            => Get<Pagina<AuditoriaFila>>($"admin/auditoria?pagina={pagina}&tamano={tamano}" + (string.IsNullOrWhiteSpace(buscar) ? "" : "&buscar=" + Uri.EscapeDataString(buscar.Trim())));

        public Task<List<AvisoFila>?> AvisosAsync() => Get<List<AvisoFila>>("admin/avisos");
        public Task<List<TipoServicioCat>?> TiposServicioAsync() => Get<List<TipoServicioCat>>("admin/catalogos/tipos-servicio");
        public Task<List<MotivoCat>?> MotivosAsync() => Get<List<MotivoCat>>("admin/catalogos/motivos-cancelacion");
        public Task<List<NacionalidadCat>?> NacionalidadesAsync() => Get<List<NacionalidadCat>>("admin/catalogos/nacionalidades");
        public Task<EstatusPanel?> EstatusMigratorioAsync() => Get<EstatusPanel>("admin/catalogos/estatus-migratorio");

        // ---------- Centro de mando
        public Task<FichaMapa?> FichaMapaAsync(int usuarioId) => Get<FichaMapa>($"admin/mapa/ficha/{usuarioId}");
        public Task<List<PuntoRecorrido>?> EstelasAsync(int minutos) => Get<List<PuntoRecorrido>>($"admin/mapa/estelas?minutos={minutos}");
        public Task<List<PuntoRecorrido>?> RecorridoServicioAsync(int trabajoId) => Get<List<PuntoRecorrido>>($"admin/mapa/recorrido?trabajoId={trabajoId}");
        public Task<List<PuntoRecorrido>?> RecorridoCuidadorAsync(int cuidadorId, DateTime desde, DateTime hasta)
            => Get<List<PuntoRecorrido>>($"admin/mapa/recorrido?cuidadorId={cuidadorId}&desde={desde:yyyy-MM-ddTHH:mm}&hasta={hasta:yyyy-MM-ddTHH:mm}");
        public Task<List<PuntoDemanda>?> DemandaAsync(int dias) => Get<List<PuntoDemanda>>($"admin/mapa/demanda?dias={dias}");
        public Task<List<AlertaMapa>?> AlertasMapaAsync() => Get<List<AlertaMapa>>("admin/mapa/alertas");
        public Task<ResultadoAccion> NotificarAsync(IEnumerable<int> usuarioIds, string titulo, string mensaje)
            => Enviar(HttpMethod.Post, "admin/mapa/notificar", new { UsuarioIds = usuarioIds.ToList(), Titulo = titulo, Mensaje = mensaje });
        public Task<ResultadoAccion> CheckinAsync(int usuarioId, int minutos, string? mensaje)
            => Enviar(HttpMethod.Post, "admin/mapa/checkin", new { UsuarioId = usuarioId, Minutos = minutos, Mensaje = mensaje });
        public Task<ResultadoAccion> OcultarCuidadorAsync(int usuarioId) => Enviar(HttpMethod.Post, $"admin/mapa/ocultar/{usuarioId}");

        public Task<List<SosAlerta>?> SosPendientesAsync() => Get<List<SosAlerta>>("sos/pendientes");
        public Task<List<SosAlerta>?> SosHistorialAsync() => Get<List<SosAlerta>>("sos/historial?top=100");

        // ---------- Acciones
        public Task<ResultadoAccion> CancelarServicioAsync(int id, string motivo) => Enviar(HttpMethod.Put, $"admin/servicios/{id}/cancelar", new { Motivo = motivo });
        public Task<ResultadoAccion> CompletarServicioAsync(int id, string motivo) => Enviar(HttpMethod.Put, $"admin/servicios/{id}/completar", new { Motivo = motivo });
        public Task<ResultadoAccion> EnviarAvisoAsync(int destino, string? idioma, string titulo, string mensaje) => Enviar(HttpMethod.Post, "admin/avisos", new { Destino = destino, Idioma = idioma, Titulo = titulo, Mensaje = mensaje });
        public Task<ResultadoAccion> ReenviarAvisoAsync(int id) => Enviar(HttpMethod.Post, $"admin/avisos/{id}/reenviar");
        public Task<ResultadoAccion> EliminarAvisoAsync(int id) => Enviar(HttpMethod.Delete, $"admin/avisos/{id}");
        public Task<ResultadoAccion> CambiarNivelAsync(int usuarioId, int nivel) => Enviar(HttpMethod.Put, $"admin/administradores/{usuarioId}/nivel", new { Nivel = nivel });

        public Task<ResultadoAccion> GuardarTipoServicioAsync(TipoServicioCat t) => Enviar(HttpMethod.Post, "admin/catalogos/tipos-servicio", t);
        public Task<ResultadoAccion> GuardarMotivoAsync(MotivoCat m) => Enviar(HttpMethod.Post, "admin/catalogos/motivos-cancelacion", m);
        public Task<ResultadoAccion> GuardarNacionalidadAsync(NacionalidadCat n) => Enviar(HttpMethod.Post, "admin/catalogos/nacionalidades", n);
        public Task<ResultadoAccion> GuardarEstatusAsync(EstatusCat e) => Enviar(HttpMethod.Post, "admin/catalogos/estatus-migratorio", e);
        public Task<ResultadoAccion> AgregarRequisitoAsync(int estatusId, string tipo) => Enviar(HttpMethod.Post, "admin/catalogos/requisitos", new { EstatusMigratorioId = estatusId, TipoDocumento = tipo });
        public Task<ResultadoAccion> QuitarRequisitoAsync(int id) => Enviar(HttpMethod.Delete, $"admin/catalogos/requisitos/{id}");

        public Task<ResultadoAccion> AtenderSosAsync(int id, string atendidoPor) => Enviar(HttpMethod.Put, $"sos/{id}/atender", new { AtendidoPor = atendidoPor });
        public Task<ResultadoAccion> DescartarSosAsync(int id) => Enviar(HttpMethod.Put, $"sos/{id}/descartar");
    }
}
