using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.DTOs.Nacionalidad;
using CUIDAPP_API.Interfaces.Nacionalidad;

namespace CUIDAPP_API.Services.Nacionalidad
{
    public class NacionalidadService : INacionalidadService
    {
        private readonly string _connectionString;

        public NacionalidadService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<IEnumerable<NacionalidadDto>> ObtenerActivasAsync()
        {
            var nacionalidades = new List<NacionalidadDto>();

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerNacionalidades", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                nacionalidades.Add(new NacionalidadDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Nombre = reader["Nombre"].ToString() ?? "",
                    Pais = reader["Pais"].ToString() ?? "",
                    CodigoIso = reader["CodigoIso"].ToString() ?? ""
                });
            }

            return nacionalidades;
        }
    }
}
