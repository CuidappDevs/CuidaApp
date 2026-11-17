using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.DTOs.Common;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Seguridad;
using CUIDAPP_API.Services;
using CUIDAPP_API.Services.Admin;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Controllers
{
    /// <summary>
    /// Calidad de los Care Partners (semáforo), documentos que vencen, comisión de la plataforma,
    /// método de pago de cada servicio y disputas. Solo panel administrativo.
    /// </summary>
    [Route("api/admin")]
    [ApiController]
    [Authorize(Policy = PoliticasAdmin.Admin)]
    [AuditarAdmin]
    public class AdminGestionController : ControllerBase
    {
        private readonly IAdminOperacionesService _svc;
        private readonly CentroMandoService _avisos;
        private readonly ITrabajoNotifier _notifier;
        private readonly ILogger<AdminGestionController> _logger;

        public AdminGestionController(IAdminOperacionesService svc, CentroMandoService avisos, ITrabajoNotifier notifier, ILogger<AdminGestionController> logger)
        {
            _svc = svc;
            _avisos = avisos;
            _notifier = notifier;
            _logger = logger;
        }

        private int AdminId => PoliticasAdmin.AdminId(User);
        private static DateTime Hoy => HoraLocalRD.Ahora.Date;

        private async Task<IActionResult> Leer(string operacion, Func<Task<object>> accion)
        {
            try { return Ok(await accion()); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en {Operacion}", operacion);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        private IActionResult Resultado(Fila? fila, string ok = "Cambios guardados.")
        {
            var codigo = fila?["Resultado"]?.ToString() ?? "ERROR";
            return codigo switch
            {
                "OK" => Ok(new { Message = ok }),
                "FECHA_VENCIMIENTO_REQUERIDA" => BadRequest(new { motivo = codigo, message = "Pon la fecha de vencimiento del documento antes de aprobarlo." }),
                "FECHA_VENCIMIENTO_INVALIDA" => BadRequest(new { motivo = codigo, message = "La fecha de vencimiento debe ser posterior a hoy." }),
                "MOTIVO_REQUERIDO" => BadRequest(new { motivo = codigo, message = "Indica el motivo del rechazo." }),
                "NO_ENCONTRADO" => NotFound(new ApiErrorDto(codigo, "No se encontró el registro.")),
                "ESTADO_INVALIDO" => Conflict(new ApiErrorDto(codigo, "El caso o el servicio ya no está en un estado que permita esta acción.")),
                "MONTO_INVALIDO" => BadRequest(new ApiErrorDto(codigo, "El monto debe ser mayor que cero y menor que la tarifa del servicio.")),
                "PAGO_REALIZADO" => Conflict(new ApiErrorDto(codigo, "El pago de este servicio ya se envió: no se puede cambiar el monto.")),
                "PORCENTAJE_INVALIDO" => BadRequest(new ApiErrorDto(codigo, "El porcentaje debe estar entre 0 y 100.")),
                "METODO_INVALIDO" => BadRequest(new ApiErrorDto(codigo, "Método de pago no válido.")),
                _ => StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "No se pudo completar la acción."))
            };
        }

        // ---------------- Calidad
        [HttpGet("calidad")]
        public Task<IActionResult> Calidad([FromQuery] int dias = 30) => Leer("Calidad", async () =>
        {
            var d = Math.Clamp(dias, 7, 365);
            return (await _svc.ConsultarAsync("sp_AdminCalidadCuidadores", ("@Desde", Hoy.AddDays(-(d - 1))), ("@Hasta", Hoy), ("@Ahora", HoraLocalRD.Ahora)))[0];
        });

        [HttpGet("calidad/{cuidadorId:int}")]
        public Task<IActionResult> CalidadDetalle(int cuidadorId, [FromQuery] int dias = 30) => Leer("CalidadDetalle", async () =>
        {
            var d = Math.Clamp(dias, 7, 365);
            return (await _svc.ConsultarAsync("sp_AdminCalidadDetalle", ("@CuidadorId", cuidadorId), ("@Desde", Hoy.AddDays(-(d - 1))), ("@Hasta", Hoy), ("@Ahora", HoraLocalRD.Ahora)))[0];
        });

        // ---------------- Documentos que vencen
        [HttpGet("vencimientos")]
        public Task<IActionResult> Vencimientos() => Leer("Vencimientos", async () =>
            (await _svc.ConsultarAsync("sp_AdminDocumentosVencimiento", ("@Hoy", Hoy)))[0]);

        [Authorize(Policy = PoliticasAdmin.Verificacion)]
        [HttpPut("vencimientos/{id:int}")]
        public async Task<IActionResult> FijarVencimiento(int id, [FromBody] FechaVencimientoDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminFijarVencimientoDocumento", ("@DocumentoId", id), ("@Fecha", dto.Fecha?.Date), ("@Hoy", Hoy)), "Fecha de vencimiento guardada.");

        [Authorize(Policy = PoliticasAdmin.Verificacion)]
        [HttpPut("vencimientos/{id:int}/revisar")]
        public async Task<IActionResult> RevisarDocumento(int id, [FromBody] RevisarDocumentoDto dto)
        {
            if (!dto.Aprobar && string.IsNullOrWhiteSpace(dto.Observaciones))
                return BadRequest(new { motivo = "MOTIVO_REQUERIDO", message = "Indica el motivo del rechazo." });

            var fila = await _svc.EjecutarAsync("sp_AdminRevisarDocumento", ("@DocumentoId", id), ("@Aprobar", dto.Aprobar),
                ("@Fecha", dto.Fecha?.Date), ("@Observacion", dto.Observaciones?.Trim()), ("@Hoy", Hoy));
            if (fila?["Resultado"]?.ToString() == "OK" && fila["CuidadorId"] is int cuidadorId)
            {
                await _avisos.EnviarAsync(AdminId, cuidadorId, "mensaje",
                    dto.Aprobar ? "Documento aprobado" : "Documento rechazado",
                    dto.Aprobar ? "Revisamos tu documento y quedó aprobado." : $"Tu documento no fue aprobado: {dto.Observaciones!.Trim()}");
            }
            return Resultado(fila, dto.Aprobar ? "Documento aprobado." : "Documento rechazado.");
        }

        // ---------------- Servicios de una persona (fichas de cliente y Care Partner)
        [HttpGet("usuarios/{usuarioId:int}/servicios")]
        public Task<IActionResult> ServiciosUsuario(int usuarioId) => Leer("ServiciosUsuario", async () =>
            (await _svc.ConsultarAsync("sp_AdminServiciosUsuario", ("@UsuarioId", usuarioId)))[0]);

        // ---------------- Caja negra (grabación de seguridad)
        [HttpGet("grabacion-seguridad")]
        public Task<IActionResult> GrabacionSeguridad() => Leer("GrabacionSeguridad", async () =>
            new { Activa = (await _svc.ConsultarAsync("sp_GrabacionSeguridadActiva"))[0].FirstOrDefault()?["Activa"] is true });

        [Authorize(Policy = PoliticasAdmin.SuperAdmin)]
        [HttpPut("grabacion-seguridad")]
        public async Task<IActionResult> GuardarGrabacionSeguridad([FromBody] ActivaDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminGuardarGrabacionSeguridad", ("@Activa", dto.Activa), ("@AdminId", AdminId), ("@Fecha", HoraLocalRD.Ahora)),
                dto.Activa ? "Grabación de seguridad encendida." : "Grabación de seguridad apagada.");

        // Reproducir la caja negra de un servicio: solo Superadmin, y cada acceso queda en la auditoría.
        [Authorize(Policy = PoliticasAdmin.AuditoriaLegal)]
        [HttpGet("servicios/{id:int}/grabaciones")]
        public Task<IActionResult> GrabacionesServicio(int id) => Leer("GrabacionesServicio", async () =>
        {
            var r = await _svc.ConsultarAsync("sp_AdminGrabacionesServicio", ("@TrabajoId", id));
            var segmentos = r[0];
            if (segmentos.Count > 0)
                await _svc.EjecutarAsync("sp_AdminRegistrarAuditoria", ("@AdminId", AdminId), ("@Accion", "Escuchó la grabación de un servicio"),
                    ("@Entidad", "Trabajo"), ("@EntidadId", id.ToString()), ("@Detalle", $"{segmentos.Count} segmento(s)"), ("@Exito", true), ("@Fecha", HoraLocalRD.Ahora));
            return new { Segmentos = segmentos, Info = r.Count > 1 ? r[1].FirstOrDefault() : null };
        });

        // ---------------- Saldos de comisiones de efectivo
        [HttpGet("saldos")]
        public Task<IActionResult> Saldos() => Leer("Saldos", async () => (await _svc.ConsultarAsync("sp_AdminSaldosCuidadores"))[0]);

        [HttpGet("saldos/{cuidadorId:int}")]
        public Task<IActionResult> MovimientosSaldo(int cuidadorId) => Leer("MovimientosSaldo", async () =>
            (await _svc.ConsultarAsync("sp_AdminMovimientosSaldo", ("@CuidadorId", cuidadorId)))[0]);

        [Authorize(Policy = PoliticasAdmin.Finanzas)]
        [HttpPost("saldos/{cuidadorId:int}/abono")]
        public async Task<IActionResult> RegistrarAbono(int cuidadorId, [FromBody] AbonoDto dto)
        {
            var fila = await _svc.EjecutarAsync("sp_AdminRegistrarAbono", ("@CuidadorId", cuidadorId), ("@Monto", dto.Monto),
                ("@Nota", dto.Nota?.Trim()), ("@AdminId", AdminId), ("@Fecha", HoraLocalRD.Ahora));
            if (fila?["Resultado"]?.ToString() == "MONTO_INVALIDO")
                return BadRequest(new ApiErrorDto("MONTO_INVALIDO", "El monto debe ser mayor que cero y no mayor que lo que debe."));
            return Resultado(fila, "Abono registrado.");
        }

        [HttpGet("config-pagos")]
        public Task<IActionResult> ConfigPagos() => Leer("ConfigPagos", async () => (await _svc.ConsultarAsync("sp_AdminObtenerConfigPagos"))[0].FirstOrDefault() ?? new Fila());

        [Authorize(Policy = PoliticasAdmin.Finanzas)]
        [HttpPut("config-pagos")]
        public async Task<IActionResult> GuardarConfigPagos([FromBody] ConfigPagosDto dto)
        {
            var fila = await _svc.EjecutarAsync("sp_AdminGuardarConfigPagos", ("@TopeDeudaEfectivo", dto.TopeDeudaEfectivo),
                ("@InstruccionesTransferencia", dto.InstruccionesTransferencia?.Trim() ?? ""), ("@AdminId", AdminId), ("@Fecha", HoraLocalRD.Ahora));
            if (fila?["Resultado"]?.ToString() == "MONTO_INVALIDO")
                return BadRequest(new ApiErrorDto("MONTO_INVALIDO", "El tope no puede ser negativo."));
            return Resultado(fila, "Configuración de pagos guardada.");
        }

        // ---------------- Comisión y método de pago
        [HttpGet("comisiones")]
        public Task<IActionResult> Comisiones() => Leer("Comisiones", async () =>
        {
            var r = await _svc.ConsultarAsync("sp_AdminObtenerComisiones");
            return new { General = r[0].FirstOrDefault(), Tipos = r[1] };
        });

        [Authorize(Policy = PoliticasAdmin.Finanzas)]
        [HttpPut("comisiones/general")]
        public async Task<IActionResult> GuardarComisionGeneral([FromBody] PorcentajeDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminGuardarComisionGeneral", ("@Porcentaje", dto.Porcentaje), ("@AdminId", AdminId), ("@Fecha", HoraLocalRD.Ahora)),
                "Comisión general guardada. Aplica a los pagos que se creen desde ahora.");

        [Authorize(Policy = PoliticasAdmin.Finanzas)]
        [HttpPut("comisiones/tipos/{id:int}")]
        public async Task<IActionResult> GuardarComisionTipo(int id, [FromBody] PorcentajeDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminGuardarComisionTipo", ("@TipoId", id), ("@Porcentaje", dto.Porcentaje)), "Comisión del tipo de servicio guardada.");

        [HttpPut("servicios/{id:int}/metodo-pago")]
        public async Task<IActionResult> FijarMetodoPago(int id, [FromBody] MetodoPagoDto dto)
            => Resultado(await _svc.EjecutarAsync("sp_AdminFijarMetodoPago", ("@TrabajoId", id), ("@MetodoPago", dto.MetodoPago)), "Método de pago guardado.");

        // ---------------- Disputas
        [HttpGet("disputas")]
        public Task<IActionResult> Disputas([FromQuery] int? estado) => Leer("Disputas", async () =>
            (await _svc.ConsultarAsync("sp_AdminDisputas", ("@Estado", estado), ("@Id", null), ("@Ahora", HoraLocalRD.Ahora)))[0]);

        [HttpGet("disputas/{id:int}")]
        public async Task<IActionResult> Disputa(int id)
        {
            try
            {
                var filas = (await _svc.ConsultarAsync("sp_AdminDisputas", ("@Estado", null), ("@Id", id), ("@Ahora", HoraLocalRD.Ahora)))[0];
                return filas.Count == 0 ? NotFound(new ApiErrorDto("NO_ENCONTRADO", "La disputa no existe.")) : Ok(filas[0]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en disputa {Id}", id);
                return StatusCode(500, new ApiErrorDto("INTERNAL_ERROR", "Error interno del servidor."));
            }
        }

        [Authorize(Policy = PoliticasAdmin.Soporte)]
        [HttpPost("disputas")]
        public async Task<IActionResult> AbrirDisputa([FromBody] AbrirDisputaDto dto)
        {
            var fila = await _svc.EjecutarAsync("sp_AdminAbrirDisputa", ("@TrabajoId", dto.TrabajoId), ("@Motivo", dto.Motivo.Trim()), ("@AdminId", AdminId), ("@Fecha", HoraLocalRD.Ahora));
            if (fila?["Resultado"]?.ToString() == "OK")
                return Ok(new { Message = "Disputa abierta.", Id = fila["Id"] });
            return Resultado(fila);
        }

        [Authorize(Policy = PoliticasAdmin.Soporte)]
        [HttpPut("disputas/{id:int}/resolver")]
        public async Task<IActionResult> ResolverDisputa(int id, [FromBody] ResolverDisputaDto dto)
        {
            var fila = await _svc.EjecutarAsync("sp_AdminResolverDisputa", ("@DisputaId", id), ("@Resolucion", dto.Resolucion), ("@Monto", dto.Monto),
                ("@Nota", dto.Nota.Trim()), ("@AdminId", AdminId), ("@Fecha", HoraLocalRD.Ahora));
            if (fila?["Resultado"]?.ToString() == "OK")
            {
                var trabajoId = Convert.ToInt32(fila["TrabajoId"]);
                var monto = Convert.ToDecimal(fila["MontoFinal"] ?? 0m);
                var texto = dto.Resolucion switch
                {
                    "completo" => $"Revisamos el servicio #{trabajoId} y se pagará completo al Care Partner.",
                    "parcial" => $"Revisamos el servicio #{trabajoId}: se pagará un monto parcial de RD$ {monto:N2} al Care Partner.",
                    _ => $"Revisamos el servicio #{trabajoId}: se cerró sin pago al Care Partner."
                };
                foreach (var clave in new[] { "ClienteId", "CuidadorId" })
                    if (fila[clave] is int usuarioId)
                    {
                        await _notifier.NotificarAsync(usuarioId, "TrabajoActualizado", new { TrabajoId = trabajoId, Estado = 4 });
                        await _avisos.EnviarAsync(AdminId, usuarioId, "mensaje", "Disputa resuelta", $"{texto} {dto.Nota.Trim()}");
                    }
            }
            return Resultado(fila, "Disputa resuelta. Se avisó al cliente y al Care Partner.");
        }
    }
}
