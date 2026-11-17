using System.ComponentModel.DataAnnotations;
namespace CUIDAPP_API.DTOs.Contrato;
public class ProponerAnexoDto
{
 public int UsuarioId {get;set;}
 [Range(0.01,99999999)] public decimal? TarifaHora {get;set;}
 public DateTime FechaInicio {get;set;}
 public List<FranjaContratoDto>? Horario {get;set;}
}
