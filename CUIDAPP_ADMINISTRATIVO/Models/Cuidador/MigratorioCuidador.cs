namespace CUIDAPP_ADMINISTRATIVO.Models.Cuidador
{
    /// <summary>Estatus migratorio de una Care Partner extranjera y sus documentos (GET admin/cuidadores/{id}/migratorio).</summary>
    public class MigratorioCuidador
    {
        public bool EsExtranjero { get; set; }
        public string? Nacionalidad { get; set; }
        public int? EstatusId { get; set; }
        public string? Estatus { get; set; }
        public List<DocumentoMigratorio> Documentos { get; set; } = new();
        public List<string> Faltantes { get; set; } = new();
        public bool PuedeAprobar { get; set; } = true;
    }

    public class DocumentoMigratorio
    {
        public string Tipo { get; set; } = "";
        /// <summary>1 pendiente, 2 aprobado, 3 rechazado (como DocumentosCuidador).</summary>
        public int? Estado { get; set; }
        public DateTime? Vencimiento { get; set; }
        public string? Url { get; set; }
        public bool Presente { get; set; }
    }

    /// <summary>Resultado de aprobar o rechazar: si la API lo bloquea, trae el código y los documentos que faltan.</summary>
    public record ResultadoEstadoCuidador(bool Ok, string? Codigo, List<string> Faltantes);
}
