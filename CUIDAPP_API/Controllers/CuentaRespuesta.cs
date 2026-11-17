using CUIDAPP_API.DTOs.AdminOperaciones;
using Microsoft.AspNetCore.Mvc;

namespace CUIDAPP_API.Controllers;

internal static class CuentaRespuesta
{
    public static IActionResult Error(ControllerBase c,Fila? fila)
    {
        var motivo=fila?["Resultado"]?.ToString() ?? "ERROR_INTERNO";
        var mensaje=motivo switch {
            "NO_ENCONTRADO"=>"No se encontró el registro solicitado.",
            "ESTADO_INVALIDO"=>"El estado actual no permite esta operación.",
            "MONTO_INVALIDO"=>"El monto debe ser válido, no negativo y tener hasta dos decimales.",
            "NOTA_REQUERIDA"=>"Debes indicar una nota o motivo.",
            "COMPROBANTE_REQUERIDO"=>"Debes indicar la URL del comprobante.",
            "CORREO_ERROR"=>"No se pudo enviar el código por correo. Inténtalo de nuevo más tarde.",
            "CODIGO_INVALIDO"=>"El código es incorrecto, venció o agotó los cinco intentos.",
            "SOLICITUD_EXISTENTE"=>"La cuenta ya tiene una solicitud de eliminación.",
            "ESPERA_CODIGO"=>"Espera al menos un minuto antes de solicitar otro código.",
            "BLOQUEOS_PENDIENTES"=>"Debes resolver los asuntos pendientes antes de eliminar la cuenta.",
            _=>"No se pudo completar la operación." };
        return new ObjectResult(new { motivo,message=mensaje }) { StatusCode=motivo switch {
            "NO_ENCONTRADO"=>404,"ERROR_INTERNO"=>500,"CORREO_ERROR"=>503,"ESPERA_CODIGO"=>429,
            "SOLICITUD_EXISTENTE" or "BLOQUEOS_PENDIENTES" or "ESTADO_INVALIDO"=>409,_=>400 } };
    }
}
