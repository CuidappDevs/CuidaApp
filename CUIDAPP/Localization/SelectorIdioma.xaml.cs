using System.ComponentModel;

namespace CUIDAPP.Localization
{
    /// <summary>
    /// Selector de idioma (tarjeta con control segmentado ES | EN | HT). Cambia el idioma al instante,
    /// sin recargar la pantalla ni navegar: los textos {loc:T ...} se refrescan por binding.
    /// </summary>
    public partial class SelectorIdioma : ContentView
    {
        private readonly Dictionary<string, (Border Segmento, Label Texto)> _segmentos = new();

        /// <summary>
        /// Modo píldora (🌐 ES | EN | HT) para colocar en una esquina sobre fondo de color, p. ej. el login.
        /// </summary>
        public static readonly BindableProperty CompactoProperty = BindableProperty.Create(
            nameof(Compacto), typeof(bool), typeof(SelectorIdioma), false,
            propertyChanged: (b, _, _) => ((SelectorIdioma)b).AplicarModo());

        public bool Compacto
        {
            get => (bool)GetValue(CompactoProperty);
            set => SetValue(CompactoProperty, value);
        }

        private void AplicarModo()
        {
            var c = Compacto;
            Textos.IsVisible = !c;
            Contenido.ColumnSpacing = c ? 2 : 12;
            Tarjeta.Stroke = c ? Color.FromArgb("#33FFFFFF") : Color.FromArgb("#E5E7EB");
            Tarjeta.BackgroundColor = c ? Color.FromArgb("#26FFFFFF") : Colors.White;
            Tarjeta.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = c ? 18 : 16 };
            Tarjeta.Padding = c ? new Thickness(8, 3, 3, 3) : new Thickness(14, 12);
            IconoCaja.BackgroundColor = c ? Colors.Transparent : Color.FromArgb("#EFF6FF");
            IconoCaja.WidthRequest = IconoCaja.HeightRequest = c ? 22 : 40;
            IconoGlobo.Fill = c ? Colors.White : Color.FromArgb("#2563EB");
            IconoGlobo.WidthRequest = IconoGlobo.HeightRequest = c ? 16 : 20;
            Pista.BackgroundColor = Colors.Transparent;
            Pista.Padding = c ? 0 : 3;
            if (!c) Pista.BackgroundColor = Color.FromArgb("#F3F4F6");

            foreach (var (segmento, texto) in _segmentos.Values)
            {
                segmento.HeightRequest = c ? 28 : 32;
                segmento.MinimumWidthRequest = c ? 36 : 48;
                segmento.Padding = new Thickness(c ? 10 : 16, 0);
                segmento.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = c ? 14 : 8 };
                texto.FontSize = c ? 12 : 13;
            }
            ActualizarEstado();
        }

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
                    Padding = new Thickness(13, 0),
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
                texto.TextColor = esActivo ? Color.FromArgb("#2563EB")
                    : Compacto ? Color.FromArgb("#D9E6F7") : Color.FromArgb("#6B7280");
                SemanticProperties.SetDescription(segmento, Localizador.Idiomas.First(i => i.Codigo == codigo).Nombre);
            }

            LblIdiomaActual.Text = Localizador.Idiomas.First(i => i.Codigo == activo).Nombre;
        }
    }
}
