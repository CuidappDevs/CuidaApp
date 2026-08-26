namespace CUIDAPP.Models
{
    public class Notificacion
    {
        public string Titulo { get; set; } = "";
        public string Mensaje { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; } = "general"; // mensaje | solicitud | trabajo | geocerca | general
        public int? TrabajoId { get; set; }
        public bool Leida { get; set; }
    }
}
