namespace CUIDAPP_API.DTOs.Nacionalidad
{
    public class NacionalidadDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Pais { get; set; } = string.Empty;
        public string CodigoIso { get; set; } = string.Empty;
    }
}
