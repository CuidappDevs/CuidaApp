using CUIDAPP.Models;
using CUIDAPP.Services;

namespace CUIDAPP.Views.Cliente
{
    public partial class NotificacionesPage : ContentPage
    {
        public NotificacionesPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            RenderizarLista();
            NotificacionHistorial.MarcarTodasLeidas();
        }

        private void RenderizarLista()
        {
            var notificaciones = NotificacionHistorial.Obtener();

            ListaNotificaciones.Clear();
            EstadoVacio.IsVisible = notificaciones.Count == 0;
            if (EstadoVacio.IsVisible)
                _ = TimbrarCampanaAsync();

            // Entrada escalonada
            var i = 0;
            foreach (var n in notificaciones)
            {
                var tarjeta = CrearTarjeta(n);
                tarjeta.Opacity = 0;
                tarjeta.TranslationY = 12;
                ListaNotificaciones.Add(tarjeta);
                var retraso = Math.Min(i++, 8) * 45;
                _ = Task.Delay(retraso).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
                {
                    tarjeta.FadeTo(1, 260, Easing.CubicOut);
                    tarjeta.TranslateTo(0, 0, 320, Easing.CubicOut);
                }));
            }
        }

        private async Task TimbrarCampanaAsync()
        {
            await Task.Delay(250);
            foreach (var angulo in new double[] { 14, -12, 8, -6, 0 })
                await CampanaVacia.RotateTo(angulo, 70, Easing.CubicInOut);
        }

        private static Microsoft.Maui.Controls.Shapes.Geometry Geo(string d) =>
            (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(d)!;

        private View CrearTarjeta(Notificacion n)
        {
            Color R(string k) => (Color)Application.Current!.Resources[k];

            // Ícono vectorial y color según el tipo de aviso
            var (colorFondo, icono, colorIcono) = n.Tipo switch
            {
                "mensaje" => (Tema.C("ColorPrimarySoft"), "M20 2H4C2.9 2 2 2.9 2 4V22L6 18H20C21.1 18 22 17.1 22 16V4C22 2.9 21.1 2 20 2ZM6 9H18V11H6V9ZM14 14H6V12H14V14ZM18 8H6V6H18V8Z", R("ColorPrimary")),
                "solicitud" => (Tema.C("ColorWarningSoft"), "M19 3H14.82C14.4 1.84 13.3 1 12 1C10.7 1 9.6 1.84 9.18 3H5C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3ZM12 3C12.55 3 13 3.45 13 4C13 4.55 12.55 5 12 5C11.45 5 11 4.55 11 4C11 3.45 11.45 3 12 3ZM14 17H7V15H14V17ZM17 13H7V11H17V13ZM17 9H7V7H17V9Z", Color.FromArgb("#E09A1A")),
                "trabajo" => (Tema.C("ColorSuccessSoft"), "M12 22C13.1 22 14 21.1 14 20H10C10 21.1 10.9 22 12 22ZM18 16V11C18 7.93 16.36 5.36 13.5 4.68V4C13.5 3.17 12.83 2.5 12 2.5C11.17 2.5 10.5 3.17 10.5 4V4.68C7.63 5.36 6 7.92 6 11V16L4 18V19H20V18L18 16Z", Tema.C("ColorSuccess")),
                "aviso" => (Tema.C("ColorPrimarySoft"), "M18 11V13H22V11H18ZM16 17.61C16.96 18.32 18.21 19.26 19.2 20C19.6 19.47 20 18.93 20.4 18.4C19.41 17.66 18.16 16.72 17.2 16C16.8 16.54 16.4 17.08 16 17.61ZM20.4 5.6C20 5.07 19.6 4.53 19.2 4C18.21 4.74 16.96 5.68 16 6.4C16.4 6.93 16.8 7.47 17.2 8C18.16 7.28 19.41 6.35 20.4 5.6ZM4 9C2.9 9 2 9.9 2 11V13C2 14.1 2.9 15 4 15H5V19H7V15H8L13 18V6L8 9H4ZM15.5 12C15.5 10.67 14.92 9.47 14 8.65V15.34C14.92 14.53 15.5 13.33 15.5 12Z", R("ColorPrimary")),
                "geocerca" => (Tema.C("ColorDangerSoft"), "M1 21H23L12 2L1 21ZM13 18H11V16H13V18ZM13 14H11V10H13V14Z", R("ColorDanger")),
                _ => (R("ColorBackground"), "M12 22C13.1 22 14 21.1 14 20H10C10 21.1 10.9 22 12 22ZM18 16V11C18 7.93 16.36 5.36 13.5 4.68V4C13.5 3.17 12.83 2.5 12 2.5C11.17 2.5 10.5 3.17 10.5 4V4.68C7.63 5.36 6 7.92 6 11V16L4 18V19H20V18L18 16Z", R("ColorTextMuted"))
            };

            var iconoBorde = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = colorFondo,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                WidthRequest = 44,
                HeightRequest = 44,
                VerticalOptions = LayoutOptions.Start,
                Content = new Microsoft.Maui.Controls.Shapes.Path { Data = Geo(icono), Fill = colorIcono, Aspect = Stretch.Uniform, WidthRequest = 20, HeightRequest = 20, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
            };

            var titulo = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            titulo.Add(new Label { Text = n.Titulo, FontSize = 14, FontFamily = n.Leida ? "OpenSansRegular" : "OpenSansSemibold", TextColor = R("ColorTextStrong") }, 0);
            titulo.Add(new Label { Text = n.Fecha.ToString("d MMM, h:mm tt"), FontSize = 11, FontFamily = "OpenSansRegular", TextColor = R("ColorTextMuted"), VerticalOptions = LayoutOptions.Start }, 1);

            var contenido = new VerticalStackLayout
            {
                Spacing = 3,
                Children =
                {
                    titulo,
                    new Label { Text = n.Mensaje, FontSize = 13, FontFamily = "OpenSansRegular", TextColor = R("ColorTextMuted"), LineHeight = 1.3 }
                }
            };

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 14 };
            grid.Add(iconoBorde, 0, 0);
            grid.Add(contenido, 1, 0);

            // Sin leer: punto azul en la esquina
            if (!n.Leida)
                grid.Add(new Microsoft.Maui.Controls.Shapes.Ellipse { Fill = R("ColorPrimary"), WidthRequest = 9, HeightRequest = 9, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(38, -2, 0, 0) }, 0, 0);

            var card = new Border
            {
                Stroke = Colors.Transparent,
                BackgroundColor = Tema.C("ColorSurface"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                Padding = new Thickness(14),
                Content = grid
            };

            if (n.TrabajoId.HasValue)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (s, e) =>
                {
                    await card.ScaleTo(0.97, 100, Easing.CubicOut);
                    _ = card.ScaleTo(1, 160, Easing.CubicOut);
                    await IrAlTrabajoAsync(n.TrabajoId.Value);
                };
                card.GestureRecognizers.Add(tap);
            }

            return card;
        }

        private async Task IrAlTrabajoAsync(int trabajoId)
        {
            var rolId = Preferences.Default.Get("RolId", 0);
            await Navigation.PopModalAsync();

            if (rolId == 3) // Cuidador
                await Shell.Current.GoToAsync("TrabajosPage");
            else // Cliente
                await Shell.Current.GoToAsync("MiServicioPage");
        }

        private async void OnCerrarTapped(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}
