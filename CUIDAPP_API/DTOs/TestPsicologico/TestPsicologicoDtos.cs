namespace CUIDAPP_API.DTOs.TestPsicologico
{
    /// <summary>
    /// Estado del candidato frente al test del tipo de trabajo elegido:
    /// SIN_TEST | LIBRE | APROBADO (ya lo pasó; Token sirve para el registro) | BLOQUEADO | DESCARTADO.
    /// </summary>
    public class EstadoTestDto
    {
        public string Estado { get; set; } = "SIN_TEST";
        public int? TestId { get; set; }
        public Guid? Token { get; set; }
        public int? MinutosLimite { get; set; }
        public int? TotalPreguntas { get; set; }
        public DateTime? BloqueadoHasta { get; set; }
    }

    public class IniciarTestDto
    {
        public int TipoTrabajoId { get; set; }
        public string? Nombre { get; set; }
        public string? Email { get; set; }
        public string? Documento { get; set; }
        public string? Telefono { get; set; }
    }

    /// <summary>Test listo para responder: las opciones no dicen cuál es la correcta y llegan mezcladas.</summary>
    public class TestParaResolverDto
    {
        public Guid Token { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int MinutosLimite { get; set; }
        public List<PreguntaTestDto> Preguntas { get; set; } = new();
    }

    public class PreguntaTestDto
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public List<OpcionTestDto> Opciones { get; set; } = new();
    }

    public class OpcionTestDto
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
    }

    public class FinalizarTestDto
    {
        public Guid Token { get; set; }
        public List<RespuestaTestDto> Respuestas { get; set; } = new();
    }

    public class RespuestaTestDto
    {
        public int PreguntaId { get; set; }
        public int OpcionId { get; set; }
    }

    /// <summary>Resultado: 1 Aprobado, 2 Revisión manual, 3 Bloqueado, 4 Descartado.</summary>
    public class ResultadoTestDto
    {
        public int Resultado { get; set; }
        public decimal Porcentaje { get; set; }
        public int Correctas { get; set; }
        public int TotalPreguntas { get; set; }
        public DateTime? BloqueadoHasta { get; set; }
    }

    // ---------- Panel

    public class TestAdminDto
    {
        public int? Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int MinutosLimite { get; set; }
        public decimal PorcentajeAprobacion { get; set; }
        public decimal PorcentajeRevision { get; set; }
        public int DiasBloqueo { get; set; }
        public bool Activo { get; set; }
        public List<int> Tipos { get; set; } = new();
        public List<PreguntaAdminDto> Preguntas { get; set; } = new();
    }

    public class PreguntaAdminDto
    {
        public string Texto { get; set; } = string.Empty;
        public bool EsCritica { get; set; }
        public List<OpcionAdminDto> Opciones { get; set; } = new();
    }

    public class OpcionAdminDto
    {
        public string Texto { get; set; } = string.Empty;
        public bool EsCorrecta { get; set; }
        public bool EsRedFlag { get; set; }
    }
}
