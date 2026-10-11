namespace CUIDAPP.Models.Cuidador
{
    /// <summary>Lo que el cuidador le debe a CuidApp por comisiones de servicios cobrados en efectivo.</summary>
    public class SaldoCuidador
    {
        public decimal Saldo { get; set; }
        public decimal Tope { get; set; }
        public bool PuedeAceptarEfectivo { get; set; } = true;
    }
}
