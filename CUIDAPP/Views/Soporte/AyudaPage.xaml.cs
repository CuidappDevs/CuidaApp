using CUIDAPP.Localization;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp.Extended.UI.Controls;

namespace CUIDAPP.Views.Soporte
{
    /// <summary>
    /// Centro de ayuda del cuidador: contacto con soporte, preguntas frecuentes por categoría (cada una
    /// con su animación Lottie) y búsqueda. Los textos viven en los archivos de idioma (faq_*).
    /// </summary>
    public partial class AyudaPage : ContentPage
    {
        // Número del equipo de soporte (WhatsApp y llamadas).
        private const string TelefonoSoporte = "18298611275";

        private record Categoria(string Clave, string Animacion, string ColorFondo, string[] Preguntas);

        // Animaciones de Resources/Raw/bienvenida (las mismas de la bienvenida).
        private static readonly Categoria[] Categorias =
        {
            new("ayuda_cat_servicios", "servicios", "ColorPrimarySoft", new[] { "aceptar", "pin", "no_confirma", "cancelar" }),
            new("ayuda_cat_pagos", "cobro", "ColorSuccessSoft", new[] { "cobro", "estados", "propina" }),
            new("ayuda_cat_visibilidad", "disponible", "ColorSuccessSoft", new[] { "visible", "modos", "segundo_plano" }),
            new("ayuda_cat_cuenta", "verificado", "ColorPrimarySoft", new[] { "rechazado", "verificacion", "datos" }),
            new("ayuda_cat_seguridad", "seguridad", "ColorDangerSoft", new[] { "sos", "caidas", "contacto" }),
        };

        // Lo que se filtra con la búsqueda.
        private readonly List<(View Tarjeta, SKLottieView Animacion, Label Contador, List<(View Fila, string Texto)> Filas, Categoria Cat)> tarjetas = new();
        private bool entradaHecha;

        public AyudaPage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();
            LblVersion.Text = Localizador.F("ayuda_version", AppInfo.Current.VersionString);
            AnimacionEncabezado.Source = new SKFileLottieImageSource { File = "bienvenida/ayuda.json" };
            ConstruirCategorias();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            _ = AnimarEntradaAsync();
        }

        // Entrada escalonada: soporte y cada categoría suben con fade, y su animación arranca al aparecer.
        private async Task AnimarEntradaAsync()
        {
            if (entradaHecha) return;
            entradaHecha = true;
            var bloques = new List<View> { BloqueSoporte, TituloPreguntas };
            bloques.AddRange(tarjetas.Select(t => t.Tarjeta));
            foreach (var b in bloques) { b.Opacity = 0; b.TranslationY = 18; }
            try
            {
                for (int i = 0; i < bloques.Count; i++)
                {
                    _ = bloques[i].FadeTo(1, 280, Easing.CubicOut);
                    _ = bloques[i].TranslateTo(0, 0, 340, Easing.CubicOut);
                    var t = tarjetas.FirstOrDefault(x => x.Tarjeta == bloques[i]);
                    if (t.Animacion != null) Reproducir(t.Animacion, t.Cat.Animacion);
                    await Task.Delay(55);
                }
            }
            finally
            {
                foreach (var b in bloques) { b.Opacity = 1; b.TranslationY = 0; }
            }
        }

        // Vuelve a reproducir la animación de una categoría desde el principio (una vez).
        private static void Reproducir(SKLottieView vista, string animacion)
            => vista.Source = new SKFileLottieImageSource { File = $"bienvenida/{animacion}.json" };

        private void ConstruirCategorias()
        {
            foreach (var cat in Categorias)
            {
                var animacion = new SKLottieView { RepeatCount = 0, WidthRequest = 52, HeightRequest = 52, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
                var icono = new Border
                {
                    StrokeThickness = 0, BackgroundColor = Tema.C(cat.ColorFondo), StrokeShape = new RoundRectangle { CornerRadius = 16 },
                    WidthRequest = 56, HeightRequest = 56, Content = animacion
                };
                var titulo = new Label { Text = Localizador.T(cat.Clave), FontSize = 15, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong") };
                var contador = new Label { Text = Localizador.F("ayuda_n_preguntas", cat.Preguntas.Length), FontSize = 12, TextColor = Tema.C("ColorTextMuted") };
                var cabecera = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 14 };
                cabecera.Add(icono, 0);
                cabecera.Add(new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { titulo, contador } }, 1);
                // Tocar la cabecera repite la animación (pequeño detalle de vida).
                var tapCabecera = new TapGestureRecognizer();
                tapCabecera.Tapped += async (_, _) =>
                {
                    Reproducir(animacion, cat.Animacion);
                    await icono.ScaleTo(0.92, 90, Easing.CubicOut);
                    await icono.ScaleTo(1, 220, Easing.SpringOut);
                };
                cabecera.GestureRecognizers.Add(tapCabecera);

                var lista = new VerticalStackLayout { Spacing = 0, Margin = new Thickness(0, 10, 0, 0) };
                var filas = new List<(View, string)>();
                foreach (var clave in cat.Preguntas)
                {
                    var pregunta = Localizador.T($"faq_{clave}_p");
                    var respuesta = Localizador.T($"faq_{clave}_r");
                    var fila = CrearPregunta(pregunta, respuesta);
                    lista.Add(fila);
                    filas.Add((fila, (pregunta + " " + respuesta).ToLower(Localizador.Cultura)));
                }

                var tarjeta = new Border
                {
                    StrokeThickness = 0, BackgroundColor = Tema.C("ColorSurface"), StrokeShape = new RoundRectangle { CornerRadius = 22 },
                    Padding = new Thickness(16, 16, 16, 8),
                    Content = new VerticalStackLayout { Children = { cabecera, lista } }
                };
                ListaCategorias.Add(tarjeta);
                tarjetas.Add((tarjeta, animacion, contador, filas, cat));
            }
        }

        private View CrearPregunta(string pregunta, string respuesta)
        {
            var chevron = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M7.41 8.59L12 13.17L16.59 8.59L18 10L12 16L6 10L7.41 8.59Z")!,
                Fill = Tema.C("ColorChevron"), Aspect = Stretch.Uniform, WidthRequest = 18, HeightRequest = 18, VerticalOptions = LayoutOptions.Center
            };
            var lblPregunta = new Label { Text = pregunta, FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center };
            var lblRespuesta = new Label { Text = respuesta, FontSize = 13.5, TextColor = Tema.C("ColorTextMuted"), IsVisible = false, Margin = new Thickness(0, 0, 26, 12) };

            var cabecera = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 10, Padding = new Thickness(0, 12) };
            cabecera.Add(lblPregunta, 0);
            cabecera.Add(chevron, 1);

            var separador = new ContentView { HeightRequest = 1, BackgroundColor = Tema.C("ColorBorder") };
            var contenedor = new VerticalStackLayout { Children = { separador, cabecera, lblRespuesta } };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                var abrir = !lblRespuesta.IsVisible;
                _ = chevron.RotateTo(abrir ? 180 : 0, 200, Easing.CubicOut);
                lblPregunta.TextColor = Tema.C(abrir ? "ColorPrimary" : "ColorTextStrong");
                if (abrir)
                {
                    lblRespuesta.Opacity = 0;
                    lblRespuesta.TranslationY = -6;
                    lblRespuesta.IsVisible = true;
                    _ = lblRespuesta.FadeTo(1, 220, Easing.CubicOut);
                    await lblRespuesta.TranslateTo(0, 0, 220, Easing.CubicOut);
                }
                else
                {
                    await lblRespuesta.FadeTo(0, 120, Easing.CubicIn);
                    lblRespuesta.IsVisible = false;
                }
            };
            cabecera.GestureRecognizers.Add(tap);
            return contenedor;
        }

        // Búsqueda: muestra solo las preguntas que contienen el texto y abre sus respuestas.
        private void OnBuscarChanged(object sender, TextChangedEventArgs e)
        {
            var texto = (e.NewTextValue ?? "").Trim().ToLower(Localizador.Cultura);
            var hay = false;
            foreach (var (tarjeta, _, contador, filas, cat) in tarjetas)
            {
                var visibles = 0;
                foreach (var (fila, contenido) in filas)
                {
                    var coincide = texto.Length == 0 || contenido.Contains(texto);
                    fila.IsVisible = coincide;
                    if (coincide) visibles++;
                    // Con búsqueda, la respuesta se ve sin tener que tocar.
                    if (fila is VerticalStackLayout v && v.Children.Count == 3 && v.Children[2] is Label respuesta)
                    {
                        respuesta.IsVisible = texto.Length > 0 && coincide;
                        respuesta.Opacity = 1;
                    }
                }
                tarjeta.IsVisible = visibles > 0;
                contador.Text = Localizador.F("ayuda_n_preguntas", texto.Length == 0 ? cat.Preguntas.Length : visibles);
                hay |= visibles > 0;
            }
            LblSinResultados.IsVisible = !hay;
            BloqueSoporte.IsVisible = texto.Length == 0 || !hay;
        }

        private async void OnReportarTapped(object sender, TappedEventArgs e)
        {
            await Pulsar(BtnReportar);
            await Shell.Current.GoToAsync("NuevoReportePage");
        }

        private async void OnMisReportesTapped(object sender, TappedEventArgs e)
        {
            await Pulsar(BtnMisReportes);
            await Shell.Current.GoToAsync("MisReportesPage");
        }

        private async void OnWhatsAppTapped(object sender, TappedEventArgs e)
        {
            await Pulsar(BtnWhatsApp);
            var texto = Uri.EscapeDataString(Localizador.T("ayuda_whatsapp_mensaje"));
            await Abrir($"https://wa.me/{TelefonoSoporte}?text={texto}");
        }

        private async void OnLlamarTapped(object sender, TappedEventArgs e)
        {
            await Pulsar(BtnLlamar);
            await Abrir($"tel:+{TelefonoSoporte}");
        }

        private static async Task Abrir(string url)
        {
            try
            {
                await Launcher.Default.OpenAsync(new Uri(url));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ayuda] No se pudo abrir {url}: {ex.Message}");
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("ayuda_no_se_pudo_abrir"), Localizador.T("ok"));
            }
        }

        private async void OnBienvenidaTapped(object sender, TappedEventArgs e)
        {
            await Pulsar(BtnBienvenida);
            await BienvenidaApp.RepetirAsync();
        }

        private static async Task Pulsar(View v)
        {
            await v.ScaleTo(0.97, 80, Easing.CubicOut);
            _ = v.ScaleTo(1, 160, Easing.CubicOut);
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
