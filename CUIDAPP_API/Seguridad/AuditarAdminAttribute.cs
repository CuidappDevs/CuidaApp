using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.Services;

namespace CUIDAPP_API.Seguridad
{
    /// <summary>
    /// Registra en AuditoriaAdmin cada acción que modifica datos (POST/PUT/DELETE) hecha desde el
    /// panel: quién, qué, sobre qué registro, el motivo si lo hubo y si salió bien. Las lecturas no
    /// se registran. Si la bitácora falla, la acción no se ve afectada.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AuditarAdminAttribute : Attribute, IAsyncActionFilter
    {
        // Nombre legible de cada acción del panel (nombre del método del controlador).
        private static readonly Dictionary<string, string> Acciones = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ActualizarEstadoCuidador"] = "Revisó un Care Partner",
            ["ActualizarEstadoDocumento"] = "Revisó un documento",
            ["SuspenderCuidador"] = "Suspendió un Care Partner",
            ["ReactivarCuidador"] = "Reactivó un Care Partner",
            ["ActualizarInfoCuidador"] = "Editó un Care Partner",
            ["SuspenderCliente"] = "Suspendió un cliente",
            ["ReactivarCliente"] = "Reactivó un cliente",
            ["ActualizarInfoCliente"] = "Editó un cliente",
            ["CrearAdmin"] = "Creó un administrador",
            ["SuspenderAdmin"] = "Suspendió un administrador",
            ["ReactivarAdmin"] = "Reactivó un administrador",
            ["CambiarNivelAdmin"] = "Cambió el nivel de un administrador",
            ["MarcarPagoComoPagado"] = "Marcó un pago como pagado",
            ["AutorizarPago"] = "Autorizó un pago",
            ["AprobarPago"] = "Confirmó el envío de un pago",
            ["CancelarServicio"] = "Canceló un servicio",
            ["CompletarServicio"] = "Dio por completado un servicio",
            ["EnviarAviso"] = "Envió un aviso masivo",
            ["Notificar"] = "Envió una notificación directa",
            ["Checkin"] = "Preguntó «¿Estás bien?»",
            ["OcultarCuidador"] = "Ocultó a un Care Partner desde el mapa",
            ["ReenviarAviso"] = "Reenvió un aviso masivo",
            ["EliminarAviso"] = "Eliminó un aviso del historial",
            ["GuardarTipoTrabajo"] = "Guardó un tipo de servicio",
            ["GuardarMotivoCancelacion"] = "Guardó un motivo de cancelación",
            ["GuardarNacionalidad"] = "Guardó una nacionalidad",
            ["GuardarEstatusMigratorio"] = "Guardó un estatus migratorio",
            ["AgregarRequisito"] = "Agregó un documento requerido",
            ["QuitarRequisito"] = "Quitó un documento requerido",
            ["AtenderAlerta"] = "Atendió una alerta SOS",
            ["DescartarAlerta"] = "Descartó una alerta SOS",
            ["CambiarEstado"] = "Cambió el estado de un reporte",
            ["ActualizarEstado"] = "Cambió el estado de un reporte",
        };

        private static readonly string[] ClavesId = { "id", "usuarioId", "pagoId", "trabajoId", "ticketId", "cuidadorId" };
        private static readonly string[] CamposDetalle = { "Motivo", "Justificacion", "Observaciones", "ObservacionesAdmin", "Titulo", "Nombre", "Descripcion", "TipoDocumento" };

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var metodo = context.HttpContext.Request.Method;
            var resultado = await next();

            if (HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || HttpMethods.IsOptions(metodo))
                return;

            var user = context.HttpContext.User;
            var adminId = PoliticasAdmin.AdminId(user);
            if (adminId == 0 || !user.IsInRole("1"))
                return;

            try
            {
                var accion = context.ActionDescriptor.RouteValues.TryGetValue("action", out var a) && a != null ? a : "?";
                var controlador = context.ActionDescriptor.RouteValues.TryGetValue("controller", out var c) ? c : null;

                string? entidadId = null;
                foreach (var clave in ClavesId)
                    if (context.RouteData.Values.TryGetValue(clave, out var v) && v != null) { entidadId = v.ToString(); break; }

                string? detalle = null;
                foreach (var arg in context.ActionArguments.Values.Where(x => x != null && !x.GetType().IsPrimitive && x is not string))
                {
                    var tipo = arg!.GetType();
                    entidadId ??= LeerId(arg);
                    foreach (var campo in CamposDetalle)
                        if (tipo.GetProperty(campo)?.GetValue(arg) is string texto && !string.IsNullOrWhiteSpace(texto))
                        {
                            detalle = texto.Length > 380 ? texto[..380] + "…" : texto;
                            break;
                        }
                }

                var status = resultado.Result switch
                {
                    IStatusCodeActionResult r => r.StatusCode ?? 200,
                    _ => resultado.Exception != null && !resultado.ExceptionHandled ? 500 : 200
                };

                var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                using var connection = new SqlConnection(config.GetConnectionString("DefaultConnection"));
                using var command = new SqlCommand("sp_AdminRegistrarAuditoria", connection) { CommandType = CommandType.StoredProcedure };
                command.Parameters.AddWithValue("@AdminId", adminId);
                command.Parameters.AddWithValue("@Accion", Acciones.TryGetValue(accion, out var legible) ? legible : $"{controlador}.{accion}");
                command.Parameters.AddWithValue("@Entidad", (object?)controlador ?? DBNull.Value);
                command.Parameters.AddWithValue("@EntidadId", (object?)entidadId ?? DBNull.Value);
                command.Parameters.AddWithValue("@Detalle", (object?)detalle ?? DBNull.Value);
                command.Parameters.AddWithValue("@Exito", status < 400);
                command.Parameters.AddWithValue("@Fecha", HoraLocalRD.Ahora);
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                context.HttpContext.RequestServices.GetRequiredService<ILogger<AuditarAdminAttribute>>()
                    .LogWarning(ex, "No se pudo registrar la auditoría del panel");
            }
        }

        private static string? LeerId(object dto)
        {
            foreach (var nombre in new[] { "UsuarioId", "CuidadorId", "TrabajoId", "PagoId", "Id" })
                if (dto.GetType().GetProperty(nombre)?.GetValue(dto) is int id && id > 0)
                    return id.ToString();
            return null;
        }
    }
}
