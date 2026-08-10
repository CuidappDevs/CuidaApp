using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Trabajos
{
    public partial class DetalleTrabajoPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private Trabajo? trabajo;

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
