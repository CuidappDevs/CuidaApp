namespace CUIDAPP.Models.Auth
{
    /// <summary>
    /// Estado del candidato frente al test del tipo de trabajo elegido en el registro:
    /// SIN_TEST | LIBRE | APROBADO (ya lo pasó; Token sirve para registrarse) | BLOQUEADO | DESCARTADO.
    /// </summary>
    public class EstadoTest
    {
        public string Estado { get; set; } = "SIN_TEST";
        public Guid? Token { get; set; }
        public int? MinutosLimite { get; set; }
        public int? TotalPreguntas { get; set; }
        public DateTime? BloqueadoHasta { get; set; }
    }

    public class IniciarTestRequest
    {
        public int TipoTrabajoId { get; set; }
        public string? Nombre { get; set; }
        public string? Email { get; set; }
        public string? Documento { get; set; }
        public string? Telefono { get; set; }
    }

    public class TestParaResolver
    {
        public Guid Token { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int MinutosLimite { get; set; }
        public List<PreguntaTest> Preguntas { get; set; } = new();
    }

    public class PreguntaTest
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
        public List<OpcionTest> Opciones { get; set; } = new();
    }

    public class OpcionTest
    {
        public int Id { get; set; }
        public string Texto { get; set; } = string.Empty;
    }

    public class FinalizarTestRequest
    {
        public Guid Token { get; set; }
        public List<RespuestaTest> Respuestas { get; set; } = new();
    }

    public class RespuestaTest
    {
        public int PreguntaId { get; set; }
        public int OpcionId { get; set; }
    }

    /// <summary>Resultado: 1 Aprobado, 2 Revisión manual, 3 Bloqueado, 4 Descartado.</summary>
    public class ResultadoTest
    {
        public const int Aprobado = 1, Revision = 2, Bloqueado = 3, Descartado = 4;

        public int Resultado { get; set; }
        public decimal Porcentaje { get; set; }
        public int Correctas { get; set; }
        public int TotalPreguntas { get; set; }
        public DateTime? BloqueadoHasta { get; set; }

        public bool PuedeSeguir => Resultado is Aprobado or Revision;
    }
}
