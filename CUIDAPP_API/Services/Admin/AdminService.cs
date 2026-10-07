using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.Admin;
using CUIDAPP_API.Interfaces.Admin;
using CUIDAPP_API.Services;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.Admin
{
    public class AdminService : IAdminService
    {
        private readonly string _connectionString;
        private readonly ITrabajoNotifier _notifier;

        public AdminService(IConfiguration config, ITrabajoNotifier notifier)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
            _notifier = notifier;
        }

        // Aviso en tiempo real: nunca debe tumbar la operación que ya se hizo.
        private async Task AvisarAsync(int usuarioId, string evento, object payload)
        {
            try { await _notifier.NotificarAsync(usuarioId, evento, payload); }
            catch (Exception ex) { Console.WriteLine($"No se pudo notificar {evento} a {usuarioId}: {ex.Message}"); }
        }

        public async Task<IEnumerable<CuidadorPendienteDto>> ObtenerCuidadoresPendientesAsync()
        {
            var cuidadores = new List<CuidadorPendienteDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerCuidadoresPendientes", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                cuidadores.Add(new CuidadorPendienteDto
                {
                    UsuarioId = Convert.ToInt32(reader["UsuarioId"]),
                    Email = reader["Email"].ToString() ?? "",
                    FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                    Especialidad = reader["Especialidad"].ToString(),
                    TarifaHora = Convert.ToDecimal(reader["TarifaHora"]),
                    EstadoAprobacion = Convert.ToInt32(reader["EstadoAprobacion"])
                });
            }
            return cuidadores;
        }

        public async Task<IEnumerable<CuidadorAdminDto>> ObtenerCuidadoresAdminAsync(int? estado)
        {
            var cuidadores = new List<CuidadorAdminDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerCuidadoresAdmin", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                cuidadores.Add(LeerCuidadorAdmin(reader));

            return cuidadores;
        }

        public async Task<CuidadorAdminDto?> ObtenerCuidadorAdminDetalleAsync(int usuarioId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerCuidadorAdminDetalle", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return LeerCuidadorAdmin(reader);
        }

        private static CuidadorAdminDto LeerCuidadorAdmin(SqlDataReader reader)
        {
            return new CuidadorAdminDto
            {
                UsuarioId = Convert.ToInt32(reader["UsuarioId"]),
                NombreCompleto = reader["NombreCompleto"] as string ?? "",
                Email = reader["Email"] as string ?? "",
                FotoUrl = reader["FotoUrl"] as string,
                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                Especialidad = reader["Especialidad"] as string,
                TarifaHora = Convert.ToDecimal(reader["TarifaHora"]),
                EstadoAprobacion = Convert.ToInt32(reader["EstadoAprobacion"]),
                Disponible = reader["Disponible"] != DBNull.Value && Convert.ToBoolean(reader["Disponible"]),
                Bio = reader["Bio"] as string,
                MetodoCobro = reader["MetodoCobro"] as string,
                PromedioCalificacion = reader["PromedioCalificacion"] == DBNull.Value ? null : Convert.ToDecimal(reader["PromedioCalificacion"]),
                TotalCalificaciones = Convert.ToInt32(reader["TotalCalificaciones"]),
                TrabajosCompletados = Convert.ToInt32(reader["TrabajosCompletados"])
            };
        }

        public async Task<IEnumerable<DocumentoDto>> ObtenerDocumentosPorCuidadorAsync(int cuidadorId)
        {
            var documentos = new List<DocumentoDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerDocumentosPorCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@CuidadorId", cuidadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                documentos.Add(new DocumentoDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    TipoDocumento = reader["TipoDocumento"].ToString() ?? "",
                    UrlArchivo = reader["UrlArchivo"].ToString() ?? "",
                    Estado = Convert.ToInt32(reader["Estado"]),
                    ObservacionesAdmin = reader["ObservacionesAdmin"].ToString(),
                    FechaSubida = Convert.ToDateTime(reader["FechaSubida"])
                });
            }
            return documentos;
        }

        public async Task<bool> ActualizarEstadoCuidadorAsync(ActualizarEstadoCuidadorDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ActualizarEstadoCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@CuidadorId", dto.CuidadorId);
            command.Parameters.AddWithValue("@NuevoEstado", dto.NuevoEstado);
            command.Parameters.AddWithValue("@Observaciones", (object?)dto.Observaciones ?? DBNull.Value);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            var ok = Convert.ToInt32(filasAfectadas) > 0;
            if (ok)
                await AvisarAsync(dto.CuidadorId, "CuentaActualizada", new { dto.CuidadorId, Estado = dto.NuevoEstado });
            return ok;
        }

        public async Task<bool> MarcarPagoComoPagadoAsync(int pagoId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_MarcarPagoComoPagado", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@PagoId", pagoId);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            return Convert.ToInt32(filasAfectadas) > 0;
        }

        public async Task<AdminActionResult> SuspenderUsuarioAsync(int usuarioId, SuspenderCuidadorDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_SuspenderUsuario", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@AdminId", SqlDbType.Int).Value = dto.AdminId;
            command.Parameters.Add("@Motivo", SqlDbType.NVarChar, 500).Value = dto.Motivo.Trim();
            var fechaFin = command.Parameters.Add("@FechaFinUtc", SqlDbType.DateTime2);
            fechaFin.Scale = 7;
            fechaFin.Value = dto.FechaFinUtc.HasValue ? dto.FechaFinUtc.Value.UtcDateTime : DBNull.Value;

            return await LeerResultadoAsync(connection, command);
        }

        public async Task<AdminActionResult> ReactivarUsuarioAsync(int usuarioId, ReactivarCuidadorDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ReactivarUsuario", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;
            command.Parameters.Add("@AdminId", SqlDbType.Int).Value = dto.AdminId;

            return await LeerResultadoAsync(connection, command);
        }

        private static async Task<AdminActionResult> LeerResultadoAsync(SqlConnection connection, SqlCommand command)
        {
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                throw new InvalidOperationException("El procedimiento no devolvió resultado.");

            var codigo = (string)reader["ResultadoCodigo"];
            return new AdminActionResult(
                codigo,
                MensajeResultado(codigo),
                Convert.ToInt32(reader["UsuarioId"]),
                reader["SancionId"] == DBNull.Value ? null : Convert.ToInt32(reader["SancionId"]),
                reader["IsActive"] == DBNull.Value ? null : Convert.ToBoolean(reader["IsActive"]),
                reader["EstadoSancion"] as string,
                reader["FechaFinUtc"] == DBNull.Value
                    ? null
                    : new DateTimeOffset(DateTime.SpecifyKind((DateTime)reader["FechaFinUtc"], DateTimeKind.Utc)));
        }

        public static string MensajeResultado(string codigo) => codigo switch
        {
            "APPLIED" => "Operación aplicada.",
            "ALREADY_COMPLETED" => "La suspensión temporal ya había vencido; la cuenta quedó activa.",
            "INVALID_REASON" => "El motivo debe tener entre 10 y 500 caracteres.",
            "INVALID_END_DATE" => "La fecha de fin debe ser futura.",
            "ADMIN_ROLE_REQUIRED" => "El autor de la operación debe ser un administrador.",
            "USER_NOT_FOUND" => "No existe el usuario indicado.",
            "ALREADY_SUSPENDED" => "La cuenta ya tiene una suspensión vigente.",
            "ALREADY_ACTIVE" => "La cuenta ya está activa.",
            "STATE_CONFLICT" => "El estado de la cuenta y sus sanciones es incoherente; requiere revisión.",
            _ => "Resultado no reconocido."
        };

        public async Task<IEnumerable<SancionCuidadorDto>> ObtenerSancionesAsync(int usuarioId)
        {
            var sanciones = new List<SancionCuidadorDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerSancionesUsuario", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                sanciones.Add(new SancionCuidadorDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    UsuarioId = Convert.ToInt32(reader["UsuarioId"]),
                    AdminId = Convert.ToInt32(reader["AdminId"]),
                    AdminNombre = reader["AdminNombre"] as string ?? "",
                    Accion = reader["Accion"] as string ?? "",
                    Motivo = reader["Motivo"] as string ?? "",
                    Tipo = reader["Tipo"] as string ?? "",
                    Estado = reader["Estado"] as string ?? "",
                    FechaInicioUtc = ComoUtc(reader["FechaInicioUtc"]),
                    FechaFinUtc = reader["FechaFinUtc"] == DBNull.Value ? null : ComoUtc(reader["FechaFinUtc"]),
                    RevocadaPorAdminId = reader["RevocadaPorAdminId"] == DBNull.Value ? null : Convert.ToInt32(reader["RevocadaPorAdminId"]),
                    RevocadaPorAdminNombre = reader["RevocadaPorAdminNombre"] as string,
                    FechaRevocacionUtc = reader["FechaRevocacionUtc"] == DBNull.Value ? null : ComoUtc(reader["FechaRevocacionUtc"]),
                    EstaVigente = Convert.ToBoolean(reader["EstaVigente"])
                });
            }

            return sanciones;
        }

        private static DateTimeOffset ComoUtc(object valor)
            => new(DateTime.SpecifyKind((DateTime)valor, DateTimeKind.Utc));

        public async Task<bool> ActualizarInfoCuidadorAsync(int usuarioId, ActualizarInfoCuidadorDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ActualizarInfoCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);
            command.Parameters.AddWithValue("@NombreCompleto", dto.NombreCompleto);
            command.Parameters.AddWithValue("@Especialidad", dto.Especialidad);
            command.Parameters.AddWithValue("@TarifaHora", dto.TarifaHora);
            command.Parameters.AddWithValue("@Bio", (object?)dto.Bio ?? DBNull.Value);
            command.Parameters.AddWithValue("@MetodoCobro", (object?)dto.MetodoCobro ?? DBNull.Value);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            return Convert.ToInt32(filasAfectadas) > 0;
        }

        public async Task<IEnumerable<ClienteAdminDto>> ObtenerClientesAdminAsync(bool? activo)
        {
            var clientes = new List<ClienteAdminDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerClientesAdmin", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Estado", (object?)activo ?? DBNull.Value);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                clientes.Add(LeerClienteAdmin(reader));

            return clientes;
        }

        public async Task<ClienteAdminDto?> ObtenerClienteAdminDetalleAsync(int usuarioId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerClienteAdminDetalle", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return LeerClienteAdmin(reader);
        }

        public async Task<bool> ActualizarInfoClienteAsync(int usuarioId, ActualizarInfoClienteDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ActualizarInfoCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);
            command.Parameters.AddWithValue("@NombreCompleto", dto.NombreCompleto);
            command.Parameters.AddWithValue("@DireccionPrincipal", (object?)dto.DireccionPrincipal ?? DBNull.Value);
            command.Parameters.AddWithValue("@ContactoEmergenciaNombre", (object?)dto.ContactoEmergenciaNombre ?? DBNull.Value);
            command.Parameters.AddWithValue("@ContactoEmergenciaTelefono", (object?)dto.ContactoEmergenciaTelefono ?? DBNull.Value);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            return Convert.ToInt32(filasAfectadas) > 0;
        }

        // Mismo algoritmo que AuthService.HashPassword (SHA256, hex minúscula), para que
        // las cuentas creadas aquí puedan iniciar sesión con auth/login sin cambios.
        private static string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
        }

        public async Task<(int NuevoId, string Motivo)> CrearAdminAsync(CrearAdminDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CrearUsuarioAdmin", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Email", dto.Email);
            command.Parameters.AddWithValue("@PasswordHash", HashPassword(dto.Password));
            command.Parameters.AddWithValue("@NombreCompleto", dto.NombreCompleto);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return (0, "ERROR");

            return (Convert.ToInt32(reader["NuevoId"]), reader["Motivo"] as string ?? "ERROR");
        }

        public async Task<IEnumerable<AdminUsuarioDto>> ObtenerAdminsAsync()
        {
            var admins = new List<AdminUsuarioDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerUsuariosAdmin", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                admins.Add(new AdminUsuarioDto
                {
                    UsuarioId = Convert.ToInt32(reader["UsuarioId"]),
                    NombreCompleto = reader["NombreCompleto"] as string ?? "",
                    Email = reader["Email"] as string ?? "",
                    FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                    IsActive = Convert.ToBoolean(reader["IsActive"])
                });
            }

            return admins;
        }

        private static ClienteAdminDto LeerClienteAdmin(SqlDataReader reader)
        {
            return new ClienteAdminDto
            {
                UsuarioId = Convert.ToInt32(reader["UsuarioId"]),
                NombreCompleto = reader["NombreCompleto"] as string ?? "",
                Email = reader["Email"] as string ?? "",
                FotoUrl = reader["FotoUrl"] as string,
                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                DireccionPrincipal = reader["DireccionPrincipal"] as string,
                ContactoEmergenciaNombre = reader["ContactoEmergenciaNombre"] as string,
                ContactoEmergenciaTelefono = reader["ContactoEmergenciaTelefono"] as string,
                TotalServiciosSolicitados = Convert.ToInt32(reader["TotalServiciosSolicitados"]),
                ServiciosCompletados = Convert.ToInt32(reader["ServiciosCompletados"])
            };
        }
    }
}
