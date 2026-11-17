-- Piloto: migración aditiva. Ejecutar únicamente en la base DEV autorizada.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF COL_LENGTH('dbo.PerfilCuidador','EstatusMigratorioId') IS NULL
 ALTER TABLE dbo.PerfilCuidador ADD EstatusMigratorioId int NULL REFERENCES dbo.EstatusMigratorio(Id);
IF COL_LENGTH('dbo.Contratos','Dormida') IS NULL
 ALTER TABLE dbo.Contratos ADD Dormida bit NOT NULL CONSTRAINT DF_Contratos_Dormida DEFAULT 0;
IF COL_LENGTH('dbo.ContratoPropuestas','Dormida') IS NULL
 ALTER TABLE dbo.ContratoPropuestas ADD Dormida bit NOT NULL CONSTRAINT DF_ContratoPropuestas_Dormida DEFAULT 0;
IF COL_LENGTH('dbo.ContratoPropuestas','Tareas') IS NULL
 ALTER TABLE dbo.ContratoPropuestas ADD Tareas nvarchar(max) NULL;
IF OBJECT_ID('dbo.ContratoTareas','U') IS NULL
BEGIN
 CREATE TABLE dbo.ContratoTareas(Id int IDENTITY PRIMARY KEY,ContratoId int NOT NULL REFERENCES dbo.Contratos(Id),
 Descripcion nvarchar(200) NOT NULL,Orden int NOT NULL,RequiereFoto bit NOT NULL);
 CREATE INDEX IX_ContratoTareas_Contrato ON dbo.ContratoTareas(ContratoId,Orden);
END;
IF COL_LENGTH('dbo.TareasTrabajo','RequiereFoto') IS NULL
 ALTER TABLE dbo.TareasTrabajo ADD RequiereFoto bit NOT NULL CONSTRAINT DF_TareasTrabajo_RequiereFoto DEFAULT 0;
IF COL_LENGTH('dbo.TareasTrabajo','FotoUrl') IS NULL
 ALTER TABLE dbo.TareasTrabajo ADD FotoUrl nvarchar(500) NULL;
GO
-- Estatus migratorios del documento previo al piloto (reunión 10-oct-2026) con su documento obligatorio.
-- Se agregan por nombre aunque el catálogo ya tenga otros (no se tocan ni se duplican los existentes).
BEGIN TRAN;
DECLARE @Estatus TABLE (Nombre nvarchar(100), Documento nvarchar(100));
INSERT @Estatus VALUES
 (N'Residencia temporal', N'Carnet de residencia'),
 (N'Residencia permanente', N'Carnet de residencia'),
 (N'Plan Nacional de Regularización (PNRE)', N'Carnet PNRE'),
 (N'Visa de trabajo', N'Visa de trabajo');
INSERT dbo.EstatusMigratorio(Nombre, Activo)
SELECT e.Nombre, 1 FROM @Estatus e WHERE NOT EXISTS (SELECT 1 FROM dbo.EstatusMigratorio m WHERE m.Nombre = e.Nombre);
INSERT dbo.RequisitosDocumentoPorEstatus(EstatusMigratorioId, TipoDocumento)
SELECT m.Id, e.Documento FROM @Estatus e JOIN dbo.EstatusMigratorio m ON m.Nombre = e.Nombre
WHERE NOT EXISTS (SELECT 1 FROM dbo.RequisitosDocumentoPorEstatus r WHERE r.EstatusMigratorioId = m.Id AND r.TipoDocumento = e.Documento);
COMMIT;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoMigratorioRegistro @NacionalidadId int=NULL,@EstatusId int=NULL
AS
BEGIN
 SET NOCOUNT ON;
 SELECT CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.Nacionalidades WHERE Id=@NacionalidadId AND CodigoIso<>'DO') THEN 1 ELSE 0 END AS bit) EsExtranjero,
 CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.EstatusMigratorio WHERE Id=@EstatusId AND Activo=1) THEN 1 ELSE 0 END AS bit) EstatusActivo;
 SELECT TipoDocumento FROM dbo.RequisitosDocumentoPorEstatus WHERE EstatusMigratorioId=@EstatusId ORDER BY Id;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoEstatusPublicos
AS
BEGIN
 SET NOCOUNT ON;
 SELECT Id,Nombre FROM dbo.EstatusMigratorio WHERE Activo=1 ORDER BY Id;
 SELECT r.EstatusMigratorioId,r.TipoDocumento FROM dbo.RequisitosDocumentoPorEstatus r
 JOIN dbo.EstatusMigratorio e ON e.Id=r.EstatusMigratorioId WHERE e.Activo=1 ORDER BY r.Id;
END;
GO
CREATE OR ALTER FUNCTION dbo.fn_PilotoMigratorioFaltantes(@UsuarioId int,@Hoy date)
RETURNS TABLE AS RETURN
 SELECT r.TipoDocumento FROM (
 SELECT CAST(N'Pasaporte' AS nvarchar(100)) TipoDocumento
 UNION SELECT rd.TipoDocumento FROM dbo.PerfilCuidador p JOIN dbo.RequisitosDocumentoPorEstatus rd ON rd.EstatusMigratorioId=p.EstatusMigratorioId WHERE p.UsuarioId=@UsuarioId
 UNION SELECT N'Estatus migratorio' WHERE NOT EXISTS(SELECT 1 FROM dbo.PerfilCuidador p JOIN dbo.EstatusMigratorio e ON e.Id=p.EstatusMigratorioId AND e.Activo=1 WHERE p.UsuarioId=@UsuarioId)
 ) r WHERE EXISTS(SELECT 1 FROM dbo.Usuarios u JOIN dbo.Nacionalidades n ON n.Id=u.NacionalidadId WHERE u.Id=@UsuarioId AND n.CodigoIso<>'DO')
 AND (r.TipoDocumento=N'Estatus migratorio' OR NOT EXISTS(SELECT 1 FROM dbo.DocumentosCuidador d WHERE d.CuidadorId=@UsuarioId
 AND d.TipoDocumento COLLATE Latin1_General_100_CI_AS=r.TipoDocumento COLLATE Latin1_General_100_CI_AS
 AND d.Estado=2 AND NULLIF(LTRIM(RTRIM(d.UrlArchivo)),N'') IS NOT NULL AND (d.FechaVencimiento IS NULL OR d.FechaVencimiento>=@Hoy)));
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoMigratorio @UsuarioId int,@Hoy date
AS
BEGIN
 SET NOCOUNT ON;
 SELECT CAST(CASE WHEN n.CodigoIso<>'DO' THEN 1 ELSE 0 END AS bit) EsExtranjero,n.Nombre Nacionalidad,p.EstatusMigratorioId EstatusId,e.Nombre Estatus,u.Email
 FROM dbo.PerfilCuidador p JOIN dbo.Usuarios u ON u.Id=p.UsuarioId LEFT JOIN dbo.Nacionalidades n ON n.Id=u.NacionalidadId
 LEFT JOIN dbo.EstatusMigratorio e ON e.Id=p.EstatusMigratorioId WHERE p.UsuarioId=@UsuarioId;
 ;WITH Tipos AS(SELECT CAST(N'Pasaporte' AS nvarchar(100)) Tipo UNION SELECT r.TipoDocumento FROM dbo.PerfilCuidador p
 JOIN dbo.RequisitosDocumentoPorEstatus r ON r.EstatusMigratorioId=p.EstatusMigratorioId WHERE p.UsuarioId=@UsuarioId)
 SELECT t.Tipo,d.Estado,d.FechaVencimiento Vencimiento,d.UrlArchivo Url,CAST(CASE WHEN NULLIF(LTRIM(RTRIM(d.UrlArchivo)),N'') IS NULL THEN 0 ELSE 1 END AS bit) Presente
 FROM Tipos t OUTER APPLY(SELECT TOP 1 * FROM dbo.DocumentosCuidador d WHERE d.CuidadorId=@UsuarioId
 AND d.TipoDocumento COLLATE Latin1_General_100_CI_AS=t.Tipo COLLATE Latin1_General_100_CI_AS
 ORDER BY CASE WHEN d.Estado=2 AND (d.FechaVencimiento IS NULL OR d.FechaVencimiento>=@Hoy) AND NULLIF(LTRIM(RTRIM(d.UrlArchivo)),N'') IS NOT NULL THEN 0 ELSE 1 END,d.Id DESC) d;
 SELECT TipoDocumento FROM dbo.fn_PilotoMigratorioFaltantes(@UsuarioId,@Hoy);
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoPedirMigratorio @UsuarioId int,@Mensaje nvarchar(1000)=NULL
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @Observacion nvarchar(1000)=COALESCE(NULLIF(@Mensaje,N''),N'Sube los documentos migratorios requeridos.');
 EXEC dbo.sp_ActualizarEstadoCuidador @CuidadorId=@UsuarioId,@NuevoEstado=1,@Observaciones=@Observacion;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoAprobarCuidador @CuidadorId int,@NuevoEstado int,@Observaciones nvarchar(1000)=NULL,@Hoy date
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
 BEGIN TRAN;
 IF @NuevoEstado=2 AND EXISTS(SELECT 1 FROM dbo.fn_PilotoMigratorioFaltantes(@CuidadorId,@Hoy))
 BEGIN
 SELECT 'DOCUMENTOS_MIGRATORIOS_FALTANTES' Resultado;
 SELECT TipoDocumento FROM dbo.fn_PilotoMigratorioFaltantes(@CuidadorId,@Hoy);
 COMMIT; RETURN;
 END;
 DECLARE @Resultado TABLE(Filas int);
 INSERT @Resultado EXEC dbo.sp_ActualizarEstadoCuidador @CuidadorId=@CuidadorId,@NuevoEstado=@NuevoEstado,@Observaciones=@Observaciones;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM @Resultado WHERE Filas>0) THEN 'OK' ELSE 'NO_ENCONTRADO' END Resultado;
 COMMIT;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoGuardarTareas @ContratoId int,@Tareas nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON;
 DELETE dbo.ContratoTareas WHERE ContratoId=@ContratoId;
 INSERT dbo.ContratoTareas(ContratoId,Descripcion,Orden,RequiereFoto)
 SELECT @ContratoId,LTRIM(RTRIM(JSON_VALUE(value,'$.Descripcion'))),CONVERT(int,[key]),COALESCE(TRY_CONVERT(bit,JSON_VALUE(value,'$.RequiereFoto')),0)
 FROM OPENJSON(@Tareas);
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoEditarTareas @ContratoId int,@UsuarioId int,@Tareas nvarchar(max)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRAN;
 IF NOT EXISTS(SELECT 1 FROM dbo.Contratos WITH(UPDLOCK,HOLDLOCK) WHERE Id=@ContratoId AND ClienteId=@UsuarioId AND Estado=4)
 BEGIN ROLLBACK; SELECT 'ESTADO_INVALIDO' Resultado; RETURN; END;
 EXEC dbo.sp_PilotoGuardarTareas @ContratoId,@Tareas;
 COMMIT; SELECT 'OK' Resultado;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoRelevos @ContratoId int,@UsuarioId int
AS
BEGIN
 SET NOCOUNT ON;
 SELECT r.Id TrabajoId,r.Fecha,r.CuidadorId,u.NombreCompleto Nombre,u.FotoUrl,r.Estado,
 (SELECT AVG(CAST(ca.Puntuacion AS decimal(5,2))) FROM dbo.Calificaciones ca WHERE ca.TrabajoId=r.Id AND ca.CalificadorId=c.ClienteId AND ca.CalificadoId=r.CuidadorId) Calificacion
 FROM dbo.Trabajos r JOIN dbo.Trabajos t ON t.Id=r.ReemplazoDeTrabajoId JOIN dbo.Contratos c ON c.Id=t.ContratoId JOIN dbo.Usuarios u ON u.Id=r.CuidadorId
 WHERE c.Id=@ContratoId AND @UsuarioId IN(c.ClienteId,c.CuidadorId) ORDER BY r.Fecha DESC,r.Id DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoHistorialCliente @ContratoId int,@UsuarioId int
AS
BEGIN
 SET NOCOUNT ON;
 SELECT t.CuidadorId,COUNT(*) VecesConCliente,
 (SELECT AVG(CAST(ca.Puntuacion AS decimal(5,2))) FROM dbo.Calificaciones ca WHERE ca.CalificadorId=c.ClienteId AND ca.CalificadoId=t.CuidadorId) CalificacionConCliente
 FROM dbo.Contratos c JOIN dbo.Trabajos t ON t.ClienteId=c.ClienteId AND t.Estado=4
 WHERE c.Id=@ContratoId AND @UsuarioId IN(c.ClienteId,c.CuidadorId) GROUP BY t.CuidadorId,c.ClienteId;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoAusenciaTurno @ContratoId int,@UsuarioId int,@Fecha date
AS
BEGIN
 SET NOCOUNT ON;
 SELECT TOP 1 t.Id TrabajoId,c.ClienteId FROM dbo.Contratos c JOIN dbo.Trabajos t ON t.ContratoId=c.Id
 WHERE c.Id=@ContratoId AND c.CuidadorId=@UsuarioId AND t.Fecha=@Fecha AND t.Estado=5 ORDER BY t.HoraInicio;
END;
GO
CREATE OR ALTER PROCEDURE sp_CrearUsuarioCuidador
    @Email NVARCHAR(150),
    @PasswordHash NVARCHAR(255),
    @NombreCompleto NVARCHAR(150),
    @FotoUrl NVARCHAR(500),
    @Especialidad NVARCHAR(100),
    @TarifaHora DECIMAL(10,2),
    @Bio NVARCHAR(500),
    @MetodoCobro NVARCHAR(50),
    @CedulaUrl NVARCHAR(500),
    @CartaAntecedentesUrl NVARCHAR(500),
    @NacionalidadId INT = NULL,
    @DocumentoIdentidad NVARCHAR(30) = NULL,
    @Telefono NVARCHAR(20) = NULL,
    @DocumentosExtra NVARCHAR(MAX) = NULL, @EstatusMigratorioId int = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @NuevoUsuarioId INT;

        INSERT INTO Usuarios (Email, PasswordHash, NombreCompleto, FotoUrl, RolId, IsActive, FechaCreacion, NacionalidadId, DocumentoIdentidad, Telefono)
        VALUES (@Email, @PasswordHash, @NombreCompleto, @FotoUrl, 3, 1, GETDATE(), @NacionalidadId, NULLIF(LTRIM(RTRIM(@DocumentoIdentidad)), N''), NULLIF(LTRIM(RTRIM(@Telefono)), N''));
        SET @NuevoUsuarioId = SCOPE_IDENTITY();

        INSERT INTO PerfilCuidador (UsuarioId, Especialidad, TarifaHora, Bio, MetodoCobro, CedulaUrl, CartaAntecedentesUrl, EstadoAprobacion, EstatusMigratorioId)
        VALUES (@NuevoUsuarioId, @Especialidad, @TarifaHora, @Bio, @MetodoCobro, @CedulaUrl, @CartaAntecedentesUrl, 1, @EstatusMigratorioId);

        IF @CedulaUrl IS NOT NULL AND LEN(@CedulaUrl) > 0
        BEGIN
            INSERT INTO DocumentosCuidador (CuidadorId, TipoDocumento, UrlArchivo, Estado, FechaSubida)
            VALUES (@NuevoUsuarioId,
                    CASE WHEN EXISTS (SELECT 1 FROM Nacionalidades WHERE Id = @NacionalidadId AND CodigoIso <> 'DO')
                         THEN 'Pasaporte' ELSE 'Cedula' END,
                    @CedulaUrl, 1, GETDATE());
        END

        IF @CartaAntecedentesUrl IS NOT NULL AND LEN(@CartaAntecedentesUrl) > 0
        BEGIN
            INSERT INTO DocumentosCuidador (CuidadorId, TipoDocumento, UrlArchivo, Estado, FechaSubida)
            VALUES (@NuevoUsuarioId, 'CartaAntecedentes', @CartaAntecedentesUrl, 1, GETDATE());
        END

        -- Documentos opcionales (certificado médico, cursos, etc.): quedan pendientes de revisión (Estado 1).
        IF @DocumentosExtra IS NOT NULL AND ISJSON(@DocumentosExtra) = 1
        BEGIN
            INSERT INTO DocumentosCuidador (CuidadorId, TipoDocumento, UrlArchivo, Estado, FechaSubida)
            SELECT @NuevoUsuarioId, d.TipoDocumento, d.UrlArchivo, 1, GETDATE()
            FROM OPENJSON(@DocumentosExtra)
                 WITH (TipoDocumento NVARCHAR(100) '$.tipoDocumento', UrlArchivo NVARCHAR(500) '$.urlArchivo') AS d
            WHERE LEN(ISNULL(d.TipoDocumento, N'')) > 0 AND LEN(ISNULL(d.UrlArchivo, N'')) > 0;
        END

        COMMIT TRANSACTION;
        SELECT @NuevoUsuarioId AS NuevoId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratoProponer
    @ClienteId INT, @CuidadorId INT, @UbicacionId INT, @TipoServicio NVARCHAR(100), @TarifaHora DECIMAL(10,2),
    @MetodoPago VARCHAR(20), @FechaInicio DATE, @FechaFin DATE, @Horario NVARCHAR(400), @Nota NVARCHAR(600), @Ahora DATETIME, @Dormida bit=0, @Tareas nvarchar(max)=NULL
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

    INSERT INTO Contratos (ClienteId, CuidadorId, UbicacionId, TipoServicio, TarifaHora, MetodoPago, FechaInicio, FechaFin, Nota, Estado, FechaCreacion, FechaUltimaAccion, Dormida)
    VALUES (@ClienteId, @CuidadorId, @UbicacionId, @TipoServicio, @TarifaHora, @MetodoPago, @FechaInicio, @FechaFin, @Nota, 1, @Ahora, @Ahora, @Dormida);
    DECLARE @Id INT = SCOPE_IDENTITY();
    EXEC dbo.sp_ContratoGuardarHorario @Id, @Horario;
    INSERT INTO ContratoPropuestas (ContratoId, Version, PropuestoPor, TarifaHora, FechaInicio, Horario, Nota, Fecha, Dormida, Tareas)
    VALUES (@Id, 1, @ClienteId, @TarifaHora, @FechaInicio, dbo.fn_ContratoHorarioActual(@Id), @Nota, @Ahora, @Dormida, @Tareas);
    IF @Tareas IS NOT NULL EXEC dbo.sp_PilotoGuardarTareas @Id,@Tareas;
    COMMIT;

    SELECT 'OK' AS Resultado, @Id AS ContratoId, @CuidadorId AS NotificarA;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratoResponder
    @ContratoId INT, @UsuarioId INT, @Accion VARCHAR(20), @TarifaHora DECIMAL(10,2) = NULL, @FechaInicio DATE = NULL,
    @Horario NVARCHAR(400) = NULL, @Nota NVARCHAR(600) = NULL, @Ahora DATETIME, @Dormida bit=NULL, @Tareas nvarchar(max)=NULL
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
        UPDATE Contratos SET Dormida=COALESCE(@Dormida,Dormida), TarifaHora = @TarifaHora, FechaInicio = @FechaInicio, Nota = @Nota, Estado = @Nuevo, FechaUltimaAccion = @Ahora
        WHERE Id = @ContratoId AND Estado = @Estado AND (@Accion = 'retirar' OR (@Estado = 1 AND CuidadorId = @UsuarioId) OR (@Estado = 2 AND ClienteId = @UsuarioId));
        EXEC dbo.sp_ContratoGuardarHorario @ContratoId, @Horario;
        INSERT INTO ContratoPropuestas (ContratoId, Version, PropuestoPor, TarifaHora, FechaInicio, Horario, Nota, Fecha, Dormida, Tareas)
        SELECT @ContratoId, ISNULL(MAX(Version), 0) + 1, @UsuarioId, @TarifaHora, @FechaInicio, dbo.fn_ContratoHorarioActual(@ContratoId), @Nota, @Ahora, (SELECT Dormida FROM Contratos WHERE Id=@ContratoId), COALESCE(@Tareas,(SELECT Descripcion,RequiereFoto FROM ContratoTareas WHERE ContratoId=@ContratoId ORDER BY Orden FOR JSON PATH))
        FROM ContratoPropuestas WHERE ContratoId = @ContratoId;
        IF @Tareas IS NOT NULL EXEC dbo.sp_PilotoGuardarTareas @ContratoId,@Tareas;
        COMMIT;
        SELECT 'OK' AS Resultado, @Nuevo AS Estado, @Otro AS NotificarA; RETURN;
    END

    ROLLBACK;
    SELECT 'ACCION_INVALIDA' AS Resultado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratoDetalle @ContratoId INT, @UsuarioId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @UsuarioId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Contratos WHERE Id = @ContratoId AND @UsuarioId IN (ClienteId, CuidadorId))
        SET @ContratoId = -1;

    SELECT c.Dormida AS dormida, c.TrabajaDesde, c.ContratoAnteriorId, (SELECT TOP 1 n.Id FROM dbo.Contratos n WHERE n.ContratoAnteriorId=c.Id ORDER BY n.Id DESC) ContratoRenovadoId, c.Id, c.Estado, c.TipoServicio, c.TarifaHora, c.MetodoPago, c.FechaInicio, c.FechaFin, c.DiasPreaviso, c.Nota,
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

    SELECT p.Dormida AS dormida,p.Tareas AS tareas, p.Version, p.PropuestoPor, u.NombreCompleto AS PropuestoPorNombre, p.TarifaHora, p.FechaInicio, p.Horario, p.Nota, p.Fecha
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
    SELECT Id AS id,Descripcion AS descripcion,Orden AS orden,RequiereFoto AS requiereFoto FROM dbo.ContratoTareas WHERE ContratoId=@ContratoId ORDER BY Orden,Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratosPorUsuario @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.Dormida AS dormida, c.Id, c.Estado, c.TipoServicio, c.TarifaHora, c.MetodoPago, c.FechaInicio, c.FechaFin,
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

CREATE OR ALTER PROCEDURE dbo.sp_AdminContratos @Estado TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.Dormida AS dormida, c.Id, c.Estado, c.TipoServicio, c.TarifaHora, c.MetodoPago, c.FechaInicio, c.FechaFin, c.FechaCreacion, c.FechaActivacion,
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

CREATE OR ALTER PROCEDURE dbo.sp_ContratosGenerarTrabajos @Ahora DATETIME, @Dias INT = 7, @ContratoId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ContratoAnexos SET Estado=8 WHERE Estado IN(1,3) AND FechaCreacion<=DATEADD(hour,-72,@Ahora);
    DECLARE @Hoy DATE = CAST(@Ahora AS DATE);

    UPDATE Contratos SET Estado = 8, FechaUltimaAccion = @Ahora
    WHERE Estado IN (1, 2) AND FechaUltimaAccion < DATEADD(HOUR, -72, @Ahora);

    UPDATE Contratos SET Estado = 6, FechaUltimaAccion = @Ahora,
           FechaTerminacion = ISNULL(FechaTerminacion, @Ahora)
    WHERE Estado IN (4, 5) AND FechaFin < @Hoy;

    SET XACT_ABORT ON;
    DECLARE @Generados TABLE (TrabajoId INT, ClienteId INT, CuidadorId INT);
    DECLARE @Nuevos TABLE (TrabajoId INT, ClienteId INT, CuidadorId INT);
    BEGIN TRAN;
    DECLARE @LockResult INT;
    EXEC @LockResult = sys.sp_getapplock @Resource = 'ContratosGenerarTrabajos', @LockMode = 'Exclusive', @LockOwner = 'Transaction';
    IF @LockResult < 0 BEGIN ROLLBACK; THROW 50001, 'No se pudo bloquear la generación de jornadas.', 1; END;
    -- Never revive an obsolete schedule or price after a signed amendment.
    UPDATE t SET MotivoCancelacionTexto=N'Anexo de contrato fijo'
    FROM dbo.Trabajos t WHERE t.Estado=5 AND t.MotivoCancelacionTexto IN(N'Contrato fijo en pausa',N'Ausencia en contrato fijo',N'Permiso de contrataci?n revocado')
      AND EXISTS(SELECT 1 FROM dbo.ContratoAnexos a WHERE a.ContratoId=t.ContratoId AND a.Estado=4 AND a.FechaInicio<=t.Fecha);
    DECLARE @Ultima date=(SELECT MAX(t.Fecha) FROM dbo.Trabajos t WHERE t.MotivoCancelacionTexto=N'Anexo de contrato fijo' AND (@ContratoId IS NULL OR t.ContratoId=@ContratoId));
    IF @Ultima>=DATEADD(day,@Dias,@Hoy) SET @Dias=DATEDIFF(day,@Hoy,@Ultima)+1;
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
               CAST(COALESCE(a.TarifaHora,c.TarifaHora) * DATEDIFF(MINUTE, h.HoraInicio, h.HoraFin) / 60.0 AS DECIMAL(10,2)) AS Tarifa
        FROM Contratos c
        CROSS JOIN Dias d
        OUTER APPLY (SELECT TOP 1 ax.TarifaHora,ax.Horario FROM dbo.ContratoAnexos ax WHERE ax.ContratoId=c.Id AND ax.Estado=4 AND ax.FechaInicio<=d.Fecha ORDER BY ax.FechaInicio DESC,ax.Id DESC) a
        CROSS APPLY dbo.fn_ContratoHorarioTexto(COALESCE(a.Horario,dbo.fn_ContratoHorarioActual(c.Id))) h
        JOIN UbicacionesCliente ub ON ub.Id = c.UbicacionId
        WHERE c.Estado = 4 AND (@ContratoId IS NULL OR c.Id = @ContratoId)
          AND (DATEPART(WEEKDAY,d.Fecha)+@@DATEFIRST-1)%7=h.DiaSemana AND d.Fecha >= c.FechaInicio AND (c.FechaFin IS NULL OR d.Fecha <= c.FechaFin)
          -- hoy solo si todavía no empezó la jornada
          AND (d.Fecha > @Hoy OR CAST(h.HoraInicio AS TIME) > CAST(@Ahora AS TIME))
          AND NOT EXISTS (SELECT 1 FROM ContratoAusencias a WHERE a.ContratoId = c.Id AND a.Fecha = d.Fecha)
          -- Las jornadas existentes se restauran arriba si las canceló el sistema.
          AND NOT EXISTS (SELECT 1 FROM Trabajos t WITH (UPDLOCK, HOLDLOCK) WHERE t.ContratoId = c.Id AND t.Fecha = d.Fecha AND t.HoraInicio = h.HoraInicio AND ISNULL(t.MotivoCancelacionTexto,'')<>N'Anexo de contrato fijo')
    )
    INSERT INTO Trabajos (ClienteId, CuidadorId, TipoServicio, Fecha, HoraInicio, HoraFin, Direccion, Estado, Tarifa, Latitud, Longitud,
                          PinInicio, PinFin, FechaCreacion, MetodoPago, ContratoId)
    OUTPUT inserted.Id, inserted.ClienteId, inserted.CuidadorId INTO @Nuevos
    SELECT ClienteId, CuidadorId, TipoServicio, Fecha, HoraInicio, HoraFin, Direccion, 2, Tarifa, Latitud, Longitud,
           RIGHT('0000' + CAST(ABS(CHECKSUM(NEWID())) % 9000 + 1000 AS VARCHAR(4)), 4),
           RIGHT('0000' + CAST(ABS(CHECKSUM(NEWID())) % 9000 + 1000 AS VARCHAR(4)), 4),
           @Ahora, MetodoPago, ContratoId
    FROM Candidatos;
    INSERT @Generados SELECT * FROM @Nuevos;
    INSERT dbo.TareasTrabajo(TrabajoId,Descripcion,Orden,RequiereFoto)
    SELECT g.TrabajoId,ct.Descripcion,ct.Orden,ct.RequiereFoto FROM @Nuevos g JOIN dbo.Trabajos t ON t.Id=g.TrabajoId
    JOIN dbo.ContratoTareas ct ON ct.ContratoId=t.ContratoId
    WHERE NOT EXISTS(SELECT 1 FROM dbo.TareasTrabajo tt WHERE tt.TrabajoId=g.TrabajoId);
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

CREATE OR ALTER PROCEDURE dbo.sp_ContratoReemplazar @ContratoId int,@TrabajoId int,@UsuarioId int,@CuidadorId int,
 @TarifaHora decimal(10,2),@Ahora datetime
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRAN;
 DECLARE @Original int;
 SELECT @Original=t.Id FROM dbo.Trabajos t WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Contratos c ON c.Id=t.ContratoId
 WHERE c.Id=@ContratoId AND c.ClienteId=@UsuarioId AND c.CuidadorId<>@CuidadorId AND t.Id=@TrabajoId AND t.Estado=5
 AND EXISTS(SELECT 1 FROM dbo.ContratoAusencias a WHERE a.ContratoId=c.Id AND a.Fecha=t.Fecha AND a.MarcadoPor=c.CuidadorId)
 AND DATEADD(SECOND,DATEDIFF(SECOND,0,t.HoraInicio),CAST(t.Fecha AS datetime))>@Ahora;
 IF @Original IS NULL BEGIN ROLLBACK; SELECT 'NO_ENCONTRADO' Resultado; RETURN; END;
 IF EXISTS(SELECT 1 FROM dbo.Trabajos WHERE ReemplazoDeTrabajoId=@TrabajoId AND Estado IN(1,2,3,4,7))
 OR EXISTS(SELECT 1 FROM dbo.Trabajos x WITH(UPDLOCK,HOLDLOCK) JOIN dbo.Trabajos t ON t.Id=@TrabajoId
 WHERE x.CuidadorId=@CuidadorId AND x.Fecha=t.Fecha AND x.Estado IN(1,2,3,7) AND x.HoraInicio<t.HoraFin AND x.HoraFin>t.HoraInicio)
 BEGIN ROLLBACK; SELECT 'CHOQUE_HORARIO' Resultado; RETURN; END;
 INSERT dbo.Trabajos(ClienteId,CuidadorId,TipoServicio,Fecha,HoraInicio,HoraFin,Direccion,Estado,Tarifa,Latitud,Longitud,
 PinInicio,PinFin,FechaCreacion,MetodoPago,ReemplazoDeTrabajoId)
 SELECT ClienteId,@CuidadorId,TipoServicio,Fecha,HoraInicio,HoraFin,Direccion,1,
 @TarifaHora*DATEDIFF(minute,HoraInicio,HoraFin)/60.0,Latitud,Longitud,
 RIGHT('0000'+CONVERT(varchar(4),ABS(CHECKSUM(NEWID())%9000)+1000),4),
 RIGHT('0000'+CONVERT(varchar(4),ABS(CHECKSUM(NEWID())%9000)+1000),4),@Ahora,MetodoPago,Id FROM dbo.Trabajos WHERE Id=@TrabajoId;
 DECLARE @Nuevo int=SCOPE_IDENTITY();
 INSERT dbo.TareasTrabajo(TrabajoId,Descripcion,Orden,RequiereFoto)
 SELECT @Nuevo,Descripcion,Orden,RequiereFoto FROM dbo.ContratoTareas WHERE ContratoId=@ContratoId;
 COMMIT;
 SELECT 'OK' Resultado,@Nuevo TrabajoId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerTareasTrabajo
    @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RequiereFoto,FotoUrl,Id, TrabajoId, Descripcion, Orden, Completada, FechaCompletada
    FROM dbo.TareasTrabajo
    WHERE TrabajoId = @TrabajoId
    ORDER BY Orden, Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_CompletarTareaTrabajo
    @TareaId INT,
    @FechaCompletada DATETIME, @FotoUrl nvarchar(500)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM dbo.TareasTrabajo WHERE Id=@TareaId AND RequiereFoto=1) AND NULLIF(LTRIM(RTRIM(@FotoUrl)),N'') IS NULL
    THROW 50002,'FOTO_REQUERIDA',1;
    UPDATE t
    SET FotoUrl=NULLIF(LTRIM(RTRIM(@FotoUrl)),N''), Completada = 1, FechaCompletada = @FechaCompletada
    OUTPUT inserted.RequiereFoto,inserted.FotoUrl,inserted.Id, inserted.TrabajoId, inserted.Descripcion, inserted.Orden, inserted.Completada, inserted.FechaCompletada
    FROM dbo.TareasTrabajo t
    INNER JOIN dbo.Trabajos j ON j.Id = t.TrabajoId
    WHERE t.Id = @TareaId AND t.Completada = 0 AND j.Estado = 3;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminCambiarNivel @UsuarioId INT, @Nivel TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    IF @Nivel NOT IN (1, 2, 3, 4, 5, 6) BEGIN SELECT 'NIVEL_INVALIDO' AS Resultado; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @UsuarioId AND RolId = 1) BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    IF @Nivel <> 1 AND ISNULL((SELECT NivelAdmin FROM Usuarios WHERE Id = @UsuarioId), 2) = 1
       AND (SELECT COUNT(*) FROM Usuarios WHERE RolId = 1 AND IsActive = 1 AND NivelAdmin = 1) <= 1
    BEGIN SELECT 'ULTIMO_SUPERADMIN' AS Resultado; RETURN; END

    UPDATE Usuarios SET NivelAdmin = @Nivel WHERE Id = @UsuarioId;
    SELECT 'OK' AS Resultado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_PilotoCrearAdmin @Email nvarchar(150),@PasswordHash nvarchar(255),@NombreCompleto nvarchar(150),@NivelAdmin int=2
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @NivelAdmin NOT BETWEEN 1 AND 6 BEGIN SELECT 0 NuevoId,'NIVEL_INVALIDO' Motivo; RETURN; END;
 BEGIN TRAN;
 DECLARE @Resultado TABLE(NuevoId int,Motivo nvarchar(100));
 INSERT @Resultado EXEC dbo.sp_CrearUsuarioAdmin @Email=@Email,@PasswordHash=@PasswordHash,@NombreCompleto=@NombreCompleto;
 UPDATE u SET NivelAdmin=@NivelAdmin FROM dbo.Usuarios u JOIN @Resultado r ON r.NuevoId=u.Id WHERE r.Motivo='OK';
 COMMIT;
 SELECT NuevoId,Motivo FROM @Resultado;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ContratoRenovar
 @ContratoId int, @UsuarioId int, @FechaFin date, @Ahora datetime
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRAN;
 DECLARE @Fin date, @Cuidador int, @Horario nvarchar(400), @Error varchar(40);
 SELECT @Fin=FechaFin,@Cuidador=CuidadorId FROM dbo.Contratos WITH(UPDLOCK,HOLDLOCK)
 WHERE Id=@ContratoId AND ClienteId=@UsuarioId AND Estado IN (4,5,6) AND FechaTerminacion IS NULL;
 IF @Fin IS NULL OR @Fin<CAST(@Ahora AS date) OR @FechaFin<DATEADD(day,7,@Fin)
 BEGIN ROLLBACK; SELECT 'FECHA_INVALIDA' Resultado; RETURN; END;
 IF EXISTS(SELECT 1 FROM dbo.Contratos WHERE ContratoAnteriorId=@ContratoId AND Estado BETWEEN 1 AND 5)
 BEGIN ROLLBACK; SELECT 'DUPLICADO' Resultado; RETURN; END;
 SET @Horario=dbo.fn_ContratoHorarioActual(@ContratoId);
 DECLARE @Inicio date=DATEADD(day,1,@Fin);
 EXEC dbo.sp_ContratoValidarHorario @Cuidador,NULL,@Horario,@Inicio,@FechaFin,@Error OUTPUT;
 IF @Error IS NOT NULL BEGIN ROLLBACK; SELECT @Error Resultado; RETURN; END;
 INSERT dbo.Contratos(ClienteId,CuidadorId,UbicacionId,TipoServicio,TarifaHora,MetodoPago,FechaInicio,FechaFin,
 DiasPreaviso,Nota,Estado,FechaCreacion,FechaUltimaAccion,ContratoAnteriorId,Dormida)
 SELECT ClienteId,CuidadorId,UbicacionId,TipoServicio,TarifaHora,MetodoPago,@Inicio,@FechaFin,
 DiasPreaviso,Nota,1,@Ahora,@Ahora,Id,Dormida FROM dbo.Contratos WHERE Id=@ContratoId;
 DECLARE @Nuevo int=SCOPE_IDENTITY();
 INSERT dbo.ContratoTareas(ContratoId,Descripcion,Orden,RequiereFoto)
 SELECT @Nuevo,Descripcion,Orden,RequiereFoto FROM dbo.ContratoTareas WHERE ContratoId=@ContratoId;
 EXEC dbo.sp_ContratoGuardarHorario @Nuevo,@Horario;
 INSERT dbo.ContratoPropuestas(ContratoId,Version,PropuestoPor,TarifaHora,FechaInicio,Horario,Nota,Fecha,Dormida,Tareas)
 SELECT Id,1,@UsuarioId,TarifaHora,FechaInicio,@Horario,Nota,@Ahora,Dormida,(SELECT Descripcion,RequiereFoto FROM dbo.ContratoTareas WHERE ContratoId=@ContratoId ORDER BY Orden FOR JSON PATH) FROM dbo.Contratos WHERE Id=@Nuevo;
 COMMIT;
 SELECT 'OK' Resultado,@Nuevo ContratoId,@Cuidador NotificarA;
END
GO
