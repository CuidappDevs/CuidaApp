using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CUIDAPP_API.Controllers;
using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.Interfaces.Auth;
using CUIDAPP_API.Services.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CUIDAPP_API.Tests;

public class TerminosRegistroTests
{
    private static IConfiguration Config => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Terminos:VersionVigente"] = "2026-11-16" }).Build();

    private sealed class GoogleDoble : IValidadorGoogle
    {
        public bool Invocado;
        public Task<GoogleIdentidad> ValidarAsync(string token)
        {
            Invocado = true;
            throw new GoogleAuthException("GOOGLE_TOKEN_INVALIDO", 401, "Token inválido.");
        }
    }

    [Theory]
    [InlineData(null, 400, "TERMINOS_REQUERIDOS")]
    [InlineData("", 400, "TERMINOS_REQUERIDOS")]
    [InlineData("   ", 400, "TERMINOS_REQUERIDOS")]
    [InlineData("2026-01-01", 409, "TERMINOS_DESACTUALIZADOS")]
    public async Task Registro_rechaza_version_sin_acceder_a_google_ni_SQL(string? version, int codigo, string motivo)
    {
        foreach (var cuidador in new[] { false, true })
        foreach (var google in new[] { false, true })
        {
            var validador = new GoogleDoble();
            var controller = new AuthController(new AuthService(Config, null!, validador), NullLogger<AuthController>.Instance);
            var resultado = Assert.IsType<ObjectResult>(cuidador
                ? await controller.RegisterCaregiver(new() { TerminosVersion = version, GoogleIdToken = google ? "token" : null })
                : await controller.RegisterClient(new() { TerminosVersion = version, GoogleIdToken = google ? "token" : null }));
            Assert.Equal(codigo, resultado.StatusCode);
            var json = JsonSerializer.SerializeToElement(resultado.Value);
            Assert.Equal(motivo, json.GetProperty("motivo").GetString());
            Assert.Equal(codigo == 400 ? "Debes aceptar los Términos y Condiciones." : "Hay una versión nueva de los Términos y Condiciones. Vuelve a abrir el registro.", json.GetProperty("message").GetString());
            Assert.Equal(2, json.EnumerateObject().Count());
            Assert.False(validador.Invocado);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Version_vigente_permite_continuar_el_registro(bool cuidador)
    {
        TerminosRegistro.Validar("2026-11-16", Config);
        var google = new GoogleDoble();
        var auth = new AuthService(Config, null!, google);
        var error = await Assert.ThrowsAsync<GoogleAuthException>(() => cuidador
            ? auth.RegisterCaregiverAsync(new() { TerminosVersion = "2026-11-16", GoogleIdToken = "token" })
            : auth.RegisterClientAsync(new() { TerminosVersion = "2026-11-16", GoogleIdToken = "token" }));
        Assert.True(google.Invocado);
        Assert.Equal("GOOGLE_TOKEN_INVALIDO", error.Motivo);
    }

    [Fact]
    public void Dtos_admiten_omision_pero_limitan_version_a_20()
    {
        foreach (object dto in new object[] { new RegisterClientDto(), new RegisterCaregiverDto() })
        {
            Assert.True(Validator.TryValidateObject(dto, new ValidationContext(dto), null, true));
            dto.GetType().GetProperty("TerminosVersion")!.SetValue(dto, new string('x', 21));
            Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), null, true));
        }
    }
}
