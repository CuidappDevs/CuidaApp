using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models.Contrato;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;
namespace CUIDAPP.Views.Contratos;
public partial class ContratoJornadasPage : ContentPage, IQueryAttributable
{
 private readonly ApiService _api = new();
 private int usuarioId, contratoId;
 private ContratoDetalle? d;
 private bool cargando;
 // Viene de la notificación de ausencia: abre la lista de relevos de la primera jornada que lo necesite.
 private bool abrirRelevo;
 public ContratoJornadasPage() { InitializeComponent(); ContenidoEncabezado.Margin=new Thickness(0,BarraEstado.Alto(),0,0); LblTitulo.Text=Localizador.T("contrato_ausencias"); LblSubtitulo.Text=""; }
 public void ApplyQueryAttributes(IDictionary<string,object> query) { if(query.TryGetValue("ContratoId",out var v))int.TryParse(v?.ToString(),out contratoId); abrirRelevo = query.TryGetValue("AbrirRelevo", out var a) && a is true; }
 protected override async void OnAppearing()
 {
     base.OnAppearing(); usuarioId=Preferences.Default.Get("UserId",0); await Task.Yield(); await CargarAsync();
     if (abrirRelevo && SoyCliente && d != null)
     {
         abrirRelevo = false;
         var pendiente = d.Trabajos.Where(t => t.Fecha.Date >= ServerClock.Today && t.Estado == 5 && d.Ausencias.Any(a => a.Fecha.Date == t.Fecha.Date && a.MarcadoPor != usuarioId))
             .OrderBy(t => t.Fecha).FirstOrDefault();
         if (pendiente != null) await BuscarReemplazoAsync(pendiente.Id);
     }
 }
 private async Task CargarAsync() {
 if(cargando)return; cargando=true;
 try { d=await _api.ObtenerContratoAsync(contratoId,usuarioId); Contenido.Clear(); if(d?.Contrato is {} c) {                 var proximas = d.Trabajos.Where(t => t.Fecha.Date >= ServerClock.Today).OrderBy(t => t.Fecha).Take(7).ToList();
                var jornadas = new List<View> { Titulo(Localizador.T("contrato_proximas_jornadas")) };
                if (proximas.Count == 0) jornadas.Add(Texto(Localizador.T(c.Estado == 5 ? "contrato_pausado_sin_jornadas" : "contrato_sin_jornadas"), true));
                foreach (var t in proximas)
                {
                    var turno = new List<View> { Titulo(Localizador.FechaLarga(t.Fecha), 15),
                        Texto($"{Hora(t.HoraInicio)} – {Hora(t.HoraFin)}"),
                        Pastilla(Localizador.T(t.Estado == 5 ? "contrato_jornada_cancelada" : t.Estado == 4 ? "contrato_resumen_completadas" : "contrato_turno_programado"),
                            t.Estado == 5 ? ("ColorDangerSoft", "ColorDanger") : t.Estado == 4 ? ("ColorSuccessSoft", "ColorSuccess") : ("ColorWarningSoft", "ColorWarning")) };
                    if (t.Estado is 1 or 2)
                        turno.Add(Boton(Localizador.T("contrato_marcar_ausencia"), TipoBoton.Secundario, async () => { dpAusencia.Date = t.Fecha.Date; await MarcarAusenciaAsync(); }));
                    jornadas.Add(Bloque(turno.ToArray()));
                    if (SoyCliente && t.Estado == 5 && d.Ausencias.Any(a => a.Fecha.Date == t.Fecha.Date && a.MarcadoPor != usuarioId))
                        jornadas.Add(Boton(Localizador.T("contrato_buscar_reemplazo"), TipoBoton.Secundario, () => BuscarReemplazoAsync(t.Id)));
                }
                Contenido.Add(Tarjeta(jornadas.ToArray()));

                var ausencias = new List<View> { Titulo(Localizador.T("contrato_ausencias")), Texto(Localizador.T("contrato_ausencias_texto"), true) };
                foreach (var a in d.Ausencias.Where(a => a.Fecha.Date >= ServerClock.Today).OrderBy(a => a.Fecha))
                {
                    var fila = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
                    fila.Add(Texto($"{Localizador.FechaLarga(a.Fecha)} · {a.MarcadoPorNombre}{(string.IsNullOrWhiteSpace(a.Motivo) ? "" : " · " + a.Motivo)}", false, 13).Centrado(), 0);
                    if (a.MarcadoPor == usuarioId && a.Fecha.Date > ServerClock.Today)
                    {
                        var quitar = Texto(Localizador.T("quitar"), false, 13).ConColor("ColorDanger");
                        quitar.Padding = new Thickness(8);
                        quitar.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await QuitarAusenciaAsync(a)) });
                        fila.Add(quitar, 1);
                    }
                    ausencias.Add(Bloque(fila));
                }
                ausencias.Add(CampoTexto(dpAusencia));
                ausencias.Add(CampoTexto(txtAusencia));
                if (!SoyCliente) { ausencias.Add(Texto(Localizador.T("contrato_emergencia"))); ausencias.Add(emergencia); }
                ausencias.Add(Boton(Localizador.T("contrato_marcar_ausencia"), TipoBoton.Secundario, MarcarAusenciaAsync));
                Contenido.Add(Tarjeta(ausencias.ToArray()));

 } }
 catch(Exception ex) { System.Diagnostics.Debug.WriteLine(ex); await Alerta.MostrarAsync(Localizador.T("error"),Localizador.T("contrato_err_generico"),Localizador.T("ok")); }
 finally { Contenido.Add(new ContentView{HeightRequest=24+BarraEstado.AltoInferior()}); Cargando.IsVisible=Cargando.IsRunning=false; Desplazamiento.IsVisible=true; cargando=false; }
 }
 private async void OnBackTapped(object sender,TappedEventArgs e)=>await Shell.Current.GoToAsync("..");
 private bool SoyCliente => d?.Contrato?.MiRol == "cliente";
 private readonly DatePicker dpAusencia = new() { Format="D", MinimumDate=ServerClock.Today, TextColor=Tema.C("ColorTextStrong") };
 private readonly Switch emergencia = new();
 private readonly Entry txtAusencia = new() { MaxLength=300, TextColor=Tema.C("ColorTextStrong"), Placeholder=Localizador.T("ausencia_motivo_placeholder") };        private async Task MarcarAusenciaAsync()
        {
            var fecha = (dpAusencia.Date ?? ServerClock.Today).Date;
            if (!await Alerta.MostrarAsync(Localizador.T("contrato_marcar_ausencia"), Localizador.F("ausencia_confirmar", Localizador.FechaLarga(fecha)), Localizador.T("si_continuar"), Localizador.T("cancelar"), TipoAlerta.Pregunta))
                return;
            var (ok, error) = await _api.MarcarAusenciaContratoAsync(contratoId, usuarioId, fecha, string.IsNullOrWhiteSpace(txtAusencia.Text) ? null : txtAusencia.Text.Trim(), emergencia.IsToggled);
            if (!ok) { await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok")); return; }
            txtAusencia.Text = "";
            await CargarAsync();
        }

        private Border? tarjetaRelevos;

        // Candidatas verificadas del mismo tipo de servicio, cerca del domicilio. Primero las que ya
        // vinieron a esta casa (y mejor calificadas por este cliente).
        private async Task BuscarReemplazoAsync(int trabajoId)
        {
            var candidatos = await _api.ReemplazosContratoAsync(contratoId, trabajoId, usuarioId) ?? new();
            var opciones = new List<View>
            {
                Titulo(Localizador.T("contrato_buscar_reemplazo")),
                Texto(Localizador.T("relevo_lista_explicacion"), true, 12.5)
            };
            if (candidatos.Count == 0) opciones.Add(Texto(Localizador.T("contrato_sin_reemplazos")));
            foreach (var candidato in candidatos)
                opciones.Add(TarjetaCandidata(candidato, trabajoId));

            if (tarjetaRelevos != null) Contenido.Remove(tarjetaRelevos);
            tarjetaRelevos = Tarjeta(opciones.ToArray());
            tarjetaRelevos.Opacity = 0;
            Contenido.Insert(0, tarjetaRelevos);
            await Desplazamiento.ScrollToAsync(0, 0, false);
            await tarjetaRelevos.FadeTo(1, 180, Easing.CubicOut);
        }

        private View TarjetaCandidata(ReemplazoContrato c, int trabajoId)
        {
            var foto = new Border
            {
                WidthRequest = 46, HeightRequest = 46, StrokeThickness = 0, BackgroundColor = Tema.C("ColorPrimarySoft"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(), VerticalOptions = LayoutOptions.Center,
                Content = string.IsNullOrWhiteSpace(c.FotoUrl)
                    ? new Label { Text = c.NombreCompleto.Length > 0 ? c.NombreCompleto[..1].ToUpperInvariant() : "?", FontFamily = "OpenSansSemibold", FontSize = 16, TextColor = Tema.C("ColorPrimary"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
                    : new Image { Source = c.FotoUrl!.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? c.FotoUrl : $"{ApiService.ServerOrigin}{c.FotoUrl}", Aspect = Aspect.AspectFill }
            };
            var datos = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
            datos.Add(Titulo(c.NombreCompleto, 15));
            datos.Add(Texto(Localizador.F("relevo_tarifa_distancia", Monto(c.TarifaHora), c.DistanciaKm.ToString("0.#", Localizador.Cultura)), true, 12.5));
            if (c.YaVino)
                datos.Add(Pastilla(c.CalificacionConCliente is decimal cal
                        ? Localizador.F("relevo_ya_vino_calificada", c.VecesConCliente, cal.ToString("0.#", Localizador.Cultura))
                        : Localizador.F("relevo_ya_vino", c.VecesConCliente),
                    ("ColorSuccessSoft", "ColorSuccess")));

            var cabecera = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 12 };
            cabecera.Add(foto, 0);
            cabecera.Add(datos, 1);

            return Bloque(cabecera, Boton(Localizador.T("relevo_elegir"), c.YaVino ? TipoBoton.Principal : TipoBoton.Secundario, async () =>
            {
                if (!await Alerta.MostrarAsync(Localizador.T("contrato_buscar_reemplazo"),
                    Localizador.F("relevo_confirmar", c.NombreCompleto), Localizador.T("si_continuar"), Localizador.T("cancelar"), TipoAlerta.Pregunta)) return;
                var (ok, error) = await _api.ReemplazarContratoAsync(contratoId, trabajoId, usuarioId, c.Id);
                await Alerta.MostrarAsync(Localizador.T(ok ? "contrato_buscar_reemplazo" : "error"),
                    ok ? Localizador.T("contrato_reemplazo_enviado") : error ?? "", Localizador.T("ok"), ok ? TipoAlerta.Exito : TipoAlerta.Error);
                if (ok) { tarjetaRelevos = null; await CargarAsync(); }
            }));
        }

        private async Task QuitarAusenciaAsync(AusenciaContrato a)
        {
            var (ok, error) = await _api.QuitarAusenciaContratoAsync(a.Id, usuarioId);
            if (!ok) { await Alerta.MostrarAsync(Localizador.T("error"), error ?? "", Localizador.T("ok")); return; }
            await CargarAsync();
        }


}