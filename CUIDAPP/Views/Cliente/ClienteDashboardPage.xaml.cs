using CUIDAPP.Localization;
using System.Globalization;
using System.Text.Json;
using CUIDAPP.Models.Busqueda;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class ClienteDashboardPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private double latitudActual = LocationService.LatitudPorDefecto;
        private double longitudActual = LocationService.LongitudPorDefecto;
        private List<ServicioCercano> serviciosCercanos = new();
        private string? categoriaSeleccionada;
        private bool panelExpandido = true;
        private bool yaCargado = false;
        private bool hayServicioActivo = false;
        private const double AlturaPanelExpandido = 460;
        private const double AlturaPanelColapsado = 100;
        private bool mapaHtmlCargado = false;
        private List<CuidadorMapa> cuidadoresEnMapa = new();
        private bool estaVisible;
        private bool pollingIniciado;
        private const string MapboxAccessToken = "pk.eyJ1IjoiZm9yemU5ZGFyayIsImEiOiJjbXNtbmtvb2oxcHV6Mnpwd2s5bTc1YXViIn0.Zm3kXe_m7Ic04GFCjA40DA";

        public ClienteDashboardPage()
        {
            InitializeComponent();

            // Última ubicación conocida (persistida entre sesiones): así el mapa arranca
            // en el lugar correcto de inmediato, aunque esta sea una instancia nueva de la
            // página y el GPS todavía no haya respondido.
            var latGuardada = Preferences.Default.Get("UltimaLatitud", 0.0);
            var lngGuardada = Preferences.Default.Get("UltimaLongitud", 0.0);
            if (latGuardada != 0.0 && lngGuardada != 0.0)
            {
                latitudActual = latGuardada;
                longitudActual = lngGuardada;
            }

            MapaWebView.Navigated += OnMapaNavigated;
        }

        private void OnMapaNavigated(object? sender, WebNavigatedEventArgs e)
        {
            // Android a veces recrea la vista nativa del WebView al volver a esta página
            // (aunque el HTML no cambió), lo que dispara una recarga fuera de nuestro
            // control. En vez de pelear contra eso, reaplicamos el estado actual (marcador
            // + cuidadores) apenas termina de cargar, para que el "reload" sea imperceptible.
            if (mapaHtmlCargado)
            {
                ReposicionarMarcador();
                ActualizarCuidadoresEnMapa();
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            estaVisible = true;

            var clienteIdActual = Preferences.Default.Get("UserId", 0);
            _ = RealtimeService.ConectarAsync(clienteIdActual);

            // Evita suscripciones duplicadas si OnAppearing se dispara más de una vez sin
            // un OnDisappearing intermedio (puede pasar con navegación "//").
            RealtimeService.DisponibilidadCambio -= OnDisponibilidadCambioTiempoReal;
            RealtimeService.UbicacionCuidadorCambio -= OnUbicacionCuidadorCambioTiempoReal;
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
            RealtimeService.DisponibilidadCambio += OnDisponibilidadCambioTiempoReal;
            RealtimeService.UbicacionCuidadorCambio += OnUbicacionCuidadorCambioTiempoReal;
            RealtimeService.TrabajoActualizado += OnTrabajoActualizadoTiempoReal;

            IniciarBuclesAmbiente();
            _ = AnimarEntradaAsync();

            try
            {
                await CargarPantallaAsync();
            }
            catch (Exception ex)
            {
                // Una excepción sin atrapar aquí (OnAppearing es "async void") mata la
                // app en Android en vez de solo mostrar un error.
                Console.WriteLine($"[ClienteDashboardPage] Error cargando dashboard: {ex}");
                OverlayCarga.IsVisible = false;
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_cargar_tu"), Localizador.T("ok"));
            }
        }

        private async Task CargarPantallaAsync()
        {
            IniciarPollingSiHaceFalta();

            var nombre = Preferences.Default.Get("UserNombre", "");
            var primerNombre = string.IsNullOrWhiteSpace(nombre) ? "" : nombre.Split(' ').First();
            LblSaludo.Text = string.IsNullOrWhiteSpace(primerNombre) ? Localizador.T("hola") : Localizador.F("hola_2", primerNombre);

            var fotoUrl = Preferences.Default.Get("UserFotoUrl", "");
            if (!string.IsNullOrWhiteSpace(fotoUrl))
                ImgFotoPerfil.Source = $"{ApiService.ServerOrigin}{fotoUrl}";

            if (yaCargado)
            {
                // Ya tenemos mapa y ubicación cargados de una visita anterior: solo
                // refrescamos servicio activo, servicios y cuidadores cercanos, sin overlay ni recarga del mapa.
                await VerificarServicioActivo();
                await CargarServiciosCercanos();
                await CargarCuidadoresEnMapa();
                return;
            }

            // El mapa se muestra de inmediato con la última ubicación conocida (o la de
            // por defecto). El overlay solo cubre la carga de datos (servicios/estado),
            // nunca el mapa, para que no se sienta como un bloqueo gigante.
            CargarMapa();

            OverlayCarga.Opacity = 0;
            OverlayCarga.IsVisible = true;
            TarjetaCarga.Scale = 0.94;
            _ = OverlayCarga.FadeTo(1, 200, Easing.CubicOut);
            _ = TarjetaCarga.ScaleTo(1, 260, Easing.CubicOut);

            // El GPS puede tardar varios segundos: se resuelve en paralelo y, cuando
            // llega, solo se reposiciona el marcador y se recentra el mapa (sin recargarlo).
            _ = ActualizarUbicacionRealAsync();

            await VerificarServicioActivo();
            await CargarServiciosCercanos();
            await CargarCuidadoresEnMapa();

            yaCargado = true;

            await Task.WhenAll(OverlayCarga.FadeTo(0, 220, Easing.CubicOut), TarjetaCarga.ScaleTo(0.96, 220, Easing.CubicOut));
            OverlayCarga.IsVisible = false;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            estaVisible = false;
            DetenerBuclesAmbiente();
            RealtimeService.DisponibilidadCambio -= OnDisponibilidadCambioTiempoReal;
            RealtimeService.UbicacionCuidadorCambio -= OnUbicacionCuidadorCambioTiempoReal;
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
        }

        private async void OnDisponibilidadCambioTiempoReal(int cuidadorId, bool disponible)
        {
            if (!hayServicioActivo)
                await CargarServiciosCercanos();
            await CargarCuidadoresEnMapa();
        }

        private void OnUbicacionCuidadorCambioTiempoReal(int cuidadorId, double lat, double lng)
        {
            var punto = cuidadoresEnMapa.FirstOrDefault(c => c.Id == cuidadorId);
            if (punto != null)
            {
                punto.Latitud = (decimal)lat;
                punto.Longitud = (decimal)lng;
                ActualizarCuidadoresEnMapa();
            }
        }

        private async void OnTrabajoActualizadoTiempoReal(int trabajoId, int estado)
        {
            await VerificarServicioActivo();
        }

        private void IniciarPollingSiHaceFalta()
        {
            if (pollingIniciado)
                return;
            pollingIniciado = true;

            // Mientras el cliente esté viendo el dashboard, refrescamos periódicamente los
            // servicios y cuidadores cercanos, para reflejar en "tiempo real" cuando un
            // cuidador se conecta/desconecta (sin esperar a que el cliente navegue y vuelva).
            Dispatcher.StartTimer(TimeSpan.FromSeconds(8), () =>
            {
                if (!estaVisible)
                    return false;

                if (!hayServicioActivo)
                    _ = CargarServiciosCercanos();

                _ = CargarCuidadoresEnMapa();
                return true;
            });
        }

        private async Task ActualizarUbicacionRealAsync()
        {
            var ubicacion = await LocationService.ObtenerUbicacionActualAsync();
            if (ubicacion == null)
                return;

            // Si estaba usando la ubicación por defecto, refina el mapa y la búsqueda
            // con la posición GPS real, sin mostrar ningún overlay ni bloquear la UI.
            var eraUbicacionPorDefecto = latitudActual == LocationService.LatitudPorDefecto && longitudActual == LocationService.LongitudPorDefecto;

            latitudActual = ubicacion.Latitude;
            longitudActual = ubicacion.Longitude;

            Preferences.Default.Set("UltimaLatitud", latitudActual);
            Preferences.Default.Set("UltimaLongitud", longitudActual);

            ReposicionarMarcador();
            await CargarCuidadoresEnMapa();

            if (eraUbicacionPorDefecto)
                await CargarServiciosCercanos();
        }

        private async Task VerificarServicioActivo()
        {
            var clienteId = Preferences.Default.Get("UserId", 0);
            if (clienteId == 0)
                return;

            var serviciosActivos = await _apiService.ObtenerTrabajosActivosPorClienteAsync(clienteId);
            hayServicioActivo = serviciosActivos.Count > 0;

            // El cliente ahora puede tener varios servicios a la vez, así que el buscador
            // se mantiene siempre disponible; el banner es solo un atajo a "Mis servicios".
            MostrarBanner(hayServicioActivo);

            if (serviciosActivos.Count == 1)
            {
                var t = serviciosActivos[0];
                var estadoTexto = t.Estado switch
                {
                    1 => Localizador.T("banner_esperando"),
                    2 => Localizador.T("banner_aceptado"),
                    3 => Localizador.T("en_progreso"),
                    4 => Localizador.T("banner_completado"),
                    7 => Localizador.T("banner_confirmalo"),
                    _ => Localizador.T("en_curso")
                };
                LblBannerServicioActivo.Text = Localizador.F("tipo_estado_banner", Localizador.D(t.TipoServicio), estadoTexto);
            }
            else if (serviciosActivos.Count > 1)
            {
                LblBannerServicioActivo.Text = Localizador.F("tienes_servicios_activos", serviciosActivos.Count);
            }
        }

        private async void OnVerServicioActivoTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("MiServicioPage");
        }

        private void CargarMapa()
        {
            var lat = latitudActual.ToString(CultureInfo.InvariantCulture);
            var lng = longitudActual.ToString(CultureInfo.InvariantCulture);

            var html = $@"
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
        var map = L.map('map', {{ zoomControl: false, attributionControl: false }}).setView([{lat}, {lng}], 14);
        L.tileLayer('https://api.mapbox.com/styles/v1/mapbox/light-v11/tiles/{{z}}/{{x}}/{{y}}{{r}}?access_token={MapboxAccessToken}', {{ maxZoom: 20, tileSize: 512, zoomOffset: -1 }}).addTo(map);
        var marker = L.circleMarker([{lat}, {lng}], {{ radius: 8, color: '#FFFFFF', weight: 3, fillColor: '#2563EB', fillOpacity: 1 }}).addTo(map);
        var capaCuidadores = L.layerGroup().addTo(map);

        // Se llama desde C# cuando llega una ubicación GPS más precisa, sin recargar la página.
        function actualizarUbicacion(lat, lng) {{
            var latlng = [lat, lng];
            marker.setLatLng(latlng);
            map.setView(latlng, map.getZoom());
        }}

        // Se llama desde C# con la lista de cuidadores cercanos [[lat,lng], ...],
        // mostrando un puntito por cada uno (estilo Uber), sin recargar la página.
        function actualizarCuidadores(puntos) {{
            capaCuidadores.clearLayers();
            puntos.forEach(function(p) {{
                L.circleMarker(p, {{ radius: 6, color: '#FFFFFF', weight: 2, fillColor: '#10B981', fillOpacity: 1 }}).addTo(capaCuidadores);
            }});
        }}
    </script>
</body>
</html>";

            MapaWebView.Source = new HtmlWebViewSource { Html = html };
            mapaHtmlCargado = true;
        }

        private async void ReposicionarMarcador()
        {
            if (!mapaHtmlCargado)
                return;

            var lat = latitudActual.ToString(CultureInfo.InvariantCulture);
            var lng = longitudActual.ToString(CultureInfo.InvariantCulture);

            try
            {
                await MapaWebView.EvaluateJavaScriptAsync($"actualizarUbicacion({lat}, {lng})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando marcador: {ex.Message}");
            }
        }

        private async Task CargarCuidadoresEnMapa()
        {
            cuidadoresEnMapa = await _apiService.ObtenerCuidadoresCercanosMapaAsync(latitudActual, longitudActual);
            ActualizarCuidadoresEnMapa();
        }

        private async void ActualizarCuidadoresEnMapa()
        {
            if (!mapaHtmlCargado)
                return;

            var puntos = cuidadoresEnMapa.Select(c => new[] { (double)c.Latitud, (double)c.Longitud }).ToList();
            var json = JsonSerializer.Serialize(puntos);

            try
            {
                await MapaWebView.EvaluateJavaScriptAsync($"actualizarCuidadores({json})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error actualizando cuidadores en el mapa: {ex.Message}");
            }
        }

        private bool serviciosCargadosAlMenosUnaVez;

        private async Task CargarServiciosCercanos()
        {
            // El spinner grande solo se muestra la primera vez; los refrescos automáticos
            // posteriores (polling) actualizan la lista en silencio, sin parpadeos.
            if (!serviciosCargadosAlMenosUnaVez)
            {
                ServiciosLoading.IsVisible = true;
                IniciarEsqueleto();
            }

            serviciosCercanos = await _apiService.ObtenerServiciosCercanosAsync(latitudActual, longitudActual);
            serviciosCargadosAlMenosUnaVez = true;

            this.AbortAnimation("Esqueleto");
            ServiciosLoading.IsVisible = false;

            RenderizarCategorias();
            AplicarFiltro();
        }

        private readonly List<(Border Chip, Label Texto, string? Valor)> chips = new();
        private string firmaCategorias = "";

        private void RenderizarCategorias()
        {
            // Si las categorías no cambiaron (refresco por polling), solo se repinta la selección.
            var firma = string.Join("|", serviciosCercanos.Select(s => s.Especialidad));
            if (firma == firmaCategorias && chips.Count > 0)
            {
                PintarChips(false);
                return;
            }
            firmaCategorias = firma;

            ListaCategorias.Clear();
            chips.Clear();

            ListaCategorias.Add(CrearChipCategoria(Localizador.T("todos"), null));
            foreach (var servicio in serviciosCercanos)
                ListaCategorias.Add(CrearChipCategoria(Localizador.D(servicio.Especialidad), servicio.Especialidad));

            PintarChips(false);

            // Entrada escalonada de los chips
            var i = 0;
            foreach (var (chip, _, _) in chips)
            {
                chip.Opacity = 0;
                chip.TranslationX = 16;
                var retraso = (uint)(i++ * 40);
                _ = Task.Delay((int)retraso).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
                {
                    chip.FadeTo(1, 220, Easing.CubicOut);
                    chip.TranslateTo(0, 0, 260, Easing.CubicOut);
                }));
            }
        }

        private View CrearChipCategoria(string texto, string? valor)
        {
            var label = new Label { Text = texto, FontSize = 13, FontFamily = "OpenSansSemibold" };
            var chip = new Border
            {
                StrokeThickness = 1.5,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                Padding = new Thickness(16, 9),
                Content = label
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                if (categoriaSeleccionada == valor) return;
                categoriaSeleccionada = valor;
                PintarChips(true);
                await chip.ScaleTo(0.94, 80, Easing.CubicOut);
                _ = chip.ScaleTo(1, 180, Easing.CubicOut);
                AplicarFiltro();
            };
            chip.GestureRecognizers.Add(tap);

            chips.Add((chip, label, valor));
            return chip;
        }

        private void PintarChips(bool animado)
        {
            var primario = (Color)Application.Current!.Resources["ColorPrimary"];
            var borde = (Color)Application.Current!.Resources["ColorBorder"];
            var texto = (Color)Application.Current!.Resources["ColorTextStrong"];

            foreach (var (chip, label, valor) in chips)
            {
                var activo = categoriaSeleccionada == valor;
                var fondoDestino = activo ? primario : Color.FromArgb("#EEF1F5");
                if (animado && chip.BackgroundColor != null && chip.BackgroundColor != fondoDestino)
                {
                    var desde = chip.BackgroundColor;
                    new Animation(t => chip.BackgroundColor = Mezclar(desde, fondoDestino, t), 0, 1)
                        .Commit(chip, "ColorChip", length: 180, easing: Easing.CubicOut);
                }
                else
                {
                    chip.BackgroundColor = fondoDestino;
                }
                chip.Stroke = Colors.Transparent;
                label.TextColor = activo ? Colors.White : texto;
            }
        }

        private static Color Mezclar(Color a, Color b, double t) => new(
            (float)(a.Red + (b.Red - a.Red) * t),
            (float)(a.Green + (b.Green - a.Green) * t),
            (float)(a.Blue + (b.Blue - a.Blue) * t),
            (float)(a.Alpha + (b.Alpha - a.Alpha) * t));

        private void OnBuscarTextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltro();
        }

        private void AplicarFiltro()
        {
            var texto = EntryBuscar.Text?.Trim() ?? "";

            var filtrados = serviciosCercanos
                .Where(s => categoriaSeleccionada == null || s.Especialidad == categoriaSeleccionada)
                .Where(s => string.IsNullOrEmpty(texto) || s.Especialidad.Contains(texto, StringComparison.OrdinalIgnoreCase))
                .ToList();

            RenderizarServicios(filtrados);
        }

        private string firmaServicios = "";

        private void RenderizarServicios(List<ServicioCercano> servicios)
        {
            var firma = string.Join("|", servicios.Select(s => $"{s.Especialidad}:{s.CuidadoresDisponibles}:{s.TarifaDesde}"));
            if (firma == firmaServicios && ListaServicios.Count == servicios.Count)
                return;
            firmaServicios = firma;

            ListaServicios.Clear();

            var sinServicios = servicios.Count == 0;
            if (sinServicios && !LblSinServicios.IsVisible)
            {
                LblSinServicios.Opacity = 0;
                LblSinServicios.IsVisible = true;
                _ = LblSinServicios.FadeTo(1, 250, Easing.CubicOut);
            }
            else
            {
                LblSinServicios.IsVisible = sinServicios;
            }

            var i = 0;
            foreach (var servicio in servicios)
            {
                var tarjeta = CrearTarjetaServicio(servicio);
                tarjeta.Opacity = 0;
                tarjeta.TranslationY = 14;
                ListaServicios.Add(tarjeta);

                var retraso = i++ * 55;
                _ = Task.Delay(retraso).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
                {
                    tarjeta.FadeTo(1, 260, Easing.CubicOut);
                    tarjeta.TranslateTo(0, 0, 320, Easing.CubicOut);
                }));
            }
        }

        private const int ColumnasServicios = 4;
        private const double EspacioServicios = 8;

        // Sugerencia estilo Uber: cuadro con el ícono grande al centro, insignia con los
        // disponibles y el nombre + precio centrados debajo. Plano (sin sombras que se recorten).
        private View CrearTarjetaServicio(ServicioCercano servicio)
        {
            var (icono, colorFondo, colorIcono) = ObtenerIconoServicio(servicio.Especialidad);
            var indice = ListaServicios.Count;
            var anchoDisponible = (BottomSheet.Width > 0 ? BottomSheet.Width : Width) - 40;
            var ancho = Math.Floor((anchoDisponible - EspacioServicios * (ColumnasServicios - 1)) / ColumnasServicios);
            var lado = Math.Min(ancho, 84);

            var cuadro = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = colorFondo,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
                WidthRequest = lado,
                HeightRequest = lado,
                HorizontalOptions = LayoutOptions.Center,
                Content = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(icono)!,
                    Fill = colorIcono,
                    Aspect = Stretch.Uniform,
                    WidthRequest = lado * 0.42,
                    HeightRequest = lado * 0.42,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            // Insignia con la cantidad de cuidadores disponibles (esquina superior derecha)
            var insignia = new Border
            {
                Stroke = Colors.White,
                StrokeThickness = 2,
                BackgroundColor = Color.FromArgb("#2E7D32"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 11 },
                HeightRequest = 22,
                MinimumWidthRequest = 22,
                Padding = new Thickness(6, 0),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, -6, (ancho - lado) / 2 - 6, 0),
                Content = new Label { Text = servicio.CuidadoresDisponibles.ToString(), FontSize = 11, FontFamily = "OpenSansSemibold", TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
            };
            SemanticProperties.SetDescription(insignia, Localizador.F("disponible_s_cerca_de_ti", servicio.CuidadoresDisponibles));

            var arriba = new Grid { Padding = new Thickness(0, 6, 0, 0) };
            arriba.Add(cuadro);
            arriba.Add(insignia);

            var tile = new VerticalStackLayout
            {
                Spacing = 2,
                WidthRequest = ancho,
                Margin = new Thickness(0, 0, (indice + 1) % ColumnasServicios == 0 ? 0 : EspacioServicios, 12),
                Children =
                {
                    arriba,
                    new Label { Text = Localizador.D(servicio.Especialidad), FontSize = 12, FontFamily = "OpenSansSemibold", TextColor = (Color)Application.Current!.Resources["ColorTextStrong"], HorizontalTextAlignment = TextAlignment.Center, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation, Margin = new Thickness(0, 6, 0, 0) },
                    new Label { Text = Localizador.F("desde_rd", servicio.TarifaDesde), FontSize = 11, FontFamily = "OpenSansRegular", TextColor = (Color)Application.Current!.Resources["ColorTextMuted"], HorizontalTextAlignment = TextAlignment.Center, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation }
                }
            };
            SemanticProperties.SetDescription(tile, Localizador.D(servicio.Especialidad));

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                // Respuesta al tocar: el cuadro se hunde y la insignia rebota.
                _ = insignia.ScaleTo(1.2, 120, Easing.CubicOut);
                await cuadro.ScaleTo(0.92, 100, Easing.CubicOut);
                _ = cuadro.ScaleTo(1, 200, Easing.CubicOut);
                _ = insignia.ScaleTo(1, 200, Easing.CubicOut);
                await AbrirServicio(servicio.Especialidad);
            };
            tile.GestureRecognizers.Add(tap);

            return tile;
        }

        internal static (string Icono, Color Fondo, Color Texto) ObtenerIconoServicio(string especialidad)
        {
            return especialidad switch
            {
                // Escoba
                "Limpieza del hogar" => ("M19.36 2.72L20.78 4.14L15.06 9.85C16.13 11.39 16.28 13.24 15.38 14.44L9.06 8.12C10.26 7.22 12.11 7.37 13.65 8.44L19.36 2.72ZM5.93 17.57C3.92 15.56 2.69 13.16 2.35 10.92L7.23 8.34L15.16 16.27L12.58 21.15C10.34 20.81 7.94 19.58 5.93 17.57Z",
                    Color.FromArgb("#EAF1FB"), Color.FromArgb("#1C4D96")),
                // Niño
                "Niñera / Cuidadora" => ("M12 2C13.66 2 15 3.34 15 5C15 6.66 13.66 8 12 8C10.34 8 9 6.66 9 5C9 3.34 10.34 2 12 2ZM16 9H8C7.45 9 7 9.45 7 10V15H9V22H15V15H17V10C17 9.45 16.55 9 16 9Z",
                    Color.FromArgb("#FCE7F3"), Color.FromArgb("#C2185B")),
                // Corazón
                "Cuidadora de adultos" => ("M12 21.35L10.55 20.03C5.4 15.36 2 12.28 2 8.5C2 5.42 4.42 3 7.5 3C9.24 3 10.91 3.81 12 5.09C13.09 3.81 14.76 3 16.5 3C19.58 3 22 5.42 22 8.5C22 12.28 18.6 15.36 13.45 20.04L12 21.35Z",
                    Color.FromArgb("#E3F4E8"), Color.FromArgb("#2E7D32")),
                // Casa
                _ => ("M10 20V14H14V20H19V12H22L12 3L2 12H5V20H10Z", Color.FromArgb("#EAF1FB"), Color.FromArgb("#1C4D96"))
            };
        }

        private async Task AbrirServicio(string especialidad)
        {
            try
            {
                var parametros = new Dictionary<string, object>
                {
                    { "Especialidad", especialidad },
                    { "Latitud", latitudActual },
                    { "Longitud", longitudActual }
                };
                await Shell.Current.GoToAsync("CuidadoresPorServicioPage", parametros);
            }
            catch (Exception ex)
            {
                await Alerta.MostrarAsync(Localizador.T("error_al_abrir_el_servicio"), ex.ToString(), Localizador.T("ok"));
            }
        }

        private async void OnPerfilTapped(object sender, EventArgs e)
        {
            await BtnPerfil.ScaleTo(0.9, 80, Easing.CubicOut);
            _ = BtnPerfil.ScaleTo(1, 160, Easing.CubicOut);
            await Shell.Current.GoToAsync("ClientePerfilPage");
        }

        private void OnTogglePanelTapped(object sender, EventArgs e)
        {
            AnimarPanel(!panelExpandido);
        }

        private double alturaAlIniciarArrastre;

        private void OnPanelPanUpdated(object sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    alturaAlIniciarArrastre = BottomSheet.Height > 0 ? BottomSheet.Height : AlturaPanelExpandido;
                    ContenidoExpandible.IsVisible = true;
                    _ = ManijaHandle.ScaleTo(1.35, 120, Easing.CubicOut);
                    break;

                case GestureStatus.Running:
                    var nuevaAltura = alturaAlIniciarArrastre - e.TotalY;
                    // Más allá de los límites el panel ofrece resistencia (no un tope seco).
                    if (nuevaAltura > AlturaPanelExpandido)
                        nuevaAltura = AlturaPanelExpandido + Math.Sqrt(nuevaAltura - AlturaPanelExpandido) * 3;
                    else if (nuevaAltura < AlturaPanelColapsado)
                        nuevaAltura = AlturaPanelColapsado - Math.Sqrt(AlturaPanelColapsado - nuevaAltura) * 3;
                    BottomSheet.HeightRequest = nuevaAltura;
                    ContenidoExpandible.Opacity = Math.Clamp((nuevaAltura - AlturaPanelColapsado) / (AlturaPanelExpandido - AlturaPanelColapsado), 0, 1);
                    break;

                case GestureStatus.Completed:
                case GestureStatus.Canceled:
                    var puntoMedio = (AlturaPanelExpandido + AlturaPanelColapsado) / 2;
                    AnimarPanel(BottomSheet.HeightRequest >= puntoMedio);
                    break;
            }
        }

        private void AnimarPanel(bool expandir)
        {
            panelExpandido = expandir;

            if (panelExpandido)
            {
                // El cliente puede tener servicios activos y seguir buscando otros a la vez:
                // el banner (si hay alguno activo) y el buscador conviven en el panel.
                ContenidoExpandible.IsVisible = true;
                BannerServicioActivo.IsVisible = hayServicioActivo;
            }

            var alturaActual = BottomSheet.Height > 0 ? BottomSheet.Height : AlturaPanelExpandido;
            var alturaDestino = panelExpandido ? AlturaPanelExpandido : AlturaPanelColapsado;

            // Curva tipo "drawer" de iOS: arranca rápido y frena suave.
            var animacion = new Animation(v => BottomSheet.HeightRequest = v, alturaActual, alturaDestino);
            animacion.Commit(this, "AnimacionPanel", 16, 340, CurvaDrawer, (v, c) =>
            {
                if (!panelExpandido)
                    ContenidoExpandible.IsVisible = false;
            });
            _ = ContenidoExpandible.FadeTo(panelExpandido ? 1 : 0, panelExpandido ? 260u : 140u, Easing.CubicOut);
            _ = ManijaHandle.ScaleTo(1, 160, Easing.CubicOut);
        }

        private async void OnNotificacionesTapped(object sender, EventArgs e)
        {
            await BtnNotificaciones.ScaleTo(0.9, 80, Easing.CubicOut);
            _ = BtnNotificaciones.ScaleTo(1, 160, Easing.CubicOut);
            await Navigation.PushModalAsync(new NotificacionesPage());
        }

        private async void OnCerrarSesionTapped(object sender, EventArgs e)
        {
            await BtnCerrarSesion.ScaleTo(0.9, 80, Easing.CubicOut);
            _ = BtnCerrarSesion.ScaleTo(1, 160, Easing.CubicOut);
            var confirmar = await Alerta.MostrarAsync(Localizador.T("cerrar_sesion"), Localizador.T("estas_seguro_de_que_deseas"), Localizador.T("si"), Localizador.T("cancelar"));
            if (!confirmar)
                return;

            Preferences.Default.Clear();
            await Shell.Current.GoToAsync("//MainPage");
        }
    
        // ================= Animaciones =================

        // Aproximación de cubic-bezier(0.32, 0.72, 0, 1) (curva del drawer de iOS).
        private static readonly Easing CurvaDrawer = new(t => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 3.2));
        private static readonly Easing EaseOutFuerte = new(t => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 4));
        private bool entradaHecha;

        /// <summary>Entrada: botones superiores caen escalonados, el panel sube y el saludo aparece.</summary>
        private async Task AnimarEntradaAsync()
        {
            var botones = new View[] { BtnPerfil, BtnNotificaciones, BtnCerrarSesion };
            if (entradaHecha)
            {
                BottomSheet.TranslationY = 0;
                BloqueSaludo.Opacity = 1;
                foreach (var b in botones) { b.Opacity = 1; b.TranslationY = 0; }
                return;
            }
            entradaHecha = true;

            try
            {
                // Esperar a que la página esté dibujada: sin handler las animaciones no corren.
                while (BottomSheet.Handler == null || BottomSheet.Height <= 0)
                    await Task.Delay(16);

                BottomSheet.TranslationY = BottomSheet.Height;
                BloqueSaludo.Opacity = 0;
                BloqueSaludo.TranslationY = 10;

                for (var i = 0; i < botones.Length; i++)
                {
                    var b = botones[i];
                    _ = Task.Delay(i * 70).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
                    {
                        b.FadeTo(1, 260, Easing.CubicOut);
                        b.TranslateTo(0, 0, 360, EaseOutFuerte);
                    }));
                }

                await Task.Delay(120);
                await BottomSheet.TranslateTo(0, 0, 560, CurvaDrawer);

                _ = BloqueSaludo.FadeTo(1, 280, Easing.CubicOut);
                await BloqueSaludo.TranslateTo(0, 0, 320, Easing.CubicOut);

                // La mano saluda y la campana timbra una vez.
                await SaludarAsync();
                await TimbrarCampanaAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClienteDashboardPage] Error en animación de entrada: {ex}");
            }
            finally
            {
                // Pase lo que pase, todo termina visible y en su lugar.
                BottomSheet.TranslationY = 0;
                BloqueSaludo.Opacity = 1;
                BloqueSaludo.TranslationY = 0;
                foreach (var b in botones) { b.Opacity = 1; b.TranslationY = 0; }
            }
        }

        private async Task SaludarAsync()
        {
            for (var i = 0; i < 2; i++)
            {
                await ManoSaludo.RotateTo(18, 120, Easing.CubicOut);
                await ManoSaludo.RotateTo(-10, 120, Easing.CubicInOut);
            }
            await ManoSaludo.RotateTo(0, 140, Easing.CubicOut);
        }

        private async Task TimbrarCampanaAsync()
        {
            foreach (var angulo in new double[] { 14, -12, 8, -6, 0 })
                await IconoCampana.RotateTo(angulo, 70, Easing.CubicInOut);
        }

        /// <summary>Animaciones de ambiente en bucle (muy sutiles), solo mientras la página está visible.</summary>
        private void IniciarBuclesAmbiente()
        {
            // Pulso "en vivo" del banner de servicio activo
            new Animation(t =>
            {
                PulsoServicio.Scale = 1 + 0.45 * t;
                PulsoServicio.Opacity = 0.8 * (1 - t);
            }, 0, 1).Commit(this, "PulsoServicio", length: 1600, easing: Easing.CubicOut, repeat: () => true);

            // La flecha del banner invita a tocar
            new Animation(t => FlechaBanner.TranslationX = 3 * Math.Sin(t * Math.PI * 2), 0, 1)
                .Commit(this, "FlechaBanner", length: 1800, easing: Easing.Linear, repeat: () => true);

            // Onda del logo en la tarjeta de carga
            new Animation(t =>
            {
                OndaCarga.Scale = 1 + 0.35 * t;
                OndaCarga.Opacity = 1 - t;
                LogoCarga.Scale = 1 + 0.05 * Math.Sin(t * Math.PI);
            }, 0, 1).Commit(this, "OndaCarga", length: 1300, easing: Easing.CubicOut, repeat: () => true);
        }

        private void DetenerBuclesAmbiente()
        {
            foreach (var n in new[] { "PulsoServicio", "FlechaBanner", "OndaCarga", "Esqueleto" })
                this.AbortAnimation(n);
        }

        /// <summary>Esqueleto de carga que "respira" mientras llegan los servicios.</summary>
        private void IniciarEsqueleto()
        {
            new Animation(t => ServiciosLoading.Opacity = 0.55 + 0.45 * Math.Sin(t * Math.PI), 0, 1)
                .Commit(this, "Esqueleto", length: 1100, easing: Easing.Linear, repeat: () => true);
        }

        private void MostrarBanner(bool mostrar)
        {
            if (mostrar == BannerServicioActivo.IsVisible)
                return;

            if (mostrar)
            {
                BannerServicioActivo.Opacity = 0;
                BannerServicioActivo.Scale = 0.96;
                BannerServicioActivo.IsVisible = true;
                _ = BannerServicioActivo.FadeTo(1, 260, Easing.CubicOut);
                _ = BannerServicioActivo.ScaleTo(1, 320, EaseOutFuerte);
            }
            else
            {
                BannerServicioActivo.IsVisible = false;
            }
        }

        // ---- Buscador: se resalta al enfocar ----
        private void OnBuscarFocused(object? sender, FocusEventArgs e)
        {
            BordeBuscar.Stroke = (Color)Application.Current!.Resources["ColorPrimary"];
            BordeBuscar.BackgroundColor = Colors.White;
            IconoBuscar.Fill = (Color)Application.Current!.Resources["ColorPrimary"];
            _ = IconoBuscar.ScaleTo(1.15, 120, Easing.CubicOut).ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(() => IconoBuscar.ScaleTo(1, 160, Easing.CubicOut)));
        }

        private void OnBuscarUnfocused(object? sender, FocusEventArgs e)
        {
            BordeBuscar.Stroke = Colors.Transparent;
            BordeBuscar.BackgroundColor = Color.FromArgb("#EEF1F5");
            IconoBuscar.Fill = (Color)Application.Current!.Resources["ColorTextStrong"];
        }
}
}
