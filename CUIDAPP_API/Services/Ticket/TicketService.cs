using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.DTOs.Ticket;
using CUIDAPP_API.Interfaces.Ticket;
using CUIDAPP_API.Services;

namespace CUIDAPP_API.Services.Ticket
{
    public class TicketService : ITicketService
    {
        private readonly string _connectionString;

        public TicketService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<int> CrearTicketAsync(CrearTicketDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CrearTicket", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", dto.UsuarioId);
            command.Parameters.AddWithValue("@Asunto", dto.Asunto);
            command.Parameters.AddWithValue("@Categoria", dto.Categoria);
            command.Parameters.AddWithValue("@Descripcion", dto.Descripcion);
            command.Parameters.AddWithValue("@TrabajoId", (object?)dto.TrabajoId ?? DBNull.Value);
            command.Parameters.AddWithValue("@FechaHora", HoraLocalRD.Ahora);

            await connection.OpenAsync();
            var resultado = await command.ExecuteScalarAsync();
            return Convert.ToInt32(resultado);
        }

        public async Task<List<TicketDto>> ObtenerTicketsPorUsuarioAsync(int usuarioId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerTicketsPorUsuario", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            var tickets = new List<TicketDto>();
            while (await reader.ReadAsync())
            {
                tickets.Add(new TicketDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Asunto = reader["Asunto"] as string ?? "",
                    Categoria = reader["Categoria"] as string ?? "",
                    TrabajoId = reader["TrabajoId"] == DBNull.Value ? null : Convert.ToInt32(reader["TrabajoId"]),
                    Estado = Convert.ToInt32(reader["Estado"]),
                    FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                    FechaActualizacion = Convert.ToDateTime(reader["FechaActualizacion"])
                });
            }

            return tickets;
        }

        public async Task<List<TicketAdminDto>> ObtenerTicketsAdminAsync(int? estado)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerTicketsAdmin", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            var tickets = new List<TicketAdminDto>();
            while (await reader.ReadAsync())
                tickets.Add(LeerTicketAdmin(reader));

            return tickets;
        }

        public async Task<TicketAdminDto?> ObtenerDetalleAsync(int ticketId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerTicketDetalle", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TicketId", ticketId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return LeerTicketAdmin(reader, conTotalMensajes: false);
        }

        public async Task<List<TicketMensajeDto>> ObtenerMensajesAsync(int ticketId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerMensajesTicket", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TicketId", ticketId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            var mensajes = new List<TicketMensajeDto>();
            while (await reader.ReadAsync())
            {
                mensajes.Add(new TicketMensajeDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    TicketId = Convert.ToInt32(reader["TicketId"]),
                    AutorId = Convert.ToInt32(reader["AutorId"]),
                    AutorNombre = reader["AutorNombre"] as string ?? "",
                    EsAdmin = Convert.ToBoolean(reader["EsAdmin"]),
                    Mensaje = reader["Mensaje"] as string ?? "",
                    FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"])
                });
            }

            return mensajes;
        }

        public async Task<bool> AgregarMensajeAsync(int ticketId, CrearMensajeTicketDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_AgregarMensajeTicket", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TicketId", ticketId);
            command.Parameters.AddWithValue("@AutorId", dto.AutorId);
            command.Parameters.AddWithValue("@EsAdmin", dto.EsAdmin);
            command.Parameters.AddWithValue("@Mensaje", dto.Mensaje);
            command.Parameters.AddWithValue("@FechaHora", HoraLocalRD.Ahora);

            await connection.OpenAsync();
            var resultado = await command.ExecuteScalarAsync();
            return Convert.ToInt32(resultado) == 1;
        }

        public async Task<bool> ActualizarEstadoAsync(int ticketId, ActualizarEstadoTicketDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ActualizarEstadoTicket", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TicketId", ticketId);
            command.Parameters.AddWithValue("@Estado", dto.Estado);
            command.Parameters.AddWithValue("@AdminId", (object?)dto.AdminId ?? DBNull.Value);
            command.Parameters.AddWithValue("@FechaHora", HoraLocalRD.Ahora);

            await connection.OpenAsync();
            var resultado = await command.ExecuteScalarAsync();
            return Convert.ToInt32(resultado) == 1;
        }

        private static TicketAdminDto LeerTicketAdmin(SqlDataReader reader, bool conTotalMensajes = true)
        {
            return new TicketAdminDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                Asunto = reader["Asunto"] as string ?? "",
                Categoria = reader["Categoria"] as string ?? "",
                TrabajoId = reader["TrabajoId"] == DBNull.Value ? null : Convert.ToInt32(reader["TrabajoId"]),
                Estado = Convert.ToInt32(reader["Estado"]),
                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                FechaActualizacion = Convert.ToDateTime(reader["FechaActualizacion"]),
                UsuarioId = Convert.ToInt32(reader["UsuarioId"]),
                UsuarioNombre = reader["UsuarioNombre"] as string ?? "",
                UsuarioEmail = reader["UsuarioEmail"] as string ?? "",
                UsuarioRolId = Convert.ToInt32(reader["UsuarioRolId"]),
                AsignadoAdminId = reader["AsignadoAdminId"] == DBNull.Value ? null : Convert.ToInt32(reader["AsignadoAdminId"]),
                AsignadoAdminNombre = reader["AsignadoAdminNombre"] as string,
                TotalMensajes = conTotalMensajes ? Convert.ToInt32(reader["TotalMensajes"]) : 0
            };
        }
    }
}
