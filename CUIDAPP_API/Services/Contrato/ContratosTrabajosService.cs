namespace CUIDAPP_API.Services.Contrato
{
    /// <summary>
    /// Cada hora: vence las propuestas sin respuesta (72 h), cierra los contratos que pasaron su fecha
    /// fin y genera los Trabajos de los próximos 7 días de cada contrato activo (sp_ContratosGenerarTrabajos).
    /// </summary>
    public class ContratosTrabajosService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<ContratosTrabajosService> _logger;

        public ContratosTrabajosService(IServiceScopeFactory scopes, ILogger<ContratosTrabajosService> logger)
        {
            _scopes = scopes;
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
                    using var scope = _scopes.CreateScope();
                    var contratos = scope.ServiceProvider.GetRequiredService<ContratoService>();
                    var activados = await contratos.ActivarPendientesAsync();
                    await contratos.ActivarAnexosPendientesAsync();
                    if (activados > 0)
                        _logger.LogInformation("Contratos fijos: {N} activados tras completar firmas", activados);
                    await contratos.RecordarRenovacionesAsync();
                    var creados = await contratos.GenerarTrabajosAsync();
                    if (creados > 0)
                        _logger.LogInformation("Contratos fijos: {N} jornadas nuevas", creados);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudieron generar los trabajos de los contratos fijos");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
