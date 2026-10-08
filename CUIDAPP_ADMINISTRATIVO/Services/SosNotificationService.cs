using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    public class SosNotificationService : IAsyncDisposable
    {
        private readonly NavigationManager _navigation;
        private readonly PanelApiService _api;
        private readonly IConfiguration _config;
        private HubConnection? _hubConnection;
        private readonly List<SosAlerta> _alertasPendientes = new();

        public event Action? OnAlertaRecibida;
        public event Action? OnAlertaAtendida;
        /// <summary>Un Care Partner se activó o desactivó (interruptor, notificación o su horario automático).</summary>
        public event Action<int, bool>? OnDisponibilidadCambio;
        /// <summary>Un Care Partner envió una ubicación nueva (CuidadorId, latitud, longitud).</summary>
        public event Action<int, double, double>? OnUbicacionCambio;
        /// <summary>Se perdió o se recuperó la conexión en tiempo real con la API.</summary>
        public event Action<bool>? OnConexionCambio;
        /// <summary>Respuesta a un "¿Estás bien?" del centro de mando (UsuarioId, nombre, "ok" / "ayuda" / "vencido").</summary>
        public event Action<int, string, string>? OnCheckinRespondido;
        public bool Conectado => _hubConnection?.State == HubConnectionState.Connected;
        public IReadOnlyList<SosAlerta> AlertasPendientes { get { lock (_alertasPendientes) return _alertasPendientes.ToList(); } }

        public SosNotificationService(NavigationManager navigation, PanelApiService api, IConfiguration config)
        {
            _config = config;
            _navigation = navigation;
            _api = api;
        }

        /// <summary>
        /// Trae de la base las alertas pendientes. Así una alerta no se pierde al recargar la página,
        /// al abrir otra pestaña o si llegó mientras ningún administrador estaba conectado.
        /// </summary>
        public async Task RecargarAsync()
        {
            var pendientes = await _api.SosPendientesAsync();
            if (pendientes == null)
                return;
            lock (_alertasPendientes)
            {
                _alertasPendientes.Clear();
                _alertasPendientes.AddRange(pendientes.OrderByDescending(a => a.FechaCreacion));
            }
            OnAlertaRecibida?.Invoke();
        }

        /// <summary>Marca la alerta como atendida en el servidor (queda en el historial con el nombre de quien la atendió).</summary>
        public async Task<ResultadoAccion> AtenderAsync(int alertaId, string atendidoPor)
        {
            var r = await _api.AtenderSosAsync(alertaId, atendidoPor);
            if (r.Ok) Quitar(alertaId);
            return r;
        }

        /// <summary>Descarta una alerta (falsa alarma). Queda en el historial como descartada.</summary>
        public async Task<ResultadoAccion> DescartarAsync(int alertaId)
        {
            var r = await _api.DescartarSosAsync(alertaId);
            if (r.Ok) Quitar(alertaId);
            return r;
        }

        public async Task IniciarAsync()
        {
            if (_hubConnection != null)
                return;

            var hubUrl = _navigation.ToAbsoluteUri("/hubs/trabajo").ToString();
            // Reemplazar la URL del navegador con la del servidor API
            var configHubUrl = GetHubUrl();
            if (!string.IsNullOrEmpty(configHubUrl))
                hubUrl = configHubUrl;

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<object>("AlertaSOS", async (alerta) =>
            {
                var propiedades = alerta.GetType().GetProperties();
                var sos = new SosAlerta
                {
                    Id = GetInt(alerta, "Id"),
                    TrabajoId = GetInt(alerta, "TrabajoId"),
                    UsuarioId = GetInt(alerta, "UsuarioId"),
                    TipoUsuario = GetString(alerta, "TipoUsuario") ?? "",
                    NombreUsuario = GetString(alerta, "NombreUsuario") ?? "Desconocido",
                    EmailUsuario = GetString(alerta, "EmailUsuario"),
                    Latitud = GetDouble(alerta, "Latitud"),
                    Longitud = GetDouble(alerta, "Longitud"),
                    Motivo = GetString(alerta, "Motivo"),
                    Estado = GetString(alerta, "Estado") ?? "Pendiente",
                    FechaCreacion = GetDateTime(alerta, "FechaCreacion"),
                    Origen = GetString(alerta, "Origen") ?? "Manual",
                    ContactoNombre = GetString(alerta, "ContactoNombre"),
                    ContactoTelefono = GetString(alerta, "ContactoTelefono"),
                    ContactoEmail = GetString(alerta, "ContactoEmail")
                };

                lock (_alertasPendientes)
                {
                    if (_alertasPendientes.Any(a => a.Id == sos.Id))
                        return;
                    _alertasPendientes.Insert(0, sos);
                }
                OnAlertaRecibida?.Invoke();
                await Task.CompletedTask;
            });

            // Al reconectar se vuelve a leer la base por si llegó algo durante el corte.
            // Eventos globales que la API ya emite a todos: los usa el mapa en vivo.
            _hubConnection.On<object>("DisponibilidadCambio", payload =>
                OnDisponibilidadCambio?.Invoke(GetInt(payload, "CuidadorId"), Leer(payload, "Disponible")?.ToString()?.ToLowerInvariant() == "true"));
            _hubConnection.On<object>("UbicacionCuidadorCambio", payload =>
                OnUbicacionCambio?.Invoke(GetInt(payload, "CuidadorId"), GetDouble(payload, "Latitud"), GetDouble(payload, "Longitud")));

            _hubConnection.On<object>("CheckinRespondido", payload =>
                OnCheckinRespondido?.Invoke(GetInt(payload, "UsuarioId"), GetString(payload, "Nombre") ?? "", GetString(payload, "Respuesta") ?? ""));

            _hubConnection.Reconnecting += _ => { OnConexionCambio?.Invoke(false); return Task.CompletedTask; };
            _hubConnection.Closed += _ => { OnConexionCambio?.Invoke(false); return Task.CompletedTask; };
            _hubConnection.Reconnected += async _ => { OnConexionCambio?.Invoke(true); await RecargarAsync(); };

            try
            {
                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error conectando al hub SOS: {ex.Message}");
            }

            await RecargarAsync();
        }

        private void Quitar(int alertaId)
        {
            lock (_alertasPendientes)
                _alertasPendientes.RemoveAll(a => a.Id == alertaId);
            OnAlertaAtendida?.Invoke();
        }

        private string GetHubUrl() => _config["SignalRHubUrl"] ?? "";

        public async ValueTask DisposeAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
        }

        // El evento llega como JsonElement (SignalR deserializa "object" así); se lee sin distinguir
        // mayúsculas. Si fuera otro tipo de objeto, se usa reflexión como respaldo.
        private static object? Leer(object obj, string name)
        {
            if (obj is System.Text.Json.JsonElement json)
            {
                foreach (var p in json.EnumerateObject())
                {
                    if (!string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                        continue;
                    return p.Value.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.Null => null,
                        System.Text.Json.JsonValueKind.Number => p.Value.GetDouble(),
                        _ => p.Value.ToString()
                    };
                }
                return null;
            }
            return obj.GetType().GetProperty(name)?.GetValue(obj);
        }

        private static int GetInt(object obj, string name) =>
            Convert.ToInt32(Leer(obj, name) ?? 0);

        private static double GetDouble(object obj, string name) =>
            Convert.ToDouble(Leer(obj, name) ?? 0.0, System.Globalization.CultureInfo.InvariantCulture);

        private static string? GetString(object obj, string name) =>
            Leer(obj, name)?.ToString();

        private static DateTime GetDateTime(object obj, string name) =>
            Leer(obj, name) is { } v && DateTime.TryParse(v.ToString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var fecha) ? fecha : DateTime.MinValue;
    }

    public class SosAlerta
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public int UsuarioId { get; set; }
        public string TipoUsuario { get; set; } = "";
        public string NombreUsuario { get; set; } = "";
        public string? EmailUsuario { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public string? Motivo { get; set; }
        public string Estado { get; set; } = "Pendiente";
        public DateTime FechaCreacion { get; set; }
        public string Origen { get; set; } = "Manual";
        public string? ContactoNombre { get; set; }
        public string? ContactoTelefono { get; set; }
        public string? ContactoEmail { get; set; }
        public DateTime? FechaAtencion { get; set; }
        public string? AtendidoPor { get; set; }
        public bool EsAutomatica => Origen == "Automatica";
    }
}
