using CUIDAPP.Helpers;
using CUIDAPP.Localization;
using CUIDAPP.Models.Contrato;
using CUIDAPP.Services;
using static CUIDAPP.Helpers.ContratoVista;
namespace CUIDAPP.Views.Contratos;
public partial class ContratoDocumentosPage : ContentPage, IQueryAttributable
{
 private readonly ApiService _api = new();
 private int usuarioId, contratoId;
 private ContratoDetalle? d;
 private bool cargando;
 public ContratoDocumentosPage() { InitializeComponent(); ContenidoEncabezado.Margin=new Thickness(0,BarraEstado.Alto(),0,0); LblTitulo.Text=Localizador.T("contrato_ver_pdf"); LblSubtitulo.Text=""; }
 public void ApplyQueryAttributes(IDictionary<string,object> query) { if(query.TryGetValue("ContratoId",out var v))int.TryParse(v?.ToString(),out contratoId); }
 protected override async void OnAppearing() { base.OnAppearing(); usuarioId=Preferences.Default.Get("UserId",0); await Task.Yield(); await CargarAsync(); }
 private async Task CargarAsync() {
 if(cargando)return; cargando=true;
 try { d=await _api.ObtenerContratoAsync(contratoId,usuarioId); Contenido.Clear(); if(d?.Contrato!=null) {
 foreach(var doc in d.Documentos) Contenido.Add(Tarjeta(Boton(Localizador.T("contrato_ver_pdf"),TipoBoton.Secundario,async()=>{ var url=await _api.ObtenerEnlacePdfContratoAsync(contratoId,usuarioId,doc.Id); if(url!=null)await Launcher.Default.OpenAsync(new Uri(url)); })));
 var anexos=await _api.AnexosContratoAsync(contratoId,usuarioId);
 foreach(var anexo in anexos.Where(a=>a.DocumentoId!=null)) Contenido.Add(Tarjeta(Boton(Localizador.T("anexos"),TipoBoton.Secundario,async()=>{ var url=await _api.ObtenerEnlacePdfContratoAsync(contratoId,usuarioId,anexo.DocumentoId); if(url!=null)await Launcher.Default.OpenAsync(new Uri(url)); })));
} }
 catch(Exception ex) { System.Diagnostics.Debug.WriteLine(ex); await Alerta.MostrarAsync(Localizador.T("error"),Localizador.T("contrato_err_generico"),Localizador.T("ok")); }
 finally { Contenido.Add(new ContentView{HeightRequest=24+BarraEstado.AltoInferior()}); Cargando.IsVisible=Cargando.IsRunning=false; Desplazamiento.IsVisible=true; cargando=false; }
 }
 private async void OnBackTapped(object sender,TappedEventArgs e)=>await Shell.Current.GoToAsync("..");

}