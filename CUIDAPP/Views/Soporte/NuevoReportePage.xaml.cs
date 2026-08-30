using CUIDAPP.Models.Ticket;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Soporte
{
    public partial class NuevoReportePage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public NuevoReportePage()
        {
            InitializeComponent();
        }

        private async void OnEnviarClicked(object sender, EventArgs e)
        {
            if (PickerCategoria.SelectedItem is not string categoria)
            {
                await DisplayAlert("Falta información", "Selecciona una categoría.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(EntryAsunto.Text) || string.IsNullOrWhiteSpace(EditorDescripcion.Text))
            {
                await DisplayAlert("Falta información", "Completa el asunto y la descripción.", "OK");
                return;
            }

            var usuarioId = Preferences.Default.Get("UserId", 0);
            if (usuarioId == 0)
                return;

            BtnEnviar.IsEnabled = false;
            BtnEnviar.Text = "Enviando...";

            var request = new CrearTicketRequest
            {
                UsuarioId = usuarioId,
                Categoria = categoria,
                Asunto = EntryAsunto.Text.Trim(),
                Descripcion = EditorDescripcion.Text.Trim()
            };

            var ticketId = await _apiService.CrearTicketAsync(request);

            if (ticketId != null)
            {
                await DisplayAlert("Reporte enviado", "Nuestro equipo lo revisará pronto. Puedes seguir su estado en Mis reportes.", "OK");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert("Error", "No se pudo enviar el reporte. Intenta de nuevo.", "OK");
                BtnEnviar.Text = "Enviar reporte";
                BtnEnviar.IsEnabled = true;
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
