using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CUIDAPP.Localization
{
    /// <summary>
    /// Lee los Términos y Condiciones en voz alta con la voz del teléfono (TextToSpeech), sección por
    /// sección, en el idioma de la app. Pausa (retoma desde la sección actual), anterior y siguiente.
    /// En creol (o si el teléfono no tiene voz para el idioma) muestra un aviso en lugar de los controles.
    /// </summary>
    public class ReproductorTerminos : ContentView
    {
        /// <summary>Índice de la parte que se empieza a leer (para resaltarla y llevar el scroll).</summary>
        public event Action<int>? ParteCambiada;
        /// <summary>Se leyó todo hasta el final (sin saltarse la última parte).</summary>
        public event Action? Completado;
        /// <summary>Se detuvo o terminó: la pantalla quita el resaltado.</summary>
        public event Action? Detenido;

        private static readonly Easing EaseOut = new(t => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 3));
        private const int MaxCaracteres = 3500; // Android corta las frases de más de ~4000 caracteres

        private List<string> segmentos = new();
        private int indice;
        private bool reproduciendo;
        private CancellationTokenSource? cts;
        private Locale? voz;

        private readonly Border boton;
        private readonly Microsoft.Maui.Controls.Shapes.Path icono;
        private readonly Label titulo, detalle;
        private readonly Border btnAnterior, btnSiguiente;
        private readonly Grid controles;
        private readonly Border aviso;
        private readonly Label lblAviso;

        private const string IconoPlay = "M8 5v14l11-7z";
        private const string IconoPausa = "M6 19h4V5H6v14zm8-14v14h4V5h-4z";

        public ReproductorTerminos()
        {
            icono = Icono(IconoPlay, Colors.White, 18);
            boton = new Border
            {
                WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0, BackgroundColor = Tema.C("ColorPrimary"),
                StrokeShape = new Ellipse(), VerticalOptions = LayoutOptions.Center, Content = icono
            };
            Tocar(boton, AlternarAsync);

            titulo = new Label { Text = Localizador.T("tts_escuchar"), FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong"), LineBreakMode = LineBreakMode.TailTruncation };
            detalle = new Label { Text = Localizador.T("tts_escuchar_desc"), FontSize = 12, FontFamily = "OpenSansRegular", TextColor = Tema.C("ColorTextMuted"), LineBreakMode = LineBreakMode.TailTruncation };

            btnAnterior = BotonChico("M6 6h2v12H6zm3.5 6 8.5 6V6z", Localizador.T("tts_anterior"), () => SaltarAsync(-1));
            btnSiguiente = BotonChico("M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z", Localizador.T("tts_siguiente"), () => SaltarAsync(1));

            controles = new Grid
            {
                ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
                ColumnSpacing = 10
            };
            controles.Add(boton, 0);
            controles.Add(new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center, Children = { titulo, detalle } }, 1);
            controles.Add(btnAnterior, 2);
            controles.Add(btnSiguiente, 3);

            lblAviso = new Label { FontSize = 12.5, FontFamily = "OpenSansRegular", TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center };
            var filaAviso = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 10 };
            filaAviso.Add(Icono("M16.5 12c0-1.77-1.02-3.29-2.5-4.03v2.21l2.45 2.45c.03-.2.05-.41.05-.63zm2.5 0c0 .94-.2 1.82-.54 2.64l1.51 1.51C20.63 14.91 21 13.5 21 12c0-4.28-2.99-7.86-7-8.77v2.06c2.89.86 5 3.54 5 6.71zM4.27 3 3 4.27 7.73 9H3v6h4l5 5v-6.73l4.25 4.25c-.67.52-1.42.93-2.25 1.18v2.06c1.38-.31 2.63-.95 3.69-1.81L19.73 21 21 19.73l-9-9L4.27 3zM12 4 9.91 6.09 12 8.18V4z", Tema.C("ColorWarning"), 20), 0);
            filaAviso.Add(lblAviso, 1);
            aviso = new Border { IsVisible = false, StrokeThickness = 0, BackgroundColor = Colors.Transparent, Content = filaAviso };

            Content = new Border
            {
                Stroke = Tema.C("ColorBorder"), StrokeThickness = 1, BackgroundColor = Tema.C("ColorSurface"),
                StrokeShape = new RoundRectangle { CornerRadius = 16 }, Padding = new Thickness(10, 8, 8, 8),
                Content = new Grid { Children = { controles, aviso } }
            };
            ActualizarBotones();
        }

        /// <summary>Carga el texto del idioma actual y decide si hay voz disponible. Detiene lo que se esté leyendo.</summary>
        public async Task PrepararAsync(List<string> segmentosNuevos)
        {
            Detener();
            segmentos = segmentosNuevos;
            indice = 0;
            voz = null;

            var codigo = Localizador.Instancia.Codigo;
            string? motivo = null;
            if (codigo == "ht")
                motivo = Localizador.T("tts_no_disponible_ht");
            else
            {
                try
                {
                    var voces = await TextToSpeech.Default.GetLocalesAsync();
                    voz = ElegirVoz(voces, codigo);
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[TTS] {ex.Message}"); }
                if (voz == null) motivo = Localizador.T("tts_sin_voz");
            }

            aviso.IsVisible = motivo != null;
            controles.IsVisible = motivo == null;
            lblAviso.Text = motivo ?? "";
            titulo.Text = Localizador.T("tts_escuchar");
            detalle.Text = Localizador.T("tts_escuchar_desc");
            ActualizarBotones();
        }

        private static Locale? ElegirVoz(IEnumerable<Locale> voces, string codigo)
        {
            var delIdioma = voces.Where(v => string.Equals(v.Language, codigo, StringComparison.OrdinalIgnoreCase)
                || (v.Language ?? "").StartsWith(codigo + "-", StringComparison.OrdinalIgnoreCase)).ToList();
            // Preferencias de país: español latino, inglés de EE. UU., chino de China continental.
            string[] preferidos = codigo switch { "es" => new[] { "DO", "US", "MX", "419", "ES" }, "en" => new[] { "US", "GB" }, "zh" => new[] { "CN" }, "ko" => new[] { "KR" }, _ => Array.Empty<string>() };
            foreach (var pais in preferidos)
            {
                var v = delIdioma.FirstOrDefault(x => string.Equals(x.Country, pais, StringComparison.OrdinalIgnoreCase));
                if (v != null) return v;
            }
            return delIdioma.FirstOrDefault();
        }

        public void Detener()
        {
            var estaba = reproduciendo;
            reproduciendo = false;
            cts?.Cancel();
            cts = null;
            ActualizarBotones();
            if (estaba) Detenido?.Invoke();
        }

        private async Task AlternarAsync()
        {
            if (reproduciendo) { Detener(); return; }
            if (voz == null || segmentos.Count == 0) return;
            if (indice >= segmentos.Count) indice = 0;
            await LeerDesdeAsync(indice);
        }

        private async Task SaltarAsync(int paso)
        {
            if (segmentos.Count == 0) return;
            var nuevo = Math.Clamp(indice + paso, 0, segmentos.Count - 1);
            if (reproduciendo)
            {
                cts?.Cancel();
                await LeerDesdeAsync(nuevo);
            }
            else
            {
                indice = nuevo;
                ActualizarBotones();
                ParteCambiada?.Invoke(indice);
            }
        }

        private async Task LeerDesdeAsync(int desde)
        {
            cts?.Cancel();
            var miCts = cts = new CancellationTokenSource();
            reproduciendo = true;
            var opciones = new SpeechOptions { Locale = voz, Pitch = 1.0f, Volume = 1.0f };
            try
            {
                for (indice = desde; indice < segmentos.Count; indice++)
                {
                    ActualizarBotones();
                    ParteCambiada?.Invoke(indice);
                    foreach (var trozo in Trozos(segmentos[indice]))
                    {
                        miCts.Token.ThrowIfCancellationRequested();
                        await TextToSpeech.Default.SpeakAsync(trozo, opciones, miCts.Token);
                        // En Android, cancelar puede terminar SpeakAsync sin excepción: se corta aquí para no
                        // avanzar de sección (al seguir, retoma la misma).
                        miCts.Token.ThrowIfCancellationRequested();
                    }
                }
                // Llegó al final sin que lo cancelaran.
                reproduciendo = false;
                indice = 0;
                ActualizarBotones();
                Completado?.Invoke();
                Detenido?.Invoke();
            }
            catch (OperationCanceledException) { /* pausa, salto o salida de la pantalla */ }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] {ex}");
                if (ReferenceEquals(cts, miCts)) Detener();
            }
        }

        // Frases agrupadas en trozos que la voz del sistema acepta.
        private static IEnumerable<string> Trozos(string texto)
        {
            if (texto.Length <= MaxCaracteres) { yield return texto; yield break; }
            var actual = new System.Text.StringBuilder();
            foreach (var frase in System.Text.RegularExpressions.Regex.Split(texto, @"(?<=[\.\?\!;:。！？])\s+"))
            {
                if (actual.Length + frase.Length > MaxCaracteres && actual.Length > 0)
                {
                    yield return actual.ToString();
                    actual.Clear();
                }
                actual.Append(frase).Append(' ');
            }
            if (actual.Length > 0) yield return actual.ToString();
        }

        private void ActualizarBotones()
        {
            icono.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(reproduciendo ? IconoPausa : IconoPlay)!;
            SemanticProperties.SetDescription(boton, Localizador.T(reproduciendo ? "tts_pausar" : "tts_escuchar"));
            var activo = reproduciendo || indice > 0;
            btnAnterior.IsVisible = btnSiguiente.IsVisible = activo;
            if (activo && segmentos.Count > 0)
            {
                titulo.Text = Localizador.F("tts_parte", indice + 1, segmentos.Count);
                var texto = segmentos[Math.Min(indice, segmentos.Count - 1)];
                detalle.Text = texto.Length > 70 ? texto[..70] + "…" : texto;
            }
        }

        // ---------- piezas

        private static Microsoft.Maui.Controls.Shapes.Path Icono(string datos, Color color, double tam) => new()
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(datos)!,
            Fill = color, Aspect = Stretch.Uniform, WidthRequest = tam, HeightRequest = tam,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };

        private Border BotonChico(string datos, string descripcion, Func<Task> accion)
        {
            var b = new Border
            {
                WidthRequest = 38, HeightRequest = 38, StrokeThickness = 0, BackgroundColor = Tema.C("ColorSubtle"),
                StrokeShape = new Ellipse(), VerticalOptions = LayoutOptions.Center,
                Content = Icono(datos, Tema.C("ColorTextStrong"), 14)
            };
            SemanticProperties.SetDescription(b, descripcion);
            Tocar(b, accion);
            return b;
        }

        private static void Tocar(View vista, Func<Task> accion)
        {
            vista.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    _ = vista.ScaleTo(0.92, 80, EaseOut).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => vista.ScaleTo(1, 140, EaseOut)));
                    await accion();
                })
            });
        }
    }
}
