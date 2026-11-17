using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models.Contrato;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;

namespace CUIDAPP.Views.Contratos
{
    /// <summary>
    /// Detalle de un contrato fijo con las acciones que tocan según su estado y quién lo mira:
    /// responder la propuesta, firmar, ver el PDF, marcar ausencias, pausar, reanudar o terminar.
    /// Parámetro: "ContratoId".
    /// </summary>
    public partial class ContratoDetallePage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _api = new();
        private int usuarioId, contratoId;
        private ContratoDetalle? d;
        private List<RelevoContrato> relevos = new();

        private readonly DatePicker dpAusencia = new() { Format = "D" };
        private readonly Switch emergencia = new();
        private readonly Entry txtAusencia = new() { MaxLength = 300 };
        private readonly Editor txtTerminar = new() { MaxLength = 600, HeightRequest = 80 };

        public ContratoDetallePage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            dpAusencia.MinimumDate = ServerClock.Today;
            dpAusencia.TextColor = Tema.C("ColorTextStrong");
            txtAusencia.Placeholder = Localizador.T("ausencia_motivo_placeholder");
            txtTerminar.Placeholder = Localizador.T("terminar_motivo_placeholder");
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("ContratoId", out var v) && int.TryParse(v?.ToString(), out var id)) contratoId = id;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await Task.Yield();
            await ServerClock.SincronizarAsync();
            dpAusencia.MinimumDate = ServerClock.Today;
            BarraEstado.Azul();
            usuarioId = Preferences.Default.Get("UserId", 0);
            RealtimeService.ContratoActualizado += AlCambiar;
            await CargarAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            RealtimeService.ContratoActualizado -= AlCambiar;
        }

        private void AlCambiar(int id, string? estado)
        {
            if (id == contratoId || id == 0) MainThread.BeginInvokeOnMainThread(async () => await CargarAsync());
        }

        private bool SoyCliente => d?.Contrato?.MiRol == "cliente";

        private readonly SemaphoreSlim carga = new(1, 1);
        private async Task CargarAsync()
        {
            if (!await carga.WaitAsync(0)) return;
            try { await CargarContenidoAsync(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("contrato_err_generico"), Localizador.T("ok"));
            }
            finally { carga.Release(); }
        }

        private async Task CargarContenidoAsync()
        {
            var detalleTarea = _api.ObtenerContratoAsync(contratoId, usuarioId);
            var relevosTarea = _api.RelevosContratoAsync(contratoId, usuarioId);
            d = await detalleTarea;
            relevos = await relevosTarea ?? new();
            Contenido.Clear();
            if (d?.Contrato is not { } c)
            {
                Contenido.Add(Tarjeta(Texto(Localizador.T("contrato_no_encontrado"))));
            }
            else
            {
                LblTitulo.Text = Localizador.F("contrato_numero", c.Id.ToString("D5"));
                LblSubtitulo.Text = SoyCliente ? Localizador.F("contrato_con", c.CuidadorNombre) : Localizador.F("contrato_con", c.ClienteNombre);
                Pintar(c);
                Contenido.Add(Tarjeta(Boton(Localizador.T("anexos"), TipoBoton.Secundario, () => Shell.Current.GoToAsync("ContratoAnexosPage", new Dictionary<string, object> { { "ContratoId", contratoId } }))));
                Contenido.Add(Tarjeta(Boton(Localizador.T("contrato_resumen"), TipoBoton.Secundario, () => Shell.Current.GoToAsync("ContratoAsistenciaPage", new Dictionary<string, object> { { "ContratoId", contratoId } }))));
            }
            Contenido.Add(new ContentView { HeightRequest = 24 + BarraEstado.AltoInferior() });
            Cargando.IsVisible = Cargando.IsRunning = false;
            Desplazamiento.IsVisible = true;
        }

        private void Pintar(ContratoCabecera c)
        {
            // ---------- Estado y lo que toca hacer
            var acciones = new List<View> { Pastilla(Estado(c.Estado, c.MiRol), ColoresEstado(c.Estado)) };
            var meToca = (c.Estado == 1 && !SoyCliente) || (c.Estado == 2 && SoyCliente);
            var yoFirme = d!.Firmas.Any(f => f.UsuarioId == usuarioId);

            switch (c.Estado)
            {
                case 1 or 2 when meToca:
                    acciones.Add(Texto(Localizador.T("contrato_responder_texto"), true));
                    acciones.Add(Boton(Localizador.T("contrato_aceptar"), TipoBoton.Principal, () => ResponderAsync("aceptar")));
                    acciones.Add(Boton(Localizador.T("contrato_contraproponer"), TipoBoton.Secundario,
                        () => Shell.Current.GoToAsync("ProponerContratoPage", new Dictionary<string, object> { { "Detalle", d! } })));
                    acciones.Add(Boton(Localizador.T("contrato_rechazar"), TipoBoton.Peligro, () => ResponderAsync("rechazar")));
                    break;
                case 1 or 2:
                    acciones.Add(Texto(Localizador.T("contrato_esperando_otro"), true));
                    if (SoyCliente)
                        acciones.Add(Boton(Localizador.T("contrato_retirar"), TipoBoton.Peligro, () => ResponderAsync("retirar")));
                    break;
                case 3:
                    acciones.Add(Texto(Localizador.T(yoFirme ? "contrato_esperando_firma_otro" : "contrato_firmar_texto"), true));
                    if (!yoFirme)
                        acciones.Add(Boton(Localizador.T("contrato_firmar"), TipoBoton.Principal,
                            () => Shell.Current.GoToAsync("FirmarContratoPage", new Dictionary<string, object> { { "Detalle", d! } })));
                    break;
                case 4 or 5 when c.FechaTerminacion != null && c.FechaFin is DateTime fin:
                    acciones.Add(Texto(Localizador.F("contrato_termina_el", Localizador.FechaLarga(fin)), true));
                    break;
                case 6 or 7 or 8 or 9 when !string.IsNullOrWhiteSpace(c.MotivoTerminacion):
                    acciones.Add(Texto(c.MotivoTerminacion!, true));
                    break;
            }
            if (d.Documentos.Count > 0)
                acciones.Add(Boton(Localizador.T("contrato_ver_pdf"), TipoBoton.Secundario, () => Shell.Current.GoToAsync("ContratoDocumentosPage", new Dictionary<string, object> { { "ContratoId", contratoId } })));
            Contenido.Add(Tarjeta(acciones.ToArray()));

            foreach (var vinculo in new[] { (c.ContratoAnteriorId, "contrato_anterior"), (c.ContratoRenovadoId, "contrato_renovado") })
                if (vinculo.Item1 is int vinculado)
                    Contenido.Add(Tarjeta(Boton(Localizador.F(vinculo.Item2, vinculado.ToString("D5")), TipoBoton.Secundario,
                        () => Shell.Current.GoToAsync("ContratoDetallePage", new Dictionary<string, object> { { "ContratoId", vinculado } }))));

            // ---------- Condiciones
            var horas = HorasSemana(d.Horario);
            var condiciones = new List<View>
            {
                Titulo(Localizador.T("proponer_condiciones")),
                Fila(Localizador.T("contrato_servicio"), Localizador.D(c.TipoServicio)),
                Fila(Localizador.T("contrato_domicilio"), $"{c.DomicilioNombre} · {c.Direccion}"),
            };
            if (!string.IsNullOrWhiteSpace(c.QuienVive)) condiciones.Add(Fila(Localizador.T("contrato_persona"), c.QuienVive!));
            condiciones.Add(Fila(Localizador.T("contrato_tarifa"), Localizador.F("contrato_tarifa_hora", Monto(c.TarifaHora))));
            condiciones.Add(Fila(Localizador.T("contrato_semana"), Localizador.F("contrato_semana_valor", horas.ToString("0.#", Localizador.Cultura), Monto(c.TarifaHora * (decimal)horas))));
            condiciones.Add(Fila(Localizador.T("contrato_pago"), Localizador.T(c.MetodoPago == "efectivo" ? "pago_efectivo" : "pago_transferencia")));
            if (c.Dormida) condiciones.Add(Fila(Localizador.T("proponer_modalidad"), Localizador.T("dormida_valor")));
            condiciones.Add(Fila(Localizador.T("contrato_inicio"), Localizador.FechaLarga(c.FechaInicio)));
            condiciones.Add(Fila(Localizador.T("contrato_fin"), c.FechaFin is DateTime ff ? Localizador.FechaLarga(ff) : Localizador.T("contrato_sin_fin")));
            condiciones.Add(Fila(Localizador.T("contrato_preaviso"), Localizador.F("contrato_dias", c.DiasPreaviso)));
            condiciones.Add(Separador());
            condiciones.Add(Titulo(Localizador.T("proponer_horario"), 15));
            foreach (var f in Ordenar(d.Horario)) condiciones.Add(Texto(Franja(f)));
            if (!string.IsNullOrWhiteSpace(c.Nota))
            {
                condiciones.Add(Separador());
                condiciones.Add(Texto("“" + c.Nota + "”", true));
            }
            Contenido.Add(Tarjeta(condiciones.ToArray()));

            var proximas = d.Trabajos.Where(t => t.Fecha.Date >= ServerClock.Today).OrderBy(t => t.Fecha).ThenBy(t => t.HoraInicio).Take(3);
            var resumenJornadas = new List<View> { Titulo(Localizador.T("contrato_proximas_jornadas")) };
            foreach(var t in proximas) resumenJornadas.Add(Fila(Localizador.FechaLarga(t.Fecha), $"{Hora(t.HoraInicio)} – {Hora(t.HoraFin)}"));
            Contenido.Add(Tarjeta(resumenJornadas.ToArray()));
            PintarChecklist(c);
            PintarRelevos();
            // ---------- Activo / en pausa: jornadas, ausencias y control
            if (c.Estado is 4 or 5)
            {
                Contenido.Add(Tarjeta(Boton(Localizador.T("contrato_ausencias"), TipoBoton.Secundario, () => Shell.Current.GoToAsync("ContratoJornadasPage", new Dictionary<string, object> { { "ContratoId", contratoId } }))));
                var control = new List<View> { Titulo(Localizador.T("contrato_control")) };
                if (c.Estado == 4 && c.FechaTerminacion == null)
                    control.Add(Boton(Localizador.T("contrato_pausar"), TipoBoton.Secundario, () => CambiarEstadoAsync("pausar")));
                if (c.Estado == 5)
                    control.Add(Boton(Localizador.T("contrato_reanudar"), TipoBoton.Principal, () => CambiarEstadoAsync("reanudar")));
                if (c.FechaTerminacion == null)
                {
                    control.Add(Texto(Localizador.F("contrato_terminar_texto", c.DiasPreaviso), true, 13));
                    control.Add(Texto(Localizador.T("terminar_motivo_placeholder"), true));
                    control.Add(CampoTexto(txtTerminar, 80));
                    control.Add(Boton(Localizador.T("contrato_terminar"), TipoBoton.Peligro, () => CambiarEstadoAsync("terminar")));
                }
                Contenido.Add(Tarjeta(control.ToArray()));
            }

            if (SoyCliente && c.Estado is 4 or 5 && c.FechaFin is DateTime finRenovacion)
            {
                var nuevoFin = new DatePicker { MinimumDate = finRenovacion.AddDays(7), Date = finRenovacion.AddMonths(1), TextColor = Tema.C("ColorTextStrong") };
                Contenido.Add(Tarjeta(Titulo(Localizador.T("contrato_renovar")),
                    Texto(Localizador.T("contrato_fin"), true), CampoTexto(nuevoFin),
                    Boton(Localizador.T("contrato_renovar"), TipoBoton.Principal, async () =>
                    {
                        var (ok, error) = await _api.RenovarContratoAsync(contratoId, usuarioId, nuevoFin.Date ?? finRenovacion.AddMonths(1));
                        await Alerta.MostrarAsync(Localizador.T(ok ? "contrato_renovar" : "error"),
                            ok ? Localizador.T("contrato_renovacion_creada") : error ?? "", Localizador.T("ok"));
                    })));
            }

            // ---------- Firmas
            if (c.Estado >= 3 && d.Firmas.Count > 0)
            {
                var firmas = new List<View> { Titulo(Localizador.T("contrato_firmas")) };
                foreach (var f in d.Firmas) firmas.Add(Fila(f.Nombre, Localizador.F("fecha_y_hora", Localizador.DiaMes(f.Fecha), f.Fecha.ToString("h:mm tt", Localizador.Cultura))));
                Contenido.Add(Tarjeta(firmas.ToArray()));
            }
        }

        // ---------- Checklist diario: el cliente lo edita mientras el contrato está activo; la Care Partner lo ve.
        private void PintarChecklist(ContratoCabecera c)
        {
            var editable = SoyCliente && c.Estado is 4 or 5 && c.FechaTerminacion == null;
            if (!editable && d!.Tareas.Count == 0) return;

            var vistas = new List<View> { Titulo(Localizador.T("checklist_titulo")) };
            if (editable)
            {
                var editor = new EditorTareasContrato(d!.Tareas.OrderBy(t => t.Orden));
                vistas.Add(Texto(Localizador.T("checklist_editar_explicacion"), true, 12.5));
                vistas.Add(editor);
                vistas.Add(Boton(Localizador.T("checklist_guardar"), TipoBoton.Principal, async () =>
                {
                    var (ok, error) = await _api.GuardarTareasContratoAsync(contratoId, usuarioId, editor.Tareas());
                    await Alerta.MostrarAsync(Localizador.T(ok ? "checklist_titulo" : "error"),
                        ok ? Localizador.T("checklist_guardado") : error ?? Localizador.T("contrato_err_generico"), Localizador.T("ok"),
                        ok ? TipoAlerta.Exito : TipoAlerta.Error);
                    if (ok) await CargarAsync();
                }));
            }
            else
            {
                vistas.Add(Texto(Localizador.T(SoyCliente ? "checklist_explicacion" : "checklist_cuidador_explicacion"), true, 12.5));
                foreach (var t in d!.Tareas.OrderBy(t => t.Orden))
                    vistas.Add(FilaTarea(t));
            }
            Contenido.Add(Tarjeta(vistas.ToArray()));
        }

        private static View FilaTarea(TareaContrato t)
        {
            var fila = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10 };
            fila.Add(new Border
            {
                WidthRequest = 20, HeightRequest = 20, StrokeThickness = 1.5, Stroke = Tema.C("ColorBorder"), BackgroundColor = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 }, VerticalOptions = LayoutOptions.Center
            }, 0);
            fila.Add(Texto(t.Descripcion).Centrado(), 1);
            if (t.RequiereFoto)
                fila.Add(Pastilla(Localizador.T("checklist_foto"), ("ColorPrimarySoft", "ColorPrimary")).Centrado(), 2);
            return fila;
        }

        // ---------- Relevos: jornadas que cubrió otra Care Partner (para volver a pedir a la que gustó).
        private void PintarRelevos()
        {
            if (relevos.Count == 0) return;
            var vistas = new List<View> { Titulo(Localizador.T("relevos_titulo")), Texto(Localizador.T("relevos_explicacion"), true, 12.5) };
            foreach (var r in relevos.OrderByDescending(r => r.Fecha))
            {
                var fila = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 12 };
                fila.Add(Avatar(r.FotoUrl, r.Nombre), 0);
                fila.Add(new VerticalStackLayout
                {
                    Spacing = 2, VerticalOptions = LayoutOptions.Center,
                    Children = { Titulo(r.Nombre, 14.5), Texto(Localizador.FechaLarga(r.Fecha), true, 12.5) }
                }, 1);
                if (r.Calificacion is int estrellas)
                    fila.Add(Pastilla(Localizador.F("relevo_estrellas", estrellas), ("ColorWarningSoft", "ColorWarning")).Centrado(), 2);
                vistas.Add(fila);
            }
            Contenido.Add(Tarjeta(vistas.ToArray()));
        }

        private static View Avatar(string? foto, string nombre)
        {
            var marco = new Border
            {
                WidthRequest = 40, HeightRequest = 40, StrokeThickness = 0, BackgroundColor = Tema.C("ColorPrimarySoft"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(), VerticalOptions = LayoutOptions.Center
            };
            if (!string.IsNullOrWhiteSpace(foto))
                marco.Content = new Image { Source = foto!.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? foto : $"{ApiService.ServerOrigin}{foto}", Aspect = Aspect.AspectFill };
            else
                marco.Content = new Label
                {
                    Text = string.Concat(nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0]))),
                    FontFamily = "OpenSansSemibold", FontSize = 14, TextColor = Tema.C("ColorPrimary"),
                    HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
                };
            return marco;
        }

        private async Task ResponderAsync(string accion)
        {
            var (titulo, texto, peligro) = accion switch
            {
                "aceptar" => ("contrato_aceptar_titulo", "contrato_aceptar_confirmar", false),
                "rechazar" => ("contrato_rechazar_titulo", "contrato_rechazar_confirmar", true),
                _ => ("contrato_retirar_titulo", "contrato_retirar_confirmar", true)
            };
            if (!await Alerta.MostrarAsync(Localizador.T(titulo), Localizador.T(texto), Localizador.T("si_continuar"), Localizador.T("cancelar"), peligro ? TipoAlerta.Advertencia : TipoAlerta.Pregunta))
                return;
            var (ok, error) = await _api.ResponderContratoAsync(contratoId, new ResponderContratoRequest { UsuarioId = usuarioId, Accion = accion });
            if (!ok) { await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok")); return; }
            if (accion == "aceptar" && d != null)
                await Shell.Current.GoToAsync("FirmarContratoPage", new Dictionary<string, object> { { "Detalle", d } });
            else
                await CargarAsync();
        }

        private async Task AbrirPdfAsync()
        {
            var url = await _api.ObtenerEnlacePdfContratoAsync(contratoId, usuarioId);
            if (url == null) { await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("contrato_pdf_error"), Localizador.T("ok")); return; }
            await Launcher.Default.OpenAsync(new Uri(url));
        }

        private async Task CambiarEstadoAsync(string accion)
        {
            var motivo = accion == "terminar" ? txtTerminar.Text?.Trim() : null;
            if (accion == "terminar" && (motivo?.Length ?? 0) < 5)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("contrato_err_motivo_corto"), Localizador.T("ok"));
                return;
            }
            if (!await Alerta.MostrarAsync(Localizador.T($"contrato_{accion}_titulo"), Localizador.T($"contrato_{accion}_confirmar"), Localizador.T("si_continuar"), Localizador.T("cancelar"),
                    accion == "reanudar" ? TipoAlerta.Pregunta : TipoAlerta.Advertencia))
                return;
            var (ok, error, fechaFin) = await _api.CambiarEstadoContratoAsync(contratoId, usuarioId, accion, motivo);
            if (!ok) { await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok")); return; }
            txtTerminar.Text = "";
            await Alerta.MostrarAsync(Localizador.T($"contrato_{accion}_titulo"),
                accion == "terminar" && fechaFin is DateTime fin
                    ? Localizador.F("contrato_termina_el", Localizador.FechaLarga(fin))
                    : Localizador.T("contrato_estado_actualizado"), Localizador.T("ok"), TipoAlerta.Exito);
            await CargarAsync();
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
