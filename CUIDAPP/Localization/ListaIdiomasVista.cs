using CUIDAPP.Helpers;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CUIDAPP.Localization
{
    /// <summary>
    /// Hoja inferior con la lista de idiomas (bandera + nombre en su propio idioma). La abre
    /// <see cref="SelectorIdioma"/>; se muestra encima de todo con <see cref="Alerta.PresentarAsync"/>.
    /// </summary>
    public class ListaIdiomasVista : ContentView, IVistaFlotante
    {
        private static readonly Easing EaseOutFuerte = new(t => 1 - Math.Pow(1 - Math.Clamp(t, 0, 1), 4));

        private readonly BoxView _velo;
        private readonly Border _hoja;
        private bool _cerrando;

        public event EventHandler? Cerrada;

        public ListaIdiomasVista()
        {
            BackgroundColor = Colors.Transparent;

            _velo = new BoxView { Color = Tema.C("ColorShadow"), Opacity = 0 };
            _velo.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => _ = CancelarAsync()) });

            var lista = new VerticalStackLayout { Spacing = 6 };
            lista.Add(new BoxView
            {
                Color = Tema.C("ColorBorder"), WidthRequest = 40, HeightRequest = 4, CornerRadius = 2,
                HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 0, 14)
            });
            lista.Add(new Label
            {
                Text = Localizador.T("idioma"), FontFamily = "OpenSansSemibold", FontSize = 18,
                TextColor = Tema.C("ColorTextStrong"), Margin = new Thickness(4, 0, 0, 10)
            });

            foreach (var (codigo, nombre) in Localizador.Idiomas)
                lista.Add(Fila(codigo, nombre, codigo == Localizador.Instancia.Codigo));

            _hoja = new Border
            {
                BackgroundColor = Tema.C("ColorSurface"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28, 28, 0, 0) },
                Padding = new Thickness(16, 12, 16, 24 + BarraEstado.AltoInferior()),
                VerticalOptions = LayoutOptions.End,
                MaximumWidthRequest = 520,
                TranslationY = 600,
                Content = lista
            };

            Content = new Grid { Children = { _velo, _hoja } };
        }

        private View Fila(string codigo, string nombre, bool activo)
        {
            var check = new Border
            {
                WidthRequest = 24, HeightRequest = 24, StrokeThickness = 0,
                BackgroundColor = Tema.C("ColorPrimary"), StrokeShape = new Ellipse(),
                VerticalOptions = LayoutOptions.Center, IsVisible = activo,
                Content = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z")!,
                    Fill = Colors.White, Aspect = Stretch.Uniform, WidthRequest = 13, HeightRequest = 13,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                }
            };

            var fila = new Border
            {
                Stroke = activo ? Tema.C("ColorPrimary") : Colors.Transparent,
                StrokeThickness = activo ? 1.5 : 0,
                BackgroundColor = activo ? Tema.C("ColorPrimarySoft") : Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(14, 0),
                HeightRequest = 56,
                Content = new Grid
                {
                    ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
                    ColumnSpacing = 14,
                    Children =
                    {
                        new Label { Text = Localizador.Bandera(codigo), FontSize = 26, VerticalOptions = LayoutOptions.Center },
                        Columna(new Label
                        {
                            Text = nombre, FontSize = 16, VerticalOptions = LayoutOptions.Center,
                            FontFamily = activo ? "OpenSansSemibold" : "OpenSansRegular",
                            TextColor = activo ? Tema.C("ColorPrimary") : Tema.C("ColorTextStrong")
                        }, 1),
                        Columna(check, 2)
                    }
                }
            };
            SemanticProperties.SetDescription(fila, nombre);
            fila.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    if (_cerrando) return;
                    await fila.ScaleTo(0.98, 80, Easing.CubicOut);
                    _ = fila.ScaleTo(1, 120, Easing.CubicOut);
                    Localizador.Instancia.Cambiar(codigo);
                    await CancelarAsync();
                })
            });
            return fila;
        }

        private static View Columna(View vista, int columna)
        {
            Grid.SetColumn(vista, columna);
            return vista;
        }

        public async Task AnimarEntradaAsync()
        {
            _ = _velo.FadeTo(0.45, 200, Easing.CubicOut);
            await _hoja.TranslateTo(0, 0, 320, EaseOutFuerte);
        }

        public async Task CancelarAsync()
        {
            if (_cerrando) return;
            _cerrando = true;
            await Task.WhenAll(
                _velo.FadeTo(0, 180, Easing.CubicOut),
                _hoja.TranslateTo(0, _hoja.Height + 40, 200, Easing.CubicIn));
            Cerrada?.Invoke(this, EventArgs.Empty);
        }
    }
}
