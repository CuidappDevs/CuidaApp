using CUIDAPP_API.DTOs.TipoTrabajo;

namespace CUIDAPP_API.Interfaces.TipoTrabajo
{
    public interface ITipoTrabajoService
    {
        /// <summary>Todos los tipos de trabajo (activos e inactivos); la app muestra los inactivos como "No disponible".</summary>
        Task<IEnumerable<TipoTrabajoDto>> ObtenerTodosAsync();
    }
}
