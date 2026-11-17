using System.Text.Json;
using CUIDAPP_API.Controllers;
using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.Interfaces.Auth;
using CUIDAPP_API.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CUIDAPP_API.Tests;

public class GoogleAuthTests
{
    private static readonly GoogleIdentidad Identidad = new("google-sub", "persona@example.com", true, "Persona", "https://example.com/foto");
    private sealed class ValidadorDoble : IValidadorGoogle
    {
        public GoogleIdentidad Identidad = GoogleAuthTests.Identidad;
        public GoogleAuthException? Error;
        public string? Token;
        public Task<GoogleIdentidad> ValidarAsync(string token)
        {
            Token = token;
            if (Error != null) throw Error;
            return Task.FromResult(Identidad);
        }
    }

    // Sustituye únicamente el acceso SQL y la emisión del JWT; la validación,
    // comprobación del correo y orquestación son las del servicio real.
    private sealed class AuthDoble(ValidadorDoble validador) : AuthService(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Terminos:VersionVigente"] = "2026-11-16" }).Build(), null!, validador)
    {
        public string? CorreoResuelto = "persona@example.com";
        public GoogleIdentidad? Consultada;
        public LoginRequestDto? LoginRecibido;
        public bool UsoGoogle;
        public LoginResult Resultado = new(LoginStatus.Success, new AuthResponseDto
        {
            Token = "jwt", Email = "persona@example.com", UserId = 7, RolId = 2
        });
        protected override Task<string?> ResolverGoogleAsync(GoogleIdentidad identidad)
        {
            Consultada = identidad;
            return Task.FromResult(CorreoResuelto);
        }
        protected override Task<LoginResult> LoginCoreAsync(LoginRequestDto dto, bool google)
        {
            LoginRecibido = dto;
            UsoGoogle = google;
            return Task.FromResult(Resultado);
        }
    }
    private static AuthController Controller(AuthDoble auth) => new(auth, NullLogger<AuthController>.Instance);
    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public async Task Login_existente_usa_identidad_validada_y_respuesta_comun()
    {
        var validador = new ValidadorDoble();
        var auth = new AuthDoble(validador);
        var result = Assert.IsType<OkObjectResult>(await Controller(auth).Google(new("id-token")));
        Assert.Same(auth.Resultado.AuthResponse, result.Value);
        Assert.Equal("id-token", validador.Token);
        Assert.Equal(Identidad, auth.Consultada);
        Assert.True(auth.UsoGoogle);
    }

    [Fact]
    public async Task Enlace_por_correo_usa_correo_canonico_de_la_cuenta()
    {
        var auth = new AuthDoble(new()) { CorreoResuelto = "Persona@Example.COM" };
        await auth.LoginGoogleAsync("token");
        Assert.Equal("Persona@Example.COM", auth.LoginRecibido!.Email);
        Assert.Equal(Identidad.Sub, auth.Consultada!.Sub);
        Assert.Empty(auth.LoginRecibido.Password);
    }

    [Fact]
    public async Task Registro_requerido_devuelve_datos_del_token()
    {
        var auth = new AuthDoble(new()) { CorreoResuelto = null };
        var result = Assert.IsType<ObjectResult>(await Controller(auth).Google(new("token")));
        Assert.Equal(404, result.StatusCode);
        var json = Json(result.Value);
        Assert.Equal("REGISTRO_REQUERIDO", json.GetProperty("motivo").GetString());
        Assert.Equal(Identidad.Email, json.GetProperty("email").GetString());
        Assert.Equal(Identidad.Nombre, json.GetProperty("nombre").GetString());
        Assert.Equal(Identidad.FotoUrl, json.GetProperty("fotoUrl").GetString());
        Assert.Null(auth.LoginRecibido);
    }

    [Fact]
    public async Task Token_invalido_no_consulta_cuenta()
    {
        var auth = new AuthDoble(new() { Error = new("GOOGLE_TOKEN_INVALIDO", 401, "Token inválido.") });
        var result = Assert.IsType<ObjectResult>(await Controller(auth).Google(new("token")));
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("GOOGLE_TOKEN_INVALIDO", Json(result.Value).GetProperty("motivo").GetString());
        Assert.Null(auth.Consultada);
    }

    [Fact]
    public async Task Correo_no_verificado_no_consulta_cuenta()
    {
        var auth = new AuthDoble(new() { Identidad = Identidad with { EmailVerified = false } });
        var result = Assert.IsType<ObjectResult>(await Controller(auth).Google(new("token")));
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("GOOGLE_CORREO_NO_VERIFICADO", Json(result.Value).GetProperty("motivo").GetString());
        Assert.Null(auth.Consultada);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registro_correo_distinto_falla_antes_de_SQL(bool cuidador)
    {
        var auth = new AuthDoble(new());
        var controller = Controller(auth);
        var result = Assert.IsType<ObjectResult>(cuidador
            ? await controller.RegisterCaregiver(new() { TerminosVersion = "2026-11-16", Email = "otra@example.com", GoogleIdToken = "token" })
            : await controller.RegisterClient(new() { TerminosVersion = "2026-11-16", Email = "otra@example.com", GoogleIdToken = "token" }));
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("GOOGLE_CORREO_DISTINTO", Json(result.Value).GetProperty("motivo").GetString());
    }

    [Theory]
    [InlineData(LoginStatus.AccountSuspended, 423)]
    [InlineData(LoginStatus.AccountInactive, 423)]
    [InlineData(LoginStatus.AccountDeleting, 403)]
    [InlineData(LoginStatus.InvalidCredentials, 401)]
    public async Task Google_reutiliza_los_resultados_del_login(LoginStatus status, int codigo)
    {
        var auth = new AuthDoble(new()) { Resultado = new(status, FechaEjecucion: new DateTime(2026,12,1), UsuarioId: 7) };
        var result = Assert.IsType<ObjectResult>(await Controller(auth).Google(new("token")));
        Assert.Equal(codigo, result.StatusCode);
        var esperado = Assert.IsType<ObjectResult>(AuthController.TraducirLogin(auth.Resultado));
        Assert.Equal(Json(esperado.Value).GetRawText(), Json(result.Value).GetRawText());
    }
}
