using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.Services.Email;

namespace CUIDAPP_API.Services.Recibo
{
    /// <summary>
    /// Recibos por correo al completar un servicio (estilo recibo de Uber, con la estética de CuidApp):
    /// - Cliente: el servicio recibido, las tareas hechas y no hechas, y el total pagado.
    /// - Cuidador: el servicio que dio y lo que ganó (servicio + propina).
    /// Los íconos son PNG servidos desde wwwroot/email (Gmail/Outlook no muestran SVG) y el HTML usa
    /// tablas y estilos en línea, que es lo único que respetan los clientes de correo.
    /// </summary>
    public class ReciboService : PlantillaCorreo
    {
        private readonly string _connectionString;

        public ReciboService(IConfiguration config) : base(config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        private record Datos(int Id, string TipoServicio, DateTime Fecha, TimeSpan HoraInicio, TimeSpan HoraFin,
            DateTime? InicioReal, DateTime? FinReal, string? Direccion, decimal Tarifa, string? Justificacion,
            string ClienteNombre, string ClienteEmail, string CuidadorNombre, string CuidadorEmail,
            decimal Monto, decimal Propina);

        private record Tarea(string Descripcion, bool Completada, DateTime? FechaCompletada);

        /// <summary>Envía los dos recibos. Nunca lanza: un fallo de correo no debe afectar el servicio.</summary>
        public async Task EnviarRecibosAsync(int trabajoId)
        {
            try
            {
                var (datos, tareas, actividades) = await CargarAsync(trabajoId);
                if (datos == null)
                    return;

                if (!string.IsNullOrWhiteSpace(datos.ClienteEmail))
                    await EnviarSeguroAsync(datos.ClienteEmail, $"Tu servicio de {datos.TipoServicio} con CuidApp", HtmlCliente(datos, tareas, actividades));

                if (!string.IsNullOrWhiteSpace(datos.CuidadorEmail))
                    await EnviarSeguroAsync(datos.CuidadorEmail, $"Ganaste {Dinero(datos.Monto + datos.Propina)} · Servicio completado", HtmlCuidador(datos, tareas, actividades));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Recibo] No se pudieron enviar los recibos del trabajo {trabajoId}: {ex.Message}");
            }
        }

        /// <summary>HTML de los dos correos sin enviarlos (vista previa). Null si el servicio no existe.</summary>
        public async Task<(string Cliente, string Cuidador)?> GenerarAsync(int trabajoId)
        {
            var (datos, tareas, actividades) = await CargarAsync(trabajoId);
            return datos == null ? null : (HtmlCliente(datos, tareas, actividades), HtmlCuidador(datos, tareas, actividades));
        }

        private async Task<(Datos?, List<Tarea>, int)> CargarAsync(int trabajoId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerDatosReciboTrabajo", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@TrabajoId", trabajoId);
            await connection.OpenAsync();
            using var r = await command.ExecuteReaderAsync();

            Datos? d = null;
            if (await r.ReadAsync())
            {
                d = new Datos(
                    Convert.ToInt32(r["Id"]), r["TipoServicio"]?.ToString() ?? "", Convert.ToDateTime(r["Fecha"]),
                    (TimeSpan)r["HoraInicio"], (TimeSpan)r["HoraFin"],
                    r["FechaInicioReal"] as DateTime?, r["FechaFin"] as DateTime?,
                    r["Direccion"] as string, Convert.ToDecimal(r["Tarifa"]), r["JustificacionFinalizacion"] as string,
                    r["ClienteNombre"]?.ToString() ?? "", r["ClienteEmail"]?.ToString() ?? "",
                    r["CuidadorNombre"]?.ToString() ?? "", r["CuidadorEmail"]?.ToString() ?? "",
                    Convert.ToDecimal(r["Monto"]), Convert.ToDecimal(r["Propina"]));
            }

            var tareas = new List<Tarea>();
            if (await r.NextResultAsync())
                while (await r.ReadAsync())
                    tareas.Add(new Tarea(r["Descripcion"]?.ToString() ?? "", Convert.ToBoolean(r["Completada"]), r["FechaCompletada"] as DateTime?));

            var actividades = 0;
            if (await r.NextResultAsync() && await r.ReadAsync())
                actividades = Convert.ToInt32(r["Actividades"]);

            return (d, tareas, actividades);
        }

        // ------------------------------------------------------------------ correos

        private string HtmlCliente(Datos d, List<Tarea> tareas, int actividades)
        {
            var nombre = PrimerNombre(d.ClienteNombre);
            var cuidador = PrimerNombre(d.CuidadorNombre);
            var total = d.Monto + d.Propina;
            var sb = new StringBuilder();

            sb.Append(Encabezado(d, $"Gracias por confiar en CuidApp, {E(nombre)}", $"Esperamos que {E(cuidador)} te haya ayudado como lo necesitabas."));

            // Total
            sb.Append(Seccion($"""
                {FilaTotal("Total pagado", Dinero(total))}
                {Separador()}
                {FilaMonto($"{E(d.TipoServicio)} · {E(Duracion(d))}", Dinero(d.Monto))}
                {(d.Propina > 0 ? FilaMonto("Propina para tu cuidador", Dinero(d.Propina), "propina") : "")}
                """));

            // Tareas
            sb.Append(SeccionTareas(tareas, paraCliente: true, cuidador));

            // Detalles
            sb.Append(SeccionDetalles(d, $"Tu cuidador: {E(d.CuidadorNombre)}", actividades));

            if (!string.IsNullOrWhiteSpace(d.Justificacion))
                sb.Append(Seccion($"""<tr><td colspan="2">{Aviso("info", "Nota de tu cuidador al finalizar", E(d.Justificacion!))}</td></tr>"""));

            sb.Append(Seccion($"""
                {Tarjetas(
                    ("estrella", $"Califica a {E(cuidador)}", "Abre CuidApp y cuéntanos cómo te fue. Tu opinión ayuda a otras familias."),
                    ("soporte", "¿Algún problema?", "Escríbenos desde Soporte en la app y lo resolvemos contigo."))}
                """, fondo: Fondo));

            sb.Append(Pie("Recibiste este correo porque usaste un servicio de CuidApp."));
            return Envolver($"Recibo de tu servicio de {d.TipoServicio}", sb.ToString());
        }

        private string HtmlCuidador(Datos d, List<Tarea> tareas, int actividades)
        {
            var nombre = PrimerNombre(d.CuidadorNombre);
            var cliente = PrimerNombre(d.ClienteNombre);
            var total = d.Monto + d.Propina;
            var sb = new StringBuilder();

            sb.Append(Encabezado(d, $"¡Buen trabajo, {E(nombre)}!", $"Completaste el servicio de {E(cliente)}. Aquí está el resumen de lo que ganaste."));

            sb.Append(Seccion($"""
                {FilaTotal("Ganaste", Dinero(total), Verde)}
                {Separador()}
                {FilaMonto($"Pago por el servicio · {E(Duracion(d))}", Dinero(d.Monto), "billetera")}
                {(d.Propina > 0 ? FilaMonto($"Propina de {E(cliente)} (100% tuya)", Dinero(d.Propina), "propina") : "")}
                <tr><td colspan="2" style="padding-top:14px">
                  {Aviso("info", "Estado del pago", "Tu pago queda pendiente de aprobación por la administración. Te avisaremos en la app cuando se apruebe.")}
                </td></tr>
                """));

            sb.Append(SeccionTareas(tareas, paraCliente: false, cliente));
            sb.Append(SeccionDetalles(d, $"Cliente: {E(d.ClienteNombre)}", actividades));

            sb.Append(Seccion($"""
                {Tarjetas(
                    ("escudo", "Sigue así", "Los servicios completados y las buenas calificaciones te dan más visibilidad."),
                    ("soporte", "¿Algún problema?", "Escríbenos desde Soporte en la app si algo no está bien con este pago."))}
                """, fondo: Fondo));

            sb.Append(Pie("Recibiste este correo porque completaste un servicio en CuidApp."));
            return Envolver($"Resumen de tu servicio de {d.TipoServicio}", sb.ToString());
        }

        // ------------------------------------------------------------------ bloques

        private string Encabezado(Datos d, string titulo, string subtitulo)
            => Encabezado(d.FinReal ?? d.Fecha, titulo, subtitulo, "servicio_" + ClaveServicio(d.TipoServicio));


        private string SeccionDetalles(Datos d, string persona, int actividades)
        {
            var programado = $"{FechaLarga(d.Fecha)} · {HoraSpan(d.HoraInicio)} – {HoraSpan(d.HoraFin)}";
            var real = d.InicioReal.HasValue && d.FinReal.HasValue
                ? $"{Hora(d.InicioReal)} – {Hora(d.FinReal)} ({Duracion(d)})"
                : "—";
            return Seccion($"""
                {Titulo("Detalles del servicio")}
                {Detalle("persona", "Con quién", persona)}
                {Detalle("calendario", "Programado", E(programado))}
                {Detalle("reloj", "Inicio y fin reales (con PIN)", E(real))}
                {(string.IsNullOrWhiteSpace(d.Direccion) ? "" : Detalle("ubicacion", "Dirección", E(d.Direccion!)))}
                {(actividades > 0 ? Detalle("actividad", "Actividades reportadas", $"{actividades} durante el servicio") : "")}
                """);
        }

        private string SeccionTareas(List<Tarea> tareas, bool paraCliente, string otro)
        {
            if (tareas.Count == 0)
                return "";

            var hechas = tareas.Count(t => t.Completada);
            var resumen = hechas == tareas.Count
                ? (paraCliente ? $"{E(otro)} completó todas las tareas que pediste." : "Completaste todas las tareas del cliente.")
                : $"{hechas} de {tareas.Count} tareas completadas.";

            var filas = new StringBuilder();
            foreach (var t in tareas)
            {
                var estado = t.Completada
                    ? $"Hecha{(t.FechaCompletada.HasValue ? " · " + Hora(t.FechaCompletada) : "")}"
                    : "No realizada";
                filas.Append($"""
                    <tr><td colspan="2" style="padding:6px 0">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
                        <td width="30" style="vertical-align:middle">{Icono(t.Completada ? "check" : "equis", 22)}</td>
                        <td style="vertical-align:middle;font:14px Arial,Helvetica,sans-serif;color:{(t.Completada ? Texto : Gris)}">{E(t.Descripcion)}</td>
                        <td align="right" style="vertical-align:middle;font:12px Arial,Helvetica,sans-serif;color:{(t.Completada ? Verde : Gris)};white-space:nowrap;padding-left:8px">{estado}</td>
                      </tr></table>
                    </td></tr>
                    """);
            }

            return Seccion($"""
                {Titulo("Tareas del servicio")}
                <tr><td colspan="2" style="padding-bottom:10px">
                  <div style="background:{(hechas == tareas.Count ? VerdeSuave : Fondo)};border-radius:10px;padding:10px 14px;font:600 13px Arial,Helvetica,sans-serif;color:{(hechas == tareas.Count ? Verde : Suave)}">{resumen}</div>
                </td></tr>
                {filas}
                """);
        }

        // ------------------------------------------------------------------ formato

        private static string Duracion(Datos d)
        {
            var dur = d.InicioReal.HasValue && d.FinReal.HasValue ? d.FinReal.Value - d.InicioReal.Value : d.HoraFin - d.HoraInicio;
            if (dur < TimeSpan.Zero) dur = TimeSpan.Zero;
            return dur.TotalHours >= 1 ? $"{(int)dur.TotalHours} h {dur.Minutes:00} min" : $"{Math.Max(1, (int)Math.Round(dur.TotalMinutes))} min";
        }

        // Ilustración del encabezado según el servicio (mismos íconos que la app).
        private static string ClaveServicio(string tipo)
        {
            var t = tipo.ToLowerInvariant();
            if (t.Contains("limpieza")) return "limpieza";
            if (t.Contains("adult")) return "adulto_mayor";
            if (t.Contains("hospital")) return "hospital";
            if (t.Contains("cocina") || t.Contains("comida")) return "cocina";
            if (t.Contains("niñ") || t.Contains("nin")) return "ninos";
            return "generico";
        }
    }
}
