using CUIDAPP.Localization;
using CUIDAPP.Models.Contrato;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;
using static CUIDAPP.Helpers.ContratoVista;

namespace CUIDAPP.Helpers
{
    /// <summary>
    /// Lista editable del checklist diario de un contrato fijo: cada tarea con su texto y si pide foto.
    /// La usan la propuesta / contraoferta y la edición del checklist de un contrato activo.
    /// </summary>
    public sealed class EditorTareasContrato : VerticalStackLayout
    {
        public const int Maximo = 20;

        private sealed record Fila(View Vista, Entry Texto, Func<bool> PideFoto);

        private readonly List<Fila> filas = new();
        private readonly VerticalStackLayout lista = new() { Spacing = 10 };
        private readonly Border botonAgregar;
        private readonly Label vacio;

        public EditorTareasContrato(IEnumerable<TareaContrato>? iniciales)
        {
            Spacing = 12;
            vacio = Texto(Localizador.T("checklist_vacio"), true, 13);
            botonAgregar = Boton(Localizador.T("checklist_agregar"), TipoBoton.Secundario, () =>
            {
                var fila = Agregar("", false);
                fila.Texto.Focus();
                return Task.CompletedTask;
            });
            Children.Add(vacio);
            Children.Add(lista);
            Children.Add(botonAgregar);
            foreach (var t in iniciales ?? Enumerable.Empty<TareaContrato>())
                Agregar(t.Descripcion, t.RequiereFoto);
            Refrescar();
        }

        /// <summary>Tareas con texto, en el orden en que se ven.</summary>
        public List<TareaContrato> Tareas() => filas
            .Where(f => !string.IsNullOrWhiteSpace(f.Texto.Text))
            .Select((f, i) => new TareaContrato { Descripcion = f.Texto.Text!.Trim(), RequiereFoto = f.PideFoto(), Orden = i + 1 })
            .ToList();

        private Fila Agregar(string texto, bool pideFoto)
        {
            var entrada = new Entry { Text = texto, MaxLength = 200, Placeholder = Localizador.T("checklist_placeholder"), ReturnType = ReturnType.Done };

            // Chip "Foto": se enciende / apaga al tocarlo.
            var foto = pideFoto;
            var icono = new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M12 15.2C13.77 15.2 15.2 13.77 15.2 12S13.77 8.8 12 8.8 8.8 10.23 8.8 12 10.23 15.2 12 15.2ZM9 2L7.17 4H4C2.9 4 2 4.9 2 6V18C2 19.1 2.9 20 4 20H20C21.1 20 22 19.1 22 18V6C22 4.9 21.1 4 20 4H16.83L15 2H9Z")!,
                Aspect = Stretch.Uniform, WidthRequest = 14, HeightRequest = 14, VerticalOptions = LayoutOptions.Center
            };
            var etiquetaFoto = new Label { Text = Localizador.T("checklist_foto"), FontSize = 12, FontFamily = "OpenSansSemibold", VerticalOptions = LayoutOptions.Center };
            var chip = new Border
            {
                StrokeThickness = 1.2, StrokeShape = new RoundRectangle { CornerRadius = 12 }, Padding = new Thickness(10, 6),
                VerticalOptions = LayoutOptions.Center,
                Content = new HorizontalStackLayout { Spacing = 5, Children = { icono, etiquetaFoto } }
            };
            void PintarChip()
            {
                chip.BackgroundColor = Tema.C(foto ? "ColorPrimarySoft" : "ColorSurface");
                chip.Stroke = Tema.C(foto ? "ColorPrimary" : "ColorBorder");
                icono.Fill = Tema.C(foto ? "ColorPrimary" : "ColorTextMuted");
                etiquetaFoto.TextColor = Tema.C(foto ? "ColorPrimary" : "ColorTextMuted");
                SemanticProperties.SetDescription(chip, Localizador.T(foto ? "checklist_foto_si" : "checklist_foto_no"));
            }
            PintarChip();
            chip.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() =>
                {
                    foto = !foto;
                    PintarChip();
                    _ = chip.ScaleTo(0.94, 70, Easing.CubicOut).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() => chip.ScaleTo(1, 120, Easing.CubicOut)));
                })
            });

            var quitar = new Border
            {
                StrokeThickness = 0, BackgroundColor = Tema.C("ColorDangerSoft"), WidthRequest = 34, HeightRequest = 34,
                StrokeShape = new Ellipse(), VerticalOptions = LayoutOptions.Center,
                Content = new Microsoft.Maui.Controls.Shapes.Path
                {
                    Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12L19 6.41Z")!,
                    Fill = Tema.C("ColorDanger"), Aspect = Stretch.Uniform, WidthRequest = 12, HeightRequest = 12,
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                }
            };
            SemanticProperties.SetDescription(quitar, Localizador.T("checklist_quitar"));

            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 8
            };
            grid.Add(CampoTexto(entrada, 46), 0);
            grid.Add(chip, 1);
            grid.Add(quitar, 2);

            var fila = new Fila(grid, entrada, () => foto);
            quitar.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    await grid.FadeTo(0, 120, Easing.CubicOut);
                    filas.Remove(fila);
                    lista.Remove(grid);
                    Refrescar();
                })
            });

            filas.Add(fila);
            grid.Opacity = 0;
            lista.Add(grid);
            _ = grid.FadeTo(1, 160, Easing.CubicOut);
            Refrescar();
            return fila;
        }

        private void Refrescar()
        {
            vacio.IsVisible = filas.Count == 0;
            lista.IsVisible = filas.Count > 0;
            botonAgregar.IsVisible = filas.Count < Maximo;
        }
    }
}
