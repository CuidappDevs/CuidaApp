using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;
using SkiaSharp.Extended.UI.Controls;

namespace CUIDAPP.Views.Comun
{
    /// <summary>
    /// Bienvenida animada (Lottie) a pantalla completa: se ve UNA vez por cuenta (Usuarios.BienvenidaVista).
    /// Contenido distinto para cliente y cuidador. Se muestra desde los paneles con <see cref="BienvenidaApp"/>.
    /// </summary>
    public partial class BienvenidaPage : ContentPage
    {
        private record Diapositiva(string Animacion, string ClaveTitulo, string ClaveTexto);

        private static readonly Diapositiva[] Cliente =
        {
            new("bienvenida_cliente", "ob_cli_1_t", "ob_cli_1_b"),
            new("servicios", "ob_cli_2_t", "ob_cli_2_b"),
            new("verificado", "ob_cli_3_t", "ob_cli_3_b"),
            new("seguimiento", "ob_cli_4_t", "ob_cli_4_b"),
            new("califica", "ob_cli_5_t", "ob_cli_5_b"),
        };

        private static readonly Diapositiva[] Cuidador =
        {
            new("bienvenida_cuidador", "ob_cui_1_t", "ob_cui_1_b"),
            new("disponible", "ob_cui_2_t", "ob_cui_2_b"),
            new("solicitud", "ob_cui_3_t", "ob_cui_3_b"),
            new("pin", "ob_cui_4_t", "ob_cui_4_b"),
            new("cobro", "ob_cui_5_t", "ob_cui_5_b"),
            new("seguridad", "ob_cui_6_t", "ob_cui_6_b"),
        };

        private readonly Diapositiva[] _diapositivas;
        private readonly string _nombre;
        private readonly bool _esCuidador;
        private readonly TaskCompletionSource _terminada = new();
        private int _indice = -1;
        private bool _animando;

        /// <summary>Se completa cuando el usuario termina o salta la bienvenida.</summary>
        public Task Terminada => _terminada.Task;

        public BienvenidaPage(bool esCuidador, string nombre)
        {
            InitializeComponent();
            _esCuidador = esCuidador;
            _diapositivas = esCuidador ? Cuidador : Cliente;
            _nombre = string.IsNullOrWhiteSpace(nombre) ? "" : nombre.Trim().Split(' ')[0];

            Contenido.Padding = new Thickness(24, BarraEstado.Alto() + 4, 24, 0);
            EspacioInferior.HeightRequest = 24 + BarraEstado.AltoInferior();

            for (int i = 0; i < _diapositivas.Length; i++)
                Puntos.Add(new Border
                {
                    StrokeThickness = 0, HeightRequest = 8, WidthRequest = 8,
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    BackgroundColor = Tema.C("ColorBorder")
                });
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            IniciarFondo();
            if (_indice < 0)
                await IrAAsync(0, adelante: true);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            this.AbortAnimation("FondoBienvenida");
        }

        // El usuario no puede salir con "atrás" sin pasar por Saltar/Comenzar (así se registra como vista).
        protected override bool OnBackButtonPressed()
        {
            _ = AnteriorAsync();
            return true;
        }

        // ---------------------------------------------------------------- navegación

        private async void OnSiguienteClicked(object? sender, EventArgs e) => await SiguienteAsync();
        private async void OnDeslizarIzquierda(object? sender, SwipedEventArgs e) => await SiguienteAsync();
        private async void OnDeslizarDerecha(object? sender, SwipedEventArgs e) => await AnteriorAsync();
        private async void OnSaltarTapped(object? sender, TappedEventArgs e) => await TerminarAsync();

        private async Task SiguienteAsync()
        {
            if (_indice >= _diapositivas.Length - 1)
                await TerminarAsync();
            else
                await IrAAsync(_indice + 1, adelante: true);
        }

        private async Task AnteriorAsync()
        {
            if (_indice > 0)
                await IrAAsync(_indice - 1, adelante: false);
        }

        private async Task IrAAsync(int indice, bool adelante)
        {
            if (_animando || indice == _indice)
                return;
            _animando = true;
            try
            {
                var dir = adelante ? 1 : -1;

                // Salida: la escena se encoge y se va hacia un lado; los textos se desvanecen.
                if (_indice >= 0)
                {
                    await Task.WhenAll(
                        EscenaAnimacion.TranslateTo(-60 * dir, 0, 200, Easing.CubicIn),
                        EscenaAnimacion.ScaleTo(0.85, 200, Easing.CubicIn),
                        EscenaAnimacion.FadeTo(0, 200, Easing.CubicIn),
                        LblTitulo.FadeTo(0, 150, Easing.CubicIn),
                        LblTexto.FadeTo(0, 150, Easing.CubicIn));
                }

                _indice = indice;
                var d = _diapositivas[indice];
                Animacion.Source = new SKFileLottieImageSource { File = $"bienvenida/{d.Animacion}.json" };
                Animacion.Progress = TimeSpan.Zero;
                LblTitulo.Text = indice == 0 ? Localizador.F(d.ClaveTitulo, _nombre) : Localizador.T(d.ClaveTitulo);
                LblTexto.Text = Localizador.T(d.ClaveTexto);

                bool ultima = indice == _diapositivas.Length - 1;
                BtnSiguiente.Text = Localizador.T(ultima ? (_esCuidador ? "ob_empezar_trabajar" : "ob_comenzar") : "ob_siguiente");
                _ = BtnSaltar.FadeTo(ultima ? 0 : 1, 200);
                BtnSaltar.InputTransparent = ultima;
                PintarPuntos();
                MoverFondo(indice);

                // Entrada: la escena llega desde el otro lado con rebote; los textos suben en cascada.
                EscenaAnimacion.TranslationX = 70 * dir;
                EscenaAnimacion.Scale = 0.8;
                LblTitulo.TranslationY = 24;
                LblTexto.TranslationY = 30;

                var escena = Task.WhenAll(
                    EscenaAnimacion.TranslateTo(0, 0, 420, Easing.CubicOut),
                    EscenaAnimacion.ScaleTo(1, 520, Easing.SpringOut),
                    EscenaAnimacion.FadeTo(1, 300, Easing.CubicOut));
                await Task.Delay(90);
                var titulo = Task.WhenAll(LblTitulo.FadeTo(1, 320, Easing.CubicOut), LblTitulo.TranslateTo(0, 0, 380, Easing.CubicOut));
                await Task.Delay(80);
                var texto = Task.WhenAll(LblTexto.FadeTo(1, 320, Easing.CubicOut), LblTexto.TranslateTo(0, 0, 420, Easing.CubicOut));
                await Task.WhenAll(escena, titulo, texto);

                if (ultima)
                {
                    // Llamada a la acción: el botón "late" una vez.
                    await BtnSiguiente.ScaleTo(1.05, 160, Easing.CubicOut);
                    await BtnSiguiente.ScaleTo(1, 220, Easing.SpringOut);
                }
            }
            finally
            {
                _animando = false;
            }
        }

        private void PintarPuntos()
        {
            for (int i = 0; i < Puntos.Children.Count; i++)
            {
                var punto = (Border)Puntos.Children[i];
                bool activo = i == _indice;
                punto.BackgroundColor = activo ? Tema.C("ColorPrimary") : Tema.C("ColorBorder");
                var desde = punto.WidthRequest;
                var hasta = activo ? 26 : 8;
                punto.Animate($"punto{i}", v => punto.WidthRequest = v, desde, hasta, length: 260, easing: Easing.CubicOut);
            }
        }

        private async Task TerminarAsync()
        {
            if (_terminada.Task.IsCompleted)
                return;
            await Task.WhenAll(Contenido.FadeTo(0, 220, Easing.CubicIn), Contenido.ScaleTo(1.04, 220, Easing.CubicIn));
            _terminada.TrySetResult();
            await Navigation.PopModalAsync(false);
        }

        // ---------------------------------------------------------------- fondo vivo

        private void IniciarFondo()
        {
            if (this.AnimationIsRunning("FondoBienvenida"))
                return;
            new Animation(t =>
            {
                var a = t * Math.PI * 2;
                Mancha1.TranslationX = 26 * Math.Sin(a); Mancha1.TranslationY = 18 * Math.Cos(a);
                Mancha2.TranslationX = 22 * Math.Cos(a); Mancha2.TranslationY = 26 * Math.Sin(a * 1.3);
                Mancha3.TranslationX = 18 * Math.Sin(a * 0.8); Mancha3.TranslationY = 20 * Math.Cos(a);
                Mancha1.Scale = 1 + 0.06 * Math.Sin(a);
                Mancha2.Scale = 1 + 0.08 * Math.Cos(a);
            }, 0, 1).Commit(this, "FondoBienvenida", length: 9000, easing: Easing.Linear, repeat: () => true);
        }

        // Cada diapositiva cambia el tono del fondo: las manchas rotan su color.
        private void MoverFondo(int indice)
        {
            var tonos = new[] { "ColorPrimarySoft", "ColorSuccessSoft", "ColorWarningSoft", "ColorDangerSoft" };
            Mancha1.Fill = new SolidColorBrush(Tema.C(tonos[indice % tonos.Length]));
            Mancha2.Fill = new SolidColorBrush(Tema.C(tonos[(indice + 1) % tonos.Length]));
            Mancha3.Fill = new SolidColorBrush(Tema.C(tonos[(indice + 2) % tonos.Length]));
            _ = Mancha1.RotateTo(indice * 40, 600, Easing.CubicOut);
        }

        // ---------------------------------------------------------------- botón
        private void OnBotonPresionado(object? sender, EventArgs e) => _ = BtnSiguiente.ScaleTo(0.96, 90, Easing.CubicOut);
        private void OnBotonSoltado(object? sender, EventArgs e) => _ = BtnSiguiente.ScaleTo(1, 140, Easing.CubicOut);
    }
}
