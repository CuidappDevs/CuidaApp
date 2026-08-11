using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.DTOs.UbicacionCliente;
using CUIDAPP_API.Interfaces.UbicacionCliente;

namespace CUIDAPP_API.Services.UbicacionCliente
{
    public class UbicacionClienteService : IUbicacionClienteService
    {
        private readonly string _connectionString;

        public UbicacionClienteService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<IEnumerable<UbicacionClienteDto>> ObtenerUbicacionesAsync(int clienteId)
        {
            var ubicaciones = new List<UbicacionClienteDto>();

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerUbicacionesCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ClienteId", clienteId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                ubicaciones.Add(new UbicacionClienteDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    ClienteId = Convert.ToInt32(reader["ClienteId"]),
                    Nombre = reader["Nombre"].ToString() ?? "",
                    Direccion = reader["Direccion"].ToString() ?? "",
                    Latitud = Convert.ToDecimal(reader["Latitud"]),
                    Longitud = Convert.ToDecimal(reader["Longitud"]),
                    EsPredeterminada = Convert.ToBoolean(reader["EsPredeterminada"])
                });
            }

            return ubicaciones;
        }

        public async Task<int> CrearUbicacionAsync(CrearUbicacionClienteDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CrearUbicacionCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ClienteId", dto.ClienteId);
            command.Parameters.AddWithValue("@Nombre", dto.Nombre);
            command.Parameters.AddWithValue("@Direccion", dto.Direccion);
            command.Parameters.AddWithValue("@Latitud", dto.Latitud);
            command.Parameters.AddWithValue("@Longitud", dto.Longitud);
            command.Parameters.AddWithValue("@EsPredeterminada", dto.EsPredeterminada);

            await connection.OpenAsync();
            var id = await command.ExecuteScalarAsync();
            return Convert.ToInt32(id);
        }

        public async Task<bool> ActualizarUbicacionAsync(ActualizarUbicacionClienteDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ActualizarUbicacionCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Id", dto.Id);
            command.Parameters.AddWithValue("@ClienteId", dto.ClienteId);
            command.Parameters.AddWithValue("@Nombre", dto.Nombre);
            command.Parameters.AddWithValue("@Direccion", dto.Direccion);
            command.Parameters.AddWithValue("@Latitud", dto.Latitud);
            command.Parameters.AddWithValue("@Longitud", dto.Longitud);
            command.Parameters.AddWithValue("@EsPredeterminada", dto.EsPredeterminada);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            return Convert.ToInt32(filasAfectadas) > 0;
        }

        public async Task<bool> EliminarUbicacionAsync(EliminarUbicacionClienteDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_EliminarUbicacionCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Id", dto.Id);
            command.Parameters.AddWithValue("@ClienteId", dto.ClienteId);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            return Convert.ToInt32(filasAfectadas) > 0;
        }
    }
}
