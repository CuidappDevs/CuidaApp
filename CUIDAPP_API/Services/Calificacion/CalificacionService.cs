using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.Calificacion;
using CUIDAPP_API.Interfaces.Calificacion;

namespace CUIDAPP_API.Services.Calificacion
{
    public class CalificacionService : ICalificacionService
    {
        private readonly string _connectionString;

        public CalificacionService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<bool> CrearCalificacionAsync(CrearCalificacionDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CrearCalificacion", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", dto.TrabajoId);
            command.Parameters.AddWithValue("@CalificadorId", dto.CalificadorId);
            command.Parameters.AddWithValue("@CalificadoId", dto.CalificadoId);
            command.Parameters.AddWithValue("@Puntuacion", dto.Puntuacion);
            command.Parameters.AddWithValue("@Comentario", (object?)dto.Comentario ?? DBNull.Value);

            await connection.OpenAsync();
            var resultado = await command.ExecuteScalarAsync();
            return Convert.ToInt32(resultado) == 1;
        }

        public async Task<CalificacionPromedioDto> ObtenerPromedioAsync(int usuarioId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerCalificacionPromedio", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new CalificacionPromedioDto
                {
                    Promedio = Convert.ToDecimal(reader["Promedio"]),
                    Total = Convert.ToInt32(reader["Total"])
                };
            }

            return new CalificacionPromedioDto { Promedio = 0, Total = 0 };
        }

        public async Task<bool> ExisteCalificacionAsync(int trabajoId, int calificadorId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ExisteCalificacion", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", trabajoId);
            command.Parameters.AddWithValue("@CalificadorId", calificadorId);

            await connection.OpenAsync();
            var resultado = await command.ExecuteScalarAsync();
            return Convert.ToInt32(resultado) == 1;
        }

        public async Task<CalificacionDto?> ObtenerCalificacionDeTrabajoAsync(int trabajoId, int calificadorId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerCalificacionDeTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", trabajoId);
            command.Parameters.AddWithValue("@CalificadorId", calificadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new CalificacionDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                TrabajoId = Convert.ToInt32(reader["TrabajoId"]),
                CalificadorId = Convert.ToInt32(reader["CalificadorId"]),
                CalificadoId = Convert.ToInt32(reader["CalificadoId"]),
                Puntuacion = Convert.ToInt32(reader["Puntuacion"]),
                Comentario = reader["Comentario"] as string,
                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"])
            };
        }
    }
}
