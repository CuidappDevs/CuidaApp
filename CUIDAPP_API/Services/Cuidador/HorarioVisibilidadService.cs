using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.Cuidador
{
    /// <summary>
    /// Cada minuto aplica el horario automático de los Care Partners que lo tienen activo
    /// (sp_AplicarHorariosCuidadores) y avisa por SignalR a quienes cambiaron: así la visibilidad
    /// cambia aunque el teléfono del cuidador esté apagado.
    /// </summary>
    public class HorarioVisibilidadService : BackgroundService
    {
        private readonly string _connectionString;
        private readonly ITrabajoNotifier _notifier;
        private readonly ILogger<HorarioVisibilidadService> _logger;
        private readonly IServiceScopeFactory _scopes;

        public HorarioVisibilidadService(IConfiguration config, ITrabajoNotifier notifier, ILogger<HorarioVisibilidadService> logger, IServiceScopeFactory scopes)
        {
            _scopes = scopes;
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
            _notifier = notifier;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Arranca alineado al inicio de cada minuto, para que "8:00" sea 8:00 y no 8:00:59.
            await Task.Delay(TimeSpan.FromSeconds(60 - DateTime.UtcNow.Second), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                try
                {
                    await AplicarAsync(_connectionString, _notifier, null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo aplicar el horario automático de los cuidadores");
                }

                // En la misma vuelta: los "¿Estás bien?" del centro de mando que vencieron sin respuesta pasan a SOS.
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<CUIDAPP_API.Services.Admin.CentroMandoService>().ProcesarVencidosAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudieron revisar los «¿Estás bien?» vencidos");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        /// <summary>Aplica el horario (de todos o de un cuidador) y avisa los cambios. También lo usa el guardado del horario.</summary>
        public static async Task AplicarAsync(string connectionString, ITrabajoNotifier notifier, int? cuidadorId)
        {
            var cambios = new List<(int CuidadorId, bool Disponible)>();
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand("sp_AplicarHorariosCuidadores", connection) { CommandType = CommandType.StoredProcedure })
            {
                command.Parameters.AddWithValue("@Ahora", HoraLocalRD.Ahora);
                command.Parameters.AddWithValue("@CuidadorId", (object?)cuidadorId ?? DBNull.Value);
                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    cambios.Add((reader.GetInt32(0), reader.GetBoolean(1)));
            }

            // Mismo evento que el interruptor manual: el mapa de los clientes y la app del cuidador se actualizan solos.
            foreach (var (id, disponible) in cambios)
                await notifier.NotificarGlobalAsync("DisponibilidadCambio", new { CuidadorId = id, Disponible = disponible, PorHorario = true });
        }
    }
}
