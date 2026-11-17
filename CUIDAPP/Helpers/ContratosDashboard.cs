using CUIDAPP.Localization;
using CUIDAPP.Services;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;
using System.Text.Json;

namespace CUIDAPP.Helpers;

/// <summary>
/// Tarjeta compacta de contratos fijos en los inicios (cliente y Care Partner): una fila por contrato con
/// foto, nombre, estado de hoy y próxima jornada. Toda la tarjeta abre el contrato; ocupa poco para no
/// tapar el resto del inicio (pedir servicios, etc.).
/// </summary>
public static class ContratosDashboard
{
    public static async Task CargarAsync(VerticalStackLayout destino, bool cuidador)
    {
        destino.Clear();
        try
        {
            var rows = await new ApiService().SeguimientoSolicitudAsync(HttpMethod.Get, $"contratos/resumen-dashboard/{Preferences.Default.Get("UserId", 0)}");
            if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0) return;

            // Mismo estilo de título que las demás secciones del inicio ("Próximo trabajo"…).
            destino.Add(new Label
            {
                Text = Localizador.T(cuidador ? "fijos_mis" : "fijos_tu"),
                FontSize = cuidador ? 17 : 15, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong")
            });
            foreach (var row in rows.EnumerateArray())
                destino.Add(Tarjeta(row, cuidador));
        }
        catch
        {
            // Sin conexión o sin datos: el inicio sigue funcionando sin la tarjeta.
            destino.Clear();
        }
    }

    private static View Tarjeta(JsonElement row, bool cuidador)
    {
        string S(string k) => row.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";
        int contrato = row.GetProperty("ContratoId").GetInt32();
        var estado = S("EstadoHoy");
        var nombre = S("NombreCompleto");

        // Estado de hoy: color según lo que significa.
        var (fondo, color) = estado switch
        {
            "trabajando" => ("ColorSuccessSoft", "ColorSuccess"),
            "en_camino" => ("ColorPrimarySoft", "ColorPrimary"),
            "ausencia" or "pausado" => ("ColorWarningSoft", "ColorWarning"),
            "terminado" => ("ColorSubtle", "ColorTextMuted"),
            "programado" => ("ColorPrimarySoft", "ColorPrimary"),
            _ => ("ColorSubtle", "ColorTextMuted")
        };
        var horasHoy = estado is "dia_libre" or "pausado" || string.IsNullOrEmpty(S("HoraInicio")) ? "" : $"{Hora(S("HoraInicio"))} – {Hora(S("HoraFin"))}";
        var pastilla = new Border
        {
            Stroke = Colors.Transparent, BackgroundColor = Tema.C(fondo), StrokeShape = new RoundRectangle { CornerRadius = 9 },
            Padding = new Thickness(8, 3), HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center,
            Content = new Label { Text = Localizador.T("fijos_" + estado), FontSize = 11.5, FontFamily = "OpenSansSemibold", TextColor = Tema.C(color) }
        };
        var lineaHoy = new HorizontalStackLayout { Spacing = 8, Children = { pastilla } };
        if (horasHoy.Length > 0)
            lineaHoy.Add(new Label { Text = horasHoy, FontSize = 13, TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center });

        var textos = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        textos.Add(new Label { Text = nombre, FontSize = 16, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong"), LineBreakMode = LineBreakMode.TailTruncation });
        textos.Add(lineaHoy);
        var fila = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 14
        };
        fila.Add(Avatar(S("FotoUrl"), nombre), 0);
        fila.Add(textos, 1);
        fila.Add(new Microsoft.Maui.Controls.Shapes.Path
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M10 6L8.59 7.41L13.17 12L8.59 16.59L10 18L16 12L10 6Z")!,
            Fill = Tema.C("ColorChevron"), Aspect = Stretch.Uniform, WidthRequest = 18, HeightRequest = 18, VerticalOptions = LayoutOptions.Center
        }, 2);

        var contenido = new VerticalStackLayout { Spacing = 12, Children = { fila } };

        // Próximo turno en su propia franja (antes se cortaba con "…").
        var proxima = Proxima(S("ProximaFecha"), S("ProximaHora"), S("ProximaFin"));
        if (proxima.Length > 0)
        {
            var franja = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 8 };
            franja.Add(new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M19 4H18V2H16V4H8V2H6V4H5C3.89 4 3 4.9 3 6V20C3 21.1 3.89 22 5 22H19C20.1 22 21 21.1 21 20V6C21 4.9 20.1 4 19 4ZM19 20H5V10H19V20Z")!,
                Fill = Tema.C("ColorTextMuted"), Aspect = Stretch.Uniform, WidthRequest = 14, HeightRequest = 14, VerticalOptions = LayoutOptions.Center
            }, 0);
            franja.Add(new Label { Text = proxima, FontSize = 12.5, TextColor = Tema.C("ColorTextMuted"), VerticalOptions = LayoutOptions.Center }, 1);
            contenido.Add(new Border
            {
                Stroke = Colors.Transparent, BackgroundColor = Tema.C("ColorSubtle"), StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(12, 9), Content = franja
            });
        }

        // Care Partner: acceso directo a la jornada de hoy (el flujo de PIN/geocerca no cambia).
        if (cuidador && int.TryParse(S("TrabajoId"), out int trabajo) && estado is "programado" or "en_camino" or "trabajando")
        {
            var accion = new Border
            {
                Stroke = Colors.Transparent, BackgroundColor = Tema.C("ColorPrimary"), StrokeShape = new RoundRectangle { CornerRadius = 12 },
                HeightRequest = 44,
                Content = new Label
                {
                    Text = Localizador.T(estado == "trabajando" ? "fijos_finalizar" : "fijos_iniciar"), FontSize = 14, FontFamily = "OpenSansSemibold",
                    TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                }
            };
            accion.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () =>
                {
                    var turno = (await new ApiService().ObtenerTrabajosAsync(Preferences.Default.Get("UserId", 0))).FirstOrDefault(t => t.Id == trabajo);
                    if (turno != null) await Shell.Current.GoToAsync("DetalleTrabajoPage", new Dictionary<string, object> { { "Trabajo", turno } });
                })
            });
            contenido.Add(accion);
        }

        // Igual que las demás tarjetas del inicio: superficie, sin borde, sombra suave.
        var tarjeta = new Border
        {
            Stroke = Colors.Transparent, BackgroundColor = Tema.C("ColorSurface"),
            StrokeShape = new RoundRectangle { CornerRadius = 22 }, Padding = new Thickness(16),
            Shadow = new Shadow { Brush = new SolidColorBrush(Tema.C("ColorShadow")), Offset = new Point(0, 3), Radius = 12, Opacity = 0.08f },
            Content = contenido
        };
        SemanticProperties.SetDescription(tarjeta, $"{Localizador.T("fijos_ver")}: {nombre}");
        tarjeta.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await Shell.Current.GoToAsync("ContratoDetallePage", new Dictionary<string, object> { { "ContratoId", contrato } }))
        });
        return tarjeta;
    }

    private static View Avatar(string foto, string nombre)
    {
        View contenido;
        // La API manda la foto como "/uploads/...": sin el servidor delante no carga (salía un círculo vacío).
        if (foto.StartsWith("/")) foto = ApiService.ServerOrigin + foto;
        if (foto.Length > 0)
            contenido = new Image { Source = foto, Aspect = Aspect.AspectFill };
        else
        {
            var iniciales = string.Concat(nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])));
            contenido = new Label { Text = iniciales, FontSize = 16, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorPrimary"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        }
        return new Border
        {
            WidthRequest = 52, HeightRequest = 52, Stroke = Colors.Transparent, BackgroundColor = Tema.C("ColorPrimarySoft"),
            StrokeShape = new RoundRectangle { CornerRadius = 26 }, VerticalOptions = LayoutOptions.Center, Content = contenido
        };
    }

    private static string Hora(string hhmm)
        => TimeSpan.TryParse(hhmm, out var t) ? DateTime.Today.Add(t).ToString("h:mm tt", Localizador.Cultura) : hhmm;

    /// <summary>"Próximo turno: dom. 15 nov. 8:00 a. m. – 2:00 p. m." (vacío si no hay).</summary>
    private static string Proxima(string fecha, string ini, string fin)
    {
        if (!DateTime.TryParse(fecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) || string.IsNullOrEmpty(ini)) return "";
        var dia = f.ToString("ddd d MMM", Localizador.Cultura);
        return Localizador.F("fijos_proximo", dia, Hora(ini), Hora(fin));
    }
}
