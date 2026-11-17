using System.Text.Json;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CUIDAPP.Localization
{
    /// <summary>
    /// Términos y Condiciones oficiales. El texto vive en Resources/Terminos/terminos.{idioma}.json
    /// (español = versión oficial; los demás idiomas son traducción de cortesía). <see cref="Version"/>
    /// identifica el texto que acepta el usuario y viaja al servidor al registrarse.
    /// </summary>
    public static class Terminos
    {
        public sealed class Tarjeta { public string Titulo { get; set; } = ""; public string? Base { get; set; } public string Texto { get; set; } = ""; }
        public sealed class Bloque
        {
            public string? Subtitulo { get; set; }
            public string? Texto { get; set; }
            public List<string>? Puntos { get; set; }
            public List<Tarjeta>? Tarjetas { get; set; }
            public string? Nota { get; set; }
        }
        public sealed class Seccion { public string Numero { get; set; } = ""; public string Titulo { get; set; } = ""; public string? Aviso { get; set; } public List<Bloque> Bloques { get; set; } = new(); }
        public sealed class Documento
        {
            public string Version { get; set; } = "";
            public string Titulo { get; set; } = "";
            public string Intro { get; set; } = "";
            public string? AvisoTraduccion { get; set; }
            public List<Seccion> Secciones { get; set; } = new();
            public string DeclaracionTitulo { get; set; } = "";
            public string Declaracion { get; set; } = "";
            public string Casilla { get; set; } = "";
        }

        private static readonly Dictionary<string, Documento> _cache = new();
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

        /// <summary>Versión vigente (la del texto oficial en español).</summary>
        public static string Version => Cargar(Localizador.IdiomaBase).Version;

        /// <summary>Texto en el idioma actual; si no hay traducción, el oficial en español.</summary>
        public static Documento Actual => Cargar(Localizador.Instancia.Codigo);

        private static Documento Cargar(string codigo)
        {
            if (_cache.TryGetValue(codigo, out var doc)) return doc;
            var asm = typeof(Terminos).Assembly;
            var recurso = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith($".Terminos.terminos.{codigo}.json", StringComparison.OrdinalIgnoreCase));
            if (recurso == null)
                return codigo == Localizador.IdiomaBase ? new Documento() : Cargar(Localizador.IdiomaBase);
            using var s = asm.GetManifestResourceStream(recurso)!;
            doc = JsonSerializer.Deserialize<Documento>(s, Json) ?? new Documento();
            _cache[codigo] = doc;
            return doc;
        }

        // ---------------- Vista

        /// <summary>
        /// Rellena el contenedor con el documento completo. Devuelve las partes en orden (introducción,
        /// cada sección y la declaración), en paralelo con <see cref="Segmentos"/>: el lector de voz
        /// resalta la parte que está leyendo con <see cref="Resaltar"/>.
        /// </summary>
        public static List<Border> Construir(Layout contenedor)
        {
            var d = Actual;
            contenedor.Clear();
            var partes = new List<Border>();

            var intro = new VerticalStackLayout { Spacing = 14 };
            intro.Add(new Label { Text = d.Titulo, FontSize = 19, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong"), LineHeight = 1.2 });
            if (!string.IsNullOrWhiteSpace(d.AvisoTraduccion))
                intro.Add(Aviso(d.AvisoTraduccion!, "ColorWarningSoft", "ColorTextStrong"));
            intro.Add(Parrafo(d.Intro));
            partes.Add(Marco(intro));

            foreach (var s in d.Secciones)
                partes.Add(Marco(SeccionVista(s)));

            partes.Add(new Border
            {
                Stroke = Tema.C("ColorPrimary"), StrokeThickness = 1.2, BackgroundColor = Tema.C("ColorPrimarySoft"),
                StrokeShape = new RoundRectangle { CornerRadius = 16 }, Padding = new Thickness(16),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = d.DeclaracionTitulo, FontSize = 15, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorPrimary") },
                        Parrafo(d.Declaracion, fuerte: true)
                    }
                }
            });

            foreach (var p in partes) contenedor.Add(p);
            return partes;
        }

        /// <summary>Texto para leer en voz alta de cada parte, en el mismo orden que <see cref="Construir"/>.</summary>
        public static List<string> Segmentos()
        {
            var d = Actual;
            var lista = new List<string> { Unir(d.Titulo, d.Intro) };
            foreach (var s in d.Secciones)
            {
                var piezas = new List<string?> { $"{s.Numero}. {s.Titulo}", s.Aviso };
                foreach (var b in s.Bloques)
                {
                    piezas.Add(b.Subtitulo);
                    piezas.Add(b.Texto);
                    piezas.AddRange(b.Puntos ?? new());
                    foreach (var t in b.Tarjetas ?? new()) { piezas.Add(t.Titulo); piezas.Add(t.Texto); }
                    piezas.Add(b.Nota);
                }
                lista.Add(Unir(piezas.ToArray()));
            }
            lista.Add(Unir(d.DeclaracionTitulo, d.Declaracion));
            return lista;
        }

        private static string Unir(params string?[] partes)
            => string.Join(". ", partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim().TrimEnd('.', ':')));

        /// <summary>Marco invisible de cada parte; se tiñe mientras el lector la lee.</summary>
        private static Border Marco(View contenido) => new()
        {
            Stroke = Colors.Transparent, StrokeThickness = 1.5, BackgroundColor = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(12), Margin = new Thickness(-12),
            Content = contenido
        };

        /// <summary>Resalta la parte que se está leyendo (las demás vuelven a su aspecto normal).</summary>
        public static void Resaltar(List<Border> partes, int indice)
        {
            for (var i = 0; i < partes.Count; i++)
            {
                if (i == partes.Count - 1) // la declaración tiene su propio recuadro
                {
                    partes[i].StrokeThickness = i == indice ? 2.5 : 1.2;
                    continue;
                }
                partes[i].BackgroundColor = i == indice ? Tema.C("ColorPrimarySoft") : Colors.Transparent;
                partes[i].Stroke = i == indice ? Tema.C("ColorPrimary") : Colors.Transparent;
            }
        }

        private static View SeccionVista(Seccion s)
        {
            var numero = new Border
            {
                MinimumWidthRequest = 32, HeightRequest = 32, Padding = new Thickness(6, 0), StrokeThickness = 0,
                BackgroundColor = Tema.C("ColorPrimarySoft"), StrokeShape = new RoundRectangle { CornerRadius = 10 },
                VerticalOptions = LayoutOptions.Start,
                Content = new Label { Text = s.Numero, FontSize = 12, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorPrimary"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
            };
            var cabecera = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 12 };
            cabecera.Add(numero, 0);
            cabecera.Add(new Label { Text = s.Titulo, FontSize = 16, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center, LineHeight = 1.2 }, 1);

            var cuerpo = new VerticalStackLayout { Spacing = 14 };
            cuerpo.Add(cabecera);
            if (!string.IsNullOrWhiteSpace(s.Aviso))
                cuerpo.Add(Aviso(s.Aviso!, "ColorWarningSoft", "ColorTextStrong"));

            foreach (var b in s.Bloques)
            {
                var bloque = new VerticalStackLayout { Spacing = 8 };
                if (!string.IsNullOrWhiteSpace(b.Subtitulo))
                    bloque.Add(new Label { Text = b.Subtitulo, FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong") });
                if (!string.IsNullOrWhiteSpace(b.Texto))
                    bloque.Add(Parrafo(b.Texto!));
                foreach (var p in b.Puntos ?? new())
                    bloque.Add(Punto(p));
                foreach (var t in b.Tarjetas ?? new())
                    bloque.Add(TarjetaVista(t));
                if (!string.IsNullOrWhiteSpace(b.Nota))
                    bloque.Add(Aviso(b.Nota!, "ColorSubtle", "ColorTextMuted"));
                cuerpo.Add(bloque);
            }
            return cuerpo;
        }

        private static Label Parrafo(string texto, bool fuerte = false) => new()
        {
            Text = texto, FontSize = 14, FontFamily = "OpenSansRegular", LineHeight = 1.45,
            TextColor = Tema.C(fuerte ? "ColorTextStrong" : "ColorTextMuted")
        };

        private static View Punto(string texto)
        {
            var fila = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 10 };
            fila.Add(new Ellipse { Fill = Tema.C("ColorPrimary"), WidthRequest = 6, HeightRequest = 6, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(2, 8, 0, 0) }, 0);
            fila.Add(Parrafo(texto), 1);
            return fila;
        }

        private static View TarjetaVista(Tarjeta t) => new Border
        {
            Stroke = Tema.C("ColorBorder"), StrokeThickness = 1, BackgroundColor = Tema.C("ColorBackground"),
            StrokeShape = new RoundRectangle { CornerRadius = 14 }, Padding = new Thickness(14, 12),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = t.Titulo, FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong") },
                    new Label { Text = t.Base, FontSize = 12, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorPrimary"), IsVisible = !string.IsNullOrWhiteSpace(t.Base) },
                    new Label { Text = t.Texto, FontSize = 13, FontFamily = "OpenSansRegular", TextColor = Tema.C("ColorTextMuted"), LineHeight = 1.4 }
                }
            }
        };

        private static Border Aviso(string texto, string fondo, string color) => new()
        {
            Stroke = Colors.Transparent, BackgroundColor = Tema.C(fondo),
            StrokeShape = new RoundRectangle { CornerRadius = 12 }, Padding = new Thickness(12, 10),
            Content = new Label { Text = texto, FontSize = 13, FontFamily = "OpenSansRegular", TextColor = Tema.C(color), LineHeight = 1.4 }
        };
    }
}
