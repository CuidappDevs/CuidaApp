namespace CUIDAPP.Models.Busqueda
{
    public class CuidadorCercano
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string? FotoUrl { get; set; }
        public string Especialidad { get; set; } = string.Empty;
        public decimal TarifaHora { get; set; }
        public string? Bio { get; set; }
        public decimal Latitud { get; set; }
        public decimal Longitud { get; set; }
        public double DistanciaKm { get; set; }
        /// <summary>False si por ahora no puede tomar servicios en efectivo (deuda de comisiones en el tope).</summary>
        public bool PuedeAceptarEfectivo { get; set; } = true;
    }
}
