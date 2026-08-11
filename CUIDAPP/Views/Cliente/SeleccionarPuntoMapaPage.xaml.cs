using System.Globalization;
using System.Web;
using CUIDAPP.Models.Cliente;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class SeleccionarPuntoMapaPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private UbicacionCliente? ubicacionExistente;
        private double latSeleccionada;
        private double lngSeleccionada;
        private bool mapaListo;
        private bool soloSeleccionar;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Ubicacion", out var value) && value is UbicacionCliente u)
                ubicacionExistente = u;

            if (query.TryGetValue("SoloSeleccionar", out var soloValue) && soloValue is bool b)
                soloSeleccionar = b;
        }

        public SeleccionarPuntoMapaPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (soloSeleccionar)
            {
                LabelNombre.IsVisible = EntryNombre.IsVisible = false;
                LabelPredeterminada.IsVisible = SwitchPredeterminada.IsVisible = false;
                BtnGuardar.Text = "Usar esta ubicación";
            }

            if (ubicacionExistente != null)
            {
                latSeleccionada = (double)ubicacionExistente.Latitud;
                lngSeleccionada = (double)ubicacionExistente.Longitud;
                EntryNombre.Text = ubicacionExistente.Nombre;
                EntryDireccion.Text = ubicacionExistente.Direccion;
                SwitchPredeterminada.IsToggled = ubicacionExistente.EsPredeterminada;
            }
            else
            {
                var ubicacionActual = await LocationService.ObtenerUbicacionActualAsync();
                latSeleccionada = ubicacionActual?.Latitude ?? LocationService.LatitudPorDefecto;
                lngSeleccionada = ubicacionActual?.Longitude ?? LocationService.LongitudPorDefecto;
            }

            CargarMapa();
        }

        private void CargarMapa()
        {
            var lat = latSeleccionada.ToString(CultureInfo.InvariantCulture);
            var lng = lngSeleccionada.ToString(CultureInfo.InvariantCulture);

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
        var map = L.map('map', {{ zoomControl: true }}).setView([{lat}, {lng}], 16);
        L.tileLayer('https://{{s}}.basemaps.cartocdn.com/rastertiles/voyager/{{z}}/{{x}}/{{y}}{{r}}.png', {{ maxZoom: 20, subdomains: 'abcd' }}).addTo(map);
        var marker = L.marker([{lat}, {lng}], {{ draggable: true }}).addTo(map);

        function avisarPunto(lat, lng) {{
            window.location.href = 'app://pick?lat=' + lat + '&lng=' + lng;
        }}

        map.on('click', function(e) {{
            marker.setLatLng(e.latlng);
            avisarPunto(e.latlng.lat, e.latlng.lng);
        }});

        marker.on('dragend', function(e) {{
            var pos = marker.getLatLng();
            avisarPunto(pos.lat, pos.lng);
        }});
    </script>
</body>
</html>";

            MapaWebView.Source = new HtmlWebViewSource { Html = html };
            mapaListo = true;
        }

        private void OnMapaNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (!e.Url.StartsWith("app://pick"))
                return;

            e.Cancel = true;

            var query = HttpUtility.ParseQueryString(new Uri(e.Url).Query);
            if (double.TryParse(query["lat"], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(query["lng"], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
            {
                latSeleccionada = lat;
                lngSeleccionada = lng;
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            if (soloSeleccionar)
                SeleccionUbicacionBroker.PendientePunto?.TrySetResult(null);

            await Shell.Current.GoToAsync("..");
        }

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            if (!mapaListo)
                return;

            if (string.IsNullOrWhiteSpace(EntryDireccion.Text))
            {
                await DisplayAlert("Falta la dirección", "Escribe la dirección de referencia.", "OK");
                return;
            }

            if (soloSeleccionar)
            {
                SeleccionUbicacionBroker.PendientePunto?.TrySetResult(new UbicacionCliente
                {
                    Nombre = "Casa",
                    Direccion = EntryDireccion.Text.Trim(),
                    Latitud = (decimal)latSeleccionada,
                    Longitud = (decimal)lngSeleccionada,
                    EsPredeterminada = true
                });
                await Shell.Current.GoToAsync("..");
                return;
            }

            if (string.IsNullOrWhiteSpace(EntryNombre.Text))
            {
                await DisplayAlert("Falta el nombre", "Ponle un nombre a esta ubicación (ej. Casa, Trabajo).", "OK");
                return;
            }

            var clienteId = Preferences.Default.Get("UserId", 0);
            if (clienteId == 0)
            {
                await DisplayAlert("Error", "Tu sesión expiró. Vuelve a iniciar sesión.", "OK");
                return;
            }

            BtnGuardar.IsEnabled = false;
            BtnGuardar.Text = "Guardando...";

            try
            {
                var nombre = EntryNombre.Text.Trim();
                var direccion = EntryDireccion.Text.Trim();
                var lat = (decimal)latSeleccionada;
                var lng = (decimal)lngSeleccionada;
                var predeterminada = SwitchPredeterminada.IsToggled;

                bool success;
                if (ubicacionExistente != null)
                {
                    success = await _apiService.ActualizarUbicacionClienteAsync(ubicacionExistente.Id, clienteId, nombre, direccion, lat, lng, predeterminada);
                }
                else
                {
                    (success, _) = await _apiService.CrearUbicacionClienteAsync(clienteId, nombre, direccion, lat, lng, predeterminada);
                }

                if (success)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Error", "No se pudo guardar la ubicación. Intenta de nuevo.", "OK");
                }
            }
            finally
            {
                BtnGuardar.IsEnabled = true;
                BtnGuardar.Text = "Guardar ubicación";
            }
        }
    }
}
