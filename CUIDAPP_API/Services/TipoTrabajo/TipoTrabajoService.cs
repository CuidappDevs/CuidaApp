using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.DTOs.TipoTrabajo;
using CUIDAPP_API.Interfaces.TipoTrabajo;

namespace CUIDAPP_API.Services.TipoTrabajo
{
    public class TipoTrabajoService : ITipoTrabajoService
    {
        private readonly string _connectionString;

        public TipoTrabajoService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<IEnumerable<TipoTrabajoDto>> ObtenerTodosAsync()
        {
            var tipos = new List<TipoTrabajoDto>();

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerTiposTrabajos", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tipos.Add(new TipoTrabajoDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Nombre = reader["Nombre"].ToString() ?? "",
                    Descripcion = reader["Descripcion"] == DBNull.Value ? null : reader["Descripcion"].ToString(),
                    Icono = reader["Icono"] == DBNull.Value ? null : reader["Icono"].ToString(),
                    Activo = Convert.ToBoolean(reader["Activo"])
                });
            }

            return tipos;
        }
    }
}
