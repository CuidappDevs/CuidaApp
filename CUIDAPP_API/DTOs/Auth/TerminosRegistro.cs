using Microsoft.Extensions.Configuration;

namespace CUIDAPP_API.DTOs.Auth;

public sealed class TerminosRegistroException(string motivo, int statusCode, string message) : Exception(message)
{
    public string Motivo { get; } = motivo;
    public int StatusCode { get; } = statusCode;
}

public static class TerminosRegistro
{
    public static void Validar(string? version, IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new TerminosRegistroException("TERMINOS_REQUERIDOS", 400, "Debes aceptar los Términos y Condiciones.");
        var vigente = config["Terminos:VersionVigente"];
        if (string.IsNullOrWhiteSpace(vigente) || vigente.Length > 20)
            throw new InvalidOperationException("Configura Terminos:VersionVigente (máximo 20 caracteres).");
        if (!string.Equals(version, vigente, StringComparison.Ordinal))
            throw new TerminosRegistroException("TERMINOS_DESACTUALIZADOS", 409, "Hay una versión nueva de los Términos y Condiciones. Vuelve a abrir el registro.");
    }
}
