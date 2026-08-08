using Microsoft.Maui.Controls;

namespace CUIDAPP.Views.Dashboard
{
    public partial class CuidadorDashboardPage : ContentPage
    {
        public CuidadorDashboardPage()
        {
            InitializeComponent();
        }

        private async void OnPerfilTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorPerfilPage");
        }

        private async void OnTrabajosTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("TrabajosPage");
        }

        private async void OnDineroTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("DineroPage");
        }
    }
}
