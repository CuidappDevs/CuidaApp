using Microsoft.Maui.Controls;

namespace CUIDAPP.Views.Trabajos
{
    public partial class DetalleTrabajoPage : ContentPage
    {
        public DetalleTrabajoPage()
        {
            InitializeComponent();
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
