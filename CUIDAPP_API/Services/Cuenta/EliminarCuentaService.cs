using System.Security.Cryptography;
using System.Text;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Interfaces.Cuenta;
using CUIDAPP_API.Interfaces.Email;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.Cuenta;

public class EliminarCuentaService(IAdminOperacionesService db, IEmailService email,
    ITrabajoNotifier notifier, IConfiguration config, ILogger<EliminarCuentaService> logger) : IEliminarCuentaService
{
    public Task<List<List<Fila>>> RevisarAsync(int usuarioId)
        => db.ConsultarAsync("sp_EliminacionRevisar", ("@UsuarioId", usuarioId));

    public async Task<int?> ValidarCredencialesAsync(string correo, string contrasena)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contrasena))).ToLowerInvariant();
        var r = await db.EjecutarAsync("sp_EliminacionCredenciales", ("@Correo", correo.Trim()), ("@PasswordHash", hash));
        return r == null ? null : Convert.ToInt32(r["UsuarioId"]);
    }

    // HMAC evita probar offline los seis dígitos si se filtra únicamente la tabla de códigos.
    private string HashCodigo(int usuarioId, string codigo, string origen)
        => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(PoliticasAdmin.ClaveJwt(config)),
            Encoding.UTF8.GetBytes($"eliminar:{origen}:{usuarioId}:{codigo}")));

    public async Task<Fila?> EnviarCodigoAsync(int usuarioId, string origen)
    {
        var codigo = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6");
        var r = await db.EjecutarAsync("sp_EliminacionCodigo", ("@UsuarioId", usuarioId),
            ("@CodigoHash", HashCodigo(usuarioId, codigo, origen)), ("@Ahora", HoraLocalRD.Ahora), ("@Origen", origen));
        if (r?["Resultado"]?.ToString() == "OK")
        {
            try { await email.EnviarAsync(r["Email"]!.ToString()!, "Confirma la eliminación de tu cuenta de CuidaApp",
                $"<p>Tu código para solicitar la eliminación de tu cuenta es: <strong>{codigo}</strong>.</p>" +
                "<p>Vence en 10 minutos y admite un máximo de 5 intentos. La cuenta se desactivará al confirmar y se anonimizará en 30 días. Puedes cancelar durante ese plazo.</p>" +
                "<p>Si no solicitaste este cambio, ignora este correo.</p>"); }
            catch(Exception ex)
            {
                logger.LogError(ex,"No se pudo enviar el código de eliminación al usuario {Id}",usuarioId);
                await db.EjecutarAsync("sp_EliminacionCodigoRevocar", ("@UsuarioId",usuarioId), ("@CodigoHash",HashCodigo(usuarioId,codigo,origen)));
                return new Fila { ["Resultado"]="CORREO_ERROR" };
            }
        }
        return r;
    }

    public async Task<Fila?> ConfirmarAsync(int usuarioId, string codigo, string? motivo, string origen)
    {
        // PROVISIONAL: 30 días de recuperación, hasta que el equipo decida la política definitiva.
        var r = await db.EjecutarAsync("sp_EliminacionConfirmar", ("@UsuarioId", usuarioId),
            ("@CodigoHash", HashCodigo(usuarioId, codigo, origen)), ("@Motivo", motivo?.Trim()),
            ("@Origen", origen), ("@Ahora", HoraLocalRD.Ahora));
        if (r?["Resultado"]?.ToString() == "OK")
        {
            await NotificarSeguro(usuarioId,new { tipo = "cuenta_en_eliminacion", fechaEjecucion = r["FechaEjecucion"] });
            try { await notifier.NotificarGlobalAsync("DisponibilidadCambio",new { CuidadorId=usuarioId,Disponible=false }); }
            catch(Exception ex) { logger.LogWarning(ex,"No se pudo anunciar la baja de disponibilidad de {Id}",usuarioId); }
        }
        return r;
    }

    public async Task<Fila?> CancelarAsync(int? usuarioId, int? solicitudId, string por, string? nota)
    {
        var r = await db.EjecutarAsync("sp_EliminacionCancelar", ("@UsuarioId", usuarioId), ("@Id", solicitudId),
            ("@Por", por), ("@Nota", nota?.Trim()), ("@Ahora", HoraLocalRD.Ahora));
        if (r?["Resultado"]?.ToString() == "OK")
            await NotificarSeguro(Convert.ToInt32(r["UsuarioId"]), new { tipo = "eliminacion_cancelada" });
        return r;
    }

    private async Task NotificarSeguro(int usuarioId,object payload)
    {
        try { await notifier.NotificarAsync(usuarioId,"CuentaActualizada",payload); }
        catch(Exception ex) { logger.LogWarning(ex,"El cambio de cuenta de {Id} se guardó pero no se pudo notificar",usuarioId); }
    }
}
