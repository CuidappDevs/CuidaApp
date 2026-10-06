using CUIDAPP.Helpers;
using Microsoft.Maui.Controls.Shapes;

namespace CUIDAPP.Views.Comun
{
    /// <summary>Contenido de la alerta propia (reemplaza al DisplayAlert nativo). Se muestra a través de <see cref="Alerta"/>.</summary>
    public partial class AlertaVista : ContentView
    {
        private readonly TaskCompletionSource<bool> _resultado = new();
        private bool _cerrando;

        public Task<bool> Resultado => _resultado.Task;

        public AlertaVista(string titulo, string mensaje, string aceptar, string? cancelar, TipoAlerta tipo)
        {
            InitializeComponent();

            LblTitulo.Text = titulo;
            LblTitulo.IsVisible = !string.IsNullOrWhiteSpace(titulo);
            LblMensaje.Text = mensaje;
            LblMensaje.IsVisible = !string.IsNullOrWhiteSpace(mensaje);

            BtnAceptar.Text = aceptar;
            if (cancelar == null)
            {
                // Un solo botón a lo ancho
                BtnCancelar.IsVisible = false;
                Grid.SetColumn(BtnAceptar, 0);
                Grid.SetColumnSpan(BtnAceptar, 2);
            }
            else
            {
                BtnCancelar.Text = cancelar;
            }

            var (color, icono) = Estilo(tipo);
            IconoCirculo.BackgroundColor = color;
            IconoHalo.Fill = color.WithAlpha(0.25f);
            Icono.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(icono)!;
            if (tipo == TipoAlerta.Error) BtnAceptar.BackgroundColor = color;
        }

        private static (Color, string) Estilo(TipoAlerta tipo) => tipo switch
        {
            TipoAlerta.Exito => (Color.FromArgb("#2E7D32"), "M9 16.17L4.83 12L3.41 13.41L9 19L21 7L19.59 5.59L9 16.17Z"),
            TipoAlerta.Error => (Color.FromArgb("#C62828"), "M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12L19 6.41Z"),
            TipoAlerta.Advertencia => (Color.FromArgb("#E09A1A"), "M11 15H13V17H11V15ZM11 7H13V13H11V7Z"),
            TipoAlerta.Pregunta => (Color.FromArgb("#1C4D96"), "M11 18H13V16H11V18ZM12 2C6.48 2 2 6.48 2 12C2 17.52 6.48 22 12 22C17.52 22 22 17.52 22 12C22 6.48 17.52 2 12 2ZM12 20C7.59 20 4 16.41 4 12C4 7.59 7.59 4 12 4C16.41 4 20 7.59 20 12C20 16.41 16.41 20 12 20ZM12 6C9.79 6 8 7.79 8 10H10C10 8.9 10.9 8 12 8C13.1 8 14 8.9 14 10C14 12 11 11.75 11 15H13C13 12.75 16 12.5 16 10C16 7.79 14.21 6 12 6Z"),
            _ => (Color.FromArgb("#1C4D96"), "M11 7H13V9H11V7ZM11 11H13V17H11V11Z"),
        };

        /// <summary>Se dispara cuando terminó la animación de salida y hay que quitar la alerta.</summary>
        public event EventHandler? Cerrada;

        public async Task AnimarEntradaAsync()
        {
            _ = Velo.FadeTo(0.45, 200, Easing.CubicOut);
            await Task.WhenAll(
                Tarjeta.FadeTo(1, 200, Easing.CubicOut),
                Tarjeta.ScaleTo(1, 260, Easing.CubicOut));
        }

        private void OnAceptar(object? sender, EventArgs e) => _ = CerrarAsync(true);

        private void OnCancelar(object? sender, EventArgs e) => _ = CerrarAsync(false);

        // Atrás del sistema = cancelar.
        public Task CancelarAsync() => CerrarAsync(false);

        private async Task CerrarAsync(bool valor)
        {
            if (_cerrando) return;
            _cerrando = true;

            // La salida es más rápida que la entrada.
            await Task.WhenAll(
                Velo.FadeTo(0, 150, Easing.CubicOut),
                Tarjeta.FadeTo(0, 150, Easing.CubicOut),
                Tarjeta.ScaleTo(0.96, 150, Easing.CubicOut));

            Cerrada?.Invoke(this, EventArgs.Empty);
            _resultado.TrySetResult(valor);
        }

        private void OnPresionado(object? sender, EventArgs e) => _ = (sender as VisualElement)?.ScaleTo(0.97, 100, Easing.CubicOut);

        private void OnSoltado(object? sender, EventArgs e) => _ = (sender as VisualElement)?.ScaleTo(1, 160, Easing.CubicOut);
    }
}
