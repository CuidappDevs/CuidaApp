using System.Security.Cryptography;
using CUIDAPP_API.Interfaces.Admin;

namespace CUIDAPP_API.Services.Cuenta;

public class EliminacionCuentasService(IServiceScopeFactory scopes,IWebHostEnvironment env,
    ILogger<EliminacionCuentasService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try { await ProcesarAsync(stoppingToken); }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(Exception ex) { logger.LogError(ex,"No se pudo procesar la eliminación de cuentas"); }
        } while(await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcesarAsync(CancellationToken ct)
    {
        using var scope=scopes.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<IAdminOperacionesService>();
        var filas=(await db.ConsultarAsync("sp_EliminacionesVencidas",("@Ahora",HoraLocalRD.Ahora)))[0];
        foreach(var fila in filas)
        {
            ct.ThrowIfCancellationRequested();
            var id=Convert.ToInt32(fila["UsuarioId"]);
            try
            {
                var revision=(await db.ConsultarAsync("sp_RevisarEliminacionCuenta",("@UsuarioId",id)))[0];
                if(revision.Any(b=>Convert.ToBoolean(b["Bloquea"]))) continue;
                await scope.ServiceProvider.GetRequiredService<BovedaCumplimientoService>().ArchivarUsuarioAsync(id,ct);
                var r=await db.EjecutarAsync("sp_AnonimizarUsuario",("@UsuarioId",id),("@Ahora",HoraLocalRD.Ahora),
                    ("@PasswordHash",Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()));
                if(r?["Resultado"]?.ToString()=="BLOQUEOS_PENDIENTES")
                    logger.LogWarning("Anonimización de {Id} pospuesta por obligaciones pendientes",id);
            }
            catch(Exception ex) { logger.LogError(ex,"No se pudo anonimizar el usuario {Id}",id); }
        }
        var archivos=(await db.ConsultarAsync("sp_EliminacionArchivos"))[0];
        var root=Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath,"wwwroot"),"uploads");
        foreach(var archivo in archivos)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var ruta=RutaUpload(root,archivo["Url"]?.ToString());
                // No sigue enlaces simbólicos/reparse points hacia fuera del almacén.
                if(ruta!=null && File.Exists(ruta))
                {
                    for(var d=new DirectoryInfo(Path.GetDirectoryName(ruta)!);d!=null;d=d.Parent)
                    {
                        if((d.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException("La ruta contiene un enlace simbólico.");
                        if(string.Equals(d.FullName,Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase)) break;
                    }
                    if((File.GetAttributes(ruta)&FileAttributes.ReparsePoint)!=0) throw new IOException("El archivo es un enlace simbólico.");
                    File.Delete(ruta);
                }
                await db.EjecutarAsync("sp_EliminacionArchivos",("@Id",archivo["Id"]));
            }
            catch(Exception ex) { logger.LogWarning(ex,"No se pudo borrar el archivo de la cola {Id}; se reintentará",archivo["Id"]); }
        }
    }

    public static string? RutaUpload(string root,string? url)
    {
        if(string.IsNullOrWhiteSpace(url)) return null;
        // Upload entrega tanto /uploads/... como URL absoluta. No descarga ni borra fuera de uploads.
        var path=Uri.TryCreate(url,UriKind.Absolute,out var uri) && uri.Scheme is "https" or "http" ? uri.AbsolutePath : url;
        path=Uri.UnescapeDataString(path.Split('?', '#')[0]).Replace('\\','/');
        if(!path.StartsWith("/uploads/",StringComparison.OrdinalIgnoreCase)) return null;
        var raiz=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var destino=Path.GetFullPath(Path.Combine(raiz,path[9..]));
        return destino.StartsWith(raiz,StringComparison.OrdinalIgnoreCase) ? destino : null;
    }
}
