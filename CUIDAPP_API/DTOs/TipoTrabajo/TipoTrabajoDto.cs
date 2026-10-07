namespace CUIDAPP_API.DTOs.TipoTrabajo
{
    public class TipoTrabajoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? Icono { get; set; }
        public bool Activo { get; set; }
    }
}
