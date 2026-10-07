namespace CUIDAPP_ADMINISTRATIVO.Models.Cuidador
{
    public class SancionCuidador
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public int AdminId { get; set; }
        public string AdminNombre { get; set; } = "";
        public string Accion { get; set; } = "";
        public string Motivo { get; set; } = "";
        public string Tipo { get; set; } = "";      // INDEFINIDA | TEMPORAL
        public string Estado { get; set; } = "";    // VIGENTE | CUMPLIDA | REVOCADA
        public DateTimeOffset FechaInicioUtc { get; set; }
        public DateTimeOffset? FechaFinUtc { get; set; }
        public int? RevocadaPorAdminId { get; set; }
        public string? RevocadaPorAdminNombre { get; set; }
        public DateTimeOffset? FechaRevocacionUtc { get; set; }
        public bool EstaVigente { get; set; }

        // Temporal vencida que aún no pasó por login: Estado sigue VIGENTE pero ya no rige.
        public bool VencidaPendienteAcceso => Estado == "VIGENTE" && !EstaVigente;

        public string EstadoTexto => Estado switch
        {
            "VIGENTE" when VencidaPendienteAcceso => "Vencida, pendiente de acceso",
            "VIGENTE" => "Vigente",
            "CUMPLIDA" => "Cumplida",
            "REVOCADA" => "Revocada",
            _ => Estado
        };
    }
}
