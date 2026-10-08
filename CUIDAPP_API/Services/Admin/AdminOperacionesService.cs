using System.Data;
using Microsoft.Data.SqlClient;
using CUIDAPP_API.DTOs.AdminOperaciones;
using CUIDAPP_API.Interfaces.Admin;

namespace CUIDAPP_API.Services.Admin
{
    public class AdminOperacionesService : IAdminOperacionesService
    {
        private readonly string _connectionString;

        public AdminOperacionesService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public async Task<List<List<Fila>>> ConsultarAsync(string sp, params (string Nombre, object? Valor)[] parametros)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = Crear(sp, connection, parametros);
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            var resultados = new List<List<Fila>>();
            do
            {
                var filas = new List<Fila>();
                while (await reader.ReadAsync())
                    filas.Add(LeerFila(reader));
                resultados.Add(filas);
            } while (await reader.NextResultAsync());

            return resultados;
        }

        public async Task<Fila?> EjecutarAsync(string sp, params (string Nombre, object? Valor)[] parametros)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = Crear(sp, connection, parametros);
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? LeerFila(reader) : null;
        }

        private static SqlCommand Crear(string sp, SqlConnection connection, (string Nombre, object? Valor)[] parametros)
        {
            var command = new SqlCommand(sp, connection) { CommandType = CommandType.StoredProcedure };
            foreach (var (nombre, valor) in parametros)
                command.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
            return command;
        }

        private static Fila LeerFila(SqlDataReader reader)
        {
            var fila = new Fila();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var valor = reader.IsDBNull(i) ? null : reader.GetValue(i);
                // TIME y DATE viajan como texto legible ("08:30", "2026-10-07").
                fila[reader.GetName(i)] = valor switch
                {
                    TimeSpan t => t.ToString(@"hh\:mm"),
                    DateOnly d => d.ToString("yyyy-MM-dd"),
                    _ => valor
                };
            }
            return fila;
        }
    }
}
