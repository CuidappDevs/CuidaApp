using CUIDAPP.Services;

namespace CUIDAPP.Views.Soporte
{
    public partial class MisReportesPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public MisReportesPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarTicketsAsync();
        }

        private async Task CargarTicketsAsync()
        {
            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            ListaTickets.IsVisible = false;
            PanelVacio.IsVisible = false;

            var usuarioId = Preferences.Default.Get("UserId", 0);
            var tickets = usuarioId != 0 ? await _apiService.ObtenerMisTicketsAsync(usuarioId) : new List<Models.Ticket.Ticket>();

            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;

            if (tickets.Count == 0)
            {
                PanelVacio.IsVisible = true;
                return;
            }

            ListaTickets.ItemsSource = tickets;
            ListaTickets.IsVisible = true;
        }

        private async void OnTicketTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not int ticketId)
                return;

            var parametros = new Dictionary<string, object> { { "TicketId", ticketId } };
            await Shell.Current.GoToAsync("DetalleReportePage", parametros);
        }

        private async void OnNuevoReporteTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("NuevoReportePage");
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
