using Microsoft.Maui.Controls;

namespace CUIDAPP.Views.Trabajos
{
    public partial class TrabajosPage : ContentPage
    {
        public TrabajosPage()
        {
            InitializeComponent();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnInicioTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorDashboardPage");
        }

        private async void OnPerfilTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorPerfilPage");
        }

        private async void OnJobTapped(object sender, EventArgs e)
        {
            // Navegar al detalle del trabajo
            await Shell.Current.GoToAsync("DetalleTrabajoPage");
        }
    }
}
