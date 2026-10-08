using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    /// <summary>
    /// Sesión del administrador dentro del panel. El JWT que devuelve la API al iniciar sesión se
    /// guarda como claim en la cookie y se adjunta a cada llamada a la API (que ahora lo exige en
    /// los endpoints de administración). También expone el nivel de permisos.
    /// Niveles: 1 Superadmin, 2 Operaciones/soporte, 3 Finanzas.
    /// </summary>
    public static class SesionAdmin
    {
        public const string ClaimToken = "api_token";
        public const string ClaimNivel = "nivel_admin";

        public const int Superadmin = 1, Operaciones = 2, Finanzas = 3;

        public static string NombreNivel(int nivel) => nivel switch
        {
            Superadmin => "Superadmin",
            Operaciones => "Operaciones",
            Finanzas => "Finanzas",
            _ => "Administrador"
        };

        public static int Nivel(ClaimsPrincipal user) => int.TryParse(user.FindFirst(ClaimNivel)?.Value, out var n) ? n : Operaciones;
        public static int AdminId(ClaimsPrincipal user) => int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        public static bool PuedeOperar(ClaimsPrincipal user) => Nivel(user) is Superadmin or Operaciones;
        public static bool PuedeFinanzas(ClaimsPrincipal user) => Nivel(user) is Superadmin or Finanzas;
        public static bool EsSuperadmin(ClaimsPrincipal user) => Nivel(user) == Superadmin;

        /// <summary>Lee el nivel y el vencimiento que vienen dentro del JWT (sin validarlo: eso lo hace la API).</summary>
        public static (int Nivel, DateTimeOffset? Vence) LeerToken(string token)
        {
            try
            {
                var partes = token.Split('.');
                var payload = partes[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
                var raiz = json.RootElement;
                var nivel = raiz.TryGetProperty(ClaimNivel, out var n) && int.TryParse(n.ToString(), out var v) ? v : Operaciones;
                DateTimeOffset? vence = raiz.TryGetProperty("exp", out var e) && e.TryGetInt64(out var s) ? DateTimeOffset.FromUnixTimeSeconds(s) : null;
                return (nivel, vence);
            }
            catch
            {
                return (Operaciones, null);
            }
        }

        /// <summary>Pone el JWT del administrador en el HttpClient de un servicio del panel.</summary>
        public static void Adjuntar(HttpClient http, AuthenticationStateProvider estado)
        {
            try
            {
                var tarea = estado.GetAuthenticationStateAsync();
                if (!tarea.IsCompletedSuccessfully)
                    return;
                var token = tarea.Result.User.FindFirst(ClaimToken)?.Value;
                if (!string.IsNullOrEmpty(token))
                    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            catch (InvalidOperationException)
            {
                // Todavía no hay estado de autenticación (página de login): se llama sin token.
            }
        }
    }
}
