using System.Security.Claims;
using CUIDAPP_API.Controllers;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.DTOs.Contrato;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace CUIDAPP_API.Tests;

public class ContratoIdentidadTests
{
    [Theory]
    [InlineData(null, false, 401)]
    [InlineData("7", false, 401)]
    [InlineData("8", true, 403)]
    [InlineData("invalid", true, 403)]
    [InlineData(null, true, 403)]
    public async Task Rechaza_suplantacion_antes_de_acceder_a_la_base(string? id, bool autenticado, int esperado)
    {
        var identity = new ClaimsIdentity(id == null ? [] : new[] { new Claim(ClaimTypes.NameIdentifier, id) }, autenticado ? "Bearer" : null);
        // Dependencias nulas: cualquier acceso a BD o firma haría fallar la prueba.
        var controller = new ContratoController(null!, null!, NullLogger<ContratoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
        };
        var acciones = new Func<Task<IActionResult>>[]
        {
            () => controller.ResumenMensual(1, 7, DateTime.Today),
            () => controller.Anexos(1, 7),
            () => controller.ProponerAnexo(1, new() { UsuarioId = 7 }),
            () => controller.ResponderAnexo(1, new() { UsuarioId = 7 }),
            () => controller.CodigoAnexo(1, new() { UsuarioId = 7 }),
            () => controller.FirmarAnexo(1, new() { UsuarioId = 7 }),
            () => controller.Renovar(1, new() { UsuarioId = 7 }),
            () => controller.Reemplazos(1, 1, 7, null!),
            () => controller.Reemplazar(1, 1, 9, new() { UsuarioId = 7 }, null!, null!),
            () => controller.Permiso(7),
            () => controller.SolicitarPermiso(new() { ClienteId = 7 }),
            () => controller.SolicitarDomicilio(new() { ClienteId = 7 }),
            () => controller.ConfigCuidador(7),
            () => controller.GuardarConfigCuidador(7, new()),
            () => controller.PuedeProponer(7, 9),
            () => controller.Proponer(new() { ClienteId = 7 }),
            () => controller.CrearInvitacion(new() { ClienteId = 7 }),
            () => controller.Invitaciones(7),
            () => controller.CancelarInvitacion(1, new() { ClienteId = 7 }),
            () => controller.Responder(1, new() { UsuarioId = 7 }),
            () => controller.PorUsuario(7),
            () => controller.Detalle(1, 7),
            () => controller.Codigo(1, new() { UsuarioId = 7 }),
            () => controller.Firmar(1, new() { UsuarioId = 7 }),
            () => controller.Ausencia(1, new() { UsuarioId = 7 }),
            () => controller.QuitarAusencia(1, 7),
            () => controller.CambiarEstado(1, new() { UsuarioId = 7 }),
            () => controller.EnlacePdf(1, 7)
        };
        foreach (var accion in acciones)
        {
            var result = Assert.IsAssignableFrom<ObjectResult>(await accion());
            Assert.Equal(esperado, result.StatusCode);
            Assert.IsType<ApiErrorDto>(result.Value);
        }
    }
}
