using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CUIDAPP_API.Controllers;
using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.DTOs.Busqueda;
using CUIDAPP_API.DTOs.Contrato;
using CUIDAPP_API.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CUIDAPP_API.Tests;

public class ParcialesPilotoTests
{
    [Fact]
    public void Documentos_exigen_archivo_y_comparan_sin_distinguir_mayusculas()
    {
        var documentos = new[] {
            new DocumentoExtraDto { TipoDocumento = " carnet pnre ", UrlArchivo = "uploads/pnre.jpg" },
            new DocumentoExtraDto { TipoDocumento = "Visa de trabajo", UrlArchivo = " " }
        };
        Assert.Equal(new[] { "Visa de trabajo", "Carnet de residencia" },
            DocumentosMigratorios.Faltantes(["Carnet PNRE", "Visa de trabajo", "Carnet de residencia"], documentos));
        Assert.Equal(new[] { "Carnet PNRE" }, DocumentosMigratorios.Faltantes(["Carnet PNRE"], null));
    }

    [Fact]
    public void Checklist_limites_y_reemplazo_por_lista_vacia()
    {
        Assert.True(ContratoController.TareasValidas(null));
        Assert.True(ContratoController.TareasValidas([]));
        Assert.True(ContratoController.TareasValidas(Enumerable.Range(0, 20).Select(_ => new TareaContratoDto { Descripcion = new string('a', 200) }).ToList()));
        Assert.False(ContratoController.TareasValidas(Enumerable.Range(0, 21).Select(_ => new TareaContratoDto { Descripcion = "a" }).ToList()));
        foreach (var descripcion in new[] { "", "  ", new string('a', 201) })
            Assert.False(ContratoController.TareasValidas([new() { Descripcion = descripcion }]));
        Assert.False(ContratoController.TareasValidas([null!]));
    }

    [Fact]
    public void Prioriza_conocidos_bien_calificados_y_conserva_distancia_en_empates()
    {
        CuidadorCercanoDto C(int id, bool vino, decimal? nota) => new() { Id = id, YaVino = vino,
            CalificacionConCliente = nota, NombreCompleto = "Test", Especialidad = "Test" };
        var resultado = ContratoController.OrdenarReemplazos([C(1, false, null), C(2, true, 3), C(3, true, 4), C(4, true, 5), C(5, true, null), C(6, false, 5)]);
        Assert.Equal(new[] { 3, 4, 2, 5, 1, 6 }, resultado.Select(c => c.Id));
    }

    [Theory]
    [InlineData(PoliticasAdmin.Verificacion, "1,2,4")]
    [InlineData(PoliticasAdmin.Soporte, "1,2,5")]
    [InlineData(PoliticasAdmin.Legal, "1,6")]
    [InlineData(PoliticasAdmin.AuditoriaLegal, "1,6")]
    [InlineData(PoliticasAdmin.Operaciones, "1,2")]
    [InlineData(PoliticasAdmin.Finanzas, "1,3")]
    [InlineData(PoliticasAdmin.SuperAdmin, "1")]
    [InlineData(PoliticasAdmin.Admin, "1,2,3,4,5,6")]
    public async Task Politicas_restringen_nivel_y_rol(string policyName, string permitidos)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSeguridadAdmin(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        for (var nivel = 1; nivel <= 6; nivel++)
        {
            foreach (var rol in new[] { "1", "2", "3" })
            {
                var user = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.Role, rol), new(PoliticasAdmin.ClaimNivel, nivel.ToString())], "test"));
                Assert.Equal(rol == "1" && permitidos.Split(',').Contains(nivel.ToString()), (await auth.AuthorizeAsync(user, null, policyName)).Succeeded);
            }
        }
        Assert.False((await auth.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policyName)).Succeeded);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(6, true)]
    [InlineData(0, false)]
    [InlineData(7, false)]
    public void Validacion_nivel_permite_uno_a_seis(int nivel, bool esperado)
    {
        var dto = new CUIDAPP_API.DTOs.AdminOperaciones.NivelAdminDto { Nivel = nivel };
        Assert.Equal(esperado, Validator.TryValidateObject(dto, new ValidationContext(dto), [], true));
    }
}
