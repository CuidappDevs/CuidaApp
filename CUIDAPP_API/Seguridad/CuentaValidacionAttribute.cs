using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CUIDAPP_API.Seguridad;

// Antes del filtro automático de ApiController: errores nuevos siempre en español,
// incluso si el model binder genera un mensaje en inglés para JSON mal formado.
public sealed class CuentaValidacionAttribute : ActionFilterAttribute
{
    public CuentaValidacionAttribute() { Order=-3000; }
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if(!context.ModelState.IsValid)
            context.Result=new BadRequestObjectResult(new { motivo="DATOS_INVALIDOS",message="Revisa los datos obligatorios, el formato y la longitud de los campos.",
                campos=context.ModelState.Where(e=>e.Value?.Errors.Count>0).Select(e=>e.Key).ToArray() });
    }
}
