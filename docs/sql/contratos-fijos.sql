-- ============================================================================
-- Contratos fijos (cliente contrata a un Care Partner con un horario semanal).
--
-- Flujo:
--   1. El cliente pide permiso para contratar (PermisoContratacion) y valida cada domicilio
--      donde quiere contratar (DomiciliosContratacion). La administración aprueba ambos.
--   2. El cliente propone (Contratos.Estado 1 = espera al cuidador). El cuidador acepta, rechaza
--      o contrapropone (Estado 2 = espera al cliente) y así hasta que uno acepta.
--   3. Aceptado → Estado 3 (por firmar). Cada parte firma con el dedo + código por correo.
--      Con las dos firmas la API genera el PDF, guarda su hash y el contrato pasa a 4 (activo).
--   4. Activo: sp_ContratosGenerarTrabajos crea los Trabajos de los próximos 7 días (Estado 2,
--      ya aceptados) según ContratoHorario, salvo ausencias. Cada jornada es un servicio normal.
--   5. Pausar (5), reanudar, terminar (FechaFin = hoy + preaviso → 6 al pasar la fecha).
--   Otros: 7 rechazado, 8 vencido (72 h sin respuesta), 9 retirado por el cliente.
--
-- Rol 4 "Recurrente" no se usa: el permiso es del cliente (RolId 2).
-- DiaSemana: 0 domingo … 6 sábado (igual que DayOfWeek de .NET).
-- Horario en parámetros: texto "dia|HH:mm|HH:mm;dia|HH:mm|HH:mm".
-- Idempotente. Requiere QUOTED_IDENTIFIER ON.
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- Tablas ----------------------------------------------------------
IF COL_LENGTH('dbo.PerfilCuidador', 'AceptaContratos') IS NULL
    ALTER TABLE dbo.PerfilCuidador ADD AceptaContratos BIT NOT NULL CONSTRAINT DF_PerfilCuidador_AceptaContratos DEFAULT 0;
GO

IF OBJECT_ID('dbo.PermisoContratacion') IS NULL
CREATE TABLE dbo.PermisoContratacion (
    ClienteId          INT NOT NULL PRIMARY KEY REFERENCES dbo.Usuarios (Id),
    Estado             TINYINT NOT NULL,          -- 1 en revisión, 2 aprobado, 3 rechazado, 4 revocado
    Motivo             NVARCHAR(600) NOT NULL,    -- para quién es el cuidado y por qué
    CedulaUrl          NVARCHAR(400) NULL,
    Referencia         NVARCHAR(300) NULL,
    FechaSolicitud     DATETIME NOT NULL,
    FechaRevision      DATETIME NULL,
    RevisadoPorAdminId INT NULL,
    ComentarioAdmin    NVARCHAR(600) NULL
);
GO

IF OBJECT_ID('dbo.DomiciliosContratacion') IS NULL
BEGIN
    CREATE TABLE dbo.DomiciliosContratacion (
        Id                 INT IDENTITY(1,1) PRIMARY KEY,
        ClienteId          INT NOT NULL REFERENCES dbo.Usuarios (Id),
        UbicacionId        INT NOT NULL REFERENCES dbo.UbicacionesCliente (Id),
        ComprobanteUrl     NVARCHAR(400) NOT NULL,
        QuienVive          NVARCHAR(300) NOT NULL,
        Estado             TINYINT NOT NULL,      -- 1 en revisión, 2 aprobado, 3 rechazado
        FechaSolicitud     DATETIME NOT NULL,
        FechaRevision      DATETIME NULL,
        RevisadoPorAdminId INT NULL,
        ComentarioAdmin    NVARCHAR(600) NULL
    );
    CREATE UNIQUE INDEX UX_DomiciliosContratacion_Ubicacion ON dbo.DomiciliosContratacion (UbicacionId);
END
GO

IF OBJECT_ID('dbo.Contratos') IS NULL
BEGIN
    CREATE TABLE dbo.Contratos (
        Id                INT IDENTITY(1,1) PRIMARY KEY,
        ClienteId         INT NOT NULL REFERENCES dbo.Usuarios (Id),
        CuidadorId        INT NOT NULL REFERENCES dbo.Usuarios (Id),
        UbicacionId       INT NOT NULL REFERENCES dbo.UbicacionesCliente (Id),
        TipoServicio      NVARCHAR(100) NOT NULL,
        TarifaHora        DECIMAL(10,2) NOT NULL,
        MetodoPago        VARCHAR(20) NOT NULL,
        FechaInicio       DATE NOT NULL,
        FechaFin          DATE NULL,
        DiasPreaviso      INT NOT NULL CONSTRAINT DF_Contratos_Preaviso DEFAULT 7,
        Nota              NVARCHAR(600) NULL,
        Estado            TINYINT NOT NULL,
        FechaCreacion     DATETIME NOT NULL,
        FechaUltimaAccion DATETIME NOT NULL,
        FechaActivacion   DATETIME NULL,
        FechaTerminacion  DATETIME NULL,
        TerminadoPor      INT NULL,
        MotivoTerminacion NVARCHAR(600) NULL
    );
    CREATE INDEX IX_Contratos_Cliente ON dbo.Contratos (ClienteId, Estado);
    CREATE INDEX IX_Contratos_Cuidador ON dbo.Contratos (CuidadorId, Estado);
END
GO

IF OBJECT_ID('dbo.ContratoHorario') IS NULL
CREATE TABLE dbo.ContratoHorario (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    ContratoId INT NOT NULL REFERENCES dbo.Contratos (Id),
    DiaSemana  TINYINT NOT NULL CHECK (DiaSemana BETWEEN 0 AND 6),
    HoraInicio TIME(0) NOT NULL,
    HoraFin    TIME(0) NOT NULL,
    CONSTRAINT CK_ContratoHorario_Rango CHECK (HoraFin > HoraInicio)
);
GO

IF OBJECT_ID('dbo.ContratoPropuestas') IS NULL
CREATE TABLE dbo.ContratoPropuestas (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    ContratoId    INT NOT NULL REFERENCES dbo.Contratos (Id),
    Version       INT NOT NULL,
    PropuestoPor  INT NOT NULL,
    TarifaHora    DECIMAL(10,2) NOT NULL,
    FechaInicio   DATE NOT NULL,
    Horario       NVARCHAR(400) NOT NULL,
    Nota          NVARCHAR(600) NULL,
    Fecha         DATETIME NOT NULL
);
GO

IF OBJECT_ID('dbo.ContratoAusencias') IS NULL
BEGIN
    CREATE TABLE dbo.ContratoAusencias (
        Id         INT IDENTITY(1,1) PRIMARY KEY,
        ContratoId INT NOT NULL REFERENCES dbo.Contratos (Id),
        Fecha      DATE NOT NULL,
        MarcadoPor INT NOT NULL,
        Motivo     NVARCHAR(300) NULL,
        FechaRegistro DATETIME NOT NULL
    );
    CREATE UNIQUE INDEX UX_ContratoAusencias ON dbo.ContratoAusencias (ContratoId, Fecha);
END
GO

IF OBJECT_ID('dbo.ContratoCodigos') IS NULL
CREATE TABLE dbo.ContratoCodigos (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    ContratoId INT NOT NULL REFERENCES dbo.Contratos (Id),
    UsuarioId  INT NOT NULL,
    CodigoHash VARCHAR(64) NOT NULL,
    Expira     DATETIME NOT NULL,
    Intentos   INT NOT NULL CONSTRAINT DF_ContratoCodigos_Intentos DEFAULT 0,
    Usado      BIT NOT NULL CONSTRAINT DF_ContratoCodigos_Usado DEFAULT 0
);
GO

-- Reenviar no borra el historial de intentos de cada parte.
IF COL_LENGTH('dbo.ContratoCodigos', 'FechaCreacion') IS NULL
    ALTER TABLE dbo.ContratoCodigos ADD FechaCreacion DATETIME NULL;
GO
IF COL_LENGTH('dbo.ContratoCodigos', 'BloqueadoHasta') IS NULL
    ALTER TABLE dbo.ContratoCodigos ADD BloqueadoHasta DATETIME NULL;
GO
IF OBJECT_ID('dbo.ContratoCodigoFallos') IS NULL
    CREATE TABLE dbo.ContratoCodigoFallos (
        Id INT IDENTITY PRIMARY KEY, ContratoId INT NOT NULL REFERENCES dbo.Contratos(Id), UsuarioId INT NOT NULL, Fecha DATETIME NOT NULL
    );
GO

IF OBJECT_ID('dbo.ContratoFirmas') IS NULL
BEGIN
    CREATE TABLE dbo.ContratoFirmas (
        Id         INT IDENTITY(1,1) PRIMARY KEY,
        ContratoId INT NOT NULL REFERENCES dbo.Contratos (Id),
        UsuarioId  INT NOT NULL,
        FirmaSvg   NVARCHAR(MAX) NOT NULL,
        Fecha      DATETIME NOT NULL,
        Ip         VARCHAR(64) NULL,
        Latitud    DECIMAL(9,6) NULL,
        Longitud   DECIMAL(9,6) NULL,
        Correo     NVARCHAR(256) NULL       -- a donde llegó el código de verificación
    );
    CREATE UNIQUE INDEX UX_ContratoFirmas ON dbo.ContratoFirmas (ContratoId, UsuarioId);
END
GO

IF OBJECT_ID('dbo.ContratoDocumentos') IS NULL
CREATE TABLE dbo.ContratoDocumentos (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    ContratoId INT NOT NULL REFERENCES dbo.Contratos (Id),
    Version    INT NOT NULL,
    Tipo       NVARCHAR(40) NOT NULL,      -- 'Contrato original'
    Ruta       NVARCHAR(400) NOT NULL,     -- relativa a la carpeta privada de la API
    Hash       VARCHAR(64) NOT NULL,       -- SHA-256 del PDF
    Fecha      DATETIME NOT NULL
);
GO

IF COL_LENGTH('dbo.Trabajos', 'ContratoId') IS NULL
    ALTER TABLE dbo.Trabajos ADD ContratoId INT NULL;
GO

-- Fecha y HoraInicio forman el inicio de una jornada (DATE + TIME).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Trabajos') AND name = 'UX_Trabajos_ContratoInicio')
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Trabajos WHERE ContratoId IS NOT NULL GROUP BY ContratoId, Fecha, HoraInicio HAVING COUNT(*) > 1)
        THROW 50002, 'Existen jornadas duplicadas: revisar Trabajos por ContratoId, Fecha, HoraInicio antes de crear el índice.', 1;
    CREATE UNIQUE INDEX UX_Trabajos_ContratoInicio ON dbo.Trabajos (ContratoId, Fecha, HoraInicio)
    WHERE ContratoId IS NOT NULL;
END
GO
IF COL_LENGTH('dbo.Contratos', 'PausaPorRevocacion') IS NULL
BEGIN
    ALTER TABLE dbo.Contratos ADD PausaPorRevocacion BIT NOT NULL CONSTRAINT DF_Contratos_PausaRevocacion DEFAULT 0;
    -- Conserva las pausas administrativas previas a esta migración.
    EXEC(N'UPDATE c SET PausaPorRevocacion = 1 FROM dbo.Contratos c WHERE c.Estado = 5 AND
        (EXISTS (SELECT 1 FROM dbo.PermisoContratacion p WHERE p.ClienteId = c.ClienteId AND p.Estado = 4)
         OR EXISTS (SELECT 1 FROM dbo.Trabajos t WHERE t.ContratoId = c.Id AND t.MotivoCancelacionTexto = N''Permiso de contratación revocado''))');
END
GO

-- ---------- Utilidades ------------------------------------------------------
-- Convierte "1|08:00|14:00;3|08:00|14:00" en filas.
CREATE OR ALTER FUNCTION dbo.fn_ContratoHorarioTexto (@Horario NVARCHAR(400))
RETURNS TABLE
AS RETURN
    SELECT TRY_CAST(PARSENAME(REPLACE(f.value, '|', '.'), 3) AS TINYINT) AS DiaSemana,
           TRY_CAST(REPLACE(PARSENAME(REPLACE(REPLACE(f.value, ':', '#'), '|', '.'), 2), '#', ':') AS TIME(0)) AS HoraInicio,
           TRY_CAST(REPLACE(PARSENAME(REPLACE(REPLACE(f.value, ':', '#'), '|', '.'), 1), '#', ':') AS TIME(0)) AS HoraFin
    FROM STRING_SPLIT(@Horario, ';') f
    WHERE LTRIM(RTRIM(f.value)) <> '';
GO

-- Horario actual del contrato como texto.
CREATE OR ALTER FUNCTION dbo.fn_ContratoHorarioActual (@ContratoId INT)
RETURNS NVARCHAR(400)
AS
BEGIN
    RETURN (SELECT STRING_AGG(CONCAT(DiaSemana, '|', CONVERT(CHAR(5), HoraInicio, 108), '|', CONVERT(CHAR(5), HoraFin, 108)), ';')
                   WITHIN GROUP (ORDER BY DiaSemana, HoraInicio)
            FROM ContratoHorario WHERE ContratoId = @ContratoId);
END
GO

-- Valida un horario: devuelve NULL si está bien o el código de error.
-- CHOQUE_HORARIO si se cruza con otro contrato vivo (1 a 5) del mismo cuidador en fechas que se solapan.
CREATE OR ALTER PROCEDURE dbo.sp_ContratoValidarHorario
    @CuidadorId INT, @ContratoId INT, @Horario NVARCHAR(400), @FechaInicio DATE, @FechaFin DATE,
    @Error VARCHAR(40) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Error = NULL;
    DECLARE @h TABLE (Id INT IDENTITY, DiaSemana TINYINT, HoraInicio TIME(0), HoraFin TIME(0));
    INSERT INTO @h (DiaSemana, HoraInicio, HoraFin) SELECT DiaSemana, HoraInicio, HoraFin FROM dbo.fn_ContratoHorarioTexto(@Horario);

    IF NOT EXISTS (SELECT 1 FROM @h)
       OR EXISTS (SELECT 1 FROM @h WHERE DiaSemana IS NULL OR DiaSemana > 6 OR HoraInicio IS NULL OR HoraFin IS NULL OR HoraFin <= HoraInicio)
       OR EXISTS (SELECT 1 FROM @h GROUP BY DiaSemana, HoraInicio HAVING COUNT(*) > 1)
       OR EXISTS (SELECT 1 FROM @h a JOIN @h b ON a.DiaSemana = b.DiaSemana AND a.Id < b.Id AND a.HoraInicio < b.HoraFin AND b.HoraInicio < a.HoraFin)
    BEGIN SET @Error = 'HORARIO_INVALIDO'; RETURN; END

    IF EXISTS (
        SELECT 1
        FROM Contratos c WITH (UPDLOCK, HOLDLOCK) JOIN ContratoHorario ch WITH (UPDLOCK, HOLDLOCK) ON ch.ContratoId = c.Id
        JOIN @h h ON h.DiaSemana = ch.DiaSemana AND h.HoraInicio < ch.HoraFin AND ch.HoraInicio < h.HoraFin
        WHERE c.CuidadorId = @CuidadorId AND c.Id <> ISNULL(@ContratoId, 0) AND c.Estado BETWEEN 1 AND 5
          AND c.FechaInicio <= ISNULL(@FechaFin, '9999-12-31') AND ISNULL(c.FechaFin, '9999-12-31') >= @FechaInicio)
        SET @Error = 'CHOQUE_HORARIO';
END
GO

-- Reemplaza el horario de un contrato.
CREATE OR ALTER PROCEDURE dbo.sp_ContratoGuardarHorario @ContratoId INT, @Horario NVARCHAR(400)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM ContratoHorario WHERE ContratoId = @ContratoId;
    INSERT INTO ContratoHorario (ContratoId, DiaSemana, HoraInicio, HoraFin)
    SELECT @ContratoId, DiaSemana, HoraInicio, HoraFin FROM dbo.fn_ContratoHorarioTexto(@Horario);
END
GO

-- Cancela los trabajos generados por el contrato que aún no empezaron, desde @Desde.
CREATE OR ALTER PROCEDURE dbo.sp_ContratoCancelarTrabajosFuturos @ContratoId INT, @Desde DATE, @Hasta DATE = NULL, @Motivo NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Trabajos SET Estado = 5, MotivoCancelacionTexto = @Motivo
    OUTPUT inserted.Id, inserted.ClienteId, inserted.CuidadorId
    WHERE ContratoId = @ContratoId AND Estado IN (1, 2) AND Fecha >= @Desde AND (@Hasta IS NULL OR Fecha <= @Hasta);
END
GO

-- ---------- Permiso para contratar (cliente) --------------------------------
-- 3 resultados: permiso, domicilios (todas las ubicaciones del cliente con su validación), resumen.
CREATE OR ALTER PROCEDURE dbo.sp_ContratoPermisoObtener @ClienteId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Estado, Motivo, Referencia, FechaSolicitud, FechaRevision, ComentarioAdmin,
           CASE WHEN Estado = 3 THEN DATEADD(DAY, 30, FechaRevision) END AS PuedeVolverASolicitar
    FROM PermisoContratacion WHERE ClienteId = @ClienteId;

    SELECT u.Id AS UbicacionId, u.Nombre, u.Direccion, d.Id AS DomicilioId, ISNULL(d.Estado, 0) AS Estado,
           d.QuienVive, d.ComentarioAdmin
    FROM UbicacionesCliente u
    LEFT JOIN DomiciliosContratacion d ON d.UbicacionId = u.Id
    WHERE u.ClienteId = @ClienteId
    ORDER BY u.EsPredeterminada DESC, u.Nombre;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratoPermisoSolicitar
    @ClienteId INT, @Motivo NVARCHAR(600), @CedulaUrl NVARCHAR(400), @Referencia NVARCHAR(300), @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @ClienteId AND RolId = 2 AND IsActive = 1)
    BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    IF LEN(LTRIM(RTRIM(ISNULL(@Motivo, '')))) < 10
    BEGIN SELECT 'DATOS_INCOMPLETOS' AS Resultado; RETURN; END

    DECLARE @Estado TINYINT, @Revision DATETIME;
    SELECT @Estado = Estado, @Revision = FechaRevision FROM PermisoContratacion WHERE ClienteId = @ClienteId;
    IF @Estado IN (1, 2) BEGIN SELECT 'YA_SOLICITADO' AS Resultado; RETURN; END
    IF @Estado IN (3, 4) AND @Revision > DATEADD(DAY, -30, @Ahora)
    BEGIN SELECT 'ESPERA' AS Resultado, DATEADD(DAY, 30, @Revision) AS PuedeVolverASolicitar; RETURN; END

    IF @Estado IS NULL
        INSERT INTO PermisoContratacion (ClienteId, Estado, Motivo, CedulaUrl, Referencia, FechaSolicitud)
        VALUES (@ClienteId, 1, @Motivo, @CedulaUrl, @Referencia, @Ahora);
    ELSE
        UPDATE PermisoContratacion SET Estado = 1, Motivo = @Motivo, CedulaUrl = @CedulaUrl, Referencia = @Referencia,
               FechaSolicitud = @Ahora, FechaRevision = NULL, RevisadoPorAdminId = NULL, ComentarioAdmin = NULL
        WHERE ClienteId = @ClienteId;
    SELECT 'OK' AS Resultado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratoDomicilioSolicitar
    @ClienteId INT, @UbicacionId INT, @ComprobanteUrl NVARCHAR(400), @QuienVive NVARCHAR(300), @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM UbicacionesCliente WHERE Id = @UbicacionId AND ClienteId = @ClienteId)
    BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    IF ISNULL(@ComprobanteUrl, '') = '' OR LEN(LTRIM(RTRIM(ISNULL(@QuienVive, '')))) < 3
    BEGIN SELECT 'DATOS_INCOMPLETOS' AS Resultado; RETURN; END

    DECLARE @Estado TINYINT = (SELECT Estado FROM DomiciliosContratacion WHERE UbicacionId = @UbicacionId);
    IF @Estado = 2 BEGIN SELECT 'YA_APROBADO' AS Resultado; RETURN; END
    IF @Estado = 1 BEGIN SELECT 'YA_SOLICITADO' AS Resultado; RETURN; END

    IF @Estado IS NULL
        INSERT INTO DomiciliosContratacion (ClienteId, UbicacionId, ComprobanteUrl, QuienVive, Estado, FechaSolicitud)
        VALUES (@ClienteId, @UbicacionId, @ComprobanteUrl, @QuienVive, 1, @Ahora);
    ELSE
        UPDATE DomiciliosContratacion SET Estado = 1, ComprobanteUrl = @ComprobanteUrl, QuienVive = @QuienVive,
               FechaSolicitud = @Ahora, FechaRevision = NULL, RevisadoPorAdminId = NULL, ComentarioAdmin = NULL
        WHERE UbicacionId = @UbicacionId;
    SELECT 'OK' AS Resultado;
END
GO

-- ---------- Cuidador: acepta contratos fijos --------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ContratoCuidadorConfig @CuidadorId INT, @Acepta BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Acepta IS NOT NULL
        UPDATE PerfilCuidador SET AceptaContratos = @Acepta WHERE UsuarioId = @CuidadorId;
    SELECT ISNULL(AceptaContratos, 0) AS AceptaContratos, TarifaHora FROM PerfilCuidador WHERE UsuarioId = @CuidadorId;
END
GO

-- Lo que necesita la pantalla "Proponer contrato" (2 resultados: estado, domicilios aprobados).
CREATE OR ALTER PROCEDURE dbo.sp_ContratoPuedeProponer @ClienteId INT, @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL((SELECT Estado FROM PermisoContratacion WHERE ClienteId = @ClienteId), 0) AS PermisoEstado,
           ISNULL(p.AceptaContratos, 0) AS AceptaContratos, p.TarifaHora, p.Especialidad, u.NombreCompleto AS CuidadorNombre
    FROM PerfilCuidador p JOIN Usuarios u ON u.Id = p.UsuarioId
    WHERE p.UsuarioId = @CuidadorId;

    SELECT u.Id AS UbicacionId, u.Nombre, u.Direccion
    FROM UbicacionesCliente u JOIN DomiciliosContratacion d ON d.UbicacionId = u.Id AND d.Estado = 2
    WHERE u.ClienteId = @ClienteId
    ORDER BY u.EsPredeterminada DESC, u.Nombre;
END
GO

-- ---------- Propuesta y negociación -----------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ContratoProponer
    @ClienteId INT, @CuidadorId INT, @UbicacionId INT, @TipoServicio NVARCHAR(100), @TarifaHora DECIMAL(10,2),
    @MetodoPago VARCHAR(20), @FechaInicio DATE, @FechaFin DATE, @Horario NVARCHAR(400), @Nota NVARCHAR(600), @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF NOT EXISTS (SELECT 1 FROM PermisoContratacion WHERE ClienteId = @ClienteId AND Estado = 2)
    BEGIN SELECT 'PERMISO_REQUERIDO' AS Resultado; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM DomiciliosContratacion WHERE UbicacionId = @UbicacionId AND ClienteId = @ClienteId AND Estado = 2)
    BEGIN SELECT 'DOMICILIO_NO_VALIDADO' AS Resultado; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM PerfilCuidador p JOIN Usuarios u ON u.Id = p.UsuarioId
                   WHERE p.UsuarioId = @CuidadorId AND p.AceptaContratos = 1 AND p.EstadoAprobacion = 2 AND u.IsActive = 1)
    BEGIN SELECT 'CUIDADOR_NO_ACEPTA' AS Resultado; RETURN; END
    IF @MetodoPago NOT IN ('efectivo', 'transferencia', 'tarjeta') BEGIN SELECT 'METODO_INVALIDO' AS Resultado; RETURN; END
    IF @TarifaHora <= 0 BEGIN SELECT 'TARIFA_INVALIDA' AS Resultado; RETURN; END
    IF @FechaInicio <= CAST(@Ahora AS DATE) OR (@FechaFin IS NOT NULL AND @FechaFin < DATEADD(DAY, 6, @FechaInicio))
    BEGIN SELECT 'FECHA_INVALIDA' AS Resultado; RETURN; END
    BEGIN TRAN;
    IF EXISTS (SELECT 1 FROM Contratos WITH (UPDLOCK, HOLDLOCK) WHERE ClienteId = @ClienteId AND CuidadorId = @CuidadorId AND UbicacionId = @UbicacionId AND Estado BETWEEN 1 AND 5)
    BEGIN ROLLBACK; SELECT 'DUPLICADO' AS Resultado; RETURN; END

    DECLARE @Error VARCHAR(40);
    EXEC dbo.sp_ContratoValidarHorario @CuidadorId, NULL, @Horario, @FechaInicio, @FechaFin, @Error OUTPUT;
    IF @Error IS NOT NULL BEGIN ROLLBACK; SELECT @Error AS Resultado; RETURN; END

    INSERT INTO Contratos (ClienteId, CuidadorId, UbicacionId, TipoServicio, TarifaHora, MetodoPago, FechaInicio, FechaFin, Nota, Estado, FechaCreacion, FechaUltimaAccion)
    VALUES (@ClienteId, @CuidadorId, @UbicacionId, @TipoServicio, @TarifaHora, @MetodoPago, @FechaInicio, @FechaFin, @Nota, 1, @Ahora, @Ahora);
    DECLARE @Id INT = SCOPE_IDENTITY();
    EXEC dbo.sp_ContratoGuardarHorario @Id, @Horario;
    INSERT INTO ContratoPropuestas (ContratoId, Version, PropuestoPor, TarifaHora, FechaInicio, Horario, Nota, Fecha)
    VALUES (@Id, 1, @ClienteId, @TarifaHora, @FechaInicio, dbo.fn_ContratoHorarioActual(@Id), @Nota, @Ahora);
    COMMIT;

    SELECT 'OK' AS Resultado, @Id AS ContratoId, @CuidadorId AS NotificarA;
END
GO

-- @Accion: aceptar | rechazar | contraproponer | retirar
CREATE OR ALTER PROCEDURE dbo.sp_ContratoResponder
    @ContratoId INT, @UsuarioId INT, @Accion VARCHAR(20), @TarifaHora DECIMAL(10,2) = NULL, @FechaInicio DATE = NULL,
    @Horario NVARCHAR(400) = NULL, @Nota NVARCHAR(600) = NULL, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    DECLARE @Estado TINYINT, @ClienteId INT, @CuidadorId INT, @FechaFin DATE;
    SELECT @Estado = Estado, @ClienteId = ClienteId, @CuidadorId = CuidadorId, @FechaFin = FechaFin FROM Contratos WITH (UPDLOCK, HOLDLOCK) WHERE Id = @ContratoId;
    IF @Estado IS NULL OR @UsuarioId NOT IN (@ClienteId, @CuidadorId) BEGIN ROLLBACK; SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END

    IF EXISTS (SELECT 1 FROM Contratos WHERE Id = @ContratoId AND Estado IN (1, 2) AND FechaUltimaAccion <= DATEADD(HOUR, -72, @Ahora))
    BEGIN ROLLBACK; SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END

    DECLARE @Otro INT = CASE WHEN @UsuarioId = @ClienteId THEN @CuidadorId ELSE @ClienteId END;

    IF @Accion = 'retirar'
    BEGIN
        IF @UsuarioId <> @ClienteId OR @Estado NOT IN (1, 2) BEGIN ROLLBACK; SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
        UPDATE Contratos SET Estado = 9, FechaUltimaAccion = @Ahora WHERE Id = @ContratoId AND Estado = @Estado AND (@Accion = 'retirar' OR (@Estado = 1 AND CuidadorId = @UsuarioId) OR (@Estado = 2 AND ClienteId = @UsuarioId));
        COMMIT;
        SELECT 'OK' AS Resultado, 9 AS Estado, @Otro AS NotificarA; RETURN;
    END

    -- Le toca responder: Estado 1 → cuidador, Estado 2 → cliente.
    IF NOT ((@Estado = 1 AND @UsuarioId = @CuidadorId) OR (@Estado = 2 AND @UsuarioId = @ClienteId))
    BEGIN ROLLBACK; SELECT 'NO_ES_SU_TURNO' AS Resultado; RETURN; END

    IF @Accion = 'aceptar'
    BEGIN
        -- Puede que otro contrato se haya activado mientras tanto.
        DECLARE @H NVARCHAR(400) = dbo.fn_ContratoHorarioActual(@ContratoId), @FI DATE = (SELECT FechaInicio FROM Contratos WHERE Id = @ContratoId), @E1 VARCHAR(40);
        EXEC dbo.sp_ContratoValidarHorario @CuidadorId, @ContratoId, @H, @FI, @FechaFin, @E1 OUTPUT;
        IF @E1 IS NOT NULL BEGIN ROLLBACK; SELECT @E1 AS Resultado; RETURN; END
        IF @FI <= CAST(@Ahora AS DATE) BEGIN ROLLBACK; SELECT 'FECHA_INVALIDA' AS Resultado; RETURN; END
        UPDATE Contratos SET Estado = 3, FechaUltimaAccion = @Ahora WHERE Id = @ContratoId AND Estado = @Estado AND (@Accion = 'retirar' OR (@Estado = 1 AND CuidadorId = @UsuarioId) OR (@Estado = 2 AND ClienteId = @UsuarioId));
        COMMIT;
        SELECT 'OK' AS Resultado, 3 AS Estado, @Otro AS NotificarA; RETURN;
    END

    IF @Accion = 'rechazar'
    BEGIN
        UPDATE Contratos SET Estado = 7, FechaUltimaAccion = @Ahora WHERE Id = @ContratoId AND Estado = @Estado AND (@Accion = 'retirar' OR (@Estado = 1 AND CuidadorId = @UsuarioId) OR (@Estado = 2 AND ClienteId = @UsuarioId));
        COMMIT;
        SELECT 'OK' AS Resultado, 7 AS Estado, @Otro AS NotificarA; RETURN;
    END

    IF @Accion = 'contraproponer'
    BEGIN
        IF ISNULL(@TarifaHora, 0) <= 0 BEGIN ROLLBACK; SELECT 'TARIFA_INVALIDA' AS Resultado; RETURN; END
        IF @FechaInicio IS NULL OR @FechaInicio <= CAST(@Ahora AS DATE) OR (@FechaFin IS NOT NULL AND @FechaFin < DATEADD(DAY, 6, @FechaInicio))
        BEGIN ROLLBACK; SELECT 'FECHA_INVALIDA' AS Resultado; RETURN; END
        DECLARE @E2 VARCHAR(40);
        EXEC dbo.sp_ContratoValidarHorario @CuidadorId, @ContratoId, @Horario, @FechaInicio, @FechaFin, @E2 OUTPUT;
        IF @E2 IS NOT NULL BEGIN ROLLBACK; SELECT @E2 AS Resultado; RETURN; END

        DECLARE @Nuevo TINYINT = CASE WHEN @UsuarioId = @CuidadorId THEN 2 ELSE 1 END;
        UPDATE Contratos SET TarifaHora = @TarifaHora, FechaInicio = @FechaInicio, Nota = @Nota, Estado = @Nuevo, FechaUltimaAccion = @Ahora
        WHERE Id = @ContratoId AND Estado = @Estado AND (@Accion = 'retirar' OR (@Estado = 1 AND CuidadorId = @UsuarioId) OR (@Estado = 2 AND ClienteId = @UsuarioId));
        EXEC dbo.sp_ContratoGuardarHorario @ContratoId, @Horario;
        INSERT INTO ContratoPropuestas (ContratoId, Version, PropuestoPor, TarifaHora, FechaInicio, Horario, Nota, Fecha)
        SELECT @ContratoId, ISNULL(MAX(Version), 0) + 1, @UsuarioId, @TarifaHora, @FechaInicio, dbo.fn_ContratoHorarioActual(@ContratoId), @Nota, @Ahora
        FROM ContratoPropuestas WHERE ContratoId = @ContratoId;
        COMMIT;
        SELECT 'OK' AS Resultado, @Nuevo AS Estado, @Otro AS NotificarA; RETURN;
    END

    ROLLBACK;
    SELECT 'ACCION_INVALIDA' AS Resultado;
END
GO

-- ---------- Consultas -------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ContratosPorUsuario @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.Id, c.Estado, c.TipoServicio, c.TarifaHora, c.MetodoPago, c.FechaInicio, c.FechaFin,
           CASE WHEN c.ClienteId = @UsuarioId THEN 'cliente' ELSE 'cuidador' END AS MiRol,
           o.Id AS OtroId, o.NombreCompleto AS OtroNombre, o.FotoUrl AS OtroFoto,
           ub.Nombre AS DomicilioNombre, ub.Direccion,
           dbo.fn_ContratoHorarioActual(c.Id) AS Horario,
           CAST(CASE WHEN (c.Estado = 1 AND c.CuidadorId = @UsuarioId) OR (c.Estado = 2 AND c.ClienteId = @UsuarioId)
                       OR (c.Estado = 3 AND NOT EXISTS (SELECT 1 FROM ContratoFirmas f WHERE f.ContratoId = c.Id AND f.UsuarioId = @UsuarioId))
                     THEN 1 ELSE 0 END AS BIT) AS RequiereAccion
    FROM Contratos c
    JOIN Usuarios o ON o.Id = CASE WHEN c.ClienteId = @UsuarioId THEN c.CuidadorId ELSE c.ClienteId END
    JOIN UbicacionesCliente ub ON ub.Id = c.UbicacionId
    WHERE c.ClienteId = @UsuarioId OR c.CuidadorId = @UsuarioId
    ORDER BY CASE WHEN c.Estado BETWEEN 1 AND 5 THEN 0 ELSE 1 END, c.FechaUltimaAccion DESC;
END
GO

-- Detalle (7 resultados): contrato, horario, propuestas, ausencias, firmas, documentos, trabajos.
-- @UsuarioId NULL = panel (sin control de pertenencia).
CREATE OR ALTER PROCEDURE dbo.sp_ContratoDetalle @ContratoId INT, @UsuarioId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @UsuarioId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Contratos WHERE Id = @ContratoId AND @UsuarioId IN (ClienteId, CuidadorId))
        SET @ContratoId = -1;

    SELECT c.Id, c.Estado, c.TipoServicio, c.TarifaHora, c.MetodoPago, c.FechaInicio, c.FechaFin, c.DiasPreaviso, c.Nota,
           c.FechaCreacion, c.FechaUltimaAccion, c.FechaActivacion, c.FechaTerminacion, c.TerminadoPor, c.MotivoTerminacion,
           c.ClienteId, cl.NombreCompleto AS ClienteNombre, cl.Email AS ClienteEmail, cl.Telefono AS ClienteTelefono,
           cl.DocumentoIdentidad AS ClienteDocumento, cl.FotoUrl AS ClienteFoto,
           c.CuidadorId, cu.NombreCompleto AS CuidadorNombre, cu.Email AS CuidadorEmail, cu.Telefono AS CuidadorTelefono,
           cu.DocumentoIdentidad AS CuidadorDocumento, cu.FotoUrl AS CuidadorFoto,
           c.UbicacionId, ub.Nombre AS DomicilioNombre, ub.Direccion, ub.Latitud, ub.Longitud, d.QuienVive,
           CASE WHEN @UsuarioId IS NULL THEN NULL WHEN @UsuarioId = c.ClienteId THEN 'cliente' ELSE 'cuidador' END AS MiRol
    FROM Contratos c
    JOIN Usuarios cl ON cl.Id = c.ClienteId
    JOIN Usuarios cu ON cu.Id = c.CuidadorId
    JOIN UbicacionesCliente ub ON ub.Id = c.UbicacionId
    LEFT JOIN DomiciliosContratacion d ON d.UbicacionId = c.UbicacionId
    WHERE c.Id = @ContratoId;

    SELECT DiaSemana, HoraInicio, HoraFin FROM ContratoHorario WHERE ContratoId = @ContratoId ORDER BY CASE DiaSemana WHEN 0 THEN 7 ELSE DiaSemana END, HoraInicio;

    SELECT p.Version, p.PropuestoPor, u.NombreCompleto AS PropuestoPorNombre, p.TarifaHora, p.FechaInicio, p.Horario, p.Nota, p.Fecha
    FROM ContratoPropuestas p JOIN Usuarios u ON u.Id = p.PropuestoPor
    WHERE p.ContratoId = @ContratoId ORDER BY p.Version;

    SELECT a.Id, a.Fecha, a.MarcadoPor, u.NombreCompleto AS MarcadoPorNombre, a.Motivo
    FROM ContratoAusencias a JOIN Usuarios u ON u.Id = a.MarcadoPor
    WHERE a.ContratoId = @ContratoId ORDER BY a.Fecha DESC;

    SELECT f.UsuarioId, u.NombreCompleto, f.FirmaSvg, f.Fecha, f.Ip, f.Latitud, f.Longitud, f.Correo
    FROM ContratoFirmas f JOIN Usuarios u ON u.Id = f.UsuarioId
    WHERE f.ContratoId = @ContratoId ORDER BY f.Fecha;

    SELECT Id, Version, Tipo, Hash, Fecha FROM ContratoDocumentos WHERE ContratoId = @ContratoId ORDER BY Version DESC;

    SELECT TOP 60 Id, Fecha, HoraInicio, HoraFin, Estado, Tarifa FROM Trabajos
    WHERE ContratoId = @ContratoId ORDER BY Fecha DESC, HoraInicio DESC;
END
GO

-- ---------- Firma -----------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ContratoCodigoGenerar @ContratoId INT, @UsuarioId INT, @CodigoHash VARCHAR(64), @Expira DATETIME, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    DECLARE @Bloqueo INT;
    SELECT @Bloqueo = Id FROM Contratos WITH (UPDLOCK, HOLDLOCK) WHERE Id = @ContratoId;
    IF NOT EXISTS (SELECT 1 FROM Contratos WHERE Id = @ContratoId AND @UsuarioId IN (ClienteId, CuidadorId))
    BEGIN COMMIT; SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM Contratos WHERE Id = @ContratoId AND Estado = 3)
    BEGIN COMMIT; SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
    IF EXISTS (SELECT 1 FROM ContratoFirmas WHERE ContratoId = @ContratoId AND UsuarioId = @UsuarioId)
    BEGIN COMMIT; SELECT 'YA_FIRMADO' AS Resultado; RETURN; END
    IF EXISTS (SELECT 1 FROM ContratoCodigos WHERE ContratoId = @ContratoId AND UsuarioId = @UsuarioId AND BloqueadoHasta > @Ahora)
       OR (SELECT COUNT(*) FROM ContratoCodigoFallos WHERE ContratoId = @ContratoId AND UsuarioId = @UsuarioId AND Fecha > DATEADD(HOUR, -1, @Ahora)) >= 15
    BEGIN COMMIT; SELECT 'FIRMA_BLOQUEADA' AS Resultado; RETURN; END
    IF (SELECT COUNT(*) FROM ContratoCodigos WHERE ContratoId = @ContratoId AND UsuarioId = @UsuarioId AND ISNULL(FechaCreacion, DATEADD(MINUTE, -10, Expira)) > DATEADD(HOUR, -1, @Ahora)) >= 3
    BEGIN COMMIT; SELECT 'LIMITE_CODIGOS' AS Resultado; RETURN; END
    UPDATE ContratoCodigos SET Usado = 1 WHERE ContratoId = @ContratoId AND UsuarioId = @UsuarioId AND Usado = 0;
    INSERT INTO ContratoCodigos (ContratoId, UsuarioId, CodigoHash, Expira, FechaCreacion) VALUES (@ContratoId, @UsuarioId, @CodigoHash, @Expira, @Ahora);
    COMMIT;
    SELECT 'OK' AS Resultado, Email, NombreCompleto FROM Usuarios WHERE Id = @UsuarioId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratoFirmar
    @ContratoId INT, @UsuarioId INT, @CodigoHash VARCHAR(64), @FirmaSvg NVARCHAR(MAX), @Ip VARCHAR(64),
    @Latitud DECIMAL(9,6), @Longitud DECIMAL(9,6), @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    DECLARE @Bloqueo INT;
    SELECT @Bloqueo = Id FROM Contratos WITH (UPDLOCK, HOLDLOCK) WHERE Id = @ContratoId;
    IF NOT EXISTS (SELECT 1 FROM Contratos WHERE Id = @ContratoId AND Estado = 3 AND @UsuarioId IN (ClienteId, CuidadorId))
    BEGIN COMMIT; SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
    IF EXISTS (SELECT 1 FROM ContratoFirmas WHERE ContratoId = @ContratoId AND UsuarioId = @UsuarioId)
    BEGIN COMMIT; SELECT 'YA_FIRMADO' AS Resultado; RETURN; END
    IF EXISTS (SELECT 1 FROM ContratoCodigos WHERE ContratoId = @ContratoId AND UsuarioId = @UsuarioId AND BloqueadoHasta > @Ahora)
       OR (SELECT COUNT(*) FROM ContratoCodigoFallos WHERE ContratoId = @ContratoId AND UsuarioId = @UsuarioId AND Fecha > DATEADD(HOUR, -1, @Ahora)) >= 15
    BEGIN COMMIT; SELECT 'FIRMA_BLOQUEADA' AS Resultado; RETURN; END
    DECLARE @CodId INT, @Hash VARCHAR(64), @Expira DATETIME, @Intentos INT;
    SELECT TOP 1 @CodId = Id, @Hash = CodigoHash, @Expira = Expira, @Intentos = Intentos
    FROM ContratoCodigos WHERE ContratoId = @ContratoId AND UsuarioId = @UsuarioId AND Usado = 0 ORDER BY Id DESC;
    IF @CodId IS NULL OR @Expira <= @Ahora OR @Intentos >= 5
    BEGIN COMMIT; SELECT 'CODIGO_VENCIDO' AS Resultado; RETURN; END
    IF @Hash <> @CodigoHash
    BEGIN
        UPDATE ContratoCodigos SET Intentos = Intentos + 1 WHERE Id = @CodId;
        INSERT INTO ContratoCodigoFallos (ContratoId, UsuarioId, Fecha) VALUES (@ContratoId, @UsuarioId, @Ahora);
        DECLARE @Fallos INT = (SELECT COUNT(*) FROM ContratoCodigoFallos WHERE ContratoId = @ContratoId AND UsuarioId = @UsuarioId AND Fecha > DATEADD(HOUR, -1, @Ahora));
        IF @Fallos >= 15 UPDATE ContratoCodigos SET BloqueadoHasta = DATEADD(HOUR, 1, @Ahora) WHERE Id = @CodId;
        COMMIT;
        SELECT CASE WHEN @Fallos >= 15 THEN 'FIRMA_BLOQUEADA' ELSE 'CODIGO_INCORRECTO' END AS Resultado; RETURN;
    END
    IF LEN(ISNULL(@FirmaSvg, '')) < 20 BEGIN COMMIT; SELECT 'FIRMA_VACIA' AS Resultado; RETURN; END
    UPDATE ContratoCodigos SET Usado = 1 WHERE Id = @CodId;
    INSERT INTO ContratoFirmas (ContratoId, UsuarioId, FirmaSvg, Fecha, Ip, Latitud, Longitud, Correo)
    SELECT @ContratoId, @UsuarioId, @FirmaSvg, @Ahora, @Ip, @Latitud, @Longitud, Email FROM Usuarios WHERE Id = @UsuarioId;
    DECLARE @Completas BIT = CASE WHEN (SELECT COUNT(*) FROM ContratoFirmas WHERE ContratoId = @ContratoId) >= 2 THEN 1 ELSE 0 END;
    COMMIT;
    SELECT 'OK' AS Resultado, @Completas AS FirmasCompletas,
           (SELECT CASE WHEN ClienteId = @UsuarioId THEN CuidadorId ELSE ClienteId END FROM Contratos WHERE Id = @ContratoId) AS NotificarA;
END
GO

-- Guarda el PDF firmado y activa el contrato.
CREATE OR ALTER PROCEDURE dbo.sp_ContratoActivar @ContratoId INT, @Ruta NVARCHAR(400), @Hash VARCHAR(64), @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    IF NOT EXISTS (SELECT 1 FROM Contratos c WITH (UPDLOCK, HOLDLOCK) WHERE Id = @ContratoId AND Estado = 3
        AND EXISTS (SELECT 1 FROM ContratoFirmas WHERE ContratoId = c.Id AND UsuarioId = c.ClienteId)
        AND EXISTS (SELECT 1 FROM ContratoFirmas WHERE ContratoId = c.Id AND UsuarioId = c.CuidadorId))
    BEGIN ROLLBACK; SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
    INSERT INTO ContratoDocumentos (ContratoId, Version, Tipo, Ruta, Hash, Fecha)
    SELECT @ContratoId, ISNULL(MAX(Version), 0) + 1, N'Contrato original', @Ruta, @Hash, @Ahora FROM ContratoDocumentos WHERE ContratoId = @ContratoId;
    UPDATE Contratos SET FechaInicio = CASE WHEN FechaInicio < CAST(@Ahora AS DATE) THEN CAST(@Ahora AS DATE) ELSE FechaInicio END, Estado = 4, FechaActivacion = @Ahora, FechaUltimaAccion = @Ahora WHERE Id = @ContratoId;
    COMMIT;
    SELECT 'OK' AS Resultado, ClienteId, CuidadorId FROM Contratos WHERE Id = @ContratoId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratoDocumento @DocumentoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT d.Id, d.ContratoId, d.Version, d.Ruta, d.Hash, d.Fecha, c.ClienteId, c.CuidadorId
    FROM ContratoDocumentos d JOIN Contratos c ON c.Id = d.ContratoId WHERE d.Id = @DocumentoId;
END
GO

-- ---------- Contrato activo -------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ContratoAusencia @ContratoId INT, @UsuarioId INT, @Fecha DATE, @Motivo NVARCHAR(300), @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ClienteId INT, @CuidadorId INT, @Estado TINYINT;
    SELECT @ClienteId = ClienteId, @CuidadorId = CuidadorId, @Estado = Estado FROM Contratos WHERE Id = @ContratoId;
    IF @Estado IS NULL OR @UsuarioId NOT IN (@ClienteId, @CuidadorId) BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    IF @Estado NOT IN (4, 5) BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
    IF @Fecha < CAST(@Ahora AS DATE) BEGIN SELECT 'FECHA_INVALIDA' AS Resultado; RETURN; END
    IF EXISTS (SELECT 1 FROM ContratoAusencias WHERE ContratoId = @ContratoId AND Fecha = @Fecha) BEGIN SELECT 'YA_MARCADA' AS Resultado; RETURN; END
    -- Un servicio que ya empezó ese día no se puede cancelar desde aquí.
    IF EXISTS (SELECT 1 FROM Trabajos WHERE ContratoId = @ContratoId AND Fecha = @Fecha AND Estado IN (3, 7))
    BEGIN SELECT 'EN_CURSO' AS Resultado; RETURN; END

    INSERT INTO ContratoAusencias (ContratoId, Fecha, MarcadoPor, Motivo, FechaRegistro) VALUES (@ContratoId, @Fecha, @UsuarioId, @Motivo, @Ahora);
    UPDATE Trabajos SET Estado = 5, MotivoCancelacionTexto = N'Ausencia en contrato fijo'
    WHERE ContratoId = @ContratoId AND Fecha = @Fecha AND Estado IN (1, 2);
    SELECT 'OK' AS Resultado, CASE WHEN @UsuarioId = @ClienteId THEN @CuidadorId ELSE @ClienteId END AS NotificarA;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratoQuitarAusencia @AusenciaId INT, @UsuarioId INT, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ContratoId INT, @Fecha DATE, @MarcadoPor INT;
    SELECT @ContratoId = ContratoId, @Fecha = Fecha, @MarcadoPor = MarcadoPor FROM ContratoAusencias WHERE Id = @AusenciaId;
    IF @ContratoId IS NULL OR @MarcadoPor <> @UsuarioId BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    IF @Fecha <= CAST(@Ahora AS DATE) BEGIN SELECT 'FECHA_INVALIDA' AS Resultado; RETURN; END
    DELETE FROM ContratoAusencias WHERE Id = @AusenciaId;
    -- Se vuelve a generar el trabajo de ese día en la próxima pasada (sp_ContratosGenerarTrabajos).
    SELECT 'OK' AS Resultado, (SELECT CASE WHEN ClienteId = @UsuarioId THEN CuidadorId ELSE ClienteId END FROM Contratos WHERE Id = @ContratoId) AS NotificarA;
END
GO

-- @Accion: pausar | reanudar | terminar
CREATE OR ALTER PROCEDURE dbo.sp_ContratoCambiarEstado @ContratoId INT, @UsuarioId INT, @Accion VARCHAR(20), @Motivo NVARCHAR(600), @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ClienteId INT, @CuidadorId INT, @Estado TINYINT, @Preaviso INT, @FechaFin DATE, @Hoy DATE = CAST(@Ahora AS DATE);
    SELECT @ClienteId = ClienteId, @CuidadorId = CuidadorId, @Estado = Estado, @Preaviso = DiasPreaviso, @FechaFin = FechaFin
    FROM Contratos WHERE Id = @ContratoId;
    IF @Estado IS NULL OR @UsuarioId NOT IN (@ClienteId, @CuidadorId) BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    DECLARE @Otro INT = CASE WHEN @UsuarioId = @ClienteId THEN @CuidadorId ELSE @ClienteId END;
    DECLARE @Cancelados TABLE (TrabajoId INT, ClienteId INT, CuidadorId INT);

    IF @Accion = 'pausar'
    BEGIN
        IF @Estado <> 4 BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
        UPDATE Contratos SET Estado = 5, FechaUltimaAccion = @Ahora WHERE Id = @ContratoId;
        DECLARE @Manana DATE = DATEADD(DAY, 1, @Hoy);
        INSERT INTO @Cancelados EXEC dbo.sp_ContratoCancelarTrabajosFuturos @ContratoId, @Manana, NULL, N'Contrato fijo en pausa';
        SELECT 'OK' AS Resultado, 5 AS Estado, @Otro AS NotificarA, NULL AS FechaFin; RETURN;
    END

    IF @Accion = 'reanudar'
    BEGIN
        IF @Estado <> 5 BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
        UPDATE c SET Estado = 4, FechaUltimaAccion = @Ahora
        FROM Contratos c WITH (UPDLOCK, HOLDLOCK)
        WHERE c.Id = @ContratoId AND c.Estado = 5 AND c.PausaPorRevocacion = 0 AND c.FechaTerminacion IS NULL
          AND EXISTS (SELECT 1 FROM PermisoContratacion p WITH (UPDLOCK, HOLDLOCK) WHERE p.ClienteId = c.ClienteId AND p.Estado = 2);
        IF @@ROWCOUNT = 0 BEGIN SELECT 'PERMISO_REQUERIDO' AS Resultado; RETURN; END
        SELECT 'OK' AS Resultado, 4 AS Estado, @Otro AS NotificarA, NULL AS FechaFin; RETURN;
    END

    IF @Accion = 'terminar'
    BEGIN
        IF @Estado NOT IN (4, 5) BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
        IF LEN(LTRIM(RTRIM(ISNULL(@Motivo, '')))) < 5 BEGIN SELECT 'DATOS_INCOMPLETOS' AS Resultado; RETURN; END
        -- El preaviso aplica también en pausa, sin generar nuevas jornadas.
        DECLARE @Fin DATE = DATEADD(DAY, @Preaviso, @Hoy);
        IF @FechaFin IS NOT NULL AND @FechaFin < @Fin SET @Fin = @FechaFin;
        UPDATE Contratos SET FechaFin = @Fin, FechaTerminacion = @Ahora, TerminadoPor = @UsuarioId, MotivoTerminacion = @Motivo,
               Estado = Estado, FechaUltimaAccion = @Ahora
        WHERE Id = @ContratoId;
        DECLARE @Despues DATE = DATEADD(DAY, 1, @Fin);
        INSERT INTO @Cancelados EXEC dbo.sp_ContratoCancelarTrabajosFuturos @ContratoId, @Despues, NULL, N'Contrato fijo terminado';
        SELECT 'OK' AS Resultado, @Estado AS Estado, @Otro AS NotificarA, @Fin AS FechaFin; RETURN;
    END

    SELECT 'ACCION_INVALIDA' AS Resultado;
END
GO

-- Pasada periódica (cada hora): vence propuestas, cierra contratos que llegaron a su fecha fin y
-- genera los trabajos de los próximos @Dias días. Devuelve los trabajos creados.
CREATE OR ALTER PROCEDURE dbo.sp_ContratosGenerarTrabajos @Ahora DATETIME, @Dias INT = 7, @ContratoId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(@Ahora AS DATE);

    UPDATE Contratos SET Estado = 8, FechaUltimaAccion = @Ahora
    WHERE Estado IN (1, 2) AND FechaUltimaAccion < DATEADD(HOUR, -72, @Ahora);

    UPDATE Contratos SET Estado = 6, FechaUltimaAccion = @Ahora,
           FechaTerminacion = ISNULL(FechaTerminacion, @Ahora)
    WHERE Estado IN (4, 5) AND FechaFin < @Hoy;

    SET XACT_ABORT ON;
    DECLARE @Generados TABLE (TrabajoId INT, ClienteId INT, CuidadorId INT);
    BEGIN TRAN;
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource = 'ContratosGenerarTrabajos', @LockMode = 'Exclusive', @LockOwner = 'Transaction';
    IF @LockResult < 0 BEGIN ROLLBACK; THROW 50001, 'No se pudo bloquear la generación de jornadas.', 1; END;
    UPDATE t SET Estado = 2, MotivoCancelacionTexto = NULL
    OUTPUT inserted.Id, inserted.ClienteId, inserted.CuidadorId INTO @Generados
    FROM Trabajos t JOIN Contratos c WITH (HOLDLOCK) ON c.Id = t.ContratoId
    WHERE c.Estado = 4 AND (@ContratoId IS NULL OR c.Id = @ContratoId)
      AND t.Fecha >= @Hoy AND t.Fecha < DATEADD(DAY, @Dias, @Hoy)
      AND (t.Fecha > @Hoy OR t.HoraInicio > CAST(@Ahora AS TIME))
      AND (c.FechaFin IS NULL OR t.Fecha <= c.FechaFin)
      AND t.Estado = 5 AND t.MotivoCancelacionTexto IN (N'Contrato fijo en pausa', N'Ausencia en contrato fijo', N'Permiso de contratación revocado')
      AND NOT EXISTS (SELECT 1 FROM ContratoAusencias a WHERE a.ContratoId = c.Id AND a.Fecha = t.Fecha);

    BEGIN TRY
    ;WITH Dias AS (
        SELECT TOP (@Dias) DATEADD(DAY, ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1, @Hoy) AS Fecha
        FROM sys.all_objects
    ),
    Candidatos AS (
        SELECT c.Id AS ContratoId, c.ClienteId, c.CuidadorId, c.TipoServicio, d.Fecha, h.HoraInicio, h.HoraFin,
               ub.Direccion, ub.Latitud, ub.Longitud, c.MetodoPago,
               CAST(c.TarifaHora * DATEDIFF(MINUTE, h.HoraInicio, h.HoraFin) / 60.0 AS DECIMAL(10,2)) AS Tarifa
        FROM Contratos c
        JOIN ContratoHorario h ON h.ContratoId = c.Id
        JOIN Dias d ON (DATEPART(WEEKDAY, d.Fecha) + @@DATEFIRST - 1) % 7 = h.DiaSemana
        JOIN UbicacionesCliente ub ON ub.Id = c.UbicacionId
        WHERE c.Estado = 4 AND (@ContratoId IS NULL OR c.Id = @ContratoId)
          AND d.Fecha >= c.FechaInicio AND (c.FechaFin IS NULL OR d.Fecha <= c.FechaFin)
          -- hoy solo si todavía no empezó la jornada
          AND (d.Fecha > @Hoy OR CAST(h.HoraInicio AS TIME) > CAST(@Ahora AS TIME))
          AND NOT EXISTS (SELECT 1 FROM ContratoAusencias a WHERE a.ContratoId = c.Id AND a.Fecha = d.Fecha)
          -- Las jornadas existentes se restauran arriba si las canceló el sistema.
          AND NOT EXISTS (SELECT 1 FROM Trabajos t WITH (UPDLOCK, HOLDLOCK) WHERE t.ContratoId = c.Id AND t.Fecha = d.Fecha AND t.HoraInicio = h.HoraInicio)
    )
    INSERT INTO Trabajos (ClienteId, CuidadorId, TipoServicio, Fecha, HoraInicio, HoraFin, Direccion, Estado, Tarifa, Latitud, Longitud,
                          PinInicio, PinFin, FechaCreacion, MetodoPago, ContratoId)
    OUTPUT inserted.Id, inserted.ClienteId, inserted.CuidadorId INTO @Generados
    SELECT ClienteId, CuidadorId, TipoServicio, Fecha, HoraInicio, HoraFin, Direccion, 2, Tarifa, Latitud, Longitud,
           RIGHT('0000' + CAST(ABS(CHECKSUM(NEWID())) % 9000 + 1000 AS VARCHAR(4)), 4),
           RIGHT('0000' + CAST(ABS(CHECKSUM(NEWID())) % 9000 + 1000 AS VARCHAR(4)), 4),
           @Ahora, MetodoPago, ContratoId
    FROM Candidatos;
    COMMIT;
    SELECT TrabajoId, ClienteId, CuidadorId FROM @Generados;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK;
        IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
        SELECT Id AS TrabajoId, ClienteId, CuidadorId FROM Trabajos WHERE 1 = 0;
    END CATCH
END
GO

-- ---------- Panel -----------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_AdminContratoSolicitudes @Estado TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.ClienteId, u.NombreCompleto, u.Email, u.Telefono, u.DocumentoIdentidad, u.FotoUrl, u.FechaCreacion AS ClienteDesde,
           p.Estado, p.Motivo, p.CedulaUrl, p.Referencia, p.FechaSolicitud, p.FechaRevision, p.ComentarioAdmin,
           ra.NombreCompleto AS RevisadoPor,
           (SELECT COUNT(*) FROM Trabajos t WHERE t.ClienteId = p.ClienteId AND t.Estado = 4) AS ServiciosCompletados,
           (SELECT COUNT(*) FROM Trabajos t WHERE t.ClienteId = p.ClienteId AND t.Estado = 5) AS ServiciosCancelados,
           (SELECT CAST(AVG(CAST(ca.Puntuacion AS DECIMAL(4,2))) AS DECIMAL(4,2)) FROM Calificaciones ca WHERE ca.CalificadoId = p.ClienteId) AS CalificacionPromedio,
           (SELECT COUNT(*) FROM SancionesUsuario s WHERE s.UsuarioId = p.ClienteId) AS Sanciones,
           (SELECT COUNT(*) FROM DomiciliosContratacion d WHERE d.ClienteId = p.ClienteId AND d.Estado = 2) AS DomiciliosAprobados,
           (SELECT COUNT(*) FROM Contratos c WHERE c.ClienteId = p.ClienteId AND c.Estado IN (4, 5)) AS ContratosActivos
    FROM PermisoContratacion p
    JOIN Usuarios u ON u.Id = p.ClienteId
    LEFT JOIN Usuarios ra ON ra.Id = p.RevisadoPorAdminId
    WHERE @Estado IS NULL OR p.Estado = @Estado
    ORDER BY CASE WHEN p.Estado = 1 THEN 0 ELSE 1 END, p.FechaSolicitud DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminContratoDomicilios @Estado TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT d.Id, d.ClienteId, u.NombreCompleto AS ClienteNombre, ISNULL(p.Estado, 0) AS PermisoEstado,
           ub.Nombre AS DomicilioNombre, ub.Direccion, ub.Latitud, ub.Longitud,
           d.ComprobanteUrl, d.QuienVive, d.Estado, d.FechaSolicitud, d.FechaRevision, d.ComentarioAdmin,
           ra.NombreCompleto AS RevisadoPor
    FROM DomiciliosContratacion d
    JOIN Usuarios u ON u.Id = d.ClienteId
    JOIN UbicacionesCliente ub ON ub.Id = d.UbicacionId
    LEFT JOIN PermisoContratacion p ON p.ClienteId = d.ClienteId
    LEFT JOIN Usuarios ra ON ra.Id = d.RevisadoPorAdminId
    WHERE @Estado IS NULL OR d.Estado = @Estado
    ORDER BY CASE WHEN d.Estado = 1 THEN 0 ELSE 1 END, d.FechaSolicitud DESC;
END
GO

-- @Accion: aprobar | rechazar | revocar. Revocar pausa los contratos activos del cliente.
CREATE OR ALTER PROCEDURE dbo.sp_AdminContratoRevisarPermiso @ClienteId INT, @Accion VARCHAR(20), @Comentario NVARCHAR(600), @AdminId INT, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Estado TINYINT = (SELECT Estado FROM PermisoContratacion WHERE ClienteId = @ClienteId);
    IF @Estado IS NULL BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    DECLARE @Nuevo TINYINT = CASE @Accion WHEN 'aprobar' THEN 2 WHEN 'rechazar' THEN 3 WHEN 'revocar' THEN 4 END;
    IF @Nuevo IS NULL OR (@Nuevo IN (2, 3) AND @Estado <> 1) OR (@Nuevo = 4 AND @Estado <> 2)
    BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
    IF @Nuevo IN (3, 4) AND LEN(LTRIM(RTRIM(ISNULL(@Comentario, '')))) < 5
    BEGIN SELECT 'COMENTARIO_REQUERIDO' AS Resultado; RETURN; END

    UPDATE PermisoContratacion SET Estado = @Nuevo, FechaRevision = @Ahora, RevisadoPorAdminId = @AdminId, ComentarioAdmin = @Comentario
    WHERE ClienteId = @ClienteId;

    IF @Nuevo = 4
    BEGIN
        DECLARE @Cancelados TABLE (TrabajoId INT, ClienteId INT, CuidadorId INT);
        DECLARE @c INT, @Manana DATE = DATEADD(DAY, 1, CAST(@Ahora AS DATE));
        DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT Id FROM Contratos WHERE ClienteId = @ClienteId AND Estado IN (4, 5);
        OPEN cur; FETCH NEXT FROM cur INTO @c;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            UPDATE Contratos SET Estado = 5, PausaPorRevocacion = 1, FechaUltimaAccion = @Ahora WHERE Id = @c;
            INSERT INTO @Cancelados EXEC dbo.sp_ContratoCancelarTrabajosFuturos @c, @Manana, NULL, N'Permiso de contratación revocado';
            FETCH NEXT FROM cur INTO @c;
        END
        CLOSE cur; DEALLOCATE cur;
        -- Las propuestas en curso se retiran.
        UPDATE Contratos SET Estado = 9, FechaUltimaAccion = @Ahora WHERE ClienteId = @ClienteId AND Estado IN (1, 2, 3);
    END

    SELECT 'OK' AS Resultado, @Nuevo AS Estado,
           (SELECT STRING_AGG(CAST(CuidadorId AS VARCHAR(12)), ',') FROM (SELECT DISTINCT CuidadorId FROM Contratos WHERE ClienteId = @ClienteId AND Estado = 5 AND FechaUltimaAccion = @Ahora) x) AS CuidadoresAfectados;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminContratoRevisarDomicilio @DomicilioId INT, @Accion VARCHAR(20), @Comentario NVARCHAR(600), @AdminId INT, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Estado TINYINT, @ClienteId INT;
    SELECT @Estado = Estado, @ClienteId = ClienteId FROM DomiciliosContratacion WHERE Id = @DomicilioId;
    IF @Estado IS NULL BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    DECLARE @Nuevo TINYINT = CASE @Accion WHEN 'aprobar' THEN 2 WHEN 'rechazar' THEN 3 END;
    IF @Nuevo IS NULL OR @Estado <> 1 BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
    IF @Nuevo = 3 AND LEN(LTRIM(RTRIM(ISNULL(@Comentario, '')))) < 5 BEGIN SELECT 'COMENTARIO_REQUERIDO' AS Resultado; RETURN; END
    UPDATE DomiciliosContratacion SET Estado = @Nuevo, FechaRevision = @Ahora, RevisadoPorAdminId = @AdminId, ComentarioAdmin = @Comentario
    WHERE Id = @DomicilioId;
    SELECT 'OK' AS Resultado, @ClienteId AS ClienteId, @Nuevo AS Estado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminContratos @Estado TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.Id, c.Estado, c.TipoServicio, c.TarifaHora, c.MetodoPago, c.FechaInicio, c.FechaFin, c.FechaCreacion, c.FechaActivacion,
           c.ClienteId, cl.NombreCompleto AS ClienteNombre, c.CuidadorId, cu.NombreCompleto AS CuidadorNombre,
           ub.Nombre AS DomicilioNombre, ub.Direccion, dbo.fn_ContratoHorarioActual(c.Id) AS Horario,
           (SELECT COUNT(*) FROM Trabajos t WHERE t.ContratoId = c.Id AND t.Estado = 4) AS JornadasCompletadas,
           (SELECT COUNT(*) FROM ContratoDocumentos d WHERE d.ContratoId = c.Id) AS Documentos
    FROM Contratos c
    JOIN Usuarios cl ON cl.Id = c.ClienteId
    JOIN Usuarios cu ON cu.Id = c.CuidadorId
    JOIN UbicacionesCliente ub ON ub.Id = c.UbicacionId
    WHERE @Estado IS NULL OR c.Estado = @Estado
    ORDER BY c.FechaUltimaAccion DESC;
END
GO

-- Contratos con las dos firmas que no llegaron a activarse (falló el PDF o la activación).
CREATE OR ALTER PROCEDURE dbo.sp_ContratosPendientesActivar
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.Id
    FROM Contratos c
    WHERE c.Estado = 3
      AND (SELECT COUNT(DISTINCT f.UsuarioId) FROM ContratoFirmas f
           WHERE f.ContratoId = c.Id AND f.UsuarioId IN (c.ClienteId, c.CuidadorId)) >= 2;
END
GO

-- Ajusta la fecha antes de leer los datos usados para el PDF.
CREATE OR ALTER PROCEDURE dbo.sp_ContratoPrepararActivacion @ContratoId INT, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE c SET FechaInicio = CAST(@Ahora AS DATE)
    FROM Contratos c
    WHERE c.Id = @ContratoId AND c.Estado = 3 AND c.FechaInicio < CAST(@Ahora AS DATE)
      AND EXISTS (SELECT 1 FROM ContratoFirmas WHERE ContratoId = c.Id AND UsuarioId = c.ClienteId)
      AND EXISTS (SELECT 1 FROM ContratoFirmas WHERE ContratoId = c.Id AND UsuarioId = c.CuidadorId);
END
GO
