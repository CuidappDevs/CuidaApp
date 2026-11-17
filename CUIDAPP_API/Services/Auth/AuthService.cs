using CUIDAPP_API.Services.Email;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.Interfaces.Auth;
using CUIDAPP_API.Interfaces.Email;
using CUIDAPP_API.Services.TestPsicologico;

namespace CUIDAPP_API.Services.Auth
{
    public partial class AuthService : IAuthService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;

        private readonly IValidadorGoogle _google;

        public AuthService(IConfiguration config, IEmailService emailService, IValidadorGoogle? google = null)
        {
            _config = config;
            _google = google ?? new ValidadorGoogle(config);
            _emailService = emailService;
            _connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
        }

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        public Task<LoginResult> LoginAsync(LoginRequestDto loginDto) => LoginCoreAsync(loginDto, false);

        protected virtual async Task<LoginResult> LoginCoreAsync(LoginRequestDto loginDto, bool google)
        {
            int id, rolId;
            bool isActive;
            int? estadoAprobacion;
            string? nombre, foto;
            int? sancionId = null;
            string? sancionMotivo = null, sancionTipo = null;
            DateTime? sancionFin = null;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Fase 1: solo lectura (credenciales, estado y sanción vigente).
            using (var command = new SqlCommand("sp_ObtenerUsuarioPorEmail", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add("@Email", SqlDbType.NVarChar, 150).Value = loginDto.Email;

                using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return new LoginResult(LoginStatus.InvalidCredentials);

                // La contraseña se verifica ANTES de revelar cualquier dato de la sanción.
                if (!google && reader["PasswordHash"].ToString() != HashPassword(loginDto.Password))
                    return new LoginResult(LoginStatus.InvalidCredentials);

                id = Convert.ToInt32(reader["Id"]);
                rolId = Convert.ToInt32(reader["RolId"]);
                isActive = Convert.ToBoolean(reader["IsActive"]);
                nombre = reader["NombreCompleto"] as string;
                foto = reader["FotoUrl"] as string;
                estadoAprobacion = reader["EstadoAprobacion"] == DBNull.Value
                    ? null
                    : Convert.ToInt32(reader["EstadoAprobacion"]);

                if (reader["SancionId"] != DBNull.Value)
                {
                    sancionId = Convert.ToInt32(reader["SancionId"]);
                    sancionMotivo = reader["SancionMotivo"] as string;
                    sancionTipo = reader["SancionTipo"] as string;
                    sancionFin = reader["SancionFechaFinUtc"] == DBNull.Value
                        ? null
                        : DateTime.SpecifyKind((DateTime)reader["SancionFechaFinUtc"], DateTimeKind.Utc);
                }
            }

            // Las credenciales ya fueron verificadas: recién ahora revelar la solicitud.
            using (var eliminacion = new SqlCommand("sp_EliminacionLogin", connection) { CommandType = CommandType.StoredProcedure })
            {
                eliminacion.Parameters.AddWithValue("@UsuarioId", id);
                var fecha = await eliminacion.ExecuteScalarAsync();
                if (fecha is DateTime ejecucion)
                    return new LoginResult(LoginStatus.AccountDeleting, FechaEjecucion: ejecucion, UsuarioId: id);
            }

            if (sancionId == null)
            {
                // Activo y sin sanción: operativo. Inactivo sin sanción: estado sin explicación.
                if (!isActive)
                    return new LoginResult(LoginStatus.AccountInactive);
            }
            else if (isActive)
            {
                // Sanción vigente con usuario activo: incoherencia; no se emite token.
                return new LoginResult(LoginStatus.AccountInactive);
            }
            else if (sancionTipo == "TEMPORAL" && sancionFin <= DateTime.UtcNow)
            {
                // Fase 2: la sanción temporal parece vencida; la base decide con su propio reloj.
                var codigo = await FinalizarSuspensionVencidaAsync(connection, id);
                if (codigo != "APPLIED" && codigo != "ALREADY_ACTIVE")
                    return codigo == "NOT_DUE"
                        ? SuspendidoResult(sancionMotivo, sancionTipo, sancionFin)
                        : new LoginResult(LoginStatus.AccountInactive);
            }
            else
            {
                return SuspendidoResult(sancionMotivo, sancionTipo, sancionFin);
            }

            // Los administradores llevan su nivel en el token (permisos del panel).
            int? nivelAdmin = rolId == 1 ? await ObtenerNivelAdminAsync(id) : null;
            var token = GenerateJwtToken(loginDto.Email, rolId.ToString(), id.ToString(), nivelAdmin);

            return new LoginResult(LoginStatus.Success, new AuthResponseDto
            {
                Token = token,
                Email = loginDto.Email,
                NombreCompleto = nombre,
                FotoUrl = foto,
                UserId = id,
                RolId = rolId,
                EstadoAprobacion = estadoAprobacion
            });
        }

        private static LoginResult SuspendidoResult(string? motivo, string? tipo, DateTime? finUtc)
            => new(LoginStatus.AccountSuspended, null,
                new SuspensionInfoDto(motivo ?? "", tipo ?? "INDEFINIDA",
                    finUtc.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(finUtc.Value, DateTimeKind.Utc)) : null));

        private static async Task<string> FinalizarSuspensionVencidaAsync(SqlConnection connection, int usuarioId)
        {
            using var command = new SqlCommand("sp_FinalizarSuspensionTemporalVencida", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add("@UsuarioId", SqlDbType.Int).Value = usuarioId;

            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException("sp_FinalizarSuspensionTemporalVencida no devolvió resultado.");
            return (string)reader["ResultadoCodigo"];
        }

        public async Task<int> RegisterClientAsync(RegisterClientDto registerDto)
        {
            TerminosRegistro.Validar(registerDto.TerminosVersion, _config);
            var identidadGoogle = await ValidarRegistroGoogleAsync(registerDto.GoogleIdToken, registerDto.Email);
            var password = identidadGoogle != null && string.IsNullOrEmpty(registerDto.Password)
                ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)) : registerDto.Password;
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CrearUsuarioCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            
            command.Parameters.AddWithValue("@Email", registerDto.Email);
            command.Parameters.AddWithValue("@PasswordHash", HashPassword(password));
            command.Parameters.AddWithValue("@NombreCompleto", registerDto.NombreCompleto);
            command.Parameters.AddWithValue("@FotoUrl", (object?)registerDto.FotoUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@DireccionPrincipal", (object?)registerDto.DireccionPrincipal ?? DBNull.Value);
            command.Parameters.AddWithValue("@ContactoEmergenciaNombre", (object?)registerDto.ContactoEmergenciaNombre ?? DBNull.Value);
            command.Parameters.AddWithValue("@ContactoEmergenciaTelefono", (object?)registerDto.ContactoEmergenciaTelefono ?? DBNull.Value);
            command.Parameters.AddWithValue("@NacionalidadId", (object?)registerDto.NacionalidadId ?? DBNull.Value);
            command.Parameters.AddWithValue("@DocumentoIdentidad", (object?)registerDto.DocumentoIdentidad ?? DBNull.Value);
            command.Parameters.AddWithValue("@Telefono", (object?)registerDto.Telefono ?? DBNull.Value);
            command.Parameters.Add("@TerminosVersion", SqlDbType.NVarChar, 20).Value = registerDto.TerminosVersion!;
            command.Parameters.Add("@TerminosAceptadosEn", SqlDbType.DateTime2).Value = HoraLocalRD.Ahora;

            await connection.OpenAsync();
            using var transaction = identidadGoogle == null ? null : connection.BeginTransaction();
            command.Transaction = transaction;
            if (identidadGoogle != null) await ComprobarRegistroGoogleAsync(connection, transaction!, identidadGoogle);
            var result = await command.ExecuteScalarAsync();
            var nuevoId = Convert.ToInt32(result);
            if (identidadGoogle != null && nuevoId > 0)
                await EnlazarGoogleAsync(connection, transaction!, nuevoId, identidadGoogle.Sub);
            if (transaction != null) await transaction.CommitAsync();
            // Correo de bienvenida en segundo plano (no demora ni afecta el registro).
            if (nuevoId > 0)
                _ = Task.Run(() => new BienvenidaService(_config).EnviarClienteAsync(registerDto.Email, registerDto.NombreCompleto));
            return nuevoId;
        }

        public async Task<int> RegisterCaregiverAsync(RegisterCaregiverDto registerDto)
        {
            TerminosRegistro.Validar(registerDto.TerminosVersion, _config);
            var identidadGoogle = await ValidarRegistroGoogleAsync(registerDto.GoogleIdToken, registerDto.Email);
            var password = identidadGoogle != null && string.IsNullOrEmpty(registerDto.Password)
                ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)) : registerDto.Password;
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using (var migratorio = new SqlCommand("sp_PilotoMigratorioRegistro", connection) { CommandType = CommandType.StoredProcedure })
            {
                migratorio.Parameters.AddWithValue("@NacionalidadId", (object?)registerDto.NacionalidadId ?? DBNull.Value);
                migratorio.Parameters.AddWithValue("@EstatusId", (object?)registerDto.EstatusMigratorioId ?? DBNull.Value);
                using var reader = await migratorio.ExecuteReaderAsync();
                await reader.ReadAsync();
                var extranjero = Convert.ToBoolean(reader["EsExtranjero"]);
                var activo = Convert.ToBoolean(reader["EstatusActivo"]);
                DocumentosMigratorios.ValidarEstatus(registerDto.EstatusMigratorioId, extranjero, activo);
                var requisitos = new List<string>();
                await reader.NextResultAsync();
                while (await reader.ReadAsync()) requisitos.Add(reader["TipoDocumento"].ToString()!);
                var faltantes = DocumentosMigratorios.Faltantes(requisitos, registerDto.DocumentosExtra);
                if (extranjero && faltantes.Length > 0) throw new MigratorioException("DOCUMENTOS_MIGRATORIOS_FALTANTES", faltantes);
            }

            // Si el tipo de trabajo tiene test psicológico activo, debe venir un intento aprobado de la misma persona.
            var documento = TestPsicologicoService.NormalizarDocumento(registerDto.DocumentoIdentidad);
            var telefono = TestPsicologicoService.NormalizarTelefono(registerDto.Telefono);
            using (var validar = new SqlCommand("sp_TestValidarRegistro", connection))
            {
                validar.CommandType = CommandType.StoredProcedure;
                validar.Parameters.AddWithValue("@Especialidad", registerDto.Especialidad);
                validar.Parameters.AddWithValue("@Token", (object?)registerDto.TestToken ?? DBNull.Value);
                validar.Parameters.AddWithValue("@Documento", (object?)documento ?? DBNull.Value);
                validar.Parameters.AddWithValue("@Telefono", (object?)telefono ?? DBNull.Value);
                if ((await validar.ExecuteScalarAsync())?.ToString() != "OK")
                    throw new TestRequeridoException();
            }

            var codigoInvitacion = string.IsNullOrWhiteSpace(registerDto.CodigoInvitacion) ? null : registerDto.CodigoInvitacion.Trim().ToUpperInvariant();
            if (codigoInvitacion != null)
            {
                using var inv = new SqlCommand("sp_InvitacionObtener", connection) { CommandType = CommandType.StoredProcedure };
                inv.Parameters.AddWithValue("@Codigo", codigoInvitacion);
                inv.Parameters.AddWithValue("@Ahora", HoraLocalRD.Ahora);
                if ((await inv.ExecuteScalarAsync())?.ToString() != "OK")
                    throw new InvitacionInvalidaException();
            }

            using var transaction = connection.BeginTransaction();
            if (identidadGoogle != null) await ComprobarRegistroGoogleAsync(connection, transaction, identidadGoogle);
            using var command = new SqlCommand("sp_CrearUsuarioCuidador", connection, transaction);
            command.CommandType = CommandType.StoredProcedure;
            
            command.Parameters.AddWithValue("@Email", registerDto.Email);
            command.Parameters.AddWithValue("@PasswordHash", HashPassword(password));
            command.Parameters.AddWithValue("@NombreCompleto", registerDto.NombreCompleto);
            command.Parameters.AddWithValue("@FotoUrl", (object?)registerDto.FotoUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@Especialidad", registerDto.Especialidad);
            command.Parameters.AddWithValue("@TarifaHora", registerDto.TarifaHora);
            command.Parameters.AddWithValue("@Bio", (object?)registerDto.Bio ?? DBNull.Value);
            command.Parameters.AddWithValue("@MetodoCobro", (object?)registerDto.MetodoCobro ?? DBNull.Value);
            command.Parameters.AddWithValue("@CedulaUrl", (object?)registerDto.CedulaUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@CartaAntecedentesUrl", (object?)registerDto.CartaAntecedentesUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@NacionalidadId", (object?)registerDto.NacionalidadId ?? DBNull.Value);
            command.Parameters.AddWithValue("@DocumentoIdentidad", (object?)registerDto.DocumentoIdentidad ?? DBNull.Value);
            command.Parameters.AddWithValue("@Telefono", (object?)registerDto.Telefono ?? DBNull.Value);
            command.Parameters.Add("@TerminosVersion", SqlDbType.NVarChar, 20).Value = registerDto.TerminosVersion!;
            command.Parameters.Add("@TerminosAceptadosEn", SqlDbType.DateTime2).Value = HoraLocalRD.Ahora;
            command.Parameters.AddWithValue("@EstatusMigratorioId", (object?)registerDto.EstatusMigratorioId ?? DBNull.Value);
            var documentosExtra = registerDto.DocumentosExtra?
                .Where(d => !string.IsNullOrWhiteSpace(d.TipoDocumento) && !string.IsNullOrWhiteSpace(d.UrlArchivo))
                .Select(d => new { tipoDocumento = d.TipoDocumento.Trim(), urlArchivo = d.UrlArchivo.Trim() })
                .ToList();
            command.Parameters.AddWithValue("@DocumentosExtra", documentosExtra is { Count: > 0 }
                ? System.Text.Json.JsonSerializer.Serialize(documentosExtra)
                : DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            var nuevoId = Convert.ToInt32(result);

            if (nuevoId > 0 && registerDto.TestToken is Guid token)
            {
                using var vincular = new SqlCommand("sp_TestVincularIntento", connection, transaction);
                vincular.CommandType = CommandType.StoredProcedure;
                vincular.Parameters.AddWithValue("@Token", token);
                vincular.Parameters.AddWithValue("@UsuarioId", nuevoId);
                await vincular.ExecuteNonQueryAsync();
            }

            // Invitada por un cliente: queda vinculada y con contratos fijos activados.
            if (nuevoId > 0 && codigoInvitacion != null)
            {
                using var aceptar = new SqlCommand("sp_InvitacionAceptar", connection, transaction) { CommandType = CommandType.StoredProcedure };
                aceptar.Parameters.AddWithValue("@Codigo", codigoInvitacion);
                aceptar.Parameters.AddWithValue("@CuidadorId", nuevoId);
                aceptar.Parameters.AddWithValue("@Ahora", HoraLocalRD.Ahora);
                if ((await aceptar.ExecuteScalarAsync())?.ToString() != "OK")
                {
                    await transaction.RollbackAsync();
                    throw new InvitacionInvalidaException();
                }
            }

            if (identidadGoogle != null && nuevoId > 0)
                await EnlazarGoogleAsync(connection, transaction, nuevoId, identidadGoogle.Sub);
            await transaction.CommitAsync();

            // Correo de bienvenida en segundo plano (no demora ni afecta el registro).
            if (nuevoId > 0)
                _ = Task.Run(() => new BienvenidaService(_config).EnviarCuidadorAsync(registerDto.Email, registerDto.NombreCompleto));
            return nuevoId;
        }

        public async Task<(bool Success, Guid ResetToken, string Message)> ForgotPasswordAsync(ForgotPasswordDto dto)
        {
            using var connection = new SqlConnection(_connectionString);

            using var cmdFind = new SqlCommand("SELECT Id FROM Usuarios WHERE Email = @Email", connection);
            cmdFind.Parameters.AddWithValue("@Email", dto.Email);
            await connection.OpenAsync();
            var userId = await cmdFind.ExecuteScalarAsync();

            if (userId == null)
                return (false, Guid.Empty, "No se encontró una cuenta con ese correo");

            var code = new Random().Next(100000, 999999).ToString();
            var expiresAt = DateTime.UtcNow.AddMinutes(15);

            using var cmdReset = new SqlCommand("sp_CrearPasswordReset", connection);
            cmdReset.CommandType = CommandType.StoredProcedure;
            cmdReset.Parameters.AddWithValue("@UserId", Convert.ToInt32(userId));
            cmdReset.Parameters.AddWithValue("@Code", code);
            cmdReset.Parameters.AddWithValue("@ExpiresAt", expiresAt);

            var resetToken = (Guid)await cmdReset.ExecuteScalarAsync();

            try
            {
                var asunto = "CuidaApp - Código de recuperación de contraseña";
                var cuerpoHtml = $"""
                    <div style="font-family: Arial, sans-serif; max-width: 400px; margin: 0 auto; padding: 20px;">
                        <h2 style="color: #1C4D96; text-align: center;">CuidaApp</h2>
                        <p>Tu código de recuperación es:</p>
                        <div style="background: #F5F8FC; border: 1px solid #D9E2EC; border-radius: 8px; padding: 15px; text-align: center; font-size: 28px; font-weight: bold; letter-spacing: 8px; color: #0A2F41;">{code}</div>
                        <p style="color: #4B5563; font-size: 13px;">Este código expira en 15 minutos. Si no solicitaste este cambio, ignora este mensaje.</p>
                    </div>
                    """;
                await _emailService.EnviarAsync(dto.Email, asunto, cuerpoHtml);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando email de recuperación: {ex.Message}");
            }

            return (true, resetToken, "Código enviado");
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ActualizarPasswordConReset", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Email", dto.Email);
            command.Parameters.AddWithValue("@Code", dto.Code);
            command.Parameters.AddWithValue("@PasswordHash", HashPassword(dto.NewPassword));

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (Convert.ToBoolean(reader["Success"]), reader["Message"].ToString() ?? "");

            return (false, "Error al procesar la solicitud");
        }

        private async Task<int> ObtenerNivelAdminAsync(int usuarioId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_AdminObtenerNivel", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);
            await connection.OpenAsync();
            var nivel = await command.ExecuteScalarAsync();
            return nivel is null or DBNull ? 2 : Convert.ToInt32(nivel);
        }

        private string GenerateJwtToken(string email, string role, string userId, int? nivelAdmin = null)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var keyString = Seguridad.PoliticasAdmin.ClaveJwt(_config);
            
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }.ToList();
            if (nivelAdmin.HasValue)
                claims.Add(new Claim(Seguridad.PoliticasAdmin.ClaimNivel, nivelAdmin.Value.ToString()));

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"] ?? "Cuidapp",
                audience: jwtSettings["Audience"] ?? "CuidappApp",
                claims: claims,
                // La sesión del panel dura 8 h; la de la app 30 días (se renueva sola al abrir la app).
                expires: nivelAdmin.HasValue ? DateTime.UtcNow.AddHours(8) : DateTime.UtcNow.AddDays(30),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Renueva el token de la app (no del panel). Acepta un token vencido hace menos de 90 días si la firma es válida,
        /// para que las sesiones recordadas no tengan que volver a iniciar sesión. Null si no se puede renovar.
        /// </summary>
        public string? RenovarToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            var parametros = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _config["Jwt:Issuer"] ?? "Cuidapp",
                ValidateAudience = true,
                ValidAudience = _config["Jwt:Audience"] ?? "CuidappApp",
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Seguridad.PoliticasAdmin.ClaveJwt(_config)))
            };
            try
            {
                var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
                var principal = handler.ValidateToken(token, parametros, out var validado);
                if (validado.ValidTo < DateTime.UtcNow.AddDays(-90)) return null;
                if (principal.FindFirst(Seguridad.PoliticasAdmin.ClaimNivel) != null) return null; // tokens del panel no
                var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                var email = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
                var rol = (principal.FindFirst("role") ?? principal.FindFirst(ClaimTypes.Role))?.Value;
                if (string.IsNullOrEmpty(sub) || email == null || rol == null) return null;
                // No renovar tokens de cuentas desactivadas o anonimizadas; compara también el correo.
                using var connection = new SqlConnection(_connectionString);
                using var cuenta = new SqlCommand("sp_EliminacionTokenValido", connection) { CommandType = CommandType.StoredProcedure };
                cuenta.Parameters.AddWithValue("@UsuarioId", int.Parse(sub));
                cuenta.Parameters.AddWithValue("@Correo", email);
                connection.Open();
                if (Convert.ToInt32(cuenta.ExecuteScalar()) != 1) return null;
                return GenerateJwtToken(email, rol, sub);
            }
            catch (Exception) { return null; }
        }
    }
}
