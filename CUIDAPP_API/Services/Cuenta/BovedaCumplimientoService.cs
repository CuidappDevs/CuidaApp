using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.DTOs.AdminOperaciones;

namespace CUIDAPP_API.Services.Cuenta;

public class BovedaCumplimientoService(IAdminOperacionesService db,IConfiguration config,IWebHostEnvironment env)
{
    private byte[] Clave() => Convert.FromBase64String(config["BovedaCumplimiento:Clave"]
        ?? throw new InvalidOperationException("Falta configurar BovedaCumplimiento:Clave; se pospone la anonimización."));
    private string Raiz => Path.GetFullPath(Path.Combine(env.ContentRootPath,config["BovedaCumplimiento:Ruta"] ?? "boveda"));
    private string Privado => config["AlmacenPrivado"] is {Length:>0} r ? r : Path.Combine(env.ContentRootPath,"privado");

    // No permite traversal ni enlaces simbólicos en el archivo o sus directorios.
    public static string RutaSegura(string raiz,string relativa)
    {
        var root=Path.GetFullPath(raiz).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var ruta=Path.GetFullPath(Path.Combine(root,relativa));
        if(!ruta.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new IOException("Ruta fuera del almacén.");
        for(var d=new DirectoryInfo(Path.GetDirectoryName(ruta)!);d!=null;d=d.Parent)
            if(d.Exists && (d.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException("Ruta con enlace simbólico.");
        if(File.Exists(ruta) && (File.GetAttributes(ruta)&FileAttributes.ReparsePoint)!=0) throw new IOException("Archivo con enlace simbólico.");
        return ruta;
    }
    public async Task ArchivarUsuarioAsync(int usuarioId,CancellationToken ct)
    {
        var docs=(await db.ConsultarAsync("sp_BovedaDocumentos",("@UsuarioId",usuarioId)))[0];
        if(docs.Count==0) return;
        var clave=Clave(); // También al reintentar documentos marcados: verificar antes de retirar cualquier original.
        foreach(var d in docs)
        {
            ct.ThrowIfCancellationRequested();
            var id=Convert.ToInt32(d["Id"]);
            var relativa=d.GetValueOrDefault("RutaBoveda")?.ToString() ?? $"documento-{id}.aes";
            var destino=RutaSegura(Raiz,relativa);
            var origen=RutaSegura(Privado,d["Ruta"]!.ToString()!);
            var hash=d["Hash"]!.ToString()!;
            if(!File.Exists(destino))
            {
                var pdf=await File.ReadAllBytesAsync(origen,ct);
                BovedaCifrado.VerificarHash(pdf,hash);
                var cifrado=BovedaCifrado.Cifrar(pdf,clave);
                Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
                var temporal=destino+"."+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    await File.WriteAllBytesAsync(temporal,cifrado,ct);
                    BovedaCifrado.VerificarHash(BovedaCifrado.Descifrar(await File.ReadAllBytesAsync(temporal,ct),clave),hash);
                    try { File.Move(temporal,destino,false); }
                    catch(IOException) when(File.Exists(destino)) { /* Otro proceso publicó: verificarlo abajo. */ }
                }
                finally { if(File.Exists(temporal)) File.Delete(temporal); }
            }
            var verificado=BovedaCifrado.Descifrar(await File.ReadAllBytesAsync(destino,ct),clave);
            BovedaCifrado.VerificarHash(verificado,hash);
            await db.EjecutarAsync("sp_BovedaGuardar",("@DocumentoId",id),("@Ruta",relativa),
                ("@Tamano",verificado.LongLength),("@Ahora",HoraLocalRD.Ahora));
            // Solo después de escritura, descifrado, hash y registro durable. Reintentar borra originales remanentes.
            if(File.Exists(origen)) File.Delete(origen);
        }
    }
    public async Task<byte[]> AbrirAsync(Fila doc,CancellationToken ct)
    {
        var archivo=await File.ReadAllBytesAsync(RutaSegura(Raiz,doc["RutaBoveda"]!.ToString()!),ct);
        var pdf=BovedaCifrado.Descifrar(archivo,Clave());
        BovedaCifrado.VerificarHash(pdf,doc["Hash"]!.ToString()!);
        return pdf;
    }
}
