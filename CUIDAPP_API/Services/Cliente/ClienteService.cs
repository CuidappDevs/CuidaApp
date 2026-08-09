using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.Cliente;
using CUIDAPP_API.Interfaces.Cliente;

namespace CUIDAPP_API.Services.Cliente
{
    public class ClienteService : IClienteService
    {
        private readonly string _connectionString;

        public ClienteService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<PerfilClienteDto?> ObtenerPerfilAsync(int clienteId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerPerfilCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", clienteId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new PerfilClienteDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                Email = reader["Email"].ToString() ?? "",
                NombreCompleto = reader["NombreCompleto"].ToString() ?? "",
                FotoUrl = reader["FotoUrl"] as string,
                DireccionPrincipal = reader["DireccionPrincipal"] as string,
                ContactoEmergenciaNombre = reader["ContactoEmergenciaNombre"] as string,
                ContactoEmergenciaTelefono = reader["ContactoEmergenciaTelefono"] as string
            };
        }
    }
}
