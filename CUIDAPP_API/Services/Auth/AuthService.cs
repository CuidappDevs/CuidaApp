using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using CUIDAPP_API.DTOs.Auth;
using CUIDAPP_API.Interfaces.Auth;

namespace CUIDAPP_API.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;

        public AuthService(IConfiguration config)
        {
            _config = config;
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

                    if (!isActive) return null;

                    var token = GenerateJwtToken(loginDto.Email, rolId.ToString(), id.ToString());

                    return new AuthResponseDto
                    {
                        Token = token,
                        Email = loginDto.Email,
                        RolId = rolId
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
