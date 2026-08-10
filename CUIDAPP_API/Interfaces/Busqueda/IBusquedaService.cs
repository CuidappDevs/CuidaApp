using CUIDAPP_API.DTOs.Busqueda;

namespace CUIDAPP_API.Interfaces.Busqueda
{
    public interface IBusquedaService
    {
        Task<IEnumerable<ServicioCercanoDto>> ObtenerServiciosCercanosAsync(decimal latitud, decimal longitud, decimal radioKm);
        Task<IEnumerable<CuidadorCercanoDto>> ObtenerCuidadoresPorServicioAsync(string especialidad, decimal latitud, decimal longitud, decimal radioKm);
        Task<IEnumerable<CuidadorMapaDto>> ObtenerCuidadoresCercanosMapaAsync(decimal latitud, decimal longitud, decimal radioKm);
    }
}
