using System.ComponentModel.DataAnnotations;

namespace CUIDAPP_API.DTOs.AdminOperaciones
{
    /// <summary>Fila genérica de un resultado del panel (columna → valor), tal como la devuelve el SP.</summary>
    public class Fila : Dictionary<string, object?>
    {
        public Fila() : base(StringComparer.OrdinalIgnoreCase) { }
    }

    public class MotivoAdminDto
    {
        [Required, StringLength(400, MinimumLength = 5)]
        public string Motivo { get; set; } = "";
    }

    public class AvisoMasivoDto
    {
        /// <summary>0 todos, 2 clientes, 3 Care Partners.</summary>
        [Range(0, 3)]
        public int Destino { get; set; }
        [Required, StringLength(120, MinimumLength = 3)]
        public string Titulo { get; set; } = "";
        [Required, StringLength(500, MinimumLength = 3)]
        public string Mensaje { get; set; } = "";
        /// <summary>Idioma de la app de los destinatarios: null todos, "es", "en" o "ht".</summary>
        [RegularExpression("^(es|en|ht)$")]
        public string? Idioma { get; set; }
    }

    public class NivelAdminDto
    {
        [Range(1, 3)]
        public int Nivel { get; set; }
    }

    public class TipoTrabajoAdminDto
    {
        public int? Id { get; set; }
        [Required, StringLength(100)]
        public string Nombre { get; set; } = "";
        [StringLength(500)]
        public string? Descripcion { get; set; }
        [StringLength(100)]
        public string? Icono { get; set; }
        public bool Activo { get; set; } = true;
    }

    public class MotivoCancelacionAdminDto
    {
        public int? Id { get; set; }
        [Required, StringLength(200)]
        public string Descripcion { get; set; } = "";
        public bool Activo { get; set; } = true;
        public int OrdenVisual { get; set; }
    }

    public class NacionalidadAdminDto
    {
        public int? Id { get; set; }
        [Required, StringLength(100)]
        public string Nombre { get; set; } = "";
        [Required, StringLength(100)]
        public string Pais { get; set; } = "";
        [Required, StringLength(2, MinimumLength = 2)]
        public string CodigoIso { get; set; } = "";
        public bool Activo { get; set; } = true;
    }

    public class EstatusMigratorioAdminDto
    {
        public int? Id { get; set; }
        [Required, StringLength(100)]
        public string Nombre { get; set; } = "";
        public bool Activo { get; set; } = true;
    }

    public class RequisitoAdminDto
    {
        public int EstatusMigratorioId { get; set; }
        [Required, StringLength(100)]
        public string TipoDocumento { get; set; } = "";
    }
}
