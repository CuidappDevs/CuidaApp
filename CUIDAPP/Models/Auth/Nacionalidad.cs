namespace CUIDAPP.Models.Auth
{
    /// <summary>Nacionalidad del catálogo (tabla Nacionalidades), elegida en el registro.</summary>
    public class Nacionalidad
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Pais { get; set; } = string.Empty;
        public string CodigoIso { get; set; } = string.Empty;
    }
}
