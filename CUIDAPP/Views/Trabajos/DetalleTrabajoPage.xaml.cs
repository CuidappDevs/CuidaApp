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

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarMapaRutaAsync();
            await ActualizarBotonCalificarAsync();
        }

        private async Task ActualizarBotonCalificarAsync()
        {
            if (trabajo == null || trabajo.Estado != 4)
                return;

            var cuidadorId = Preferences.Default.Get("UserId", 0);
            if (cuidadorId == 0)
                return;

            var yaCalifico = await _apiService.ExisteCalificacionAsync(trabajo.Id, cuidadorId);
            BtnCalificarCliente.IsVisible = !yaCalifico;
        }

        private async void OnCalificarClienteClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var parametros = new Dictionary<string, object>
            {
                { "TrabajoId", trabajo.Id },
                { "CalificadoId", trabajo.ClienteId },
                { "CalificadoNombre", trabajo.ClienteNombre }
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

            LblTipoServicio.Text = trabajo.TipoServicio;
            LblFecha.Text = trabajo.Fecha.ToString("dddd, d 'de' MMMM");
            LblHora.Text = $"{FormatearHora(trabajo.HoraInicio)} - {FormatearHora(trabajo.HoraFin)}";
            LblDireccion.Text = string.IsNullOrWhiteSpace(trabajo.Direccion) ? "Sin dirección" : trabajo.Direccion;
            LblPago.Text = $"RD${trabajo.Tarifa:N2}";

            var (colorFondo, colorTexto, texto) = trabajo.Estado switch
            {
                1 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), "Pendiente"),
                2 => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#1E40AF"), "Aceptado"),
                3 => (Color.FromArgb("#EDE9FE"), Color.FromArgb("#5B21B6"), "En progreso"),
                4 => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#166534"), "Completado"),
                5 => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), "Cancelado"),
                6 => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#991B1B"), "Rechazado"),
                _ => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), "Desconocido")
            };
            BadgeEstado.BackgroundColor = colorFondo;
            LblEstado.TextColor = colorTexto;
            LblEstado.Text = texto;

            PanelPendiente.IsVisible = trabajo.Estado == 1;
            BtnIniciar.IsVisible = trabajo.Estado == 2;
            BtnCompletar.IsVisible = trabajo.Estado == 3;
            BtnCancelar.IsVisible = trabajo.Estado == 2 || trabajo.Estado == 3;
        }

        private static string FormatearHora(TimeSpan hora)
        {
            return DateTime.Today.Add(hora).ToString("h:mm tt");
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnAceptarClicked(object sender, EventArgs e) => await CambiarEstado(2);

        private async void OnRechazarClicked(object sender, EventArgs e) => await CambiarEstado(6);

        private const double DistanciaMaximaKm = 0.15; // 150 metros

        private async void OnIniciarClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            if (trabajo.Fecha.Date != DateTime.Today)
            {
                var mensaje = trabajo.Fecha.Date > DateTime.Today
                    ? $"Este servicio está programado para el {trabajo.Fecha:d 'de' MMMM}. Todavía no puedes iniciarlo."
                    : $"Este servicio estaba programado para el {trabajo.Fecha:d 'de' MMMM} y ya pasó la fecha.";
                await DisplayAlert("No es la fecha del servicio", mensaje, "OK");
                return;
            }

            if (trabajo.Latitud != null && trabajo.Longitud != null)
            {
                var ubicacionActual = await LocationService.ObtenerUbicacionActualAsync();
                if (ubicacionActual == null)
                {
                    await DisplayAlert("Ubicación no disponible", "No pudimos verificar tu ubicación. Activa el GPS e intenta de nuevo.", "OK");
                    return;
                }

                var ubicacionServicio = new Location((double)trabajo.Latitud, (double)trabajo.Longitud);
                var distanciaKm = Location.CalculateDistance(ubicacionActual, ubicacionServicio, DistanceUnits.Kilometers);

                if (distanciaKm > DistanciaMaximaKm)
                {
                    await DisplayAlert("Estás muy lejos", $"Debes estar en la dirección del servicio para iniciar el trabajo. Estás a {distanciaKm * 1000:N0} m de distancia.", "OK");
                    return;
                }
            }

            var parametros = new Dictionary<string, object> { { "Trabajo", trabajo } };
            await Shell.Current.GoToAsync("IniciarTrabajoPage", parametros);
        }

        private async void OnCompletarClicked(object sender, EventArgs e) => await CambiarEstado(4);

        private async void OnCancelarClicked(object sender, EventArgs e)
        {
            if (trabajo == null)
                return;

            var parametros = new Dictionary<string, object> { { "Trabajo", trabajo } };
            await Shell.Current.GoToAsync("CancelarServicioPage", parametros);
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

                marcadorCuidadorJs = $"L.circleMarker([{origLat}, {origLon}], {{ radius: 8, color: '#FFFFFF', weight: 3, fillColor: '#5A31F4', fillOpacity: 1 }}).addTo(map);";

                var coordsJson = JsonSerializer.Serialize((puntosRuta ?? new List<(double Lat, double Lon)>())
                    .Select(p => new[] { p.Lat, p.Lon }));
                lineaRutaJs = $"var rutaCoords = {coordsJson}; L.polyline(rutaCoords, {{ color: '#5A31F4', weight: 5 }}).addTo(map);";

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
        L.tileLayer('https://{{s}}.basemaps.cartocdn.com/rastertiles/voyager_nolabels/{{z}}/{{x}}/{{y}}{{r}}.png', {{ maxZoom: 20, subdomains: 'abcd' }}).addTo(map);

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
                var url = $"https://router.project-osrm.org/route/v1/driving/{origenLon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{origenLat.ToString(System.Globalization.CultureInfo.InvariantCulture)};{destinoLon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{destinoLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}?overview=full&geometries=geojson";

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

            var success = await _apiService.ActualizarEstadoTrabajoAsync(trabajo.Id, nuevoEstado);

            if (success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert("Error", "No se pudo actualizar el trabajo. Intenta de nuevo.", "OK");
            }
        }
    }
}
