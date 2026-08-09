namespace CUIDAPP.Views.Cliente
{
    public partial class NotificacionesPage : ContentPage
    {
        public NotificacionesPage()
        {
            InitializeComponent();
        }

        private async void OnCerrarTapped(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}
