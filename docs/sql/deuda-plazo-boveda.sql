-- Aplicar manualmente despues de deuda-cliente-eliminar-cuenta.sql y contratos/anexos.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF COL_LENGTH('dbo.DeudasCliente','FechaLimite') IS NULL
 ALTER TABLE dbo.DeudasCliente ADD FechaLimite DATETIME2 NULL;
IF COL_LENGTH('dbo.DeudasCliente','AvisoVencidaEnviado') IS NULL
 ALTER TABLE dbo.DeudasCliente ADD AvisoVencidaEnviado BIT NOT NULL CONSTRAINT DF_DeudaVencida DEFAULT 0;
IF COL_LENGTH('dbo.DeudasCliente','AvisoRecordatorioEnviado') IS NULL
 ALTER TABLE dbo.DeudasCliente ADD AvisoRecordatorioEnviado BIT NOT NULL CONSTRAINT DF_DeudaRecordatorio DEFAULT 0;
GO
UPDATE dbo.DeudasCliente SET FechaLimite=DATEADD(HOUR,24,FechaCreacion) WHERE FechaLimite IS NULL;
GO
IF COL_LENGTH('dbo.ContratoDocumentos','EnBoveda') IS NULL
 ALTER TABLE dbo.ContratoDocumentos ADD EnBoveda BIT NOT NULL CONSTRAINT DF_ContratoBoveda DEFAULT 0;
IF COL_LENGTH('dbo.ContratoDocumentos','RutaBoveda') IS NULL
 ALTER TABLE dbo.ContratoDocumentos ADD RutaBoveda NVARCHAR(400) NULL;
IF COL_LENGTH('dbo.ContratoDocumentos','FechaBoveda') IS NULL
 ALTER TABLE dbo.ContratoDocumentos ADD FechaBoveda DATETIME2 NULL;
IF COL_LENGTH('dbo.ContratoDocumentos','TamanoBytes') IS NULL
 ALTER TABLE dbo.ContratoDocumentos ADD TamanoBytes BIGINT NULL;
IF OBJECT_ID('dbo.BovedaAccesos') IS NULL
 CREATE TABLE dbo.BovedaAccesos(Id BIGINT IDENTITY PRIMARY KEY,DocumentoId INT NOT NULL REFERENCES dbo.ContratoDocumentos(Id),
 AdminId INT NOT NULL REFERENCES dbo.Usuarios(Id),Fecha DATETIME2 NOT NULL,Motivo NVARCHAR(400) NOT NULL,Ip NVARCHAR(64) NULL);
GO
CREATE OR ALTER PROCEDURE dbo.sp_FinalizarTrabajoConDeuda
 @TrabajoId INT,@Pin NVARCHAR(20),@Justificacion NVARCHAR(1000)=NULL,
 @MontoRecibido DECIMAL(18,4)=NULL,@Ahora DATETIME2
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @Total DECIMAL(10,2),@Metodo VARCHAR(20),@Cliente INT,@Cuidador INT;
 SELECT @Total=ISNULL((SELECT TOP 1 Monto FROM dbo.Pagos WHERE TrabajoId=t.Id),t.Tarifa),
 @Metodo=t.MetodoPago,@Cliente=t.ClienteId,@Cuidador=t.CuidadorId
 FROM dbo.Trabajos t WITH(UPDLOCK,HOLDLOCK) WHERE t.Id=@TrabajoId;
 IF @Metodo='efectivo' AND @MontoRecibido IS NOT NULL AND
 (@MontoRecibido<0 OR @MontoRecibido>@Total OR @MontoRecibido<>ROUND(@MontoRecibido,2))
 BEGIN ROLLBACK; SELECT 0 FilasAfectadas,'MONTO_RECIBIDO_INVALIDO' Motivo; RETURN; END;
 DECLARE @DeudaCreada BIT=0;
 DECLARE @R TABLE(FilasAfectadas INT,Motivo VARCHAR(100));
 INSERT @R EXEC dbo.sp_FinalizarTrabajo @TrabajoId=@TrabajoId,@Pin=@Pin,@Justificacion=@Justificacion;
 IF EXISTS(SELECT 1 FROM @R WHERE FilasAfectadas>0) AND @Metodo='efectivo' AND @MontoRecibido<@Total
 AND NOT EXISTS(SELECT 1 FROM dbo.DeudasCliente WHERE TrabajoId=@TrabajoId)
 BEGIN
 INSERT dbo.DeudasCliente(ClienteId,TrabajoId,CuidadorId,MontoTotal,MontoRecibido,Monto,FechaCreacion,FechaLimite)
 VALUES(@Cliente,@TrabajoId,@Cuidador,@Total,@MontoRecibido,@Total-@MontoRecibido,@Ahora,DATEADD(HOUR,24,@Ahora));
 SET @DeudaCreada=1;
 END;
 COMMIT;
 SELECT FilasAfectadas,Motivo,@DeudaCreada DeudaCreada FROM @R;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_DeudasClienteLeer @ClienteId INT=NULL,@TrabajoId INT=NULL,@Estado TINYINT=NULL,@Ahora DATETIME2=NULL AS
BEGIN
 SET NOCOUNT ON;
 SET @Ahora=ISNULL(@Ahora,CONVERT(DATETIME2,SWITCHOFFSET(SYSDATETIMEOFFSET(),'-04:00')));
 SELECT d.Id,d.TrabajoId,t.Fecha FechaServicio,u.NombreCompleto NombreCuidador,
 d.MontoTotal,d.MontoRecibido,d.Monto,d.Estado,d.ComprobanteUrl,d.NotaAdmin,d.FechaCreacion,d.FechaLimite,CAST(IIF(d.Estado IN(1,3) AND @Ahora>d.FechaLimite,1,0) AS BIT) Vencida,
 d.ClienteId,c.NombreCompleto NombreCliente,c.Email CorreoCliente,d.CuidadorId,d.NotaCliente,d.FechaResolucion,d.AdminId
 FROM dbo.DeudasCliente d JOIN dbo.Trabajos t ON t.Id=d.TrabajoId
 JOIN dbo.Usuarios u ON u.Id=d.CuidadorId JOIN dbo.Usuarios c ON c.Id=d.ClienteId
 WHERE (@ClienteId IS NULL OR d.ClienteId=@ClienteId) AND (@TrabajoId IS NULL OR d.TrabajoId=@TrabajoId)
 AND (@Estado IS NULL OR d.Estado=@Estado)
 ORDER BY IIF(d.Estado IN(1,3),0,1),d.FechaCreacion DESC,d.Id DESC;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_DeudaClienteAccion
 @Id INT,@Accion VARCHAR(20),@ClienteId INT=NULL,@ComprobanteUrl NVARCHAR(1000)=NULL,
 @Nota NVARCHAR(1000)=NULL,@Monto DECIMAL(18,4)=NULL,@AdminId INT=NULL,@Ahora DATETIME2
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @Estado TINYINT,@Cuidador INT,@Trabajo INT,@Pendiente DECIMAL(10,2),@Cliente INT;
 SELECT @Estado=Estado,@Cuidador=CuidadorId,@Trabajo=TrabajoId,@Pendiente=Monto,@Cliente=ClienteId
 FROM dbo.DeudasCliente WITH(UPDLOCK,HOLDLOCK) WHERE Id=@Id;
 IF @Estado IS NULL OR (@AdminId IS NULL AND ISNULL(@ClienteId,0)<>@Cliente)
 BEGIN ROLLBACK; SELECT 'NO_ENCONTRADO' Resultado; RETURN; END;
 IF @Estado NOT IN(1,3) OR (@Accion='reabrir' AND @Estado<>3)
 BEGIN ROLLBACK; SELECT 'ESTADO_INVALIDO' Resultado; RETURN; END;
 IF @Accion NOT IN('comprobante','objetar','confirmar-pago','condonar','ajustar','reabrir')
 OR (@AdminId IS NULL AND @Accion NOT IN('comprobante','objetar'))
 BEGIN ROLLBACK; SELECT 'ACCION_INVALIDA' Resultado; RETURN; END;
 IF @Accion IN('objetar','condonar','ajustar','reabrir') AND NULLIF(LTRIM(RTRIM(@Nota)),'') IS NULL
 BEGIN ROLLBACK; SELECT 'NOTA_REQUERIDA' Resultado; RETURN; END;
 IF @Accion='comprobante' AND NULLIF(LTRIM(RTRIM(@ComprobanteUrl)),'') IS NULL
 BEGIN ROLLBACK; SELECT 'COMPROBANTE_REQUERIDO' Resultado; RETURN; END;
 IF @Accion='ajustar' AND (@Monto IS NULL OR @Monto<0 OR @Monto>99999999.99 OR @Monto<>ROUND(@Monto,2))
 BEGIN ROLLBACK; SELECT 'MONTO_INVALIDO' Resultado; RETURN; END;
 IF @Accion='confirmar-pago' AND NOT EXISTS(SELECT 1 FROM dbo.DeudasCliente WHERE Id=@Id AND NULLIF(LTRIM(RTRIM(ComprobanteUrl)),'') IS NOT NULL)
 BEGIN ROLLBACK; SELECT 'COMPROBANTE_REQUERIDO' Resultado; RETURN; END;
 IF @Accion='confirmar-pago'
 BEGIN
 -- PROVISIONAL: crÃ©dito Ã­ntegro al saldo; negativo = a favor de la cuidadora.
 INSERT dbo.MovimientosSaldoCuidador(CuidadorId,TrabajoId,Tipo,Monto,Nota,AdminId,Fecha)
 VALUES(@Cuidador,@Trabajo,'abono',-@Pendiente,CONCAT(N'Cobro de deuda de cliente #',@Id,N' recibido por la plataforma'),@AdminId,@Ahora);
 END;
 UPDATE dbo.DeudasCliente SET
 Estado=CASE @Accion WHEN 'confirmar-pago' THEN 2 WHEN 'condonar' THEN 4 WHEN 'objetar' THEN 3
 WHEN 'ajustar' THEN IIF(@Monto=0,4,Estado) ELSE 1 END,
 Monto=IIF(@Accion='ajustar',@Monto,Monto),
 ComprobanteUrl=IIF(@Accion='comprobante',@ComprobanteUrl,ComprobanteUrl),
 NotaCliente=IIF(@AdminId IS NULL,@Nota,NotaCliente),NotaAdmin=IIF(@AdminId IS NOT NULL,@Nota,NotaAdmin),
 AdminId=ISNULL(@AdminId,AdminId),
 FechaResolucion=IIF(@Accion IN('confirmar-pago','condonar') OR (@Accion='ajustar' AND @Monto=0),@Ahora,NULL)
 WHERE Id=@Id;
 COMMIT; SELECT 'OK' Resultado,@Cliente ClienteId;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_AnonimizarUsuario @UsuarioId INT,@Ahora DATETIME2,@PasswordHash VARCHAR(64) AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @Lock INT,@Solicitud INT;
 SELECT @Lock=Id FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE Id=@UsuarioId;
 SELECT @Solicitud=Id FROM dbo.SolicitudesEliminacionCuenta WITH(UPDLOCK,HOLDLOCK)
 WHERE UsuarioId=@UsuarioId AND Estado=1 AND FechaEjecucion<=@Ahora;
 IF @Solicitud IS NULL BEGIN ROLLBACK; SELECT 'NO_ENCONTRADO' Resultado; RETURN; END;
 -- RevisiÃ³n final: si surgiÃ³ una obligaciÃ³n durante el plazo, se pospone la anonimizaciÃ³n.
 DECLARE @B TABLE(Codigo VARCHAR(40),Detalle NVARCHAR(300),ReferenciaId INT,Bloquea BIT);
 INSERT @B EXEC dbo.sp_RevisarEliminacionCuenta @UsuarioId;
 IF EXISTS(SELECT 1 FROM @B WHERE Bloquea=1) BEGIN ROLLBACK; SELECT 'BLOQUEOS_PENDIENTES' Resultado; RETURN; END;
 IF EXISTS(SELECT 1 FROM dbo.ContratoDocumentos d JOIN dbo.Contratos c ON c.Id=d.ContratoId WHERE @UsuarioId IN(c.ClienteId,c.CuidadorId) AND d.EnBoveda=0)
 BEGIN ROLLBACK; SELECT 'BOVEDA_PENDIENTE' Resultado; RETURN; END;
 INSERT dbo.ArchivosEliminacionCuenta(UsuarioId,Url)
 SELECT @UsuarioId,UrlArchivo FROM dbo.DocumentosCuidador WHERE CuidadorId=@UsuarioId AND UrlArchivo IS NOT NULL
 UNION SELECT @UsuarioId,FotoUrl FROM dbo.Usuarios WHERE Id=@UsuarioId AND FotoUrl IS NOT NULL
 UNION SELECT @UsuarioId,ComprobanteUrl FROM dbo.DomiciliosContratacion WHERE ClienteId=@UsuarioId
 UNION SELECT @UsuarioId,CedulaUrl FROM dbo.PermisoContratacion WHERE ClienteId=@UsuarioId AND CedulaUrl IS NOT NULL
 UNION SELECT @UsuarioId,ComprobanteUrl FROM dbo.DeudasCliente WHERE ClienteId=@UsuarioId AND ComprobanteUrl IS NOT NULL;
 DELETE dbo.DocumentosCuidador WHERE CuidadorId=@UsuarioId;
 DELETE dbo.UbicacionesHistorial WHERE CuidadorId=@UsuarioId;
 DELETE dbo.DomiciliosContratacion WHERE ClienteId=@UsuarioId;
 -- Contratos.UbicacionId es NOT NULL y FK: conservar solo las filas referenciadas, SIN direcciÃ³n/GPS.
 DELETE ub FROM dbo.UbicacionesCliente ub WHERE ClienteId=@UsuarioId
 AND NOT EXISTS(SELECT 1 FROM dbo.Contratos c WHERE c.UbicacionId=ub.Id);
 UPDATE dbo.UbicacionesCliente SET Nombre=N'UbicaciÃ³n eliminada',Direccion=N'Eliminada',Latitud=0,Longitud=0 WHERE ClienteId=@UsuarioId;
 UPDATE dbo.Usuarios SET NombreCompleto=N'Usuario eliminado',Email=CONCAT('eliminado+',Id,'@cuidapp.invalid'),
 Telefono='',DocumentoIdentidad=CONCAT('ELIM-',Id),FotoUrl=NULL,PasswordHash=@PasswordHash,IsActive=0 WHERE Id=@UsuarioId;
 UPDATE dbo.PerfilCliente SET DireccionPrincipal=NULL,ContactoEmergenciaNombre=NULL,ContactoEmergenciaTelefono=NULL WHERE UsuarioId=@UsuarioId;
 UPDATE dbo.PerfilCuidador SET Disponible=0,HorarioAutomatico=0,Latitud=NULL,Longitud=NULL,Bio=NULL,MetodoCobro=NULL WHERE UsuarioId=@UsuarioId;
 UPDATE dbo.PermisoContratacion SET CedulaUrl=NULL,Referencia=NULL,Motivo=N'Usuario eliminado',ComentarioAdmin=NULL WHERE ClienteId=@UsuarioId;
 UPDATE dbo.DeudasCliente SET ComprobanteUrl=NULL,NotaCliente=NULL,NotaAdmin=NULL WHERE ClienteId=@UsuarioId;
 -- Conserva los registros histÃ³ricos; retira los datos personales estructurados que no son el PDF firmado.
 UPDATE dbo.Trabajos SET Direccion=N'Eliminada',Latitud=NULL,Longitud=NULL,Notas=NULL WHERE ClienteId=@UsuarioId;
 UPDATE dbo.ContratoFirmas SET Correo=NULL,Ip=NULL,Latitud=NULL,Longitud=NULL WHERE UsuarioId=@UsuarioId;
 DELETE dbo.CodigosEliminacionCuenta WHERE UsuarioId=@UsuarioId;
 DELETE dbo.ContratoCodigos WHERE UsuarioId=@UsuarioId;
 DELETE dbo.ContratoCodigoFallos WHERE UsuarioId=@UsuarioId;
 IF OBJECT_ID('dbo.SeguimientosCompartidos') IS NOT NULL
  EXEC sp_executesql N'DELETE dbo.SeguimientosCompartidos WHERE ClienteId=@U',N'@U int',@UsuarioId;
 -- Tablas de recuperaciÃ³n/tokens previas no estÃ¡n versionadas en este repo: resolver su FK/columna real.
 DECLARE @Sql NVARCHAR(MAX)=N'';
 SELECT @Sql+=N'DELETE FROM '+QUOTENAME(SCHEMA_NAME(t.schema_id))+N'.'+QUOTENAME(t.name)+
 N' WHERE '+QUOTENAME(c.name)+N'=@U;'
 FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
 WHERE (t.name LIKE '%PasswordReset%' OR t.name LIKE '%RefreshToken%' OR t.name LIKE '%DeviceToken%' OR t.name LIKE '%PushToken%')
 AND c.name IN('UsuarioId','UserId');
 IF LEN(@Sql)>0 EXEC sp_executesql @Sql,N'@U int',@UsuarioId;
 -- PROVISIONAL: conservar pruebas y PDF firmados Ã­ntegros; retirar PII del perfil y ubicaciones.
 -- Los textos libres histÃ³ricos pueden contener PII: requieren revisiÃ³n del equipo antes de una polÃ­tica definitiva.
 UPDATE dbo.SolicitudesEliminacionCuenta SET Estado=3,Motivo=NULL,NotaAdmin=NULL WHERE Id=@Solicitud;
 COMMIT; SELECT 'OK' Resultado;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_DeudasAvisos @Ahora DATETIME2,@Id INT=NULL,@Tipo VARCHAR(30)=NULL AS
BEGIN
 SET NOCOUNT ON;
 IF @Id IS NOT NULL
 BEGIN
  UPDATE dbo.DeudasCliente SET
   AvisoRecordatorioEnviado=IIF(@Tipo='deuda_recordatorio',1,AvisoRecordatorioEnviado),
   AvisoVencidaEnviado=IIF(@Tipo='deuda_vencida',1,AvisoVencidaEnviado)
  WHERE Id=@Id AND Estado IN(1,3);
  RETURN;
 END;
 SELECT TOP(1) Id,ClienteId,TrabajoId,Monto,FechaLimite,
 IIF(@Ahora>FechaLimite,'deuda_vencida','deuda_recordatorio') Tipo
 FROM dbo.DeudasCliente WITH(UPDLOCK,READPAST,READCOMMITTEDLOCK) WHERE Estado IN(1,3) AND
 ((@Ahora>FechaLimite AND AvisoVencidaEnviado=0) OR
 (@Ahora>=DATEADD(HOUR,-2,FechaLimite) AND @Ahora<=FechaLimite AND AvisoRecordatorioEnviado=0));
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_ContratoDocumento @DocumentoId INT AS
BEGIN
 SET NOCOUNT ON;
 SELECT d.*,c.ClienteId,c.CuidadorId FROM dbo.ContratoDocumentos d
 JOIN dbo.Contratos c ON c.Id=d.ContratoId WHERE d.Id=@DocumentoId;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_BovedaDocumentos @UsuarioId INT=NULL AS
BEGIN
 SET NOCOUNT ON;
 SELECT d.* FROM dbo.ContratoDocumentos d JOIN dbo.Contratos c ON c.Id=d.ContratoId
 WHERE @UsuarioId IN(c.ClienteId,c.CuidadorId);
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_BovedaGuardar @DocumentoId INT,@Ruta NVARCHAR(400),@Tamano BIGINT,@Ahora DATETIME2 AS
BEGIN
 SET NOCOUNT ON;
 UPDATE dbo.ContratoDocumentos SET EnBoveda=1,RutaBoveda=@Ruta,FechaBoveda=ISNULL(FechaBoveda,@Ahora),TamanoBytes=@Tamano
 WHERE Id=@DocumentoId AND (EnBoveda=0 OR RutaBoveda=@Ruta);
 IF @@ROWCOUNT<>1 THROW 51063,N'No se pudo registrar el documento en la boveda.',1;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_BovedaLista AS
BEGIN
 SET NOCOUNT ON;
 SELECT d.Id DocumentoId,d.ContratoId,d.Fecha FechaFirma,d.FechaBoveda,
 (SELECT STRING_AGG(CONVERT(VARCHAR(12),s.UsuarioId),',') FROM
 (SELECT DISTINCT UsuarioId FROM dbo.SolicitudesEliminacionCuenta
 WHERE Estado=3 AND UsuarioId IN(c.ClienteId,c.CuidadorId)) s) UsuariosIdEliminados,
 d.TamanoBytes,d.Hash Sha256
 FROM dbo.ContratoDocumentos d JOIN dbo.Contratos c ON c.Id=d.ContratoId WHERE d.EnBoveda=1;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_BovedaAcceso @DocumentoId INT,@AdminId INT,@Ahora DATETIME2,@Motivo NVARCHAR(400),@Ip NVARCHAR(64)=NULL AS
BEGIN
 SET NOCOUNT ON;
 IF LEN(LTRIM(RTRIM(@Motivo)))<10 THROW 51063,N'El motivo debe tener al menos diez caracteres.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Id=@AdminId AND RolId=1 AND NivelAdmin IN(1,6) AND IsActive=1)
 THROW 51063,N'No tienes acceso a la boveda.',1;
 INSERT dbo.BovedaAccesos(DocumentoId,AdminId,Fecha,Motivo,Ip) VALUES(@DocumentoId,@AdminId,@Ahora,@Motivo,@Ip);
END
GO

