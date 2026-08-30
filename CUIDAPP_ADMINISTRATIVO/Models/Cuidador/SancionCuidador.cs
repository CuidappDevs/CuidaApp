namespace CUIDAPP_ADMINISTRATIVO.Models.Cuidador
{
    public class SancionCuidador
    {
        public int Id { get; set; }
        public string Accion { get; set; } = "";
        public string? Motivo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string AdminNombre { get; set; } = "";
    }
}
