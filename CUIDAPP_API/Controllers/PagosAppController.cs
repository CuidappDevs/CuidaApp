using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.Interfaces.Admin;

namespace CUIDAPP_API.Controllers
{
    /// <summary>
    /// Datos de pago que usa la app (sin token): saldo de comisiones de efectivo del Care Partner e
    /// instrucciones para pagar por transferencia.
    /// </summary>
    [Route("api/pagos-app")]
    [ApiController]
    public class PagosAppController : ControllerBase
    {
        private readonly IAdminOperacionesService _db;
        private readonly ILogger<PagosAppController> _logger;

        public PagosAppController(IAdminOperacionesService db, ILogger<PagosAppController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>Lo que el Care Partner le debe a CuidApp por servicios cobrados en efectivo.</summary>
        [HttpGet("saldo/{cuidadorId:int}")]
        public async Task<IActionResult> Saldo(int cuidadorId)
        {
            try
            {
                var r = await _db.ConsultarAsync("sp_SaldoCuidador", ("@CuidadorId", cuidadorId));
                var f = r[0].FirstOrDefault();
                return Ok(new
                {
                    Saldo = Convert.ToDecimal(f?["Saldo"] ?? 0m),
                    Tope = Convert.ToDecimal(f?["Tope"] ?? 0m),
                    PuedeAceptarEfectivo = f?["PuedeAceptarEfectivo"] is not bool b || b,
                    Movimientos = r.Count > 1 ? r[1] : new List<CUIDAPP_API.DTOs.AdminOperaciones.Fila>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando saldo del cuidador {Id}", cuidadorId);
                return StatusCode(500, new { Message = "Error interno." });
            }
        }

        /// <summary>Instrucciones para pagar por transferencia (las define Finanzas en el panel).</summary>
        [HttpGet("instrucciones-transferencia")]
        public async Task<IActionResult> InstruccionesTransferencia()
        {
            try
            {
                var f = (await _db.ConsultarAsync("sp_ObtenerInstruccionesTransferencia"))[0].FirstOrDefault();
                return Ok(new { Instrucciones = f?["Instrucciones"]?.ToString() ?? "" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando instrucciones de transferencia");
                return Ok(new { Instrucciones = "" });
            }
        }
    }
}
