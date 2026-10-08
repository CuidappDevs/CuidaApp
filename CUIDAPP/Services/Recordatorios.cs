using CUIDAPP.Localization;

namespace CUIDAPP.Services
{
    /// <summary>
    /// Notificaciones programadas en el propio teléfono (AlarmManager en Android): llegan aunque
    /// la app esté cerrada, sin depender del servidor.
    /// - 1 hora y 15 minutos antes de cada servicio aceptado (cliente y cuidador).
    /// - Al día siguiente de completar un servicio, si el cliente no lo calificó.
    /// </summary>
    public static class Recordatorios
    {
        private const string ClaveProgramados = "RecordatoriosServicios"; // ids de trabajos con recordatorio
        private const string ClaveHorario = "RecordatoriosHorario";        // "id|ticks|i/f;..." avisos del horario
        private const int BaseIdHorario = 2_000_000;

        private static int IdUnaHora(int trabajoId) => trabajoId * 10 + 1;
        private static int IdQuinceMin(int trabajoId) => trabajoId * 10 + 2;
        private static int IdCalificar(int trabajoId) => trabajoId * 10 + 3;

        /// <summary>
        /// Vuelve a programar los recordatorios de todos los servicios aceptados que aún no empiezan,
        /// y quita los de servicios cancelados, rechazados o ya iniciados. Se llama al abrir la sesión
        /// y cada vez que cambia el estado de un servicio.
        /// </summary>
        public static async Task SincronizarServiciosAsync()
        {
            try
            {
                var usuarioId = Preferences.Default.Get("UserId", 0);
                if (usuarioId == 0)
                    return;

                var api = new ApiService();
                var proximos = new List<(int Id, DateTime Inicio, string Otro, string Servicio)>();

                if (Preferences.Default.Get("RolId", 0) == 3)
                {
                    foreach (var t in await api.ObtenerTrabajosAsync(usuarioId))
                        if (t.Estado == 2)
                            proximos.Add((t.Id, t.Fecha.Date + t.HoraInicio, t.ClienteNombre, t.TipoServicio));
                }
                else
                {
                    foreach (var t in await api.ObtenerTrabajosActivosPorClienteAsync(usuarioId))
                        if (t.Estado == 2)
                            proximos.Add((t.Id, t.Fecha.Date + t.HoraInicio, t.CuidadorNombre, t.TipoServicio));
                }

                var vigentes = new HashSet<int>();
                foreach (var (id, inicio, otro, servicio) in proximos.Where(p => p.Inicio > DateTime.Now))
                {
                    vigentes.Add(id);
                    var nombreServicio = Localizador.D(servicio);
                    var hora = DateTime.Today.Add(inicio.TimeOfDay).ToString("h:mm tt", Localizador.Cultura);
                    Programar(IdUnaHora(id), inicio.AddHours(-1),
                        Localizador.T("rec_servicio_1h_titulo"),
                        Localizador.F("rec_servicio_1h_texto", nombreServicio, otro, hora),
                        NotificacionDestino.Trabajo(id));
                    Programar(IdQuinceMin(id), inicio.AddMinutes(-15),
                        Localizador.T("rec_servicio_15m_titulo"),
                        Localizador.F("rec_servicio_15m_texto", nombreServicio, otro),
                        NotificacionDestino.Trabajo(id));
                }

                foreach (var id in LeerProgramados().Where(id => !vigentes.Contains(id)))
                {
                    Cancelar(IdUnaHora(id));
                    Cancelar(IdQuinceMin(id));
                }
                Preferences.Default.Set(ClaveProgramados, string.Join(",", vigentes));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Recordatorios] No se pudieron sincronizar: {ex.Message}");
            }
        }

        /// <summary>Recordatorio para calificar, 24 h después de completar el servicio.</summary>
        public static void ProgramarCalificacion(int trabajoId, string nombreCuidador)
            => Programar(IdCalificar(trabajoId), DateTime.Now.AddHours(24),
                Localizador.T("rec_calificar_titulo"),
                Localizador.F("rec_calificar_texto", nombreCuidador),
                NotificacionDestino.Calificar(trabajoId));

        public static void CancelarCalificacion(int trabajoId) => Cancelar(IdCalificar(trabajoId));

        /// <summary>
        /// Programa en el teléfono el aviso de inicio y de fin de cada franja del horario automático para
        /// los próximos 7 días (llegan aunque la app esté cerrada). Se vuelve a llamar cada vez que se
        /// abre el panel o se guarda el horario, así la ventana de 7 días se mantiene al día.
        /// </summary>
        public static void ProgramarHorario(Models.Cuidador.HorarioCuidador horario)
        {
            CancelarHorario();
            if (!horario.Activo)
                return;

            var guardados = new List<string>();
            var n = 0;
            foreach (var (ini, fin) in horario.Proximas(DateTime.Now, 7))
            {
                var hasta = fin.ToString("h:mm tt", Localizador.Cultura);
                if (ini > DateTime.Now)
                {
                    Programar(BaseIdHorario + n, ini, Localizador.T("notif_horario_inicio_titulo"), Localizador.F("notif_horario_inicio_texto", hasta), "");
                    guardados.Add($"{BaseIdHorario + n}|{ini.Ticks}|i");
                    n++;
                }
                if (fin > DateTime.Now)
                {
                    Programar(BaseIdHorario + n, fin, Localizador.T("notif_horario_fin_titulo"), Localizador.T("notif_horario_fin_texto"), "");
                    guardados.Add($"{BaseIdHorario + n}|{fin.Ticks}|f");
                    n++;
                }
            }
            Preferences.Default.Set(ClaveHorario, string.Join(";", guardados));
        }

        public static void CancelarHorario()
        {
            foreach (var (id, _, _) in LeerHorario())
                Cancelar(id);
            Preferences.Default.Remove(ClaveHorario);
        }

        /// <summary>
        /// Si la app ya mostró el aviso en vivo (llegó el cambio del servidor), se quita la alarma de ese
        /// mismo momento para que no llegue repetido unos minutos después.
        /// </summary>
        public static void QuitarAvisoHorarioCercano(bool inicio)
        {
            var ahora = DateTime.Now;
            var restantes = new List<string>();
            foreach (var (id, cuando, esInicio) in LeerHorario())
            {
                if (esInicio == inicio && Math.Abs((cuando - ahora).TotalMinutes) <= 20)
                    Cancelar(id);
                else
                    restantes.Add($"{id}|{cuando.Ticks}|{(esInicio ? "i" : "f")}");
            }
            Preferences.Default.Set(ClaveHorario, string.Join(";", restantes));
        }

        private static List<(int Id, DateTime Cuando, bool Inicio)> LeerHorario()
            => Preferences.Default.Get(ClaveHorario, "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split('|'))
                .Where(p => p.Length == 3 && int.TryParse(p[0], out _) && long.TryParse(p[1], out _))
                .Select(p => (int.Parse(p[0]), new DateTime(long.Parse(p[1])), p[2] == "i"))
                .ToList();

        /// <summary>Al cerrar sesión: no deben llegar recordatorios de otra cuenta.</summary>
        public static void CancelarTodos()
        {
            CancelarHorario();
            foreach (var id in LeerProgramados())
            {
                Cancelar(IdUnaHora(id));
                Cancelar(IdQuinceMin(id));
            }
            Preferences.Default.Remove(ClaveProgramados);
        }

        private static IEnumerable<int> LeerProgramados()
            => Preferences.Default.Get(ClaveProgramados, "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var n) ? n : 0)
                .Where(n => n > 0)
                .ToList();

        private static void Programar(int id, DateTime cuando, string titulo, string texto, string destino)
        {
            if (cuando <= DateTime.Now)
                return;
#if ANDROID
            try
            {
                var contexto = Android.App.Application.Context;
                var alarmas = (Android.App.AlarmManager)contexto.GetSystemService(Android.Content.Context.AlarmService)!;
                var millis = new DateTimeOffset(cuando).ToUnixTimeMilliseconds();
                // Inexacta (puede llegar con unos minutos de diferencia) pero no necesita el permiso
                // especial de alarmas exactas y respeta el ahorro de batería.
                alarmas.SetAndAllowWhileIdle(Android.App.AlarmType.RtcWakeup, millis, CrearIntent(id, titulo, texto, destino));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Recordatorios] No se pudo programar {id}: {ex.Message}");
            }
#endif
        }

        private static void Cancelar(int id)
        {
#if ANDROID
            try
            {
                var contexto = Android.App.Application.Context;
                var alarmas = (Android.App.AlarmManager)contexto.GetSystemService(Android.Content.Context.AlarmService)!;
                alarmas.Cancel(CrearIntent(id, "", "", ""));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Recordatorios] No se pudo cancelar {id}: {ex.Message}");
            }
#endif
        }

#if ANDROID
        private static Android.App.PendingIntent CrearIntent(int id, string titulo, string texto, string destino)
        {
            var contexto = Android.App.Application.Context;
            var intent = new Android.Content.Intent(contexto, typeof(Platforms.Android.RecordatorioReceiver));
            intent.PutExtra("titulo", titulo);
            intent.PutExtra("texto", texto);
            intent.PutExtra(NotificacionDestino.ClaveExtra, destino);
            return Android.App.PendingIntent.GetBroadcast(contexto, id, intent,
                Android.App.PendingIntentFlags.UpdateCurrent | Android.App.PendingIntentFlags.Immutable)!;
        }
#endif
    }
}
