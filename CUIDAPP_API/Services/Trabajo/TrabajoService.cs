using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CUIDAPP_API.DTOs.Trabajo;
using CUIDAPP_API.Interfaces.Trabajo;
using CUIDAPP_API.Services.Realtime;

namespace CUIDAPP_API.Services.Trabajo
{
    public class TrabajoService : ITrabajoService
    {
        private readonly string _connectionString;
        private readonly ITrabajoNotifier _notifier;

        public TrabajoService(IConfiguration config, ITrabajoNotifier notifier)
        {
            _connectionString = config.GetConnectionString("DefaultConnection") ?? "";
            _notifier = notifier;
        }

        public async Task<int> CrearTrabajoAsync(CrearTrabajoDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CrearTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;

            command.Parameters.AddWithValue("@ClienteId", dto.ClienteId);
            command.Parameters.AddWithValue("@CuidadorId", dto.CuidadorId);
            command.Parameters.AddWithValue("@TipoServicio", dto.TipoServicio);
            command.Parameters.AddWithValue("@Fecha", dto.Fecha);
            command.Parameters.AddWithValue("@HoraInicio", dto.HoraInicio);
            command.Parameters.AddWithValue("@HoraFin", dto.HoraFin);
            command.Parameters.AddWithValue("@Direccion", (object?)dto.Direccion ?? DBNull.Value);
            command.Parameters.AddWithValue("@Tarifa", dto.Tarifa);
            command.Parameters.AddWithValue("@Latitud", (object?)dto.Latitud ?? DBNull.Value);
            command.Parameters.AddWithValue("@Longitud", (object?)dto.Longitud ?? DBNull.Value);

            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            var trabajoId = Convert.ToInt32(result);

            await _notifier.NotificarAsync(dto.CuidadorId, "NuevaSolicitud", new { TrabajoId = trabajoId, dto.ClienteId });

            return trabajoId;
        }

        private async Task<(int ClienteId, int CuidadorId)?> ObtenerParticipantesAsync(int trabajoId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("SELECT ClienteId, CuidadorId FROM Trabajos WHERE Id = @TrabajoId", connection);
            command.Parameters.AddWithValue("@TrabajoId", trabajoId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (Convert.ToInt32(reader["ClienteId"]), Convert.ToInt32(reader["CuidadorId"]));

            return null;
        }

        public async Task<IEnumerable<TrabajoDto>> ObtenerTrabajosPorCuidadorAsync(int cuidadorId)
        {
            var trabajos = new List<TrabajoDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerTrabajosPorCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@CuidadorId", cuidadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                trabajos.Add(MapearTrabajo(reader));
            }

            return trabajos;
        }

        public async Task<TrabajoDto?> ObtenerProximoTrabajoAsync(int cuidadorId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerProximoTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@CuidadorId", cuidadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return MapearTrabajo(reader);

            return null;
        }

        public async Task<bool> ActualizarEstadoTrabajoAsync(ActualizarEstadoTrabajoDto dto)
        {
            var participantes = await ObtenerParticipantesAsync(dto.TrabajoId);

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ActualizarEstadoTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", dto.TrabajoId);
            command.Parameters.AddWithValue("@NuevoEstado", dto.NuevoEstado);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            var success = Convert.ToInt32(filasAfectadas) > 0;

            if (success && participantes.HasValue)
            {
                await _notifier.NotificarAsync(participantes.Value.ClienteId, "TrabajoActualizado", new { dto.TrabajoId, Estado = dto.NuevoEstado });
                await _notifier.NotificarAsync(participantes.Value.CuidadorId, "TrabajoActualizado", new { dto.TrabajoId, Estado = dto.NuevoEstado });
            }

            return success;
        }

        private static TrabajoClienteDto MapearTrabajoCliente(SqlDataReader reader)
        {
            return new TrabajoClienteDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                ClienteId = Convert.ToInt32(reader["ClienteId"]),
                CuidadorId = Convert.ToInt32(reader["CuidadorId"]),
                CuidadorNombre = reader["CuidadorNombre"].ToString() ?? "",
                CuidadorFotoUrl = reader["CuidadorFotoUrl"] as string,
                TipoServicio = reader["TipoServicio"].ToString() ?? "",
                Fecha = Convert.ToDateTime(reader["Fecha"]),
                HoraInicio = (TimeSpan)reader["HoraInicio"],
                HoraFin = (TimeSpan)reader["HoraFin"],
                Direccion = reader["Direccion"] as string,
                Estado = Convert.ToInt32(reader["Estado"]),
                Tarifa = Convert.ToDecimal(reader["Tarifa"]),
                Notas = reader["Notas"] as string,
                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                PinInicio = reader["PinInicio"] as string,
                PinFin = reader["PinFin"] as string,
                FechaInicioReal = reader["FechaInicioReal"] as DateTime?,
                JustificacionFinalizacion = reader["JustificacionFinalizacion"] as string,
                RechazadoPorCliente = Convert.ToBoolean(reader["RechazadoPorCliente"])
            };
        }

        public async Task<TrabajoClienteDto?> ObtenerTrabajoActivoPorClienteAsync(int clienteId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerTrabajoActivoPorCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ClienteId", clienteId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return MapearTrabajoCliente(reader);

            return null;
        }

        public async Task<IEnumerable<TrabajoClienteDto>> ObtenerTrabajosActivosPorClienteAsync(int clienteId)
        {
            var trabajos = new List<TrabajoClienteDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerTrabajosActivosPorCliente", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ClienteId", clienteId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                trabajos.Add(MapearTrabajoCliente(reader));

            return trabajos;
        }

        public async Task<TrabajoClienteDto?> ObtenerTrabajoPorIdAsync(int trabajoId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerTrabajoPorId", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", trabajoId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return MapearTrabajoCliente(reader);

            return null;
        }

        public async Task<(bool Success, string Motivo)> IniciarTrabajoAsync(IniciarTrabajoDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_IniciarTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", dto.TrabajoId);
            command.Parameters.AddWithValue("@Pin", dto.Pin);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var filasAfectadas = Convert.ToInt32(reader["FilasAfectadas"]);
                var motivo = reader["Motivo"].ToString() ?? "ERROR_DESCONOCIDO";
                var success = filasAfectadas > 0;

                if (success)
                {
                    reader.Close();
                    var participantes = await ObtenerParticipantesAsync(dto.TrabajoId);
                    if (participantes.HasValue)
                        await _notifier.NotificarAsync(participantes.Value.ClienteId, "TrabajoActualizado", new { dto.TrabajoId, Estado = 3 });
                }

                return (success, motivo);
            }
            return (false, "ERROR_DESCONOCIDO");
        }

        public async Task<(bool Success, string Motivo)> FinalizarTrabajoAsync(FinalizarTrabajoDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_FinalizarTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", dto.TrabajoId);
            command.Parameters.AddWithValue("@Pin", dto.Pin);
            command.Parameters.AddWithValue("@Justificacion", (object?)dto.Justificacion ?? DBNull.Value);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var filasAfectadas = Convert.ToInt32(reader["FilasAfectadas"]);
                var motivo = reader["Motivo"].ToString() ?? "ERROR_DESCONOCIDO";
                var success = filasAfectadas > 0;

                if (success)
                {
                    // Pasa a Estado 7 (esperando confirmación del cliente): el pago y el
                    // Estado 4 (Completado) solo se generan cuando el cliente confirma.
                    reader.Close();
                    var participantes = await ObtenerParticipantesAsync(dto.TrabajoId);
                    if (participantes.HasValue)
                    {
                        await _notifier.NotificarAsync(participantes.Value.ClienteId, "TrabajoActualizado", new { dto.TrabajoId, Estado = 7 });
                        await _notifier.NotificarAsync(participantes.Value.CuidadorId, "TrabajoActualizado", new { dto.TrabajoId, Estado = 7 });
                    }
                }

                return (success, motivo);
            }
            return (false, "ERROR_DESCONOCIDO");
        }

        public async Task<(bool Success, string Motivo)> ConfirmarFinalizacionAsync(int trabajoId, int clienteId, bool confirmado)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ConfirmarFinalizacionTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", trabajoId);
            command.Parameters.AddWithValue("@ClienteId", clienteId);
            command.Parameters.AddWithValue("@Confirmado", confirmado);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var filasAfectadas = Convert.ToInt32(reader["FilasAfectadas"]);
                var motivo = reader["Motivo"].ToString() ?? "ERROR_DESCONOCIDO";
                var success = filasAfectadas > 0;

                if (success)
                {
                    reader.Close();
                    var participantes = await ObtenerParticipantesAsync(trabajoId);
                    if (participantes.HasValue)
                    {
                        var nuevoEstado = confirmado ? 4 : 3;
                        await _notifier.NotificarAsync(participantes.Value.ClienteId, "TrabajoActualizado", new { TrabajoId = trabajoId, Estado = nuevoEstado });
                        await _notifier.NotificarAsync(participantes.Value.CuidadorId, "TrabajoActualizado", new { TrabajoId = trabajoId, Estado = nuevoEstado });
                    }
                }

                return (success, motivo);
            }
            return (false, "ERROR_DESCONOCIDO");
        }

        public async Task<(bool Success, string Motivo)> ForzarFinalizacionAsync(int trabajoId, int cuidadorId)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ForzarFinalizacionSinPago", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", trabajoId);
            command.Parameters.AddWithValue("@CuidadorId", cuidadorId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var filasAfectadas = Convert.ToInt32(reader["FilasAfectadas"]);
                var motivo = reader["Motivo"].ToString() ?? "ERROR_DESCONOCIDO";
                var success = filasAfectadas > 0;

                if (success)
                {
                    reader.Close();
                    var participantes = await ObtenerParticipantesAsync(trabajoId);
                    if (participantes.HasValue)
                        await _notifier.NotificarAsync(participantes.Value.ClienteId, "TrabajoActualizado", new { TrabajoId = trabajoId, Estado = 4 });
                }

                return (success, motivo);
            }
            return (false, "ERROR_DESCONOCIDO");
        }

        public async Task<IEnumerable<ActividadTrabajoDto>> ObtenerActividadesAsync(int trabajoId)
        {
            var actividades = new List<ActividadTrabajoDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerActividadesTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", trabajoId);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                actividades.Add(new ActividadTrabajoDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    TrabajoId = Convert.ToInt32(reader["TrabajoId"]),
                    Descripcion = reader["Descripcion"].ToString() ?? "",
                    FechaHora = Convert.ToDateTime(reader["FechaHora"])
                });
            }

            return actividades;
        }

        public async Task<ActividadTrabajoDto> AgregarActividadAsync(AgregarActividadTrabajoDto dto)
        {
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_AgregarActividadTrabajo", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", dto.TrabajoId);
            command.Parameters.AddWithValue("@Descripcion", dto.Descripcion);

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            var actividad = new ActividadTrabajoDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                TrabajoId = dto.TrabajoId,
                Descripcion = dto.Descripcion,
                FechaHora = Convert.ToDateTime(reader["FechaHora"])
            };

            reader.Close();
            var participantes = await ObtenerParticipantesAsync(dto.TrabajoId);
            if (participantes.HasValue)
            {
                await _notifier.NotificarAsync(participantes.Value.ClienteId, "ActividadAgregada", actividad);
            }

            return actividad;
        }

        public async Task AlertarGeocercaAsync(int trabajoId, double distanciaMetros)
        {
            var participantes = await ObtenerParticipantesAsync(trabajoId);
            if (!participantes.HasValue)
                return;

            await _notifier.NotificarAsync(participantes.Value.ClienteId, "AlertaGeocerca", new { TrabajoId = trabajoId, DistanciaMetros = distanciaMetros });
        }

        public async Task<IEnumerable<MotivoCancelacionDto>> ObtenerMotivosCancelacionAsync()
        {
            var motivos = new List<MotivoCancelacionDto>();
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_ObtenerMotivosCancelacion", connection);
            command.CommandType = CommandType.StoredProcedure;

            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                motivos.Add(new MotivoCancelacionDto
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    Descripcion = reader["Descripcion"].ToString() ?? ""
                });
            }

            return motivos;
        }

        public async Task<bool> CancelarTrabajoCuidadorAsync(CancelarTrabajoDto dto)
        {
            var participantes = await ObtenerParticipantesAsync(dto.TrabajoId);

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand("sp_CancelarTrabajoCuidador", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@TrabajoId", dto.TrabajoId);
            command.Parameters.AddWithValue("@MotivoId", dto.MotivoId);
            command.Parameters.AddWithValue("@MotivoTexto", (object?)dto.MotivoTexto ?? DBNull.Value);

            await connection.OpenAsync();
            var filasAfectadas = await command.ExecuteScalarAsync();
            var success = Convert.ToInt32(filasAfectadas) > 0;

            if (success && participantes.HasValue)
                await _notifier.NotificarAsync(participantes.Value.ClienteId, "TrabajoActualizado", new { dto.TrabajoId, Estado = 5 });

            return success;
        }

        private static TrabajoDto MapearTrabajo(SqlDataReader reader)
        {
            return new TrabajoDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                ClienteId = Convert.ToInt32(reader["ClienteId"]),
                ClienteNombre = reader["ClienteNombre"].ToString() ?? "",
                ClienteFotoUrl = reader["ClienteFotoUrl"] as string,
                TipoServicio = reader["TipoServicio"].ToString() ?? "",
                Fecha = Convert.ToDateTime(reader["Fecha"]),
                HoraInicio = (TimeSpan)reader["HoraInicio"],
                HoraFin = (TimeSpan)reader["HoraFin"],
                Direccion = reader["Direccion"] as string,
                Estado = Convert.ToInt32(reader["Estado"]),
                Tarifa = Convert.ToDecimal(reader["Tarifa"]),
                Notas = reader["Notas"] as string,
                FechaCreacion = Convert.ToDateTime(reader["FechaCreacion"]),
                Latitud = reader["Latitud"] as decimal?,
                Longitud = reader["Longitud"] as decimal?,
                RechazadoPorCliente = Convert.ToBoolean(reader["RechazadoPorCliente"]),
                PagoDisputado = Convert.ToBoolean(reader["PagoDisputado"])
            };
        }
    }
}
