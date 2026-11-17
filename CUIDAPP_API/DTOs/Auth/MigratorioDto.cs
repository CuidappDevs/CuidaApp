namespace CUIDAPP_API.DTOs.Auth;

public class MigratorioException(string code, string[]? faltantes = null) : Exception(code)
{
    public string Code { get; } = code;
    public string[] Faltantes { get; } = faltantes ?? [];
}

public static class DocumentosMigratorios
{
    public static void ValidarEstatus(int? estatusId, bool extranjero, bool activo)
    {
        if (!activo && estatusId.HasValue) throw new MigratorioException("ESTATUS_MIGRATORIO_INVALIDO");
        if (extranjero && !activo) throw new MigratorioException("ESTATUS_MIGRATORIO_REQUERIDO");
    }

    public static string[] Faltantes(IEnumerable<string> requisitos, IEnumerable<DocumentoExtraDto>? documentos)
    {
        var presentes = (documentos ?? []).Where(d => d != null && !string.IsNullOrWhiteSpace(d.UrlArchivo))
            .Select(d => d.TipoDocumento?.Trim() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
        return requisitos.Where(r => !presentes.Contains(r.Trim())).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
