using Microsoft.Maui.Controls;

namespace CUIDAPP.Views.Perfil
{
    public partial class CuidadorPerfilPage : ContentPage
    {
        public CuidadorPerfilPage()
        {
            InitializeComponent();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnInicioTapped(object sender, EventArgs e)
        {
            // Regresar al dashboard al tocar Inicio
            await Shell.Current.GoToAsync("..");
        }

        private async void OnTrabajosTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("TrabajosPage");
        }
    }
}
