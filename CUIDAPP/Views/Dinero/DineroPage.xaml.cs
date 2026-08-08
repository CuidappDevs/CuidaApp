using Microsoft.Maui.Controls;

namespace CUIDAPP.Views.Dinero
{
    public partial class DineroPage : ContentPage
    {
        public DineroPage()
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

        private async void OnTrabajosTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("TrabajosPage");
        }

        private async void OnPerfilTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorPerfilPage");
        }
    }
}
