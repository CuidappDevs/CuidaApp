using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Services.Admin;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.Cuidador
{
    /// <summary>
    /// Cada hora revisa los documentos con fecha de vencimiento (sp_ProcesarVencimientosDocumentos):
    /// avisa al Care Partner 30 y 7 días antes, y al vencer el documento pasa a rechazado (lo puede
    /// reemplazar desde la app) y el Care Partner queda oculto hasta que se apruebe uno vigente.
    /// </summary>
    public class VencimientoDocumentosService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ITrabajoNotifier _notifier;
        private readonly ILogger<VencimientoDocumentosService> _logger;

        public VencimientoDocumentosService(IServiceScopeFactory scopes, ITrabajoNotifier notifier, ILogger<VencimientoDocumentosService> logger)
        {
            _scopes = scopes;
            _notifier = notifier;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            do
            {
                try
                {
                    await ProcesarAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudieron revisar los documentos que vencen");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task ProcesarAsync()
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAdminOperacionesService>();
            var avisos = scope.ServiceProvider.GetRequiredService<CentroMandoService>();

            var filas = (await db.ConsultarAsync("sp_ProcesarVencimientosDocumentos", ("@Hoy", HoraLocalRD.Ahora.Date)))[0];
            var ocultos = new HashSet<int>();
            foreach (var f in filas)
            {
                var cuidadorId = Convert.ToInt32(f["CuidadorId"]);
                if (f["Oculto"] is true)
                    ocultos.Add(cuidadorId);

                var nivel = Convert.ToInt32(f["Nivel"]);
                if (nivel == 0)
                    continue;

                var documento = f["TipoDocumento"]?.ToString() ?? "documento";
                var fecha = f["FechaVencimiento"] is DateTime d ? d.ToString("dd/MM/yyyy") : "";
                var (titulo, mensaje) = nivel switch
                {
                    3 => ("Documento vencido", $"Tu {documento} venció el {fecha}. Quedaste oculto hasta que subas uno vigente desde tu perfil y lo aprobemos."),
                    2 => ("Tu documento vence pronto", $"Tu {documento} vence el {fecha}. Súbelo renovado desde tu perfil para no quedar oculto."),
                    _ => ("Renueva tu documento", $"Tu {documento} vence el {fecha} (en menos de 30 días). Ve preparando el nuevo.")
                };
                await avisos.EnviarAsync(null, cuidadorId, "mensaje", titulo, mensaje);
            }

            foreach (var id in ocultos)
                await _notifier.NotificarGlobalAsync("DisponibilidadCambio", new { CuidadorId = id, Disponible = false, PorHorario = false });
        }
    }
}
