using System.Text.Json;
using CUIDAPP_API.DTOs.Busqueda;
using CUIDAPP_API.DTOs.Contrato;
using CUIDAPP_API.Interfaces.Busqueda;
using Microsoft.AspNetCore.Mvc;

namespace CUIDAPP_API.Controllers;

public partial class ContratoController
{
    public static bool TareasValidas(List<TareaContratoDto>? tareas) => tareas == null ||
        tareas.Count <= 20 && tareas.All(t => t != null && !string.IsNullOrWhiteSpace(t.Descripcion) && t.Descripcion.Length <= 200);

    private static string? SerializarTareas(List<TareaContratoDto>? tareas) => tareas == null ? null : JsonSerializer.Serialize(tareas);

    [HttpPut("{id:int}/tareas")]
    public async Task<IActionResult> EditarTareas(int id, [FromBody] EditarTareasContratoDto dto)
    {
        if (ValidarUsuario(dto.UsuarioId) is { } error) return error;
        if (dto.Tareas == null || !TareasValidas(dto.Tareas)) return Error("TAREAS_INVALIDAS");
        var fila = await _db.EjecutarAsync("sp_PilotoEditarTareas", ("@ContratoId", id), ("@UsuarioId", dto.UsuarioId), ("@Tareas", SerializarTareas(dto.Tareas)));
        var r = fila?["Resultado"]?.ToString() ?? "ERROR";
        return r == "OK" ? Ok(new { ok = true }) : Error(r);
    }

    [HttpGet("{id:int}/relevos")]
    public async Task<IActionResult> Relevos(int id, [FromQuery] int usuarioId)
    {
        if (ValidarUsuario(usuarioId) is { } error) return error;
        var detalle = await _db.ConsultarAsync("sp_ContratoDetalle", ("@ContratoId", id), ("@UsuarioId", usuarioId));
        if (detalle[0].Count == 0) return Error("NO_ENCONTRADO");
        var r = await _db.ConsultarAsync("sp_PilotoRelevos", ("@ContratoId", id), ("@UsuarioId", usuarioId));
        return Ok(r[0].Select(f => new { trabajoId = f["TrabajoId"], fecha = f["Fecha"], cuidadorId = f["CuidadorId"],
            nombre = f["Nombre"], fotoUrl = f["FotoUrl"], estado = f["Estado"], calificacion = f["Calificacion"] }));
    }

    private async Task<List<CuidadorCercanoDto>> Candidatos(int id, int trabajoId, int usuarioId, IBusquedaService busqueda)
    {
        var t = await _db.EjecutarAsync("sp_ContratoTurnoReemplazo", ("@ContratoId", id), ("@TrabajoId", trabajoId),
            ("@UsuarioId", usuarioId), ("@Ahora", Ahora));
        if (t == null) return [];
        var candidatos = (await busqueda.ObtenerCuidadoresPorServicioAsync(t["TipoServicio"]!.ToString()!,
            Convert.ToDecimal(t["Latitud"]), Convert.ToDecimal(t["Longitud"]), 15))
            .Where(c => c.Id != Convert.ToInt32(t["TitularId"]) && (t["MetodoPago"]?.ToString() != "efectivo" || c.PuedeAceptarEfectivo)).ToList();
        var historial = (await _db.ConsultarAsync("sp_PilotoHistorialCliente", ("@ContratoId", id), ("@UsuarioId", usuarioId)))[0];
        foreach (var c in candidatos)
        {
            var h = historial.FirstOrDefault(f => Convert.ToInt32(f["CuidadorId"]) == c.Id);
            c.VecesConCliente = h == null ? 0 : Convert.ToInt32(h["VecesConCliente"]);
            c.YaVino = c.VecesConCliente > 0;
            c.CalificacionConCliente = h?["CalificacionConCliente"] is { } nota ? Convert.ToDecimal(nota) : null;
        }
        return OrdenarReemplazos(candidatos).ToList();
    }

    public static IEnumerable<CuidadorCercanoDto> OrdenarReemplazos(IEnumerable<CuidadorCercanoDto> candidatos)
        => candidatos.OrderByDescending(c => c.YaVino && c.CalificacionConCliente >= 4).ThenByDescending(c => c.YaVino);

    /// <summary>True si avisó al cliente del relevo (la titular marcó la ausencia y ese día había jornada).</summary>
    private async Task<bool> NotificarAusenciaRelevo(int id, AusenciaContratoDto dto)
    {
        var t = await _db.EjecutarAsync("sp_PilotoAusenciaTurno", ("@ContratoId", id), ("@UsuarioId", dto.UsuarioId), ("@Fecha", dto.Fecha.Date));
        if (t == null) return false;
        var clienteId = Convert.ToInt32(t["ClienteId"]);
        var trabajoId = Convert.ToInt32(t["TrabajoId"]);
        var sugerido = (await Candidatos(id, trabajoId, clienteId, HttpContext.RequestServices.GetRequiredService<IBusquedaService>())).FirstOrDefault();
        await _contratos.NotificarAsync(clienteId, id, "ausencia_relevo", trabajoId, sugerido?.Id, sugerido?.NombreCompleto);
        return true;
    }
}
