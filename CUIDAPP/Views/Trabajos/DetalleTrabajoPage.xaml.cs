using CUIDAPP.Localization;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Trabajos
{
    public partial class DetalleTrabajoPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private static readonly HttpClient _httpClient = new HttpClient();
        private Trabajo? trabajo;
        private string? mapaRutaHtml;
        private bool estaVisible;
        private bool geocercaMonitorIniciado;
        private bool fueraDeGeocerca;
        private const double RadioGeocercaKm = 0.3; // 300 metros
        private const string MapboxAccessToken = "pk.eyJ1IjoiZm9yemU5ZGFyayIsImEiOiJjbXNtbmtvb2oxcHV6Mnpwd2s5bTc1YXViIn0.Zm3kXe_m7Ic04GFCjA40DA";

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Trabajo", out var value) && value is Trabajo t)
            {
                trabajo = t;
                RenderizarTrabajo();
            }
        }

        public DetalleTrabajoPage()
        {
            InitializeComponent();
        }

        private bool entradaHecha;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Blanca();
            if (!entradaHecha)
            {
                entradaHecha = true;
                Contenido.Opacity = 0;
                Contenido.TranslationY = 16;
                _ = Contenido.FadeTo(1, 300, Easing.CubicOut);
                _ = Contenido.TranslateTo(0, 0, 360, Easing.CubicOut);
            }
            estaVisible = true;
            RealtimeService.TrabajoActualizado += OnTrabajoActualizadoTiempoReal;
            await CargarMapaRutaAsync();
            await ActualizarBotonCalificarAsync();
            await CargarActividadesAsync();
            await CargarTareasAsync();
            IniciarMonitorGeocercaSiHaceFalta();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            estaVisible = false;
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
        }

        private async void OnTrabajoActualizadoTiempoReal(int trabajoId, int estado)
        {
            if (trabajo == null || trabajo.Id != trabajoId)
                return;

            // Re-consultamos el trabajo completo (no solo el Estado del evento) para traer
            // también RechazadoPorCliente/PagoDisputado actualizados.
            var cuidadorId = Preferences.Default.Get("UserId", 0);
            var trabajos = await _apiService.ObtenerTrabajosAsync(cuidadorId);
            var actualizado = trabajos.FirstOrDefault(t => t.Id == trabajoId);
            if (actualizado == null)
                return;

            var estadoAnterior = trabajo.Estado;
            trabajo = actualizado;
            RenderizarTrabajo();
            await ActualizarBotonCalificarAsync();
            await CargarActividadesAsync();
            await CargarTareasAsync();
            await CargarMapaRutaAsync();

            if (estadoAnterior == 7 && trabajo.Estado == 3 && trabajo.RechazadoPorCliente)
            {
                await Alerta.MostrarAsync(Localizador.T("el_cliente_indico_que_el_2"),
                    Localizador.T("puedes_volver_a_intentar_finalizarlo"), Localizador.T("entendido"));
            }
        }

        private void IniciarMonitorGeocercaSiHaceFalta()
        {
            if (geocercaMonitorIniciado)
                return;
            geocercaMonitorIniciado = true;

            // Mientras el trabajo esté En Progreso y esta pantalla abierta, verificamos
            // periódicamente la distancia del cuidador al sitio del servicio (sin polling
            // al servidor: es solo GPS local + una notificación puntual si se aleja).
            Dispatcher.StartTimer(TimeSpan.FromSeconds(20), () =>
            {
                if (!estaVisible)
                    return false;

                if (trabajo?.Estado == 3)
                    _ = VerificarGeocercaAsync();

                return true;
            });
        }

        private async Task VerificarGeocercaAsync()
        {
            if (trabajo?.Latitud == null || trabajo.Longitud == null)
                return;

            var ubicacionActual = await LocationService.ObtenerUbicacionActualAsync();
            if (ubicacionActual == null)
                return;

            var ubicacionServicio = new Location((double)trabajo.Latitud, (double)trabajo.Longitud);
            var distanciaKm = Location.CalculateDistance(ubicacionActual, ubicacionServicio, DistanceUnits.Kilometers);

            if (distanciaKm > RadioGeocercaKm)
            {
                if (!fueraDeGeocerca)
                {
                    fueraDeGeocerca = true;
                    await _apiService.AlertarGeocercaAsync(trabajo.Id, distanciaKm * 1000);
                    await Alerta.MostrarAsync(Localizador.T("estas_lejos_del_sitio_del"), Localizador.F("te_alejaste_m_del_domicilio", distanciaKm * 1000), Localizador.T("ok"));
                }
            }
            else
            {
                fueraDeGeocerca = false;
            }
        }

        private async Task CargarTareasAsync()
        {
            if (trabajo == null)
                return;

            var tareasTrabajo = await _apiService.ObtenerTareasAsync(trabajo.Id);
            var puedeMarcar = trabajo.Estado == 3;

            CardTareas.IsVisible = tareasTrabajo.Count > 0 && trabajo.Estado is 1 or 2 or 3;
            LblAyudaTareas.IsVisible = puedeMarcar;
            ListaTareas.Clear();

            foreach (var tarea in tareasTrabajo)
            {
                var check = new CheckBox { IsChecked = tarea.Completada, IsEnabled = puedeMarcar && !tarea.Completada, VerticalOptions = LayoutOptions.Center };
                var texto = new Label
                {
                    Text = tarea.Descripcion,
                    FontSize = 14,
                    FontFamily = "OpenSansRegular",
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = tarea.Completada ? (Color)Application.Current!.Resources["ColorTextMuted"] : (Color)Application.Current!.Resources["ColorTextStrong"],
                    TextDecorations = tarea.Completada ? TextDecorations.Strikethrough : TextDecorations.None
                };

                var tareaId = tarea.Id;
                check.CheckedChanged += async (s, args) =>
                {
                    if (!args.Value)
                        return;

                    check.IsEnabled = false;
                    var ok = await _apiService.CompletarTareaAsync(tareaId);
                    if (ok)
                    {
                        texto.TextColor = (Color)Application.Current!.Resources["ColorTextMuted"];
                        texto.TextDecorations = TextDecorations.Strikethrough;
                    }
                    else
                    {
                        check.IsChecked = false;
                        check.IsEnabled = true;
                        await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_marcar_la"), Localizador.T("ok"));
                    }
                };

                var fila = new HorizontalStackLayout { Spacing = 6 };
                fila.Add(check);
                fila.Add(texto);
                ListaTareas.Add(fila);
            }
        }

        private async Task CargarActividadesAsync()
        {
            if (trabajo == null || trabajo.Estado != 3)
                return;

            var actividades = await _apiService.ObtenerActividadesAsync(trabajo.Id);
            RenderizarActividades(actividades);
        }

        private void RenderizarActividades(List<ActividadTrabajo> actividades)
        {
            ListaActividades.Clear();
            LblSinActividades.IsVisible = actividades.Count == 0;

            foreach (var actividad in actividades)
            {
                ListaActividades.Add(new Border
                {
                    Stroke = Colors.Transparent,
                    BackgroundColor = (Color)Application.Current!.Resources["ColorBackground"],
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(12, 10),
                    Content = new VerticalStackLayout
                    {
                        Spacing = 2,
                        Children =
                        {
                            new Label { Text = actividad.Descripcion, FontSize = 14, FontFamily = "OpenSansRegular", TextColor = (Color)Application.Current!.Resources["ColorTextStrong"] },
                            new Label { Text = actividad.FechaHora.ToString("h:mm tt"), FontSize = 11, FontFamily = "OpenSansRegular", TextColor = (Color)Application.Current!.Resources["ColorTextMuted"] }
                        }
                    }
                });
            }
        }

        private async void OnAgregarActividadClicked(object sender, EventArgs e)
        {
            if (trabajo == null || string.IsNullOrWhiteSpace(EntryActividad.Text))
                return;

            var descripcion = EntryActividad.Text.Trim();

            BtnAgregarActividad.IsEnabled = false;
            BtnAgregarActividad.Text = "...";

            try
            {
                var success = await _apiService.AgregarActividadAsync(trabajo.Id, descripcion);
                if (success)
                {
                    EntryActividad.Text = "";
                    await CargarActividadesAsync();
                }
                else
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_enviar_el"), Localizador.T("ok"));
                }
            }
            finally
            {
                BtnAgregarActividad.IsEnabled = true;
                BtnAgregarActividad.Text = Localizador.T("enviar");
            }
        }

        private async Task ActualizarBotonCalificarAsync()
        {
            if (trabajo == null || trabajo.Estado != 4)
            {
                BtnCalificarCliente.IsVisible = false;
                CardYaCalifico.IsVisible = false;
                return;
            }

            var cuidadorId = Preferences.Default.Get("UserId", 0);
            if (cuidadorId == 0)
                return;

            var calificacion = await _apiService.ObtenerCalificacionDeTrabajoAsync(trabajo.Id, cuidadorId);

            BtnCalificarCliente.IsVisible = calificacion == null;
            CardYaCalifico.IsVisible = calificacion != null;

            if (calificacion != null)
                LblEstrellasDadas.Text = new string('★', calificacion.Puntuacion) + new string('☆', 5 - calificacion.Puntuacion);
        }

        private async void OnCalificarClienteClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var parametros = new Dictionary<string, object>
            {
                { "TrabajoId", trabajo.Id },
                { "CalificadoId", trabajo.ClienteId },
                { "CalificadoNombre", trabajo.ClienteNombre },
                { "RutaSalida", "//MainPage/CuidadorDashboardPage" }
            };
            await Shell.Current.GoToAsync("CalificarPage", parametros);
        }

        private void RenderizarTrabajo()
        {
            if (trabajo == null)
                return;

            LblClienteNombre.Text = trabajo.ClienteNombre;
            if (!string.IsNullOrWhiteSpace(trabajo.ClienteFotoUrl))
                ImgCliente.Source = $"{ApiService.ServerOrigin}{trabajo.ClienteFotoUrl}";

            LblTipoServicio.Text = Localizador.D(trabajo.TipoServicio);
            LblFecha.Text = Localizador.FechaLarga(trabajo.Fecha);
            LblHora.Text = $"{FormatearHora(trabajo.HoraInicio)} - {FormatearHora(trabajo.HoraFin)}";
            LblDireccion.Text = string.IsNullOrWhiteSpace(trabajo.Direccion) ? Localizador.T("sin_direccion") : trabajo.Direccion;
            LblPago.Text = Localizador.F("rd", trabajo.Tarifa);

            var (colorFondo, colorTexto, texto) = trabajo.Estado switch
            {
                1 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), Localizador.T("pendiente")),
                2 => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#1E40AF"), Localizador.T("aceptado")),
                3 => (Color.FromArgb("#EDE9FE"), Color.FromArgb("#5B21B6"), Localizador.T("en_progreso")),
                4 => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#166534"), Localizador.T("completado")),
                5 => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), Localizador.T("cancelado")),
                6 => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#991B1B"), Localizador.T("rechazado")),
                7 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), Localizador.T("esperando_confirmacion_del_cliente")),
                _ => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), Localizador.T("desconocido"))
            };
            BadgeEstado.BackgroundColor = colorFondo;
            LblEstado.TextColor = colorTexto;
            LblEstado.Text = texto;

            PanelPendiente.IsVisible = trabajo.Estado == 1;
            BtnIniciar.IsVisible = trabajo.Estado == 2;
            BtnCompletar.IsVisible = trabajo.Estado == 3;
            BtnCancelar.IsVisible = trabajo.Estado == 2 || trabajo.Estado == 3;
            BtnSOS.IsVisible = trabajo.Estado == 2 || trabajo.Estado == 3;
            CardActividades.IsVisible = trabajo.Estado == 3;
            BtnChat.IsVisible = trabajo.Estado is 2 or 3 or 7;

            CardRechazado.IsVisible = trabajo.Estado == 3 && trabajo.RechazadoPorCliente;
            CardEsperandoConfirmacion.IsVisible = trabajo.Estado == 7;
        }

        private async void OnInsistirSinCobroClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var confirmar = await Alerta.MostrarAsync(
                Localizador.T("insistir_sin_cobro"),
                Localizador.T("el_trabajo_se_marcara_como"),
                Localizador.T("si_insistir"), Localizador.T("cancelar"));
            if (!confirmar)
                return;

            var cuidadorId = Preferences.Default.Get("UserId", 0);
            var (success, error) = await _apiService.ForzarFinalizacionAsync(trabajo.Id, cuidadorId);

            if (success)
                await Shell.Current.GoToAsync("..");
            else
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("no_se_pudo_forzar_la"), Localizador.T("ok"));
        }

        private async void OnChatClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var parametros = new Dictionary<string, object>
            {
                { "TrabajoId", trabajo.Id },
                { "OtroNombre", trabajo.ClienteNombre },
                { "OtroFotoUrl", trabajo.ClienteFotoUrl ?? "" }
            };
            await Shell.Current.GoToAsync("ChatPage", parametros);
        }

        private static string FormatearHora(TimeSpan hora)
        {
            return DateTime.Today.Add(hora).ToString("h:mm tt");
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnAceptarClicked(object sender, EventArgs e)
        {
            BtnAceptar.IsEnabled = BtnRechazar.IsEnabled = false;
            BtnAceptar.Text = Localizador.T("aceptando");
            await CambiarEstado(2);
            BtnAceptar.IsEnabled = BtnRechazar.IsEnabled = true;
            BtnAceptar.Text = Localizador.T("aceptar");
        }

        private async void OnRechazarClicked(object sender, EventArgs e)
        {
            BtnAceptar.IsEnabled = BtnRechazar.IsEnabled = false;
            BtnRechazar.Text = Localizador.T("rechazando");
            await CambiarEstado(6);
            BtnAceptar.IsEnabled = BtnRechazar.IsEnabled = true;
            BtnRechazar.Text = Localizador.T("rechazar");
        }

        private const double DistanciaMaximaKm = 0.15; // 150 metros

        private async void OnIniciarClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            if (trabajo.Fecha.Date != ServerClock.Today)
            {
                var mensaje = trabajo.Fecha.Date > ServerClock.Today
                    ? Localizador.F("servicio_programado_futuro", Localizador.DiaMes(trabajo.Fecha))
                    : Localizador.F("servicio_programado_pasado", Localizador.DiaMes(trabajo.Fecha));
                await Alerta.MostrarAsync(Localizador.T("no_es_la_fecha_del"), mensaje, Localizador.T("ok"));
                return;
            }

            if (ServerClock.Now.TimeOfDay > trabajo.HoraFin)
            {
                var continuar = await Alerta.MostrarAsync(
                    Localizador.T("el_horario_programado_ya_paso"),
                    Localizador.F("este_servicio_estaba_programado_de", FormatearHora(trabajo.HoraInicio), FormatearHora(trabajo.HoraFin)),
                    Localizador.T("si_iniciar"), Localizador.T("cancelar"));
                if (!continuar)
                    return;
            }

            if (trabajo.Latitud != null && trabajo.Longitud != null)
            {
                var ubicacionActual = await LocationService.ObtenerUbicacionActualAsync();
                if (ubicacionActual == null)
                {
                    await Alerta.MostrarAsync(Localizador.T("ubicacion_no_disponible"), Localizador.T("no_pudimos_verificar_tu_ubicacion"), Localizador.T("ok"));
                    return;
                }

                var ubicacionServicio = new Location((double)trabajo.Latitud, (double)trabajo.Longitud);
                var distanciaKm = Location.CalculateDistance(ubicacionActual, ubicacionServicio, DistanceUnits.Kilometers);

                if (distanciaKm > DistanciaMaximaKm)
                {
                    await Alerta.MostrarAsync(Localizador.T("estas_muy_lejos"), Localizador.F("debes_estar_en_la_direccion", distanciaKm * 1000), Localizador.T("ok"));
                    return;
                }
            }

            var parametros = new Dictionary<string, object> { { "Trabajo", trabajo } };
            await Shell.Current.GoToAsync("IniciarTrabajoPage", parametros);
        }

        private async void OnCompletarClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var parametros = new Dictionary<string, object> { { "Trabajo", trabajo } };
            await Shell.Current.GoToAsync("FinalizarTrabajoPage", parametros);
        }

        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var parametros = new Dictionary<string, object> { { "Trabajo", trabajo } };
            await Shell.Current.GoToAsync("CancelarServicioPage", parametros);
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

        private async Task CargarMapaRutaAsync()
        {
            if (trabajo == null || trabajo.Latitud == null || trabajo.Longitud == null)
                return;

            if (trabajo.Estado is not (1 or 2 or 3))
                return;

            CardMapa.IsVisible = true;
            MapaRutaLoading.IsRunning = true;
            MapaRutaLoading.IsVisible = true;
            LblSinUbicacionCuidador.IsVisible = false;

            var destinoLat = (double)trabajo.Latitud;
            var destinoLon = (double)trabajo.Longitud;

            var ubicacionActual = await LocationService.ObtenerUbicacionActualAsync();
            var hayUbicacionCuidador = ubicacionActual != null;
            var origenLat = ubicacionActual?.Latitude ?? destinoLat;
            var origenLon = ubicacionActual?.Longitude ?? destinoLon;

            var puntosRuta = hayUbicacionCuidador
                ? (await ObtenerRutaAsync(origenLat, origenLon, destinoLat, destinoLon))
                    ?? new List<(double Lat, double Lon)> { (origenLat, origenLon), (destinoLat, destinoLon) }
                : null;

            mapaRutaHtml = ConstruirHtmlRuta(origenLat, origenLon, destinoLat, destinoLon, hayUbicacionCuidador, puntosRuta);
            MapaRuta.Source = new HtmlWebViewSource { Html = mapaRutaHtml };

            LblSinUbicacionCuidador.IsVisible = !hayUbicacionCuidador;

            MapaRutaLoading.IsRunning = false;
            MapaRutaLoading.IsVisible = false;
        }

        private static string ConstruirHtmlRuta(double origenLat, double origenLon, double destinoLat, double destinoLon, bool hayUbicacionCuidador, List<(double Lat, double Lon)>? puntosRuta)
        {
            var ci = CultureInfo.InvariantCulture;
            var destLat = destinoLat.ToString(ci);
            var destLon = destinoLon.ToString(ci);

            var marcadorCuidadorJs = "";
            var lineaRutaJs = "";
            var encuadreJs = $"map.setView([{destLat}, {destLon}], 15);";

            if (hayUbicacionCuidador)
            {
                var origLat = origenLat.ToString(ci);
                var origLon = origenLon.ToString(ci);

                marcadorCuidadorJs = $"L.circleMarker([{origLat}, {origLon}], {{ radius: 8, color: '#FFFFFF', weight: 3, fillColor: '#2563EB', fillOpacity: 1 }}).addTo(map);";

                var coordsJson = JsonSerializer.Serialize((puntosRuta ?? new List<(double Lat, double Lon)>())
                    .Select(p => new[] { p.Lat, p.Lon }));
                lineaRutaJs = $"var rutaCoords = {coordsJson}; L.polyline(rutaCoords, {{ color: '#2563EB', weight: 5 }}).addTo(map);";

                encuadreJs = $"map.fitBounds([[{origLat}, {origLon}], [{destLat}, {destLon}]], {{ padding: [40, 40] }});";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <style>
        html, body, #map {{ height: 100%; margin: 0; padding: 0; background: #EAECEF; }}
        .leaflet-control-attribution {{ display: none; }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <script>
        var map = L.map('map', {{ zoomControl: false, attributionControl: false }});
        L.tileLayer('https://api.mapbox.com/styles/v1/mapbox/light-v11/tiles/{{z}}/{{x}}/{{y}}{{r}}?access_token={MapboxAccessToken}', {{ maxZoom: 20, tileSize: 512, zoomOffset: -1 }}).addTo(map);

        {lineaRutaJs}
        {marcadorCuidadorJs}
        L.circleMarker([{destLat}, {destLon}], {{ radius: 9, color: '#FFFFFF', weight: 3, fillColor: '#EF4444', fillOpacity: 1 }}).addTo(map);

        {encuadreJs}
    </script>
</body>
</html>";
        }

        private async void OnMapaTapped(object sender, EventArgs e)
        {
            if (mapaRutaHtml == null)
                return;

            var parametros = new Dictionary<string, object> { { "Html", mapaRutaHtml } };
            await Shell.Current.GoToAsync("MapaCompletoPage", parametros);
        }

        private async Task<List<(double Lat, double Lon)>?> ObtenerRutaAsync(double origenLat, double origenLon, double destinoLat, double destinoLon)
        {
            try
            {
                var url = $"https://api.mapbox.com/directions/v5/mapbox/driving/{origenLon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{origenLat.ToString(System.Globalization.CultureInfo.InvariantCulture)};{destinoLon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{destinoLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}?overview=full&geometries=geojson&access_token={MapboxAccessToken}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var respuesta = await _httpClient.GetFromJsonAsync<OsrmResponse>(url, cts.Token);

                var coordenadas = respuesta?.Routes?.FirstOrDefault()?.Geometry?.Coordinates;
                if (coordenadas == null || coordenadas.Count < 2)
                    return null;

                return coordenadas.Select(c => (Lat: c[1], Lon: c[0])).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo ruta: {ex.Message}");
                return null;
            }
        }

        private class OsrmResponse
        {
            [JsonPropertyName("routes")]
            public List<OsrmRoute>? Routes { get; set; }
        }

        private class OsrmRoute
        {
            [JsonPropertyName("geometry")]
            public OsrmGeometry? Geometry { get; set; }
        }

        private class OsrmGeometry
        {
            [JsonPropertyName("coordinates")]
            public List<List<double>>? Coordinates { get; set; }
        }

        private async Task CambiarEstado(int nuevoEstado)
        {
            if (trabajo == null)
                return;

            AvisosApp.MarcarAccionPropia(trabajo.Id, nuevoEstado);
            var success = await _apiService.ActualizarEstadoTrabajoAsync(trabajo.Id, nuevoEstado);

            if (success)
            {
                // Al aceptar un servicio se recuerda llevar siempre el celular (detección de caídas).
                var irAPerfil = nuevoEstado == 2 && await DeadManService.MostrarAvisoCelularAsync(this);
                await Shell.Current.GoToAsync("..");
                if (irAPerfil)
                    await Shell.Current.GoToAsync("CuidadorPerfilPage");
            }
            else
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_actualizar_el"), Localizador.T("ok"));
            }
        }
    }
}
