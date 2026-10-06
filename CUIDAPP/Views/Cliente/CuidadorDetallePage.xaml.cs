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
            // Valor grande en la tarjeta de estadísticas; el total va como etiqueta.
            LblRatingValor.Text = promedio.Promedio.ToString("N1");
            LblRatingEtiqueta.Text = $"{Localizador.T("stat_calificacion")} ({promedio.Total})";
            ContenedorRating.IsVisible = false;
            LblRatingValor.Scale = 0.8;
            _ = LblRatingValor.ScaleTo(1, 260, Easing.CubicOut);
        }

        public CuidadorDetallePage()
        {
            InitializeComponent();

            // Borde a borde: la barra superior empieza debajo de la barra de estado y el contenido
            // termina con espacio para la barra de gestos.
            var alto = BarraEstado.Alto();
            BarraSuperior.Margin = new Thickness(0, alto, 0, 0);
            Encabezado.HeightRequest += alto;
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();


            foreach (var v in BloquesEntrada())
            {
                v.Opacity = 0;
                v.TranslationY = 16;
            }
            BloqueFoto.Scale = 0.9;
        }

        private VisualElement[] BloquesEntrada() =>
            new VisualElement[] { BloqueFoto, BloqueNombre, TarjetaStats, TarjetaBio, TarjetaVerificado, PieAccion };

        private bool entradaHecha;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            if (entradaHecha) return;
            entradaHecha = true;
            try
            {
                _ = BloqueFoto.ScaleTo(1, 420, Easing.CubicOut);
                foreach (var v in BloquesEntrada())
                {
                    _ = v.FadeTo(1, 300, Easing.CubicOut);
                    _ = v.TranslateTo(0, 0, 360, Easing.CubicOut);
                    await Task.Delay(60);
                }
            }
            finally
            {
                await Task.Delay(400);
                foreach (var v in BloquesEntrada()) { v.Opacity = 1; v.TranslationY = 0; }
                BloqueFoto.Scale = 1;

            }
        }


        private void OnBotonPresionado(object? sender, EventArgs e) => _ = BtnSolicitar.ScaleTo(0.97, 100, Easing.CubicOut);

        private void OnBotonSoltado(object? sender, EventArgs e) => _ = BtnSolicitar.ScaleTo(1, 160, Easing.CubicOut);

        private void Renderizar(CuidadorCercano cuidador)
        {
            LblNombre.Text = cuidador.NombreCompleto;
            LblEspecialidad.Text = Localizador.D(cuidador.Especialidad);
            LblDistanciaValor.Text = $"{cuidador.DistanciaKm:0.0} km";
            SemanticProperties.SetDescription(LblDistanciaValor, Localizador.F("a_km_de_tu_ubicacion", cuidador.DistanciaKm));
            LblTarifa.Text = Localizador.F("rd_monto", cuidador.TarifaHora);
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
                await Alerta.MostrarAsync(Localizador.T("error_al_continuar"), ex.ToString(), Localizador.T("ok"));
            }
        }
    }
}
