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

            var nombre = Preferences.Default.Get("UserNombre", "");
            var primerNombre = string.IsNullOrWhiteSpace(nombre) ? "" : nombre.Split(' ').First();
            LblSaludo.Text = string.IsNullOrWhiteSpace(primerNombre) ? "Hola" : $"Hola, {primerNombre}";

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

            OverlayCarga.Opacity = 1;
            OverlayCarga.IsVisible = true;

            // El GPS puede tardar varios segundos: se resuelve en paralelo y, cuando
            // llega, solo se reposiciona el marcador y se recentra el mapa (sin recargarlo).
            _ = ActualizarUbicacionRealAsync();

            await VerificarServicioActivo();
            await CargarServiciosCercanos();
            await CargarCuidadoresEnMapa();

            yaCargado = true;

            await OverlayCarga.FadeTo(0, 250);
            OverlayCarga.IsVisible = false;
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

            var trabajoActivo = await _apiService.ObtenerTrabajoActivoPorClienteAsync(clienteId);
            hayServicioActivo = trabajoActivo != null;

            BannerServicioActivo.IsVisible = hayServicioActivo;
            ContenidoExpandible.IsVisible = !hayServicioActivo;

            if (trabajoActivo != null)
            {
                var estadoTexto = trabajoActivo.Estado switch
                {
                    1 => "Esperando respuesta del cuidador",
                    2 => "Aceptado, en espera de la fecha programada",
                    3 => "En progreso",
                    4 => "Completado, ¡califica a tu cuidador!",
                    _ => "En curso"
                };
                LblBannerServicioActivo.Text = $"{trabajoActivo.TipoServicio} · {estadoTexto}";
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
        L.tileLayer('https://{{s}}.basemaps.cartocdn.com/rastertiles/voyager_nolabels/{{z}}/{{x}}/{{y}}{{r}}.png', {{ maxZoom: 20, subdomains: 'abcd' }}).addTo(map);
        var marker = L.circleMarker([{lat}, {lng}], {{ radius: 8, color: '#FFFFFF', weight: 3, fillColor: '#5A31F4', fillOpacity: 1 }}).addTo(map);
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

        private async Task CargarServiciosCercanos()
        {
            ServiciosLoading.IsVisible = true;
            ServiciosLoading.IsRunning = true;

            serviciosCercanos = await _apiService.ObtenerServiciosCercanosAsync(latitudActual, longitudActual);

            ServiciosLoading.IsRunning = false;
            ServiciosLoading.IsVisible = false;

            RenderizarCategorias();
            AplicarFiltro();
        }

        private void RenderizarCategorias()
        {
            ListaCategorias.Clear();

            var chipTodos = CrearChipCategoria("Todos", null);
            ListaCategorias.Add(chipTodos);

            foreach (var servicio in serviciosCercanos)
            {
                ListaCategorias.Add(CrearChipCategoria(servicio.Especialidad, servicio.Especialidad));
            }
        }

        private View CrearChipCategoria(string texto, string? valor)
        {
            var esSeleccionado = categoriaSeleccionada == valor;

            var chip = new Border
            {
                Stroke = esSeleccionado ? Colors.Transparent : Color.FromArgb("#E5E7EB"),
                StrokeThickness = 1,
                BackgroundColor = esSeleccionado ? Color.FromArgb("#5A31F4") : Colors.White,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                Padding = new Thickness(16, 8),
                Content = new Label
                {
                    Text = texto,
                    FontSize = 13,
                    FontFamily = "OpenSansSemibold",
                    TextColor = esSeleccionado ? Colors.White : Color.FromArgb("#374151")
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                categoriaSeleccionada = valor;
                RenderizarCategorias();
                AplicarFiltro();
            };
            chip.GestureRecognizers.Add(tap);

            return chip;
        }

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

        private void RenderizarServicios(List<ServicioCercano> servicios)
        {
            ListaServicios.Clear();
            LblSinServicios.IsVisible = servicios.Count == 0;

            foreach (var servicio in servicios)
            {
                ListaServicios.Add(CrearTarjetaServicio(servicio));
            }
        }

        private View CrearTarjetaServicio(ServicioCercano servicio)
        {
            var (icono, colorFondo, colorIcono) = ObtenerIconoServicio(servicio.Especialidad);

            var iconoView = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = colorFondo,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                WidthRequest = 50,
                HeightRequest = 50,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 15, 0),
                Content = new Label { Text = icono, FontSize = 22, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, TextColor = colorIcono }
            };

            var textos = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = servicio.Especialidad, FontSize = 15, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#111827") },
                    new Label { Text = $"{servicio.CuidadoresDisponibles} disponible(s) cerca de ti", FontSize = 12, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#6B7280") }
                }
            };

            var precio = new Label
            {
                Text = $"Desde\nRD${servicio.TarifaDesde:N0}",
                FontSize = 12,
                FontFamily = "OpenSansSemibold",
                TextColor = Color.FromArgb("#5A31F4"),
                HorizontalTextAlignment = TextAlignment.End,
                VerticalOptions = LayoutOptions.Center
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            grid.Add(iconoView, 0, 0);
            grid.Add(textos, 1, 0);
            grid.Add(precio, 2, 0);

            var card = new Border
            {
                Stroke = Color.FromArgb("#F3F4F6"),
                StrokeThickness = 1,
                BackgroundColor = Color.FromArgb("#FAFAFA"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(14),
                Content = grid
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) => await AbrirServicio(servicio.Especialidad);
            card.GestureRecognizers.Add(tap);

            return card;
        }

        private static (string Icono, Color Fondo, Color Texto) ObtenerIconoServicio(string especialidad)
        {
            return especialidad switch
            {
                "Limpieza del hogar" => ("🧹", Color.FromArgb("#DBEAFE"), Color.FromArgb("#3B82F6")),
                "Niñera / Cuidadora" => ("👶", Color.FromArgb("#FCE7F3"), Color.FromArgb("#DB2777")),
                "Cuidadora de adultos" => ("🧑‍⚕️", Color.FromArgb("#D1FAE5"), Color.FromArgb("#10B981")),
                _ => ("🏠", Color.FromArgb("#F3E8FF"), Color.FromArgb("#A855F7"))
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
                await DisplayAlert("Error al abrir el servicio", ex.ToString(), "OK");
            }
        }

        private async void OnPerfilTapped(object sender, EventArgs e)
        {
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
                    break;

                case GestureStatus.Running:
                    var nuevaAltura = alturaAlIniciarArrastre - e.TotalY;
                    BottomSheet.HeightRequest = Math.Clamp(nuevaAltura, AlturaPanelColapsado, AlturaPanelExpandido);
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
                // Si hay un servicio activo, el panel expandido muestra el banner, no el buscador.
                ContenidoExpandible.IsVisible = !hayServicioActivo;
                BannerServicioActivo.IsVisible = hayServicioActivo;
            }

            var alturaActual = BottomSheet.Height > 0 ? BottomSheet.Height : AlturaPanelExpandido;
            var alturaDestino = panelExpandido ? AlturaPanelExpandido : AlturaPanelColapsado;

            var animacion = new Animation(v => BottomSheet.HeightRequest = v, alturaActual, alturaDestino);
            animacion.Commit(this, "AnimacionPanel", 16, 220, Easing.CubicInOut, (v, c) =>
            {
                if (!panelExpandido)
                    ContenidoExpandible.IsVisible = false;
            });
        }

        private async void OnNotificacionesTapped(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new NotificacionesPage());
        }

        private async void OnCerrarSesionTapped(object sender, EventArgs e)
        {
            var confirmar = await DisplayAlert("Cerrar sesión", "¿Estás seguro de que deseas cerrar sesión?", "Sí", "Cancelar");
            if (!confirmar)
                return;

            Preferences.Default.Clear();
            await Shell.Current.GoToAsync("//MainPage");
        }
    }
}
