using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.Cuenta;

public class DeudasClienteAvisosService(IServiceScopeFactory scopes,IConfiguration config,
    ILogger<DeudasClienteAvisosService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try { await ProcesarAsync(stoppingToken); }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(Exception ex) { logger.LogError(ex,"No se pudieron procesar los avisos de deudas; se reintentará"); }
        } while(await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcesarAsync(CancellationToken ct)
    {
        using var scope=scopes.CreateScope();
        var notifier=scope.ServiceProvider.GetRequiredService<ITrabajoNotifier>();
        using var connection=new SqlConnection(config.GetConnectionString("DefaultConnection"));
        await connection.OpenAsync(ct);
        while(!ct.IsCancellationRequested)
        {
            using var transaction=connection.BeginTransaction();
            using var leer=new SqlCommand("sp_DeudasAvisos",connection,transaction) {CommandType=CommandType.StoredProcedure};
            leer.Parameters.AddWithValue("@Ahora",HoraLocalRD.Ahora);
            Fila? d=null;
            using(var reader=await leer.ExecuteReaderAsync(ct))
            {
                if(await reader.ReadAsync(ct))
                {
                    d=new Fila();
                    for(var i=0;i<reader.FieldCount;i++) d[reader.GetName(i)]=reader.GetValue(i);
                }
            }
            if(d==null) { await transaction.CommitAsync(ct); break; }
            // El SP bloquea esta deuda hasta guardar el envío: otros procesos y cierres esperan/omiten la fila.
            var tipo=d["Tipo"]!.ToString()!;
            object payload=tipo=="deuda_recordatorio"
                ? new { tipo,trabajoId=d["TrabajoId"],monto=d["Monto"],fechaLimite=d["FechaLimite"] }
                : new { tipo,trabajoId=d["TrabajoId"],monto=d["Monto"] };
            await notifier.NotificarAsync(Convert.ToInt32(d["ClienteId"]),"CuentaActualizada",payload).WaitAsync(ct);
            using var marcar=new SqlCommand("sp_DeudasAvisos",connection,transaction) {CommandType=CommandType.StoredProcedure};
            marcar.Parameters.AddWithValue("@Ahora",HoraLocalRD.Ahora);
            marcar.Parameters.AddWithValue("@Id",d["Id"]);
            marcar.Parameters.AddWithValue("@Tipo",tipo);
            await marcar.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}
