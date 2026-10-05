using System.ComponentModel;

namespace CUIDAPP.Localization
{
    /// <summary>
    /// Selector de idioma (tarjeta con control segmentado ES | EN). Cambia el idioma al instante,
    /// sin recargar la pantalla ni navegar: los textos {loc:T ...} se refrescan por binding.
    /// </summary>
    public partial class SelectorIdioma : ContentView
    {
        private readonly Dictionary<string, (Border Segmento, Label Texto)> _segmentos = new();

        public SelectorIdioma()
        {
            InitializeComponent();
            ConstruirSegmentos();
            ActualizarEstado();
        }

        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();

            // Suscripción solo mientras el control está en pantalla (evita fugas de memoria).
            Localizador.Instancia.PropertyChanged -= OnIdiomaCambiado;
            if (Handler != null)
            {
                Localizador.Instancia.PropertyChanged += OnIdiomaCambiado;
                ActualizarEstado();
            }
        }

        private void OnIdiomaCambiado(object? sender, PropertyChangedEventArgs e) => ActualizarEstado();

        private void ConstruirSegmentos()
        {
            foreach (var (codigo, _) in Localizador.Idiomas)
            {
                var texto = new Label
                {
                    Text = codigo.ToUpperInvariant(),
                    FontSize = 13,
                    FontFamily = "OpenSansSemibold",
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                var segmento = new Border
                {
                    Stroke = Colors.Transparent,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(16, 0),
                    HeightRequest = 32,
                    MinimumWidthRequest = 48,
                    Content = texto
                };
                segmento.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(() => Localizador.Instancia.Cambiar(codigo))
                });

                _segmentos[codigo] = (segmento, texto);
                Segmentos.Add(segmento);
            }
        }

        private void ActualizarEstado()
        {
            var activo = Localizador.Instancia.Codigo;

            foreach (var (codigo, (segmento, texto)) in _segmentos)
            {
                var esActivo = codigo == activo;
                segmento.BackgroundColor = esActivo ? Colors.White : Colors.Transparent;
                segmento.Shadow = esActivo
                    ? new Shadow { Brush = Colors.Black, Offset = new Point(0, 1), Radius = 3, Opacity = 0.12f }
                    : null!;
                texto.TextColor = esActivo ? Color.FromArgb("#2563EB") : Color.FromArgb("#6B7280");
                SemanticProperties.SetDescription(segmento, Localizador.Idiomas.First(i => i.Codigo == codigo).Nombre);
            }

            LblIdiomaActual.Text = Localizador.Idiomas.First(i => i.Codigo == activo).Nombre;
        }
    }
}
