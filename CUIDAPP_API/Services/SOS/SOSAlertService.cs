using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.SOS;
using CUIDAPP_API.Interfaces.Email;
using CUIDAPP_API.Interfaces.SOS;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.SOS
{
    public class SOSAlertService : ISOSAlertService
    {
        private readonly string _connectionString;
        private readonly ITrabajoNotifier _notifier;
        private readonly IEmailService _email;
        private readonly ILogger<SOSAlertService> _logger;

        public SOSAlertService(IConfiguration config, ITrabajoNotifier notifier, IEmailService email, ILogger<SOSAlertService> logger)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
            _notifier = notifier;
            _email = email;
            _logger = logger;
        }

        public Task<SOSAlertaDto?> CrearAlertaAsync(CrearSOSAlertaDto dto) => CrearAlertaAsync(dto, "Manual");

        /// <summary>
        /// Alerta automática: la app detectó un impacto seguido de inmovilidad y el cuidador no
        /// confirmó que está bien. Se valida que el servicio sea del cuidador y esté En Progreso,
        /// y se evita duplicar la alerta si ya hay una automática pendiente para ese trabajo.
        /// </summary>
        public async Task<(SOSAlertaDto? Alerta, string? Error)> CrearAlertaDeadManAsync(DeadManTriggeredDto dto)
        {
            int? cuidadorId = null, estado = null;
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand("SELECT CuidadorId, Estado FROM Trabajos WHERE Id = @Id", connection))
            {
                command.Parameters.AddWithValue("@Id", dto.TrabajoId);
                await connection.OpenAsync();
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    cuidadorId = Convert.ToInt32(reader["CuidadorId"]);
                    estado = Convert.ToInt32(reader["Estado"]);
                }
            }

            if (cuidadorId == null || cuidadorId != dto.UsuarioId)
                return (null, "El trabajo no existe o no pertenece a este cuidador.");
            if (estado != 3)
                return (null, "El servicio no está en progreso.");

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(
                "SELECT TOP 1 Id FROM SOSAlertas WHERE TrabajoId = @T AND Origen = 'Automatica' AND Estado = 'Pendiente'", connection))
            {
                command.Parameters.AddWithValue("@T", dto.TrabajoId);
                await connection.OpenAsync();
                var existente = await command.ExecuteScalarAsync();
                if (existente != null)
                    return (await ObtenerAlertaPorIdAsync(Convert.ToInt32(existente)), null);
            }

            var detalle = new List<string> { "Detección automática: posible caída o accidente. El cuidador no confirmó que estaba bien en 30 segundos." };
            if (dto.ImpactoG is > 0)
                detalle.Add($"Impacto de {dto.ImpactoG:F1} g.");
            if (dto.SegundosInmovil is > 0)
                detalle.Add($"{dto.SegundosInmovil} s sin movimiento.");

            var alerta = await CrearAlertaAsync(new CrearSOSAlertaDto
            {
                TrabajoId = dto.TrabajoId,
                UsuarioId = dto.UsuarioId,
                TipoUsuario = "Cuidador",
                Latitud = dto.Latitud,
                Longitud = dto.Longitud,
                Motivo = string.Join(" ", detalle)
            }, "Automatica");

            if (alerta != null)
                _ = AvisarAlFamiliarAsync(alerta); // no debe retrasar ni tumbar la alerta al panel

            return (alerta, null);
        }

        // Aviso por correo al familiar (si el cuidador registró uno). No hay proveedor de SMS configurado.
        private async Task AvisarAlFamiliarAsync(SOSAlertaDto alerta)
        {
            if (string.IsNullOrWhiteSpace(alerta.ContactoEmail))
                return;

            try
            {
                var mapa = $"https://www.google.com/maps?q={alerta.Latitud.ToString(System.Globalization.CultureInfo.InvariantCulture)},{alerta.Longitud.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                var nombre = System.Net.WebUtility.HtmlEncode(alerta.NombreUsuario);
                var familiar = System.Net.WebUtility.HtmlEncode(alerta.ContactoNombre ?? "");
                var cuerpo = $"""
                    <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 20px;">
                        <h2 style="color: #C62828;">🚨 Alerta de emergencia - CuidApp</h2>
                        <p>Hola {familiar},</p>
                        <p>El sistema de seguridad de CuidApp detectó un <b>posible accidente</b> de <b>{nombre}</b> mientras realizaba un servicio,
                        y no respondió a la confirmación de bienestar.</p>
                        <p>El equipo de CuidApp ya fue notificado. Te recomendamos intentar comunicarte con {nombre} de inmediato.</p>
                        <p>📍 Última ubicación conocida: <a href="{mapa}">ver en el mapa</a></p>
                        <p style="color:#6B7280; font-size:12px;">Hora de la alerta: {alerta.FechaCreacion:dd/MM/yyyy HH:mm}</p>
                    </div>
                    """;
                await _email.EnviarAsync(alerta.ContactoEmail!, "🚨 Alerta de emergencia - CuidApp", cuerpo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el aviso por correo al familiar de la alerta {AlertaId}", alerta.Id);
            }
        }

        private async Task<SOSAlertaDto?> CrearAlertaAsync(CrearSOSAlertaDto dto, string origen)
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
            command.Parameters.AddWithValue("@Origen", origen);

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
                    EmailUsuario = reader["EmailUsuario"] as string,
                    Latitud = Convert.ToDouble(reader["Latitud"]),
                    Longitud = Convert.ToDouble(reader["Longitud"]),
                    Motivo = reader["Motivo"] as string,
                    Estado = reader["Estado"].ToString()!,
                    FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                    Origen = reader["Origen"] as string ?? "Manual",
                    ContactoNombre = reader["ContactoNombre"] as string,
                    ContactoTelefono = reader["ContactoTelefono"] as string,
                    ContactoEmail = reader["ContactoEmail"] as string
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
            using var command = new SqlCommand(@"SELECT a.*, u.NombreCompleto AS NombreUsuario, u.Email AS EmailUsuario,
                    COALESCE(pq.ContactoEmergenciaNombre, pc.ContactoEmergenciaNombre) AS ContactoNombre,
                    COALESCE(pq.ContactoEmergenciaTelefono, pc.ContactoEmergenciaTelefono) AS ContactoTelefono,
                    pq.ContactoEmergenciaEmail AS ContactoEmail
                FROM SOSAlertas a
                INNER JOIN Usuarios u ON a.UsuarioId = u.Id
                LEFT JOIN PerfilCuidador pq ON pq.UsuarioId = a.UsuarioId
                LEFT JOIN PerfilCliente pc ON pc.UsuarioId = a.UsuarioId
                WHERE a.Id = @Id", connection);
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
                EmailUsuario = reader["EmailUsuario"] as string,
                Latitud = Convert.ToDouble(reader["Latitud"]),
                Longitud = Convert.ToDouble(reader["Longitud"]),
                Motivo = reader["Motivo"] as string,
                Estado = reader["Estado"].ToString()!,
                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                FechaAtencion = reader["FechaAtencion"] as DateTime?,
                AtendidoPor = reader["AtendidoPor"] as string,
                Origen = reader["Origen"] as string ?? "Manual",
                ContactoNombre = reader["ContactoNombre"] as string,
                ContactoTelefono = reader["ContactoTelefono"] as string,
                ContactoEmail = reader["ContactoEmail"] as string
            };
        }
    }
}
