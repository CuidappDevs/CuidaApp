using CUIDAPP.Models.Busqueda;
using CUIDAPP.Models.Cliente;
using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class SolicitarServicioPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private CuidadorCercano? cuidador;
        private UbicacionCliente? ubicacionElegida;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Cuidador", out var value) && value is CuidadorCercano c)
            {
                cuidador = c;
                Renderizar(c);
            }
        }

        public SolicitarServicioPage()
        {
            InitializeComponent();
            PickerFecha.MinimumDate = DateTime.Today;
            PickerFecha.Date = DateTime.Today;
            PickerHoraInicio.Time = new TimeSpan(9, 0, 0);
            PickerHoraFin.Time = new TimeSpan(11, 0, 0);

            PickerHoraInicio.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(TimePicker.Time)) ActualizarTotal(); };
            PickerHoraFin.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(TimePicker.Time)) ActualizarTotal(); };
        }

        private void Renderizar(CuidadorCercano c)
        {
            LblNombre.Text = c.NombreCompleto;
            LblEspecialidad.Text = c.Especialidad;
            LblTarifa.Text = $"RD$ {c.TarifaHora:N0} / hr";

            if (!string.IsNullOrWhiteSpace(c.FotoUrl))
                ImgFoto.Source = $"{ApiService.ServerOrigin}{c.FotoUrl}";

            ActualizarTotal();
        }

        private void ActualizarTotal()
        {
            if (cuidador == null)
                return;

            var horaInicio = PickerHoraInicio.Time ?? TimeSpan.Zero;
            var horaFin = PickerHoraFin.Time ?? TimeSpan.Zero;
            var horas = (decimal)(horaFin - horaInicio).TotalHours;

            LblTotal.Text = horas > 0
                ? $"RD${(cuidador.TarifaHora * horas):N2}"
                : "--";
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnElegirUbicacionTapped(object sender, EventArgs e)
        {
            var tcs = new TaskCompletionSource<UbicacionCliente?>();
            SeleccionUbicacionBroker.Pendiente = tcs;

            await Shell.Current.GoToAsync("MisUbicacionesPage", new Dictionary<string, object> { { "ModoSeleccion", true } });

            var seleccionada = await tcs.Task;
            SeleccionUbicacionBroker.Pendiente = null;

            if (seleccionada == null)
                return;

            ubicacionElegida = seleccionada;
            LblUbicacionNombre.Text = seleccionada.Nombre;
            LblUbicacionDireccion.Text = seleccionada.Direccion;
        }

        private async void OnEnviarClicked(object sender, EventArgs e)
        {
            if (cuidador == null)
                return;

            if (ubicacionElegida == null)
            {
                await DisplayAlert("Error", "Selecciona a dónde debe ir el cuidador.", "OK");
                return;
            }

            if (PickerHoraFin.Time <= PickerHoraInicio.Time)
            {
                await DisplayAlert("Error", "La hora de fin debe ser posterior a la hora de inicio.", "OK");
                return;
            }

            var clienteId = Preferences.Default.Get("UserId", 0);
            if (clienteId == 0)
            {
                await DisplayAlert("Error", "Tu sesión expiró. Vuelve a iniciar sesión.", "OK");
                return;
            }

            BtnEnviar.IsEnabled = false;
            BtnEnviar.Text = "Enviando...";

            try
            {
                var trabajoActivo = await _apiService.ObtenerTrabajoActivoPorClienteAsync(clienteId);
                if (trabajoActivo != null)
                {
                    await DisplayAlert("Ya tienes un servicio activo", "No puedes solicitar otro servicio mientras tengas uno pendiente o en curso.", "OK");
                    await Shell.Current.GoToAsync("../../..");
                    return;
                }

                var horaInicio = PickerHoraInicio.Time ?? TimeSpan.Zero;
                var horaFin = PickerHoraFin.Time ?? TimeSpan.Zero;
                var horas = (decimal)(horaFin - horaInicio).TotalHours;
                var tarifaTotal = cuidador.TarifaHora * horas;

                var request = new CrearTrabajoRequest
                {
                    ClienteId = clienteId,
                    CuidadorId = cuidador.Id,
                    TipoServicio = cuidador.Especialidad,
                    Fecha = PickerFecha.Date ?? DateTime.Today,
                    HoraInicio = horaInicio,
                    HoraFin = horaFin,
                    Direccion = ubicacionElegida.Direccion,
                    Tarifa = tarifaTotal,
                    Latitud = ubicacionElegida.Latitud,
                    Longitud = ubicacionElegida.Longitud
                };

                var (success, error) = await _apiService.CrearTrabajoAsync(request);

                if (success)
                {
                    await DisplayAlert("Solicitud enviada", $"Le avisamos a {cuidador.NombreCompleto}. Te notificaremos cuando responda.", "OK");
                    await Shell.Current.GoToAsync("../../..");
                }
                else
                {
                    await DisplayAlert("Error", $"No se pudo enviar la solicitud.\n\n{error}", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error inesperado", ex.ToString(), "OK");
            }
            finally
            {
                BtnEnviar.IsEnabled = true;
                BtnEnviar.Text = "Enviar solicitud";
            }
        }
    }
}
