using System.Collections.Concurrent;
using CUIDAPP_ADMINISTRATIVO.Models.Auth;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    // Puente de un solo uso entre el componente interactivo de Blazor Server (que no
    // puede escribir cookies de respuesta HTTP directamente, porque corre sobre un
    // circuito de SignalR) y el endpoint mínimo /account/login-complete (una petición
    // HTTP normal, donde sí se puede llamar HttpContext.SignInAsync). El login exitoso
    // guarda aquí el resultado bajo un código de un solo uso; el navegador es redirigido
    // (forceLoad) a ese endpoint, que lo recupera, arma la cookie de sesión y lo borra.
    public static class PendingLoginStore
    {
        private static readonly ConcurrentDictionary<string, (AuthResponse Data, DateTime CreadoUtc)> _pendientes = new();
        private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(2);

        public static string Guardar(AuthResponse data)
        {
            PurgarExpirados();
            var codigo = Guid.NewGuid().ToString("N");
            _pendientes[codigo] = (data, DateTime.UtcNow);
            return codigo;
        }

        public static bool TryTomar(string codigo, out AuthResponse? data)
        {
            if (_pendientes.TryRemove(codigo, out var entry) && DateTime.UtcNow - entry.CreadoUtc <= Vigencia)
            {
                data = entry.Data;
                return true;
            }

            data = null;
            return false;
        }

        private static void PurgarExpirados()
        {
            var limite = DateTime.UtcNow - Vigencia;
            foreach (var kv in _pendientes)
            {
                if (kv.Value.CreadoUtc < limite)
                    _pendientes.TryRemove(kv.Key, out _);
            }
        }
    }
}
