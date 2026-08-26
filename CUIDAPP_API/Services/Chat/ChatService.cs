using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.DTOs.Chat;
using CUIDAPP_API.Interfaces.Chat;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.Chat
{
    public class ChatService : IChatService
    {
        private readonly string _connectionString;
        private readonly ITrabajoNotifier _notifier;

        public ChatService(IConfiguration config, ITrabajoNotifier notifier)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
            _notifier = notifier;
        }

        public async Task<ConversacionDto> ObtenerOCrearConversacionAsync(int trabajoId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ConversacionCrear", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoID", trabajoId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            return new ConversacionDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                TrabajoId = Convert.ToInt32(reader["TrabajoID"]),
                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                Activa = Convert.ToBoolean(reader["Activa"])
            };
        }

        public async Task<IEnumerable<MensajeDto>> ObtenerMensajesAsync(int conversacionId)
        {
            var mensajes = new List<MensajeDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_MensajesObtener", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ConversacionId", conversacionId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                mensajes.Add(new MensajeDto
                {
                    Id = Convert.ToInt64(reader["Id"]),
                    ConversacionId = Convert.ToInt32(reader["ConversacionId"]),
                    RemitenteId = Convert.ToInt32(reader["RemitenteId"]),
                    Contenido = reader["Contenido"].ToString() ?? "",
                    FechaEnvio = Convert.ToDateTime(reader["FechaEnvio"]),
                    Leido = Convert.ToBoolean(reader["Leido"]),
                    Tipo = reader["Tipo"].ToString() ?? "texto",
                    UrlArchivo = reader["UrlArchivo"] as string,
                    DuracionSegundos = reader["DuracionSegundos"] as int?
                });
            }

            return mensajes;
        }

        public async Task<MensajeDto> EnviarMensajeAsync(EnviarMensajeDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_MensajeEnviar", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ConversacionId", dto.ConversacionId);
            command.Parameters.AddWithValue("@RemitenteId", dto.RemitenteId);
            command.Parameters.AddWithValue("@Contenido", dto.Contenido);
            command.Parameters.AddWithValue("@Tipo", dto.Tipo);
            command.Parameters.AddWithValue("@UrlArchivo", (object?)dto.UrlArchivo ?? DBNull.Value);
            command.Parameters.AddWithValue("@DuracionSegundos", (object?)dto.DuracionSegundos ?? DBNull.Value);
            command.Parameters.AddWithValue("@FechaEnvio", DateTime.Now);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            var mensaje = new MensajeDto
            {
                Id = Convert.ToInt64(reader["Id"]),
                ConversacionId = Convert.ToInt32(reader["ConversacionId"]),
                RemitenteId = Convert.ToInt32(reader["RemitenteId"]),
                Contenido = reader["Contenido"].ToString() ?? "",
                FechaEnvio = Convert.ToDateTime(reader["FechaEnvio"]),
                Leido = Convert.ToBoolean(reader["Leido"]),
                Tipo = reader["Tipo"].ToString() ?? "texto",
                UrlArchivo = reader["UrlArchivo"] as string,
                DuracionSegundos = reader["DuracionSegundos"] as int?
            };

            reader.Close();
            var participantes = await ObtenerParticipantesAsync(dto.ConversacionId);
            if (participantes.HasValue)
            {
                await _notifier.NotificarAsync(participantes.Value.ClienteId, "MensajeNuevo", mensaje);
                await _notifier.NotificarAsync(participantes.Value.CuidadorId, "MensajeNuevo", mensaje);
            }

            return mensaje;
        }

        public async Task MarcarLeidosAsync(MarcarLeidosDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_MensajesMarcarLeidos", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ConversacionId", dto.ConversacionId);
            command.Parameters.AddWithValue("@UsuarioId", dto.UsuarioId);

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<NoLeidosDto>> ContarNoLeidosAsync(int usuarioId)
        {
            var resultado = new List<NoLeidosDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_MensajesContarNoLeidos", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                resultado.Add(new NoLeidosDto
                {
                    TrabajoId = Convert.ToInt32(reader["TrabajoId"]),
                    ConversacionId = Convert.ToInt32(reader["ConversacionId"]),
                    NoLeidos = Convert.ToInt32(reader["NoLeidos"])
                });
            }

            return resultado;
        }

        private async Task<(int ClienteId, int CuidadorId)?> ObtenerParticipantesAsync(int conversacionId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(
                "SELECT t.ClienteId, t.CuidadorId FROM Conversaciones c JOIN Trabajos t ON t.Id = c.TrabajoID WHERE c.Id = @ConversacionId",
                connection);
            command.Parameters.AddWithValue("@ConversacionId", conversacionId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (Convert.ToInt32(reader["ClienteId"]), Convert.ToInt32(reader["CuidadorId"]));

            return null;
        }
    }
}
