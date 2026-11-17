using System.Text.Json;
using CUIDAPP_API.Controllers;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.Interfaces.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace CUIDAPP_API.Tests;

public class RevisionDocumentosTests
{
    private sealed class Db(string resultado) : IAdminOperacionesService
    {
        public (string Nombre, object? Valor)[] Parametros = [];
        public Task<Fila?> EjecutarAsync(string sp, params (string Nombre, object? Valor)[] parametros)
        {
            Parametros = parametros;
            return Task.FromResult<Fila?>(new() { ["Resultado"] = resultado });
        }
        public Task<List<List<Fila>>> ConsultarAsync(string sp, params (string Nombre, object? Valor)[] parametros)
            => Task.FromResult(new List<List<Fila>> { new() { new() { ["Resultado"] = resultado } },
                new() { new() { ["TipoDocumento"] = "Cedula (sin fecha)" } } });
    }

    [Theory]
    [InlineData("FECHA_VENCIMIENTO_REQUERIDA", "Pon la fecha de vencimiento del documento antes de aprobarlo.")]
    [InlineData("FECHA_VENCIMIENTO_INVALIDA", "La fecha de vencimiento debe ser posterior a hoy.")]
    public async Task Revision_y_fijacion_traducen_reglas_SQL_a_400(string motivo, string mensaje)
    {
        var db = new Db(motivo);
        var c = new AdminGestionController(db, null!, null!, NullLogger<AdminGestionController>.Instance);
        foreach (var revisar in new[] { true, false })
        {
            var r = Assert.IsType<BadRequestObjectResult>(revisar
                ? await c.RevisarDocumento(1, new() { Aprobar = true })
                : await c.FijarVencimiento(1, new()));
            var json = JsonSerializer.SerializeToElement(r.Value);
            Assert.Equal(motivo, json.GetProperty("motivo").GetString());
            Assert.Equal(mensaje, json.GetProperty("message").GetString());
            Assert.Contains(db.Parametros, p => p.Nombre == "@Hoy" && p.Valor is DateTime);
        }
    }

    [Fact]
    public async Task Rechazo_exige_motivo()
    {
        var db = new Db("OK");
        var c = new AdminGestionController(db, null!, null!, NullLogger<AdminGestionController>.Instance);
        var r = Assert.IsType<BadRequestObjectResult>(await c.RevisarDocumento(1, new() { Aprobar = false, Observaciones = "  " }));
        Assert.Equal("MOTIVO_REQUERIDO", JsonSerializer.SerializeToElement(r.Value).GetProperty("motivo").GetString());
        Assert.Empty(db.Parametros);
    }

    [Fact]
    public async Task Cuenta_bloqueada_conserva_lista_de_documentos()
    {
        var c = new AdminController(null!, new Db("DOCUMENTOS_SIN_REVISAR"), null!, NullLogger<AdminController>.Instance);
        var r = Assert.IsType<ConflictObjectResult>(await c.ActualizarEstadoCuidador(new() { CuidadorId = 1, NuevoEstado = 2 }));
        var json = JsonSerializer.SerializeToElement(r.Value);
        Assert.Equal("DOCUMENTOS_SIN_REVISAR", json.GetProperty("motivo").GetString());
        Assert.Equal("Cedula (sin fecha)", json.GetProperty("faltantes")[0].GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Estatus_inactivo_se_rechaza_incluso_para_nacional(bool extranjero)
    {
        var ex = Assert.Throws<MigratorioException>(() => DocumentosMigratorios.ValidarEstatus(5, extranjero, false));
        Assert.Equal("ESTATUS_MIGRATORIO_INVALIDO", ex.Code);
        DocumentosMigratorios.ValidarEstatus(5, extranjero, true);
    }
}
