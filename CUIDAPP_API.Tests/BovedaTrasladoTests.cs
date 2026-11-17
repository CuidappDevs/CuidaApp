using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Services.Cuenta;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System.Security.Cryptography;

namespace CUIDAPP_API.Tests;

public class BovedaTrasladoTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"cuida-boveda-tests-"+Guid.NewGuid().ToString("N"));
    private readonly byte[] clave=RandomNumberGenerator.GetBytes(32);
    private readonly byte[] pdf="%PDF-1.7 contrato firmado de prueba"u8.ToArray();
    private readonly Db db=new();
    public BovedaTrasladoTests()
    {
        Directory.CreateDirectory(Path.Combine(root,"privado"));
        File.WriteAllBytes(Path.Combine(root,"privado","contrato.pdf"),pdf);
        db.Doc=new() { ["Id"]=1,["Ruta"]="contrato.pdf",["Hash"]=Convert.ToHexString(SHA256.HashData(pdf)),["EnBoveda"]=false };
    }
    private BovedaCumplimientoService Servicio(bool conClave=true) => new(db,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["BovedaCumplimiento:Clave"]=conClave?Convert.ToBase64String(clave):null }).Build(),new Entorno {ContentRootPath=root});
    [Fact]
    public async Task Traslado_verificado_elimina_original_y_es_repetible()
    {
        var servicio=Servicio();
        await servicio.ArchivarUsuarioAsync(2,default);
        Assert.False(File.Exists(Path.Combine(root,"privado","contrato.pdf")));
        Assert.True((bool)db.Doc["EnBoveda"]!);
        Assert.Equal(pdf,await servicio.AbrirAsync(db.Doc,default));
        await servicio.ArchivarUsuarioAsync(2,default);
        Assert.Equal(pdf,await servicio.AbrirAsync(db.Doc,default));
    }
    [Fact]
    public async Task Fallo_al_registrar_conserva_original_y_reintento_recupera_cifrado()
    {
        db.Fallar=true;
        await Assert.ThrowsAsync<IOException>(()=>Servicio().ArchivarUsuarioAsync(2,default));
        Assert.True(File.Exists(Path.Combine(root,"privado","contrato.pdf")));
        var cifrado=File.ReadAllBytes(Path.Combine(root,"boveda","documento-1.aes"));
        db.Fallar=false;
        await Servicio().ArchivarUsuarioAsync(2,default);
        Assert.Equal(cifrado,File.ReadAllBytes(Path.Combine(root,"boveda","documento-1.aes")));
        Assert.False(File.Exists(Path.Combine(root,"privado","contrato.pdf")));
    }
    [Fact]
    public async Task Sin_clave_conserva_original_sin_registrar_traslado()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Servicio(false).ArchivarUsuarioAsync(2,default));
        Assert.True(File.Exists(Path.Combine(root,"privado","contrato.pdf")));
        Assert.False((bool)db.Doc["EnBoveda"]!);
    }
    [Fact]
    public async Task Hash_incorrecto_no_borra_original()
    {
        db.Doc["Hash"]=new string('0',64);
        await Assert.ThrowsAsync<CryptographicException>(()=>Servicio().ArchivarUsuarioAsync(2,default));
        Assert.True(File.Exists(Path.Combine(root,"privado","contrato.pdf")));
        Assert.False((bool)db.Doc["EnBoveda"]!);
    }
    [Fact]
    public void Rechaza_ruta_fuera_de_almacen() => Assert.Throws<IOException>(()=>BovedaCumplimientoService.RutaSegura(root,"../otro.pdf"));
    public void Dispose() { if(Directory.Exists(root)) Directory.Delete(root,true); }
    private class Db : IAdminOperacionesService
    {
        public Fila Doc=new(); public bool Fallar;
        public Task<List<List<Fila>>> ConsultarAsync(string sp,params (string Nombre,object? Valor)[] parametros)
            => Task.FromResult(new List<List<Fila>> {new() {Doc}});
        public Task<Fila?> EjecutarAsync(string sp,params (string Nombre,object? Valor)[] parametros)
        {
            if(Fallar) throw new IOException("Fallo simulado antes de guardar metadatos");
            Doc["EnBoveda"]=true;
            Doc["RutaBoveda"]=parametros.Single(p=>p.Nombre=="@Ruta").Valor;
            return Task.FromResult<Fila?>(null);
        }
    }
    private class Entorno : IWebHostEnvironment
    {
        public string ApplicationName {get;set;}="Pruebas";
        public string EnvironmentName {get;set;}="Testing";
        public string ContentRootPath {get;set;}="";
        public string WebRootPath {get;set;}="";
        public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider();
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
    }
}
