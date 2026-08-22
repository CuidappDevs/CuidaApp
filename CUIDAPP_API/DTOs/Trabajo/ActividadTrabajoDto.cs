namespace CUIDAPP_API.DTOs.Trabajo
{
    public class ActividadTrabajoDto
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public required string Descripcion { get; set; }
        public DateTime FechaHora { get; set; }
    }

    public class AgregarActividadTrabajoDto
    {
        public int TrabajoId { get; set; }
        public required string Descripcion { get; set; }
    }

    public class AlertaGeocercaDto
    {
        public int TrabajoId { get; set; }
        public double DistanciaMetros { get; set; }
    }
}
