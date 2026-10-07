using CUIDAPP.Localization;
using CUIDAPP.Models.Chat;
using CUIDAPP.Views.Chat;

namespace CUIDAPP.Services
{
    /// <summary>
    /// Arma las notificaciones de los eventos en tiempo real con datos reales (nombre, servicio,
    /// fecha) y decide quién las recibe. En primer plano se muestran como banner; en segundo
    /// plano, como notificación nativa que al tocarla abre la pantalla correspondiente.
    /// </summary>
    public static class AvisosApp
    {
        // Acciones propias: el servidor avisa a los dos participantes, pero quien hizo la acción
        // no necesita que le notifiquen lo que acaba de hacer.
        private static readonly HashSet<(int TrabajoId, int Estado)> _propias = new();

        // ConversacionId -> (TrabajoId, nombre de la otra persona).
        private static readonly Dictionary<int, (int TrabajoId, string Nombre)> _conversaciones = new();

        private static bool EsCuidador => Preferences.Default.Get("RolId", 0) == 3;
        private static int UsuarioId => Preferences.Default.Get("UserId", 0);

        /// <summary>Marcar justo antes de que el usuario cambie el estado de un servicio.</summary>
        public static void MarcarAccionPropia(int trabajoId, int estado)
        {
            lock (_propias) _propias.Add((trabajoId, estado));
        }

        private static bool EsAccionPropia(int trabajoId, int estado)
        {
            lock (_propias) return _propias.Remove((trabajoId, estado));
        }

        private static void Avisar(string titulo, string texto, string tipoHistorial, int? trabajoId, string? destino)
        {
            NotificacionHistorial.Agregar(titulo, texto, tipoHistorial, trabajoId);
            if (App.EstaEnPrimerPlano)
                GlobalNotifier.MostrarBanner(titulo, texto);
            else
                NativeNotifier.Mostrar(titulo, texto, destino);
        }

        private static string Cuando(DateTime fecha, TimeSpan hora)
            => Localizador.F("fecha_y_hora", Localizador.DiaMes(fecha), DateTime.Today.Add(hora).ToString("h:mm tt", Localizador.Cultura));

        // ---------- 1. Nueva solicitud (cuidador)
        public static async Task NuevaSolicitudAsync(int trabajoId)
        {
            if (!EsCuidador || UsuarioId == 0)
                return;

            var titulo = Localizador.T("nueva_solicitud_de_servicio");
            var texto = Localizador.T("un_cliente_solicito_tus_servicios");
            try
            {
                var t = (await new ApiService().ObtenerTrabajosAsync(UsuarioId)).FirstOrDefault(x => x.Id == trabajoId);
                if (t != null)
                    texto = Localizador.F("notif_solicitud_texto", t.ClienteNombre, Localizador.D(t.TipoServicio), Cuando(t.Fecha, t.HoraInicio));
            }
            catch { /* se avisa con el texto genérico */ }

            Avisar(titulo, texto, "solicitud", trabajoId, NotificacionDestino.Trabajo(trabajoId));
        }

        // ---------- 2. Cambios de estado del servicio (aceptado, iniciado, terminado, etc.)
        public static async Task TrabajoActualizadoAsync(int trabajoId, int estado)
        {
            if (UsuarioId == 0)
                return;

            _ = Recordatorios.SincronizarServiciosAsync();

            if (EsAccionPropia(trabajoId, estado))
                return;

            if (EsCuidador)
                await AvisarCuidadorAsync(trabajoId, estado);
            else
                await AvisarClienteAsync(trabajoId, estado);
        }

        private static async Task AvisarClienteAsync(int trabajoId, int estado)
        {
            Models.Trabajo.TrabajoCliente? t = null;
            try { t = await new ApiService().ObtenerTrabajoClientePorIdAsync(trabajoId); } catch { }

            var nombre = string.IsNullOrWhiteSpace(t?.CuidadorNombre) ? Localizador.T("tu_cuidador") : t!.CuidadorNombre.Split(' ')[0];
            var servicio = t != null ? Localizador.D(t.TipoServicio) : Localizador.T("tu_servicio");
            var destino = NotificacionDestino.Trabajo(trabajoId);

            switch (estado)
            {
                case 2:
                    Avisar(Localizador.F("notif_aceptado_titulo", nombre),
                        t != null ? Localizador.F("notif_aceptado_texto", servicio, Cuando(t.Fecha, t.HoraInicio)) : Localizador.T("est_aceptada"),
                        "trabajo", trabajoId, destino);
                    break;
                case 3:
                    Avisar(Localizador.F("notif_llego_titulo", nombre), Localizador.T("notif_llego_texto"), "trabajo", trabajoId, destino);
                    break;
                case 7:
                    Avisar(Localizador.F("notif_termino_titulo", nombre), Localizador.T("notif_termino_texto"), "trabajo", trabajoId, destino);
                    break;
                case 4:
                    Avisar(Localizador.T("notif_completado_titulo"), Localizador.F("notif_completado_texto", nombre),
                        "trabajo", trabajoId, NotificacionDestino.Calificar(trabajoId));
                    Recordatorios.ProgramarCalificacion(trabajoId, nombre);
                    break;
                case 5:
                    Avisar(Localizador.T("notif_cancelado_titulo"), Localizador.F("notif_cancelado_cliente_texto", servicio), "trabajo", trabajoId, destino);
                    break;
                case 6:
                    Avisar(Localizador.F("notif_rechazado_titulo", nombre), Localizador.F("notif_rechazado_texto", servicio), "trabajo", trabajoId, null);
                    break;
            }
        }

        private static async Task AvisarCuidadorAsync(int trabajoId, int estado)
        {
            // 2, 6 y 7 son acciones del propio cuidador (aceptar, rechazar, terminar).
            if (estado is not (3 or 4 or 5))
                return;

            Models.Trabajo.Trabajo? t = null;
            try { t = (await new ApiService().ObtenerTrabajosAsync(UsuarioId)).FirstOrDefault(x => x.Id == trabajoId); } catch { }
            var cliente = string.IsNullOrWhiteSpace(t?.ClienteNombre) ? Localizador.T("el_cliente") : t!.ClienteNombre.Split(' ')[0];
            var destino = NotificacionDestino.Trabajo(trabajoId);

            switch (estado)
            {
                case 3: // El cliente no confirmó el final: el servicio vuelve a "en progreso".
                    Avisar(Localizador.T("notif_no_confirmo_titulo"), Localizador.F("notif_no_confirmo_texto", cliente), "trabajo", trabajoId, destino);
                    break;
                case 4:
                    Avisar(Localizador.T("notif_confirmado_titulo"), Localizador.F("notif_confirmado_texto", cliente), "trabajo", trabajoId, destino);
                    break;
                case 5:
                    Avisar(Localizador.T("notif_cancelado_titulo"),
                        t != null ? Localizador.F("notif_cancelado_cuidador_texto", cliente, Cuando(t.Fecha, t.HoraInicio)) : Localizador.T("est_cancelado"),
                        "trabajo", trabajoId, destino);
                    break;
            }
        }

        // ---------- 3. Mensaje de chat
        public static async Task MensajeNuevoAsync(Mensaje mensaje)
        {
            if (mensaje.RemitenteId == UsuarioId || UsuarioId == 0)
                return;

            // Si ya tiene esa conversación abierta, ChatPage lo pinta en vivo.
            if (mensaje.ConversacionId == ChatPage.ConversacionAbiertaId)
                return;

            var texto = mensaje.Tipo switch
            {
                "imagen" => Localizador.T("notif_foto"),
                "audio" => Localizador.T("notif_audio"),
                _ => mensaje.Contenido
            };

            var (trabajoId, nombre) = await ResolverConversacionAsync(mensaje.ConversacionId);
            var titulo = string.IsNullOrWhiteSpace(nombre) ? Localizador.T("nuevo_mensaje") : nombre;

            NotificacionHistorial.Agregar(Localizador.T("nuevo_mensaje"), texto, "mensaje", trabajoId > 0 ? trabajoId : null);
            if (App.EstaEnPrimerPlano)
                GlobalNotifier.MostrarBanner(titulo, texto);
            else
                NativeNotifier.Mostrar(titulo, texto, trabajoId > 0 ? NotificacionDestino.Chat(trabajoId) : null);
        }

        private static async Task<(int TrabajoId, string Nombre)> ResolverConversacionAsync(int conversacionId)
        {
            lock (_conversaciones)
                if (_conversaciones.TryGetValue(conversacionId, out var conocida))
                    return conocida;

            try
            {
                var api = new ApiService();
                var candidatos = EsCuidador
                    ? (await api.ObtenerTrabajosAsync(UsuarioId)).Where(t => t.Estado is 2 or 3 or 7).Select(t => (t.Id, t.ClienteNombre))
                    : (await api.ObtenerTrabajosActivosPorClienteAsync(UsuarioId)).Select(t => (t.Id, t.CuidadorNombre));

                foreach (var (id, nombre) in candidatos)
                {
                    var conversacion = await api.ObtenerOCrearConversacionAsync(id);
                    if (conversacion == null)
                        continue;
                    lock (_conversaciones) _conversaciones[conversacion.Id] = (id, nombre);
                    if (conversacion.Id == conversacionId)
                        return (id, nombre);
                }
            }
            catch { /* se avisa sin nombre */ }

            return (0, "");
        }

        // ---------- 4. Cuenta del cuidador aprobada / rechazada
        public static void CuentaActualizada(int estado)
        {
            if (!EsCuidador)
                return;

            if (estado == 2)
            {
                Preferences.Default.Set("EstadoAprobacion", 2);
                Avisar(Localizador.T("notif_cuenta_aprobada_titulo"), Localizador.T("notif_cuenta_aprobada_texto"), "verificacion", null, NotificacionDestino.Verificacion);
            }
            else if (estado == 3)
            {
                Avisar(Localizador.T("notif_cuenta_rechazada_titulo"), Localizador.T("notif_cuenta_rechazada_texto"), "verificacion", null, NotificacionDestino.Verificacion);
            }
        }

        // ---------- 5. Pago aprobado (cuidador)
        public static void PagoAprobado(int trabajoId, decimal monto)
        {
            if (!EsCuidador)
                return;
            Avisar(Localizador.T("notif_pago_titulo"), Localizador.F("notif_pago_texto", monto), "pago", trabajoId > 0 ? trabajoId : null, NotificacionDestino.Dinero);
        }

        // ---------- 6. Soporte respondió / cambió el estado del reporte
        public static void TicketActualizado(int ticketId, bool respuesta, int? estado, string? asunto)
        {
            var titulo = respuesta ? Localizador.T("notif_soporte_respondio_titulo")
                       : estado == 3 ? Localizador.T("notif_soporte_resuelto_titulo")
                       : Localizador.T("notif_soporte_estado_titulo");
            var texto = string.IsNullOrWhiteSpace(asunto) ? Localizador.T("notif_soporte_texto") : asunto!;
            Avisar(titulo, texto, "soporte", null, NotificacionDestino.Ticket(ticketId));
        }
    }
}
