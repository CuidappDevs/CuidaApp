using System.Text.Json;
using CUIDAPP_API.Controllers;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.DTOs.Cuenta;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Interfaces.Cuenta;
using CUIDAPP_API.Services.Cuenta;
using CUIDAPP_API.Services.Realtime;
using Microsoft.AspNetCore.Mvc;

namespace CUIDAPP_API.Tests;

public class DeudaEliminacionTests
{
    [Theory]
    [InlineData("{}", false)]
    [InlineData("{\"monto\":null}", false)]
    [InlineData("{\"monto\":0}", true)]
    [InlineData("{\"monto\":250.50}", true)]
    public void Ajustar_exige_monto_explicito_para_evitar_condonar_por_omision(string body, bool valido)
    {
        var dto = JsonSerializer.Deserialize<AjustarDeudaDto>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var errores = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.Equal(valido, System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            dto, new System.ComponentModel.DataAnnotations.ValidationContext(dto), errores, true));
    }

    [Theory]
    [InlineData(null,true)]
    [InlineData("0",true)]
    [InlineData("100.25",true)]
    [InlineData("-0.01",false)]
    [InlineData("1.001",false)]
    [InlineData("100000000",false)]
    [InlineData("79228162514264337593543950335",false)]
    public void Rechaza_montos_imposibles_antes_de_enviarlos_a_sql(string? monto,bool valido)
        => Assert.Equal(valido,CUIDAPP_API.Services.Trabajo.TrabajoService.MontoRecibidoValido(monto==null?null:decimal.Parse(monto,System.Globalization.CultureInfo.InvariantCulture)));

    private static JsonElement Json(object? value)=>JsonSerializer.SerializeToElement(value,new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public async Task Suma_pendientes_y_revision_sin_sumar_historial_ni_exponer_datos_privados()
    {
        Fila Deuda(int estado,decimal monto)=>new() { ["Id"]=estado,["TrabajoId"]=10,["FechaServicio"]=DateTime.Today,
            ["NombreCuidador"]="Test",["MontoTotal"]=100m,["MontoRecibido"]=20m,["Monto"]=monto,["Estado"]=estado,
            ["ComprobanteUrl"]=null,["NotaAdmin"]=null,["FechaCreacion"]=DateTime.Today,["CorreoCliente"]="privado@example.test",["FechaLimite"]=DateTime.Today.AddDays(1),["Vencida"]=true };
        var db=new Db { Filas=[[Deuda(1,80),Deuda(3,20),Deuda(2,50),Deuda(4,40)]] };
        var result=Assert.IsType<OkObjectResult>(await new DeudaClienteController(db,new Notifier()).Cliente(1));
        var json=Json(result.Value);
        Assert.Equal(100m,json.GetProperty("totalPendiente").GetDecimal());
        Assert.True(json.GetProperty("bloqueado").GetBoolean());
        Assert.False(json.GetProperty("deudas")[0].TryGetProperty("CorreoCliente",out _));
        Assert.Equal(13,json.GetProperty("deudas")[0].EnumerateObject().Count());
        Assert.True(json.GetProperty("deudas")[0].GetProperty("vencida").GetBoolean());
        Assert.Equal(DateTime.Today.AddDays(1),json.GetProperty("deudas")[0].GetProperty("fechaLimite").GetDateTime());
    }

    [Fact]
    public async Task No_tener_deuda_devuelve_204()
        => Assert.IsType<NoContentResult>(await new DeudaClienteController(new Db(),new Notifier()).Trabajo(20));

    [Fact]
    public async Task Cobro_pendiente_es_aviso_y_no_bloqueo()
    {
        var servicio=new Cuentas { Revisar=[[new Fila { ["UsuarioId"]=2 }],
            [new Fila { ["Codigo"]="PAGO_POR_COBRAR",["Detalle"]="Pendiente",["ReferenciaId"]=10,["Bloquea"]=false }],[]] };
        var result=Assert.IsType<OkObjectResult>(await new EliminarCuentaController(servicio).Revisar(2));
        Assert.True(Json(result.Value).GetProperty("puedeEliminar").GetBoolean());
    }

    [Fact]
    public async Task Bloqueo_impide_enviar_codigo_y_devuelve_detalle()
    {
        var servicio=new Cuentas { Revisar=[[new Fila { ["UsuarioId"]=2 }],
            [new Fila { ["Codigo"]="DEUDA_CLIENTE",["Detalle"]="Deuda",["ReferenciaId"]=10,["Bloquea"]=true }],[]] };
        var result=Assert.IsType<ConflictObjectResult>(await new EliminarCuentaController(servicio).Codigo(new() { UsuarioId=2 }));
        Assert.False(servicio.Envio);
        Assert.Equal("DEUDA_CLIENTE",Json(result.Value).GetProperty("bloqueos")[0].GetProperty("codigo").GetString());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(3, false)]
    [InlineData(2, true)]
    public async Task Cancelar_exige_credenciales_y_no_permite_cambiar_otro_usuario(int? credencial,bool permite)
    {
        var servicio=new Cuentas { Identidad=credencial };
        var result=await new EliminarCuentaController(servicio).Cancelar(new CancelarEliminacionDto { UsuarioId=2,Correo="a@example.test",Contrasena="test" });
        Assert.Equal(permite,servicio.Cancelo);
        if(!permite) Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void Login_en_eliminacion_revela_solo_datos_para_recuperar()
    {
        var r=Assert.IsType<ObjectResult>(AuthController.TraducirLogin(new(LoginStatus.AccountDeleting,FechaEjecucion:new DateTime(2026,12,1),UsuarioId:7)));
        Assert.Equal(403,r.StatusCode);
        var json=Json(r.Value);
        Assert.Equal("CUENTA_EN_ELIMINACION",json.GetProperty("motivo").GetString());
        Assert.Equal(7,json.GetProperty("usuarioId").GetInt32());
        Assert.False(json.TryGetProperty("token",out _));
    }

    [Theory]
    [InlineData("/uploads/../privado/contrato.pdf",false)]
    [InlineData("/uploads/%2e%2e/privado/contrato.pdf",false)]
    [InlineData("/uploads/../../uploads-otro/dato.pdf",false)]
    [InlineData("/privado/contrato.pdf",false)]
    [InlineData("/uploads/usuarios/2/doc.pdf",true)]
    [InlineData("https://api.example.test/uploads/usuarios/2/doc.pdf?x=1",true)]
    public void Borrado_no_sale_de_uploads(string url,bool permitido)
    {
        var root=Path.Combine(Path.GetTempPath(),"cuidapp-pruebas","uploads");
        Assert.Equal(permitido,EliminacionCuentasService.RutaUpload(root,url)!=null);
    }

    private sealed class Db : IAdminOperacionesService
    {
        public List<List<Fila>> Filas { get; set; }=[[]];
        public Task<List<List<Fila>>> ConsultarAsync(string sp,params (string Nombre,object? Valor)[] parametros)=>Task.FromResult(Filas);
        public Task<Fila?> EjecutarAsync(string sp,params (string Nombre,object? Valor)[] parametros)=>Task.FromResult<Fila?>(null);
    }
    private sealed class Notifier : ITrabajoNotifier
    {
        public Task NotificarAsync(int usuarioId,string evento,object payload)=>Task.CompletedTask;
        public Task NotificarGlobalAsync(string evento,object payload)=>Task.CompletedTask;
    }
    private sealed class Cuentas : IEliminarCuentaService
    {
        public List<List<Fila>> Revisar { get; set; }=[[],[],[]];
        public int? Identidad { get; set; }
        public bool Envio { get; private set; }
        public bool Cancelo { get; private set; }
        public Task<List<List<Fila>>> RevisarAsync(int usuarioId)=>Task.FromResult(Revisar);
        public Task<int?> ValidarCredencialesAsync(string correo,string contrasena)=>Task.FromResult(Identidad);
        public Task<Fila?> EnviarCodigoAsync(int usuarioId,string origen) { Envio=true;return Task.FromResult<Fila?>(new() { ["Resultado"]="OK" }); }
        public Task<Fila?> ConfirmarAsync(int usuarioId,string codigo,string? motivo,string origen)=>throw new NotImplementedException();
        public Task<Fila?> CancelarAsync(int? usuarioId,int? solicitudId,string por,string? nota) { Cancelo=true;return Task.FromResult<Fila?>(new() { ["Resultado"]="OK" }); }
    }
}
