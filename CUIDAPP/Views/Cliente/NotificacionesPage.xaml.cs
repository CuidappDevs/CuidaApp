using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CUIDAPP.Views.Cliente
{
    /// <summary>
    /// Historial de notificaciones del teléfono, agrupado por día. Tocar una abre su destino
    /// (servicio, chat, contrato, pago, ticket…) con la misma lógica que las notificaciones del sistema.
    /// </summary>
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
        }

        // Se marcan como leídas al salir: mientras la lista está abierta se ve cuáles eran nuevas.
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            NotificacionHistorial.MarcarTodasLeidas();
        }

        private void RenderizarLista()
        {
            var notificaciones = NotificacionHistorial.Obtener();
            var noLeidas = notificaciones.Count(n => !n.Leida);

            ListaNotificaciones.Clear();
            EstadoVacio.IsVisible = notificaciones.Count == 0;
            Desplazamiento.IsVisible = notificaciones.Count > 0;
            Acciones.IsVisible = notificaciones.Count > 0;
            BtnLeidas.IsVisible = noLeidas > 0;
            LblResumen.Text = notificaciones.Count == 0 ? ""
                : noLeidas > 0 ? Localizador.F("nh_sin_leer", noLeidas) : Localizador.T("nh_al_dia");
            if (EstadoVacio.IsVisible)
                _ = TimbrarCampanaAsync();

            var i = 0;
            string? grupoActual = null;
            foreach (var n in notificaciones)
            {
                var grupo = Grupo(n.Fecha);
                if (grupo != grupoActual)
                {
                    grupoActual = grupo;
                    ListaNotificaciones.Add(new Label
                    {
                        Text = grupo, FontSize = 13, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextMuted"),
                        Margin = new Thickness(4, i == 0 ? 4 : 14, 0, 2)
                    });
                }

                var tarjeta = CrearTarjeta(n);
                tarjeta.Opacity = 0;
                tarjeta.TranslationY = 10;
                ListaNotificaciones.Add(tarjeta);
                var retraso = Math.Min(i++, 8) * 40;
                _ = Task.Delay(retraso).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
                {
                    tarjeta.FadeTo(1, 240, Easing.CubicOut);
                    tarjeta.TranslateTo(0, 0, 300, Easing.CubicOut);
                }));
            }
            ListaNotificaciones.Add(new ContentView { HeightRequest = 16 + BarraEstado.AltoInferior() });
        }

        private static string Grupo(DateTime f)
        {
            var hoy = DateTime.Today;
            if (f.Date == hoy) return Localizador.T("nh_hoy");
            if (f.Date == hoy.AddDays(-1)) return Localizador.T("nh_ayer");
            if (f.Date > hoy.AddDays(-7)) return Localizador.T("nh_semana");
            return Localizador.T("nh_anteriores");
        }

        private static string Cuando(DateTime f)
        {
            var dif = DateTime.Now - f;
            if (dif.TotalMinutes < 1) return Localizador.T("nh_ahora");
            if (dif.TotalMinutes < 60) return Localizador.F("nh_hace_min", (int)dif.TotalMinutes);
            if (f.Date == DateTime.Today || f.Date == DateTime.Today.AddDays(-1)) return f.ToString("h:mm tt", Localizador.Cultura);
            if (f.Date > DateTime.Today.AddDays(-7)) return f.ToString("ddd h:mm tt", Localizador.Cultura);
            return f.ToString("d MMM", Localizador.Cultura);
        }

        private async Task TimbrarCampanaAsync()
        {
            await Task.Delay(250);
            foreach (var angulo in new double[] { 14, -12, 8, -6, 0 })
                await CampanaVacia.RotateTo(angulo, 70, Easing.CubicInOut);
        }

        private static Geometry Geo(string d) => (Geometry)new PathGeometryConverter().ConvertFromInvariantString(d)!;

        // Ícono y color según a dónde lleva (o, en avisos viejos sin destino, según el tipo).
        private static (string Fondo, string Color, string Icono) Estilo(Notificacion n)
        {
            var clase = (n.Destino ?? "").Split(':')[0];
            if (string.IsNullOrEmpty(clase)) clase = n.Tipo;
            return clase switch
            {
                "chat" or "chatpin" or "mensaje" => ("ColorPrimarySoft", "ColorPrimary", "M20 2H4C2.9 2 2 2.9 2 4V22L6 18H20C21.1 18 22 17.1 22 16V4C22 2.9 21.1 2 20 2ZM6 9H18V11H6V9ZM14 14H6V12H14V14ZM18 8H6V6H18V8Z"),
                "contrato" or "jornadas" => ("ColorPrimarySoft", "ColorPrimary", "M14 2H6C4.9 2 4 2.9 4 4V20C4 21.1 4.9 22 6 22H18C19.1 22 20 21.1 20 20V8L14 2ZM16 18H8V16H16V18ZM16 14H8V12H16V14ZM13 9V3.5L18.5 9H13Z"),
                "dinero" or "pago" => ("ColorSuccessSoft", "ColorSuccess", "M21 7.28V5C21 3.9 20.1 3 19 3H5C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V16.72C21.59 16.37 22 15.74 22 15V9C22 8.26 21.59 7.63 21 7.28ZM20 15H13V9H20V15ZM16 13.5C15.17 13.5 14.5 12.83 14.5 12C14.5 11.17 15.17 10.5 16 10.5C16.83 10.5 17.5 11.17 17.5 12C17.5 12.83 16.83 13.5 16 13.5Z"),
                "deudas" => ("ColorWarningSoft", "ColorWarning", "M1 21H23L12 2L1 21ZM13 18H11V16H13V18ZM13 14H11V10H13V14Z"),
                "calificar" => ("ColorWarningSoft", "ColorWarning", "M12 17.27L18.18 21L16.54 13.97L22 9.24L14.81 8.62L12 2L9.19 8.62L2 9.24L7.46 13.97L5.82 21L12 17.27Z"),
                "ticket" => ("ColorPrimarySoft", "ColorPrimary", "M12 1C7.03 1 3 5.03 3 10V17C3 18.66 4.34 20 6 20H9V12H5V10C5 6.13 8.13 3 12 3C15.87 3 19 6.13 19 10V12H15V20H19V21H12V23H18C19.66 23 21 21.66 21 20V10C21 5.03 16.97 1 12 1Z"),
                "verificacion" => ("ColorSuccessSoft", "ColorSuccess", "M12 1L3 5V11C3 16.55 6.84 21.74 12 23C17.16 21.74 21 16.55 21 11V5L12 1ZM10 17L6 13L7.41 11.59L10 14.17L16.59 7.58L18 9L10 17Z"),
                "geocerca" => ("ColorDangerSoft", "ColorDanger", "M12 2C8.13 2 5 5.13 5 9C5 14.25 12 22 12 22C12 22 19 14.25 19 9C19 5.13 15.87 2 12 2ZM12 11.5C10.62 11.5 9.5 10.38 9.5 9C9.5 7.62 10.62 6.5 12 6.5C13.38 6.5 14.5 7.62 14.5 9C14.5 10.38 13.38 11.5 12 11.5Z"),
                "aviso" or "checkin" => ("ColorPrimarySoft", "ColorPrimary", "M18 11V13H22V11H18ZM16 17.61C16.96 18.32 18.21 19.26 19.2 20L20.4 18.4C19.41 17.66 18.16 16.72 17.2 16L16 17.61ZM20.4 5.6L19.2 4C18.21 4.74 16.96 5.68 16 6.4L17.2 8C18.16 7.28 19.41 6.35 20.4 5.6ZM4 9C2.9 9 2 9.9 2 11V13C2 14.1 2.9 15 4 15H5V19H7V15H8L13 18V6L8 9H4Z"),
                "solicitud" => ("ColorWarningSoft", "ColorWarning", "M19 3H14.82C14.4 1.84 13.3 1 12 1C10.7 1 9.6 1.84 9.18 3H5C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3ZM12 3C12.55 3 13 3.45 13 4C13 4.55 12.55 5 12 5C11.45 5 11 4.55 11 4C11 3.45 11.45 3 12 3ZM17 13H7V11H17V13ZM17 9H7V7H17V9Z"),
                "trabajo" => ("ColorSuccessSoft", "ColorSuccess", "M20 6H16V4C16 2.89 15.11 2 14 2H10C8.89 2 8 2.89 8 4V6H4C2.89 6 2.01 6.89 2.01 8L2 19C2 20.11 2.89 21 4 21H20C21.11 21 22 20.11 22 19V8C22 6.89 21.11 6 20 6ZM14 6H10V4H14V6Z"),
                _ => ("ColorSubtle", "ColorTextMuted", "M12 22C13.1 22 14 21.1 14 20H10C10 21.1 10.9 22 12 22ZM18 16V11C18 7.93 16.36 5.36 13.5 4.68V4C13.5 3.17 12.83 2.5 12 2.5C11.17 2.5 10.5 3.17 10.5 4V4.68C7.63 5.36 6 7.92 6 11V16L4 18V19H20V18L18 16Z")
            };
        }

        private View CrearTarjeta(Notificacion n)
        {
            var (fondo, color, icono) = Estilo(n);
            var tieneDestino = !string.IsNullOrWhiteSpace(n.Destino);

            var iconoBorde = new Border
            {
                Stroke = Colors.Transparent, BackgroundColor = Tema.C(fondo),
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                WidthRequest = 42, HeightRequest = 42, VerticalOptions = LayoutOptions.Start,
                Content = new Microsoft.Maui.Controls.Shapes.Path { Data = Geo(icono), Fill = Tema.C(color), Aspect = Stretch.Uniform, WidthRequest = 19, HeightRequest = 19, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
            };

            var cabecera = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
            cabecera.Add(new Label
            {
                Text = n.Titulo, FontSize = 14.5, FontFamily = n.Leida ? "OpenSansRegular" : "OpenSansSemibold",
                TextColor = Tema.C("ColorTextStrong"), LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2
            }, 0);
            var hora = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Start };
            hora.Add(new Label { Text = Cuando(n.Fecha), FontSize = 11.5, FontFamily = n.Leida ? "OpenSansRegular" : "OpenSansSemibold", TextColor = Tema.C(n.Leida ? "ColorTextMuted" : "ColorPrimary"), VerticalOptions = LayoutOptions.Center });
            if (!n.Leida)
                hora.Add(new Ellipse { Fill = Tema.C("ColorPrimary"), WidthRequest = 8, HeightRequest = 8, VerticalOptions = LayoutOptions.Center });
            cabecera.Add(hora, 1);

            var contenido = new VerticalStackLayout { Spacing = 3, Children = { cabecera } };
            if (!string.IsNullOrWhiteSpace(n.Mensaje))
                contenido.Add(new Label { Text = n.Mensaje, FontSize = 13, FontFamily = "OpenSansRegular", TextColor = Tema.C("ColorTextMuted"), LineHeight = 1.3, MaxLines = 3, LineBreakMode = LineBreakMode.TailTruncation });
            if (tieneDestino)
                contenido.Add(new Label { Text = Localizador.T("nh_ver"), FontSize = 12.5, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorPrimary"), Margin = new Thickness(0, 4, 0, 0) });

            var grid = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 12 };
            grid.Add(iconoBorde, 0);
            grid.Add(contenido, 1);

            var card = new Border
            {
                Stroke = n.Leida ? Colors.Transparent : Tema.C("ColorPrimarySoft"),
                StrokeThickness = n.Leida ? 0 : 1.5,
                BackgroundColor = Tema.C("ColorSurface"),
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                Padding = new Thickness(14, 12),
                Content = grid
            };
            SemanticProperties.SetDescription(card, $"{n.Titulo}. {n.Mensaje}");

            if (tieneDestino)
            {
                card.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () =>
                    {
                        await card.ScaleTo(0.97, 90, Easing.CubicOut);
                        _ = card.ScaleTo(1, 150, Easing.CubicOut);
                        NotificacionHistorial.MarcarLeida(n);
                        await Navigation.PopModalAsync();
                        await NotificacionDestino.AbrirAsync(n.Destino!);
                    })
                });
            }
            return card;
        }

        private void OnMarcarLeidasTapped(object? sender, TappedEventArgs e)
        {
            NotificacionHistorial.MarcarTodasLeidas();
            RenderizarLista();
        }

        private async void OnBorrarTapped(object? sender, TappedEventArgs e)
        {
            if (!await Alerta.MostrarAsync(Localizador.T("nh_borrar_titulo"), Localizador.T("nh_borrar_texto"), Localizador.T("nh_borrar"), Localizador.T("cancelar"), TipoAlerta.Pregunta))
                return;
            NotificacionHistorial.Limpiar();
            RenderizarLista();
        }

        private async void OnCerrarTapped(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}
