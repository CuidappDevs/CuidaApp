using CUIDAPP_API.DTOs.Nacionalidad;

namespace CUIDAPP_API.Interfaces.Nacionalidad
{
    public interface INacionalidadService
    {
        /// <summary>Nacionalidades activas (tabla Nacionalidades) para el registro.</summary>
        Task<IEnumerable<NacionalidadDto>> ObtenerActivasAsync();
    }
}
