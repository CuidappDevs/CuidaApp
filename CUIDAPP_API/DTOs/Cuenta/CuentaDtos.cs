using System.ComponentModel.DataAnnotations;

namespace CUIDAPP_API.DTOs.Cuenta;

public class ComprobanteDeudaDto
{
    [Range(1, int.MaxValue)] public int ClienteId { get; set; }
    [Required, StringLength(1000)] public string ComprobanteUrl { get; set; } = "";
    [StringLength(1000)] public string? Nota { get; set; }
}
public class ObjetarDeudaDto
{
    [Range(1, int.MaxValue)] public int ClienteId { get; set; }
    [Required, StringLength(1000)] public string Motivo { get; set; } = "";
}
public class NotaCuentaDto
{
    [StringLength(1000)] public string? Nota { get; set; }
}
public class AjustarDeudaDto : NotaCuentaDto
{
    // Cero condona la deuda: exigir un valor explícito evita condonaciones por omisión.
    [Required] public decimal? Monto { get; set; }
}
public class UsuarioEliminacionDto
{
    [Range(1, int.MaxValue)] public int UsuarioId { get; set; }
}
public class ConfirmarEliminacionDto : UsuarioEliminacionDto
{
    [Required, RegularExpression("^[0-9]{6}$")] public string Codigo { get; set; } = "";
    [StringLength(1000)] public string? Motivo { get; set; }
}
public class CredencialesEliminacionDto
{
    [Required, EmailAddress, StringLength(150)] public string Correo { get; set; } = "";
    [Required, StringLength(1000)] public string Contrasena { get; set; } = "";
    [StringLength(1000)] public string? Motivo { get; set; }
}
public class ConfirmarWebEliminacionDto : CredencialesEliminacionDto
{
    [Required, RegularExpression("^[0-9]{6}$")] public string Codigo { get; set; } = "";
}
// UsuarioId es opcional por compatibilidad con la recuperación desde el login.
// Siempre se verifican correo y contraseña; un identificador por sí solo nunca autoriza cancelar.
public class CancelarEliminacionDto : CredencialesEliminacionDto
{
    public int? UsuarioId { get; set; }
}

public class AbrirBovedaDto
{
    [Required, StringLength(400,MinimumLength=10)] public string Motivo { get; set; } = "";
}
