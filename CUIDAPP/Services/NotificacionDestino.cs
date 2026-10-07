namespace CUIDAPP.Services
{
    /// <summary>
    /// A dónde lleva una notificación al tocarla. Formato: "trabajo:12", "chat:12", "calificar:12",
    /// "ticket:5", "verificacion" o "dinero". Si la app arranca desde la notificación, el destino
    /// queda pendiente hasta que la sesión esté abierta (<see cref="ProcesarPendienteAsync"/>).
    /// </summary>
    public static class NotificacionDestino
    {
        public const string ClaveExtra = "cuidapp_destino";

        private static string? _pendiente;

        public static string Trabajo(int trabajoId) => $"trabajo:{trabajoId}";
        public static string Chat(int trabajoId) => $"chat:{trabajoId}";
        public static string Calificar(int trabajoId) => $"calificar:{trabajoId}";
        public static string Ticket(int ticketId) => $"ticket:{ticketId}";
        public const string Verificacion = "verificacion";
        public const string Dinero = "dinero";

        /// <summary>Llega desde Android al tocar una notificación.</summary>
        public static void Recibir(string? destino, bool appYaAbierta)
        {
            if (string.IsNullOrWhiteSpace(destino))
                return;

            if (appYaAbierta && Shell.Current != null && Preferences.Default.Get("UserId", 0) != 0)
                MainThread.BeginInvokeOnMainThread(async () => await IrAsync(destino));
            else
                _pendiente = destino;
        }

        /// <summary>Se llama cuando la sesión ya quedó abierta (login o sesión guardada).</summary>
        public static async Task ProcesarPendienteAsync()
        {
            var destino = _pendiente;
            _pendiente = null;
            if (destino != null)
                await IrAsync(destino);
        }

        private static async Task IrAsync(string destino)
        {
            try
            {
                var usuarioId = Preferences.Default.Get("UserId", 0);
                if (usuarioId == 0 || Shell.Current == null)
                    return;

                bool esCuidador = Preferences.Default.Get("RolId", 0) == 3;
                var partes = destino.Split(':');
                var tipo = partes[0];
                int.TryParse(partes.Length > 1 ? partes[1] : "", out var id);
                var api = new ApiService();

                switch (tipo)
                {
                    case "trabajo" when esCuidador:
                        var trabajo = (await api.ObtenerTrabajosAsync(usuarioId)).FirstOrDefault(t => t.Id == id);
                        if (trabajo != null)
                            await Shell.Current.GoToAsync("DetalleTrabajoPage", new Dictionary<string, object> { { "Trabajo", trabajo } });
                        else
                            await Shell.Current.GoToAsync("TrabajosPage");
                        break;

                    case "trabajo":
                        await Shell.Current.GoToAsync("DetalleServicioClientePage", new Dictionary<string, object> { { "TrabajoId", id } });
                        break;

                    case "chat" when esCuidador:
                        var conCliente = (await api.ObtenerTrabajosAsync(usuarioId)).FirstOrDefault(t => t.Id == id);
                        if (conCliente != null)
                            await Shell.Current.GoToAsync("ChatPage", new Dictionary<string, object>
                            {
                                { "TrabajoId", id }, { "OtroNombre", conCliente.ClienteNombre }, { "OtroFotoUrl", conCliente.ClienteFotoUrl ?? "" }
                            });
                        break;

                    case "chat":
                        var conCuidador = await api.ObtenerTrabajoClientePorIdAsync(id);
                        if (conCuidador != null)
                            await Shell.Current.GoToAsync("ChatPage", new Dictionary<string, object>
                            {
                                { "TrabajoId", id }, { "OtroNombre", conCuidador.CuidadorNombre }, { "OtroFotoUrl", conCuidador.CuidadorFotoUrl ?? "" }
                            });
                        break;

                    case "calificar":
                        var servicio = await api.ObtenerTrabajoClientePorIdAsync(id);
                        if (servicio != null && !await api.ExisteCalificacionAsync(id, usuarioId))
                            await Shell.Current.GoToAsync("CalificarPage", new Dictionary<string, object>
                            {
                                { "TrabajoId", id }, { "CalificadoId", servicio.CuidadorId },
                                { "CalificadoNombre", servicio.CuidadorNombre }, { "RutaSalida", "//MainPage/ClienteDashboardPage" }
                            });
                        break;

                    case "ticket":
                        await Shell.Current.GoToAsync("DetalleReportePage", new Dictionary<string, object> { { "TicketId", id } });
                        break;

                    case Verificacion:
                        if (Preferences.Default.Get("EstadoAprobacion", 0) != 2 && Shell.Current.CurrentPage is not Views.Verificacion.VerificacionPendientePage)
                            await Shell.Current.GoToAsync("VerificacionPendientePage");
                        break;

                    case Dinero when esCuidador:
                        await Shell.Current.GoToAsync("DineroPage");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NotificacionDestino] No se pudo abrir {destino}: {ex.Message}");
            }
        }
    }
}
