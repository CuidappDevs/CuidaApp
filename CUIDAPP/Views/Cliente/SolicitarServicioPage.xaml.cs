using CUIDAPP.Models.Busqueda;
using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class SolicitarServicioPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private CuidadorCercano? cuidador;

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
        }

        private void Renderizar(CuidadorCercano c)
        {
            LblNombre.Text = c.NombreCompleto;
            LblEspecialidad.Text = c.Especialidad;
            LblTarifa.Text = $"RD$ {c.TarifaHora:N0} / hr";

            if (!string.IsNullOrWhiteSpace(c.FotoUrl))
                ImgFoto.Source = $"{ApiService.ServerOrigin}{c.FotoUrl}";
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnEnviarClicked(object sender, EventArgs e)
        {
            if (cuidador == null)
                return;

            if (string.IsNullOrWhiteSpace(EntryDireccion.Text))
            {
                await DisplayAlert("Error", "Ingresa la dirección donde se realizará el servicio.", "OK");
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
                var request = new CrearTrabajoRequest
                {
                    ClienteId = clienteId,
                    CuidadorId = cuidador.Id,
                    TipoServicio = cuidador.Especialidad,
                    Fecha = PickerFecha.Date ?? DateTime.Today,
                    HoraInicio = PickerHoraInicio.Time ?? TimeSpan.Zero,
                    HoraFin = PickerHoraFin.Time ?? TimeSpan.Zero,
                    Direccion = EntryDireccion.Text.Trim(),
                    Tarifa = cuidador.TarifaHora
                };

                var success = await _apiService.CrearTrabajoAsync(request);

                if (success)
                {
                    await DisplayAlert("Solicitud enviada", $"Le avisamos a {cuidador.NombreCompleto}. Te notificaremos cuando responda.", "OK");
                    await Shell.Current.GoToAsync("../../..");
                }
                else
                {
                    await DisplayAlert("Error", "No se pudo enviar la solicitud. Intenta de nuevo.", "OK");
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
