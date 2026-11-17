using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models.Contrato;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;
namespace CUIDAPP.Views.Contratos;
public partial class ContratoAnexosPage : ContentPage, IQueryAttributable
{
 private readonly ApiService _api = new();
 private int usuarioId, contratoId;
 private ContratoDetalle? d;
 private bool cargando;
 public ContratoAnexosPage() { InitializeComponent(); ContenidoEncabezado.Margin=new Thickness(0,BarraEstado.Alto(),0,0); LblTitulo.Text=Localizador.T("anexos"); LblSubtitulo.Text=""; }
 public void ApplyQueryAttributes(IDictionary<string,object> query) { if(query.TryGetValue("ContratoId",out var v))int.TryParse(v?.ToString(),out contratoId); }
 protected override async void OnAppearing() { base.OnAppearing(); usuarioId=Preferences.Default.Get("UserId",0); await Task.Yield(); await CargarAsync(); }
 private async Task CargarAsync() {
 if(cargando)return; cargando=true;
 try { d=await _api.ObtenerContratoAsync(contratoId,usuarioId); Contenido.Clear(); if(d?.Contrato!=null)await AgregarAnexosAsync(); }
 catch(Exception ex) { System.Diagnostics.Debug.WriteLine(ex); await Alerta.MostrarAsync(Localizador.T("error"),Localizador.T("contrato_err_generico"),Localizador.T("ok")); }
 finally { Contenido.Add(new ContentView{HeightRequest=24+BarraEstado.AltoInferior()}); Cargando.IsVisible=Cargando.IsRunning=false; Desplazamiento.IsVisible=true; cargando=false; }
 }
 private async void OnBackTapped(object sender,TappedEventArgs e)=>await Shell.Current.GoToAsync("..");
 private async Task AgregarAnexosAsync()
 {
  var anexos=await _api.AnexosContratoAsync(contratoId,usuarioId);
  var views=new List<View> {Titulo(Localizador.T("anexos"))};
  if(anexos.Count == 0) views.Add(Texto(Localizador.T("contrato_anexos_vacio"), true));
  foreach(var a in anexos)
  {
   if(a.Estado is 1 or 3 && a.FechaCreacion.AddHours(72)<=ServerClock.Now) a.Estado=8;
   views.Add(Separador());
   views.Add(Pastilla(Localizador.T("anexo_estado_"+a.Estado), a.Estado == 4 ? ("ColorSuccessSoft", "ColorSuccess") : a.Estado is 1 or 3 ? ("ColorWarningSoft", "ColorWarning") : ("ColorSubtle", "ColorTextMuted")));
   views.Add(Titulo(Localizador.FechaLarga(a.FechaInicio),15));
   views.Add(Texto(Localizador.F("contrato_tarifa_hora",Monto(a.TarifaHora))));
   foreach(var f in LeerHorarioAnexo(a.Horario)) views.Add(Texto(Franja(f)));
   if(a.Estado==1 && a.PropuestoPor!=usuarioId && a.FechaCreacion.AddHours(72)>ServerClock.Now)
    foreach(var accion in new[]{"aceptar","rechazar"}) views.Add(Boton(Localizador.T("contrato_"+accion),TipoBoton.Secundario,async()=>{
     if(!await Alerta.MostrarAsync(Localizador.T("anexos"),Localizador.T("anexo_confirmar"),Localizador.T("si_continuar"),Localizador.T("cancelar")))return;
     var (ok,error)=await _api.ResponderAnexoAsync(a.Id,usuarioId,accion);await ResultadoAnexo(ok,error);
    }));
   if(a.Estado==3 && a.YoFirme==0) views.Add(Boton(Localizador.T("contrato_firmar"),TipoBoton.Principal,()=>{
    var copia=System.Text.Json.JsonSerializer.Deserialize<ContratoDetalle>(System.Text.Json.JsonSerializer.Serialize(d))!;
    copia.Contrato!.TarifaHora=a.TarifaHora;copia.Contrato.FechaInicio=a.FechaInicio;copia.Horario=LeerHorarioAnexo(a.Horario);
    return Shell.Current.GoToAsync("FirmarContratoPage",new Dictionary<string,object>{{"Detalle",copia},{"AnexoId",a.Id}});
   }));
   if(a.DocumentoId is int documento) views.Add(Boton(Localizador.T("contrato_ver_pdf"),TipoBoton.Secundario,async()=>{
    var url=await _api.ObtenerEnlacePdfContratoAsync(contratoId,usuarioId,documento);
    if(url!=null)await Launcher.Default.OpenAsync(new Uri(url));
   }));
  }
  if(d!.Contrato!.Estado==4 && !anexos.Any(a=>a.Estado is 1 or 3 && a.FechaCreacion.AddHours(72)>ServerClock.Now))
  {
   var tarifa=new Entry{Text=d.Contrato.TarifaHora.ToString("0.##",Localizador.Cultura),Keyboard=Keyboard.Numeric,TextColor=Tema.C("ColorTextStrong")};
   var fecha=new DatePicker{MinimumDate=ServerClock.Today.AddDays(1),Date=ServerClock.Today.AddDays(1),TextColor=Tema.C("ColorTextStrong")};
   views.Add(Titulo(Localizador.T("anexo_proponer")));views.Add(Texto(Localizador.T("contrato_tarifa")));views.Add(CampoTexto(tarifa));
   views.Add(Texto(Localizador.T("anexo_vigencia")));views.Add(CampoTexto(fecha));
   var franjas=new List<(int Dia,Switch Activo,TimePicker Inicio,TimePicker Fin)>();
   views.Add(Titulo(Localizador.T("proponer_horario")));
   views.Add(EditorSemana(d.Horario,franjas));
   views.Add(Boton(Localizador.T("anexo_proponer"),TipoBoton.Principal,async()=>{
    if(!decimal.TryParse(tarifa.Text,System.Globalization.NumberStyles.Number,Localizador.Cultura,out var importe)||importe<=0) {await ResultadoAnexo(false,Localizador.T("contrato_err_datos_incompletos"));return;}
    var horario=franjas.Where(f=>f.Activo.IsToggled).Select(f=>new FranjaContrato{DiaSemana=f.Dia,HoraInicio=(f.Inicio.Time??TimeSpan.Zero).ToString(@"hh\:mm"),HoraFin=(f.Fin.Time??TimeSpan.Zero).ToString(@"hh\:mm")}).ToList();
    if(horario.Count==0 || horario.Any(f=>string.CompareOrdinal(f.HoraFin,f.HoraInicio)<=0)) {await ResultadoAnexo(false,Localizador.T("contrato_err_horario_invalido"));return;}
    if(!await Alerta.MostrarAsync(Localizador.T("anexo_proponer"),Localizador.T("anexo_confirmar"),Localizador.T("si_continuar"),Localizador.T("cancelar")))return;
    var (ok,error)=await _api.ProponerAnexoAsync(contratoId,usuarioId,importe,fecha.Date??ServerClock.Today.AddDays(1),horario);await ResultadoAnexo(ok,error);
   }));
  }
  if(views.Count>1) Contenido.Add(Tarjeta(views.ToArray()));
 }
 /// <summary>Un día por fila (lunes primero): nombre + interruptor; si está activo, las horas en una sola línea.</summary>
 private async Task ResultadoAnexo(bool ok,string? error) {if(ok)await CargarAsync();else await Alerta.MostrarAsync(Localizador.T("error"),error??"",Localizador.T("ok"));}
 private static List<FranjaContrato> LeerHorarioAnexo(string texto)=>texto.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(f=>f.Split('|')).Select(v=>new FranjaContrato{DiaSemana=int.Parse(v[0]),HoraInicio=v[1],HoraFin=v[2]}).ToList();
}
