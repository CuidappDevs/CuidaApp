-- Ejecutar después de calidad-documentos-comision-disputas.sql y parciales-piloto.sql.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF OBJECT_ID(N'dbo.TiposDocumentoVencimiento', N'U') IS NULL
 CREATE TABLE dbo.TiposDocumentoVencimiento(TipoDocumento nvarchar(100) COLLATE Latin1_General_100_CI_AS NOT NULL PRIMARY KEY);
GO
INSERT dbo.TiposDocumentoVencimiento(TipoDocumento)
SELECT v.Tipo FROM (VALUES (N'Cedula'),(N'CartaAntecedentes'),(N'Certificado Apostillado'),(N'Certificado médico'),
 (N'Certificado de Buena Conducta (PGR)'),(N'Carnet de residencia'),(N'Carnet PNRE'),(N'Visa de trabajo'),(N'Pasaporte')) v(Tipo)
WHERE NOT EXISTS(SELECT 1 FROM dbo.TiposDocumentoVencimiento t
 WHERE LTRIM(RTRIM(t.TipoDocumento)) COLLATE Latin1_General_100_CI_AS = v.Tipo COLLATE Latin1_General_100_CI_AS);
UPDATE dbo.EstatusMigratorio SET Activo=0
 WHERE LTRIM(RTRIM(Nombre)) COLLATE Latin1_General_100_CI_AS=N'Extranjero en proceso';
GO
CREATE OR ALTER FUNCTION dbo.fn_DocumentoRequiereVencimiento(@Tipo nvarchar(100)) RETURNS bit AS
BEGIN
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.TiposDocumentoVencimiento
 WHERE LTRIM(RTRIM(TipoDocumento)) COLLATE Latin1_General_100_CI_AS=LTRIM(RTRIM(@Tipo)) COLLATE Latin1_General_100_CI_AS) THEN 1 ELSE 0 END;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerDocumentosPorCuidador @CuidadorId int AS
BEGIN
 SET NOCOUNT ON;
 SELECT Id,TipoDocumento,UrlArchivo,Estado,ObservacionesAdmin,FechaSubida,
 dbo.fn_DocumentoRequiereVencimiento(TipoDocumento) RequiereVencimiento,CAST(FechaVencimiento AS date) FechaVencimiento
 FROM dbo.DocumentosCuidador WHERE CuidadorId=@CuidadorId ORDER BY Id;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AdminFijarVencimientoDocumento @DocumentoId int,@Fecha date=NULL,@Hoy date=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 SET @Hoy=COALESCE(@Hoy,CAST(DATEADD(HOUR,-4,GETUTCDATE()) AS date));
 BEGIN TRAN;
 DECLARE @Tipo nvarchar(100);
 SELECT @Tipo=TipoDocumento FROM dbo.DocumentosCuidador WITH(UPDLOCK,HOLDLOCK) WHERE Id=@DocumentoId;
 DECLARE @Resultado varchar(50)=CASE
 WHEN @Tipo IS NULL THEN 'NO_ENCONTRADO'
 WHEN @Fecha IS NULL AND dbo.fn_DocumentoRequiereVencimiento(@Tipo)=1 THEN 'FECHA_VENCIMIENTO_REQUERIDA'
 WHEN @Fecha<=@Hoy THEN 'FECHA_VENCIMIENTO_INVALIDA' ELSE 'OK' END;
 IF @Resultado='OK' UPDATE dbo.DocumentosCuidador SET FechaVencimiento=@Fecha,AvisoVencimiento=0 WHERE Id=@DocumentoId;
 COMMIT;
 SELECT @Resultado Resultado;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_AdminRevisarDocumento @DocumentoId int,@Aprobar bit,@Fecha date=NULL,@Observacion nvarchar(500)=NULL,@Hoy date=NULL AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 SET @Hoy=COALESCE(@Hoy,CAST(DATEADD(HOUR,-4,GETUTCDATE()) AS date));
 BEGIN TRAN;
 DECLARE @Tipo nvarchar(100),@Estado int,@CuidadorId int;
 SELECT @Tipo=TipoDocumento,@Estado=Estado,@CuidadorId=CuidadorId FROM dbo.DocumentosCuidador WITH(UPDLOCK,HOLDLOCK) WHERE Id=@DocumentoId;
 DECLARE @Resultado varchar(50)=CASE
 WHEN @Tipo IS NULL THEN 'NO_ENCONTRADO'
 WHEN @Estado NOT IN(1,3) THEN 'ESTADO_INVALIDO'
 WHEN @Aprobar=0 AND NULLIF(LTRIM(RTRIM(@Observacion)),N'') IS NULL THEN 'MOTIVO_REQUERIDO'
 WHEN @Aprobar=1 AND @Fecha IS NULL AND dbo.fn_DocumentoRequiereVencimiento(@Tipo)=1 THEN 'FECHA_VENCIMIENTO_REQUERIDA'
 WHEN @Fecha<=@Hoy THEN 'FECHA_VENCIMIENTO_INVALIDA' ELSE 'OK' END;
 IF @Resultado='OK'
 BEGIN
 IF @Aprobar=1 UPDATE dbo.DocumentosCuidador SET Estado=2,ObservacionesAdmin=NULL,FechaVencimiento=@Fecha,AvisoVencimiento=0 WHERE Id=@DocumentoId;
 ELSE UPDATE dbo.DocumentosCuidador SET Estado=3,ObservacionesAdmin=LTRIM(RTRIM(@Observacion)) WHERE Id=@DocumentoId;
 END;
 COMMIT;
 SELECT @Resultado Resultado,@CuidadorId CuidadorId;
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
 IF @NuevoEstado=2
 BEGIN
 DECLARE @Faltantes TABLE(TipoDocumento nvarchar(200));
 INSERT @Faltantes SELECT TipoDocumento+N' ('+CASE WHEN Estado=3 THEN N'rechazado' WHEN Estado<>2 OR Estado IS NULL THEN N'sin revisar'
 WHEN FechaVencimiento IS NULL THEN N'sin fecha' ELSE N'vencido' END+N')'
 FROM dbo.DocumentosCuidador WITH(UPDLOCK,HOLDLOCK) WHERE CuidadorId=@CuidadorId
 AND (Estado IS NULL OR Estado<>2 OR (dbo.fn_DocumentoRequiereVencimiento(TipoDocumento)=1 AND (FechaVencimiento IS NULL OR FechaVencimiento<=@Hoy)));
 IF EXISTS(SELECT 1 FROM @Faltantes)
 BEGIN
 SELECT 'DOCUMENTOS_SIN_REVISAR' Resultado;
 SELECT TipoDocumento FROM @Faltantes ORDER BY TipoDocumento;
 COMMIT; RETURN;
 END;
 END;
 -- Solo se llama al procedimiento anterior después de comprobar que no hay pendientes.
 DECLARE @Resultado TABLE(Filas int);
 INSERT @Resultado EXEC dbo.sp_ActualizarEstadoCuidador @CuidadorId=@CuidadorId,@NuevoEstado=@NuevoEstado,@Observaciones=@Observaciones;
 SELECT CASE WHEN EXISTS(SELECT 1 FROM @Resultado WHERE Filas>0) THEN 'OK' ELSE 'NO_ENCONTRADO' END Resultado;
 COMMIT;
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
 AND LTRIM(RTRIM(d.TipoDocumento)) COLLATE Latin1_General_100_CI_AS=r.TipoDocumento COLLATE Latin1_General_100_CI_AS
 AND d.Estado=2 AND NULLIF(LTRIM(RTRIM(d.UrlArchivo)),N'') IS NOT NULL AND (dbo.fn_DocumentoRequiereVencimiento(d.TipoDocumento)=0 OR d.FechaVencimiento>@Hoy)));
GO
CREATE OR ALTER PROCEDURE dbo.sp_PilotoMigratorio @UsuarioId int,@Hoy date
AS
BEGIN
 SET NOCOUNT ON;
 SELECT CAST(CASE WHEN n.CodigoIso<>'DO' THEN 1 ELSE 0 END AS bit) EsExtranjero,n.Nombre Nacionalidad,p.EstatusMigratorioId EstatusId,e.Nombre Estatus,u.Email,CAST(COALESCE(e.Activo,0) AS bit) EstatusActivo
 FROM dbo.PerfilCuidador p JOIN dbo.Usuarios u ON u.Id=p.UsuarioId LEFT JOIN dbo.Nacionalidades n ON n.Id=u.NacionalidadId
 LEFT JOIN dbo.EstatusMigratorio e ON e.Id=p.EstatusMigratorioId WHERE p.UsuarioId=@UsuarioId;
 ;WITH Tipos AS(SELECT CAST(N'Pasaporte' AS nvarchar(100)) Tipo UNION SELECT r.TipoDocumento FROM dbo.PerfilCuidador p
 JOIN dbo.RequisitosDocumentoPorEstatus r ON r.EstatusMigratorioId=p.EstatusMigratorioId WHERE p.UsuarioId=@UsuarioId)
 SELECT t.Tipo,d.Estado,d.FechaVencimiento Vencimiento,d.UrlArchivo Url,CAST(CASE WHEN NULLIF(LTRIM(RTRIM(d.UrlArchivo)),N'') IS NULL THEN 0 ELSE 1 END AS bit) Presente
 FROM Tipos t OUTER APPLY(SELECT TOP 1 * FROM dbo.DocumentosCuidador d WHERE d.CuidadorId=@UsuarioId
 AND LTRIM(RTRIM(d.TipoDocumento)) COLLATE Latin1_General_100_CI_AS=t.Tipo COLLATE Latin1_General_100_CI_AS
 ORDER BY CASE WHEN d.Estado=2 AND (dbo.fn_DocumentoRequiereVencimiento(d.TipoDocumento)=0 OR d.FechaVencimiento>@Hoy) AND NULLIF(LTRIM(RTRIM(d.UrlArchivo)),N'') IS NOT NULL THEN 0 ELSE 1 END,d.Id DESC) d;
 SELECT TipoDocumento FROM dbo.fn_PilotoMigratorioFaltantes(@UsuarioId,@Hoy);
END;
GO
CREATE OR ALTER FUNCTION dbo.fn_TieneDocumentoVencido(@CuidadorId int,@Hoy date) RETURNS bit AS
BEGIN
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.DocumentosCuidador WHERE CuidadorId=@CuidadorId AND FechaVencimiento<=@Hoy) THEN 1 ELSE 0 END;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ProcesarVencimientosDocumentos @Hoy DATE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Avisos TABLE (DocumentoId INT, CuidadorId INT, TipoDocumento NVARCHAR(100), FechaVencimiento DATE, Nivel TINYINT);

    UPDATE DocumentosCuidador
    SET Estado = 3, AvisoVencimiento = 3,
        ObservacionesAdmin = N'Venció el ' + CONVERT(NVARCHAR(10), FechaVencimiento, 103) + N'. Sube uno vigente.'
    OUTPUT inserted.Id, inserted.CuidadorId, inserted.TipoDocumento, inserted.FechaVencimiento, 3 INTO @Avisos
    WHERE FechaVencimiento <= @Hoy AND AvisoVencimiento < 3 AND Estado = 2;

    UPDATE DocumentosCuidador SET AvisoVencimiento = 2
    OUTPUT inserted.Id, inserted.CuidadorId, inserted.TipoDocumento, inserted.FechaVencimiento, 2 INTO @Avisos
    WHERE Estado = 2 AND FechaVencimiento > @Hoy AND DATEDIFF(DAY, @Hoy, FechaVencimiento) <= 7 AND AvisoVencimiento < 2;

    UPDATE DocumentosCuidador SET AvisoVencimiento = 1
    OUTPUT inserted.Id, inserted.CuidadorId, inserted.TipoDocumento, inserted.FechaVencimiento, 1 INTO @Avisos
    WHERE Estado = 2 AND DATEDIFF(DAY, @Hoy, FechaVencimiento) BETWEEN 8 AND 30 AND AvisoVencimiento < 1;

    -- Ocultar a quien tenga algo vencido (avisa la API por SignalR con la columna Oculto).
    DECLARE @Ocultos TABLE (CuidadorId INT);
    UPDATE PerfilCuidador SET Disponible = 0
    OUTPUT inserted.UsuarioId INTO @Ocultos
    WHERE Disponible = 1 AND dbo.fn_TieneDocumentoVencido(UsuarioId, @Hoy) = 1;

    SELECT a.DocumentoId, a.CuidadorId, a.TipoDocumento, a.FechaVencimiento, a.Nivel,
           CAST(IIF(o.CuidadorId IS NULL, 0, 1) AS BIT) AS Oculto
    FROM @Avisos a LEFT JOIN (SELECT DISTINCT CuidadorId FROM @Ocultos) o ON o.CuidadorId = a.CuidadorId
    UNION ALL
    -- Ocultos sin aviso nuevo (p. ej. se pusieron visibles por otra vía): solo para el evento de SignalR.
    SELECT NULL, o.CuidadorId, NULL, NULL, 0, CAST(1 AS BIT)
    FROM (SELECT DISTINCT CuidadorId FROM @Ocultos) o
    WHERE NOT EXISTS (SELECT 1 FROM @Avisos a WHERE a.CuidadorId = o.CuidadorId);
END
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
