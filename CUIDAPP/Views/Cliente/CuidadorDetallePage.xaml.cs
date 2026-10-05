using CUIDAPP.Localization;
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
            LblEspecialidad.Text = Localizador.D(cuidador.Especialidad);
            LblDistancia.Text = Localizador.F("a_km_de_tu_ubicacion", cuidador.DistanciaKm);
            LblTarifa.Text = Localizador.F("rd_hr", cuidador.TarifaHora);
            LblBio.Text = string.IsNullOrWhiteSpace(cuidador.Bio) ? Localizador.T("este_cuidador_no_ha_agregado") : cuidador.Bio;

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
                await DisplayAlert(Localizador.T("error_al_continuar"), ex.ToString(), Localizador.T("ok"));
            }
        }
    }
}
