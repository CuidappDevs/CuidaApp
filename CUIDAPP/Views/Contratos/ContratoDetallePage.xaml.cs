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

        private readonly DatePicker dpAusencia = new() { Format = "D" };
        private readonly Entry txtAusencia = new() { MaxLength = 300 };
        private readonly Editor txtTerminar = new() { MaxLength = 600, HeightRequest = 80 };

        public ContratoDetallePage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            dpAusencia.MinimumDate = DateTime.Today;
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
            if (id == contratoId || id == 0) _ = CargarAsync();
        }

        private bool SoyCliente => d?.Contrato?.MiRol == "cliente";

        private async Task CargarAsync()
        {
            d = await _api.ObtenerContratoAsync(contratoId, usuarioId);
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
                case 4 when c.FechaTerminacion != null && c.FechaFin is DateTime fin:
                    acciones.Add(Texto(Localizador.F("contrato_termina_el", Localizador.FechaLarga(fin)), true));
                    break;
                case 6 or 7 or 8 or 9 when !string.IsNullOrWhiteSpace(c.MotivoTerminacion):
                    acciones.Add(Texto(c.MotivoTerminacion!, true));
                    break;
            }
            if (d.Documentos.Count > 0)
                acciones.Add(Boton(Localizador.T("contrato_ver_pdf"), TipoBoton.Secundario, AbrirPdfAsync));
            Contenido.Add(Tarjeta(acciones.ToArray()));

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

            // ---------- Activo / en pausa: jornadas, ausencias y control
            if (c.Estado is 4 or 5)
            {
                var proximas = d.Trabajos.Where(t => t.Fecha.Date >= DateTime.Today).OrderBy(t => t.Fecha).Take(7).ToList();
                var jornadas = new List<View> { Titulo(Localizador.T("contrato_proximas_jornadas")) };
                if (proximas.Count == 0) jornadas.Add(Texto(Localizador.T(c.Estado == 5 ? "contrato_pausado_sin_jornadas" : "contrato_sin_jornadas"), true));
                foreach (var t in proximas)
                    jornadas.Add(Fila(Localizador.DiaMes(t.Fecha), t.Estado == 5 ? Localizador.T("contrato_jornada_cancelada") : $"{Hora(t.HoraInicio)} – {Hora(t.HoraFin)}"));
                Contenido.Add(Tarjeta(jornadas.ToArray()));

                var ausencias = new List<View> { Titulo(Localizador.T("contrato_ausencias")), Texto(Localizador.T("contrato_ausencias_texto"), true) };
                foreach (var a in d.Ausencias.Where(a => a.Fecha.Date >= DateTime.Today).OrderBy(a => a.Fecha))
                {
                    var fila = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
                    fila.Add(Texto($"{Localizador.FechaLarga(a.Fecha)} · {a.MarcadoPorNombre}{(string.IsNullOrWhiteSpace(a.Motivo) ? "" : " · " + a.Motivo)}", false, 13).Centrado(), 0);
                    if (a.MarcadoPor == usuarioId && a.Fecha.Date > DateTime.Today)
                    {
                        var quitar = Texto(Localizador.T("quitar"), false, 13).ConColor("ColorDanger");
                        quitar.Padding = new Thickness(8);
                        quitar.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await QuitarAusenciaAsync(a)) });
                        fila.Add(quitar, 1);
                    }
                    ausencias.Add(fila);
                }
                ausencias.Add(CampoTexto(dpAusencia));
                ausencias.Add(CampoTexto(txtAusencia));
                ausencias.Add(Boton(Localizador.T("contrato_marcar_ausencia"), TipoBoton.Secundario, MarcarAusenciaAsync));
                Contenido.Add(Tarjeta(ausencias.ToArray()));

                var control = new List<View> { Titulo(Localizador.T("contrato_control")) };
                if (c.Estado == 4 && c.FechaTerminacion == null)
                    control.Add(Boton(Localizador.T("contrato_pausar"), TipoBoton.Secundario, () => CambiarEstadoAsync("pausar")));
                if (c.Estado == 5)
                    control.Add(Boton(Localizador.T("contrato_reanudar"), TipoBoton.Principal, () => CambiarEstadoAsync("reanudar")));
                if (c.FechaTerminacion == null)
                {
                    control.Add(Texto(Localizador.F("contrato_terminar_texto", c.DiasPreaviso), true, 13));
                    control.Add(CampoTexto(txtTerminar, 80));
                    control.Add(Boton(Localizador.T("contrato_terminar"), TipoBoton.Peligro, () => CambiarEstadoAsync("terminar")));
                }
                Contenido.Add(Tarjeta(control.ToArray()));
            }

            // ---------- Firmas
            if (c.Estado >= 3 && d.Firmas.Count > 0)
            {
                var firmas = new List<View> { Titulo(Localizador.T("contrato_firmas")) };
                foreach (var f in d.Firmas) firmas.Add(Fila(f.Nombre, Localizador.F("fecha_y_hora", Localizador.DiaMes(f.Fecha), f.Fecha.ToString("h:mm tt", Localizador.Cultura))));
                Contenido.Add(Tarjeta(firmas.ToArray()));
            }
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

        private async Task MarcarAusenciaAsync()
        {
            var fecha = (dpAusencia.Date ?? DateTime.Today).Date;
            if (!await Alerta.MostrarAsync(Localizador.T("contrato_marcar_ausencia"), Localizador.F("ausencia_confirmar", Localizador.FechaLarga(fecha)), Localizador.T("si_continuar"), Localizador.T("cancelar"), TipoAlerta.Pregunta))
                return;
            var (ok, error) = await _api.MarcarAusenciaContratoAsync(contratoId, usuarioId, fecha, string.IsNullOrWhiteSpace(txtAusencia.Text) ? null : txtAusencia.Text.Trim());
            if (!ok) { await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok")); return; }
            txtAusencia.Text = "";
            await CargarAsync();
        }

        private async Task QuitarAusenciaAsync(AusenciaContrato a)
        {
            var (ok, error) = await _api.QuitarAusenciaContratoAsync(a.Id, usuarioId);
            if (!ok) { await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok")); return; }
            await CargarAsync();
        }

        private async Task CambiarEstadoAsync(string accion)
        {
            var motivo = accion == "terminar" ? txtTerminar.Text?.Trim() : null;
            if (accion == "terminar" && (motivo?.Length ?? 0) < 5)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("terminar_motivo_corto"), Localizador.T("ok"));
                return;
            }
            if (!await Alerta.MostrarAsync(Localizador.T($"contrato_{accion}_titulo"), Localizador.T($"contrato_{accion}_confirmar"), Localizador.T("si_continuar"), Localizador.T("cancelar"),
                    accion == "reanudar" ? TipoAlerta.Pregunta : TipoAlerta.Advertencia))
                return;
            var (ok, error) = await _api.CambiarEstadoContratoAsync(contratoId, usuarioId, accion, motivo);
            if (!ok) { await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok")); return; }
            txtTerminar.Text = "";
            await CargarAsync();
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
