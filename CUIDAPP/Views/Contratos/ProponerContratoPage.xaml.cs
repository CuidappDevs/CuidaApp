using System.Globalization;
using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models.Busqueda;
using CUIDAPP.Models.Contrato;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;

namespace CUIDAPP.Views.Contratos
{
    /// <summary>
    /// Proponer un contrato fijo (cliente, desde el perfil del Care Partner) o contraproponer
    /// (cualquiera de las dos partes, desde el detalle): tarifa, inicio, horario semanal y nota.
    /// Parámetros: "Cuidador" (CuidadorCercano) para proponer; "Detalle" (ContratoDetalle) para contraproponer.
    /// </summary>
    public partial class ProponerContratoPage : ContentPage, IQueryAttributable
    {
        private readonly ApiService _api = new();
        private int usuarioId;
        private CuidadorCercano? cuidador;
        private ContratoDetalle? detalle;
        private bool cargado;

        private static readonly int[] OrdenDias = { 1, 2, 3, 4, 5, 6, 0 };
        private readonly Dictionary<int, (Switch Activo, TimePicker Inicio, TimePicker Fin)> dias = new();

        private readonly Picker pkDomicilio = new() { Title = Localizador.T("proponer_domicilio") };
        private readonly Picker pkMetodo = new() { Title = Localizador.T("proponer_metodo_pago") };
        private readonly Entry txtTarifa = new() { Keyboard = Keyboard.Numeric, MaxLength = 7 };
        private readonly DatePicker dpInicio = new() { Format = "D" };
        private readonly Switch swFin = new();
        private readonly DatePicker dpFin = new() { Format = "D", IsVisible = false };
        private readonly Editor txtNota = new() { MaxLength = 600, HeightRequest = 90 };
        private readonly Label lblResumen = Texto("", false, 14);
        private List<DomicilioAprobado> domicilios = new();

        public ProponerContratoPage()
        {
            InitializeComponent();
            ContenidoEncabezado.Margin = new Thickness(0, BarraEstado.Alto(), 0, 0);
            var manana = DateTime.Today.AddDays(1);
            dpInicio.MinimumDate = manana;
            dpInicio.Date = manana;
            dpFin.MinimumDate = manana.AddDays(7);
            dpFin.Date = manana.AddMonths(3);
            swFin.OnColor = Tema.C("ColorPrimary");
            swFin.Toggled += (_, e) => dpFin.IsVisible = e.Value;
            foreach (var p in new View[] { pkDomicilio, pkMetodo, dpInicio, dpFin })
                if (p is Picker pk) { pk.TextColor = Tema.C("ColorTextStrong"); pk.TitleColor = Tema.C("ColorPlaceholder"); }
                else if (p is DatePicker d) d.TextColor = Tema.C("ColorTextStrong");
            pkMetodo.ItemsSource = new List<string> { Localizador.T("pago_transferencia"), Localizador.T("pago_efectivo") };
            pkMetodo.SelectedIndex = 0;
            txtTarifa.TextChanged += (_, _) => ActualizarResumen();
            txtNota.Placeholder = Localizador.T("proponer_nota_placeholder");
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("Cuidador", out var c) && c is CuidadorCercano cc) cuidador = cc;
            if (query.TryGetValue("Detalle", out var d) && d is ContratoDetalle cd) detalle = cd;
        }

        private bool EsContraoferta => detalle?.Contrato != null;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            BarraEstado.Azul();
            usuarioId = Preferences.Default.Get("UserId", 0);
            if (cargado) return;
            cargado = true;

            if (EsContraoferta)
            {
                LblTitulo.Text = Localizador.T("contraproponer_titulo");
                LblSubtitulo.Text = Localizador.T("contraproponer_subtitulo");
                PintarFormulario();
            }
            else if (cuidador != null)
            {
                LblSubtitulo.Text = Localizador.F("proponer_subtitulo_con", cuidador.NombreCompleto);
                var info = await _api.PuedeProponerContratoAsync(usuarioId, cuidador.Id);
                domicilios = info?.Domicilios ?? new();
                if (info?.Estado is not { } e)
                    PintarAviso(Localizador.T("contrato_err_generico"), null);
                else if (!e.AceptaContratos)
                    PintarAviso(Localizador.T("proponer_cuidador_no_acepta"), null);
                else if (e.PermisoEstado != 2)
                    PintarAviso(Localizador.T("proponer_sin_permiso"), Localizador.T("permiso_gestionar"));
                else if (domicilios.Count == 0)
                    PintarAviso(Localizador.T("proponer_sin_domicilio"), Localizador.T("permiso_gestionar"));
                else
                    PintarFormulario();
            }

            Cargando.IsVisible = Cargando.IsRunning = false;
            Desplazamiento.IsVisible = true;
        }

        private void PintarAviso(string texto, string? boton)
        {
            Contenido.Clear();
            var vistas = new List<View> { Texto(texto) };
            if (boton != null)
                vistas.Add(Boton(boton, TipoBoton.Principal, () => Shell.Current.GoToAsync("PermisoContratarPage")));
            Contenido.Add(Tarjeta(vistas.ToArray()));
        }

        private void PintarFormulario()
        {
            Contenido.Clear();
            var c = detalle?.Contrato;

            // Valores iniciales
            txtTarifa.Text = (c?.TarifaHora ?? cuidador?.TarifaHora ?? 0).ToString("0", CultureInfo.InvariantCulture);
            if (c != null && c.FechaInicio.Date > DateTime.Today) dpInicio.Date = c.FechaInicio.Date;
            if (c != null) txtNota.Text = "";

            // Condiciones
            var condiciones = new List<View> { Titulo(Localizador.T("proponer_condiciones")) };
            if (!EsContraoferta)
            {
                pkDomicilio.ItemsSource = domicilios.Select(d => $"{d.Nombre} · {d.Direccion}").ToList();
                pkDomicilio.SelectedIndex = domicilios.Count == 1 ? 0 : -1;
                condiciones.Add(Texto(Localizador.F("proponer_servicio", Localizador.D(cuidador?.Especialidad)), true));
                condiciones.Add(Etiqueta("proponer_domicilio"));
                condiciones.Add(CampoTexto(pkDomicilio));
            }
            condiciones.Add(Etiqueta("proponer_tarifa"));
            condiciones.Add(CampoTexto(txtTarifa));
            if (cuidador != null)
                condiciones.Add(Texto(Localizador.F("proponer_tarifa_referencia", Monto(cuidador.TarifaHora)), true, 12.5));
            condiciones.Add(Etiqueta("proponer_inicio"));
            condiciones.Add(CampoTexto(dpInicio));
            if (!EsContraoferta)
            {
                var filaFin = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
                filaFin.Add(Texto(Localizador.T("proponer_tiene_fin")).Centrado(), 0);
                filaFin.Add(swFin, 1);
                condiciones.Add(filaFin);
                condiciones.Add(CampoTexto(dpFin));
                condiciones.Add(Etiqueta("proponer_metodo_pago"));
                condiciones.Add(CampoTexto(pkMetodo));
            }
            Contenido.Add(Tarjeta(condiciones.ToArray()));

            // Horario
            Contenido.Add(Titulo(Localizador.T("proponer_horario")).ConMargen(4, 0));
            var lista = new VerticalStackLayout();
            ConstruirDias(lista, detalle?.Horario);
            Contenido.Add(new Border
            {
                Stroke = Colors.Transparent, BackgroundColor = Tema.C("ColorSurface"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 }, Content = lista
            });

            // Nota + resumen + enviar
            Contenido.Add(Tarjeta(Etiqueta("proponer_nota"), CampoTexto(txtNota, 90)));
            Contenido.Add(Tarjeta(lblResumen,
                Texto(Localizador.T(EsContraoferta ? "contraproponer_explicacion" : "proponer_explicacion"), true, 12.5),
                Boton(Localizador.T(EsContraoferta ? "contraproponer_enviar" : "proponer_enviar"), TipoBoton.Principal, EnviarAsync)));
            Contenido.Add(new ContentView { HeightRequest = 24 + BarraEstado.AltoInferior() });
            ActualizarResumen();
        }

        private static Label Etiqueta(string clave) => Texto(Localizador.T(clave), false, 13);

        private void ConstruirDias(VerticalStackLayout lista, List<FranjaContrato>? iniciales)
        {
            dias.Clear();
            for (int i = 0; i < OrdenDias.Length; i++)
            {
                var dia = OrdenDias[i];
                var franja = iniciales?.FirstOrDefault(f => f.DiaSemana == dia);
                var activo = new Switch { OnColor = Tema.C("ColorPrimary"), VerticalOptions = LayoutOptions.Center, IsToggled = franja != null || (iniciales == null && dia is >= 1 and <= 5) };
                var inicio = new TimePicker { Time = franja != null && TimeSpan.TryParse(franja.HoraInicio, out var ti) ? ti : new TimeSpan(8, 0, 0), Format = "h:mm tt", FontSize = 14, TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center };
                var fin = new TimePicker { Time = franja != null && TimeSpan.TryParse(franja.HoraFin, out var tf) ? tf : new TimeSpan(14, 0, 0), Format = "h:mm tt", FontSize = 14, TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center };
                var horas = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End, IsVisible = activo.IsToggled, Children = { inicio, Texto(Localizador.T("horario_hasta"), true, 13).Centrado(), fin } };
                activo.Toggled += (_, e) => { horas.IsVisible = e.Value; ActualizarResumen(); };
                inicio.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TimePicker.Time)) ActualizarResumen(); };
                fin.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TimePicker.Time)) ActualizarResumen(); };

                var fila = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 8, Padding = new Thickness(14, 8), MinimumHeightRequest = 56
                };
                fila.Add(activo, 0);
                fila.Add(new Label { Text = Localizador.T($"dia_{dia}"), FontSize = 14, FontFamily = "OpenSansSemibold", TextColor = Tema.C("ColorTextStrong"), VerticalOptions = LayoutOptions.Center }, 1);
                fila.Add(horas, 2);
                dias[dia] = (activo, inicio, fin);
                lista.Add(fila);
                if (i < OrdenDias.Length - 1)
                    lista.Add(new ContentView { HeightRequest = 1, BackgroundColor = Tema.C("ColorBorder"), Margin = new Thickness(14, 0) });
            }
        }

        private List<FranjaContrato> Horario() => OrdenDias.Where(d => dias.ContainsKey(d) && dias[d].Activo.IsToggled)
            .Select(d => new FranjaContrato
            {
                DiaSemana = d,
                HoraInicio = (dias[d].Inicio.Time ?? TimeSpan.Zero).ToString(@"hh\:mm"),
                HoraFin = (dias[d].Fin.Time ?? TimeSpan.Zero).ToString(@"hh\:mm")
            }).ToList();

        private decimal Tarifa() => decimal.TryParse(txtTarifa.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var t) ? t : 0;

        private void ActualizarResumen()
        {
            var horas = HorasSemana(Horario().Where(f => string.CompareOrdinal(f.HoraFin, f.HoraInicio) > 0));
            lblResumen.Text = Localizador.F("proponer_resumen", horas.ToString("0.#", Localizador.Cultura), Monto(Tarifa() * (decimal)horas));
        }

        private async Task EnviarAsync()
        {
            var horario = Horario();
            var malo = horario.FirstOrDefault(f => string.CompareOrdinal(f.HoraFin, f.HoraInicio) <= 0);
            if (horario.Count == 0 || malo != null)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), malo != null ? Localizador.F("horario_err_rango", Localizador.T($"dia_{malo.DiaSemana}").ToLower(Localizador.Cultura)) : Localizador.T("horario_err_sin_dias"), Localizador.T("ok"));
                return;
            }
            if (Tarifa() <= 0)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("contrato_err_tarifa_invalida"), Localizador.T("ok"));
                return;
            }
            var inicio = (dpInicio.Date ?? DateTime.Today.AddDays(1)).Date;
            var nota = string.IsNullOrWhiteSpace(txtNota.Text) ? null : txtNota.Text.Trim();

            (bool Ok, string? Error) r;
            if (EsContraoferta)
            {
                r = await _api.ResponderContratoAsync(detalle!.Contrato!.Id, new ResponderContratoRequest
                {
                    UsuarioId = usuarioId, Accion = "contraproponer", TarifaHora = Tarifa(), FechaInicio = inicio, Horario = horario, Nota = nota
                });
            }
            else
            {
                if (pkDomicilio.SelectedIndex < 0)
                {
                    await Alerta.MostrarAsync(Localizador.T("error"), Localizador.T("proponer_elige_domicilio"), Localizador.T("ok"));
                    return;
                }
                r = await _api.ProponerContratoAsync(new ProponerContratoRequest
                {
                    ClienteId = usuarioId, CuidadorId = cuidador!.Id, UbicacionId = domicilios[pkDomicilio.SelectedIndex].UbicacionId,
                    TipoServicio = cuidador.Especialidad ?? "", TarifaHora = Tarifa(),
                    MetodoPago = pkMetodo.SelectedIndex == 1 ? "efectivo" : "transferencia",
                    FechaInicio = inicio, FechaFin = swFin.IsToggled ? (dpFin.Date ?? inicio.AddMonths(3)).Date : null,
                    Horario = horario, Nota = nota
                });
            }

            if (!r.Ok)
            {
                await Alerta.MostrarAsync(Localizador.T("error"), r.Error ?? "", Localizador.T("ok"));
                return;
            }
            await Alerta.MostrarAsync(Localizador.T(EsContraoferta ? "contraproponer_enviada_titulo" : "proponer_enviada_titulo"),
                Localizador.T(EsContraoferta ? "contraproponer_enviada_texto" : "proponer_enviada_texto"), Localizador.T("entendido"), TipoAlerta.Exito);
            await Shell.Current.GoToAsync(EsContraoferta ? ".." : "../MisContratosPage");
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
