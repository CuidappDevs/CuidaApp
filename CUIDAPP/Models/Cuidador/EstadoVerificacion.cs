namespace CUIDAPP.Models.Cuidador
{
    public class EstadoVerificacion
    {
        public int EstadoAprobacion { get; set; } // 1=Pendiente, 2=Aprobado, 3=Rechazado
        public List<DocumentoEstado> Documentos { get; set; } = new();
    }
}
