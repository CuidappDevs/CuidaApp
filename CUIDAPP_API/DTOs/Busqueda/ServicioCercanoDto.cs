namespace CUIDAPP_API.DTOs.Busqueda
{
    public class ServicioCercanoDto
    {
        public required string Especialidad { get; set; }
        public int CuidadoresDisponibles { get; set; }
        public decimal TarifaDesde { get; set; }
    }
}
