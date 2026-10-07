using CUIDAPP.Localization;
using CUIDAPP.Models.Trabajo;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class MiServicioPage : ContentPage
    {
        private readonly ApiService _apiService = new ApiService();

        public MiServicioPage()
        {
            InitializeComponent();

            // Borde a borde: el encabezado empieza debajo de la barra de estado y la lista
            // termina con espacio para la barra de gestos.
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            RealtimeService.TrabajoActualizado += OnTrabajoActualizadoTiempoReal;
            await CargarServicios();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            RealtimeService.TrabajoActualizado -= OnTrabajoActualizadoTiempoReal;
        }

        private async void OnTrabajoActualizadoTiempoReal(int trabajoId, int estado)
        {
            await CargarServicios();
        }

        private async Task CargarServicios()
        {
            var clienteId = Preferences.Default.Get("UserId", 0);
            if (clienteId == 0)
                return;

            LoadingIndicator.IsRunning = true;
            ListaServicios.Clear();
            ContenedorVacio.IsVisible = false;

            var servicios = await _apiService.ObtenerTrabajosActivosPorClienteAsync(clienteId);

            LoadingIndicator.IsRunning = false;

            if (servicios.Count == 0)
            {
                ContenedorVacio.Opacity = 0;
                ContenedorVacio.IsVisible = true;
                _ = ContenedorVacio.FadeTo(1, 300, Easing.CubicOut);
                return;
            }

            // Con un solo servicio activo, saltamos directo al detalle (misma experiencia de antes).
            // Usamos "../DetalleServicioClientePage" (en vez de solo el nombre de ruta) para que
            // esta página se reemplace en la pila de navegación, no se apile debajo: si no,
            // el botón "atrás" del detalle regresa aquí y este OnAppearing vuelve a saltar
            // adelante de inmediato, dando la sensación de un loop/bug al usuario.
            if (servicios.Count == 1)
            {
                await Shell.Current.GoToAsync($"../DetalleServicioClientePage", new Dictionary<string, object> { { "TrabajoId", servicios[0].Id } });
                return;
            }

            // Entrada escalonada de las tarjetas
            var i = 0;
            foreach (var servicio in servicios)
            {
                var tarjeta = CrearTarjetaServicio(servicio);
                tarjeta.Opacity = 0;
                tarjeta.TranslationY = 16;
                ListaServicios.Add(tarjeta);
                var retraso = i++ * 60;
                _ = Task.Delay(retraso).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
                {
                    tarjeta.FadeTo(1, 280, Easing.CubicOut);
                    tarjeta.TranslateTo(0, 0, 340, Easing.CubicOut);
                }));
            }
        }

        private View CrearTarjetaServicio(TrabajoCliente t)
        {
            var (colorFondo, colorTexto, texto) = t.Estado switch
            {
                1 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), Localizador.T("esperando_respuesta")),
                2 => (Color.FromArgb("#DBEAFE"), Color.FromArgb("#1E40AF"), Localizador.T("aceptado")),
                3 => (Color.FromArgb("#EDE9FE"), Color.FromArgb("#5B21B6"), Localizador.T("en_progreso")),
                4 => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#166534"), Localizador.T("completado_califica")),
                7 => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E"), Localizador.T("requiere_tu_confirmacion")),
                _ => (Color.FromArgb("#F3F4F6"), Color.FromArgb("#374151"), Localizador.T("en_curso"))
            };

            Color R(string k) => (Color)Application.Current!.Resources[k];

            var badge = new Border
            {
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                BackgroundColor = colorFondo,
                Padding = new Thickness(10, 4),
                HorizontalOptions = LayoutOptions.Start,
                Content = new HorizontalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        new Microsoft.Maui.Controls.Shapes.Ellipse { Fill = colorTexto, WidthRequest = 7, HeightRequest = 7, VerticalOptions = LayoutOptions.Center },
                        new Label { Text = texto, FontSize = 11, FontFamily = "OpenSansSemibold", TextColor = colorTexto, VerticalOptions = LayoutOptions.Center }
                    }
                }
            };

            var foto = new Border
            {
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(),
                BackgroundColor = Color.FromArgb("#E5E7EB"),
                WidthRequest = 56,
                HeightRequest = 56,
                VerticalOptions = LayoutOptions.Center,
                Content = new Image
                {
                    Aspect = Aspect.AspectFill,
                    Source = string.IsNullOrWhiteSpace(t.CuidadorFotoUrl) ? null : $"{ApiService.ServerOrigin}{t.CuidadorFotoUrl}"
                }
            };

            var contenido = new VerticalStackLayout
            {
                Spacing = 4,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = t.CuidadorNombre, FontSize = 16, FontFamily = "OpenSansSemibold", TextColor = R("ColorTextStrong"), MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation },
                    new Label { Text = Localizador.D(t.TipoServicio), FontSize = 13, FontFamily = "OpenSansRegular", TextColor = R("ColorTextMuted") },
                    badge
                }
            };

            var flecha = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString("M10 6L8.59 7.41L13.17 12L8.59 16.59L10 18L16 12L10 6Z")!,
                Fill = Color.FromArgb("#B0BEC5"), Aspect = Stretch.Uniform, WidthRequest = 18, HeightRequest = 18, VerticalOptions = LayoutOptions.Center
            };

            var grid = new Grid { ColumnSpacing = 14, ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            grid.Add(foto, 0, 0);
            grid.Add(contenido, 1, 0);
            grid.Add(flecha, 2, 0);

            var card = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Colors.White,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                Padding = new Thickness(14),
                Content = grid
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                _ = flecha.TranslateTo(4, 0, 120, Easing.CubicOut);
                await card.ScaleTo(0.97, 100, Easing.CubicOut);
                _ = card.ScaleTo(1, 180, Easing.CubicOut);
                await Shell.Current.GoToAsync("DetalleServicioClientePage", new Dictionary<string, object> { { "TrabajoId", t.Id } });
                flecha.TranslationX = 0;
            };
            card.GestureRecognizers.Add(tap);

            return card;
        }

        private async void OnBackTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
