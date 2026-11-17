using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CUIDAPP.Localization;
using CUIDAPP.Models.Cuenta;

namespace CUIDAPP.Services
{
    /// <summary>
    /// Deuda del cliente (api/DeudaCliente) y eliminación de cuenta (api/EliminarCuenta).
    /// Los errores llegan como { motivo, message }: se muestran con la clave cuenta_err_{motivo}
    /// si existe; si no, un mensaje genérico.
    /// </summary>
    public partial class ApiService
    {
        /// <summary>Error especial de CrearTrabajoAsync: el cliente tiene una deuda que le impide pedir servicios.</summary>
        public const string ErrorDeudaPendiente = "__DEUDA_PENDIENTE__";

        private static async Task<(string? Motivo, JsonElement Json)> LeerMotivoAsync(HttpResponseMessage r)
        {
            try
            {
                var json = await r.Content.ReadFromJsonAsync<JsonElement>();
                if (json.ValueKind == JsonValueKind.Object)
                {
                    foreach (var nombre in new[] { "motivo", "Motivo", "code" })
                        if (json.TryGetProperty(nombre, out var m) && m.ValueKind == JsonValueKind.String)
                            return (m.GetString(), json);
                    return (null, json);
                }
            }
            catch { /* sin cuerpo JSON */ }
            return (null, default);
        }

        private static string TextoErrorCuenta(string? motivo)
        {
            if (!string.IsNullOrWhiteSpace(motivo))
            {
                var clave = "cuenta_err_" + motivo.ToLowerInvariant();
                var texto = Localizador.T(clave);
                if (texto != clave) return texto;
            }
            return Localizador.T("cuenta_err_generico");
        }

        private async Task<(bool Ok, string? Error)> PostCuentaAsync(string url, object cuerpo)
        {
            try
            {
                var r = await _httpClient.PostAsJsonAsync(url, cuerpo);
                if (r.IsSuccessStatusCode) return (true, null);
                var (motivo, _) = await LeerMotivoAsync(r);
                return (false, TextoErrorCuenta(motivo));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en {url}: {ex.Message}");
                return (false, Localizador.T("ocurrio_un_error_al_conectar"));
            }
        }

        // ---------------- Deuda del cliente

        public async Task<ResumenDeudasCliente?> ObtenerDeudasClienteAsync(int clienteId)
        {
            try { return await _httpClient.GetFromJsonAsync<ResumenDeudasCliente>($"deudacliente/cliente/{clienteId}"); }
            catch (Exception ex) { Console.WriteLine($"Error deudas cliente: {ex.Message}"); return null; }
        }

        public async Task<DeudaCliente?> ObtenerDeudaTrabajoAsync(int trabajoId)
        {
            try
            {
                var r = await _httpClient.GetAsync($"deudacliente/trabajo/{trabajoId}");
                if (r.StatusCode == HttpStatusCode.NoContent || !r.IsSuccessStatusCode) return null;
                return await r.Content.ReadFromJsonAsync<DeudaCliente>();
            }
            catch { return null; }
        }

        public Task<(bool Ok, string? Error)> EnviarComprobanteDeudaAsync(int deudaId, int clienteId, string comprobanteUrl, string? nota)
            => PostCuentaAsync($"deudacliente/{deudaId}/comprobante", new { ClienteId = clienteId, ComprobanteUrl = comprobanteUrl, Nota = nota });

        public Task<(bool Ok, string? Error)> ObjetarDeudaAsync(int deudaId, int clienteId, string motivo)
            => PostCuentaAsync($"deudacliente/{deudaId}/objetar", new { ClienteId = clienteId, Motivo = motivo });

        // ---------------- Eliminar cuenta

        public async Task<RevisionEliminacion?> RevisarEliminacionAsync(int usuarioId)
        {
            try { return await _httpClient.GetFromJsonAsync<RevisionEliminacion>($"eliminarcuenta/revisar/{usuarioId}"); }
            catch (Exception ex) { Console.WriteLine($"Error revisando eliminación: {ex.Message}"); return null; }
        }

        public Task<(bool Ok, string? Error)> EnviarCodigoEliminacionAsync(int usuarioId)
            => PostCuentaAsync("eliminarcuenta/codigo", new { UsuarioId = usuarioId });

        public async Task<(DateTime? FechaEjecucion, string? Error)> ConfirmarEliminacionAsync(int usuarioId, string codigo, string? motivo)
        {
            try
            {
                var r = await _httpClient.PostAsJsonAsync("eliminarcuenta/confirmar", new { UsuarioId = usuarioId, Codigo = codigo, Motivo = motivo });
                if (r.IsSuccessStatusCode)
                {
                    var s = await r.Content.ReadFromJsonAsync<SolicitudEliminacion>();
                    return (s?.FechaEjecucion ?? DateTime.Now.AddDays(30), null);
                }
                var (motivoError, _) = await LeerMotivoAsync(r);
                return (null, TextoErrorCuenta(motivoError));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error confirmando eliminación: {ex.Message}");
                return (null, Localizador.T("ocurrio_un_error_al_conectar"));
            }
        }

        /// <summary>Recupera una cuenta en proceso de eliminación (exige las credenciales).</summary>
        public Task<(bool Ok, string? Error)> CancelarEliminacionAsync(string correo, string contrasena)
            => PostCuentaAsync("eliminarcuenta/cancelar", new { Correo = correo, Contrasena = contrasena });
    }
}
