using CUIDAPP.Models.Busqueda;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    [QueryProperty(nameof(CuidadorParam), "Cuidador")]
    public partial class CuidadorDetallePage : ContentPage
    {
        public object? CuidadorParam
        {
            set
            {
                if (value is CuidadorCercano cuidador)
                    Renderizar(cuidador);
            }
        }

        public CuidadorDetallePage()
        {
            InitializeComponent();
        }

        private void Renderizar(CuidadorCercano cuidador)
        {
            LblNombre.Text = cuidador.NombreCompleto;
            LblEspecialidad.Text = cuidador.Especialidad;
            LblDistancia.Text = $"A {cuidador.DistanciaKm:N1} km de tu ubicación";
            LblTarifa.Text = $"RD$ {cuidador.TarifaHora:N0} / hr";
            LblBio.Text = string.IsNullOrWhiteSpace(cuidador.Bio) ? "Este cuidador no ha agregado una biografía." : cuidador.Bio;

            if (!string.IsNullOrWhiteSpace(cuidador.FotoUrl))
                ImgFoto.Source = $"{ApiService.ServerOrigin}{cuidador.FotoUrl}";
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnSolicitarTapped(object sender, EventArgs e)
        {
            await DisplayAlert("Próximamente", "La solicitud de servicio estará disponible en la siguiente actualización.", "OK");
        }
    }
}
