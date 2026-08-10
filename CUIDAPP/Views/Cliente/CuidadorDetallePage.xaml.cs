using CUIDAPP.Models.Busqueda;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class CuidadorDetallePage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _apiService = new ApiService();
        private CuidadorCercano? cuidador;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Cuidador", out var value) && value is CuidadorCercano c)
            {
                cuidador = c;
                Renderizar(c);
                _ = CargarRatingAsync(c.Id);
            }
        }

        private async Task CargarRatingAsync(int cuidadorId)
        {
            var promedio = await _apiService.ObtenerPromedioCalificacionAsync(cuidadorId);
            if (promedio == null || promedio.Total == 0)
                return;

            LblRating.Text = $"{promedio.Promedio:N1} ({promedio.Total})";
            ContenedorRating.IsVisible = true;
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
            if (cuidador == null)
                return;

            try
            {
                var parametros = new Dictionary<string, object> { { "Cuidador", cuidador } };
                await Shell.Current.GoToAsync("SolicitarServicioPage", parametros);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error al continuar", ex.ToString(), "OK");
            }
        }
    }
}
