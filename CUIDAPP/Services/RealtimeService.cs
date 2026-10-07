using Microsoft.AspNetCore.SignalR.Client;
using CUIDAPP.Models.Chat;

namespace CUIDAPP.Services
{
    // Conexión SignalR única para toda la app. Se conecta una vez por sesión (tras login)
    // y las páginas se suscriben/desuscriben a los eventos que les interesan en OnAppearing/OnDisappearing.
    public static class RealtimeService
    {
        private static HubConnection? _connection;
        private static int _usuarioIdConectado;

        public static event Action<int, int>? NuevaSolicitud;          // TrabajoId, ClienteId
        public static event Action<int, int>? TrabajoActualizado;      // TrabajoId, Estado
        public static event Action<int, bool>? DisponibilidadCambio;   // CuidadorId, Disponible
        public static event Action<int, double, double>? UbicacionCuidadorCambio; // CuidadorId, Lat, Lng
        public static event Action<int, string, DateTime>? ActividadAgregada; // TrabajoId, Descripcion, FechaHora
        public static event Action<int, decimal>? PropinaRecibida; // TrabajoId, Monto
        public static event Action<int, int, string>? TareaCompletada; // TrabajoId, TareaId, Descripcion
        public static event Action<Mensaje>? MensajeNuevo;
        public static event Action<int, double>? AlertaGeocerca; // TrabajoId, DistanciaMetros
        public static event Action<int, int, bool>? UsuarioEscribiendo; // ConversacionId, UsuarioId, Escribiendo
        public static event Action<int>? CuentaActualizada; // Estado (2=Aprobado, 3=Rechazado)
        public static event Action<int, decimal>? PagoAprobado; // TrabajoId, Monto
        public static event Action<int, bool, int?, string?>? TicketActualizado; // TicketId, Respuesta, Estado, Asunto

        public static bool EstaConectado => _connection?.State == HubConnectionState.Connected;

        public static async Task ConectarAsync(int usuarioId)
        {
            if (usuarioId == 0)
                return;

            if (_connection != null && _usuarioIdConectado == usuarioId && _connection.State != HubConnectionState.Disconnected)
                return;

            if (_connection != null)
                await _connection.DisposeAsync();

            _usuarioIdConectado = usuarioId;

            _connection = new HubConnectionBuilder()
                .WithUrl($"{ApiService.ServerOrigin}/hubs/trabajo")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<object>("NuevaSolicitud", payload => { Console.WriteLine("[Realtime] Evento recibido: NuevaSolicitud"); DispatchNuevaSolicitud(payload); });
            _connection.On<object>("TrabajoActualizado", payload => { Console.WriteLine("[Realtime] Evento recibido: TrabajoActualizado"); DispatchTrabajoActualizado(payload); });
            _connection.On<object>("DisponibilidadCambio", payload => { Console.WriteLine("[Realtime] Evento recibido: DisponibilidadCambio"); DispatchDisponibilidadCambio(payload); });
            _connection.On<object>("UbicacionCuidadorCambio", payload => DispatchUbicacionCambio(payload));
            _connection.On<object>("ActividadAgregada", payload => { Console.WriteLine("[Realtime] Evento recibido: ActividadAgregada"); DispatchActividadAgregada(payload); });
            _connection.On<object>("TareaCompletada", payload => DispatchTareaCompletada(payload));
            _connection.On<object>("PropinaRecibida", payload => DispatchPropinaRecibida(payload));
            _connection.On<Mensaje>("MensajeNuevo", mensaje =>
            {
                Console.WriteLine("[Realtime] Evento recibido: MensajeNuevo");
                MainThread.BeginInvokeOnMainThread(() => MensajeNuevo?.Invoke(mensaje));
            });
            _connection.On<object>("AlertaGeocerca", payload => DispatchAlertaGeocerca(payload));
            _connection.On<object>("UsuarioEscribiendo", payload => DispatchUsuarioEscribiendo(payload));
            _connection.On<object>("CuentaActualizada", payload =>
            {
                var json = (System.Text.Json.JsonElement)payload;
                var estado = json.GetProperty("estado").GetInt32();
                MainThread.BeginInvokeOnMainThread(() => CuentaActualizada?.Invoke(estado));
            });
            _connection.On<object>("PagoAprobado", payload =>
            {
                var json = (System.Text.Json.JsonElement)payload;
                var trabajoId = json.TryGetProperty("trabajoId", out var t) ? t.GetInt32() : 0;
                var monto = json.GetProperty("monto").GetDecimal();
                MainThread.BeginInvokeOnMainThread(() => PagoAprobado?.Invoke(trabajoId, monto));
            });
            _connection.On<object>("TicketActualizado", payload =>
            {
                var json = (System.Text.Json.JsonElement)payload;
                var ticketId = json.GetProperty("ticketId").GetInt32();
                var respuesta = json.TryGetProperty("respuesta", out var r) && r.GetBoolean();
                int? estado = json.TryGetProperty("estado", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.Number ? e.GetInt32() : null;
                var asunto = json.TryGetProperty("asunto", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.String ? a.GetString() : null;
                MainThread.BeginInvokeOnMainThread(() => TicketActualizado?.Invoke(ticketId, respuesta, estado, asunto));
            });

            _connection.Reconnecting += ex =>
            {
                Console.WriteLine($"[Realtime] Reconectando... ({ex?.Message})");
                return Task.CompletedTask;
            };

            _connection.Reconnected += async _ =>
            {
                Console.WriteLine("[Realtime] Reconectado, uniéndose al grupo de nuevo.");
                await _connection.InvokeAsync("Unirse", usuarioId);
            };

            // WithAutomaticReconnect() reintenta un rato y si no lo logra, dispara Closed
            // y se queda desconectado para siempre (no reintenta más por su cuenta). Sin
            // este manejador, la app se queda sin tiempo real hasta que el usuario vuelve
            // a visitar una pantalla que llame a ConectarAsync.
            _connection.Closed += async ex =>
            {
                Console.WriteLine($"Conexión de tiempo real cerrada: {ex?.Message}");
                await Task.Delay(TimeSpan.FromSeconds(3));
                if (_usuarioIdConectado == usuarioId)
                    await ConectarAsync(usuarioId);
            };

            try
            {
                await _connection.StartAsync();
                await _connection.InvokeAsync("Unirse", usuarioId);
                Console.WriteLine($"[Realtime] Conectado y unido al grupo del usuario {usuarioId} (hub: {ApiService.ServerOrigin}/hubs/trabajo).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Realtime] ERROR conectando: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public static async Task DesconectarAsync()
        {
            if (_connection == null)
                return;

            await _connection.DisposeAsync();
            _connection = null;
            _usuarioIdConectado = 0;
        }

        private static void DispatchNuevaSolicitud(object payload)
        {
            var (trabajoId, clienteId) = LeerDosEnteros(payload, "trabajoId", "clienteId");
            MainThread.BeginInvokeOnMainThread(() => NuevaSolicitud?.Invoke(trabajoId, clienteId));
        }

        private static void DispatchTrabajoActualizado(object payload)
        {
            var (trabajoId, estado) = LeerDosEnteros(payload, "trabajoId", "estado");
            MainThread.BeginInvokeOnMainThread(() => TrabajoActualizado?.Invoke(trabajoId, estado));
        }

        private static void DispatchDisponibilidadCambio(object payload)
        {
            var json = (System.Text.Json.JsonElement)payload;
            var cuidadorId = json.GetProperty("cuidadorId").GetInt32();
            var disponible = json.GetProperty("disponible").GetBoolean();
            MainThread.BeginInvokeOnMainThread(() => DisponibilidadCambio?.Invoke(cuidadorId, disponible));
        }

        private static void DispatchUbicacionCambio(object payload)
        {
            var json = (System.Text.Json.JsonElement)payload;
            var cuidadorId = json.GetProperty("cuidadorId").GetInt32();
            var lat = json.GetProperty("latitud").GetDouble();
            var lng = json.GetProperty("longitud").GetDouble();
            MainThread.BeginInvokeOnMainThread(() => UbicacionCuidadorCambio?.Invoke(cuidadorId, lat, lng));
        }

        private static void DispatchActividadAgregada(object payload)
        {
            var json = (System.Text.Json.JsonElement)payload;
            var trabajoId = json.GetProperty("trabajoId").GetInt32();
            var descripcion = json.GetProperty("descripcion").GetString() ?? "";
            var fechaHora = json.GetProperty("fechaHora").GetDateTime();
            MainThread.BeginInvokeOnMainThread(() => ActividadAgregada?.Invoke(trabajoId, descripcion, fechaHora));
        }

        private static void DispatchPropinaRecibida(object payload)
        {
            var json = (System.Text.Json.JsonElement)payload;
            var trabajoId = json.GetProperty("trabajoId").GetInt32();
            var monto = json.GetProperty("monto").GetDecimal();
            MainThread.BeginInvokeOnMainThread(() => PropinaRecibida?.Invoke(trabajoId, monto));
        }

        private static void DispatchTareaCompletada(object payload)
        {
            var json = (System.Text.Json.JsonElement)payload;
            var trabajoId = json.GetProperty("trabajoId").GetInt32();
            var tareaId = json.GetProperty("id").GetInt32();
            var descripcion = json.GetProperty("descripcion").GetString() ?? "";
            MainThread.BeginInvokeOnMainThread(() => TareaCompletada?.Invoke(trabajoId, tareaId, descripcion));
        }

        private static void DispatchAlertaGeocerca(object payload)
        {
            var json = (System.Text.Json.JsonElement)payload;
            var trabajoId = json.GetProperty("trabajoId").GetInt32();
            var distancia = json.GetProperty("distanciaMetros").GetDouble();
            MainThread.BeginInvokeOnMainThread(() => AlertaGeocerca?.Invoke(trabajoId, distancia));
        }

        private static void DispatchUsuarioEscribiendo(object payload)
        {
            var json = (System.Text.Json.JsonElement)payload;
            var conversacionId = json.GetProperty("conversacionId").GetInt32();
            var usuarioId = json.GetProperty("usuarioId").GetInt32();
            var escribiendo = json.GetProperty("escribiendo").GetBoolean();
            MainThread.BeginInvokeOnMainThread(() => UsuarioEscribiendo?.Invoke(conversacionId, usuarioId, escribiendo));
        }

        /// <summary>
        /// Avisa al otro participante del chat que este usuario está (o dejó de estar) escribiendo.
        /// Es "mejor esfuerzo": si no hay conexión, simplemente no se avisa.
        /// </summary>
        public static async Task AvisarEscribiendoAsync(int conversacionId, int usuarioId, bool escribiendo)
        {
            if (_connection?.State != HubConnectionState.Connected)
                return;
            try
            {
                await _connection.InvokeAsync("Escribiendo", conversacionId, usuarioId, escribiendo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Realtime] No se pudo avisar 'escribiendo': {ex.Message}");
            }
        }

        private static (int, int) LeerDosEnteros(object payload, string campo1, string campo2)
        {
            var json = (System.Text.Json.JsonElement)payload;
            return (json.GetProperty(campo1).GetInt32(), json.GetProperty(campo2).GetInt32());
        }
    }
}
