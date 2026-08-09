using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Trabajos
{
    [QueryProperty(nameof(TrabajoParam), "Trabajo")]
    public partial class DetalleTrabajoPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private Trabajo? trabajo;

        public object? TrabajoParam
        {
            set
            {
                trabajo = value as Trabajo;
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

        private async void OnIniciarClicked(object sender, EventArgs e) => await CambiarEstado(3);

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
