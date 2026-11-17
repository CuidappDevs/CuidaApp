using System.Globalization;
using System.Text;
using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models.Contrato;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;

namespace CUIDAPP.Views.Contratos
{
    /// <summary>
    /// Firma electrónica simple del contrato: la persona acepta las condiciones, recibe un código de un
    /// solo uso en su correo y dibuja su firma con el dedo. El servidor guarda la firma, la hora, la IP y
    /// la ubicación; cuando firman los dos genera el PDF. Parámetro: "Detalle" (ContratoDetalle).
    /// </summary>
    public partial class FirmarContratoPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _api = new();
        private int usuarioId;
        private ContratoDetalle? d;
        private bool pintado;
        private int? anexoId;

        private readonly CheckBox chkAcepto = new() { VerticalOptions = LayoutOptions.Center };
        private readonly Entry txtCodigo = new() { Keyboard = Keyboard.Numeric, MaxLength = 6, Placeholder = "000000", HorizontalTextAlignment = TextAlignment.Center, CharacterSpacing = 6 };
        private readonly Label lblCodigo = Texto(Localizador.T("firmar_codigo_texto"), true, 13);
        private readonly PanelFirma panel = new();
        private readonly GraphicsView lienzo;

        public FirmarContratoPage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            chkAcepto.Color = Tema.C("ColorPrimary");

            lienzo = new GraphicsView { Drawable = panel, HeightRequest = 180, BackgroundColor = Tema.C("ColorSurface") };
            // Mientras se firma, la página no se desplaza (si no, el trazo mueve la pantalla).
            lienzo.StartInteraction += (_, e) => { Desplazamiento.Orientation = ScrollOrientation.Neither; panel.Empezar(e.Touches[0]); lienzo.Invalidate(); };
            lienzo.DragInteraction += (_, e) => { panel.Seguir(e.Touches[0]); lienzo.Invalidate(); };
            lienzo.EndInteraction += (_, _) => Desplazamiento.Orientation = ScrollOrientation.Vertical;
            lienzo.CancelInteraction += (_, _) => Desplazamiento.Orientation = ScrollOrientation.Vertical;
#if ANDROID
            // En Android el ScrollView nativo captura el gesto antes de que llegue StartInteraction:
            // desde el primer toque se le pide a todos los contenedores que no lo intercepten.
            lienzo.HandlerChanged += (_, _) =>
            {
                if (lienzo.Handler?.PlatformView is not Android.Views.View vista) return;
                vista.Touch += (_, e) =>
                {
                    var accion = e.Event?.ActionMasked;
                    if (accion is Android.Views.MotionEventActions.Down or Android.Views.MotionEventActions.Move)
                        vista.Parent?.RequestDisallowInterceptTouchEvent(true);
                    else if (accion is Android.Views.MotionEventActions.Up or Android.Views.MotionEventActions.Cancel)
                        vista.Parent?.RequestDisallowInterceptTouchEvent(false);
                    e.Handled = false; // el trazo lo sigue dibujando el GraphicsView
                };
            };
#endif
            SemanticProperties.SetDescription(lienzo, Localizador.T("firmar_dibuja"));
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("AnexoId", out var aid) && int.TryParse(aid?.ToString(), out var numero)) anexoId = numero;
            if (query.TryGetValue("Detalle", out var v) && v is ContratoDetalle cd) d = cd;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            usuarioId = Preferences.Default.Get("UserId", 0);
            if (pintado || d?.Contrato is not { } c) return;
            pintado = true;

            var soyCliente = c.MiRol == "cliente";
            LblSubtitulo.Text = Localizador.F("firmar_subtitulo_con", c.Id.ToString("D5"), soyCliente ? c.CuidadorNombre : c.ClienteNombre);

            var horas = HorasSemana(d.Horario);
            var resumen = new List<View>
            {
                Titulo(Localizador.T(anexoId.HasValue ? "anexos" : "firmar_resumen")),
                Fila(Localizador.T("contrato_tarifa"), Localizador.F("contrato_tarifa_hora", Monto(c.TarifaHora))),
                Fila(Localizador.T("contrato_semana"), Localizador.F("contrato_semana_valor", horas.ToString("0.#", Localizador.Cultura), Monto(c.TarifaHora * (decimal)horas))),
                Fila(Localizador.T("contrato_inicio"), Localizador.FechaLarga(c.FechaInicio)),
                Fila(Localizador.T("contrato_domicilio"), c.DomicilioNombre),
            };
            foreach (var f in Ordenar(d.Horario)) resumen.Add(Bloque(Texto(Franja(f), false, 13)));
            resumen.Add(Separador());
            resumen.Add(Texto(anexoId.HasValue ? Localizador.T("anexo_firma_condiciones") : c.TrabajaDesde is DateTime desde
                ? Localizador.F("firmar_condiciones_antiguedad", c.DiasPreaviso, Localizador.FechaLarga(desde))
                : Localizador.F("firmar_condiciones", c.DiasPreaviso), true, 12.5));
            var acepto = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8 };
            acepto.Add(chkAcepto, 0);
            acepto.Add(Texto(Localizador.T("firmar_acepto"), false, 13.5).Centrado(), 1);
            acepto.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => chkAcepto.IsChecked = !chkAcepto.IsChecked) });
            resumen.Add(acepto);
            Contenido.Add(Tarjeta(resumen.ToArray()));

            Contenido.Add(Tarjeta(
                Titulo(Localizador.T("firmar_paso_codigo")),
                lblCodigo,
                Boton(Localizador.T("firmar_enviar_codigo"), TipoBoton.Secundario, EnviarCodigoAsync),
                CampoTexto(txtCodigo)));

            var borrar = Texto(Localizador.T("firmar_borrar"), false, 13).ConColor("ColorPrimary");
            borrar.HorizontalOptions = LayoutOptions.End;
            borrar.Padding = new Thickness(8, 4);
            borrar.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => { panel.Borrar(); lienzo.Invalidate(); }) });
            Contenido.Add(Tarjeta(
                Titulo(Localizador.T("firmar_paso_firma")),
                Texto(Localizador.T("firmar_dibuja"), true, 13),
                new Border { Stroke = Tema.C("ColorBorder"), StrokeThickness = 1.5, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 }, Content = lienzo },
                borrar,
                Boton(Localizador.T("firmar_boton"), TipoBoton.Principal, FirmarAsync),
                Texto(Localizador.T("firmar_nota_legal"), true, 12)));
            Contenido.Add(new ContentView { HeightRequest = 24 + BarraEstado.AltoInferior() });

            Cargando.IsVisible = Cargando.IsRunning = false;
            Desplazamiento.IsVisible = true;
        }

        private async Task EnviarCodigoAsync()
        {
            var (ok, correo, error) = await _api.EnviarCodigoFirmaAsync(d!.Contrato!.Id, usuarioId, anexoId);
            if (!ok) { await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok")); return; }
            lblCodigo.Text = Localizador.F("firmar_codigo_enviado", correo ?? "");
            txtCodigo.Focus();
        }

        private async Task FirmarAsync()
        {
            if (!chkAcepto.IsChecked) { await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("firmar_falta_aceptar"), Localizador.T("ok")); return; }
            var codigo = txtCodigo.Text?.Trim() ?? "";
            if (codigo.Length != 6) { await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("firmar_falta_codigo"), Localizador.T("ok")); return; }
            if (!panel.TieneFirma) { await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("contrato_err_firma_vacia"), Localizador.T("ok")); return; }

            Location? ubicacion = null;
            try { ubicacion = await Geolocation.Default.GetLastKnownLocationAsync(); } catch { /* opcional */ }

            var (ok, activado, error) = await _api.FirmarContratoAsync(d!.Contrato!.Id, new FirmarContratoRequest
            {
                UsuarioId = usuarioId, Codigo = codigo, Trazos = panel.Trazos(),
                Ancho = Math.Max(lienzo.Width, 10), Alto = Math.Max(lienzo.Height, 10),
                Latitud = ubicacion?.Latitude, Longitud = ubicacion?.Longitude
            });
            if (!ok) { await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok")); return; }

            await Alerta.MostrarAsync(Localizador.T(activado ? "firmar_listo_activo_titulo" : "firmar_listo_titulo"),
                Localizador.T(activado ? "firmar_listo_activo_texto" : "firmar_listo_texto"), Localizador.T("entendido"), TipoAlerta.Exito);
            await Shell.Current.GoToAsync("..");
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

        /// <summary>Guarda los trazos del dedo y los dibuja; los exporta como datos de un path SVG (M/L).</summary>
        private sealed class PanelFirma : IDrawable
        {
            private readonly List<List<PointF>> trazos = new();

            public bool TieneFirma => trazos.Sum(t => t.Count) >= 6;

            public void Empezar(PointF p) => trazos.Add(new List<PointF> { p });
            public void Seguir(PointF p)
            {
                if (trazos.Count == 0) Empezar(p);
                var ultimo = trazos[^1];
                if (ultimo.Count == 0 || ultimo[^1].Distance(p) >= 1.5f) ultimo.Add(p);
            }
            public void Borrar() => trazos.Clear();

            public string Trazos()
            {
                var sb = new StringBuilder();
                foreach (var t in trazos.Where(t => t.Count > 1))
                {
                    sb.Append('M').Append(N(t[0].X)).Append(' ').Append(N(t[0].Y));
                    foreach (var p in t.Skip(1)) sb.Append(" L").Append(N(p.X)).Append(' ').Append(N(p.Y));
                    sb.Append(' ');
                }
                return sb.ToString().Trim();
            }

            private static string N(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

            public void Draw(ICanvas canvas, RectF rect)
            {
                canvas.StrokeColor = Tema.C("ColorTextStrong");
                canvas.StrokeSize = 3;
                canvas.StrokeLineCap = LineCap.Round;
                canvas.StrokeLineJoin = LineJoin.Round;
                foreach (var t in trazos)
                {
                    if (t.Count < 2) continue;
                    var path = new PathF();
                    path.MoveTo(t[0]);
                    foreach (var p in t.Skip(1)) path.LineTo(p);
                    canvas.DrawPath(path);
                }
                if (trazos.Count == 0)
                {
                    // Línea guía.
                    canvas.StrokeColor = Tema.C("ColorBorder");
                    canvas.StrokeSize = 1;
                    canvas.DrawLine(24, rect.Height - 36, rect.Width - 24, rect.Height - 36);
                }
            }
        }
    }
}
