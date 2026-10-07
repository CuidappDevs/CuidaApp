using CUIDAPP.Localization;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CUIDAPP.Views.Comun
{
    /// <summary>
    /// Selector de apariencia (claro | oscuro | automático). Al cambiar se recrea la interfaz
    /// y se vuelve a la pantalla indicada en <see cref="VolverA"/> (la ruta del perfil).
    /// </summary>
    public partial class SelectorTema : ContentView
    {
        private static readonly (ModoTema Modo, string Clave, string Icono)[] Opciones =
        {
            (ModoTema.Claro, "tema_claro", "M12 7C9.24 7 7 9.24 7 12S9.24 17 12 17 17 14.76 17 12 14.76 7 12 7ZM2 13H4C4.55 13 5 12.55 5 12S4.55 11 4 11H2C1.45 11 1 11.45 1 12S1.45 13 2 13ZM20 13H22C22.55 13 23 12.55 23 12S22.55 11 22 11H20C19.45 11 19 11.45 19 12S19.45 13 20 13ZM11 2V4C11 4.55 11.45 5 12 5S13 4.55 13 4V2C13 1.45 12.55 1 12 1S11 1.45 11 2ZM11 20V22C11 22.55 11.45 23 12 23S13 22.55 13 22V20C13 19.45 12.55 19 12 19S11 19.45 11 20ZM5.99 4.58C5.6 4.19 4.96 4.19 4.58 4.58 4.19 4.97 4.19 5.61 4.58 5.99L5.64 7.05C6.03 7.44 6.67 7.44 7.05 7.05S7.44 6.03 7.05 5.65L5.99 4.58ZM18.36 16.95C17.97 16.56 17.33 16.56 16.95 16.95 16.56 17.34 16.56 17.98 16.95 18.36L18.01 19.42C18.4 19.81 19.04 19.81 19.42 19.42 19.81 19.03 19.81 18.39 19.42 18.01L18.36 16.95ZM19.42 5.99C19.81 5.6 19.81 4.96 19.42 4.58 19.03 4.19 18.39 4.19 18.01 4.58L16.95 5.64C16.56 6.03 16.56 6.67 16.95 7.05S17.97 7.44 18.35 7.05L19.42 5.99ZM7.05 18.36C7.44 17.97 7.44 17.33 7.05 16.95 6.66 16.56 6.02 16.56 5.64 16.95L4.58 18.01C4.19 18.4 4.19 19.04 4.58 19.42S5.6 19.81 5.98 19.42L7.05 18.36Z"),
            (ModoTema.Oscuro, "tema_oscuro", "M12 3C7.03 3 3 7.03 3 12S7.03 21 12 21 21 16.97 21 12C21 11.54 20.96 11.08 20.9 10.64 19.92 12.01 18.32 12.9 16.5 12.9 13.52 12.9 11.1 10.48 11.1 7.5 11.1 5.69 11.99 4.08 13.36 3.1 12.92 3.04 12.46 3 12 3Z"),
            (ModoTema.Sistema, "tema_sistema", "M17 1.01L7 1C5.9 1 5 1.9 5 3V21C5 22.1 5.9 23 7 23H17C18.1 23 19 22.1 19 21V3C19 1.9 18.1 1.01 17 1.01ZM17 19H7V5H17V19Z"),
        };

        /// <summary>Ruta a la que volver tras recrear la interfaz (p. ej. "CuidadorPerfilPage").</summary>
        public string? VolverA { get; set; }

        private bool cambiando;

        public SelectorTema()
        {
            InitializeComponent();
            Construir();
        }

        private void Construir()
        {
            var actual = Tema.Modo;
            LblModoActual.Text = Localizador.T(Opciones.First(o => o.Modo == actual).Clave);
            Segmentos.Clear();

            foreach (var (modo, clave, icono) in Opciones)
            {
                bool activo = modo == actual;
                var segmento = new Border
                {
                    Stroke = Colors.Transparent,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    WidthRequest = 44,
                    HeightRequest = 32,
                    BackgroundColor = activo ? Tema.C("ColorSurface") : Colors.Transparent,
                    Shadow = activo ? new Shadow { Brush = Colors.Black, Offset = new Point(0, 1), Radius = 3, Opacity = 0.12f } : null!,
                    Content = new Microsoft.Maui.Controls.Shapes.Path
                    {
                        Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(icono)!,
                        Fill = activo ? Tema.C("ColorPrimary") : Tema.C("ColorTextMuted"),
                        Aspect = Stretch.Uniform,
                        WidthRequest = 16,
                        HeightRequest = 16,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    }
                };
                SemanticProperties.SetDescription(segmento, Localizador.T(clave));
                var m = modo;
                segmento.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                    {
                        if (cambiando || m == Tema.Modo) return;
                        cambiando = true;
                        await segmento.ScaleTo(0.92, 70, Easing.CubicOut);
                        await Tema.CambiarAsync(m, VolverA);
                        // Si el tema efectivo no cambió (p. ej. Automático con el teléfono en claro), no se
                        // recrea la interfaz: solo se repinta el selector.
                        Construir();
                        cambiando = false;
                    })
                });
                Segmentos.Add(segmento);
            }
        }
    }
}
