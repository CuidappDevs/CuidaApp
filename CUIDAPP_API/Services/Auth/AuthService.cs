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

namespace CUIDAPP_API.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;

        public AuthService(IConfiguration config, IEmailService emailService)
        {
            _config = config;
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

        public async Task<AuthResponseDto?> LoginAsync(LoginRequestDto loginDto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerUsuarioPorEmail", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Email", loginDto.Email);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var storedHash = reader["PasswordHash"].ToString();
                
                // Verificación de contraseña usando el hash
                if (storedHash == HashPassword(loginDto.Password)) 
                {
                    var id = Convert.ToInt32(reader["Id"]);
                    var rolId = Convert.ToInt32(reader["RolId"]);
                    var isActive = Convert.ToBoolean(reader["IsActive"]);
                    var estadoAprobacion = reader["EstadoAprobacion"] == DBNull.Value
                        ? (int?)null
                        : Convert.ToInt32(reader["EstadoAprobacion"]);

                    if (!isActive) return null;

                    var token = GenerateJwtToken(loginDto.Email, rolId.ToString(), id.ToString());

                    return new AuthResponseDto
                    {
                        Token = token,
                        Email = loginDto.Email,
                        NombreCompleto = reader["NombreCompleto"] as string,
                        FotoUrl = reader["FotoUrl"] as string,
                        UserId = id,
                        RolId = rolId,
                        EstadoAprobacion = estadoAprobacion
                    };
                }
            }
            return null;
        }

        public async Task<int> RegisterClientAsync(RegisterClientDto registerDto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CrearUsuarioCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            
            command.Parameters.AddWithValue("@Email", registerDto.Email);
            command.Parameters.AddWithValue("@PasswordHash", HashPassword(registerDto.Password));
            command.Parameters.AddWithValue("@NombreCompleto", registerDto.NombreCompleto);
            command.Parameters.AddWithValue("@FotoUrl", (object?)registerDto.FotoUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@DireccionPrincipal", (object?)registerDto.DireccionPrincipal ?? DBNull.Value);
            command.Parameters.AddWithValue("@ContactoEmergenciaNombre", (object?)registerDto.ContactoEmergenciaNombre ?? DBNull.Value);
            command.Parameters.AddWithValue("@ContactoEmergenciaTelefono", (object?)registerDto.ContactoEmergenciaTelefono ?? DBNull.Value);

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<int> RegisterCaregiverAsync(RegisterCaregiverDto registerDto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CrearUsuarioCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            
            command.Parameters.AddWithValue("@Email", registerDto.Email);
            command.Parameters.AddWithValue("@PasswordHash", HashPassword(registerDto.Password));
            command.Parameters.AddWithValue("@NombreCompleto", registerDto.NombreCompleto);
            command.Parameters.AddWithValue("@FotoUrl", (object?)registerDto.FotoUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@Especialidad", registerDto.Especialidad);
            command.Parameters.AddWithValue("@TarifaHora", registerDto.TarifaHora);
            command.Parameters.AddWithValue("@Bio", (object?)registerDto.Bio ?? DBNull.Value);
            command.Parameters.AddWithValue("@MetodoCobro", (object?)registerDto.MetodoCobro ?? DBNull.Value);
            command.Parameters.AddWithValue("@CedulaUrl", (object?)registerDto.CedulaUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("@CartaAntecedentesUrl", (object?)registerDto.CartaAntecedentesUrl ?? DBNull.Value);

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
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

        private string GenerateJwtToken(string email, string role, string userId)
        {
            var jwtSettings = _config.GetSection("Jwt");
            var keyString = jwtSettings["Key"] ?? "TuSuperClaveSecretaMuyLargaParaQueSeaSegura123!";
            
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"] ?? "Cuidapp",
                audience: jwtSettings["Audience"] ?? "CuidappApp",
                claims: claims,
                expires: DateTime.Now.AddHours(2),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
