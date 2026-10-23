using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.DTOs.TestPsicologico;
using CUIDAPP_API.Interfaces.TestPsicologico;

namespace CUIDAPP_API.Services.TestPsicologico
{
    public class TestPsicologicoService : ITestPsicologicoService
    {
        private readonly string _connectionString;

        public TestPsicologicoService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        // Los bloqueos se buscan por documento o teléfono: se guardan siempre con el mismo formato.
        public static string? NormalizarDocumento(string? documento)
        {
            var limpio = new string((documento ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            return limpio.Length == 0 ? null : limpio;
        }

        public static string? NormalizarTelefono(string? telefono)
        {
            var limpio = new string((telefono ?? "").Where(char.IsDigit).ToArray());
            return limpio.Length == 0 ? null : limpio;
        }

        private static object Db(object? valor) => valor ?? DBNull.Value;

        public async Task<EstadoTestDto> EstadoAsync(int tipoTrabajoId, string? documento, string? telefono)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return await LeerEstadoAsync(connection, tipoTrabajoId, NormalizarDocumento(documento), NormalizarTelefono(telefono));
        }

        private static async Task<EstadoTestDto> LeerEstadoAsync(SqlConnection connection, int tipoTrabajoId, string? documento, string? telefono)
        {
            using var command = new SqlCommand("sp_TestEstadoCandidato", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TipoTrabajoId", tipoTrabajoId);
            command.Parameters.AddWithValue("@Documento", Db(documento));
            command.Parameters.AddWithValue("@Telefono", Db(telefono));
            command.Parameters.AddWithValue("@Ahora", HoraLocalRD.Ahora);

            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return new EstadoTestDto();

            return new EstadoTestDto
            {
                Estado = reader["Estado"].ToString() ?? "SIN_TEST",
                TestId = reader["TestId"] == DBNull.Value ? null : Convert.ToInt32(reader["TestId"]),
                Token = reader["Token"] == DBNull.Value ? null : (Guid)reader["Token"],
                MinutosLimite = reader["MinutosLimite"] == DBNull.Value ? null : Convert.ToInt32(reader["MinutosLimite"]),
                TotalPreguntas = reader["TotalPreguntas"] == DBNull.Value ? null : Convert.ToInt32(reader["TotalPreguntas"]),
                BloqueadoHasta = reader["BloqueadoHasta"] == DBNull.Value ? null : Convert.ToDateTime(reader["BloqueadoHasta"])
            };
        }

        public async Task<(TestParaResolverDto? Test, EstadoTestDto Estado)> IniciarAsync(IniciarTestDto dto)
        {
            var documento = NormalizarDocumento(dto.Documento);
            var telefono = NormalizarTelefono(dto.Telefono);

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var estado = await LeerEstadoAsync(connection, dto.TipoTrabajoId, documento, telefono);
            if (estado.Estado != "LIBRE" || estado.TestId == null)
                return (null, estado);

            using var command = new SqlCommand("sp_TestIniciarIntento", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TestId", estado.TestId.Value);
            command.Parameters.AddWithValue("@TipoTrabajoId", dto.TipoTrabajoId);
            command.Parameters.AddWithValue("@Nombre", Db(dto.Nombre?.Trim()));
            command.Parameters.AddWithValue("@Email", Db(dto.Email?.Trim()));
            command.Parameters.AddWithValue("@Documento", Db(documento));
            command.Parameters.AddWithValue("@Telefono", Db(telefono));
            command.Parameters.AddWithValue("@Ahora", HoraLocalRD.Ahora);

            using var reader = await command.ExecuteReaderAsync();
            var test = new TestParaResolverDto();
            if (await reader.ReadAsync())
            {
                test.Token = (Guid)reader["Token"];
                test.Nombre = reader["Nombre"].ToString() ?? "";
                test.Descripcion = reader["Descripcion"] == DBNull.Value ? null : reader["Descripcion"].ToString();
                test.MinutosLimite = Convert.ToInt32(reader["MinutosLimite"]);
            }

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
                test.Preguntas.Add(new PreguntaTestDto { Id = Convert.ToInt32(reader["Id"]), Texto = reader["Texto"].ToString() ?? "" });

            await reader.NextResultAsync();
            var porPregunta = test.Preguntas.ToDictionary(p => p.Id);
            while (await reader.ReadAsync())
            {
                if (porPregunta.TryGetValue(Convert.ToInt32(reader["PreguntaId"]), out var pregunta))
                    pregunta.Opciones.Add(new OpcionTestDto { Id = Convert.ToInt32(reader["Id"]), Texto = reader["Texto"].ToString() ?? "" });
            }

            // Mezcla las opciones para que la correcta no quede siempre en el mismo lugar.
            foreach (var pregunta in test.Preguntas)
                pregunta.Opciones = pregunta.Opciones.OrderBy(_ => Random.Shared.Next()).ToList();

            return (test, estado);
        }

        public async Task<ResultadoTestDto?> FinalizarAsync(FinalizarTestDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_TestFinalizarIntento", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@Token", dto.Token);
            command.Parameters.AddWithValue("@Respuestas", JsonSerializer.Serialize(
                (dto.Respuestas ?? new()).Select(r => new { preguntaId = r.PreguntaId, opcionId = r.OpcionId })));
            command.Parameters.AddWithValue("@Ahora", HoraLocalRD.Ahora);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader["Codigo"].ToString() != "OK")
                return null;

            return new ResultadoTestDto
            {
                Resultado = Convert.ToInt32(reader["Resultado"]),
                Porcentaje = Convert.ToDecimal(reader["Porcentaje"]),
                Correctas = Convert.ToInt32(reader["Correctas"]),
                TotalPreguntas = Convert.ToInt32(reader["TotalPreguntas"]),
                BloqueadoHasta = reader["BloqueadoHasta"] == DBNull.Value ? null : Convert.ToDateTime(reader["BloqueadoHasta"])
            };
        }
    }
}
