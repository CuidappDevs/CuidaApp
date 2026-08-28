namespace CUIDAPP.Models.Trabajo
{
    public class ActividadTrabajo
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public string Descripcion { get; set; } = "";
        public DateTime FechaHora { get; set; }
    }
}
