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

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CargarPerfil();
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
                LblNombre.Text = perfil.NombreCompleto;
                LblNombreCarnet.Text = perfil.NombreCompleto;
                LblEspecialidad.Text = perfil.Especialidad ?? "";
                LblEspecialidadBanner.Text = (perfil.Especialidad ?? "").ToUpperInvariant();
                LblEspecialidadTarifa.Text = perfil.Especialidad ?? "";
                LblBio.Text = string.IsNullOrWhiteSpace(perfil.Bio) ? "Aún no has agregado una biografía." : perfil.Bio;
                LblTarifa.Text = $"RD$ {perfil.TarifaHora:N0} / hr";
                LblEstadoCuenta.Text = perfil.EstadoAprobacion switch
                {
                    2 => "Aprobado",
                    3 => "Rechazado",
                    _ => "Pendiente"
                };

                if (!string.IsNullOrWhiteSpace(perfil.FotoUrl))
                {
                    var urlCompleta = $"{ApiService.ServerOrigin}{perfil.FotoUrl}";
                    ImgFotoPerfilGrande.Source = urlCompleta;
                    ImgFotoCarnet.Source = urlCompleta;
                }
            }

            LblTrabajosCompletados.Text = trabajosTask.Result.Count(t => t.Estado == 4).ToString();

            RenderizarVerificaciones(estadoTask.Result?.Documentos ?? new List<DocumentoEstado>());
        }

        private void RenderizarVerificaciones(List<DocumentoEstado> documentos)
        {
            ListaVerificaciones.Clear();

            if (documentos.Count == 0)
            {
                ListaVerificaciones.Add(new Label
                {
                    Text = "No hay documentos registrados.",
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
                2 => (Color.FromArgb("#D1FAE5"), Color.FromArgb("#10B981"), "Verificado", "Documento aprobado por administración"),
                3 => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#DC2626"), "Rechazado", doc.ObservacionesAdmin ?? "Debes volver a subir este documento"),
                _ => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#D97706"), "En revisión", "Aún no ha sido revisado por administración")
            };

            var nombreDocumento = doc.TipoDocumento switch
            {
                "Cedula" => "Cédula de Identidad",
                "CartaAntecedentes" => "Carta de Antecedentes Penales",
                _ => doc.TipoDocumento
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
                    Text = titulo == "Verificado" ? "✓" : titulo == "Rechazado" ? "✕" : "…",
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
    }
}
