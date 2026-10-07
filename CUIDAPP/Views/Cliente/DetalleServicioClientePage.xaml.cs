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

            // Borde a borde: el encabezado empieza debajo de la barra de estado y el contenido
            // termina con espacio para la barra de gestos.
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            new Animation(t => PuntoTrabajando.Opacity = 0.35 + 0.65 * Math.Abs(Math.Cos(t * Math.PI)), 0, 1)
                .Commit(this, "PuntoTrabajando", length: 1400, easing: Easing.Linear, repeat: () => true);
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
            this.AbortAnimation("PuntoTrabajando");
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
                var hecha = tarea.Completada;
                var check = new Border
                {
                    Stroke = hecha ? Colors.Transparent : (Color)Application.Current!.Resources["ColorPrimaryLight"],
                    StrokeThickness = 2,
                    BackgroundColor = hecha ? Tema.C("ColorSuccess") : Tema.C("ColorSurface"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    WidthRequest = 22,
                    HeightRequest = 22,
                    VerticalOptions = LayoutOptions.Center,
                    Content = hecha ? new Microsoft.Maui.Controls.Shapes.Path
                    {
                        Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString("M9 16.17L4.83 12L3.41 13.41L9 19L21 7L19.59 5.59L9 16.17Z")!,
                        Fill = Colors.White, Aspect = Stretch.Uniform, WidthRequest = 12, HeightRequest = 12,
                        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                    } : null
                };
                var fila = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
                fila.Add(check, 0);
                fila.Add(new Label
                {
                    Text = tarea.Descripcion,
                    FontSize = 14,
                    FontFamily = "OpenSansRegular",
                    TextColor = hecha ? (Color)Application.Current!.Resources["ColorTextMuted"] : (Color)Application.Current!.Resources["ColorTextStrong"],
                    TextDecorations = hecha ? TextDecorations.Strikethrough : TextDecorations.None,
                    VerticalOptions = LayoutOptions.Center
                }, 1);
                ListaTareasCliente.Add(new Border
                {
                    Stroke = Colors.Transparent,
                    BackgroundColor = Tema.C("ColorSurface"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                    Padding = new Thickness(14, 12),
                    Content = fila
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

            if (!ContenedorInfo.IsVisible)
            {
                Contenido.Opacity = 0;
                Contenido.TranslationY = 18;
                ContenedorInfo.IsVisible = true;
                _ = Contenido.FadeTo(1, 300, Easing.CubicOut);
                _ = Contenido.TranslateTo(0, 0, 360, Easing.CubicOut);
            }
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
            LblPago.Text = Localizador.F("rd_monto", t.Tarifa);

            var (colorFondo, colorTexto, texto) = t.Estado switch
            {
                1 => (Tema.C("ColorWarningSoft"), Tema.C("ColorWarning"), Localizador.T("banner_esperando")),
                2 => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#1E40AF"), Localizador.T("aceptado_tu_cuidador_asistira_en")),
                3 => (Color.FromArgb("#EDE9FE"), Color.FromArgb("#5B21B6"), Localizador.T("en_progreso")),
                4 => (Tema.C("ColorSuccessSoft"), Tema.C("ColorSuccess"), Localizador.T("servicio_completado")),
                5 => (Tema.C("ColorSubtle"), Tema.C("ColorTextStrong"), Localizador.T("cancelado")),
                6 => (Tema.C("ColorDangerSoft"), Tema.C("ColorDanger"), Localizador.T("rechazado_por_cuidador")),
                7 => (Tema.C("ColorWarningSoft"), Tema.C("ColorWarning"), Localizador.T("esperando_tu_confirmacion")),
                _ => (Tema.C("ColorSubtle"), Tema.C("ColorTextStrong"), Localizador.T("desconocido"))
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
            var fila = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            fila.Add(new Microsoft.Maui.Controls.Shapes.Ellipse { Fill = (Color)Application.Current!.Resources["ColorPrimary"], WidthRequest = 8, HeightRequest = 8, VerticalOptions = LayoutOptions.Center }, 0);
            fila.Add(new Label { Text = descripcion, FontSize = 14, FontFamily = "OpenSansRegular", TextColor = (Color)Application.Current!.Resources["ColorTextStrong"], VerticalOptions = LayoutOptions.Center }, 1);
            fila.Add(new Label { Text = fechaHora.ToString("h:mm tt"), FontSize = 11, FontFamily = "OpenSansSemibold", TextColor = (Color)Application.Current!.Resources["ColorTextMuted"], VerticalOptions = LayoutOptions.Center }, 2);
            return new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Tema.C("ColorSurface"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(14, 12),
                Content = fila
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

            var primario = (Color)Application.Current!.Resources["ColorPrimary"];
            var borde = (Color)Application.Current!.Resources["ColorBorder"];
            for (var i = 0; i < pasos.Count; i++)
            {
                var (texto, completado) = pasos[i];
                var esUltimo = i == pasos.Count - 1;
                var siguienteCompletado = !esUltimo && pasos[i + 1].Completado;

                var punto = new Border
                {
                    Stroke = completado ? Colors.Transparent : borde,
                    StrokeThickness = 2,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                    BackgroundColor = completado ? primario : Tema.C("ColorSurface"),
                    WidthRequest = 22,
                    HeightRequest = 22,
                    HorizontalOptions = LayoutOptions.Center,
                    Content = completado ? new Microsoft.Maui.Controls.Shapes.Path
                    {
                        Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString("M9 16.17L4.83 12L3.41 13.41L9 19L21 7L19.59 5.59L9 16.17Z")!,
                        Fill = Colors.White, Aspect = Stretch.Uniform, WidthRequest = 11, HeightRequest = 11,
                        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                    } : null
                };

                // Línea vertical hacia el siguiente paso
                var linea = new BoxView
                {
                    WidthRequest = 2,
                    HeightRequest = 22,
                    Color = siguienteCompletado ? primario : borde,
                    HorizontalOptions = LayoutOptions.Center,
                    IsVisible = !esUltimo
                };

                var columna = new VerticalStackLayout { Spacing = 0, WidthRequest = 22, Children = { punto, linea } };

                var label = new Label
                {
                    Text = texto,
                    FontSize = 14,
                    FontFamily = completado ? "OpenSansSemibold" : "OpenSansRegular",
                    TextColor = completado ? (Color)Application.Current!.Resources["ColorTextStrong"] : (Color)Application.Current!.Resources["ColorTextMuted"],
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, 1, 0, 0)
                };

                var fila = new Grid { ColumnSpacing = 14, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
                fila.Add(columna, 0);
                fila.Add(label, 1);
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

            // Fila 1: montos sugeridos, en tarjetas del mismo ancho ("RD$" arriba, el número grande).
            var montos = new Grid { ColumnSpacing = 8 };
            for (int c = 0; c < PropinasSugeridas.Length; c++)
            {
                montos.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                var m = PropinasSugeridas[c];
                montos.Add(CrearOpcionPropina(null, $"{m:N0}", !propinaOtroMonto && propina == m,
                    () => { propina = m; propinaOtroMonto = false; }), c, 0);
            }
            ChipsPropina.Add(montos);

            // Fila 2: sin propina / otro monto.
            var opciones = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
            opciones.Add(CrearOpcionPropina(Localizador.T("sin_propina"), null, !propinaOtroMonto && propina == 0,
                () => { propina = 0; propinaOtroMonto = false; }), 0, 0);
            opciones.Add(CrearOpcionPropina(Localizador.T("otro_monto"), null, propinaOtroMonto,
                () => { propinaOtroMonto = true; propina = ParsearPropina(EntryPropina.Text) ?? 0; }), 1, 0);
            ChipsPropina.Add(opciones);

            BoxPropinaOtro.IsVisible = propinaOtroMonto;
            ActualizarResumenPropina();
        }

        // Tarjeta de opción: con monto ("RD$" + número) o con texto. Seleccionada = verde lleno.
        private View CrearOpcionPropina(string? texto, string? monto, bool seleccionado, Action alElegir)
        {
            var verde = Tema.C("ColorSuccess");
            var colorTexto = seleccionado ? Colors.White : (Color)Application.Current!.Resources["ColorTextStrong"];

            View contenido;
            if (monto != null)
            {
                var pila = new VerticalStackLayout { Spacing = 0, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
                pila.Add(new Label { Text = "RD$", FontSize = 11, FontFamily = "OpenSansSemibold", HorizontalOptions = LayoutOptions.Center,
                                     TextColor = seleccionado ? Color.FromArgb("#D7F0DC") : (Color)Application.Current!.Resources["ColorTextMuted"] });
                pila.Add(new Label { Text = monto, FontSize = 19, FontFamily = "OpenSansSemibold", TextColor = colorTexto, HorizontalOptions = LayoutOptions.Center });
                contenido = pila;
            }
            else
            {
                contenido = new Label { Text = texto, FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = colorTexto,
                                        HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            }

            var tarjeta = new Border
            {
                Stroke = seleccionado ? verde : Tema.C("ColorBorder"),
                StrokeThickness = 1.5,
                BackgroundColor = seleccionado ? verde : Tema.C("ColorSubtle"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                HeightRequest = monto != null ? 62 : 46,
                Content = contenido
            };
            tarjeta.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    await tarjeta.ScaleTo(0.95, 70, Easing.CubicOut);
                    alElegir();
                    ConstruirChipsPropina();
                })
            });
            return tarjeta;
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
            CajaResumenPropina.IsVisible = LblResumenPropina.IsVisible = trabajo != null && propina > 0;
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
            AvisosApp.MarcarAccionPropia(trabajo.Id, 4);
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
            AvisosApp.MarcarAccionPropia(trabajo.Id, 3);
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

            AvisosApp.MarcarAccionPropia(trabajo.Id, 5);
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
