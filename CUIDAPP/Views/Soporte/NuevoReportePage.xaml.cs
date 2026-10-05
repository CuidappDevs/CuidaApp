using CUIDAPP.Localization;
using CUIDAPP.Models.Ticket;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Soporte
{
    public partial class NuevoReportePage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        // El servidor guarda la categoría en español; al usuario se la mostramos traducida.
        private static readonly string[] CategoriasValor = { "Cuenta", "Pago", "Servicio", "Chat", "Otro" };

        public NuevoReportePage()
        {
            InitializeComponent();
            PickerCategoria.ItemsSource = CategoriasValor.Select(Localizador.D).ToList();
        }

        private async void OnEnviarClicked(object sender, EventArgs e)
        {
            if (PickerCategoria.SelectedIndex < 0)
            {
                await DisplayAlert(Localizador.T("falta_informacion"), Localizador.T("selecciona_una_categoria_2"), Localizador.T("ok"));
                return;
            }

            if (string.IsNullOrWhiteSpace(EntryAsunto.Text) || string.IsNullOrWhiteSpace(EditorDescripcion.Text))
            {
                await DisplayAlert(Localizador.T("falta_informacion"), Localizador.T("completa_el_asunto_y_la"), Localizador.T("ok"));
                return;
            }

            var categoria = CategoriasValor[PickerCategoria.SelectedIndex];
            var usuarioId = Preferences.Default.Get("UserId", 0);
            if (usuarioId == 0)
                return;

            BtnEnviar.IsEnabled = false;
            BtnEnviar.Text = Localizador.T("enviando");

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
                await DisplayAlert(Localizador.T("reporte_enviado"), Localizador.T("nuestro_equipo_lo_revisara_pronto"), Localizador.T("ok"));
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlert(Localizador.T("error"), Localizador.T("no_se_pudo_enviar_el"), Localizador.T("ok"));
                BtnEnviar.Text = Localizador.T("enviar_reporte");
                BtnEnviar.IsEnabled = true;
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
