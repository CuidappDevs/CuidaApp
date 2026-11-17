namespace CUIDAPP.Models.Trabajo
{
    public class TareaTrabajo
    {
        public int Id { get; set; }
        public int TrabajoId { get; set; }
        public string Descripcion { get; set; } = "";
        public int Orden { get; set; }
        public bool Completada { get; set; }
        public DateTime? FechaCompletada { get; set; }
        /// <summary>Tarea del checklist de un contrato que pide foto como evidencia al marcarla.</summary>
        public bool RequiereFoto { get; set; }
        public string? FotoUrl { get; set; }
    }
}
