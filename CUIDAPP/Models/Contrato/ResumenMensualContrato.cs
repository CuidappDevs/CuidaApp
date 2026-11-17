namespace CUIDAPP.Models.Contrato;
public class ResumenMensualContrato
{
 public int Completadas { get; set; }
 public int NoIniciadas { get; set; }
 public int Canceladas { get; set; }
 public int CanceladasCliente { get; set; }
 public int CanceladasCuidador { get; set; }
 public int CanceladasSinAtribuir { get; set; }
 public int Ausencias { get; set; }
 public decimal HorasTrabajadas { get; set; }
}
