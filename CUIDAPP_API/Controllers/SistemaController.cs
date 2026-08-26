using Microsoft.AspNetCore.Mvc;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SistemaController : ControllerBase
    {
        // Fuente de verdad para validaciones de fecha/hora en el cliente (ej. PIN de
        // inicio/salida): la app compara contra ESTA hora, no contra el reloj del
        // dispositivo. Se usa el reloj del servidor de la API (no el de SQL Server)
        // porque el sistema operativo de la máquina de SQL Server está mal configurado
        // (zona horaria incorrecta) y los stored procedures relevantes (sp_IniciarTrabajo,
        // sp_ConfirmarFinalizacionTrabajo, etc.) ahora también reciben la hora como
        // parámetro desde aquí en vez de generarla con GETDATE().
        [HttpGet("hora")]
        public IActionResult ObtenerHoraServidor()
        {
            return Ok(new { horaServidor = DateTime.Now });
        }
    }
}
