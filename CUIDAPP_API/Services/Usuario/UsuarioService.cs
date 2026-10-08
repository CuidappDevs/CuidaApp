using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.Interfaces.Usuario;

namespace CUIDAPP_API.Services.Usuario
{
    public class UsuarioService : IUsuarioService
    {
        private readonly string _connectionString;

        public UsuarioService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<bool?> ObtenerBienvenidaVistaAsync(int usuarioId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerBienvenidaVista", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);

            await connection.OpenAsync();
            var resultado = await command.ExecuteScalarAsync();
            return resultado == null || resultado == DBNull.Value ? null : Convert.ToBoolean(resultado);
        }

        public async Task<bool> MarcarBienvenidaVistaAsync(int usuarioId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_MarcarBienvenidaVista", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", usuarioId);

            await connection.OpenAsync();
            return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
        }
    }
}
