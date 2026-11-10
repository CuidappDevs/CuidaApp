using System.Net.Http.Json;
using System.Text.Json;
using CUIDAPP.Localization;
using CUIDAPP.Models.Contrato;

namespace CUIDAPP.Services
{
    /// <summary>
    /// Contratos fijos (api/contratos). Los errores del servidor llegan como { code }: se muestran con la
    /// clave contrato_err_{code} (si no existe, un mensaje genérico).
    /// </summary>
    public partial class ApiService
    {
        private static async Task<string> ErrorContratoAsync(HttpResponseMessage r)
        {
            try
            {
                var json = await r.Content.ReadFromJsonAsync<JsonElement>();
                if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("code", out var c) && c.GetString() is { Length: > 0 } code)
                {
                    var clave = "contrato_err_" + code.ToLowerInvariant();
                    var texto = Localizador.T(clave);
                    if (texto != clave) return texto;
                }
            }
            catch { /* sin cuerpo JSON */ }
            return Localizador.T("contrato_err_generico");
        }

        private async Task<(bool Ok, string? Error)> EnviarContratoAsync(HttpMethod metodo, string url, object? cuerpo)
        {
            try
            {
                using var req = new HttpRequestMessage(metodo, url) { Content = cuerpo == null ? null : JsonContent.Create(cuerpo) };
                var r = await _httpClient.SendAsync(req);
                return r.IsSuccessStatusCode ? (true, null) : (false, await ErrorContratoAsync(r));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Contratos] {metodo} {url}: {ex.Message}");
                return (false, Localizador.T("ocurrio_un_error_al_conectar"));
            }
        }

        private async Task<T?> LeerContratoAsync<T>(string url)
        {
            try
            {
                var r = await _httpClient.GetAsync(url);
                return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<T>() : default;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Contratos] GET {url}: {ex.Message}");
                return default;
            }
        }

        // ---------- Permiso para contratar y domicilios (cliente)
        public Task<PermisoYDomicilios?> ObtenerPermisoContratoAsync(int clienteId) => LeerContratoAsync<PermisoYDomicilios>($"contratos/permiso/{clienteId}");

        public Task<(bool Ok, string? Error)> SolicitarPermisoContratoAsync(int clienteId, string motivo, string? cedulaUrl, string? referencia)
            => EnviarContratoAsync(HttpMethod.Post, "contratos/permiso", new { ClienteId = clienteId, Motivo = motivo, CedulaUrl = cedulaUrl, Referencia = referencia });

        public Task<(bool Ok, string? Error)> SolicitarDomicilioContratoAsync(int clienteId, int ubicacionId, string comprobanteUrl, string quienVive)
            => EnviarContratoAsync(HttpMethod.Post, "contratos/domicilio", new { ClienteId = clienteId, UbicacionId = ubicacionId, ComprobanteUrl = comprobanteUrl, QuienVive = quienVive });

        // ---------- Care Partner
        public Task<ConfigCuidadorContratos?> ObtenerConfigContratosAsync(int cuidadorId) => LeerContratoAsync<ConfigCuidadorContratos>($"contratos/cuidador/{cuidadorId}/config");

        public Task<(bool Ok, string? Error)> GuardarConfigContratosAsync(int cuidadorId, bool acepta)
            => EnviarContratoAsync(HttpMethod.Put, $"contratos/cuidador/{cuidadorId}/config", new { Acepta = acepta });

        // ---------- Propuesta y negociación
        public Task<PuedeProponer?> PuedeProponerContratoAsync(int clienteId, int cuidadorId)
            => LeerContratoAsync<PuedeProponer>($"contratos/puede-proponer?clienteId={clienteId}&cuidadorId={cuidadorId}");

        public Task<(bool Ok, string? Error)> ProponerContratoAsync(ProponerContratoRequest request)
            => EnviarContratoAsync(HttpMethod.Post, "contratos", request);

        public Task<(bool Ok, string? Error)> ResponderContratoAsync(int contratoId, ResponderContratoRequest request)
            => EnviarContratoAsync(HttpMethod.Post, $"contratos/{contratoId}/responder", request);

        public async Task<List<ContratoResumen>> ObtenerContratosAsync(int usuarioId)
            => await LeerContratoAsync<List<ContratoResumen>>($"contratos/usuario/{usuarioId}") ?? new();

        public Task<ContratoDetalle?> ObtenerContratoAsync(int contratoId, int usuarioId)
            => LeerContratoAsync<ContratoDetalle>($"contratos/{contratoId}?usuarioId={usuarioId}");

        // ---------- Firma
        /// <summary>Envía el código de firma al correo. Devuelve el correo enmascarado.</summary>
        public async Task<(bool Ok, string? Correo, string? Error)> EnviarCodigoFirmaAsync(int contratoId, int usuarioId)
        {
            try
            {
                var r = await _httpClient.PostAsJsonAsync($"contratos/{contratoId}/codigo", new { UsuarioId = usuarioId });
                if (!r.IsSuccessStatusCode) return (false, null, await ErrorContratoAsync(r));
                var json = await r.Content.ReadFromJsonAsync<JsonElement>();
                return (true, json.TryGetProperty("correo", out var c) ? c.GetString() : null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Contratos] código: {ex.Message}");
                return (false, null, Localizador.T("ocurrio_un_error_al_conectar"));
            }
        }

        /// <summary>Firma. Activado = true si con esta firma quedó firmado por las dos partes.</summary>
        public async Task<(bool Ok, bool Activado, string? Error)> FirmarContratoAsync(int contratoId, FirmarContratoRequest request)
        {
            try
            {
                var r = await _httpClient.PostAsJsonAsync($"contratos/{contratoId}/firmar", request);
                if (!r.IsSuccessStatusCode) return (false, false, await ErrorContratoAsync(r));
                var json = await r.Content.ReadFromJsonAsync<JsonElement>();
                return (true, json.TryGetProperty("activado", out var a) && a.GetBoolean(), null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Contratos] firmar: {ex.Message}");
                return (false, false, Localizador.T("ocurrio_un_error_al_conectar"));
            }
        }

        // ---------- Contrato activo
        public Task<(bool Ok, string? Error)> MarcarAusenciaContratoAsync(int contratoId, int usuarioId, DateTime fecha, string? motivo)
            => EnviarContratoAsync(HttpMethod.Post, $"contratos/{contratoId}/ausencias", new { UsuarioId = usuarioId, Fecha = fecha.Date, Motivo = motivo });

        public Task<(bool Ok, string? Error)> QuitarAusenciaContratoAsync(int ausenciaId, int usuarioId)
            => EnviarContratoAsync(HttpMethod.Delete, $"contratos/ausencias/{ausenciaId}?usuarioId={usuarioId}", null);

        public Task<(bool Ok, string? Error)> CambiarEstadoContratoAsync(int contratoId, int usuarioId, string accion, string? motivo)
            => EnviarContratoAsync(HttpMethod.Post, $"contratos/{contratoId}/estado", new { UsuarioId = usuarioId, Accion = accion, Motivo = motivo });

        /// <summary>URL completa y temporal (10 min) del PDF firmado, o null.</summary>
        public async Task<string?> ObtenerEnlacePdfContratoAsync(int contratoId, int usuarioId)
        {
            var json = await LeerContratoAsync<JsonElement>($"contratos/{contratoId}/enlace-pdf?usuarioId={usuarioId}");
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("url", out var u) && u.GetString() is { } url
                ? BaseUrl + url
                : null;
        }
    }
}
