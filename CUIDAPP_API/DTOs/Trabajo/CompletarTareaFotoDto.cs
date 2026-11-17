using System.ComponentModel.DataAnnotations;
namespace CUIDAPP_API.DTOs.Trabajo;

public class CompletarTareaFotoDto
{
    [StringLength(500)] public string? FotoUrl { get; set; }
}
