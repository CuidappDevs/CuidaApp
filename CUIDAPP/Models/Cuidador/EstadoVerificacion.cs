namespace CUIDAPP.Models.Cuidador
{
    public class EstadoVerificacion
    {
        public int EstadoAprobacion { get; set; } // 1=Pendiente, 2=Aprobado, 3=Rechazado
        public List<DocumentoEstado> Documentos { get; set; } = new();

        /// <summary>
        /// Puede entrar a su panel: cuenta aprobada, o todos sus documentos aprobados
        /// (ninguno pendiente ni rechazado) y la cuenta no está rechazada.
        /// </summary>
        public bool PuedeTrabajar =>
            EstadoAprobacion == 2 ||
            (EstadoAprobacion != 3 && Documentos.Count > 0 && Documentos.All(d => d.Estado == 2));
    }
}
