-- ============================================================================
-- Centro de mando del panel ("Ojo de Dios"): historial de ubicaciones (estelas y
-- reproducción de recorridos), batería, avisos directos a una persona y el
-- "¿Estás bien?" con respuesta (si no responde a tiempo se crea un SOS).
-- Idempotente. Requiere QUOTED_IDENTIFIER ON.
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- Historial de ubicaciones del Care Partner (se guarda solo mientras está visible o en servicio)
IF OBJECT_ID('dbo.UbicacionesHistorial') IS NULL
BEGIN
    CREATE TABLE dbo.UbicacionesHistorial (
        Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
        CuidadorId  INT          NOT NULL,
        TrabajoId   INT          NULL,
        Latitud     DECIMAL(9,6) NOT NULL,
        Longitud    DECIMAL(9,6) NOT NULL,
        Bateria     TINYINT      NULL,
        Fecha       DATETIME     NOT NULL
    );
    CREATE INDEX IX_UbicacionesHistorial_Cuidador_Fecha ON dbo.UbicacionesHistorial (CuidadorId, Fecha DESC);
    CREATE INDEX IX_UbicacionesHistorial_Trabajo ON dbo.UbicacionesHistorial (TrabajoId, Fecha) WHERE TrabajoId IS NOT NULL;
END
GO

IF COL_LENGTH('dbo.PerfilCuidador', 'UltimaUbicacion') IS NULL
    ALTER TABLE dbo.PerfilCuidador ADD UltimaUbicacion DATETIME NULL, Bateria TINYINT NULL;
GO

-- La app manda la ubicación (y la batería). Se guarda en el historial si está visible o con un servicio en curso.
-- Se borra lo que tenga más de 30 días de ese mismo cuidador.
CREATE OR ALTER PROCEDURE dbo.sp_ActualizarUbicacionCuidador
    @CuidadorId INT,
    @Latitud DECIMAL(9,6),
    @Longitud DECIMAL(9,6),
    @Bateria TINYINT = NULL,
    @Fecha DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @Fecha = ISNULL(@Fecha, GETDATE());
    UPDATE PerfilCuidador SET Latitud = @Latitud, Longitud = @Longitud, UltimaUbicacion = @Fecha, Bateria = ISNULL(@Bateria, Bateria)
    WHERE UsuarioId = @CuidadorId;
    DECLARE @Filas INT = @@ROWCOUNT;

    DECLARE @TrabajoId INT = (SELECT TOP 1 Id FROM Trabajos WHERE CuidadorId = @CuidadorId AND Estado IN (3, 7) ORDER BY FechaInicioReal DESC);
    IF @Filas > 0 AND (@TrabajoId IS NOT NULL OR EXISTS (SELECT 1 FROM PerfilCuidador WHERE UsuarioId = @CuidadorId AND Disponible = 1))
    BEGIN
        INSERT INTO UbicacionesHistorial (CuidadorId, TrabajoId, Latitud, Longitud, Bateria, Fecha)
        VALUES (@CuidadorId, @TrabajoId, @Latitud, @Longitud, @Bateria, @Fecha);
        DELETE FROM UbicacionesHistorial WHERE CuidadorId = @CuidadorId AND Fecha < DATEADD(DAY, -30, @Fecha);
    END

    SELECT @Filas AS FilasAfectadas;
END;
GO

-- ---------- Avisos directos a una persona y "¿Estás bien?"
IF OBJECT_ID('dbo.AvisosDirectos') IS NULL
BEGIN
    CREATE TABLE dbo.AvisosDirectos (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        AdminId         INT           NOT NULL,
        UsuarioId       INT           NOT NULL,
        Tipo            VARCHAR(10)   NOT NULL,       -- 'mensaje' o 'checkin'
        Titulo          NVARCHAR(120) NOT NULL,
        Mensaje         NVARCHAR(500) NOT NULL,
        Fecha           DATETIME      NOT NULL,
        VenceEn         DATETIME      NULL,           -- checkin: si no responde antes, se crea un SOS
        Respuesta       VARCHAR(10)   NULL,           -- 'ok', 'ayuda' o 'vencido'
        FechaRespuesta  DATETIME      NULL
    );
    CREATE INDEX IX_AvisosDirectos_Pendientes ON dbo.AvisosDirectos (Tipo, Respuesta, VenceEn);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminRegistrarAvisoDirecto
    @AdminId INT, @UsuarioId INT, @Tipo VARCHAR(10), @Titulo NVARCHAR(120), @Mensaje NVARCHAR(500), @Fecha DATETIME, @VenceEn DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @UsuarioId) BEGIN SELECT CAST(0 AS INT) AS Id; RETURN; END
    INSERT INTO AvisosDirectos (AdminId, UsuarioId, Tipo, Titulo, Mensaje, Fecha, VenceEn)
    VALUES (@AdminId, @UsuarioId, @Tipo, @Titulo, @Mensaje, @Fecha, @VenceEn);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
GO

-- Respuesta desde la app. Devuelve el aviso (para avisar al panel y, si pide ayuda, crear el SOS).
CREATE OR ALTER PROCEDURE dbo.sp_ResponderCheckin @Id INT, @UsuarioId INT, @Respuesta VARCHAR(10), @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE AvisosDirectos SET Respuesta = @Respuesta, FechaRespuesta = @Fecha
    WHERE Id = @Id AND UsuarioId = @UsuarioId AND Tipo = 'checkin' AND Respuesta IS NULL;
    IF @@ROWCOUNT = 0 BEGIN SELECT 'NO_PENDIENTE' AS Resultado; RETURN; END

    SELECT 'OK' AS Resultado, a.Id, a.UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, u.RolId,
           (SELECT TOP 1 t.Id FROM Trabajos t WHERE (t.CuidadorId = a.UsuarioId OR t.ClienteId = a.UsuarioId) AND t.Estado IN (2, 3, 7) ORDER BY t.Fecha DESC) AS TrabajoId,
           CAST(COALESCE(pc.Latitud, 0) AS FLOAT) AS Latitud, CAST(COALESCE(pc.Longitud, 0) AS FLOAT) AS Longitud
    FROM AvisosDirectos a JOIN Usuarios u ON u.Id = a.UsuarioId LEFT JOIN PerfilCuidador pc ON pc.UsuarioId = a.UsuarioId
    WHERE a.Id = @Id;
END
GO

-- "¿Estás bien?" sin respuesta a tiempo: se marcan como vencidos y se devuelven para crear el SOS.
CREATE OR ALTER PROCEDURE dbo.sp_CheckinsVencidos @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @V TABLE (Id INT);
    UPDATE AvisosDirectos SET Respuesta = 'vencido', FechaRespuesta = @Ahora
    OUTPUT inserted.Id INTO @V
    WHERE Tipo = 'checkin' AND Respuesta IS NULL AND VenceEn <= @Ahora;

    SELECT a.Id, a.UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, u.RolId,
           (SELECT TOP 1 t.Id FROM Trabajos t WHERE (t.CuidadorId = a.UsuarioId OR t.ClienteId = a.UsuarioId) AND t.Estado IN (2, 3, 7) ORDER BY t.Fecha DESC) AS TrabajoId,
           COALESCE(pc.Latitud, 0) AS Latitud, COALESCE(pc.Longitud, 0) AS Longitud
    FROM AvisosDirectos a JOIN @V v ON v.Id = a.Id JOIN Usuarios u ON u.Id = a.UsuarioId
    LEFT JOIN PerfilCuidador pc ON pc.UsuarioId = a.UsuarioId;
END
GO

-- ---------- Mapa: agrega última señal y batería a los Care Partners (resto igual que antes)
CREATE OR ALTER PROCEDURE dbo.sp_AdminMapa
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.Id AS UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, u.FotoUrl, pc.Especialidad,
           CAST(pc.Latitud AS FLOAT) AS Latitud, CAST(pc.Longitud AS FLOAT) AS Longitud,
           CAST(IIF(EXISTS (SELECT 1 FROM Trabajos t WHERE t.CuidadorId = u.Id AND t.Estado IN (3, 7)), 1, 0) AS BIT) AS EnServicio,
           pc.UltimaUbicacion, pc.Bateria
    FROM PerfilCuidador pc JOIN Usuarios u ON u.Id = pc.UsuarioId
    WHERE (pc.Disponible = 1 AND pc.EstadoAprobacion = 2 AND u.IsActive = 1
           OR EXISTS (SELECT 1 FROM Trabajos t WHERE t.CuidadorId = u.Id AND t.Estado IN (3, 7)))
      AND pc.Latitud IS NOT NULL AND pc.Longitud IS NOT NULL;

    SELECT t.Id, t.TipoServicio, t.Estado, t.Direccion, t.HoraInicio, t.HoraFin,
           CAST(t.Latitud AS FLOAT) AS Latitud, CAST(t.Longitud AS FLOAT) AS Longitud,
           ISNULL(c.NombreCompleto, c.Email) AS ClienteNombre, ISNULL(q.NombreCompleto, q.Email) AS CuidadorNombre,
           t.CuidadorId, CAST(pc.Latitud AS FLOAT) AS CuidadorLatitud, CAST(pc.Longitud AS FLOAT) AS CuidadorLongitud
    FROM Trabajos t JOIN Usuarios c ON c.Id = t.ClienteId JOIN Usuarios q ON q.Id = t.CuidadorId
    LEFT JOIN PerfilCuidador pc ON pc.UsuarioId = t.CuidadorId
    WHERE t.Estado IN (2, 3, 7) AND t.Latitud IS NOT NULL AND t.Longitud IS NOT NULL;

    SELECT a.Id, a.TrabajoId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, a.TipoUsuario, a.Latitud, a.Longitud, a.FechaCreacion, a.Origen
    FROM SOSAlertas a JOIN Usuarios u ON u.Id = a.UsuarioId
    WHERE a.Estado = 'Pendiente';
END
GO

-- Ficha rápida de una persona en el mapa (3 resultados: persona, servicio en curso o próximo, avisos recientes).
CREATE OR ALTER PROCEDURE dbo.sp_AdminFichaMapa @UsuarioId INT, @Hoy DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.Id AS UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, u.Email, u.Telefono, u.FotoUrl, u.RolId,
           pc.Especialidad, pc.Disponible, pc.HorarioAutomatico, pc.EstadoAprobacion, pc.UltimaUbicacion, pc.Bateria,
           CAST(pc.Latitud AS FLOAT) AS Latitud, CAST(pc.Longitud AS FLOAT) AS Longitud,
           pc.ContactoEmergenciaNombre, pc.ContactoEmergenciaTelefono,
           (SELECT CAST(AVG(CAST(c.Puntuacion AS DECIMAL(5,2))) AS DECIMAL(5,2)) FROM Calificaciones c WHERE c.CalificadoId = u.Id) AS Calificacion,
           (SELECT COUNT(*) FROM Trabajos t WHERE t.CuidadorId = u.Id AND t.Estado = 4) AS Completados,
           (SELECT COUNT(*) FROM Trabajos t WHERE t.CuidadorId = u.Id AND t.Fecha = @Hoy AND t.Estado NOT IN (5, 6)) AS ServiciosHoy
    FROM Usuarios u LEFT JOIN PerfilCuidador pc ON pc.UsuarioId = u.Id
    WHERE u.Id = @UsuarioId;

    SELECT TOP 1 t.Id, t.TipoServicio, t.Estado, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion,
           CAST(t.Latitud AS FLOAT) AS Latitud, CAST(t.Longitud AS FLOAT) AS Longitud,
           ISNULL(c.NombreCompleto, c.Email) AS ClienteNombre, c.Telefono AS ClienteTelefono, t.FechaInicioReal,
           (SELECT COUNT(*) FROM TareasTrabajo x WHERE x.TrabajoId = t.Id) AS Tareas,
           (SELECT COUNT(*) FROM TareasTrabajo x WHERE x.TrabajoId = t.Id AND x.Completada = 1) AS TareasHechas
    FROM Trabajos t JOIN Usuarios c ON c.Id = t.ClienteId
    WHERE t.CuidadorId = @UsuarioId AND t.Estado IN (2, 3, 7)
    ORDER BY IIF(t.Estado IN (3, 7), 0, 1), t.Fecha, t.HoraInicio;

    SELECT TOP 5 a.Id, a.Tipo, a.Titulo, a.Fecha, a.Respuesta, a.FechaRespuesta, ISNULL(u.NombreCompleto, u.Email) AS AdminNombre
    FROM AvisosDirectos a LEFT JOIN Usuarios u ON u.Id = a.AdminId
    WHERE a.UsuarioId = @UsuarioId ORDER BY a.Fecha DESC;
END
GO

-- Estelas: puntos de los últimos @Minutos de quienes están en el mapa.
CREATE OR ALTER PROCEDURE dbo.sp_AdminEstelas @Desde DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SELECT h.CuidadorId, CAST(h.Latitud AS FLOAT) AS Latitud, CAST(h.Longitud AS FLOAT) AS Longitud, h.Fecha
    FROM UbicacionesHistorial h
    WHERE h.Fecha >= @Desde
    ORDER BY h.CuidadorId, h.Fecha;
END
GO

-- Recorrido para reproducir: de un servicio (de su inicio a su fin) o de un Care Partner en un rango.
CREATE OR ALTER PROCEDURE dbo.sp_AdminRecorrido @TrabajoId INT = NULL, @CuidadorId INT = NULL, @Desde DATETIME = NULL, @Hasta DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @TrabajoId IS NOT NULL
        SELECT CAST(Latitud AS FLOAT) AS Latitud, CAST(Longitud AS FLOAT) AS Longitud, Fecha, Bateria
        FROM UbicacionesHistorial WHERE TrabajoId = @TrabajoId ORDER BY Fecha;
    ELSE
        SELECT CAST(Latitud AS FLOAT) AS Latitud, CAST(Longitud AS FLOAT) AS Longitud, Fecha, Bateria
        FROM UbicacionesHistorial WHERE CuidadorId = @CuidadorId AND Fecha BETWEEN @Desde AND @Hasta ORDER BY Fecha;
END
GO

-- Mapa de calor: dónde se piden servicios (últimos @Dias días).
CREATE OR ALTER PROCEDURE dbo.sp_AdminDemanda @Desde DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(ROUND(Latitud, 3) AS FLOAT) AS Latitud, CAST(ROUND(Longitud, 3) AS FLOAT) AS Longitud, COUNT(*) AS Peso
    FROM Trabajos
    WHERE Fecha >= @Desde AND Latitud IS NOT NULL AND Longitud IS NOT NULL
    GROUP BY ROUND(Latitud, 3), ROUND(Longitud, 3);
END
GO

-- Alertas del centro de mando (además de los SOS): servicios que no empezaron a tiempo y Care Partners en servicio sin señal.
CREATE OR ALTER PROCEDURE dbo.sp_AdminAlertasMapa @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 'sin_iniciar' AS Tipo, t.Id AS TrabajoId, t.CuidadorId, ISNULL(q.NombreCompleto, q.Email) AS Nombre,
           t.TipoServicio, CAST(t.Fecha AS DATETIME) + CAST(t.HoraInicio AS DATETIME) AS Desde,
           CAST(t.Latitud AS FLOAT) AS Latitud, CAST(t.Longitud AS FLOAT) AS Longitud
    FROM Trabajos t JOIN Usuarios q ON q.Id = t.CuidadorId
    WHERE t.Estado = 2 AND CAST(t.Fecha AS DATETIME) + CAST(t.HoraInicio AS DATETIME) < DATEADD(MINUTE, -15, @Ahora)
      AND t.Fecha >= DATEADD(DAY, -1, CAST(@Ahora AS DATE))
    UNION ALL
    SELECT 'sin_senal', t.Id, t.CuidadorId, ISNULL(q.NombreCompleto, q.Email), t.TipoServicio, pc.UltimaUbicacion,
           CAST(pc.Latitud AS FLOAT), CAST(pc.Longitud AS FLOAT)
    FROM Trabajos t JOIN Usuarios q ON q.Id = t.CuidadorId JOIN PerfilCuidador pc ON pc.UsuarioId = t.CuidadorId
    WHERE t.Estado = 3 AND (pc.UltimaUbicacion IS NULL OR pc.UltimaUbicacion < DATEADD(MINUTE, -10, @Ahora));
END
GO

-- Ocultar a un Care Partner de inmediato (también apaga su horario automático para que no vuelva a aparecer solo).
CREATE OR ALTER PROCEDURE dbo.sp_AdminOcultarCuidador @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE PerfilCuidador SET Disponible = 0, HorarioAutomatico = 0 WHERE UsuarioId = @CuidadorId;
    SELECT IIF(@@ROWCOUNT > 0, 'OK', 'NO_ENCONTRADO') AS Resultado;
END
GO
