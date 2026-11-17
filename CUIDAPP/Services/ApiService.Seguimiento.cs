using System.Net.Http.Json;
using System.Text.Json;
namespace CUIDAPP.Services;
public partial class ApiService
{
 public async Task<JsonElement> SeguimientoSolicitudAsync(HttpMethod metodo,string ruta)
 {
  using var r=await EnviarSolicitudContratoAsync(metodo,ruta);
  if(!r.IsSuccessStatusCode) throw new InvalidOperationException(await ErrorContratoAsync(r));
  return r.StatusCode==System.Net.HttpStatusCode.NoContent ? default : await r.Content.ReadFromJsonAsync<JsonElement>();
 }
}
