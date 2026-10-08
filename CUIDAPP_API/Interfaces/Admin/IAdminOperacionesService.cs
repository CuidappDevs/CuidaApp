using CUIDAPP_API.DTOs.AdminOperaciones;

namespace CUIDAPP_API.Interfaces.Admin
{
    /// <summary>Lecturas y acciones del panel: dashboard, servicios, verificación, mapa, finanzas, catálogos, auditoría y avisos.</summary>
    public interface IAdminOperacionesService
    {
        /// <summary>Ejecuta un SP y devuelve cada resultado como lista de filas.</summary>
        Task<List<List<Fila>>> ConsultarAsync(string sp, params (string Nombre, object? Valor)[] parametros);

        /// <summary>Ejecuta un SP de escritura que devuelve una fila con "Resultado" (OK o un código de error).</summary>
        Task<Fila?> EjecutarAsync(string sp, params (string Nombre, object? Valor)[] parametros);
    }
}
