using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CUIDAPP_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SistemaController : ControllerBase
    {
        private readonly string _connectionString;

        public SistemaController(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        // Fuente de verdad para validaciones de fecha/hora en el cliente (ej. PIN de
        // inicio/salida): la app compara contra ESTA hora, no contra el reloj del
        // dispositivo, porque sp_IniciarTrabajo/sp_FinalizarTrabajo también validan
        // contra GETDATE() del mismo servidor SQL. Si el reloj del teléfono está mal,
        // ya no genera bloqueos "sin razón aparente".
        [HttpGet("hora")]
        public async Task<IActionResult> ObtenerHoraServidor()
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("SELECT GETDATE() AS HoraServidor", connection);

            await connection.OpenAsync();
            var horaServidor = (DateTime)(await command.ExecuteScalarAsync() ?? DateTime.Now);

            return Ok(new { horaServidor });
        }
    }
}
