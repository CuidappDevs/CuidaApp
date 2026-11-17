namespace CUIDAPP_API.Services.Contrato;
public partial class ContratoService
{
    public async Task RecordarRenovacionesAsync()
    {
        var ahora = HoraLocalRD.Ahora;
        var pendientes = (await _db.ConsultarAsync("sp_ContratoRecordatorios", ("@Ahora", ahora)))[0];
        foreach (var r in pendientes)
        {
            var id = Convert.ToInt32(r["ContratoId"]);
            var dias = Convert.ToInt32(r["Dias"]);
            await NotificarAsync(Convert.ToInt32(r["ClienteId"]), id, "renovacion_" + dias);
            await NotificarAsync(Convert.ToInt32(r["CuidadorId"]), id, "renovacion_" + dias);
            await _db.EjecutarAsync("sp_ContratoRecordatorioEnviado", ("@ContratoId", id), ("@Dias", dias),
                ("@FechaFin", r["FechaFin"]), ("@Ahora", ahora));
        }
    }
}
