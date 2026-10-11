using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Services;

namespace CUIDAPP_API.Services.Admin
{
    /// <summary>
    /// Caja negra: cada 6 horas borra del disco los segmentos de audio con más de 30 días, salvo que su
    /// servicio tenga una disputa abierta o una alerta SOS (esos se conservan como prueba).
    /// </summary>
    public class GrabacionRetencionService : BackgroundService
    {
        private const int DiasRetencion = 30;
        private readonly IServiceScopeFactory _scopes;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<GrabacionRetencionService> _logger;

        public GrabacionRetencionService(IServiceScopeFactory scopes, IWebHostEnvironment env, ILogger<GrabacionRetencionService> logger)
        {
            _scopes = scopes;
            _env = env;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
            do
            {
                try { await LimpiarAsync(); }
                catch (Exception ex) { _logger.LogWarning(ex, "No se pudieron limpiar las grabaciones vencidas"); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task LimpiarAsync()
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAdminOperacionesService>();
            var vencidos = (await db.ConsultarAsync("sp_GrabacionesParaBorrar", ("@Hoy", HoraLocalRD.Ahora), ("@DiasRetencion", DiasRetencion)))[0];
            if (vencidos.Count == 0)
                return;

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            foreach (var g in vencidos)
            {
                var url = g["UrlArchivo"]?.ToString();
                if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("/uploads/"))
                {
                    try
                    {
                        var ruta = Path.Combine(new[] { webRoot }.Concat(url.TrimStart('/').Split('/')).ToArray());
                        if (System.IO.File.Exists(ruta))
                            System.IO.File.Delete(ruta);
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "No se pudo borrar el archivo de grabación {Url}", url); }
                }
                await db.EjecutarAsync("sp_MarcarGrabacionBorrada", ("@Id", Convert.ToInt32(g["Id"])));
            }
            _logger.LogInformation("Grabaciones vencidas borradas: {N}", vencidos.Count);
        }
    }
}
