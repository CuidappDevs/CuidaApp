using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.Cuidador;
using CUIDAPP_API.Interfaces.Cuidador;

namespace CUIDAPP_API.Services.Cuidador
{
    public class CuidadorService : ICuidadorService
    {
        private readonly string _connectionString;

        public CuidadorService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<int> SubirDocumentoAsync(SubirDocumentoDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_SubirDocumentoCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@CuidadorId", dto.CuidadorId);
            command.Parameters.AddWithValue("@TipoDocumento", dto.TipoDocumento);
            command.Parameters.AddWithValue("@UrlArchivo", dto.UrlArchivo);

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
    }
}
