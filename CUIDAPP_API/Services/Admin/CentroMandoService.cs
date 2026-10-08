using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.DTOs.SOS;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Interfaces.SOS;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.Admin
{
    /// <summary>
    /// Avisos directos a una persona desde el centro de mando y el "¿Estás bien?": si la persona pide
    /// ayuda o no responde a tiempo, se crea una alerta SOS como cualquier otra.
    /// </summary>
    public class CentroMandoService
    {
        private readonly IAdminOperacionesService _db;
        private readonly ITrabajoNotifier _notifier;
        private readonly ISOSAlertService _sos;
        private readonly ILogger<CentroMandoService> _logger;

        public CentroMandoService(IAdminOperacionesService db, ITrabajoNotifier notifier, ISOSAlertService sos, ILogger<CentroMandoService> logger)
        {
            _db = db;
            _notifier = notifier;
            _sos = sos;
            _logger = logger;
        }

        /// <summary>Guarda el aviso y lo manda al teléfono de la persona (evento "AvisoDirecto" a su grupo).</summary>
        public async Task<int> EnviarAsync(int adminId, int usuarioId, string tipo, string titulo, string mensaje, int? minutosRespuesta = null)
        {
            var ahora = HoraLocalRD.Ahora;
            DateTime? vence = minutosRespuesta.HasValue ? ahora.AddMinutes(minutosRespuesta.Value) : null;
            var fila = await _db.EjecutarAsync("sp_AdminRegistrarAvisoDirecto", ("@AdminId", adminId), ("@UsuarioId", usuarioId), ("@Tipo", tipo),
                ("@Titulo", titulo), ("@Mensaje", mensaje), ("@Fecha", ahora), ("@VenceEn", vence));
            var id = Convert.ToInt32(fila?["Id"] ?? 0);
            if (id > 0)
                await _notifier.NotificarAsync(usuarioId, "AvisoDirecto", new { Id = id, Tipo = tipo, Titulo = titulo, Mensaje = mensaje, Minutos = minutosRespuesta });
            return id;
        }

        /// <summary>Respuesta del "¿Estás bien?" desde la app. False si ya no estaba pendiente.</summary>
        public async Task<bool> ResponderAsync(int avisoId, int usuarioId, bool estaBien)
        {
            var respuesta = estaBien ? "ok" : "ayuda";
            var fila = await _db.EjecutarAsync("sp_ResponderCheckin", ("@Id", avisoId), ("@UsuarioId", usuarioId), ("@Respuesta", respuesta), ("@Fecha", HoraLocalRD.Ahora));
            if (fila?["Resultado"]?.ToString() != "OK")
                return false;

            await _notifier.NotificarGlobalAsync("CheckinRespondido", new { Id = avisoId, UsuarioId = usuarioId, Nombre = fila["Nombre"], Respuesta = respuesta });
            if (!estaBien)
                await CrearSosAsync(fila, "Respondió «Necesito ayuda» al «¿Estás bien?» del centro de mando.",
                    Convert.ToDouble(fila["Latitud"] ?? 0), Convert.ToDouble(fila["Longitud"] ?? 0));
            return true;
        }

        /// <summary>Lo llama el servicio de fondo cada minuto: los "¿Estás bien?" sin respuesta se convierten en SOS.</summary>
        public async Task ProcesarVencidosAsync()
        {
            var vencidos = (await _db.ConsultarAsync("sp_CheckinsVencidos", ("@Ahora", HoraLocalRD.Ahora)))[0];
            foreach (var v in vencidos)
            {
                await _notifier.NotificarGlobalAsync("CheckinRespondido", new { Id = v["Id"], UsuarioId = v["UsuarioId"], Nombre = v["Nombre"], Respuesta = "vencido" });
                await CrearSosAsync(v, "No respondió a tiempo al «¿Estás bien?» del centro de mando.",
                    Convert.ToDouble(v["Latitud"] ?? 0), Convert.ToDouble(v["Longitud"] ?? 0));
            }
        }

        // La alerta SOS va ligada a un servicio; sin servicio activo queda solo el aviso en el panel.
        private async Task CrearSosAsync(Fila fila, string motivo, double? lat, double? lng)
        {
            if (fila["TrabajoId"] is not int trabajoId)
            {
                _logger.LogInformation("«¿Estás bien?» sin respuesta de {UsuarioId} sin servicio activo: no se crea SOS", fila["UsuarioId"]);
                return;
            }
            await _sos.CrearAlertaAsync(new CrearSOSAlertaDto
            {
                TrabajoId = trabajoId,
                UsuarioId = Convert.ToInt32(fila["UsuarioId"]),
                TipoUsuario = Convert.ToInt32(fila["RolId"]) == 3 ? "Cuidador" : "Cliente",
                Latitud = lat ?? 0,
                Longitud = lng ?? 0,
                Motivo = motivo
            });
        }
    }
}
