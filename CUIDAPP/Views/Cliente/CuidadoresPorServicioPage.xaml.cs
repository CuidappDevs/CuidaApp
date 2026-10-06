using CUIDAPP.Localization;
using CUIDAPP.Models.Busqueda;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    [QueryProperty(nameof(Especialidad), "Especialidad")]
    [QueryProperty(nameof(Latitud), "Latitud")]
    [QueryProperty(nameof(Longitud), "Longitud")]
    public partial class CuidadoresPorServicioPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();
        private bool estaVisible;
        private bool primeraCarga = true;

        public string Especialidad { get; set; } = string.Empty;
        public double Latitud { get; set; }
        public double Longitud { get; set; }

        public CuidadoresPorServicioPage()
        {
            InitializeComponent();

            // Borde a borde: el encabezado empieza debajo de la barra de estado.
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);

            // Estado inicial de la entrada (antes del primer frame).
            BtnBack.Opacity = 0;
            BtnBack.TranslationX = -12;
            BloqueServicio.Opacity = 0;
            BloqueServicio.TranslationX = 12;
        }

        private bool entradaHecha;

        private async Task AnimarEntradaAsync()
        {
            if (entradaHecha) return;
            entradaHecha = true;
            try
            {
                _ = BtnBack.FadeTo(1, 250, Easing.CubicOut);
                _ = BtnBack.TranslateTo(0, 0, 300, Easing.CubicOut);
                await Task.Delay(80);
                _ = BloqueServicio.FadeTo(1, 300, Easing.CubicOut);
                await BloqueServicio.TranslateTo(0, 0, 360, Easing.CubicOut);
            }
            finally
            {
                BtnBack.Opacity = 1; BtnBack.TranslationX = 0;
                BloqueServicio.Opacity = 1; BloqueServicio.TranslationX = 0;
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            LblTituloServicio.Text = Localizador.D(Especialidad);
            var (icono, _, color) = ClienteDashboardPage.ObtenerIconoServicio(Especialidad);
            IconoServicio.Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(icono)!;
            IconoServicio.Fill = color;
            estaVisible = true;
            BarraEstado.Azul();
            _ = AnimarEntradaAsync();

            // Punto "en vivo" junto al conteo
            new Animation(t => PuntoEnVivo.Opacity = 0.4 + 0.6 * Math.Abs(Math.Cos(t * Math.PI)), 0, 1)
                .Commit(this, "PuntoEnVivo", length: 1600, easing: Easing.Linear, repeat: () => estaVisible);

            await CargarCuidadores();

            // Mientras el cliente esté viendo esta lista, la refrescamos periódicamente
            // para reflejar en "tiempo real" cuando un cuidador se conecta/desconecta.
            Dispatcher.StartTimer(TimeSpan.FromSeconds(8), () =>
            {
                if (!estaVisible)
                    return false;

                _ = CargarCuidadores();
                return true;
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            estaVisible = false;
            this.AbortAnimation("PuntoEnVivo");
            this.AbortAnimation("Esqueleto");
        }

        private string firmaLista = "";

        private async Task CargarCuidadores()
        {
            // Solo mostramos el esqueleto en la primera carga; los refrescos
            // automáticos posteriores actualizan la lista en silencio, sin parpadeos.
            if (primeraCarga)
            {
                LoadingIndicator.IsVisible = true;
                new Animation(t => LoadingIndicator.Opacity = 0.55 + 0.45 * Math.Sin(t * Math.PI), 0, 1)
                    .Commit(this, "Esqueleto", length: 1100, easing: Easing.Linear, repeat: () => LoadingIndicator.IsVisible);
                primeraCarga = false;
            }

            var cuidadores = await _apiService.ObtenerCuidadoresPorServicioAsync(Especialidad, Latitud, Longitud);

            this.AbortAnimation("Esqueleto");
            LoadingIndicator.IsVisible = false;
            LblConteo.Text = Localizador.F("disponible_s_cerca_de_ti", cuidadores.Count);

            // Si nada cambió desde el último refresco, no se toca la lista (sin parpadeo).
            var firma = string.Join("|", cuidadores.Select(c => $"{c.Id}:{c.TarifaHora}:{c.DistanciaKm:0.0}"));
            if (firma == firmaLista && ListaCuidadores.Count == cuidadores.Count)
                return;
            firmaLista = firma;

            ListaCuidadores.Clear();

            var vacio = cuidadores.Count == 0;
            if (vacio && !LblSinCuidadores.IsVisible)
            {
                LblSinCuidadores.Opacity = 0;
                LblSinCuidadores.Scale = 0.95;
                LblSinCuidadores.IsVisible = true;
                _ = LblSinCuidadores.FadeTo(1, 300, Easing.CubicOut);
                _ = LblSinCuidadores.ScaleTo(1, 360, Easing.CubicOut);
            }
            else
            {
                LblSinCuidadores.IsVisible = vacio;
            }

            var i = 0;
            foreach (var cuidador in cuidadores)
            {
                var tarjeta = CrearTarjetaCuidador(cuidador);
                tarjeta.Opacity = 0;
                tarjeta.TranslationY = 18;
                ListaCuidadores.Add(tarjeta);

                var retraso = 120 + i++ * 60;
                _ = Task.Delay(retraso).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
                {
                    tarjeta.FadeTo(1, 280, Easing.CubicOut);
                    tarjeta.TranslateTo(0, 0, 340, Easing.CubicOut);
                }));
            }
        }

        private static Microsoft.Maui.Controls.Shapes.Geometry Geo(string d) =>
            (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(d)!;

        private static View Etiqueta(string icono, string texto, Color fondo, Color color) => new Border
        {
            Stroke = Colors.Transparent,
            BackgroundColor = fondo,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(10, 5),
            Content = new HorizontalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    new Microsoft.Maui.Controls.Shapes.Path { Data = Geo(icono), Fill = color, Aspect = Stretch.Uniform, WidthRequest = 12, HeightRequest = 12, VerticalOptions = LayoutOptions.Center },
                    new Label { Text = texto, FontSize = 12, FontFamily = "OpenSansSemibold", TextColor = color, VerticalOptions = LayoutOptions.Center }
                }
            }
        };

        private View CrearTarjetaCuidador(CuidadorCercano cuidador)
        {
            Color R(string k) => (Color)Application.Current!.Resources[k];

            // Foto con indicador "en línea"
            var foto = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb("#E5E7EB"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                WidthRequest = 64,
                HeightRequest = 64
            };
            if (!string.IsNullOrWhiteSpace(cuidador.FotoUrl))
                foto.Content = new Image { Source = $"{ApiService.ServerOrigin}{cuidador.FotoUrl}", Aspect = Aspect.AspectFill };
            else
                foto.Content = new Label
                {
                    Text = string.Concat(cuidador.NombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0]))),
                    FontSize = 20, FontFamily = "OpenSansSemibold", TextColor = R("ColorPrimary"),
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                };

            var enLinea = new Border
            {
                Stroke = Colors.White,
                StrokeThickness = 3,
                BackgroundColor = Color.FromArgb("#2E7D32"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                WidthRequest = 18,
                HeightRequest = 18,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.End
            };
            var bloqueFoto = new Grid { WidthRequest = 64, HeightRequest = 64, VerticalOptions = LayoutOptions.Start };
            bloqueFoto.Add(foto);
            bloqueFoto.Add(enLinea);

            // ---- Nivel superior: foto · nombre/bio · precio ----
            var info = new VerticalStackLayout
            {
                Spacing = 3,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = cuidador.NombreCompleto, FontSize = 16, FontFamily = "OpenSansSemibold", TextColor = R("ColorTextStrong"), MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation }
                }
            };
            if (!string.IsNullOrWhiteSpace(cuidador.Bio))
                info.Add(new Label { Text = cuidador.Bio, FontSize = 13, FontFamily = "OpenSansRegular", TextColor = R("ColorTextMuted"), MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation });

            var precio = new VerticalStackLayout
            {
                Spacing = 0,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End,
                Children =
                {
                    new Label { Text = Localizador.F("rd_monto", cuidador.TarifaHora), FontSize = 17, FontFamily = "OpenSansSemibold", TextColor = R("ColorPrimary"), HorizontalTextAlignment = TextAlignment.End },
                    new Label { Text = Localizador.T("sufijo_por_hora"), FontSize = 12, FontFamily = "OpenSansRegular", TextColor = R("ColorTextMuted"), HorizontalTextAlignment = TextAlignment.End }
                }
            };

            var arriba = new Grid
            {
                ColumnSpacing = 14,
                ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
            };
            arriba.Add(bloqueFoto, 0, 0);
            arriba.Add(info, 1, 0);
            arriba.Add(precio, 2, 0);

            // ---- Nivel inferior: distancia · "Ver perfil" ----
            var distancia = Etiqueta("M12 2C8.13 2 5 5.13 5 9C5 14.25 12 22 12 22C12 22 19 14.25 19 9C19 5.13 15.87 2 12 2ZM12 11.5C10.62 11.5 9.5 10.38 9.5 9C9.5 7.62 10.62 6.5 12 6.5C13.38 6.5 14.5 7.62 14.5 9C14.5 10.38 13.38 11.5 12 11.5Z",
                Localizador.F("a_km_de_ti", cuidador.DistanciaKm), R("ColorBackground"), R("ColorTextMuted"));
            distancia.HorizontalOptions = LayoutOptions.Start;
            distancia.VerticalOptions = LayoutOptions.Center;

            var flecha = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = Geo("M10 6L8.59 7.41L13.17 12L8.59 16.59L10 18L16 12L10 6Z"),
                Fill = R("ColorPrimary"), Aspect = Stretch.Uniform, WidthRequest = 16, HeightRequest = 16, VerticalOptions = LayoutOptions.Center
            };
            var verPerfil = new HorizontalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = Localizador.T("ver_perfil"), FontSize = 13, FontFamily = "OpenSansSemibold", TextColor = R("ColorPrimary"), VerticalOptions = LayoutOptions.Center },
                    flecha
                }
            };

            var abajo = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }
            };
            abajo.Add(distancia, 0, 0);
            abajo.Add(verPerfil, 1, 0);

            var grid = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    arriba,
                    new BoxView { HeightRequest = 1, Color = R("ColorBorder") },
                    abajo
                }
            };

            var card = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Colors.White,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
                Padding = new Thickness(14),
                Content = grid
            };
            SemanticProperties.SetDescription(card, cuidador.NombreCompleto);

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                // Respuesta al tocar: la tarjeta se hunde y la flecha de "Ver perfil" avanza.
                _ = flecha.TranslateTo(5, 0, 120, Easing.CubicOut);
                await card.ScaleTo(0.97, 100, Easing.CubicOut);
                _ = card.ScaleTo(1, 180, Easing.CubicOut);
                await AbrirDetalle(cuidador);
                flecha.TranslationX = 0;
            };
            card.GestureRecognizers.Add(tap);

            return card;
        }

        private async Task AbrirDetalle(CuidadorCercano cuidador)
        {
            try
            {
                var parametros = new Dictionary<string, object> { { "Cuidador", cuidador } };
                await Shell.Current.GoToAsync("CuidadorDetallePage", parametros);
            }
            catch (Exception ex)
            {
                await Alerta.MostrarAsync(Localizador.T("error_al_abrir_el_perfil"), ex.ToString(), Localizador.T("ok"));
            }
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
