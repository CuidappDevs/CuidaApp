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

        private bool entradaHecha;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Blanca();
            if (!entradaHecha)
            {
                entradaHecha = true;
                FotoPerfil.Scale = 0.85;
                _ = FotoPerfil.ScaleTo(1, 420, Easing.CubicOut);
                foreach (var v in new VisualElement[] { TarjetaPerfil, TarjetaStats, Contenido })
                {
                    v.Opacity = 0;
                    v.TranslationY = 16;
                    _ = v.FadeTo(1, 320, Easing.CubicOut);
                    _ = v.TranslateTo(0, 0, 380, Easing.CubicOut);
                }
            }
            Localizador.Instancia.IdiomaCambiado -= RepintarTextos;
            Localizador.Instancia.IdiomaCambiado += RepintarTextos;
            await CargarPerfil();
            await CargarContactoAsync();
        }

        private async Task CargarContactoAsync()
        {
            var cuidadorId = Preferences.Default.Get("UserId", 0);
            var contacto = await _apiService.ObtenerContactoEmergenciaCuidadorAsync(cuidadorId);
            if (contacto == null)
                return;

            EntryContactoNombre.Text = contacto.Nombre;
            EntryContactoTelefono.Text = contacto.Telefono;
            EntryContactoEmail.Text = contacto.Email;
            ActualizarResumenContacto();
        }

        // La fila muestra a quién se avisaría, o un aviso si todavía no hay contacto.
        private void ActualizarResumenContacto()
        {
            var nombre = EntryContactoNombre.Text?.Trim();
            var contacto = !string.IsNullOrWhiteSpace(EntryContactoTelefono.Text) ? EntryContactoTelefono.Text.Trim() : EntryContactoEmail.Text?.Trim();
            bool configurado = !string.IsNullOrWhiteSpace(nombre);

            LblContactoResumen.Text = configurado
                ? string.IsNullOrWhiteSpace(contacto) ? nombre : $"{nombre} · {contacto}"
                : Localizador.T("contacto_sin_configurar");
            LblContactoResumen.TextColor = configurado ? (Color)Application.Current!.Resources["ColorTextMuted"] : Color.FromArgb("#B26A00");
        }

        private async void OnContactoTapped(object? sender, TappedEventArgs e)
        {
            await FilaContacto.ScaleTo(0.97, 80, Easing.CubicOut);
            await FilaContacto.ScaleTo(1, 120, Easing.CubicOut);

            VentanaContacto.IsVisible = true;
            TarjetaContacto.Scale = 0.94;
            await Task.WhenAll(
                VeloContacto.FadeTo(0.45, 200, Easing.CubicOut),
                TarjetaContacto.FadeTo(1, 200, Easing.CubicOut),
                TarjetaContacto.ScaleTo(1, 220, Easing.CubicOut));
        }

        private void OnCerrarContactoTapped(object? sender, TappedEventArgs e) => _ = CerrarVentanaContactoAsync();

        private async Task CerrarVentanaContactoAsync()
        {
            EntryContactoNombre.Unfocus();
            EntryContactoTelefono.Unfocus();
            EntryContactoEmail.Unfocus();
            await Task.WhenAll(
                VeloContacto.FadeTo(0, 160, Easing.CubicOut),
                TarjetaContacto.FadeTo(0, 160, Easing.CubicOut),
                TarjetaContacto.ScaleTo(0.96, 160, Easing.CubicOut));
            VentanaContacto.IsVisible = false;
        }

        private async void OnGuardarContactoClicked(object sender, EventArgs e)
        {
            var cuidadorId = Preferences.Default.Get("UserId", 0);
            if (cuidadorId == 0)
                return;

            BtnGuardarContacto.IsEnabled = false;
            BtnGuardarContacto.Text = Localizador.T("guardando");

            var (success, error) = await _apiService.GuardarContactoEmergenciaCuidadorAsync(cuidadorId, new Models.Cuidador.ContactoEmergencia
            {
                Nombre = EntryContactoNombre.Text?.Trim(),
                Telefono = EntryContactoTelefono.Text?.Trim(),
                Email = EntryContactoEmail.Text?.Trim()
            });

            BtnGuardarContacto.IsEnabled = true;
            BtnGuardarContacto.Text = Localizador.T("guardar_contacto");

            if (success)
            {
                ActualizarResumenContacto();
                await CerrarVentanaContactoAsync();
                await Alerta.MostrarAsync(Localizador.T("contacto_guardado"), Localizador.T("contacto_guardado_texto"), Localizador.T("ok"));
            }
            else
                await Alerta.MostrarAsync(Localizador.T("error"), error ?? Localizador.T("contacto_error"), Localizador.T("ok"));
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
            LblTarifa.Text = $"{Localizador.F("rd_monto", perfil.TarifaHora)} {Localizador.T("sufijo_por_hora")}";
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
                    TextColor = (Color)Application.Current!.Resources["ColorTextMuted"]
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
                2 => (Color.FromArgb("#E3F4E8"), Color.FromArgb("#2E7D32"), Localizador.T("verificado"), Localizador.T("documento_aprobado_por_administracion")),
                3 => (Color.FromArgb("#FDECEC"), Color.FromArgb("#C62828"), Localizador.T("rechazado"), doc.ObservacionesAdmin ?? Localizador.T("debes_volver_a_subir_este")),
                _ => (Color.FromArgb("#FFF4DC"), Color.FromArgb("#E09A1A"), Localizador.T("en_revision"), Localizador.T("aun_no_ha_sido_revisado"))
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
                    new Label { Text = $"{nombreDocumento} — {titulo}", FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = (Color)Application.Current!.Resources["ColorTextStrong"] },
                    new Label { Text = subtitulo, FontSize = 12, FontFamily = "OpenSansRegular", TextColor = (Color)Application.Current!.Resources["ColorTextMuted"] }
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
