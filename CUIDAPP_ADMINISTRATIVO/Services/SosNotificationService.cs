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
                    FechaCreacion = GetDateTime(alerta, "FechaCreacion")
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

        private static int GetInt(object obj, string name) =>
            Convert.ToInt32(obj.GetType().GetProperty(name)?.GetValue(obj) ?? 0);

        private static double GetDouble(object obj, string name) =>
            Convert.ToDouble(obj.GetType().GetProperty(name)?.GetValue(obj) ?? 0.0);

        private static string? GetString(object obj, string name) =>
            obj.GetType().GetProperty(name)?.GetValue(obj)?.ToString();

        private static DateTime GetDateTime(object obj, string name) =>
            Convert.ToDateTime(obj.GetType().GetProperty(name)?.GetValue(obj) ?? DateTime.MinValue);
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
    }
}
