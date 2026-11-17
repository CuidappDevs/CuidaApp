using CUIDAPP.Helpers;
using CUIDAPP.Services;
using System.ComponentModel;

namespace CUIDAPP.Localization
{
    /// <summary>
    /// Selector de idioma: un desplegable con la bandera y el idioma actual. Al tocarlo se abre una
    /// hoja con todos los idiomas (<see cref="ListaIdiomasVista"/>). Cambia el idioma al instante,
    /// sin recargar la pantalla: los textos {loc:T ...} se refrescan por binding.
    /// </summary>
    public partial class SelectorIdioma : ContentView
    {
        private bool _abierto;

        /// <summary>Modo píldora (bandera + código + flecha) para una esquina sobre fondo de color, p. ej. el login.</summary>
        public static readonly BindableProperty CompactoProperty = BindableProperty.Create(
            nameof(Compacto), typeof(bool), typeof(SelectorIdioma), false,
            propertyChanged: (b, _, _) => ((SelectorIdioma)b).AplicarModo());

        public bool Compacto
        {
            get => (bool)GetValue(CompactoProperty);
            set => SetValue(CompactoProperty, value);
        }

        public SelectorIdioma()
        {
            InitializeComponent();
            ActualizarEstado();
        }

        private void AplicarModo()
        {
            var c = Compacto;
            IconoCaja.IsVisible = !c;
            Titulo.IsVisible = !c;
            Contenido.ColumnSpacing = c ? 0 : 12;
            Tarjeta.Stroke = c ? Colors.Transparent : Tema.C("ColorBorder");
            Tarjeta.BackgroundColor = Colors.Transparent;
            if (!c) Tarjeta.BackgroundColor = Tema.C("ColorSurface");
            Tarjeta.Padding = c ? 0 : new Thickness(14, 12);

            // Sobre el encabezado azul: píldora blanca translúcida con texto blanco.
            Desplegable.BackgroundColor = c ? Color.FromArgb("#26FFFFFF") : Tema.C("ColorSubtle");
            Desplegable.Stroke = c ? Color.FromArgb("#40FFFFFF") : Colors.Transparent;
            Desplegable.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = c ? 18 : 12 };
            Desplegable.HeightRequest = c ? 36 : 40;
            Desplegable.Padding = c ? new Thickness(12, 0, 12, 0) : new Thickness(12, 0, 10, 0);
            LblIdioma.TextColor = c ? Colors.White : Tema.C("ColorTextStrong");
            Flecha.Fill = c ? Colors.White : Tema.C("ColorTextMuted");
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

        private void ActualizarEstado()
        {
            var activo = Localizador.Instancia.Codigo;
            var nombre = Localizador.Idiomas.First(i => i.Codigo == activo).Nombre;
            LblBandera.Text = Localizador.Bandera(activo);
            LblIdioma.Text = Compacto ? activo.ToUpperInvariant() : nombre;
            SemanticProperties.SetDescription(Desplegable, $"{Localizador.T("idioma")}: {nombre}");
        }

        private async void OnAbrir(object? sender, TappedEventArgs e)
        {
            if (_abierto) return;
            _abierto = true;
            try
            {
                await Desplegable.ScaleTo(0.96, 80, Easing.CubicOut);
                _ = Desplegable.ScaleTo(1, 140, Easing.CubicOut);
                _ = Flecha.RotateTo(180, 180, Easing.CubicOut);

                var hoja = new ListaIdiomasVista();
                var cerrada = new TaskCompletionSource();
                hoja.Cerrada += (_, _) => cerrada.TrySetResult();
                if (await Alerta.PresentarAsync(hoja))
                    await cerrada.Task;
            }
            finally
            {
                _ = Flecha.RotateTo(0, 180, Easing.CubicOut);
                _abierto = false;
            }
        }
    }
}
