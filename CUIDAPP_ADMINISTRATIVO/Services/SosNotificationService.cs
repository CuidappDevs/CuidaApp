using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace CUIDAPP_ADMINISTRATIVO.Services
{
    public class SosNotificationService : IAsyncDisposable
    {
        private readonly NavigationManager _navigation;
        private HubConnection? _hubConnection;
        private readonly List<SosAlerta> _alertasPendientes = new();

        public event Action? OnAlertaRecibida;
        public event Action? OnAlertaAtendida;
        public IReadOnlyList<SosAlerta> AlertasPendientes => _alertasPendientes.AsReadOnly();

        public SosNotificationService(NavigationManager navigation)
        {
            _navigation = navigation;
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

                _alertasPendientes.Add(sos);
                OnAlertaRecibida?.Invoke();
                await Task.CompletedTask;
            });

            try
            {
                await _hubConnection.StartAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error conectando al hub SOS: {ex.Message}");
            }
        }

        public void MarcarAtendida(int alertaId)
        {
            var alerta = _alertasPendientes.FirstOrDefault(a => a.Id == alertaId);
            if (alerta != null)
            {
                _alertasPendientes.Remove(alerta);
                OnAlertaAtendida?.Invoke();
            }
        }

        private string GetHubUrl()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
                    .Build();
                return config["SignalRHubUrl"] ?? "";
            }
            catch { return ""; }
        }

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
        public bool EsAutomatica => Origen == "Automatica";
    }
}
