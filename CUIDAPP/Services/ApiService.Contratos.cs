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
        private Task<HttpResponseMessage> EnviarSolicitudContratoAsync(HttpMethod metodo, string url, object? cuerpo = null)
        {
            var req = new HttpRequestMessage(metodo, url) { Content = cuerpo == null ? null : JsonContent.Create(cuerpo) };
            var token = Preferences.Default.Get("AuthToken", "");
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return EnviarYDisponerAsync(req);
        }

        /// <summary>Pide un token nuevo con el guardado (aunque esté vencido) y lo guarda. Sin errores visibles.</summary>
        public async Task RenovarTokenAsync()
        {
            try
            {
                var token = Preferences.Default.Get("AuthToken", "");
                if (string.IsNullOrWhiteSpace(token)) return;
                var r = await _httpClient.PostAsJsonAsync("auth/renovar-token", new { Token = token });
                if (!r.IsSuccessStatusCode) return;
                var json = await r.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("token", out var t) && t.GetString() is { Length: > 0 } nuevo)
                    Preferences.Default.Set("AuthToken", nuevo);
            }
            catch { /* sin conexión: se intenta la próxima vez */ }
        }

        private async Task<HttpResponseMessage> EnviarYDisponerAsync(HttpRequestMessage req)
        {
            using (req) return await _httpClient.SendAsync(req);
        }

        private static async Task<string> ErrorContratoAsync(HttpResponseMessage r)
        {
            try
            {
                var json = await r.Content.ReadFromJsonAsync<JsonElement>();
                if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("motivo", out var m) && m.GetString() is "DEUDA_PENDIENTE" or "CUENTA_INACTIVA")
                    return Localizador.T("cuenta_err_" + m.GetString()!.ToLowerInvariant());
                if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("code", out var c) && c.GetString() is { Length: > 0 } code)
                {
                    var clave = "contrato_err_" + code.ToLowerInvariant();
                    var texto = Localizador.T(clave);
                    if (texto != clave) return texto;
                }
            }
            catch { /* sin cuerpo JSON */ }
            return Localizador.T(r.StatusCode == System.Net.HttpStatusCode.Unauthorized ? "contrato_err_auth_required"
                : r.StatusCode == System.Net.HttpStatusCode.Forbidden ? "contrato_err_identidad_invalida" : "contrato_err_generico");
        }

        private async Task<(bool Ok, string? Error)> EnviarContratoAsync(HttpMethod metodo, string url, object? cuerpo)
        {
            try
            {
                var r = await EnviarSolicitudContratoAsync(metodo, url, cuerpo);
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
                var r = await EnviarSolicitudContratoAsync(HttpMethod.Get, url);
                return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<T>() : default;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Contratos] GET {url}: {ex.Message}");
                return default;
            }
        }

        public Task<ResumenMensualContrato?> ResumenMensualContratoAsync(int id, int usuarioId, DateTime mes)
            => LeerContratoAsync<ResumenMensualContrato>($"contratos/{id}/resumen-mensual?usuarioId={usuarioId}&mes={mes:yyyy-MM-dd}");

        public Task<(bool Ok, string? Error)> RenovarContratoAsync(int id, int usuarioId, DateTime fechaFin)
            => EnviarContratoAsync(HttpMethod.Post, $"contratos/{id}/renovar", new { UsuarioId = usuarioId, FechaFin = fechaFin });

        public Task<List<ReemplazoContrato>?> ReemplazosContratoAsync(int id, int trabajoId, int usuarioId)
            => LeerContratoAsync<List<ReemplazoContrato>>($"contratos/{id}/turnos/{trabajoId}/reemplazos?usuarioId={usuarioId}");
        public Task<List<RelevoContrato>?> RelevosContratoAsync(int id, int usuarioId)
            => LeerContratoAsync<List<RelevoContrato>>($"contratos/{id}/relevos?usuarioId={usuarioId}");

        /// <summary>El cliente cambia el checklist de un contrato activo (vale para las próximas jornadas).</summary>
        public Task<(bool Ok, string? Error)> GuardarTareasContratoAsync(int id, int usuarioId, List<TareaContrato> tareas)
            => EnviarContratoAsync(HttpMethod.Put, $"contratos/{id}/tareas", new { UsuarioId = usuarioId, Tareas = tareas.Select(t => new { t.Descripcion, t.RequiereFoto }) });

        public Task<(bool Ok, string? Error)> ReemplazarContratoAsync(int id, int trabajoId, int usuarioId, int cuidadorId)
            => EnviarContratoAsync(HttpMethod.Post, $"contratos/{id}/turnos/{trabajoId}/reemplazos/{cuidadorId}", new { UsuarioId = usuarioId });

        // ---------- Permiso para contratar y domicilios (cliente)
        public Task<PermisoYDomicilios?> ObtenerPermisoContratoAsync(int clienteId) => LeerContratoAsync<PermisoYDomicilios>($"contratos/permiso/{clienteId}");

        public Task<(bool Ok, string? Error)> SolicitarPermisoContratoAsync(int clienteId, string motivo, string? cedulaUrl, string? referencia)
            => EnviarContratoAsync(HttpMethod.Post, "contratos/permiso", new { ClienteId = clienteId, Motivo = motivo, CedulaUrl = cedulaUrl, Referencia = referencia });

        public Task<(bool Ok, string? Error)> SolicitarDomicilioContratoAsync(int clienteId, int ubicacionId, string comprobanteUrl, string quienVive)
            => EnviarContratoAsync(HttpMethod.Post, "contratos/domicilio", new { ClienteId = clienteId, UbicacionId = ubicacionId, ComprobanteUrl = comprobanteUrl, QuienVive = quienVive });

        // ---------- Invitar a mi cuidadora
        public async Task<(InvitacionCreada? Invitacion, string? Error)> CrearInvitacionCuidadoraAsync(int clienteId, string nombre, string? telefono, string? email, DateTime? trabajaDesde)
        {
            try
            {
                var r = await EnviarSolicitudContratoAsync(HttpMethod.Post, "contratos/invitaciones",
                    new { ClienteId = clienteId, Nombre = nombre, Telefono = telefono, Email = email, TrabajaDesde = trabajaDesde?.Date });
                return r.IsSuccessStatusCode ? (await r.Content.ReadFromJsonAsync<InvitacionCreada>(), null) : (null, await ErrorContratoAsync(r));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Contratos] invitación: {ex.Message}");
                return (null, Localizador.T("ocurrio_un_error_al_conectar"));
            }
        }

        public async Task<List<InvitacionCuidador>> ObtenerInvitacionesAsync(int clienteId)
            => await LeerContratoAsync<List<InvitacionCuidador>>($"contratos/invitaciones/cliente/{clienteId}") ?? new();

        public Task<(bool Ok, string? Error)> CancelarInvitacionAsync(int invitacionId, int clienteId)
            => EnviarContratoAsync(HttpMethod.Post, $"contratos/invitaciones/{invitacionId}/cancelar", new { ClienteId = clienteId });

        public async Task<(InvitacionInfo? Info, string? Error)> ObtenerInvitacionPorCodigoAsync(string codigo)
        {
            try
            {
                var r = await EnviarSolicitudContratoAsync(HttpMethod.Get, $"contratos/invitaciones/codigo/{Uri.EscapeDataString(codigo)}");
                return r.IsSuccessStatusCode ? (await r.Content.ReadFromJsonAsync<InvitacionInfo>(), null) : (null, await ErrorContratoAsync(r));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Contratos] código invitación: {ex.Message}");
                return (null, Localizador.T("ocurrio_un_error_al_conectar"));
            }
        }

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

        public async Task<List<AnexoContrato>> AnexosContratoAsync(int id,int usuarioId) => await LeerContratoAsync<List<AnexoContrato>>($"contratos/{id}/anexos?usuarioId={usuarioId}") ?? new();
        public Task<(bool Ok,string? Error)> ProponerAnexoAsync(int id,int usuarioId,decimal tarifa,DateTime fecha,List<FranjaContrato> horario)
          => EnviarContratoAsync(HttpMethod.Post,$"contratos/{id}/anexos",new { UsuarioId=usuarioId,TarifaHora=tarifa,FechaInicio=fecha,Horario=horario });
        public Task<(bool Ok,string? Error)> ResponderAnexoAsync(int id,int usuarioId,string accion)
          => EnviarContratoAsync(HttpMethod.Post,$"contratos/anexos/{id}/responder",new {UsuarioId=usuarioId,Accion=accion});
        // ---------- Firma
        /// <summary>Envía el código de firma al correo. Devuelve el correo enmascarado.</summary>
        public async Task<(bool Ok, string? Correo, string? Error)> EnviarCodigoFirmaAsync(int contratoId, int usuarioId, int? anexoId = null)
        {
            try
            {
                var r = await EnviarSolicitudContratoAsync(HttpMethod.Post, anexoId is int aId ? $"contratos/anexos/{aId}/codigo" : $"contratos/{contratoId}/codigo", new { UsuarioId = usuarioId });
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
        public async Task<(bool Ok, bool Activado, string? Error)> FirmarContratoAsync(int contratoId, FirmarContratoRequest request, int? anexoId = null)
        {
            try
            {
                var r = await EnviarSolicitudContratoAsync(HttpMethod.Post, anexoId is int aId ? $"contratos/anexos/{aId}/firmar" : $"contratos/{contratoId}/firmar", request);
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
        public Task<(bool Ok, string? Error)> MarcarAusenciaContratoAsync(int contratoId, int usuarioId, DateTime fecha, string? motivo, bool emergencia = false)
            => EnviarContratoAsync(HttpMethod.Post, $"contratos/{contratoId}/ausencias", new { UsuarioId = usuarioId, Fecha = fecha.Date, Motivo = motivo, Emergencia = emergencia });

        public Task<(bool Ok, string? Error)> QuitarAusenciaContratoAsync(int ausenciaId, int usuarioId)
            => EnviarContratoAsync(HttpMethod.Delete, $"contratos/ausencias/{ausenciaId}?usuarioId={usuarioId}", null);

        public async Task<(bool Ok, string? Error, DateTime? FechaFin)> CambiarEstadoContratoAsync(int contratoId, int usuarioId, string accion, string? motivo)
        {
            try
            {
                using var r = await EnviarSolicitudContratoAsync(HttpMethod.Post, $"contratos/{contratoId}/estado", new { UsuarioId = usuarioId, Accion = accion, Motivo = motivo });
                if (!r.IsSuccessStatusCode) return (false, await ErrorContratoAsync(r), null);
                var resultado = await r.Content.ReadFromJsonAsync<EstadoContratoResultado>();
                return (true, null, resultado?.FechaFin);
            }
            catch { return (false, Localizador.T("contrato_err_generico"), null); }
        }

        private sealed class EstadoContratoResultado
        {
            public DateTime? FechaFin { get; set; }
        }

        /// <summary>URL completa y temporal (10 min) del PDF firmado, o null.</summary>
        public async Task<string?> ObtenerEnlacePdfContratoAsync(int contratoId, int usuarioId, int? documentoId = null)
        {
            var json = await LeerContratoAsync<JsonElement>($"contratos/{contratoId}/enlace-pdf?usuarioId={usuarioId}&documentoId={documentoId}");
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("url", out var u) && u.GetString() is { } url
                ? BaseUrl + url
                : null;
        }
    }
}
