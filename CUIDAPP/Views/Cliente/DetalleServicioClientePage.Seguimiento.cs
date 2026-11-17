using CUIDAPP.Localization;
using CUIDAPP.Services;
namespace CUIDAPP.Views.Cliente;
public partial class DetalleServicioClientePage
{
 private async Task CargarSeguimientosAsync()
 {
  CompartirSeguimiento.Clear();
  CompartirSeguimiento.IsVisible=trabajo?.Estado is 2 or 3;
  if(!CompartirSeguimiento.IsVisible) return;
  int cliente=Preferences.Default.Get("UserId",0);
  var ruta=$"seguimiento/trabajos/{trabajoId}/{cliente}";
  var crear=new Button { Text=Localizador.T("seguimiento_compartir"),BackgroundColor=Tema.C("ColorPrimary"),TextColor=Colors.White };
  CompartirSeguimiento.Add(crear);
  crear.Clicked+=async(_,_)=>{crear.IsEnabled=false;try{
   var r=await _apiService.SeguimientoSolicitudAsync(HttpMethod.Post,ruta);
   // El enlace va en su propia línea dentro del texto (no en Uri): así WhatsApp y otras apps lo reconocen y se puede tocar.
   var url=r.GetProperty("url").GetString();
   await Share.Default.RequestAsync(new ShareTextRequest { Title=Localizador.T("seguimiento_compartir"),Text=$"{Localizador.T("seguimiento_texto")}\n\n{url}" });
   await CargarSeguimientosAsync();
  }catch(Exception ex){await Alerta.MostrarAsync(Localizador.T("seguimiento_compartir"),ex.Message,Localizador.T("entendido"));}finally{crear.IsEnabled=true;}};
  try{
   var links=await _apiService.SeguimientoSolicitudAsync(HttpMethod.Get,ruta);
   foreach(var link in links.EnumerateArray()){
    int id=link.GetProperty("Id").GetInt32();
    CompartirSeguimiento.Add(new Label { Text=Localizador.F("seguimiento_enlace",id),TextColor=Tema.C("ColorTextMuted") });
    var revoke=new Button { Text=Localizador.T("seguimiento_revocar"),TextColor=Tema.C("ColorDanger"),BackgroundColor=Tema.C("ColorSurface") };
    revoke.Clicked+=async(_,_)=>{try{await _apiService.SeguimientoSolicitudAsync(HttpMethod.Post,$"seguimiento/{id}/revocar/{cliente}");await CargarSeguimientosAsync();}catch(Exception ex){await Alerta.MostrarAsync(Localizador.T("seguimiento_compartir"),ex.Message,Localizador.T("entendido"));}};
    CompartirSeguimiento.Add(revoke);
   }
  }catch{CompartirSeguimiento.Add(new Label {Text=Localizador.T("seguimiento_error"),TextColor=Tema.C("ColorTextMuted")});}
 }
}
