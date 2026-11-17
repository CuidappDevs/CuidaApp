namespace CUIDAPP.Models.Contrato;

/// <summary>Candidata para cubrir una jornada de un contrato fijo (ausencia de la titular).</summary>
public class ReemplazoContrato
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = "";
    public string? FotoUrl { get; set; }
    public decimal TarifaHora { get; set; }
    public double DistanciaKm { get; set; }
    /// <summary>Ya completó al menos un servicio para este mismo cliente.</summary>
    public bool YaVino { get; set; }
    public int VecesConCliente { get; set; }
    /// <summary>Promedio de lo que este cliente le calificó (null si nunca la calificó).</summary>
    public decimal? CalificacionConCliente { get; set; }
}

/// <summary>Una jornada del contrato que cubrió otra Care Partner.</summary>
public class RelevoContrato
{
    public int TrabajoId { get; set; }
    public DateTime Fecha { get; set; }
    public int CuidadorId { get; set; }
    public string Nombre { get; set; } = "";
    public string? FotoUrl { get; set; }
    public int Estado { get; set; }
    public int? Calificacion { get; set; }
}
