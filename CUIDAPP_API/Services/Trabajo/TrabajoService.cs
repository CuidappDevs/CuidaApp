using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.Trabajo;
using CUIDAPP_API.Interfaces.Trabajo;

namespace CUIDAPP_API.Services.Trabajo
{
    public class TrabajoService : ITrabajoService
    {
        private readonly string _connectionString;

        public TrabajoService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<int> CrearTrabajoAsync(CrearTrabajoDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CrearTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@ClienteId", dto.ClienteId);
            command.Parameters.AddWithValue("@CuidadorId", dto.CuidadorId);
            command.Parameters.AddWithValue("@TipoServicio", dto.TipoServicio);
            command.Parameters.AddWithValue("@Fecha", dto.Fecha);
            command.Parameters.AddWithValue("@HoraInicio", dto.HoraInicio);
            command.Parameters.AddWithValue("@HoraFin", dto.HoraFin);
            command.Parameters.AddWithValue("@Direccion", (object?)dto.Direccion ?? DBNull.Value);
            command.Parameters.AddWithValue("@Tarifa", dto.Tarifa);
            command.Parameters.AddWithValue("@Latitud", (object?)dto.Latitud ?? DBNull.Value);
            command.Parameters.AddWithValue("@Longitud", (object?)dto.Longitud ?? DBNull.Value);

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<IEnumerable<TrabajoDto>> ObtenerTrabajosPorCuidadorAsync(int cuidadorId)
        {
            var trabajos = new List<TrabajoDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerTrabajosPorCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@CuidadorId", cuidadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                trabajos.Add(MapearTrabajo(reader));
            }

            return trabajos;
        }

        public async Task<TrabajoDto?> ObtenerProximoTrabajoAsync(int cuidadorId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerProximoTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@CuidadorId", cuidadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return MapearTrabajo(reader);

            return null;
        }

        public async Task<bool> ActualizarEstadoTrabajoAsync(ActualizarEstadoTrabajoDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ActualizarEstadoTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", dto.TrabajoId);
            command.Parameters.AddWithValue("@NuevoEstado", dto.NuevoEstado);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            return Convert.ToInt32(filasAfectadas) > 0;
        }

        public async Task<TrabajoClienteDto?> ObtenerTrabajoActivoPorClienteAsync(int clienteId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerTrabajoActivoPorCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ClienteId", clienteId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new TrabajoClienteDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    ClienteId = Convert.ToInt32(reader["ClienteId"]),
                    CuidadorId = Convert.ToInt32(reader["CuidadorId"]),
                    CuidadorNombre = reader["CuidadorNombre"].ToString() ?? "",
                    CuidadorFotoUrl = reader["CuidadorFotoUrl"] as string,
                    TipoServicio = reader["TipoServicio"].ToString() ?? "",
                    Fecha = Convert.ToDateTime(reader["Fecha"]),
                    HoraInicio = (TimeSpan)reader["HoraInicio"],
                    HoraFin = (TimeSpan)reader["HoraFin"],
                    Direccion = reader["Direccion"] as string,
                    Estado = Convert.ToInt32(reader["Estado"]),
                    Tarifa = Convert.ToDecimal(reader["Tarifa"]),
                    Notas = reader["Notas"] as string,
                    FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                    PinInicio = reader["PinInicio"] as string
                };
            }

            return null;
        }

        public async Task<bool> IniciarTrabajoAsync(IniciarTrabajoDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_IniciarTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", dto.TrabajoId);
            command.Parameters.AddWithValue("@Pin", dto.Pin);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            return Convert.ToInt32(filasAfectadas) > 0;
        }

        private static TrabajoDto MapearTrabajo(SqlDataReader reader)
        {
            return new TrabajoDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                ClienteId = Convert.ToInt32(reader["ClienteId"]),
                ClienteNombre = reader["ClienteNombre"].ToString() ?? "",
                ClienteFotoUrl = reader["ClienteFotoUrl"] as string,
                TipoServicio = reader["TipoServicio"].ToString() ?? "",
                Fecha = Convert.ToDateTime(reader["Fecha"]),
                HoraInicio = (TimeSpan)reader["HoraInicio"],
                HoraFin = (TimeSpan)reader["HoraFin"],
                Direccion = reader["Direccion"] as string,
                Estado = Convert.ToInt32(reader["Estado"]),
                Tarifa = Convert.ToDecimal(reader["Tarifa"]),
                Notas = reader["Notas"] as string,
                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                Latitud = reader["Latitud"] as decimal?,
                Longitud = reader["Longitud"] as decimal?
            };
        }
    }
}
