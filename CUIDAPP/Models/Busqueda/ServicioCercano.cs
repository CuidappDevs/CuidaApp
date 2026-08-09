namespace CUIDAPP.Models.Busqueda
{
    public class ServicioCercano
    {
        public string Especialidad { get; set; } = string.Empty;
        public int CuidadoresDisponibles { get; set; }
        public decimal TarifaDesde { get; set; }
    }
}
