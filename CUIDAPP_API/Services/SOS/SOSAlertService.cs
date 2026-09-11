using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.SOS;
using CUIDAPP_API.Interfaces.SOS;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.SOS
{
    public class SOSAlertService : ISOSAlertService
    {
        private readonly string _connectionString;
        private readonly ITrabajoNotifier _notifier;

        public SOSAlertService(IConfiguration config, ITrabajoNotifier notifier)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
            _notifier = notifier;
        }

        public async Task<SOSAlertaDto?> CrearAlertaAsync(CrearSOSAlertaDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CrearSOSAlerta", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@TrabajoId", dto.TrabajoId);
            command.Parameters.AddWithValue("@UsuarioId", dto.UsuarioId);
            command.Parameters.AddWithValue("@TipoUsuario", dto.TipoUsuario);
            command.Parameters.AddWithValue("@Latitud", dto.Latitud);
            command.Parameters.AddWithValue("@Longitud", dto.Longitud);
            command.Parameters.AddWithValue("@Motivo", (object?)dto.Motivo ?? DBNull.Value);
            command.Parameters.AddWithValue("@FechaCreacion", HoraLocalRD.Ahora);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            SOSAlertaDto? alerta = null;
            if (await reader.ReadAsync())
            {
                alerta = new SOSAlertaDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    TrabajoId = Convert.ToInt32(reader["TrabajoId"]),
                    UsuarioId = Convert.ToInt32(reader["UsuarioId"]),
                    TipoUsuario = reader["TipoUsuario"].ToString()!,
                    NombreUsuario = reader["NombreUsuario"].ToString()!,
                    TelefonoUsuario = reader["TelefonoUsuario"] as string,
                    Latitud = Convert.ToDouble(reader["Latitud"]),
                    Longitud = Convert.ToDouble(reader["Longitud"]),
                    Motivo = reader["Motivo"] as string,
                    Estado = reader["Estado"].ToString()!,
                    FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"])
                };
            }

            if (alerta != null)
            {
                await _notifier.NotificarGlobalAsync("AlertaSOS", alerta);
            }

            return alerta;
        }

        public async Task<IEnumerable<SOSAlertaDto>> ObtenerAlertasPendientesAsync()
        {
            var alertas = new List<SOSAlertaDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerSOSAlertasPendientes", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                alertas.Add(MapearAlerta(reader));
            }

            return alertas;
        }

        public async Task<SOSAlertaDto?> ObtenerAlertaPorIdAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("SELECT a.*, u.Nombre + ' ' + ISNULL(u.Apellido, '') AS NombreUsuario, u.Telefono AS TelefonoUsuario FROM SOSAlertas a INNER JOIN Usuarios u ON a.UsuarioId = u.Id WHERE a.Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return MapearAlerta(reader);

            return null;
        }

        public async Task<bool> AtenderAlertaAsync(int id, AtenderSOSDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_AtenderSOSAlerta", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@AtendidoPor", dto.AtendidoPor);
            command.Parameters.AddWithValue("@FechaAtencion", HoraLocalRD.Ahora);

            await connection.OpenAsync();
            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task<bool> DescartarAlertaAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("UPDATE SOSAlertas SET Estado = 'Descartada' WHERE Id = @Id AND Estado = 'Pendiente'", connection);
            command.Parameters.AddWithValue("@Id", id);

            await connection.OpenAsync();
            var rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }

        private static SOSAlertaDto MapearAlerta(SqlDataReader reader)
        {
            return new SOSAlertaDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                TrabajoId = Convert.ToInt32(reader["TrabajoId"]),
                UsuarioId = Convert.ToInt32(reader["UsuarioId"]),
                TipoUsuario = reader["TipoUsuario"].ToString()!,
                NombreUsuario = reader["NombreUsuario"].ToString()!,
                TelefonoUsuario = reader["TelefonoUsuario"] as string,
                Latitud = Convert.ToDouble(reader["Latitud"]),
                Longitud = Convert.ToDouble(reader["Longitud"]),
                Motivo = reader["Motivo"] as string,
                Estado = reader["Estado"].ToString()!,
                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                FechaAtencion = reader["FechaAtencion"] as DateTime?,
                AtendidoPor = reader["AtendidoPor"] as string
            };
        }
    }
}
