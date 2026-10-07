namespace CUIDAPP.Models.Cuidador
{
    /// <summary>Tipo de trabajo del registro de cuidadores (tabla TiposTrabajos).</summary>
    public class TipoTrabajo
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? Icono { get; set; }
        public bool Activo { get; set; }
    }
}
