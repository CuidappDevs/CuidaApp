using System.ComponentModel.DataAnnotations;

namespace CUIDAPP_API.DTOs.Contrato
{
    public class SolicitarPermisoContratoDto
    {
        public int ClienteId { get; set; }
        [Required, StringLength(600, MinimumLength = 10)]
        public string Motivo { get; set; } = "";
        [StringLength(400)]
        public string? CedulaUrl { get; set; }
        [StringLength(300)]
        public string? Referencia { get; set; }
    }

    public class SolicitarDomicilioContratoDto
    {
        public int ClienteId { get; set; }
        public int UbicacionId { get; set; }
        [Required, StringLength(400)]
        public string ComprobanteUrl { get; set; } = "";
        [Required, StringLength(300, MinimumLength = 3)]
        public string QuienVive { get; set; } = "";
    }

    public class CuidadorAceptaContratosDto
    {
        public bool Acepta { get; set; }
    }

    /// <summary>Franja del horario semanal. DiaSemana: 0 domingo … 6 sábado; horas "HH:mm".</summary>
    public class FranjaContratoDto
    {
        [Range(0, 6)]
        public int DiaSemana { get; set; }
        [Required]
        public string HoraInicio { get; set; } = "";
        [Required]
        public string HoraFin { get; set; } = "";
    }

    public class TareaContratoDto
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public string Descripcion { get; set; } = "";
        public bool RequiereFoto { get; set; }
    }

    public class EditarTareasContratoDto
    {
        public int UsuarioId { get; set; }
        [Required, MaxLength(20)]
        public List<TareaContratoDto> Tareas { get; set; } = [];
    }

    public class ProponerContratoDto
    {
        public bool Dormida { get; set; }
        [MaxLength(20)] public List<TareaContratoDto>? Tareas { get; set; }
        public int ClienteId { get; set; }
        public int CuidadorId { get; set; }
        public int UbicacionId { get; set; }
        [Required, StringLength(100)]
        public string TipoServicio { get; set; } = "";
        public decimal TarifaHora { get; set; }
        [Required]
        public string MetodoPago { get; set; } = "";
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        [Required, MinLength(1)]
        public List<FranjaContratoDto> Horario { get; set; } = new();
        [StringLength(600)]
        public string? Nota { get; set; }
        /// <summary>Desde cuándo trabaja con la familia (antigüedad previa a CuidApp); sale en el PDF.</summary>
        public DateTime? TrabajaDesde { get; set; }
    }

    /// <summary>"Invitar a mi cuidadora": alguien que ya trabaja con la familia fuera de CuidApp.</summary>
    public class CrearInvitacionDto
    {
        public int ClienteId { get; set; }
        [Required, StringLength(120, MinimumLength = 2)]
        public string Nombre { get; set; } = "";
        [StringLength(30)]
        public string? Telefono { get; set; }
        [StringLength(150)]
        public string? Email { get; set; }
        public DateTime? TrabajaDesde { get; set; }
    }

    public class CancelarInvitacionDto
    {
        public int ClienteId { get; set; }
    }

    /// <summary>Accion: aceptar | rechazar | contraproponer | retirar. Los demás campos solo para contraproponer.</summary>
    public class ResponderContratoDto
    {
        public bool? Dormida { get; set; }
        [MaxLength(20)] public List<TareaContratoDto>? Tareas { get; set; }
        public int UsuarioId { get; set; }
        [Required]
        public string Accion { get; set; } = "";
        public decimal? TarifaHora { get; set; }
        public DateTime? FechaInicio { get; set; }
        public List<FranjaContratoDto>? Horario { get; set; }
        [StringLength(600)]
        public string? Nota { get; set; }
    }

    public class RenovarContratoDto
    {
        public int UsuarioId { get; set; }
        public DateTime FechaFin { get; set; }
    }

    public class CodigoFirmaDto
    {
        public int UsuarioId { get; set; }
    }

    /// <summary>
    /// Firma: los trazos se mandan como datos de un path SVG (solo comandos M/L y números) y el lienzo
    /// donde se dibujaron; el servidor arma el SVG, así nunca se guarda un SVG arbitrario.
    /// </summary>
    public class FirmarContratoDto
    {
        public int UsuarioId { get; set; }
        [Required, StringLength(6, MinimumLength = 6)]
        public string Codigo { get; set; } = "";
        [Required, StringLength(200000, MinimumLength = 10)]
        public string Trazos { get; set; } = "";
        [Range(10, 4000)]
        public double Ancho { get; set; }
        [Range(10, 4000)]
        public double Alto { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
    }

    public class AusenciaContratoDto
    {
        public bool Emergencia { get; set; }
        public int UsuarioId { get; set; }
        public DateTime Fecha { get; set; }
        [StringLength(300)]
        public string? Motivo { get; set; }
    }

    /// <summary>Accion: pausar | reanudar | terminar (terminar exige motivo).</summary>
    public class EstadoContratoDto
    {
        public int UsuarioId { get; set; }
        [Required]
        public string Accion { get; set; } = "";
        [StringLength(600)]
        public string? Motivo { get; set; }
    }

    /// <summary>Accion del panel: aprobar | rechazar | revocar.</summary>
    public class RevisionContratoDto
    {
        [Required]
        public string Accion { get; set; } = "";
        [StringLength(600)]
        public string? Comentario { get; set; }
    }
}
