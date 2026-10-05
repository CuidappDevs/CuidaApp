using CUIDAPP.Localization;
using CUIDAPP.Models.Cuidador;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Perfil
{
    public partial class CuidadorPerfilPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public CuidadorPerfilPage()
        {
            InitializeComponent();
        }

        private PerfilCuidador? _perfil;
        private List<DocumentoEstado> _documentos = new();

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Localizador.Instancia.IdiomaCambiado -= RepintarTextos;
            Localizador.Instancia.IdiomaCambiado += RepintarTextos;
            await CargarPerfil();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            Localizador.Instancia.IdiomaCambiado -= RepintarTextos;
        }

        // Textos armados por código: se vuelven a pintar al instante cuando cambia el idioma.
        private void RepintarTextos()
        {
            AplicarPerfil();
            RenderizarVerificaciones(_documentos);
        }

        private void AplicarPerfil()
        {
            var perfil = _perfil;
            if (perfil == null)
                return;

            LblNombre.Text = perfil.NombreCompleto;
            LblNombreCarnet.Text = perfil.NombreCompleto;
            LblEspecialidad.Text = Localizador.D(perfil.Especialidad);
            LblEspecialidadBanner.Text = Localizador.D(perfil.Especialidad).ToUpper(Localizador.Cultura);
            LblEspecialidadTarifa.Text = Localizador.D(perfil.Especialidad);
            LblBio.Text = string.IsNullOrWhiteSpace(perfil.Bio) ? Localizador.T("aun_no_has_agregado_una") : perfil.Bio;
            LblTarifa.Text = Localizador.F("rd_hr", perfil.TarifaHora);
            LblEstadoCuenta.Text = perfil.EstadoAprobacion switch
            {
                2 => Localizador.T("aprobado"),
                3 => Localizador.T("rechazado"),
                _ => Localizador.T("pendiente")
            };
        }

        private async Task CargarPerfil()
        {
            var cuidadorId = Preferences.Default.Get("UserId", 0);
            if (cuidadorId == 0)
                return;

            var perfilTask = _apiService.ObtenerPerfilCuidadorAsync(cuidadorId);
            var trabajosTask = _apiService.ObtenerTrabajosAsync(cuidadorId);
            var estadoTask = _apiService.ObtenerEstadoVerificacionAsync(cuidadorId);

            await Task.WhenAll(perfilTask, trabajosTask, estadoTask);

            var perfil = perfilTask.Result;
            if (perfil != null)
            {
                _perfil = perfil;
                AplicarPerfil();

                if (!string.IsNullOrWhiteSpace(perfil.FotoUrl))
                {
                    var urlCompleta = $"{ApiService.ServerOrigin}{perfil.FotoUrl}";
                    ImgFotoPerfilGrande.Source = urlCompleta;
                    ImgFotoCarnet.Source = urlCompleta;
                }
            }

            LblTrabajosCompletados.Text = trabajosTask.Result.Count(t => t.Estado == 4).ToString();

            _documentos = estadoTask.Result?.Documentos ?? new List<DocumentoEstado>();
            RenderizarVerificaciones(_documentos);
        }

        private void RenderizarVerificaciones(List<DocumentoEstado> documentos)
        {
            ListaVerificaciones.Clear();

            if (documentos.Count == 0)
            {
                ListaVerificaciones.Add(new Label
                {
                    Text = Localizador.T("no_hay_documentos_registrados"),
                    FontSize = 13,
                    FontFamily = "OpenSansRegular",
                    TextColor = Color.FromArgb("#9CA3AF")
                });
                return;
            }

            foreach (var doc in documentos)
            {
                ListaVerificaciones.Add(CrearFilaVerificacion(doc));
            }
        }

        private static View CrearFilaVerificacion(DocumentoEstado doc)
        {
            var (colorFondo, colorTexto, titulo, subtitulo) = doc.Estado switch
            {
                2 => (Color.FromArgb("#D1FAE5"), Color.FromArgb("#10B981"), Localizador.T("verificado"), Localizador.T("documento_aprobado_por_administracion")),
                3 => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#DC2626"), Localizador.T("rechazado"), doc.ObservacionesAdmin ?? Localizador.T("debes_volver_a_subir_este")),
                _ => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#D97706"), Localizador.T("en_revision"), Localizador.T("aun_no_ha_sido_revisado"))
            };

            var nombreDocumento = doc.TipoDocumento switch
            {
                "Cedula" => Localizador.T("doc_cedula"),
                "CartaAntecedentes" => Localizador.T("doc_carta"),
                _ => Localizador.D(doc.TipoDocumento)
            };

            var icono = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = colorFondo,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                WidthRequest = 32,
                HeightRequest = 32,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Content = new Label
                {
                    Text = titulo == Localizador.T("verificado") ? "✓" : titulo == Localizador.T("rechazado") ? "✕" : "…",
                    TextColor = colorTexto,
                    FontFamily = "OpenSansSemibold",
                    FontSize = 14,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            var textos = new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = $"{nombreDocumento} — {titulo}", FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Color.FromArgb("#111827") },
                    new Label { Text = subtitulo, FontSize = 12, FontFamily = "OpenSansRegular", TextColor = Color.FromArgb("#6B7280") }
                }
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
            grid.Add(icono, 0, 0);
            grid.Add(textos, 1, 0);
            return grid;
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnInicioTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnTrabajosTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("TrabajosPage");
        }

        private async void OnDineroTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("DineroPage");
        }

        private async void OnMisCalificacionesTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("MisCalificacionesPage");
        }

        private async void OnSoporteTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("MisReportesPage");
        }
    }
}
