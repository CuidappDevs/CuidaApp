using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;

namespace CUIDAPP_API.Seguridad;

public class CuentaSqlExceptionFilter(ILogger<CuentaSqlExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if(context.Exception is not SqlException ex) return;
        if(ex.Number==51061)
        {
            var texto=ex.Errors.Cast<SqlError>().FirstOrDefault(e=>e.Number==51061)?.Message ?? "";
            var monto=decimal.TryParse(texto.Split('|').Last(),NumberStyles.Number,CultureInfo.InvariantCulture,out var valor)?valor:0;
            context.Result=new ConflictObjectResult(new { motivo="DEUDA_PENDIENTE",message="Debes resolver tu deuda pendiente antes de solicitar un servicio o contrato.",monto });
        }
        else if(ex.Number==51062)
            context.Result=new ObjectResult(new { motivo="CUENTA_INACTIVA",message="La cuenta no está activa." }) { StatusCode=403 };
        else
        {
            logger.LogError(ex,"Error SQL procesando la solicitud");
            context.Result=new ObjectResult(new { motivo="ERROR_INTERNO",message="No se pudo completar la operación. Inténtalo más tarde." }) { StatusCode=500 };
        }
        context.ExceptionHandled=true;
    }
}
