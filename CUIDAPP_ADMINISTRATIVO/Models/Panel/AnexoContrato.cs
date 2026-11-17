namespace CUIDAPP_ADMINISTRATIVO.Models.Panel;
public class AnexoContrato
{
 public int Id {get;set;} public int PropuestoPor {get;set;} public int Estado {get;set;}
 public decimal TarifaHora {get;set;} public string Horario {get;set;} = "";
 public DateTime FechaInicio {get;set;} public DateTime FechaCreacion {get;set;}
 public int YoFirme {get;set;} public int? DocumentoId {get;set;}
}
