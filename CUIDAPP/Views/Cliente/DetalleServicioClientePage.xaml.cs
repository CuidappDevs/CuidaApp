using CUIDAPP.Localization;
using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class DetalleServicioClientePage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private TrabajoCliente? trabajo;
        private bool relojIniciado;
        private int trabajoId;
        private int estadoAnterior;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("TrabajoId", out var value) && value is int id)
                trabajoId = id;
        }

        public DetalleServicioClientePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            RealtimeService.TrabajoActualizado += OnTrabajoActualizadoTiempoReal;
            RealtimeService.ActividadAgregada += OnActividadAgregadaTiempoReal;
            RealtimeService.AlertaGeocerca += OnAlertaGeocercaTiempoReal;
            RealtimeService.TareaCompletada += OnTareaCompletadaTiempoReal;
            ConstruirChipsPropina();
            IniciarRelojSiHaceFalta();
            await CargarTrabajo();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
            RealtimeService.ActividadAgregada -= OnActividadAgregadaTiempoReal;
            RealtimeService.AlertaGeocerca -= OnAlertaGeocercaTiempoReal;
            RealtimeService.TareaCompletada -= OnTareaCompletadaTiempoReal;
        }

        private async void OnAlertaGeocercaTiempoReal(int idTrabajo, double distanciaMetros)
        {
            if (idTrabajo != trabajoId)
                return;

            await Alerta.MostrarAsync(Localizador.T("tu_cuidador_se_alejo_del"), Localizador.F("se_alejo_m_del_domicilio", distanciaMetros), Localizador.T("entendido"));
        }

        private async void OnTrabajoActualizadoTiempoReal(int idActualizado, int estado)
        {
            if (idActualizado != trabajoId)
                return;

            await CargarTrabajo();
        }

        private void OnActividadAgregadaTiempoReal(int idActividad, string descripcion, DateTime fechaHora)
        {
            if (idActividad != trabajoId)
                return;

            LblSinActividadesCliente.IsVisible = false;
            ListaActividadesCliente.Insert(0, CrearTarjetaActividad(descripcion, fechaHora));
        }

        private async void OnTareaCompletadaTiempoReal(int idTrabajo, int tareaId, string descripcion)
        {
            if (idTrabajo != trabajoId)
                return;

            await CargarTareasAsync();
        }

        private async Task CargarTareasAsync()
        {
            if (trabajo == null)
                return;

            var tareasTrabajo = await _apiService.ObtenerTareasAsync(trabajo.Id);
            CardTareasCliente.IsVisible = tareasTrabajo.Count > 0 && trabajo.Estado is 2 or 3 or 7;
            ListaTareasCliente.Clear();

            foreach (var tarea in tareasTrabajo)
            {
                ListaTareasCliente.Add(new Border
                {
                    Stroke = Colors.Transparent,
                    BackgroundColor = Colors.White,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(14, 10),
                    Content = new Label
                    {
                        Text = (tarea.Completada ? "☑  " : "☐  ") + tarea.Descripcion,
                        FontSize = 14,
                        FontFamily = "OpenSansRegular",
                        TextColor = tarea.Completada ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#111827"),
                        TextDecorations = tarea.Completada ? TextDecorations.Strikethrough : TextDecorations.None
                    }
                });
            }
        }

        private void IniciarRelojSiHaceFalta()
        {
            if (relojIniciado)
                return;
            relojIniciado = true;

            // Reloj puramente local (sin red): solo recalcula el texto en base a la hora
            // real de inicio que ya tenemos, no consulta nada al servidor.
            Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                if (trabajo?.Estado == 3 && trabajo.FechaInicioReal.HasValue)
                {
                    // FechaInicioReal viene de GETDATE() del servidor; comparar contra el
                    // reloj del dispositivo puede desfasarse (visto en producción: mostraba
                    // 7 horas al iniciar). Se usa ServerClock, sincronizado con el servidor.
                    var transcurrido = ServerClock.Now - trabajo.FechaInicioReal.Value;
                    if (transcurrido < TimeSpan.Zero)
                        transcurrido = TimeSpan.Zero;
                    LblTiempoTranscurrido.Text = transcurrido.ToString(@"hh\:mm\:ss");
                }
                return true;
            });
        }

        private async Task CargarTrabajo()
        {
            if (trabajoId == 0)
                return;

            LoadingIndicator.IsRunning = true;
            ContenedorInfo.IsVisible = false;

            trabajo = await _apiService.ObtenerTrabajoClientePorIdAsync(trabajoId);

            LoadingIndicator.IsRunning = false;

            if (trabajo == null)
                return;

            ContenedorInfo.IsVisible = true;
            Renderizar(trabajo);
        }

        private void Renderizar(TrabajoCliente t)
        {
            LblCuidadorNombre.Text = t.CuidadorNombre;
            LblTipoServicio.Text = Localizador.D(t.TipoServicio);

            if (!string.IsNullOrWhiteSpace(t.CuidadorFotoUrl))
                ImgCuidador.Source = $"{ApiService.ServerOrigin}{t.CuidadorFotoUrl}";

            LblFechaHora.Text = Localizador.F("fecha_hora_rango", Localizador.FechaLarga(t.Fecha), FormatearHora(t.HoraInicio), FormatearHora(t.HoraFin));
            LblDireccion.Text = string.IsNullOrWhiteSpace(t.Direccion) ? Localizador.T("sin_direccion") : t.Direccion;
            LblPago.Text = Localizador.F("rd", t.Tarifa);

            var (colorFondo, colorTexto, texto) = t.Estado switch
            {
                1 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), Localizador.T("banner_esperando")),
                2 => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#1E40AF"), Localizador.T("aceptado_tu_cuidador_asistira_en")),
                3 => (Color.FromArgb("#EDE9FE"), Color.FromArgb("#5B21B6"), Localizador.T("en_progreso")),
                4 => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#166534"), Localizador.T("servicio_completado")),
                5 => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), Localizador.T("cancelado")),
                6 => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#991B1B"), Localizador.T("rechazado_por_cuidador")),
                7 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), Localizador.T("esperando_tu_confirmacion")),
                _ => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), Localizador.T("desconocido"))
            };
            BadgeEstado.BackgroundColor = colorFondo;
            DotEstado.BackgroundColor = colorTexto;
            LblEstado.TextColor = colorTexto;
            LblEstado.Text = texto;

            BtnCancelar.IsVisible = t.Estado == 1;
            BtnSOS.IsVisible = t.Estado == 2 || t.Estado == 3;
            BtnCalificar.IsVisible = t.Estado == 4;

            CardConfirmarFinalizacion.IsVisible = t.Estado == 7;
            if (CardConfirmarFinalizacion.IsVisible)
            {
                LblJustificacionFinalizacion.IsVisible = !string.IsNullOrWhiteSpace(t.JustificacionFinalizacion);
                LblJustificacionFinalizacion.Text = string.IsNullOrWhiteSpace(t.JustificacionFinalizacion)
                    ? ""
                    : Localizador.F("motivo", t.JustificacionFinalizacion);
            }

            if (estadoAnterior == 7 && t.Estado == 4)
                _ = MostrarAnuncioTerminadoAsync();

            estadoAnterior = t.Estado;

            CardPin.IsVisible = t.Estado == 2 && !string.IsNullOrWhiteSpace(t.PinInicio);
            if (CardPin.IsVisible)
                LblPin.Text = t.PinInicio;

            CardEnProgreso.IsVisible = t.Estado == 3 && !string.IsNullOrWhiteSpace(t.PinFin);
            if (CardEnProgreso.IsVisible)
                LblPinFin.Text = t.PinFin;

            CardActividadesCliente.IsVisible = t.Estado == 3;
            if (CardActividadesCliente.IsVisible)
                _ = CargarActividadesAsync();

            _ = CargarTareasAsync();

            BtnChat.IsVisible = t.Estado is 2 or 3 or 7;

            RenderizarPasos(t.Estado);
        }

        private async void OnChatClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var parametros = new Dictionary<string, object>
            {
                { "TrabajoId", trabajo.Id },
                { "OtroNombre", trabajo.CuidadorNombre },
                { "OtroFotoUrl", trabajo.CuidadorFotoUrl ?? "" }
            };
            await Shell.Current.GoToAsync("ChatPage", parametros);
        }

        private async Task CargarActividadesAsync()
        {
            if (trabajo == null)
                return;

            var actividades = await _apiService.ObtenerActividadesAsync(trabajo.Id);

            ListaActividadesCliente.Clear();
            LblSinActividadesCliente.IsVisible = actividades.Count == 0;

            foreach (var actividad in actividades.OrderByDescending(a => a.FechaHora))
                ListaActividadesCliente.Add(CrearTarjetaActividad(actividad.Descripcion, actividad.FechaHora));
        }

        private static View CrearTarjetaActividad(string descripcion, DateTime fechaHora)
        {
            return new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb("#FFFFFF"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(14, 10),
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label { Text = descripcion, FontSize = 14, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#111827") },
                        new Label { Text = fechaHora.ToString("h:mm tt"), FontSize = 11, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#9CA3AF") }
                    }
                }
            };
        }

        private void RenderizarPasos(int estado)
        {
            ListaPasos.Clear();

            var pasos = new List<(string Texto, bool Completado)>
            {
                (Localizador.T("solicitud_enviada"), true),
                (Localizador.T("cuidador_acepto"), estado >= 2 && estado != 6),
                (Localizador.T("servicio_en_progreso"), estado >= 3 && estado != 6 && estado != 5),
                (Localizador.T("servicio_completado"), estado == 4)
            };

            if (estado == 6)
                pasos = new List<(string, bool)> { (Localizador.T("solicitud_enviada"), true), (Localizador.T("rechazado_por_cuidador"), true) };
            else if (estado == 5)
                pasos = new List<(string, bool)> { (Localizador.T("solicitud_enviada"), true), (Localizador.T("cancelado"), true) };

            foreach (var (texto, completado) in pasos)
            {
                var punto = new Border
                {
                    Stroke = Colors.Transparent,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                    BackgroundColor = completado ? Color.FromArgb("#2563EB") : Color.FromArgb("#E5E7EB"),
                    WidthRequest = 12,
                    HeightRequest = 12,
                    VerticalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 0, 12, 0)
                };

                var label = new Label
                {
                    Text = texto,
                    FontSize = 14,
                    FontFamily = completado ? "OpenSansSemibold" : "OpenSansRegular",
                    TextColor = completado ? Color.FromArgb("#111827") : Color.FromArgb("#9CA3AF"),
                    VerticalOptions = LayoutOptions.Center
                };

                var fila = new HorizontalStackLayout { Spacing = 0, Children = { punto, label }, Margin = new Thickness(0, 8) };
                ListaPasos.Add(fila);
            }
        }

        private static string FormatearHora(TimeSpan hora)
        {
            return DateTime.Today.Add(hora).ToString("h:mm tt");
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnCalificarClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var parametros = new Dictionary<string, object>
            {
                { "TrabajoId", trabajo.Id },
                { "CalificadoId", trabajo.CuidadorId },
                { "CalificadoNombre", trabajo.CuidadorNombre },
                { "RutaSalida", "//MainPage/ClienteDashboardPage" }
            };
            await Shell.Current.GoToAsync("CalificarPage", parametros);
        }

        private async Task MostrarAnuncioTerminadoAsync()
        {
            OverlayTerminado.IsVisible = true;
            await OverlayTerminado.FadeTo(1, 250);
            await Task.Delay(2200);
            await OverlayTerminado.FadeTo(0, 250);
            OverlayTerminado.IsVisible = false;
        }

        // ---- Propina opcional ----
        private static readonly decimal[] PropinasSugeridas = { 100m, 150m, 200m };
        private const decimal MaxPropina = 50000m; // mismo tope que valida la API
        private decimal propina;       // 0 = sin propina
        private bool propinaOtroMonto; // el cliente eligió "Otro monto"

        private void ConstruirChipsPropina()
        {
            ChipsPropina.Clear();
            AgregarChipPropina(Localizador.T("sin_propina"), !propinaOtroMonto && propina == 0, () => { propina = 0; propinaOtroMonto = false; });
            foreach (var monto in PropinasSugeridas)
            {
                var m = monto;
                AgregarChipPropina($"RD${m:N0}", !propinaOtroMonto && propina == m, () => { propina = m; propinaOtroMonto = false; });
            }
            AgregarChipPropina(Localizador.T("otro_monto"), propinaOtroMonto, () => { propinaOtroMonto = true; propina = ParsearPropina(EntryPropina.Text) ?? 0; });

            BoxPropinaOtro.IsVisible = propinaOtroMonto;
            ActualizarResumenPropina();
        }

        private void AgregarChipPropina(string texto, bool seleccionado, Action alElegir)
        {
            var chip = new Border
            {
                Stroke = seleccionado ? Color.FromArgb("#16A34A") : Color.FromArgb("#F59E0B"),
                StrokeThickness = 1,
                BackgroundColor = seleccionado ? Color.FromArgb("#16A34A") : Colors.White,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
                Padding = new Thickness(14, 8),
                Margin = new Thickness(0, 0, 8, 8),
                Content = new Label
                {
                    Text = texto,
                    FontSize = 13,
                    FontFamily = "OpenSansSemibold",
                    TextColor = seleccionado ? Colors.White : Color.FromArgb("#92400E")
                }
            };
            chip.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    alElegir();
                    ConstruirChipsPropina();
                })
            });
            ChipsPropina.Add(chip);
        }

        private static decimal? ParsearPropina(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;
            return decimal.TryParse(texto.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var valor)
                ? Math.Round(valor, 2)
                : null;
        }

        private void OnPropinaTextChanged(object sender, TextChangedEventArgs e)
        {
            propina = ParsearPropina(EntryPropina.Text) ?? 0;
            ActualizarResumenPropina();
        }

        private void ActualizarResumenPropina()
        {
            LblResumenPropina.IsVisible = trabajo != null && propina > 0;
            if (LblResumenPropina.IsVisible)
                LblResumenPropina.Text = Localizador.F("total_con_propina", trabajo!.Tarifa + propina);
        }

        private async void OnConfirmarFinalizacionClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            if (propinaOtroMonto && (propina <= 0 || propina > MaxPropina))
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("propina_invalida"), Localizador.T("ok"));
                return;
            }

            var clienteId = Preferences.Default.Get("UserId", 0);

            BtnConfirmarFinalizacion.IsEnabled = false;
            BtnConfirmarFinalizacion.Text = Localizador.T("confirmando");

            var propinaEnviada = propina;
            var (success, error) = await _apiService.ConfirmarFinalizacionAsync(trabajo.Id, clienteId, true, propinaEnviada);

            if (success)
            {
                propina = 0;
                propinaOtroMonto = false;
                EntryPropina.Text = "";
                ConstruirChipsPropina();
                await CargarTrabajo();

                if (propinaEnviada > 0)
                    await Alerta.MostrarAsync(Localizador.T("gracias"), Localizador.F("gracias_propina", propinaEnviada), Localizador.T("ok"));
            }
            else
            {
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("no_se_pudo_confirmar_intenta"), Localizador.T("ok"));
                BtnConfirmarFinalizacion.IsEnabled = true;
                BtnConfirmarFinalizacion.Text = Localizador.T("si_termino");
            }
        }

        private async void OnRechazarFinalizacionClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var confirmar = await Alerta.MostrarAsync(Localizador.T("el_trabajo_no_ha_terminado"), Localizador.T("le_avisaremos_a_tu_cuidador"), Localizador.T("si_avisar"), Localizador.T("cancelar"));
            if (!confirmar)
                return;

            var clienteId = Preferences.Default.Get("UserId", 0);
            var (success, error) = await _apiService.ConfirmarFinalizacionAsync(trabajo.Id, clienteId, false);

            if (success)
                await CargarTrabajo();
            else
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("no_se_pudo_registrar_tu"), Localizador.T("ok"));
        }

        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var confirmar = await Alerta.MostrarAsync(Localizador.T("cancelar_solicitud"), Localizador.T("seguro_que_deseas_cancelar_esta"), Localizador.T("si_cancelar"), Localizador.T("no"));
            if (!confirmar)
                return;

            BtnCancelar.IsEnabled = false;
            BtnCancelar.Text = Localizador.T("cancelando");

            var success = await _apiService.ActualizarEstadoTrabajoAsync(trabajo.Id, 5);

            if (success)
            {
                await CargarTrabajo();
            }
            else
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_cancelar_la"), Localizador.T("ok"));
                BtnCancelar.IsEnabled = true;
                BtnCancelar.Text = Localizador.T("cancelar_solicitud");
            }
        }

        private async void OnSOSClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var confirmar = await Alerta.MostrarAsync(
                Localizador.T("sos_pedir_auxilio_2"),
                Localizador.T("se_enviara_una_alerta_de"),
                Localizador.T("si_enviar_alerta"), Localizador.T("cancelar"));
            if (!confirmar)
                return;

            BtnSOS.IsEnabled = false;
            BtnSOS.Text = Localizador.T("enviando_alerta");

            try
            {
                var ubicacion = await LocationService.ObtenerUbicacionActualAsync();
                var latitud = ubicacion?.Latitude ?? 0;
                var longitud = ubicacion?.Longitude ?? 0;

                var usuarioId = Preferences.Default.Get("UserId", 0);
                var tipoUsuario = Preferences.Default.Get("RolId", 0) == 3 ? "Cuidador" : "Cliente";

                var success = await _apiService.EnviarSOSAsync(trabajo.Id, usuarioId, tipoUsuario, latitud, longitud);

                if (success)
                {
                    await Alerta.MostrarAsync(Localizador.T("alerta_enviada"), Localizador.T("nuestro_equipo_de_administracion_ha"), Localizador.T("ok"));
                }
                else
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_enviar_la_2"), Localizador.T("ok"));
                }
            }
            finally
            {
                BtnSOS.IsEnabled = true;
                BtnSOS.Text = Localizador.T("sos_pedir_auxilio");
            }
        }
    }
}
