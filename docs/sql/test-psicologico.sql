-- Test psicológico del registro de Care Partners (Filtro #1)
--
-- El panel crea los tests (preguntas, opciones, tiempo y umbrales) y los vincula a uno o varios
-- tipos de servicio (TiposTrabajos). Al registrarse, si el tipo de trabajo elegido tiene un test
-- activo, la app lo pide antes de seguir con los documentos.
--
-- Resultado de cada intento (TestIntentos.Resultado):
--   0 Abandonado (se inició y no se terminó)
--   1 Aprobado            -> sigue el registro
--   2 Revisión manual     -> sigue el registro; el equipo lo ve en Verificación
--   3 Bloqueado           -> no puede registrarse hasta BloqueadoHasta
--   4 Descartado          -> eligió una opción red flag: no puede volver a registrarse
-- Reglas (en sp_TestFinalizarIntento):
--   red flag -> Descartado; falló una pregunta crítica -> Bloqueado;
--   % >= PorcentajeAprobacion -> Aprobado; % >= PorcentajeRevision -> Revisión; si no -> Bloqueado.
-- Los bloqueos se aplican por documento de identidad o teléfono. Anular un intento desde el panel
-- quita su bloqueo.
--
-- Las respuestas se guardan como copia del texto (TestRespuestas): editar un test no cambia los
-- intentos ya hechos.
--
-- Idempotente. Ejecutar con sqlcmd -f 65001 (UTF-8) para no dañar acentos ni la ñ.

-- ===================== Tablas =====================

IF OBJECT_ID('dbo.TestsPsicologicos') IS NULL
CREATE TABLE dbo.TestsPsicologicos (
    Id                    INT IDENTITY PRIMARY KEY,
    Nombre                NVARCHAR(150) NOT NULL,
    Descripcion           NVARCHAR(500) NULL,
    MinutosLimite         INT NOT NULL,
    PorcentajeAprobacion  DECIMAL(5,2) NOT NULL,
    PorcentajeRevision    DECIMAL(5,2) NOT NULL,
    DiasBloqueo           INT NOT NULL,
    Activo                BIT NOT NULL CONSTRAINT DF_TestsPsicologicos_Activo DEFAULT 1,
    FechaCreacion         DATETIME2 NOT NULL,
    FechaModificacion     DATETIME2 NULL
);
GO

-- Un tipo de servicio solo puede tener un test.
IF OBJECT_ID('dbo.TestsPsicologicosTipos') IS NULL
CREATE TABLE dbo.TestsPsicologicosTipos (
    TestId        INT NOT NULL REFERENCES dbo.TestsPsicologicos(Id),
    TipoTrabajoId INT NOT NULL,            -- TiposTrabajos.Id (esa tabla no tiene PK declarada)
    CONSTRAINT PK_TestsPsicologicosTipos PRIMARY KEY (TestId, TipoTrabajoId),
    CONSTRAINT UQ_TestsPsicologicosTipos_Tipo UNIQUE (TipoTrabajoId)
);
GO

IF OBJECT_ID('dbo.TestPreguntas') IS NULL
CREATE TABLE dbo.TestPreguntas (
    Id        INT IDENTITY PRIMARY KEY,
    TestId    INT NOT NULL REFERENCES dbo.TestsPsicologicos(Id),
    Orden     INT NOT NULL,
    Texto     NVARCHAR(1000) NOT NULL,
    EsCritica BIT NOT NULL
);
GO

IF OBJECT_ID('dbo.TestOpciones') IS NULL
CREATE TABLE dbo.TestOpciones (
    Id         INT IDENTITY PRIMARY KEY,
    PreguntaId INT NOT NULL REFERENCES dbo.TestPreguntas(Id) ON DELETE CASCADE,
    Orden      INT NOT NULL,
    Texto      NVARCHAR(500) NOT NULL,
    EsCorrecta BIT NOT NULL,
    EsRedFlag  BIT NOT NULL
);
GO

IF OBJECT_ID('dbo.TestIntentos') IS NULL
CREATE TABLE dbo.TestIntentos (
    Id              INT IDENTITY PRIMARY KEY,
    Token           UNIQUEIDENTIFIER NOT NULL CONSTRAINT UQ_TestIntentos_Token UNIQUE,
    TestId          INT NOT NULL REFERENCES dbo.TestsPsicologicos(Id),
    TipoTrabajoId   INT NOT NULL,
    UsuarioId       INT NULL,              -- se llena al terminar el registro
    Nombre          NVARCHAR(200) NULL,
    Email           NVARCHAR(200) NULL,
    Documento       NVARCHAR(30) NULL,     -- solo letras y dígitos, en mayúsculas
    Telefono        NVARCHAR(30) NULL,     -- solo dígitos
    FechaInicio     DATETIME2 NOT NULL,
    FechaFin        DATETIME2 NULL,
    SegundosUsados  INT NULL,
    TiempoAgotado   BIT NOT NULL CONSTRAINT DF_TestIntentos_Tiempo DEFAULT 0,
    TotalPreguntas  INT NULL,
    Correctas       INT NULL,
    Porcentaje      DECIMAL(5,2) NULL,
    FalloCritica    BIT NOT NULL CONSTRAINT DF_TestIntentos_Critica DEFAULT 0,
    EligioRedFlag   BIT NOT NULL CONSTRAINT DF_TestIntentos_RedFlag DEFAULT 0,
    Resultado       TINYINT NULL,          -- NULL = en curso
    BloqueadoHasta  DATETIME2 NULL,
    Anulado         BIT NOT NULL CONSTRAINT DF_TestIntentos_Anulado DEFAULT 0,
    AnuladoPor      INT NULL,
    FechaAnulado    DATETIME2 NULL
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TestIntentos_Documento')
    CREATE INDEX IX_TestIntentos_Documento ON dbo.TestIntentos (Documento) INCLUDE (Resultado, BloqueadoHasta, Anulado);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TestIntentos_Telefono')
    CREATE INDEX IX_TestIntentos_Telefono ON dbo.TestIntentos (Telefono) INCLUDE (Resultado, BloqueadoHasta, Anulado);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TestIntentos_Usuario')
    CREATE INDEX IX_TestIntentos_Usuario ON dbo.TestIntentos (UsuarioId);
GO

IF OBJECT_ID('dbo.TestRespuestas') IS NULL
CREATE TABLE dbo.TestRespuestas (
    Id             INT IDENTITY PRIMARY KEY,
    IntentoId      INT NOT NULL REFERENCES dbo.TestIntentos(Id),
    Orden          INT NOT NULL,
    PreguntaTexto  NVARCHAR(1000) NOT NULL,
    EsCritica      BIT NOT NULL,
    OpcionTexto    NVARCHAR(500) NULL,     -- NULL = no respondió
    CorrectaTexto  NVARCHAR(500) NULL,
    EsCorrecta     BIT NOT NULL,
    EsRedFlag      BIT NOT NULL
);
GO

-- ===================== App =====================

-- Estado del candidato antes de hacer el test del tipo de trabajo elegido.
-- Estado: SIN_TEST | LIBRE | APROBADO (ya lo pasó y no ha terminado el registro) | BLOQUEADO | DESCARTADO
CREATE OR ALTER PROCEDURE sp_TestEstadoCandidato
    @TipoTrabajoId INT,
    @Documento     NVARCHAR(30) = NULL,
    @Telefono      NVARCHAR(30) = NULL,
    @Ahora         DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TestId INT, @Minutos INT, @Nombre NVARCHAR(150), @Preguntas INT;
    SELECT @TestId = t.Id, @Minutos = t.MinutosLimite, @Nombre = t.Nombre,
           @Preguntas = (SELECT COUNT(*) FROM TestPreguntas p WHERE p.TestId = t.Id)
    FROM TestsPsicologicos t
    JOIN TestsPsicologicosTipos tt ON tt.TestId = t.Id
    WHERE tt.TipoTrabajoId = @TipoTrabajoId AND t.Activo = 1;

    IF @TestId IS NULL OR @Preguntas = 0
    BEGIN
        SELECT 'SIN_TEST' AS Estado, NULL AS TestId, NULL AS Token, NULL AS MinutosLimite, NULL AS TotalPreguntas, NULL AS BloqueadoHasta;
        RETURN;
    END;

    -- Intentos válidos de la misma persona (por documento o teléfono), en cualquier test.
    ;WITH Mios AS (
        SELECT * FROM TestIntentos
        WHERE Anulado = 0 AND Resultado IN (1, 2, 3, 4)
          AND ((@Documento IS NOT NULL AND Documento = @Documento) OR (@Telefono IS NOT NULL AND Telefono = @Telefono))
    )
    SELECT TOP 1 * INTO #Marca FROM (
        SELECT 1 AS Prio, 'DESCARTADO' AS Estado, NULL AS Token, NULL AS BloqueadoHasta, FechaFin FROM Mios WHERE Resultado = 4
        UNION ALL
        SELECT 2, 'BLOQUEADO', NULL, BloqueadoHasta, FechaFin FROM Mios WHERE Resultado = 3 AND BloqueadoHasta > @Ahora
        UNION ALL
        -- Ya lo pasó (p. ej. la app se cerró antes de terminar el registro): no lo repite.
        SELECT 3, 'APROBADO', Token, NULL, FechaFin FROM Mios
        WHERE Resultado IN (1, 2) AND TestId = @TestId AND UsuarioId IS NULL AND FechaFin > DATEADD(DAY, -7, @Ahora)
    ) x
    ORDER BY Prio, FechaFin DESC;

    IF EXISTS (SELECT 1 FROM #Marca)
        SELECT Estado, @TestId AS TestId, Token, @Minutos AS MinutosLimite, @Preguntas AS TotalPreguntas, BloqueadoHasta FROM #Marca;
    ELSE
        SELECT 'LIBRE' AS Estado, @TestId AS TestId, NULL AS Token, @Minutos AS MinutosLimite, @Preguntas AS TotalPreguntas, NULL AS BloqueadoHasta;
END;
GO

-- Abre un intento y devuelve las preguntas (sin decir cuál es la correcta).
-- La API comprueba antes con sp_TestEstadoCandidato que el candidato esté LIBRE.
CREATE OR ALTER PROCEDURE sp_TestIniciarIntento
    @TestId        INT,
    @TipoTrabajoId INT,
    @Nombre        NVARCHAR(200) = NULL,
    @Email         NVARCHAR(200) = NULL,
    @Documento     NVARCHAR(30) = NULL,
    @Telefono      NVARCHAR(30) = NULL,
    @Ahora         DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    -- Un intento que quedó a medias de la misma persona se cierra como abandonado.
    UPDATE TestIntentos SET Resultado = 0, FechaFin = @Ahora
    WHERE Resultado IS NULL
      AND ((@Documento IS NOT NULL AND Documento = @Documento) OR (@Telefono IS NOT NULL AND Telefono = @Telefono));

    DECLARE @Token UNIQUEIDENTIFIER = NEWID();
    INSERT INTO TestIntentos (Token, TestId, TipoTrabajoId, Nombre, Email, Documento, Telefono, FechaInicio)
    VALUES (@Token, @TestId, @TipoTrabajoId, @Nombre, @Email, @Documento, @Telefono, @Ahora);

    SELECT @Token AS Token, t.Nombre, t.Descripcion, t.MinutosLimite
    FROM TestsPsicologicos t WHERE t.Id = @TestId;

    SELECT Id, Orden, Texto FROM TestPreguntas WHERE TestId = @TestId ORDER BY Orden, Id;

    SELECT o.Id, o.PreguntaId, o.Texto
    FROM TestOpciones o JOIN TestPreguntas p ON p.Id = o.PreguntaId
    WHERE p.TestId = @TestId
    ORDER BY o.PreguntaId, o.Orden, o.Id;
END;
GO

-- Califica el intento. @Respuestas = JSON [{"preguntaId":1,"opcionId":5}, ...]
CREATE OR ALTER PROCEDURE sp_TestFinalizarIntento
    @Token      UNIQUEIDENTIFIER,
    @Respuestas NVARCHAR(MAX) = NULL,
    @Ahora      DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Id INT, @TestId INT, @Inicio DATETIME2, @Resultado TINYINT;
    SELECT @Id = Id, @TestId = TestId, @Inicio = FechaInicio, @Resultado = Resultado FROM TestIntentos WHERE Token = @Token;

    IF @Id IS NULL BEGIN SELECT 'NO_ENCONTRADO' AS Codigo; RETURN; END;
    IF @Resultado IS NOT NULL BEGIN SELECT 'YA_TERMINADO' AS Codigo; RETURN; END;

    DECLARE @Minutos INT, @PctAprob DECIMAL(5,2), @PctRev DECIMAL(5,2), @Dias INT;
    SELECT @Minutos = MinutosLimite, @PctAprob = PorcentajeAprobacion, @PctRev = PorcentajeRevision, @Dias = DiasBloqueo
    FROM TestsPsicologicos WHERE Id = @TestId;

    DECLARE @Segundos INT = DATEDIFF(SECOND, @Inicio, @Ahora);
    -- 30 s de gracia por la latencia de la red.
    DECLARE @Agotado BIT = CASE WHEN @Segundos > @Minutos * 60 + 30 THEN 1 ELSE 0 END;

    -- Respuesta elegida por pregunta (solo opciones que de verdad son de esa pregunta).
    SELECT p.Id AS PreguntaId, p.Orden, p.Texto, p.EsCritica, o.Id AS OpcionId, o.Texto AS OpcionTexto,
           ISNULL(o.EsCorrecta, 0) AS EsCorrecta, ISNULL(o.EsRedFlag, 0) AS EsRedFlag,
           (SELECT TOP 1 c.Texto FROM TestOpciones c WHERE c.PreguntaId = p.Id AND c.EsCorrecta = 1) AS CorrectaTexto
    INTO #Resp
    FROM TestPreguntas p
    OUTER APPLY (
        SELECT TOP 1 op.* FROM OPENJSON(ISNULL(@Respuestas, '[]')) WITH (preguntaId INT, opcionId INT) j
        JOIN TestOpciones op ON op.Id = j.opcionId AND op.PreguntaId = p.Id
        WHERE j.preguntaId = p.Id
    ) o
    WHERE p.TestId = @TestId;

    DECLARE @Total INT = (SELECT COUNT(*) FROM #Resp);
    DECLARE @Correctas INT = (SELECT COUNT(*) FROM #Resp WHERE EsCorrecta = 1);
    DECLARE @Pct DECIMAL(5,2) = CASE WHEN @Total = 0 THEN 0 ELSE CAST(@Correctas * 100.0 / @Total AS DECIMAL(5,2)) END;
    DECLARE @FalloCritica BIT = CASE WHEN EXISTS (SELECT 1 FROM #Resp WHERE EsCritica = 1 AND EsCorrecta = 0) THEN 1 ELSE 0 END;
    DECLARE @RedFlag BIT = CASE WHEN EXISTS (SELECT 1 FROM #Resp WHERE EsRedFlag = 1) THEN 1 ELSE 0 END;

    DECLARE @Final TINYINT =
        CASE WHEN @RedFlag = 1 THEN 4
             WHEN @FalloCritica = 1 THEN 3
             WHEN @Pct >= @PctAprob THEN 1
             WHEN @Pct >= @PctRev THEN 2
             ELSE 3 END;

    BEGIN TRAN;
        UPDATE TestIntentos
        SET FechaFin = @Ahora, SegundosUsados = @Segundos, TiempoAgotado = @Agotado, TotalPreguntas = @Total,
            Correctas = @Correctas, Porcentaje = @Pct, FalloCritica = @FalloCritica, EligioRedFlag = @RedFlag,
            Resultado = @Final, BloqueadoHasta = CASE WHEN @Final = 3 THEN DATEADD(DAY, @Dias, @Ahora) END
        WHERE Id = @Id;

        INSERT INTO TestRespuestas (IntentoId, Orden, PreguntaTexto, EsCritica, OpcionTexto, CorrectaTexto, EsCorrecta, EsRedFlag)
        SELECT @Id, ROW_NUMBER() OVER (ORDER BY Orden, PreguntaId), Texto, EsCritica, OpcionTexto, CorrectaTexto, EsCorrecta, EsRedFlag
        FROM #Resp;
    COMMIT;

    SELECT 'OK' AS Codigo, @Final AS Resultado, @Pct AS Porcentaje, @Correctas AS Correctas, @Total AS TotalPreguntas,
           CASE WHEN @Final = 3 THEN DATEADD(DAY, @Dias, @Ahora) END AS BloqueadoHasta;
END;
GO

-- Antes de crear la cuenta: si el tipo de trabajo tiene test activo, exige un intento aprobado
-- (o en revisión) de la misma persona. Codigo: OK | TEST_REQUERIDO
CREATE OR ALTER PROCEDURE sp_TestValidarRegistro
    @Especialidad NVARCHAR(100),
    @Token        UNIQUEIDENTIFIER = NULL,
    @Documento    NVARCHAR(30) = NULL,
    @Telefono     NVARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TestId INT;
    SELECT @TestId = t.Id
    FROM TestsPsicologicos t
    JOIN TestsPsicologicosTipos tt ON tt.TestId = t.Id
    JOIN TiposTrabajos tp ON tp.Id = tt.TipoTrabajoId
    WHERE tp.Nombre = @Especialidad AND t.Activo = 1
      AND EXISTS (SELECT 1 FROM TestPreguntas p WHERE p.TestId = t.Id);

    IF @TestId IS NULL BEGIN SELECT 'OK' AS Codigo; RETURN; END;

    IF EXISTS (
        SELECT 1 FROM TestIntentos
        WHERE Token = @Token AND TestId = @TestId AND Resultado IN (1, 2) AND Anulado = 0 AND UsuarioId IS NULL
          AND ((@Documento IS NOT NULL AND Documento = @Documento) OR (@Telefono IS NOT NULL AND Telefono = @Telefono))
    )
        SELECT 'OK' AS Codigo;
    ELSE
        SELECT 'TEST_REQUERIDO' AS Codigo;
END;
GO

CREATE OR ALTER PROCEDURE sp_TestVincularIntento
    @Token     UNIQUEIDENTIFIER,
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE TestIntentos SET UsuarioId = @UsuarioId WHERE Token = @Token AND UsuarioId IS NULL;
END;
GO

-- ===================== Panel =====================

CREATE OR ALTER PROCEDURE sp_AdminTests
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.Id, t.Nombre, t.Descripcion, t.MinutosLimite, t.PorcentajeAprobacion, t.PorcentajeRevision, t.DiasBloqueo, t.Activo,
           t.FechaCreacion, t.FechaModificacion,
           (SELECT COUNT(*) FROM TestPreguntas p WHERE p.TestId = t.Id) AS Preguntas,
           (SELECT COUNT(*) FROM TestPreguntas p WHERE p.TestId = t.Id AND p.EsCritica = 1) AS Criticas,
           (SELECT COUNT(*) FROM TestIntentos i WHERE i.TestId = t.Id AND i.Resultado IS NOT NULL AND i.Resultado <> 0) AS Intentos
    FROM TestsPsicologicos t
    ORDER BY t.Activo DESC, t.Nombre;

    SELECT tt.TestId, tp.Id AS TipoTrabajoId, tp.Nombre
    FROM TestsPsicologicosTipos tt JOIN TiposTrabajos tp ON tp.Id = tt.TipoTrabajoId
    ORDER BY tp.Nombre;
END;
GO

CREATE OR ALTER PROCEDURE sp_AdminTestDetalle
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Nombre, Descripcion, MinutosLimite, PorcentajeAprobacion, PorcentajeRevision, DiasBloqueo, Activo
    FROM TestsPsicologicos WHERE Id = @Id;

    SELECT TipoTrabajoId FROM TestsPsicologicosTipos WHERE TestId = @Id;

    SELECT Id, Orden, Texto, EsCritica FROM TestPreguntas WHERE TestId = @Id ORDER BY Orden, Id;

    SELECT o.Id, o.PreguntaId, o.Orden, o.Texto, o.EsCorrecta, o.EsRedFlag
    FROM TestOpciones o JOIN TestPreguntas p ON p.Id = o.PreguntaId
    WHERE p.TestId = @Id ORDER BY o.PreguntaId, o.Orden, o.Id;
END;
GO

-- Guarda el test completo. @Tipos = JSON [1,2]; @Preguntas = JSON
-- [{"texto":"...","esCritica":true,"opciones":[{"texto":"...","esCorrecta":true,"esRedFlag":false}]}]
-- Las preguntas se reemplazan enteras (los intentos guardan su propia copia del texto).
-- Resultado: OK (+ Id) | NO_ENCONTRADO | TIPO_OCUPADO (+ Detalle)
CREATE OR ALTER PROCEDURE sp_AdminGuardarTest
    @Id                   INT = NULL,
    @Nombre               NVARCHAR(150),
    @Descripcion          NVARCHAR(500) = NULL,
    @MinutosLimite        INT,
    @PorcentajeAprobacion DECIMAL(5,2),
    @PorcentajeRevision   DECIMAL(5,2),
    @DiasBloqueo          INT,
    @Activo               BIT,
    @Tipos                NVARCHAR(MAX),
    @Preguntas            NVARCHAR(MAX),
    @Ahora                DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TestsPsicologicos WHERE Id = @Id)
    BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END;

    DECLARE @Ocupado NVARCHAR(400) = (
        SELECT TOP 1 tp.Nombre + N' (ya está en «' + t.Nombre + N'»)'
        FROM OPENJSON(@Tipos) j
        JOIN TestsPsicologicosTipos tt ON tt.TipoTrabajoId = CAST(j.value AS INT)
        JOIN TestsPsicologicos t ON t.Id = tt.TestId
        JOIN TiposTrabajos tp ON tp.Id = tt.TipoTrabajoId
        WHERE tt.TestId <> ISNULL(@Id, 0));
    IF @Ocupado IS NOT NULL BEGIN SELECT 'TIPO_OCUPADO' AS Resultado, @Ocupado AS Detalle; RETURN; END;

    BEGIN TRAN;
        IF @Id IS NULL
        BEGIN
            INSERT INTO TestsPsicologicos (Nombre, Descripcion, MinutosLimite, PorcentajeAprobacion, PorcentajeRevision, DiasBloqueo, Activo, FechaCreacion)
            VALUES (@Nombre, @Descripcion, @MinutosLimite, @PorcentajeAprobacion, @PorcentajeRevision, @DiasBloqueo, @Activo, @Ahora);
            SET @Id = SCOPE_IDENTITY();
        END
        ELSE
            UPDATE TestsPsicologicos
            SET Nombre = @Nombre, Descripcion = @Descripcion, MinutosLimite = @MinutosLimite, PorcentajeAprobacion = @PorcentajeAprobacion,
                PorcentajeRevision = @PorcentajeRevision, DiasBloqueo = @DiasBloqueo, Activo = @Activo, FechaModificacion = @Ahora
            WHERE Id = @Id;

        DELETE FROM TestsPsicologicosTipos WHERE TestId = @Id;
        INSERT INTO TestsPsicologicosTipos (TestId, TipoTrabajoId)
        SELECT DISTINCT @Id, CAST(value AS INT) FROM OPENJSON(@Tipos);

        DELETE FROM TestPreguntas WHERE TestId = @Id;   -- las opciones se borran en cascada

        DECLARE @Mapa TABLE (Orden INT, PreguntaId INT);
        MERGE TestPreguntas AS destino
        USING (
            SELECT CAST([key] AS INT) + 1 AS Orden, JSON_VALUE(value, '$.texto') AS Texto,
                   ISNULL(CAST(JSON_VALUE(value, '$.esCritica') AS BIT), 0) AS EsCritica
            FROM OPENJSON(@Preguntas)
        ) AS origen ON 1 = 0
        WHEN NOT MATCHED THEN
            INSERT (TestId, Orden, Texto, EsCritica) VALUES (@Id, origen.Orden, origen.Texto, origen.EsCritica)
        OUTPUT origen.Orden, inserted.Id INTO @Mapa;

        INSERT INTO TestOpciones (PreguntaId, Orden, Texto, EsCorrecta, EsRedFlag)
        SELECT m.PreguntaId, CAST(o.[key] AS INT) + 1, JSON_VALUE(o.value, '$.texto'),
               ISNULL(CAST(JSON_VALUE(o.value, '$.esCorrecta') AS BIT), 0),
               ISNULL(CAST(JSON_VALUE(o.value, '$.esRedFlag') AS BIT), 0)
        FROM OPENJSON(@Preguntas) p
        JOIN @Mapa m ON m.Orden = CAST(p.[key] AS INT) + 1
        CROSS APPLY OPENJSON(p.value, '$.opciones') o;
    COMMIT;

    SELECT 'OK' AS Resultado, @Id AS Id;
END;
GO

-- Intentos terminados. @Resultado NULL = todos; @Buscar por nombre, correo, documento o teléfono.
CREATE OR ALTER PROCEDURE sp_AdminTestIntentos
    @Resultado TINYINT = NULL,
    @Buscar    NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 500 i.Id, i.TestId, t.Nombre AS Test, tp.Nombre AS TipoTrabajo, i.UsuarioId,
           ISNULL(u.NombreCompleto, i.Nombre) AS Nombre, ISNULL(u.Email, i.Email) AS Email, i.Documento, i.Telefono,
           i.FechaInicio, i.FechaFin, i.SegundosUsados, i.TiempoAgotado, i.TotalPreguntas, i.Correctas, i.Porcentaje,
           i.FalloCritica, i.EligioRedFlag, i.Resultado, i.BloqueadoHasta, i.Anulado
    FROM TestIntentos i
    JOIN TestsPsicologicos t ON t.Id = i.TestId
    LEFT JOIN TiposTrabajos tp ON tp.Id = i.TipoTrabajoId
    LEFT JOIN Usuarios u ON u.Id = i.UsuarioId
    WHERE i.Resultado IS NOT NULL AND i.Resultado <> 0
      AND (@Resultado IS NULL OR i.Resultado = @Resultado)
      AND (@Buscar IS NULL OR ISNULL(u.NombreCompleto, i.Nombre) LIKE '%' + @Buscar + '%' OR ISNULL(u.Email, i.Email) LIKE '%' + @Buscar + '%'
           OR i.Documento LIKE '%' + @Buscar + '%' OR i.Telefono LIKE '%' + @Buscar + '%')
    ORDER BY i.FechaFin DESC;
END;
GO

CREATE OR ALTER PROCEDURE sp_AdminTestIntentoDetalle
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.Id, i.TestId, t.Nombre AS Test, tp.Nombre AS TipoTrabajo, i.UsuarioId,
           ISNULL(u.NombreCompleto, i.Nombre) AS Nombre, ISNULL(u.Email, i.Email) AS Email, i.Documento, i.Telefono,
           i.FechaInicio, i.FechaFin, i.SegundosUsados, i.TiempoAgotado, i.TotalPreguntas, i.Correctas, i.Porcentaje,
           i.FalloCritica, i.EligioRedFlag, i.Resultado, i.BloqueadoHasta, i.Anulado, i.FechaAnulado,
           a.NombreCompleto AS AnuladoPorNombre
    FROM TestIntentos i
    JOIN TestsPsicologicos t ON t.Id = i.TestId
    LEFT JOIN TiposTrabajos tp ON tp.Id = i.TipoTrabajoId
    LEFT JOIN Usuarios u ON u.Id = i.UsuarioId
    LEFT JOIN Usuarios a ON a.Id = i.AnuladoPor
    WHERE i.Id = @Id;

    SELECT Orden, PreguntaTexto, EsCritica, OpcionTexto, CorrectaTexto, EsCorrecta, EsRedFlag
    FROM TestRespuestas WHERE IntentoId = @Id ORDER BY Orden;
END;
GO

-- Anula un intento: deja de contar para bloqueos y descartes (la persona puede volver a intentarlo).
CREATE OR ALTER PROCEDURE sp_AdminAnularTestIntento
    @Id      INT,
    @AdminId INT,
    @Ahora   DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM TestIntentos WHERE Id = @Id AND Anulado = 0)
    BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END;
    UPDATE TestIntentos SET Anulado = 1, AnuladoPor = @AdminId, FechaAnulado = @Ahora WHERE Id = @Id;
    SELECT 'OK' AS Resultado;
END;
GO

-- Último resultado de test de cada usuario (para la cola de Verificación).
CREATE OR ALTER PROCEDURE sp_AdminTestResultadosUsuarios
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UsuarioId, Id AS IntentoId, Resultado, Porcentaje
    FROM (
        SELECT *, ROW_NUMBER() OVER (PARTITION BY UsuarioId ORDER BY FechaFin DESC) AS n
        FROM TestIntentos
        WHERE UsuarioId IS NOT NULL AND Resultado IN (1, 2, 3, 4)
    ) x
    WHERE n = 1;
END;
GO
