using System.Net.Http.Json;

namespace CUIDAPP_ADMINISTRATIVO.Models.Common
{
    // Resultado tipado de suspender/reactivar. Exitoso = HTTP 200 (APPLIED o ALREADY_COMPLETED).
    public class AdminActionResult
    {
        public bool Success { get; set; }
        public string Code { get; set; } = "";
        public string Message { get; set; } = "";
        public int? SancionId { get; set; }
        public bool? IsActive { get; set; }
        public string? EstadoSancion { get; set; }
        public DateTimeOffset? FechaFinUtc { get; set; }

        public static AdminActionResult Falla(string code, string message)
            => new() { Success = false, Code = code, Message = message };

        // Mensaje seguro: solo el texto que la API define para cada código, nunca excepciones.
        public static async Task<AdminActionResult> DesdeRespuestaAsync(HttpResponseMessage response)
        {
            try
            {
                if (response.IsSuccessStatusCode)
                {
                    var ok = await response.Content.ReadFromJsonAsync<AdminActionResult>();
                    if (ok != null)
                    {
                        ok.Success = true;
                        return ok;
                    }
                    return new AdminActionResult { Success = true, Code = "APPLIED", Message = "Operación aplicada." };
                }

                var error = await response.Content.ReadFromJsonAsync<ApiError>();
                if (error != null && !string.IsNullOrWhiteSpace(error.Code))
                {
                    var detalle = error.Errors is { Count: > 0 }
                        ? " " + string.Join(" ", error.Errors.Values.SelectMany(v => v))
                        : "";
                    return Falla(error.Code, error.Message + detalle);
                }
            }
            catch (Exception)
            {
                // cuerpo no JSON: se cae al mensaje genérico
            }

            return Falla("INTERNAL_ERROR", "No se pudo completar la operación. Intenta de nuevo.");
        }
    }
}
