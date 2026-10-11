-- ============================================================================
-- Caja negra: grabación de audio de seguridad del servicio (TRANSPARENTE y consentida).
-- El cliente y el Care Partner la aceptan; mientras graba hay una notificación visible en el
-- teléfono del Care Partner. Al terminar el servicio el audio queda para el panel; solo el
-- Superadmin lo escucha, cada reproducción queda en auditoría y se borra solo a los 30 días si
-- no hubo disputa ni SOS. Correr DESPUÉS de llegada-cuidador.sql.
-- Idempotente. Requiere QUOTED_IDENTIFIER ON.
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Interruptor general (apagado por defecto): la administración lo enciende desde el panel.
IF NOT EXISTS (SELECT 1 FROM dbo.ConfiguracionPlataforma WHERE Clave = 'GrabacionSeguridad')
    INSERT INTO dbo.ConfiguracionPlataforma (Clave, Valor) VALUES ('GrabacionSeguridad', '0');
GO

-- Una fila por segmento de audio (se graba por tramos para no perder nada si se corta la conexión).
IF OBJECT_ID('dbo.GrabacionesServicio') IS NULL
BEGIN
    CREATE TABLE dbo.GrabacionesServicio (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        TrabajoId     INT           NOT NULL,
        CuidadorId    INT           NOT NULL,
        UrlArchivo    NVARCHAR(400) NOT NULL,
        Orden         INT           NOT NULL DEFAULT 0,       -- posición del segmento dentro del servicio
        DuracionSeg   INT           NULL,
        Fecha         DATETIME      NOT NULL,
        Borrado       BIT           NOT NULL DEFAULT 0        -- 1 = archivo ya eliminado del disco
    );
    CREATE INDEX IX_GrabacionesServicio_Trabajo ON dbo.GrabacionesServicio (TrabajoId, Orden);
    CREATE INDEX IX_GrabacionesServicio_Fecha ON dbo.GrabacionesServicio (Fecha) WHERE Borrado = 0;
END
GO

-- Marca en el servicio que su grabación está consentida y activa (para el panel y los reportes).
IF COL_LENGTH('dbo.Trabajos', 'GrabacionActiva') IS NULL
    ALTER TABLE dbo.Trabajos ADD GrabacionActiva BIT NOT NULL CONSTRAINT DF_Trabajos_GrabacionActiva DEFAULT 0;
GO

-- ---------- ¿Está encendida la grabación? La consulta la app al iniciar el servicio.
CREATE OR ALTER PROCEDURE dbo.sp_GrabacionSeguridadActiva
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(ISNULL((SELECT TRY_CAST(Valor AS INT) FROM dbo.ConfiguracionPlataforma WHERE Clave = 'GrabacionSeguridad'), 0) AS BIT) AS Activa;
END
GO

-- Encender/apagar desde el panel.
CREATE OR ALTER PROCEDURE dbo.sp_AdminGuardarGrabacionSeguridad @Activa BIT, @AdminId INT, @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ConfiguracionPlataforma SET Valor = CAST(@Activa AS NVARCHAR(2)), AdminId = @AdminId, FechaActualizacion = @Fecha
    WHERE Clave = 'GrabacionSeguridad';
    IF @@ROWCOUNT = 0
        INSERT INTO dbo.ConfiguracionPlataforma (Clave, Valor, AdminId, FechaActualizacion) VALUES ('GrabacionSeguridad', CAST(@Activa AS NVARCHAR(2)), @AdminId, @Fecha);
    SELECT 'OK' AS Resultado;
END
GO

-- La app marca que este servicio quedó con grabación consentida (al iniciar con el PIN).
CREATE OR ALTER PROCEDURE dbo.sp_MarcarGrabacionServicio @TrabajoId INT, @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Trabajos SET GrabacionActiva = 1 WHERE Id = @TrabajoId AND CuidadorId = @CuidadorId AND Estado IN (3, 7);
    SELECT IIF(@@ROWCOUNT = 0, 'NO_ENCONTRADO', 'OK') AS Resultado;
END
GO

-- La app registra un segmento de audio ya subido.
CREATE OR ALTER PROCEDURE dbo.sp_RegistrarSegmentoGrabacion
    @TrabajoId INT, @CuidadorId INT, @UrlArchivo NVARCHAR(400), @DuracionSeg INT, @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    -- Solo acepta segmentos de un servicio real de ese Care Partner (en curso o recién terminado).
    IF NOT EXISTS (SELECT 1 FROM Trabajos WHERE Id = @TrabajoId AND CuidadorId = @CuidadorId AND Estado IN (3, 4, 7))
    BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END

    DECLARE @Orden INT = (SELECT ISNULL(MAX(Orden), -1) + 1 FROM GrabacionesServicio WHERE TrabajoId = @TrabajoId);
    INSERT INTO GrabacionesServicio (TrabajoId, CuidadorId, UrlArchivo, Orden, DuracionSeg, Fecha)
    VALUES (@TrabajoId, @CuidadorId, @UrlArchivo, @Orden, @DuracionSeg, @Fecha);
    SELECT 'OK' AS Resultado;
END
GO

-- Segmentos de un servicio (para el reproductor del panel). No incluye los ya borrados.
CREATE OR ALTER PROCEDURE dbo.sp_AdminGrabacionesServicio @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT g.Id, g.UrlArchivo, g.Orden, g.DuracionSeg, g.Fecha
    FROM GrabacionesServicio g
    WHERE g.TrabajoId = @TrabajoId AND g.Borrado = 0
    ORDER BY g.Orden;

    SELECT CAST(t.GrabacionActiva AS BIT) AS GrabacionActiva,
           (SELECT SUM(ISNULL(DuracionSeg, 0)) FROM GrabacionesServicio WHERE TrabajoId = @TrabajoId AND Borrado = 0) AS DuracionTotal,
           t.Estado,
           CAST(IIF(EXISTS (SELECT 1 FROM GrabacionesServicio WHERE TrabajoId = @TrabajoId AND Borrado = 1), 1, 0) AS BIT) AS HayBorrados
    FROM Trabajos t WHERE t.Id = @TrabajoId;
END
GO

-- Lista de segmentos vencidos para borrar del disco (los borra el servicio de fondo de la API).
-- Se conservan si el servicio tuvo disputa abierta o una alerta SOS.
CREATE OR ALTER PROCEDURE dbo.sp_GrabacionesParaBorrar @Hoy DATETIME, @DiasRetencion INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    SELECT g.Id, g.UrlArchivo
    FROM GrabacionesServicio g
    WHERE g.Borrado = 0 AND g.Fecha < DATEADD(DAY, -@DiasRetencion, @Hoy)
      AND NOT EXISTS (SELECT 1 FROM Disputas d WHERE d.TrabajoId = g.TrabajoId AND d.Estado = 1)
      AND NOT EXISTS (SELECT 1 FROM SOSAlertas s WHERE s.TrabajoId = g.TrabajoId);
END
GO

-- Marca un segmento como borrado una vez eliminado el archivo.
CREATE OR ALTER PROCEDURE dbo.sp_MarcarGrabacionBorrada @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE GrabacionesServicio SET Borrado = 1, UrlArchivo = '' WHERE Id = @Id;
END
GO

-- ---------- Detalle de un servicio (igual que en llegada-cuidador.sql + GrabacionActiva para el reproductor)
CREATE OR ALTER PROCEDURE dbo.sp_AdminTrabajoDetalle @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.Id, t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas,
           t.Latitud, t.Longitud, t.FechaCreacion, t.FechaLlegada, t.FechaInicioReal, t.FechaFin, t.JustificacionFinalizacion,
           t.RechazadoPorCliente, t.PagoDisputado, t.MetodoPago, t.GrabacionActiva,
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
