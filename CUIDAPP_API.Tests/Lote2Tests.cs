using System.Text.Json;
using CUIDAPP_API.Controllers;
using CUIDAPP_API.DTOs.Admin;
using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Interfaces.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace CUIDAPP_API.Tests;

public class Lote2Tests
{
    private const string Secreto = "Server=db-secreto;Password=hunter2";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // ---------- fakes ----------
    private sealed class FakeAdmin : IAdminService
    {
        public Func<AdminActionResult>? Accion;
        public int Llamadas;

        private AdminActionResult Responder()
        {
            Llamadas++;
            return Accion!();
        }

        public Task<AdminActionResult> SuspenderUsuarioAsync(int usuarioId, SuspenderCuidadorDto dto) => Task.FromResult(Responder());
        public Task<AdminActionResult> ReactivarUsuarioAsync(int usuarioId, ReactivarCuidadorDto dto) => Task.FromResult(Responder());
        public Task<IEnumerable<SancionCuidadorDto>> ObtenerSancionesAsync(int usuarioId)
            => throw new InvalidOperationException(Secreto);

        public Task<IEnumerable<CuidadorPendienteDto>> ObtenerCuidadoresPendientesAsync() => throw new InvalidOperationException(Secreto);
        public Task<IEnumerable<CuidadorAdminDto>> ObtenerCuidadoresAdminAsync(int? estado) => throw new NotImplementedException();
        public Task<CuidadorAdminDto?> ObtenerCuidadorAdminDetalleAsync(int usuarioId) => throw new NotImplementedException();
        public Task<IEnumerable<DocumentoDto>> ObtenerDocumentosPorCuidadorAsync(int cuidadorId) => throw new NotImplementedException();
        public Task<bool> ActualizarEstadoCuidadorAsync(ActualizarEstadoCuidadorDto dto) => throw new NotImplementedException();
        public Task<bool> MarcarPagoComoPagadoAsync(int pagoId) => throw new NotImplementedException();
        public Task<bool> ActualizarInfoCuidadorAsync(int usuarioId, ActualizarInfoCuidadorDto dto) => throw new NotImplementedException();
        public Task<IEnumerable<ClienteAdminDto>> ObtenerClientesAdminAsync(bool? activo) => throw new NotImplementedException();
        public Task<ClienteAdminDto?> ObtenerClienteAdminDetalleAsync(int usuarioId) => throw new NotImplementedException();
        public Task<bool> ActualizarInfoClienteAsync(int usuarioId, ActualizarInfoClienteDto dto) => throw new NotImplementedException();
        public Task<(int NuevoId, string Motivo)> CrearAdminAsync(CrearAdminDto dto) => throw new NotImplementedException();
        public Task<IEnumerable<AdminUsuarioDto>> ObtenerAdminsAsync() => throw new NotImplementedException();
    }

    private sealed class FakeAuth : IAuthService
    {
        public Func<LoginResult> Login = () => new LoginResult(LoginStatus.InvalidCredentials);
        public Task<LoginResult> LoginAsync(LoginRequestDto loginDto) => Task.FromResult(Login());
        public Task<int> RegisterClientAsync(RegisterClientDto registerDto) => throw new NotImplementedException();
        public Task<int> RegisterCaregiverAsync(RegisterCaregiverDto registerDto) => throw new NotImplementedException();
        public Task<(bool Success, Guid ResetToken, string Message)> ForgotPasswordAsync(ForgotPasswordDto dto) => throw new NotImplementedException();
        public Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto dto) => throw new NotImplementedException();
    }

    private static AdminController Admin(FakeAdmin fake) => new(fake, NullLogger<AdminController>.Instance);
    private static AuthController Auth(FakeAuth fake) => new(fake, NullLogger<AuthController>.Instance);

    private static AdminActionResult Resultado(string codigo)
        => new(codigo, "msg " + codigo, 7, 9, codigo == "APPLIED", "VIGENTE", null);

    private static SuspenderCuidadorDto SuspensionValida() => new() { AdminId = 1, Motivo = "Motivo suficientemente largo" };

    // ---------- validación de DTO ----------
    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(-3, 1, false)]
    [InlineData(5, 0, false)]
    [InlineData(5, -1, false)]
    [InlineData(5, 1, true)]
    public void Validar_ids_positivos(int usuarioId, int adminId, bool valido)
    {
        var dto = new SuspenderCuidadorDto { AdminId = adminId, Motivo = "Motivo suficientemente largo" };
        Assert.Equal(valido, dto.Validar(usuarioId, DateTimeOffset.UtcNow).Count == 0);
        Assert.Equal(valido, new ReactivarCuidadorDto { AdminId = adminId }.Validar(usuarioId).Count == 0);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("corto", false)]
    [InlineData("  nueve ch  ", false)]          // recortado = 9
    [InlineData("exactamente10", true)]
    public void Validar_motivo_recortado(string motivo, bool valido)
    {
        var dto = new SuspenderCuidadorDto { AdminId = 1, Motivo = motivo };
        Assert.Equal(valido, dto.Validar(1, DateTimeOffset.UtcNow).Count == 0);
    }

    [Fact]
    public void Validar_motivo_largo()
    {
        var ahora = DateTimeOffset.UtcNow;
        Assert.Empty(new SuspenderCuidadorDto { AdminId = 1, Motivo = new string('a', 500) }.Validar(1, ahora));
        Assert.Contains("motivo", new SuspenderCuidadorDto { AdminId = 1, Motivo = new string('a', 501) }.Validar(1, ahora).Keys);
    }

    [Fact]
    public void Validar_fecha_fin_temporal()
    {
        var ahora = DateTimeOffset.UtcNow;
        var dto = SuspensionValida();

        dto.FechaFinUtc = null;                                  // indefinida
        Assert.Empty(dto.Validar(1, ahora));
        dto.FechaFinUtc = ahora.AddMinutes(4);                   // menos de 5 minutos
        Assert.Contains("fechaFinUtc", dto.Validar(1, ahora).Keys);
        dto.FechaFinUtc = ahora.AddDays(-1);                     // pasada
        Assert.Contains("fechaFinUtc", dto.Validar(1, ahora).Keys);
        dto.FechaFinUtc = ahora.AddMinutes(6);
        Assert.Empty(dto.Validar(1, ahora));
        // con offset: instante equivalente, se compara en UTC
        dto.FechaFinUtc = ahora.AddHours(2).ToOffset(TimeSpan.FromHours(-4));
        Assert.Empty(dto.Validar(1, ahora));
    }

    [Fact]
    public async Task Dto_invalido_devuelve_400_sin_llamar_al_servicio()
    {
        var fake = new FakeAdmin { Accion = () => Resultado("APPLIED") };
        var r = Assert.IsAssignableFrom<ObjectResult>(await Admin(fake).SuspenderCuidador(5, new SuspenderCuidadorDto { AdminId = 0, Motivo = "" }));
        Assert.Equal(400, r.StatusCode);
        Assert.Equal("VALIDATION_ERROR", Assert.IsType<ApiErrorDto>(r.Value).Code);
        Assert.Equal(0, fake.Llamadas);
    }

    // ---------- mapeo de códigos administrativos ----------
    [Theory]
    [InlineData("APPLIED", 200, "APPLIED")]
    [InlineData("ALREADY_COMPLETED", 200, "ALREADY_COMPLETED")]
    [InlineData("INVALID_REASON", 400, "INVALID_REASON")]
    [InlineData("INVALID_END_DATE", 400, "INVALID_END_DATE")]
    [InlineData("ADMIN_ROLE_REQUIRED", 403, "ADMIN_ROLE_REQUIRED")]
    [InlineData("USER_NOT_FOUND", 404, "USER_NOT_FOUND")]
    [InlineData("ALREADY_SUSPENDED", 409, "ALREADY_SUSPENDED")]
    [InlineData("ALREADY_ACTIVE", 409, "ALREADY_ACTIVE")]
    [InlineData("STATE_CONFLICT", 409, "SANCTION_STATE_CONFLICT")]
    public async Task Mapea_codigo_de_dominio_a_http(string codigo, int http, string codigoApi)
    {
        var fake = new FakeAdmin { Accion = () => Resultado(codigo) };
        var suspender = Assert.IsAssignableFrom<ObjectResult>(await Admin(fake).SuspenderCuidador(5, SuspensionValida()));
        var reactivar = Assert.IsAssignableFrom<ObjectResult>(await Admin(fake).ReactivarCuidador(5, new ReactivarCuidadorDto { AdminId = 1 }));

        foreach (var r in new[] { suspender, reactivar })
        {
            Assert.Equal(http, r.StatusCode ?? 200);
            if (http == 200)
                Assert.Equal(codigoApi, Assert.IsType<AdminActionResult>(r.Value).Code);
            else
                Assert.Equal(codigoApi, Assert.IsType<ApiErrorDto>(r.Value).Code);
        }
    }

    [Fact]
    public async Task Rutas_de_clientes_y_administradores_reutilizan_la_misma_logica()
    {
        var fake = new FakeAdmin { Accion = () => Resultado("ALREADY_SUSPENDED") };
        var c = Admin(fake);
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(await c.SuspenderCliente(5, SuspensionValida())).StatusCode);
        Assert.Equal(409, Assert.IsAssignableFrom<ObjectResult>(await c.SuspenderAdmin(5, SuspensionValida())).StatusCode);
    }

    // ---------- 500 sin exposición ----------
    [Fact]
    public async Task Error_inesperado_en_admin_devuelve_500_generico()
    {
        var fake = new FakeAdmin { Accion = () => throw new InvalidOperationException(Secreto) };
        var r = Assert.IsAssignableFrom<ObjectResult>(await Admin(fake).SuspenderCuidador(5, SuspensionValida()));
        Assert.Equal(500, r.StatusCode);
        Assert.Equal("INTERNAL_ERROR", Assert.IsType<ApiErrorDto>(r.Value).Code);
        Assert.DoesNotContain("hunter2", JsonSerializer.Serialize(r.Value, Web));

        // un endpoint administrativo ajeno al lote tampoco filtra el mensaje
        var otro = Assert.IsAssignableFrom<ObjectResult>(await Admin(fake).ObtenerCuidadoresPendientes());
        Assert.Equal(500, otro.StatusCode);
        Assert.DoesNotContain("hunter2", JsonSerializer.Serialize(otro.Value, Web));
    }

    // ---------- login ----------
    private static LoginRequestDto Peticion() => new() { Email = "a@b.c", Password = "x" };

    [Fact]
    public async Task Login_200_conserva_AuthResponseDto()
    {
        var auth = new AuthResponseDto { Token = "t", Email = "a@b.c", UserId = 1, RolId = 2 };
        var fake = new FakeAuth { Login = () => new LoginResult(LoginStatus.Success, auth) };
        var r = Assert.IsType<OkObjectResult>(await Auth(fake).Login(Peticion()));
        Assert.Same(auth, r.Value);
    }

    [Fact]
    public async Task Login_401_credenciales_invalidas()
    {
        var r = Assert.IsAssignableFrom<ObjectResult>(await Auth(new FakeAuth()).Login(Peticion()));
        Assert.Equal(401, r.StatusCode);
        var error = Assert.IsType<ApiErrorDto>(r.Value);
        Assert.Equal("INVALID_CREDENTIALS", error.Code);
        Assert.Null(error.Suspension);
    }

    [Fact]
    public async Task Login_423_temporal_incluye_vigencia()
    {
        var fin = new DateTimeOffset(2030, 1, 2, 15, 0, 0, TimeSpan.Zero);
        var fake = new FakeAuth { Login = () => new LoginResult(LoginStatus.AccountSuspended, null, new SuspensionInfoDto("motivo", "TEMPORAL", fin)) };
        var r = Assert.IsAssignableFrom<ObjectResult>(await Auth(fake).Login(Peticion()));
        Assert.Equal(423, r.StatusCode);
        var error = Assert.IsType<ApiErrorDto>(r.Value);
        Assert.Equal("ACCOUNT_SUSPENDED", error.Code);
        Assert.Equal("TEMPORAL", error.Suspension!.Tipo);
        Assert.Equal(fin, error.Suspension.FechaFinUtc);
    }

    [Fact]
    public async Task Login_423_indefinida_sin_fecha_fin()
    {
        var fake = new FakeAuth { Login = () => new LoginResult(LoginStatus.AccountSuspended, null, new SuspensionInfoDto("motivo", "INDEFINIDA", null)) };
        var r = Assert.IsAssignableFrom<ObjectResult>(await Auth(fake).Login(Peticion()));
        Assert.Equal(423, r.StatusCode);
        Assert.Null(Assert.IsType<ApiErrorDto>(r.Value).Suspension!.FechaFinUtc);
    }

    [Fact]
    public async Task Login_423_inactivo_sin_motivo_inventado()
    {
        var fake = new FakeAuth { Login = () => new LoginResult(LoginStatus.AccountInactive) };
        var r = Assert.IsAssignableFrom<ObjectResult>(await Auth(fake).Login(Peticion()));
        Assert.Equal(423, r.StatusCode);
        var error = Assert.IsType<ApiErrorDto>(r.Value);
        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
        Assert.Null(error.Suspension);
    }

    [Fact]
    public async Task Login_400_dto_invalido()
    {
        var r = Assert.IsAssignableFrom<ObjectResult>(await Auth(new FakeAuth()).Login(new LoginRequestDto { Email = " ", Password = "" }));
        Assert.Equal(400, r.StatusCode);
        Assert.Equal("VALIDATION_ERROR", Assert.IsType<ApiErrorDto>(r.Value).Code);
    }

    [Fact]
    public async Task Login_500_generico_sin_mensaje_de_la_excepcion()
    {
        var fake = new FakeAuth { Login = () => throw new InvalidOperationException(Secreto) };
        var r = Assert.IsAssignableFrom<ObjectResult>(await Auth(fake).Login(Peticion()));
        Assert.Equal(500, r.StatusCode);
        Assert.Equal("INTERNAL_ERROR", Assert.IsType<ApiErrorDto>(r.Value).Code);
        Assert.DoesNotContain("hunter2", JsonSerializer.Serialize(r.Value, Web));
    }

    // ---------- contratos JSON que consumen MAUI y panel ----------
    [Fact]
    public void Json_error_de_login_tiene_la_forma_del_contrato()
    {
        var fin = new DateTimeOffset(2030, 1, 2, 15, 0, 0, TimeSpan.Zero);
        var json = JsonSerializer.Serialize(
            new ApiErrorDto("ACCOUNT_SUSPENDED", "Tu cuenta está suspendida.", null, new SuspensionInfoDto("m", "TEMPORAL", fin)), Web);
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;
        Assert.Equal("ACCOUNT_SUSPENDED", raiz.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, raiz.GetProperty("errors").ValueKind);
        var s = raiz.GetProperty("suspension");
        Assert.Equal("TEMPORAL", s.GetProperty("tipo").GetString());
        Assert.Equal(fin, s.GetProperty("fechaFinUtc").GetDateTimeOffset());
    }

    [Fact]
    public void Json_exito_administrativo_siempre_incluye_todos_los_campos()
    {
        var json = JsonSerializer.Serialize(new AdminActionResult("APPLIED", "Operación aplicada.", 123, 456, false, "VIGENTE", null), Web);
        using var doc = JsonDocument.Parse(json);
        foreach (var campo in new[] { "code", "message", "usuarioId", "sancionId", "isActive", "estadoSancion", "fechaFinUtc" })
            Assert.True(doc.RootElement.TryGetProperty(campo, out _), campo);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("fechaFinUtc").ValueKind);

        var vuelta = JsonSerializer.Deserialize<AdminActionResult>(json, Web)!;
        Assert.Equal(456, vuelta.SancionId);
        Assert.False(vuelta.IsActive);
    }

    [Fact]
    public void Json_sancion_roundtrip_con_fechas_utc()
    {
        var dto = new SancionCuidadorDto
        {
            Id = 1, UsuarioId = 2, AdminId = 3, AdminNombre = "A", Accion = "SUSPENSION", Motivo = "m", Tipo = "TEMPORAL",
            Estado = "REVOCADA", FechaInicioUtc = DateTimeOffset.UtcNow, FechaFinUtc = DateTimeOffset.UtcNow.AddDays(1),
            RevocadaPorAdminId = 3, RevocadaPorAdminNombre = "A", FechaRevocacionUtc = DateTimeOffset.UtcNow, EstaVigente = false
        };
        var vuelta = JsonSerializer.Deserialize<SancionCuidadorDto>(JsonSerializer.Serialize(dto, Web), Web)!;
        Assert.Equal(dto.FechaFinUtc, vuelta.FechaFinUtc);
        Assert.Equal("REVOCADA", vuelta.Estado);
        Assert.False(vuelta.EstaVigente);
    }

    [Fact]
    public void Mensaje_de_resultado_cubre_todos_los_codigos()
    {
        foreach (var codigo in new[] { "APPLIED", "ALREADY_COMPLETED", "INVALID_REASON", "INVALID_END_DATE", "ADMIN_ROLE_REQUIRED",
                                       "USER_NOT_FOUND", "ALREADY_SUSPENDED", "ALREADY_ACTIVE", "STATE_CONFLICT" })
            Assert.NotEqual("Resultado no reconocido.", CUIDAPP_API.Services.Admin.AdminService.MensajeResultado(codigo));
    }
}
