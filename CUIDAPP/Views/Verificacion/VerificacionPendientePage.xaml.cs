using CUIDAPP.Localization;
using CUIDAPP.Models.Cuidador;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Verificacion
{
    public partial class VerificacionPendientePage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        // Todos los documentos aprobados (sin aprobación formal de la cuenta): el botón pasa a "Ir a mi panel".
        private bool puedeIrAlPanel;

        public VerificacionPendientePage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            new Animation(t =>
            {
                AnilloEstado.Scale = 1 + 0.08 * Math.Sin(t * Math.PI * 2);
                AnilloEstado.Opacity = 0.7 + 0.3 * Math.Sin(t * Math.PI * 2);
            }, 0, 1).Commit(this, "AnilloEstado", length: 2600, easing: Easing.Linear, repeat: () => true);
            await CargarEstado(mostrarErrorSiFalla: false);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            this.AbortAnimation("AnilloEstado");
        }

        private async void OnActualizarTapped(object sender, EventArgs e)
        {
            if (puedeIrAlPanel)
            {
                Preferences.Default.Set("EstadoAprobacion", 2);
                await MostrarExitoYContinuar();
                return;
            }
            await CargarEstado(mostrarErrorSiFalla: true);
        }

        private async void OnCerrarSesionTapped(object sender, EventArgs e)
        {
            Recordatorios.CancelarTodos();
            Preferences.Default.Clear();
            await Shell.Current.GoToAsync("//MainPage");
        }

        private async Task CargarEstado(bool mostrarErrorSiFalla)
        {
            var userId = Preferences.Default.Get("UserId", 0);
            if (userId == 0)
            {
                await Shell.Current.GoToAsync("//MainPage");
                return;
            }

            BtnActualizar.IsEnabled = false;
            BtnActualizar.Text = Localizador.T("consultando");

            var estado = await _apiService.ObtenerEstadoVerificacionAsync(userId);

            BtnActualizar.IsEnabled = true;
            BtnActualizar.Text = Localizador.T("actualizar_estado");

            if (estado == null)
            {
                if (mostrarErrorSiFalla)
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_consultar_el"), Localizador.T("ok"));
                return;
            }

            // Si ya fue aprobado, mostrar la felicitación y pasar al dashboard.
            if (estado.EstadoAprobacion == 2)
            {
                Preferences.Default.Set("EstadoAprobacion", 2);
                await MostrarExitoYContinuar();
                return;
            }

            RenderizarEstado(estado);
        }

        // Modal con el motivo del rechazo y la opción de subir otro archivo.
        private async Task VerDocumentoRechazadoAsync(DocumentoEstado doc, string nombreDocumento)
        {
            var motivo = string.IsNullOrWhiteSpace(doc.ObservacionesAdmin)
                ? Localizador.T("rechazo_sin_motivo")
                : Localizador.F("motivo", doc.ObservacionesAdmin);

            var subir = await Alerta.MostrarAsync(nombreDocumento, motivo,
                Localizador.T("subir_de_nuevo"), Localizador.T("cerrar"), TipoAlerta.Error);
            if (subir)
                await ReemplazarDocumentoAsync(doc);
        }

        private async Task ReemplazarDocumentoAsync(DocumentoEstado doc)
        {
            FileResult? archivo;
            try
            {
                archivo = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = Localizador.T("subir_otro_documento") });
            }
            catch (Exception ex)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.F("no_se_pudo_seleccionar_el", ex.Message), Localizador.T("ok"));
                return;
            }
            if (archivo == null) return;

            BtnActualizar.IsEnabled = false;
            BtnActualizar.Text = Localizador.T("subiendo");

            // Misma carpeta del usuario en el servidor que el archivo anterior (/uploads/usuarios/{carpeta}/...).
            var partes = doc.UrlArchivo.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            var i = partes.IndexOf("usuarios");
            var carpeta = i >= 0 && i + 2 < partes.Count ? partes[i + 1] : Guid.NewGuid().ToString("N");

            var url = await _apiService.UploadFileAsync(archivo.FullPath, carpeta);
            var ok = !string.IsNullOrEmpty(url) &&
                     await _apiService.ReemplazarDocumentoAsync(doc.Id, Preferences.Default.Get("UserId", 0), url!);

            if (!ok)
            {
                BtnActualizar.IsEnabled = true;
                BtnActualizar.Text = Localizador.T(puedeIrAlPanel ? "ir_a_mi_panel" : "actualizar_estado");
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("no_se_pudo_reemplazar_documento"), Localizador.T("ok"));
                return;
            }

            await Alerta.MostrarAsync(Localizador.T("documento_enviado_titulo"), Localizador.T("documento_enviado_desc"), Localizador.T("ok"));
            await CargarEstado(mostrarErrorSiFalla: true);
        }

        private async Task MostrarExitoYContinuar()
        {
            OverlayExito.IsVisible = true;
            await OverlayExito.FadeTo(1, 250);

            CirculoExito.Scale = 0.5;
            await CirculoExito.ScaleTo(1.1, 300, Easing.SpringOut);
            await CirculoExito.ScaleTo(1.0, 120);

            await Task.Delay(1400);

            await Shell.Current.GoToAsync("CuidadorDashboardPage");
        }

        private void RenderizarEstado(EstadoVerificacion estado)
        {
            bool rechazado = estado.EstadoAprobacion == 3;
            puedeIrAlPanel = estado.PuedeTrabajar;
            BtnActualizar.Text = Localizador.T(puedeIrAlPanel ? "ir_a_mi_panel" : "actualizar_estado");

            if (puedeIrAlPanel)
            {
                LblTituloEstado.Text = Localizador.T("documentos_aprobados_titulo");
                LblDescripcionEstado.Text = Localizador.T("documentos_aprobados_desc");
                IconEstadoGeneral.Fill = Color.FromArgb("#2E7D32");
                AnilloEstado.Fill = Color.FromArgb("#332E7D32");
            }
            else
            {
                LblTituloEstado.Text = rechazado ? Localizador.T("documentos_rechazados") : Localizador.T("documentos_en_revision");
                LblDescripcionEstado.Text = rechazado
                    ? Localizador.T("uno_o_mas_documentos_fueron")
                    : Localizador.T("estamos_verificando_tus_documentos_te");

                IconEstadoGeneral.Fill = rechazado ? (Color)Application.Current!.Resources["ColorDanger"] : (Color)Application.Current!.Resources["ColorPrimary"];
                AnilloEstado.Fill = rechazado ? Color.FromArgb("#33C62828") : Color.FromArgb("#331C4D96");
            }

            ListaDocumentos.Clear();

            if (estado.Documentos.Count == 0)
            {
                LblSinDocumentos.IsVisible = true;
                return;
            }

            LblSinDocumentos.IsVisible = false;

            foreach (var doc in estado.Documentos)
            {
                ListaDocumentos.Add(CrearTarjetaDocumento(doc));
            }
        }

        private View CrearTarjetaDocumento(DocumentoEstado doc)
        {
            var (colorFondo, colorTexto, textoEstado) = doc.Estado switch
            {
                2 => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#166534"), Localizador.T("aprobado")),
                3 => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#991B1B"), Localizador.T("rechazado")),
                _ => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), Localizador.T("pendiente"))
            };

            var nombreDocumento = doc.TipoDocumento switch
            {
                "Cedula" => Localizador.T("doc_cedula"),
                "CartaAntecedentes" => Localizador.T("doc_carta"),
                _ => Localizador.D(doc.TipoDocumento)
            };

            var badge = new Border
            {
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                BackgroundColor = colorFondo,
                Padding = new Thickness(12, 6),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = textoEstado,
                    TextColor = colorTexto,
                    FontFamily = "OpenSansSemibold",
                    FontSize = 12,
                    LineHeight = 1,
                    VerticalOptions = LayoutOptions.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            };

            var contenido = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = nombreDocumento, FontFamily = "OpenSansSemibold", FontSize = 15, TextColor = (Color)Application.Current!.Resources["ColorTextStrong"] },
                    new Label { Text = Localizador.F("subido_el", doc.FechaSubida), FontFamily = "OpenSansRegular", FontSize = 12, TextColor = (Color)Application.Current!.Resources["ColorTextMuted"] }
                }
            };

            if (doc.Estado == 3)
            {
                contenido.Children.Add(new Label
                {
                    Text = Localizador.T("toca_para_ver_detalles"),
                    FontFamily = "OpenSansSemibold",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#991B1B")
                });
            }

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            contenido.VerticalOptions = LayoutOptions.Center;
            grid.Add(contenido, 0, 0);
            grid.Add(badge, 1, 0);

            var tarjeta = new Border
            {
                Stroke = doc.Estado == 3 ? Color.FromArgb("#FECACA") : Colors.Transparent,
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                BackgroundColor = Colors.White,
                Padding = new Thickness(15),
                Content = grid
            };

            if (doc.Estado == 3)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) =>
                {
                    await tarjeta.ScaleTo(0.97, 80, Easing.CubicOut);
                    await tarjeta.ScaleTo(1, 120, Easing.CubicOut);
                    await VerDocumentoRechazadoAsync(doc, nombreDocumento);
                };
                tarjeta.GestureRecognizers.Add(tap);
            }

            return tarjeta;
        }
    }
}
