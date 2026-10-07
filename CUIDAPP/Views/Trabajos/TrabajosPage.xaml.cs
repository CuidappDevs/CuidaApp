using CUIDAPP.Localization;
using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Trabajos
{
    public partial class TrabajosPage : ContentPage
    {
        private enum Tab { Nuevos, Aceptados, Historial }

        private readonly ApiService _apiService = new ApiService();
        private List<Trabajo> todosLosTrabajos = new();
        private Tab tabActual = Tab.Nuevos;

        public TrabajosPage()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Blanca();
            RealtimeService.NuevaSolicitud += OnCambioTiempoReal;
            RealtimeService.TrabajoActualizado += OnCambioTiempoReal;
            await CargarTrabajos();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            RealtimeService.NuevaSolicitud -= OnCambioTiempoReal;
            RealtimeService.TrabajoActualizado -= OnCambioTiempoReal;
        }

        private async void OnCambioTiempoReal(int a, int b)
        {
            await CargarTrabajos();
        }

        private async Task CargarTrabajos()
        {
            var cuidadorId = Preferences.Default.Get("UserId", 0);
            if (cuidadorId == 0)
                return;

            LoadingIndicator.IsRunning = true;
            ListaTrabajos.IsVisible = false;

            todosLosTrabajos = await _apiService.ObtenerTrabajosAsync(cuidadorId);

            LoadingIndicator.IsRunning = false;
            ListaTrabajos.IsVisible = true;

            RenderizarTab();
        }

        private void OnTabNuevosTapped(object sender, EventArgs e) => CambiarTab(Tab.Nuevos);
        private void OnTabAceptadosTapped(object sender, EventArgs e) => CambiarTab(Tab.Aceptados);
        private void OnTabHistorialTapped(object sender, EventArgs e) => CambiarTab(Tab.Historial);

        private void CambiarTab(Tab nuevaTab)
        {
            tabActual = nuevaTab;

            var activo = (Color)Application.Current!.Resources["ColorPrimary"];
            var inactivo = (Color)Application.Current!.Resources["ColorTextMuted"];

            // Control segmentado: la pestaña activa es una píldora blanca
            foreach (var (tab, indicador, etiqueta) in new[]
            {
                (Tab.Nuevos, IndicadorTabNuevos, LblTabNuevos),
                (Tab.Aceptados, IndicadorTabAceptados, LblTabAceptados),
                (Tab.Historial, IndicadorTabHistorial, LblTabHistorial)
            })
            {
                var esActiva = nuevaTab == tab;
                indicador.BackgroundColor = esActiva ? Colors.White : Colors.Transparent;
                etiqueta.TextColor = esActiva ? activo : inactivo;
                etiqueta.FontFamily = esActiva ? "OpenSansSemibold" : "OpenSansRegular";
                if (esActiva)
                {
                    indicador.Scale = 0.94;
                    _ = indicador.ScaleTo(1, 180, Easing.CubicOut);
                }
            }

            RenderizarTab();
        }

        private void RenderizarTab()
        {
            ListaTrabajos.Clear();

            var filtrados = tabActual switch
            {
                Tab.Nuevos => todosLosTrabajos.Where(t => t.Estado == 1),
                Tab.Aceptados => todosLosTrabajos.Where(t => t.Estado == 2 || t.Estado == 3 || t.Estado == 7),
                Tab.Historial => todosLosTrabajos.Where(t => t.Estado == 4 || t.Estado == 5 || t.Estado == 6),
                _ => Enumerable.Empty<Trabajo>()
            };

            var lista = filtrados.OrderBy(t => t.Fecha).ThenBy(t => t.HoraInicio).ToList();

            LblSinTrabajosTab.IsVisible = lista.Count == 0;

            // Entrada escalonada de las tarjetas
            var i = 0;
            foreach (var trabajo in lista)
            {
                var tarjeta = CrearTarjetaTrabajo(trabajo);
                tarjeta.Opacity = 0;
                tarjeta.TranslationY = 14;
                ListaTrabajos.Add(tarjeta);
                var retraso = Math.Min(i++, 8) * 50;
                _ = Task.Delay(retraso).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
                {
                    tarjeta.FadeTo(1, 260, Easing.CubicOut);
                    tarjeta.TranslateTo(0, 0, 320, Easing.CubicOut);
                }));
            }
        }

        private View CrearTarjetaTrabajo(Trabajo trabajo)
        {
            var (colorFondo, colorTexto, textoEstado) = trabajo.Estado switch
            {
                1 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), Localizador.T("pendiente")),
                2 => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#1E40AF"), Localizador.T("aceptado")),
                3 => (Color.FromArgb("#EDE9FE"), Color.FromArgb("#5B21B6"), Localizador.T("en_progreso")),
                4 => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#166534"), Localizador.T("completado")),
                5 => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), Localizador.T("cancelado")),
                6 => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#991B1B"), Localizador.T("rechazado")),
                7 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), Localizador.T("esperando_confirmacion")),
                _ => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), Localizador.T("desconocido"))
            };

            Color R(string k) => (Color)Application.Current!.Resources[k];
            var cultura = System.Globalization.CultureInfo.CurrentUICulture;

            // "Calendario" con el día y el mes
            var fecha = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb("#EAF1FB"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                WidthRequest = 56,
                HeightRequest = 60,
                VerticalOptions = LayoutOptions.Start,
                Content = new VerticalStackLayout
                {
                    Spacing = 0,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label { Text = trabajo.Fecha.ToString("dd"), FontSize = 20, FontFamily = "OpenSansSemibold", TextColor = R("ColorPrimary"), HorizontalOptions = LayoutOptions.Center },
                        new Label { Text = trabajo.Fecha.ToString("MMM", cultura).TrimEnd('.').ToUpperInvariant(), FontSize = 11, FontFamily = "OpenSansSemibold", TextColor = R("ColorPrimary"), HorizontalOptions = LayoutOptions.Center }
                    }
                }
            };

            var badge = new Border
            {
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                BackgroundColor = colorFondo,
                Padding = new Thickness(9, 3),
                HorizontalOptions = LayoutOptions.Start,
                Content = new HorizontalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        new Microsoft.Maui.Controls.Shapes.Ellipse { Fill = colorTexto, WidthRequest = 6, HeightRequest = 6, VerticalOptions = LayoutOptions.Center },
                        new Label { Text = textoEstado, FontSize = 11, FontFamily = "OpenSansSemibold", TextColor = colorTexto, VerticalOptions = LayoutOptions.Center }
                    }
                }
            };

            var reloj = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString("M11.99 2C6.47 2 2 6.48 2 12C2 17.52 6.47 22 11.99 22C17.52 22 22 17.52 22 12C22 6.48 17.52 2 11.99 2ZM12 20C7.58 20 4 16.42 4 12C4 7.58 7.58 4 12 4C16.42 4 20 7.58 20 12C20 16.42 16.42 20 12 20ZM12.5 7H11V13L16.25 16.15L17 14.92L12.5 12.25V7Z")!,
                Fill = R("ColorTextMuted"), Aspect = Stretch.Uniform, WidthRequest = 13, HeightRequest = 13, VerticalOptions = LayoutOptions.Center
            };

            var pie = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, Margin = new Thickness(0, 2, 0, 0) };
            pie.Add(new HorizontalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    reloj,
                    new Label { Text = $"{FormatearHora(trabajo.HoraInicio)} - {FormatearHora(trabajo.HoraFin)}", FontSize = 13, FontFamily = "OpenSansRegular", TextColor = R("ColorTextMuted"), VerticalOptions = LayoutOptions.Center }
                }
            }, 0, 0);
            pie.Add(new Label
            {
                Text = Localizador.F("rd_monto", trabajo.Tarifa),
                FontSize = 17,
                FontFamily = "OpenSansSemibold",
                TextColor = R("ColorPrimary"),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);

            var contenido = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    badge,
                    new Label { Text = Localizador.D(trabajo.TipoServicio), FontSize = 16, FontFamily = "OpenSansSemibold", TextColor = R("ColorTextStrong") },
                    new Label { Text = trabajo.ClienteNombre, FontSize = 13, FontFamily = "OpenSansRegular", TextColor = R("ColorTextMuted") },
                    pie
                }
            };

            var grid = new Grid { ColumnSpacing = 14, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
            grid.Add(fecha, 0, 0);
            grid.Add(contenido, 1, 0);

            var card = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Colors.White,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
                Padding = new Thickness(14),
                Content = grid
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                await card.ScaleTo(0.97, 100, Easing.CubicOut);
                _ = card.ScaleTo(1, 180, Easing.CubicOut);
                await AbrirDetalle(trabajo);
            };
            card.GestureRecognizers.Add(tap);

            return card;
        }

        private static string FormatearHora(TimeSpan hora)
        {
            return DateTime.Today.Add(hora).ToString("h:mm tt");
        }

        private async Task AbrirDetalle(Trabajo trabajo)
        {
            var parametros = new Dictionary<string, object> { { "Trabajo", trabajo } };
            await Shell.Current.GoToAsync("DetalleTrabajoPage", parametros);
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnInicioTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorDashboardPage");
        }

        private async void OnPerfilTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("CuidadorPerfilPage");
        }

        private async void OnDineroTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("DineroPage");
        }
    }
}
