using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.Admin;
using CUIDAPP_API.Interfaces.Admin;

namespace CUIDAPP_API.Services.Admin
{
    public class AdminService : IAdminService
    {
        private readonly string _connectionString;

        public AdminService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
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
            var rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
    }
}
