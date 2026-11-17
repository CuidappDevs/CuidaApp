namespace CUIDAPP_ADMINISTRATIVO.Models.Panel;
public class AlertaContrato
{
    public int ContratoId { get; set; }
    public string Tipo { get; set; } = "";
    public int Cantidad { get; set; }
    public string Texto => Tipo switch
    {
        "AUSENCIAS" => "Ausencias de la Care Partner en los últimos 30 días",
        "CANCELACIONES" => "Cancelaciones en los últimos 30 días",
        "RENOVACION" => "Vence pronto y no tiene renovación firmada",
        "ANEXO" => "Anexos pendientes por más de 72 horas",
        "PDF" => "PDF ausente o con integridad incorrecta",
        _ => Tipo
    };
}
