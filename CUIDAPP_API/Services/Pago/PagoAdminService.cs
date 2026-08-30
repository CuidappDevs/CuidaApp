using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.DTOs.Pago;
using CUIDAPP_API.Interfaces.Pago;
using CUIDAPP_API.Services;

namespace CUIDAPP_API.Services.Pago
{
    public class PagoAdminService : IPagoAdminService
    {
        private readonly string _connectionString;

        public PagoAdminService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<List<PagoAdminDto>> ObtenerPagosAsync(int? estado)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerPagosAdmin", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            var pagos = new List<PagoAdminDto>();
            while (await reader.ReadAsync())
            {
                pagos.Add(new PagoAdminDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    TrabajoId = Convert.ToInt32(reader["TrabajoId"]),
                    CuidadorId = Convert.ToInt32(reader["CuidadorId"]),
                    CuidadorNombre = reader["CuidadorNombre"] as string ?? "",
                    ClienteId = Convert.ToInt32(reader["ClienteId"]),
                    ClienteNombre = reader["ClienteNombre"] as string ?? "",
                    TipoServicio = reader["TipoServicio"] as string ?? "",
                    Monto = Convert.ToDecimal(reader["Monto"]),
                    Estado = Convert.ToInt32(reader["Estado"]),
                    FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                    AutorizadoPorAdminId = reader["AutorizadoPorAdminId"] == DBNull.Value ? null : Convert.ToInt32(reader["AutorizadoPorAdminId"]),
                    AutorizadoPorNombre = reader["AutorizadoPorNombre"] as string,
                    FechaAutorizacion = reader["FechaAutorizacion"] == DBNull.Value ? null : Convert.ToDateTime(reader["FechaAutorizacion"]),
                    AprobadoPorAdminId = reader["AprobadoPorAdminId"] == DBNull.Value ? null : Convert.ToInt32(reader["AprobadoPorAdminId"]),
                    AprobadoPorNombre = reader["AprobadoPorNombre"] as string,
                    FechaPago = reader["FechaPago"] == DBNull.Value ? null : Convert.ToDateTime(reader["FechaPago"]),
                    PagoDisputado = Convert.ToBoolean(reader["PagoDisputado"])
                });
            }

            return pagos;
        }

        public async Task<bool> AutorizarPagoAsync(int pagoId, int adminId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_AutorizarPago", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@PagoId", pagoId);
            command.Parameters.AddWithValue("@AdminId", adminId);
            command.Parameters.AddWithValue("@FechaHora", HoraLocalRD.Ahora);

            await connection.OpenAsync();
            var resultado = await command.ExecuteScalarAsync();
            return Convert.ToInt32(resultado) == 1;
        }

        public async Task<bool> AprobarPagoAsync(int pagoId, int adminId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_AprobarPago", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@PagoId", pagoId);
            command.Parameters.AddWithValue("@AdminId", adminId);
            command.Parameters.AddWithValue("@FechaHora", HoraLocalRD.Ahora);

            await connection.OpenAsync();
            var resultado = await command.ExecuteScalarAsync();
            return Convert.ToInt32(resultado) == 1;
        }
    }
}
