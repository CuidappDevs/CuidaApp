using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.Busqueda;
using CUIDAPP_API.Interfaces.Busqueda;

namespace CUIDAPP_API.Services.Busqueda
{
    public class BusquedaService : IBusquedaService
    {
        private readonly string _connectionString;

        public BusquedaService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<IEnumerable<ServicioCercanoDto>> ObtenerServiciosCercanosAsync(decimal latitud, decimal longitud, decimal radioKm)
        {
            var servicios = new List<ServicioCercanoDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerServiciosCercanos", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Latitud", latitud);
            command.Parameters.AddWithValue("@Longitud", longitud);
            command.Parameters.AddWithValue("@RadioKm", radioKm);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                servicios.Add(new ServicioCercanoDto
                {
                    Especialidad = reader["Especialidad"].ToString() ?? "",
                    CuidadoresDisponibles = Convert.ToInt32(reader["CuidadoresDisponibles"]),
                    TarifaDesde = Convert.ToDecimal(reader["TarifaDesde"])
                });
            }

            return servicios;
        }

        public async Task<IEnumerable<CuidadorCercanoDto>> ObtenerCuidadoresPorServicioAsync(string especialidad, decimal latitud, decimal longitud, decimal radioKm)
        {
            var cuidadores = new List<CuidadorCercanoDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerCuidadoresPorServicio", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Especialidad", especialidad);
            command.Parameters.AddWithValue("@Latitud", latitud);
            command.Parameters.AddWithValue("@Longitud", longitud);
            command.Parameters.AddWithValue("@RadioKm", radioKm);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                cuidadores.Add(new CuidadorCercanoDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    NombreCompleto = reader["NombreCompleto"].ToString() ?? "",
                    FotoUrl = reader["FotoUrl"] as string,
                    Especialidad = reader["Especialidad"].ToString() ?? "",
                    TarifaHora = Convert.ToDecimal(reader["TarifaHora"]),
                    Bio = reader["Bio"] as string,
                    Latitud = Convert.ToDecimal(reader["Latitud"]),
                    Longitud = Convert.ToDecimal(reader["Longitud"]),
                    DistanciaKm = Convert.ToDouble(reader["DistanciaKm"])
                });
            }

            return cuidadores;
        }
    }
}
