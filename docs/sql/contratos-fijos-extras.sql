-- Apply after contratos-fijos.sql. Do not execute as part of builds.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF COL_LENGTH('dbo.Contratos','TrabajaDesde') IS NULL ALTER TABLE dbo.Contratos ADD TrabajaDesde date NULL;
GO

-- Anexos: 1 propuesto, 3 por firmar, 4 firmado, 7 rechazado, 8 vencido.
IF OBJECT_ID('dbo.ContratoAnexos') IS NULL
 CREATE TABLE dbo.ContratoAnexos(Id int IDENTITY PRIMARY KEY, ContratoId int NOT NULL REFERENCES dbo.Contratos(Id),
 ClienteId int NOT NULL, CuidadorId int NOT NULL, PropuestoPor int NOT NULL, TarifaHora decimal(10,2) NOT NULL,
 Horario nvarchar(400) NOT NULL, FechaInicio date NOT NULL, Estado tinyint NOT NULL, FechaCreacion datetime NOT NULL,
 DocumentoId int NULL REFERENCES dbo.ContratoDocumentos(Id));
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.ContratoAnexos') AND name='UX_AnexoPendiente')
 CREATE UNIQUE INDEX UX_AnexoPendiente ON dbo.ContratoAnexos(ContratoId) WHERE Estado IN (1,3);
GO
IF OBJECT_ID('dbo.AnexoCodigos') IS NULL
CREATE TABLE dbo.AnexoCodigos (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    AnexoId INT NOT NULL REFERENCES dbo.ContratoAnexos (Id),
    UsuarioId  INT NOT NULL,
    CodigoHash VARCHAR(64) NOT NULL,
    Expira     DATETIME NOT NULL,
    Intentos   INT NOT NULL CONSTRAINT DF_AnexoCodigos_Intentos DEFAULT 0,
    Usado      BIT NOT NULL CONSTRAINT DF_AnexoCodigos_Usado DEFAULT 0
);
GO
IF OBJECT_ID('dbo.AnexoCodigoFallos') IS NULL
    CREATE TABLE dbo.AnexoCodigoFallos (
        Id INT IDENTITY PRIMARY KEY, AnexoId INT NOT NULL REFERENCES dbo.ContratoAnexos(Id), UsuarioId INT NOT NULL, Fecha DATETIME NOT NULL
    );
GO
IF OBJECT_ID('dbo.AnexoFirmas') IS NULL
BEGIN
    CREATE TABLE dbo.AnexoFirmas (
        Id         INT IDENTITY(1,1) PRIMARY KEY,
        AnexoId INT NOT NULL REFERENCES dbo.ContratoAnexos (Id),
        UsuarioId  INT NOT NULL,
        FirmaSvg   NVARCHAR(MAX) NOT NULL,
        Fecha      DATETIME NOT NULL,
        Ip         VARCHAR(64) NULL,
        Latitud    DECIMAL(9,6) NULL,
        Longitud   DECIMAL(9,6) NULL,
        Correo     NVARCHAR(256) NULL       -- a donde llegó el código de verificación
    );
    CREATE UNIQUE INDEX UX_AnexoFirmas ON dbo.AnexoFirmas (AnexoId, UsuarioId);
END
GO
IF COL_LENGTH('dbo.AnexoCodigos','FechaCreacion') IS NULL ALTER TABLE dbo.AnexoCodigos ADD FechaCreacion datetime NULL;
GO
IF COL_LENGTH('dbo.AnexoCodigos','BloqueadoHasta') IS NULL ALTER TABLE dbo.AnexoCodigos ADD BloqueadoHasta datetime NULL;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ContratoAnexoProponer @ContratoId int,@UsuarioId int,@TarifaHora decimal(10,2)=NULL,@Horario nvarchar(400)=NULL,@FechaInicio date,@Ahora datetime
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRAN;
 DECLARE @Cliente int,@Cuidador int,@Fin date,@Tarifa decimal(10,2),@Error varchar(40);
 SELECT @Cliente=ClienteId,@Cuidador=CuidadorId,@Fin=FechaFin,@Tarifa=TarifaHora FROM dbo.Contratos WITH(UPDLOCK,HOLDLOCK)
 WHERE Id=@ContratoId AND Estado=4 AND FechaTerminacion IS NULL AND @UsuarioId IN(ClienteId,CuidadorId);
 IF @Cliente IS NULL BEGIN ROLLBACK; SELECT 'ESTADO_INVALIDO' Resultado; RETURN; END;
 IF @FechaInicio IS NULL OR @FechaInicio<=CAST(@Ahora AS date) OR @FechaInicio>ISNULL(@Fin,'99991231')
 BEGIN ROLLBACK; SELECT 'FECHA_INVALIDA' Resultado; RETURN; END;
 UPDATE dbo.ContratoAnexos SET Estado=8 WHERE ContratoId=@ContratoId AND Estado IN(1,3) AND FechaCreacion<=DATEADD(hour,-72,@Ahora);
 IF EXISTS(SELECT 1 FROM dbo.ContratoAnexos WHERE ContratoId=@ContratoId AND Estado IN(1,3))
 BEGIN ROLLBACK; SELECT 'DUPLICADO' Resultado; RETURN; END;
 SELECT TOP 1 @Tarifa=TarifaHora FROM dbo.ContratoAnexos WHERE ContratoId=@ContratoId AND Estado=4 AND FechaInicio<=@FechaInicio ORDER BY FechaInicio DESC,Id DESC;
 SET @TarifaHora=COALESCE(@TarifaHora,@Tarifa);
 IF @Horario IS NULL SET @Horario=COALESCE((SELECT TOP 1 Horario FROM dbo.ContratoAnexos WHERE ContratoId=@ContratoId AND Estado=4 AND FechaInicio<=@FechaInicio ORDER BY FechaInicio DESC,Id DESC),dbo.fn_ContratoHorarioActual(@ContratoId));
 IF @TarifaHora<=0 BEGIN ROLLBACK; SELECT 'DATOS_INCOMPLETOS' Resultado; RETURN; END;
 EXEC dbo.sp_ContratoValidarHorario @Cuidador,@ContratoId,@Horario,@FechaInicio,@Fin,@Error OUTPUT;
 IF @Error IS NOT NULL BEGIN ROLLBACK; SELECT @Error Resultado; RETURN; END;
 INSERT dbo.ContratoAnexos(ContratoId,ClienteId,CuidadorId,PropuestoPor,TarifaHora,Horario,FechaInicio,Estado,FechaCreacion)
 VALUES(@ContratoId,@Cliente,@Cuidador,@UsuarioId,@TarifaHora,@Horario,@FechaInicio,1,@Ahora);
 DECLARE @Id int=SCOPE_IDENTITY(); COMMIT;
 SELECT 'OK' Resultado,@Id AnexoId,CASE WHEN @UsuarioId=@Cliente THEN @Cuidador ELSE @Cliente END NotificarA;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_ContratoAnexos @ContratoId int,@UsuarioId int=NULL
AS
BEGIN
 SET NOCOUNT ON;
 SELECT a.*, (SELECT COUNT(*) FROM dbo.AnexoFirmas f WHERE f.AnexoId=a.Id AND f.UsuarioId=@UsuarioId) YoFirme
 FROM dbo.ContratoAnexos a JOIN dbo.Contratos c ON c.Id=a.ContratoId
 WHERE c.Id=@ContratoId AND (@UsuarioId IS NULL OR @UsuarioId IN(c.ClienteId,c.CuidadorId)) ORDER BY a.Id DESC;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_ContratoAnexoResponder @AnexoId int,@UsuarioId int,@Accion varchar(20),@Ahora datetime
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRAN;
 DECLARE @Contrato int,@Otro int;
 SELECT @Contrato=a.ContratoId,@Otro=a.PropuestoPor FROM dbo.ContratoAnexos a WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Contratos c ON c.Id=a.ContratoId WHERE a.Id=@AnexoId AND a.Estado=1 AND c.Estado=4
 AND @UsuarioId IN(a.ClienteId,a.CuidadorId) AND @UsuarioId<>a.PropuestoPor
 AND a.FechaCreacion>DATEADD(hour,-72,@Ahora) AND a.FechaInicio>CAST(@Ahora AS date);
 IF @Contrato IS NULL OR @Accion NOT IN('aceptar','rechazar') BEGIN ROLLBACK; SELECT 'ESTADO_INVALIDO' Resultado; RETURN; END;
 UPDATE dbo.ContratoAnexos SET Estado=CASE WHEN @Accion='aceptar' THEN 3 ELSE 7 END WHERE Id=@AnexoId;
 COMMIT; SELECT 'OK' Resultado,@Contrato ContratoId,@Otro NotificarA;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_AnexoCodigoGenerar @AnexoId INT, @UsuarioId INT, @CodigoHash VARCHAR(64), @Expira DATETIME, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    IF NOT EXISTS(SELECT 1 FROM dbo.ContratoAnexos a JOIN dbo.Contratos c ON c.Id=a.ContratoId WHERE a.Id=@AnexoId AND c.Estado=4 AND a.FechaInicio>CAST(@Ahora AS date) AND a.FechaCreacion>DATEADD(hour,-72,@Ahora))
    BEGIN COMMIT; SELECT 'ESTADO_INVALIDO' Resultado; RETURN; END;
    DECLARE @Bloqueo INT;
    SELECT @Bloqueo = Id FROM ContratoAnexos WITH (UPDLOCK, HOLDLOCK) WHERE Id = @AnexoId;
    IF NOT EXISTS (SELECT 1 FROM ContratoAnexos WHERE Id = @AnexoId AND @UsuarioId IN (ClienteId, CuidadorId))
    BEGIN COMMIT; SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM ContratoAnexos WHERE Id = @AnexoId AND Estado = 3)
    BEGIN COMMIT; SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
    IF EXISTS (SELECT 1 FROM AnexoFirmas WHERE AnexoId = @AnexoId AND UsuarioId = @UsuarioId)
    BEGIN COMMIT; SELECT 'YA_FIRMADO' AS Resultado; RETURN; END
    IF EXISTS (SELECT 1 FROM AnexoCodigos WHERE AnexoId = @AnexoId AND UsuarioId = @UsuarioId AND BloqueadoHasta > @Ahora)
       OR (SELECT COUNT(*) FROM AnexoCodigoFallos WHERE AnexoId = @AnexoId AND UsuarioId = @UsuarioId AND Fecha > DATEADD(HOUR, -1, @Ahora)) >= 15
    BEGIN COMMIT; SELECT 'FIRMA_BLOQUEADA' AS Resultado; RETURN; END
    IF (SELECT COUNT(*) FROM AnexoCodigos WHERE AnexoId = @AnexoId AND UsuarioId = @UsuarioId AND ISNULL(FechaCreacion, DATEADD(MINUTE, -10, Expira)) > DATEADD(HOUR, -1, @Ahora)) >= 3
    BEGIN COMMIT; SELECT 'LIMITE_CODIGOS' AS Resultado; RETURN; END
    UPDATE AnexoCodigos SET Usado = 1 WHERE AnexoId = @AnexoId AND UsuarioId = @UsuarioId AND Usado = 0;
    INSERT INTO AnexoCodigos (AnexoId, UsuarioId, CodigoHash, Expira, FechaCreacion) VALUES (@AnexoId, @UsuarioId, @CodigoHash, @Expira, @Ahora);
    COMMIT;
    SELECT 'OK' AS Resultado, Email, NombreCompleto FROM Usuarios WHERE Id = @UsuarioId;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_AnexoFirmar
    @AnexoId INT, @UsuarioId INT, @CodigoHash VARCHAR(64), @FirmaSvg NVARCHAR(MAX), @Ip VARCHAR(64),
    @Latitud DECIMAL(9,6), @Longitud DECIMAL(9,6), @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    IF NOT EXISTS(SELECT 1 FROM dbo.ContratoAnexos a JOIN dbo.Contratos c ON c.Id=a.ContratoId WHERE a.Id=@AnexoId AND c.Estado=4 AND a.FechaInicio>CAST(@Ahora AS date) AND a.FechaCreacion>DATEADD(hour,-72,@Ahora))
    BEGIN COMMIT; SELECT 'ESTADO_INVALIDO' Resultado; RETURN; END;
    DECLARE @Bloqueo INT;
    SELECT @Bloqueo = Id FROM ContratoAnexos WITH (UPDLOCK, HOLDLOCK) WHERE Id = @AnexoId;
    IF NOT EXISTS (SELECT 1 FROM ContratoAnexos WHERE Id = @AnexoId AND Estado = 3 AND @UsuarioId IN (ClienteId, CuidadorId))
    BEGIN COMMIT; SELECT 'ESTADO_INVALIDO' AS Resultado; RETURN; END
    IF EXISTS (SELECT 1 FROM AnexoFirmas WHERE AnexoId = @AnexoId AND UsuarioId = @UsuarioId)
    BEGIN COMMIT; SELECT 'YA_FIRMADO' AS Resultado; RETURN; END
    IF EXISTS (SELECT 1 FROM AnexoCodigos WHERE AnexoId = @AnexoId AND UsuarioId = @UsuarioId AND BloqueadoHasta > @Ahora)
       OR (SELECT COUNT(*) FROM AnexoCodigoFallos WHERE AnexoId = @AnexoId AND UsuarioId = @UsuarioId AND Fecha > DATEADD(HOUR, -1, @Ahora)) >= 15
    BEGIN COMMIT; SELECT 'FIRMA_BLOQUEADA' AS Resultado; RETURN; END
    DECLARE @CodId INT, @Hash VARCHAR(64), @Expira DATETIME, @Intentos INT;
    SELECT TOP 1 @CodId = Id, @Hash = CodigoHash, @Expira = Expira, @Intentos = Intentos
    FROM AnexoCodigos WHERE AnexoId = @AnexoId AND UsuarioId = @UsuarioId AND Usado = 0 ORDER BY Id DESC;
    IF @CodId IS NULL OR @Expira <= @Ahora OR @Intentos >= 5
    BEGIN COMMIT; SELECT 'CODIGO_VENCIDO' AS Resultado; RETURN; END
    IF @Hash <> @CodigoHash
    BEGIN
        UPDATE AnexoCodigos SET Intentos = Intentos + 1 WHERE Id = @CodId;
        INSERT INTO AnexoCodigoFallos (AnexoId, UsuarioId, Fecha) VALUES (@AnexoId, @UsuarioId, @Ahora);
        DECLARE @Fallos INT = (SELECT COUNT(*) FROM AnexoCodigoFallos WHERE AnexoId = @AnexoId AND UsuarioId = @UsuarioId AND Fecha > DATEADD(HOUR, -1, @Ahora));
        IF @Fallos >= 15 UPDATE AnexoCodigos SET BloqueadoHasta = DATEADD(HOUR, 1, @Ahora) WHERE Id = @CodId;
        COMMIT;
        SELECT CASE WHEN @Fallos >= 15 THEN 'FIRMA_BLOQUEADA' ELSE 'CODIGO_INCORRECTO' END AS Resultado; RETURN;
    END
    IF LEN(ISNULL(@FirmaSvg, '')) < 20 BEGIN COMMIT; SELECT 'FIRMA_VACIA' AS Resultado; RETURN; END
    UPDATE AnexoCodigos SET Usado = 1 WHERE Id = @CodId;
    INSERT INTO AnexoFirmas (AnexoId, UsuarioId, FirmaSvg, Fecha, Ip, Latitud, Longitud, Correo)
    SELECT @AnexoId, @UsuarioId, @FirmaSvg, @Ahora, @Ip, @Latitud, @Longitud, Email FROM Usuarios WHERE Id = @UsuarioId;
    DECLARE @Completas BIT = CASE WHEN (SELECT COUNT(*) FROM AnexoFirmas WHERE AnexoId = @AnexoId) >= 2 THEN 1 ELSE 0 END;
    COMMIT;
    SELECT 'OK' AS Resultado, @Completas AS FirmasCompletas,
           (SELECT CASE WHEN ClienteId = @UsuarioId THEN CuidadorId ELSE ClienteId END FROM ContratoAnexos WHERE Id = @AnexoId) AS NotificarA;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_AnexoActivar @AnexoId int,@Ruta nvarchar(400),@Hash varchar(64),@Ahora datetime
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON; BEGIN TRAN;
 DECLARE @Contrato int,@Fecha date,@Version int,@Doc int,@Lock int;
 EXEC @Lock=sys.sp_getapplock @Resource='ContratosGenerarTrabajos',@LockMode='Exclusive',@LockOwner='Transaction';
 IF @Lock<0 BEGIN ROLLBACK; THROW 50001,'No se pudo bloquear la generación.',1; END;
 SELECT @Contrato=a.ContratoId,@Fecha=a.FechaInicio FROM dbo.ContratoAnexos a WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.Contratos c WITH(UPDLOCK,HOLDLOCK) ON c.Id=a.ContratoId WHERE a.Id=@AnexoId AND a.Estado=3 AND c.Estado=4
 AND a.FechaInicio>CAST(@Ahora AS date) AND a.FechaCreacion>DATEADD(hour,-72,@Ahora)
 AND (SELECT COUNT(*) FROM dbo.AnexoFirmas f WHERE f.AnexoId=a.Id)=2;
 IF @Contrato IS NULL BEGIN ROLLBACK; SELECT 'ESTADO_INVALIDO' Resultado; RETURN; END;
 SELECT @Version=ISNULL(MAX(Version),0)+1 FROM dbo.ContratoDocumentos WITH(UPDLOCK,HOLDLOCK) WHERE ContratoId=@Contrato;
 INSERT dbo.ContratoDocumentos(ContratoId,Version,Tipo,Ruta,Hash,Fecha) VALUES(@Contrato,@Version,N'Anexo',@Ruta,@Hash,@Ahora);
 SET @Doc=SCOPE_IDENTITY();
 UPDATE dbo.ContratoAnexos SET Estado=4,DocumentoId=@Doc WHERE Id=@AnexoId;
 UPDATE dbo.Trabajos SET Estado=5,MotivoCancelacionTexto=N'Anexo de contrato fijo' WHERE ContratoId=@Contrato AND Fecha>=@Fecha AND Estado IN(1,2);
 COMMIT; SELECT 'OK' Resultado;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_AnexoDetalle @AnexoId int
AS
BEGIN
 SET NOCOUNT ON;
 SELECT * FROM dbo.ContratoAnexos WHERE Id=@AnexoId;
 SELECT f.*,u.NombreCompleto FROM dbo.AnexoFirmas f JOIN dbo.Usuarios u ON u.Id=f.UsuarioId WHERE AnexoId=@AnexoId;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_AnexosPendientesActivar
AS
BEGIN
 SET NOCOUNT ON;
 SELECT Id FROM dbo.ContratoAnexos a WHERE Estado=3 AND (SELECT COUNT(*) FROM dbo.AnexoFirmas f WHERE f.AnexoId=a.Id)=2;
END
GO

-- Apply after contratos-fijos.sql. Idempotent; not executed against a database.
CREATE OR ALTER PROCEDURE dbo.sp_ContratoResumenMensual
 @ContratoId int, @Mes date, @Ahora datetime, @UsuarioId int = NULL
AS
BEGIN
 SET NOCOUNT ON;
 IF NOT EXISTS (SELECT 1 FROM dbo.Contratos WHERE Id=@ContratoId
 AND (@UsuarioId IS NULL OR @UsuarioId IN (ClienteId,CuidadorId))) RETURN;
 DECLARE @Desde date=DATEFROMPARTS(YEAR(@Mes),MONTH(@Mes),1);
 SELECT COUNT(CASE WHEN t.Estado=4 THEN 1 END) Completadas,
 COUNT(CASE WHEN t.Estado IN (1,2) AND t.FechaInicioReal IS NULL
 AND DATEADD(SECOND,DATEDIFF(SECOND,0,t.HoraFin),CAST(t.Fecha AS datetime)) < @Ahora THEN 1 END) NoIniciadas,
 COUNT(CASE WHEN t.Estado=5 THEN 1 END) Canceladas,
 COUNT(CASE WHEN t.Estado=5 AND a.MarcadoPor=c.ClienteId THEN 1 END) CanceladasCliente,
 COUNT(CASE WHEN t.Estado=5 AND a.MarcadoPor=c.CuidadorId THEN 1 END) CanceladasCuidador,
 COUNT(CASE WHEN t.Estado=5 AND a.Id IS NULL THEN 1 END) CanceladasSinAtribuir,
 CAST(COALESCE(SUM(CASE WHEN t.Estado=4 AND t.FechaInicioReal IS NOT NULL AND t.FechaFin>=t.FechaInicioReal
 THEN DATEDIFF(MINUTE,t.FechaInicioReal,t.FechaFin)/60.0 ELSE 0 END),0) AS decimal(12,2)) HorasTrabajadas,
 (SELECT COUNT(*) FROM dbo.ContratoAusencias a WHERE a.ContratoId=@ContratoId
 AND a.Fecha>=@Desde AND a.Fecha<DATEADD(MONTH,1,@Desde)) Ausencias
 FROM dbo.Trabajos t JOIN dbo.Contratos c ON c.Id=t.ContratoId
 LEFT JOIN dbo.ContratoAusencias a ON a.ContratoId=t.ContratoId AND a.Fecha=t.Fecha
 WHERE t.ContratoId=@ContratoId AND t.Fecha>=@Desde AND t.Fecha<DATEADD(MONTH,1,@Desde);
END
GO

IF COL_LENGTH('dbo.Contratos','ContratoAnteriorId') IS NULL
 ALTER TABLE dbo.Contratos ADD ContratoAnteriorId int NULL REFERENCES dbo.Contratos(Id);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_ContratoRenovacionPendiente' AND object_id=OBJECT_ID('dbo.Contratos'))
 CREATE INDEX UX_ContratoRenovacionPendiente ON dbo.Contratos(ContratoAnteriorId,Estado);
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
 DiasPreaviso,Nota,Estado,FechaCreacion,FechaUltimaAccion,ContratoAnteriorId)
 SELECT ClienteId,CuidadorId,UbicacionId,TipoServicio,TarifaHora,MetodoPago,@Inicio,@FechaFin,
 DiasPreaviso,Nota,1,@Ahora,@Ahora,Id FROM dbo.Contratos WHERE Id=@ContratoId;
 DECLARE @Nuevo int=SCOPE_IDENTITY();
 EXEC dbo.sp_ContratoGuardarHorario @Nuevo,@Horario;
 INSERT dbo.ContratoPropuestas(ContratoId,Version,PropuestoPor,TarifaHora,FechaInicio,Horario,Nota,Fecha)
 SELECT Id,1,@UsuarioId,TarifaHora,FechaInicio,@Horario,Nota,@Ahora FROM dbo.Contratos WHERE Id=@Nuevo;
 COMMIT;
 SELECT 'OK' Resultado,@Nuevo ContratoId,@Cuidador NotificarA;
END
GO
IF OBJECT_ID('dbo.ContratoRecordatorios') IS NULL
 CREATE TABLE dbo.ContratoRecordatorios(ContratoId int NOT NULL REFERENCES dbo.Contratos(Id),
 Dias int NOT NULL, FechaFin date NOT NULL, Enviado datetime NULL, CONSTRAINT PK_ContratoRecordatorios PRIMARY KEY(ContratoId,Dias,FechaFin));
GO
CREATE OR ALTER PROCEDURE dbo.sp_ContratoRecordatorios @Ahora datetime
AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.ContratoRecordatorios(ContratoId,Dias,FechaFin)
 SELECT c.Id,d.Dias,c.FechaFin FROM dbo.Contratos c CROSS JOIN (VALUES(14),(3)) d(Dias)
 WHERE c.Estado IN(4,5) AND DATEDIFF(day,CAST(@Ahora AS date),c.FechaFin) BETWEEN 0 AND d.Dias
 AND NOT EXISTS(SELECT 1 FROM dbo.Contratos n WHERE n.ContratoAnteriorId=c.Id AND n.Estado BETWEEN 1 AND 5)
 AND NOT EXISTS(SELECT 1 FROM dbo.ContratoRecordatorios r WHERE r.ContratoId=c.Id AND r.Dias=d.Dias AND r.FechaFin=c.FechaFin);
 SELECT r.ContratoId,r.Dias,r.FechaFin,c.ClienteId,c.CuidadorId FROM dbo.ContratoRecordatorios r
 JOIN dbo.Contratos c ON c.Id=r.ContratoId WHERE r.Enviado IS NULL AND c.Estado IN(4,5) AND c.FechaFin>=CAST(@Ahora AS date);
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_ContratoRecordatorioEnviado @ContratoId int,@Dias int,@FechaFin date,@Ahora datetime
AS
BEGIN
 SET NOCOUNT ON;
 UPDATE dbo.ContratoRecordatorios SET Enviado=@Ahora WHERE ContratoId=@ContratoId AND Dias=@Dias AND FechaFin=@FechaFin;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminContratoAlertas @Ahora datetime
AS
BEGIN
 SET NOCOUNT ON;
 SELECT c.Id ContratoId,'AUSENCIAS' Tipo,COUNT(*) Cantidad FROM dbo.Contratos c
 JOIN dbo.ContratoAusencias a ON a.ContratoId=c.Id AND a.MarcadoPor=c.CuidadorId
 WHERE a.Fecha>=DATEADD(day,-30,CAST(@Ahora AS date)) AND a.Fecha<=CAST(@Ahora AS date)
 GROUP BY c.Id HAVING COUNT(*)>=3
 UNION ALL
 SELECT ContratoId,'CANCELACIONES',COUNT(*) FROM dbo.Trabajos
 WHERE ContratoId IS NOT NULL AND Estado=5 AND Fecha>=DATEADD(day,-30,CAST(@Ahora AS date)) AND Fecha<=CAST(@Ahora AS date)
 GROUP BY ContratoId HAVING COUNT(*)>=3
 UNION ALL
 SELECT ContratoId,'ANEXO',COUNT(*) FROM dbo.ContratoAnexos WHERE Estado IN(1,3,8) AND FechaCreacion<=DATEADD(hour,-72,@Ahora) GROUP BY ContratoId
 UNION ALL
 SELECT c.Id,'RENOVACION',1 FROM dbo.Contratos c WHERE c.Estado IN(4,5)
 AND DATEDIFF(day,CAST(@Ahora AS date),c.FechaFin) BETWEEN 0 AND 14
 AND NOT EXISTS(SELECT 1 FROM dbo.Contratos n WHERE n.ContratoAnteriorId=c.Id AND n.Estado IN(3,4,5));
 SELECT d.Id FROM dbo.ContratoDocumentos d JOIN dbo.Contratos c ON c.Id=d.ContratoId WHERE c.Estado IN(4,5);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratoAusencia @ContratoId INT, @UsuarioId INT, @Fecha DATE, @Motivo NVARCHAR(300), @Ahora DATETIME, @Emergencia bit = 0
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

    IF @UsuarioId=@CuidadorId AND (LEN(LTRIM(RTRIM(ISNULL(@Motivo,''))))<5
       OR (@Emergencia=0 AND EXISTS (SELECT 1 FROM Trabajos WHERE ContratoId=@ContratoId AND Fecha=@Fecha
       AND DATEADD(SECOND,DATEDIFF(SECOND,0,HoraInicio),CAST(Fecha AS datetime))<DATEADD(hour,12,@Ahora))))
    BEGIN SELECT 'PREAVISO_REQUERIDO' Resultado; RETURN; END;
    IF NOT EXISTS (SELECT 1 FROM Trabajos WHERE ContratoId=@ContratoId AND Fecha=@Fecha AND Estado=2
       AND DATEADD(SECOND,DATEDIFF(SECOND,0,HoraInicio),CAST(Fecha AS datetime))>@Ahora)
    BEGIN SELECT 'FECHA_INVALIDA' Resultado; RETURN; END;
    INSERT INTO ContratoAusencias (ContratoId, Fecha, MarcadoPor, Motivo, FechaRegistro) VALUES (@ContratoId, @Fecha, @UsuarioId, @Motivo, @Ahora);
    UPDATE Trabajos SET Estado = 5, MotivoCancelacionTexto = N'Ausencia en contrato fijo'
    WHERE ContratoId = @ContratoId AND Fecha = @Fecha AND Estado IN (1, 2);
    SELECT 'OK' AS Resultado, CASE WHEN @UsuarioId = @ClienteId THEN @CuidadorId ELSE @ClienteId END AS NotificarA;
END
GO

IF COL_LENGTH('dbo.Trabajos','ReemplazoDeTrabajoId') IS NULL
 ALTER TABLE dbo.Trabajos ADD ReemplazoDeTrabajoId int NULL REFERENCES dbo.Trabajos(Id);
GO
CREATE OR ALTER PROCEDURE dbo.sp_ContratoTurnoReemplazo @ContratoId int,@TrabajoId int,@UsuarioId int,@Ahora datetime
AS
BEGIN
 SET NOCOUNT ON;
 SELECT t.*,c.CuidadorId TitularId FROM dbo.Trabajos t JOIN dbo.Contratos c ON c.Id=t.ContratoId
 WHERE c.Id=@ContratoId AND c.ClienteId=@UsuarioId AND t.Id=@TrabajoId AND t.Estado=5
 AND EXISTS(SELECT 1 FROM dbo.ContratoAusencias a WHERE a.ContratoId=c.Id AND a.Fecha=t.Fecha AND a.MarcadoPor=c.CuidadorId)
 AND DATEADD(SECOND,DATEDIFF(SECOND,0,t.HoraInicio),CAST(t.Fecha AS datetime))>@Ahora;
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
 DECLARE @Nuevo int=SCOPE_IDENTITY(); COMMIT;
 SELECT 'OK' Resultado,@Nuevo TrabajoId;
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
    IF EXISTS (SELECT 1 FROM dbo.ContratoAusencias a JOIN dbo.Trabajos t ON t.ContratoId=a.ContratoId AND t.Fecha=a.Fecha
        JOIN dbo.Trabajos r ON r.ReemplazoDeTrabajoId=t.Id
        WHERE a.Id=@AusenciaId AND r.Estado IN(1,2,3,4,7))
    BEGIN SELECT 'EN_CURSO' Resultado; RETURN; END;
    DELETE FROM ContratoAusencias WHERE Id = @AusenciaId;
    -- Se vuelve a generar el trabajo de ese día en la próxima pasada (sp_ContratosGenerarTrabajos).
    SELECT 'OK' AS Resultado, (SELECT CASE WHEN ClienteId = @UsuarioId THEN CuidadorId ELSE ClienteId END FROM Contratos WHERE Id = @ContratoId) AS NotificarA;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContratoDetalle @ContratoId INT, @UsuarioId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @UsuarioId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Contratos WHERE Id = @ContratoId AND @UsuarioId IN (ClienteId, CuidadorId))
        SET @ContratoId = -1;

    SELECT c.TrabajaDesde, c.ContratoAnteriorId, (SELECT TOP 1 n.Id FROM dbo.Contratos n WHERE n.ContratoAnteriorId=c.Id ORDER BY n.Id DESC) ContratoRenovadoId, c.Id, c.Estado, c.TipoServicio, c.TarifaHora, c.MetodoPago, c.FechaInicio, c.FechaFin, c.DiasPreaviso, c.Nota,
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

IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Trabajos') AND name='UX_Trabajos_ContratoInicio') DROP INDEX UX_Trabajos_ContratoInicio ON dbo.Trabajos;
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Trabajos') AND name='UX_Trabajos_ContratoVigente')
 CREATE UNIQUE INDEX UX_Trabajos_ContratoVigente ON dbo.Trabajos(ContratoId,Fecha,HoraInicio) WHERE ContratoId IS NOT NULL AND Estado<>5;
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

CREATE OR ALTER PROCEDURE dbo.sp_ContratoCancelarTrabajosFuturos @ContratoId INT, @Desde DATE, @Hasta DATE = NULL, @Motivo NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Trabajos SET Estado = 5, MotivoCancelacionTexto = @Motivo
    OUTPUT inserted.Id, inserted.ClienteId, inserted.CuidadorId
    WHERE ContratoId = @ContratoId AND Estado IN (1, 2) AND Fecha >= @Desde AND (@Hasta IS NULL OR Fecha <= @Hasta);
END
GO
