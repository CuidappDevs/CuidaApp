-- ============================================================================
-- Aviso de llegada: cuando el Care Partner llega al lugar de un servicio aceptado, se avisa
-- al cliente (con el PIN para empezar). Correr DESPUÉS de calidad-documentos-comision-disputas.sql
-- (reemplaza sp_AdminTrabajoDetalle para mostrar la llegada en el panel).
-- Idempotente. Requiere QUOTED_IDENTIFIER ON.
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('dbo.Trabajos', 'FechaLlegada') IS NULL
    ALTER TABLE dbo.Trabajos ADD FechaLlegada DATETIME NULL;
GO

-- Lo llama la API con cada ubicación del Care Partner. Si está a @RadioMetros o menos de un servicio
-- aceptado (Estado 2) que empieza entre 2 h antes y 3 h después de ahora, y todavía no se avisó,
-- marca la llegada y devuelve los datos del aviso. Si no, no devuelve filas.
CREATE OR ALTER PROCEDURE dbo.sp_DetectarLlegadaCuidador
    @CuidadorId INT, @Latitud FLOAT, @Longitud FLOAT, @Fecha DATETIME, @RadioMetros INT = 150
AS
BEGIN
    SET NOCOUNT ON;
    IF @Latitud NOT BETWEEN -90 AND 90 OR @Longitud NOT BETWEEN -180 AND 180 RETURN;
    DECLARE @Punto GEOGRAPHY = GEOGRAPHY::Point(@Latitud, @Longitud, 4326);
    DECLARE @Marcado TABLE (TrabajoId INT, ClienteId INT, PinInicio CHAR(4), TipoServicio NVARCHAR(100));

    WITH Candidato AS (
        SELECT TOP 1 t.*
        FROM Trabajos t
        WHERE t.CuidadorId = @CuidadorId AND t.Estado = 2 AND t.FechaLlegada IS NULL
          AND t.Latitud IS NOT NULL AND t.Longitud IS NOT NULL
          AND DATEDIFF(MINUTE, @Fecha, CAST(t.Fecha AS DATETIME) + CAST(t.HoraInicio AS DATETIME)) BETWEEN -180 AND 120
          AND GEOGRAPHY::Point(CAST(t.Latitud AS FLOAT), CAST(t.Longitud AS FLOAT), 4326).STDistance(@Punto) <= @RadioMetros
        ORDER BY t.Fecha, t.HoraInicio
    )
    UPDATE Candidato SET FechaLlegada = @Fecha
    OUTPUT inserted.Id, inserted.ClienteId, inserted.PinInicio, inserted.TipoServicio INTO @Marcado;

    SELECT m.TrabajoId, m.ClienteId, RTRIM(m.PinInicio) AS PinInicio, m.TipoServicio,
           ISNULL(u.NombreCompleto, u.Email) AS CuidadorNombre
    FROM @Marcado m JOIN Usuarios u ON u.Id = @CuidadorId;
END
GO

-- ---------- Detalle de un servicio (igual que antes + FechaLlegada)
CREATE OR ALTER PROCEDURE dbo.sp_AdminTrabajoDetalle @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.Id, t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas,
           t.Latitud, t.Longitud, t.FechaCreacion, t.FechaLlegada, t.FechaInicioReal, t.FechaFin, t.JustificacionFinalizacion,
           t.RechazadoPorCliente, t.PagoDisputado, t.MetodoPago,
           ISNULL(m.Descripcion, t.MotivoCancelacionTexto) AS MotivoCancelacion, t.MotivoCancelacionTexto,
           t.ClienteId, ISNULL(c.NombreCompleto, c.Email) AS ClienteNombre, c.Email AS ClienteEmail, c.Telefono AS ClienteTelefono, c.FotoUrl AS ClienteFoto,
           t.CuidadorId, ISNULL(q.NombreCompleto, q.Email) AS CuidadorNombre, q.Email AS CuidadorEmail, q.Telefono AS CuidadorTelefono, q.FotoUrl AS CuidadorFoto,
           (SELECT TOP 1 d.Id FROM Disputas d WHERE d.TrabajoId = t.Id) AS DisputaId
    FROM Trabajos t
    JOIN Usuarios c ON c.Id = t.ClienteId
    JOIN Usuarios q ON q.Id = t.CuidadorId
    LEFT JOIN MotivosCancelacion m ON m.Id = t.MotivoCancelacionId
    WHERE t.Id = @TrabajoId;

    SELECT Id, Descripcion, Orden, Completada, FechaCompletada FROM TareasTrabajo WHERE TrabajoId = @TrabajoId ORDER BY Orden;
    SELECT Id, Descripcion, FechaHora FROM ActividadesTrabajo WHERE TrabajoId = @TrabajoId ORDER BY FechaHora;
    SELECT p.Id, p.Monto, p.Propina, p.Estado, p.FechaCreacion, p.FechaAutorizacion, p.FechaPago,
           ISNULL(p.ComisionPorcentaje, dbo.fn_ComisionServicio(t.TipoServicio)) AS ComisionPorcentaje
    FROM Pagos p JOIN Trabajos t ON t.Id = p.TrabajoId WHERE p.TrabajoId = @TrabajoId;
    SELECT c.Id, c.CalificadorId, ISNULL(u.NombreCompleto, u.Email) AS CalificadorNombre, c.Puntuacion, c.Comentario, c.FechaCreacion
    FROM Calificaciones c JOIN Usuarios u ON u.Id = c.CalificadorId WHERE c.TrabajoId = @TrabajoId;
    SELECT m.Id, m.RemitenteId, ISNULL(u.NombreCompleto, u.Email) AS RemitenteNombre, m.Contenido, m.Tipo, m.UrlArchivo,
           m.DuracionSegundos, m.FechaEnvio, m.Eliminado
    FROM Mensajes m JOIN Conversaciones cv ON cv.ID = m.ConversacionId JOIN Usuarios u ON u.Id = m.RemitenteId
    WHERE cv.TrabajoID = @TrabajoId ORDER BY m.FechaEnvio;
    SELECT Id, TipoUsuario, Motivo, Estado, Origen, FechaCreacion, AtendidoPor FROM SOSAlertas WHERE TrabajoId = @TrabajoId ORDER BY FechaCreacion;
    SELECT Id, Asunto, Categoria, Estado, FechaCreacion FROM Tickets WHERE TrabajoId = @TrabajoId ORDER BY FechaCreacion;
END
GO
