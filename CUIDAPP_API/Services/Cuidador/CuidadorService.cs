using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.Cuidador;
using CUIDAPP_API.Interfaces.Cuidador;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.Cuidador
{
    public class CuidadorService : ICuidadorService
    {
        private readonly string _connectionString;
        private readonly ITrabajoNotifier _notifier;

        public CuidadorService(IConfiguration config, ITrabajoNotifier notifier)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
            _notifier = notifier;
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

        public async Task<EstadoVerificacionDto> ObtenerEstadoVerificacionAsync(int cuidadorId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerEstadoVerificacionCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@CuidadorId", cuidadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            var dto = new EstadoVerificacionDto();

            if (await reader.ReadAsync())
            {
                dto.EstadoAprobacion = Convert.ToInt32(reader["EstadoAprobacion"]);
            }

            await reader.NextResultAsync();

            while (await reader.ReadAsync())
            {
                dto.Documentos.Add(new DocumentoEstadoDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    TipoDocumento = reader["TipoDocumento"].ToString() ?? "",
                    UrlArchivo = reader["UrlArchivo"].ToString() ?? "",
                    Estado = Convert.ToInt32(reader["Estado"]),
                    ObservacionesAdmin = reader["ObservacionesAdmin"] as string,
                    FechaSubida = Convert.ToDateTime(reader["FechaSubida"])
                });
            }

            return dto;
        }

        public async Task<PerfilCuidadorDto?> ObtenerPerfilAsync(int cuidadorId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerPerfilCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UsuarioId", cuidadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return new PerfilCuidadorDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                Email = reader["Email"].ToString() ?? "",
                NombreCompleto = reader["NombreCompleto"].ToString() ?? "",
                FotoUrl = reader["FotoUrl"] as string,
                Especialidad = reader["Especialidad"] as string,
                TarifaHora = Convert.ToDecimal(reader["TarifaHora"]),
                Bio = reader["Bio"] as string,
                MetodoCobro = reader["MetodoCobro"] as string,
                EstadoAprobacion = Convert.ToInt32(reader["EstadoAprobacion"]),
                Disponible = Convert.ToBoolean(reader["Disponible"])
            };
        }

        public async Task<bool> ActualizarDisponibilidadAsync(ActualizarDisponibilidadDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ActualizarDisponibilidadCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@CuidadorId", dto.CuidadorId);
            command.Parameters.AddWithValue("@Disponible", dto.Disponible);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            var success = Convert.ToInt32(filasAfectadas) > 0;

            if (success)
                await _notifier.NotificarGlobalAsync("DisponibilidadCambio", new { dto.CuidadorId, dto.Disponible });

            return success;
        }

        public async Task<bool> ActualizarUbicacionAsync(ActualizarUbicacionDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ActualizarUbicacionCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@CuidadorId", dto.CuidadorId);
            command.Parameters.AddWithValue("@Latitud", dto.Latitud);
            command.Parameters.AddWithValue("@Longitud", dto.Longitud);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            var success = Convert.ToInt32(filasAfectadas) > 0;

            if (success)
                await _notifier.NotificarGlobalAsync("UbicacionCuidadorCambio", new { dto.CuidadorId, dto.Latitud, dto.Longitud });

            return success;
        }

        public async Task<GananciasDto> ObtenerGananciasAsync(int cuidadorId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerGananciasCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@CuidadorId", cuidadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new GananciasDto
                {
                    GanadoHoy = Convert.ToDecimal(reader["GanadoHoy"]),
                    PendientePorCobrar = Convert.ToDecimal(reader["PendientePorCobrar"]),
                    TotalCobrado = Convert.ToDecimal(reader["TotalCobrado"])
                };
            }

            return new GananciasDto();
        }

        public async Task<IEnumerable<PagoDto>> ObtenerPagosAsync(int cuidadorId)
        {
            var pagos = new List<PagoDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerPagosPorCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@CuidadorId", cuidadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                pagos.Add(new PagoDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    TrabajoId = Convert.ToInt32(reader["TrabajoId"]),
                    TipoServicio = reader["TipoServicio"].ToString() ?? "",
                    ClienteNombre = reader["ClienteNombre"].ToString() ?? "",
                    Monto = Convert.ToDecimal(reader["Monto"]),
                    Estado = Convert.ToInt32(reader["Estado"]),
                    FechaPago = reader["FechaPago"] == DBNull.Value ? null : Convert.ToDateTime(reader["FechaPago"]),
                    FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"])
                });
            }

            return pagos;
        }
    }
}
