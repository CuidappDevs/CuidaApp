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

        // ---------- Calidad, vencimientos, comisión y disputas
        public Task<List<CalidadCuidador>?> CalidadAsync(int dias) => Get<List<CalidadCuidador>>($"admin/calidad?dias={dias}");
        public Task<List<IncidenciaCalidad>?> CalidadDetalleAsync(int cuidadorId, int dias) => Get<List<IncidenciaCalidad>>($"admin/calidad/{cuidadorId}?dias={dias}");
        public Task<List<DocumentoVencimiento>?> VencimientosAsync() => Get<List<DocumentoVencimiento>>("admin/vencimientos");
        public Task<ResultadoAccion> FijarVencimientoAsync(int documentoId, DateTime? fecha) => Enviar(HttpMethod.Put, $"admin/vencimientos/{documentoId}", new { Fecha = fecha });
        public Task<ResultadoAccion> RevisarDocumentoAsync(int documentoId, bool aprobar, DateTime? fecha, string? observaciones)
            => Enviar(HttpMethod.Put, $"admin/vencimientos/{documentoId}/revisar", new { Aprobar = aprobar, Fecha = fecha, Observaciones = observaciones });
        public Task<ComisionesPanel?> ComisionesAsync() => Get<ComisionesPanel>("admin/comisiones");
        public Task<ResultadoAccion> GuardarComisionGeneralAsync(decimal porcentaje) => Enviar(HttpMethod.Put, "admin/comisiones/general", new { Porcentaje = porcentaje });
        public Task<ResultadoAccion> GuardarComisionTipoAsync(int tipoId, decimal? porcentaje) => Enviar(HttpMethod.Put, $"admin/comisiones/tipos/{tipoId}", new { Porcentaje = porcentaje });
        public Task<ResultadoAccion> FijarMetodoPagoAsync(int trabajoId, string? metodo) => Enviar(HttpMethod.Put, $"admin/servicios/{trabajoId}/metodo-pago", new { MetodoPago = metodo });
        public Task<GrabacionServicio?> GrabacionesServicioAsync(int trabajoId) => Get<GrabacionServicio>($"admin/servicios/{trabajoId}/grabaciones");
        public Task<bool> GrabacionSeguridadAsync() => Get<ActivaResp>("admin/grabacion-seguridad").ContinueWith(t => t.Result?.Activa == true);
        public Task<ResultadoAccion> GuardarGrabacionSeguridadAsync(bool activa) => Enviar(HttpMethod.Put, "admin/grabacion-seguridad", new { Activa = activa });
        private sealed class ActivaResp { public bool Activa { get; set; } }

        public Task<List<SaldoCuidadorFila>?> SaldosAsync() => Get<List<SaldoCuidadorFila>>("admin/saldos");
        public Task<List<MovimientoSaldo>?> MovimientosSaldoAsync(int cuidadorId) => Get<List<MovimientoSaldo>>($"admin/saldos/{cuidadorId}");
        public Task<ResultadoAccion> RegistrarAbonoAsync(int cuidadorId, decimal monto, string? nota) => Enviar(HttpMethod.Post, $"admin/saldos/{cuidadorId}/abono", new { Monto = monto, Nota = nota });
        public Task<ConfigPagos?> ConfigPagosAsync() => Get<ConfigPagos>("admin/config-pagos");
        public Task<ResultadoAccion> GuardarConfigPagosAsync(decimal tope, string? instrucciones) => Enviar(HttpMethod.Put, "admin/config-pagos", new { TopeDeudaEfectivo = tope, InstruccionesTransferencia = instrucciones });

        public Task<List<ServicioFila>?> ServiciosUsuarioAsync(int usuarioId) => Get<List<ServicioFila>>($"admin/usuarios/{usuarioId}/servicios");
        public Task<List<DisputaFila>?> DisputasAsync(int? estado) => Get<List<DisputaFila>>(estado.HasValue ? $"admin/disputas?estado={estado}" : "admin/disputas");
        public Task<DisputaFila?> DisputaAsync(int id) => Get<DisputaFila>($"admin/disputas/{id}");
        public Task<ResultadoAccion> AbrirDisputaAsync(int trabajoId, string motivo) => Enviar(HttpMethod.Post, "admin/disputas", new { TrabajoId = trabajoId, Motivo = motivo });
        public Task<ResultadoAccion> ResolverDisputaAsync(int id, string resolucion, decimal? monto, string nota)
            => Enviar(HttpMethod.Put, $"admin/disputas/{id}/resolver", new { Resolucion = resolucion, Monto = monto, Nota = nota });

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

        // Test psicológico del registro
        public Task<TestsPanel?> TestsAsync() => Get<TestsPanel>("admin/tests");
        public Task<TestDetalleRespuesta?> TestDetalleAsync(int id) => Get<TestDetalleRespuesta>($"admin/tests/{id}");
        public Task<ResultadoAccion> GuardarTestAsync(TestEdicion t) => Enviar(HttpMethod.Post, "admin/tests", t);
        public Task<List<TestIntentoFila>?> TestIntentosAsync(int? resultado, string? buscar)
            => Get<List<TestIntentoFila>>("admin/tests/intentos?" + string.Join("&", new[]
            {
                resultado is int r ? $"resultado={r}" : null,
                string.IsNullOrWhiteSpace(buscar) ? null : $"buscar={Uri.EscapeDataString(buscar.Trim())}"
            }.Where(p => p != null)));
        public Task<TestIntentoDetalle?> TestIntentoAsync(int id) => Get<TestIntentoDetalle>($"admin/tests/intentos/{id}");
        public Task<ResultadoAccion> AnularTestIntentoAsync(int id) => Enviar(HttpMethod.Post, $"admin/tests/intentos/{id}/anular");
        public Task<List<TestResultadoUsuario>?> TestResultadosUsuariosAsync() => Get<List<TestResultadoUsuario>>("admin/tests/resultados-usuarios");

        public Task<ResultadoAccion> AtenderSosAsync(int id, string atendidoPor) => Enviar(HttpMethod.Put, $"sos/{id}/atender", new { AtendidoPor = atendidoPor });
        public Task<ResultadoAccion> DescartarSosAsync(int id) => Enviar(HttpMethod.Put, $"sos/{id}/descartar");
    }
}
