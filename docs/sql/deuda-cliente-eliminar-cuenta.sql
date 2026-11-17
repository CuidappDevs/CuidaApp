-- Aplicar manualmente después de metodo-pago-saldo.sql, contratos-fijos/parciales-piloto
-- y la migración de SancionesUsuario. NO se ejecuta desde la API.
-- REGLAS PROVISIONALES hasta decisión del equipo: deuda por tarifa sin propinas futuras,
-- bloqueo con estados 1/3, anonimización a 30 días y cobros pendientes solo como aviso.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF OBJECT_ID('dbo.DeudasCliente') IS NULL
BEGIN
 CREATE TABLE dbo.DeudasCliente (
 Id INT IDENTITY PRIMARY KEY, ClienteId INT NOT NULL REFERENCES dbo.Usuarios(Id),
 TrabajoId INT NOT NULL REFERENCES dbo.Trabajos(Id), CuidadorId INT NOT NULL REFERENCES dbo.Usuarios(Id),
 MontoTotal DECIMAL(10,2) NOT NULL, MontoRecibido DECIMAL(10,2) NOT NULL, Monto DECIMAL(10,2) NOT NULL,
 Estado TINYINT NOT NULL DEFAULT 1 CHECK (Estado IN (1,2,3,4)),
 ComprobanteUrl NVARCHAR(1000) NULL, NotaCliente NVARCHAR(1000) NULL, NotaAdmin NVARCHAR(1000) NULL,
 FechaCreacion DATETIME2 NOT NULL, FechaResolucion DATETIME2 NULL, AdminId INT NULL,
 CONSTRAINT CK_DeudasCliente_Montos CHECK (MontoTotal>=0 AND MontoRecibido BETWEEN 0 AND MontoTotal AND Monto>=0),
 CONSTRAINT UX_DeudasCliente_Trabajo UNIQUE(TrabajoId));
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.DeudasCliente') AND name='IX_DeudasCliente_ClienteEstado')
 CREATE INDEX IX_DeudasCliente_ClienteEstado ON dbo.DeudasCliente(ClienteId,Estado);
GO
-- Envuelve el SP instalado para conservar todas sus validaciones del PIN y horario.
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
 INSERT dbo.DeudasCliente(ClienteId,TrabajoId,CuidadorId,MontoTotal,MontoRecibido,Monto,FechaCreacion)
 VALUES(@Cliente,@TrabajoId,@Cuidador,@Total,@MontoRecibido,@Total-@MontoRecibido,@Ahora);
 SET @DeudaCreada=1;
 END;
 COMMIT;
 SELECT FilasAfectadas,Motivo,@DeudaCreada DeudaCreada FROM @R;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_DeudasClienteLeer @ClienteId INT=NULL,@TrabajoId INT=NULL,@Estado TINYINT=NULL AS
BEGIN
 SET NOCOUNT ON;
 SELECT d.Id,d.TrabajoId,t.Fecha FechaServicio,u.NombreCompleto NombreCuidador,
 d.MontoTotal,d.MontoRecibido,d.Monto,d.Estado,d.ComprobanteUrl,d.NotaAdmin,d.FechaCreacion,
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
 IF @Accion='confirmar-pago'
 BEGIN
 -- PROVISIONAL: crédito íntegro al saldo; negativo = a favor de la cuidadora.
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
-- Protege incluso llamadas directas a SP existentes, sin copiar sus implementaciones.
CREATE OR ALTER TRIGGER dbo.tr_DeudaCliente_Trabajos ON dbo.Trabajos AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @Monto DECIMAL(18,2)=(SELECT SUM(d.Monto) FROM dbo.DeudasCliente d
 WHERE d.Estado IN(1,3) AND EXISTS(SELECT 1 FROM inserted i WHERE i.ClienteId=d.ClienteId));
 IF @Monto>0 BEGIN DECLARE @Error NVARCHAR(200)=CONCAT('DEUDA_PENDIENTE|',@Monto); THROW 51061,@Error,1; END;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) ON u.Id IN(i.ClienteId,i.CuidadorId) WHERE u.IsActive=0)
 THROW 51062,N'La cuenta no está activa.',1;
END
GO
CREATE OR ALTER TRIGGER dbo.tr_DeudaCliente_Contratos ON dbo.Contratos AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @Monto DECIMAL(18,2)=(SELECT SUM(d.Monto) FROM dbo.DeudasCliente d
 WHERE d.Estado IN(1,3) AND EXISTS(SELECT 1 FROM inserted i WHERE i.ClienteId=d.ClienteId));
 IF @Monto>0 BEGIN DECLARE @Error NVARCHAR(200)=CONCAT('DEUDA_PENDIENTE|',@Monto); THROW 51061,@Error,1; END;
 IF EXISTS(SELECT 1 FROM inserted i JOIN dbo.Usuarios u WITH(UPDLOCK,HOLDLOCK) ON u.Id IN(i.ClienteId,i.CuidadorId) WHERE u.IsActive=0)
 THROW 51062,N'La cuenta no está activa.',1;
END
GO
IF OBJECT_ID('dbo.SolicitudesEliminacionCuenta') IS NULL
BEGIN
 CREATE TABLE dbo.SolicitudesEliminacionCuenta(
 Id INT IDENTITY PRIMARY KEY,UsuarioId INT NOT NULL REFERENCES dbo.Usuarios(Id),Rol INT NOT NULL,
 Motivo NVARCHAR(1000) NULL,Estado TINYINT NOT NULL CHECK(Estado IN(1,2,3)),
 FechaSolicitud DATETIME2 NOT NULL,FechaEjecucion DATETIME2 NOT NULL,FechaCancelacion DATETIME2 NULL,
 CanceladaPor VARCHAR(10) NULL CHECK(CanceladaPor IN('usuario','admin')),
 Origen VARCHAR(3) NOT NULL CHECK(Origen IN('app','web')),IsActiveAnterior BIT NOT NULL,NotaAdmin NVARCHAR(1000) NULL);
 CREATE UNIQUE INDEX UX_EliminacionProgramada ON dbo.SolicitudesEliminacionCuenta(UsuarioId) WHERE Estado=1;
 CREATE INDEX IX_EliminacionesVencidas ON dbo.SolicitudesEliminacionCuenta(Estado,FechaEjecucion);
END
IF OBJECT_ID('dbo.CodigosEliminacionCuenta') IS NULL
 CREATE TABLE dbo.CodigosEliminacionCuenta(UsuarioId INT PRIMARY KEY REFERENCES dbo.Usuarios(Id),
 CodigoHash VARCHAR(64) NOT NULL,Expira DATETIME2 NOT NULL,Intentos INT NOT NULL DEFAULT 0,
 FechaEnvio DATETIME2 NOT NULL,Origen VARCHAR(3) NOT NULL);
-- Cola duradera: los errores de disco se reintentan incluso tras reiniciar el proceso.
IF OBJECT_ID('dbo.ArchivosEliminacionCuenta') IS NULL
 CREATE TABLE dbo.ArchivosEliminacionCuenta(Id BIGINT IDENTITY PRIMARY KEY,UsuarioId INT NOT NULL,Url NVARCHAR(1000) NOT NULL);
GO
CREATE OR ALTER PROCEDURE dbo.sp_RevisarEliminacionCuenta @UsuarioId INT AS
BEGIN
 SET NOCOUNT ON;
 SELECT CAST('DISPUTA_ABIERTA' AS VARCHAR(40)) Codigo,CAST(N'Tienes una disputa abierta.' AS NVARCHAR(300)) Detalle,d.Id ReferenciaId,CAST(1 AS BIT) Bloquea
 FROM dbo.Disputas d JOIN dbo.Trabajos t ON t.Id=d.TrabajoId
 WHERE d.Estado=1 AND @UsuarioId IN(t.ClienteId,t.CuidadorId)
 UNION ALL SELECT 'DISPUTA_ABIERTA',N'Tienes un servicio con pago disputado.',t.Id,1 FROM dbo.Trabajos t
 WHERE @UsuarioId IN(t.ClienteId,t.CuidadorId) AND t.PagoDisputado=1
 AND NOT EXISTS(SELECT 1 FROM dbo.Disputas d WHERE d.TrabajoId=t.Id)
 UNION ALL SELECT 'SERVICIO_ACTIVO',N'Tienes un servicio pendiente o activo.',Id,1 FROM dbo.Trabajos
 WHERE Estado IN(1,2,3,7) AND @UsuarioId IN(ClienteId,CuidadorId)
 UNION ALL SELECT 'CONTRATO_ACTIVO',N'Tienes un contrato pendiente o activo.',Id,1 FROM dbo.Contratos
 WHERE Estado BETWEEN 1 AND 5 AND @UsuarioId IN(ClienteId,CuidadorId)
 UNION ALL SELECT 'DEUDA_CLIENTE',N'Tienes una deuda pendiente o en revisión.',Id,1 FROM dbo.DeudasCliente WHERE ClienteId=@UsuarioId AND Estado IN(1,3)
 UNION ALL SELECT 'DEUDA_CUIDADOR',N'Tienes comisiones pendientes de pagar.',@UsuarioId,1
 WHERE EXISTS(SELECT 1 FROM dbo.Usuarios WHERE Id=@UsuarioId AND RolId=3) AND dbo.fn_SaldoCuidador(@UsuarioId)>0
 UNION ALL SELECT 'PAGO_POR_COBRAR',N'Tienes un pago pendiente de cobrar. Contacta con soporte.',Id,0
 FROM dbo.Pagos WHERE CuidadorId=@UsuarioId AND Estado IN(1,3);
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_EliminacionRevisar @UsuarioId INT AS
BEGIN
 SET NOCOUNT ON;
 SELECT Id UsuarioId FROM dbo.Usuarios WHERE Id=@UsuarioId AND RolId IN(2,3)
 AND NOT EXISTS(SELECT 1 FROM dbo.SolicitudesEliminacionCuenta s WHERE s.UsuarioId=@UsuarioId AND s.Estado=3);
 EXEC dbo.sp_RevisarEliminacionCuenta @UsuarioId;
 SELECT FechaEjecucion FROM dbo.SolicitudesEliminacionCuenta WHERE UsuarioId=@UsuarioId AND Estado=1;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_EliminacionCredenciales @Correo NVARCHAR(150),@PasswordHash VARCHAR(64) AS
BEGIN
 SET NOCOUNT ON;
 SELECT Id UsuarioId FROM dbo.Usuarios WHERE Email=@Correo AND PasswordHash=@PasswordHash AND RolId IN(2,3)
 AND NOT EXISTS(SELECT 1 FROM dbo.SolicitudesEliminacionCuenta s WHERE s.UsuarioId=Usuarios.Id AND s.Estado=3);
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_EliminacionLogin @UsuarioId INT AS
BEGIN
 SET NOCOUNT ON;
 SELECT FechaEjecucion FROM dbo.SolicitudesEliminacionCuenta WHERE UsuarioId=@UsuarioId AND Estado=1;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_EliminacionCodigo @UsuarioId INT,@CodigoHash VARCHAR(64),@Ahora DATETIME2,@Origen VARCHAR(3) AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @Rol INT;
 SELECT @Rol=RolId FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE Id=@UsuarioId;
 IF ISNULL(@Rol,0) NOT IN(2,3) BEGIN ROLLBACK; SELECT 'NO_ENCONTRADO' Resultado; RETURN; END;
 IF EXISTS(SELECT 1 FROM dbo.SolicitudesEliminacionCuenta WHERE UsuarioId=@UsuarioId AND Estado IN(1,3))
 BEGIN ROLLBACK; SELECT 'SOLICITUD_EXISTENTE' Resultado; RETURN; END;
 DECLARE @B TABLE(Codigo VARCHAR(40),Detalle NVARCHAR(300),ReferenciaId INT,Bloquea BIT);
 INSERT @B EXEC dbo.sp_RevisarEliminacionCuenta @UsuarioId;
 IF EXISTS(SELECT 1 FROM @B WHERE Bloquea=1) BEGIN ROLLBACK; SELECT 'BLOQUEOS_PENDIENTES' Resultado; RETURN; END;
 IF EXISTS(SELECT 1 FROM dbo.CodigosEliminacionCuenta WHERE UsuarioId=@UsuarioId AND FechaEnvio>DATEADD(SECOND,-60,@Ahora))
 BEGIN ROLLBACK; SELECT 'ESPERA_CODIGO' Resultado; RETURN; END;
 DELETE dbo.CodigosEliminacionCuenta WHERE UsuarioId=@UsuarioId;
 INSERT dbo.CodigosEliminacionCuenta(UsuarioId,CodigoHash,Expira,FechaEnvio,Origen) VALUES(@UsuarioId,@CodigoHash,DATEADD(MINUTE,10,@Ahora),@Ahora,@Origen);
 COMMIT; SELECT 'OK' Resultado,Email FROM dbo.Usuarios WHERE Id=@UsuarioId;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_EliminacionConfirmar @UsuarioId INT,@CodigoHash VARCHAR(64),@Motivo NVARCHAR(1000)=NULL,@Origen VARCHAR(3),@Ahora DATETIME2 AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 DECLARE @Rol INT,@Activo BIT;
 SELECT @Rol=RolId,@Activo=IsActive FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE Id=@UsuarioId;
 IF ISNULL(@Rol,0) NOT IN(2,3) BEGIN ROLLBACK; SELECT 'NO_ENCONTRADO' Resultado; RETURN; END;
 IF EXISTS(SELECT 1 FROM dbo.SolicitudesEliminacionCuenta WHERE UsuarioId=@UsuarioId AND Estado IN(1,3))
 BEGIN ROLLBACK; SELECT 'SOLICITUD_EXISTENTE' Resultado; RETURN; END;
 DECLARE @B TABLE(Codigo VARCHAR(40),Detalle NVARCHAR(300),ReferenciaId INT,Bloquea BIT);
 INSERT @B EXEC dbo.sp_RevisarEliminacionCuenta @UsuarioId;
 IF EXISTS(SELECT 1 FROM @B WHERE Bloquea=1) BEGIN ROLLBACK; SELECT 'BLOQUEOS_PENDIENTES' Resultado; RETURN; END;
 DECLARE @Hash VARCHAR(64),@Expira DATETIME2,@Intentos INT,@OrigenCodigo VARCHAR(3);
 SELECT @Hash=CodigoHash,@Expira=Expira,@Intentos=Intentos,@OrigenCodigo=Origen FROM dbo.CodigosEliminacionCuenta WITH(UPDLOCK,HOLDLOCK) WHERE UsuarioId=@UsuarioId;
 IF @Hash IS NULL OR @Expira<=@Ahora OR @Intentos>=5 OR @OrigenCodigo<>@Origen
 BEGIN ROLLBACK; SELECT 'CODIGO_INVALIDO' Resultado; RETURN; END;
 IF @Hash<>@CodigoHash
 BEGIN UPDATE dbo.CodigosEliminacionCuenta SET Intentos=Intentos+1 WHERE UsuarioId=@UsuarioId; COMMIT; SELECT 'CODIGO_INVALIDO' Resultado; RETURN; END;
 INSERT dbo.SolicitudesEliminacionCuenta(UsuarioId,Rol,Motivo,Estado,FechaSolicitud,FechaEjecucion,Origen,IsActiveAnterior)
 VALUES(@UsuarioId,@Rol,@Motivo,1,@Ahora,DATEADD(DAY,30,@Ahora),@Origen,@Activo);
 DELETE dbo.CodigosEliminacionCuenta WHERE UsuarioId=@UsuarioId;
 UPDATE dbo.Usuarios SET IsActive=0 WHERE Id=@UsuarioId;
 UPDATE dbo.PerfilCuidador SET Disponible=0,HorarioAutomatico=0,Latitud=NULL,Longitud=NULL WHERE UsuarioId=@UsuarioId;
 COMMIT; SELECT 'OK' Resultado,DATEADD(DAY,30,@Ahora) FechaEjecucion;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_EliminacionCancelar @UsuarioId INT=NULL,@Id INT=NULL,@Por VARCHAR(10),@Nota NVARCHAR(1000)=NULL,@Ahora DATETIME2 AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY
 BEGIN TRAN;
 IF @UsuarioId IS NULL SELECT @UsuarioId=UsuarioId FROM dbo.SolicitudesEliminacionCuenta WHERE Id=@Id;
 DECLARE @Activo BIT,@Solicitud INT,@Lock INT;
 SELECT @Lock=Id FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE Id=@UsuarioId;
 SELECT @Solicitud=Id,@Activo=IsActiveAnterior FROM dbo.SolicitudesEliminacionCuenta WITH(UPDLOCK,HOLDLOCK)
 WHERE UsuarioId=@UsuarioId AND Estado=1 AND (@Id IS NULL OR Id=@Id);
 IF @Solicitud IS NULL BEGIN ROLLBACK; SELECT 'NO_ENCONTRADO' Resultado; RETURN; END;
 UPDATE dbo.SolicitudesEliminacionCuenta SET Estado=2,FechaCancelacion=@Ahora,CanceladaPor=@Por,NotaAdmin=IIF(@Por='admin',@Nota,NULL) WHERE Id=@Solicitud;
 -- No levanta sanciones ni vuelve a habilitar disponibilidad/horario automáticamente.
 UPDATE dbo.Usuarios SET IsActive=IIF(EXISTS(SELECT 1 FROM dbo.SancionesUsuario WHERE UsuarioId=@UsuarioId AND Estado='VIGENTE'),0,@Activo) WHERE Id=@UsuarioId;
 DELETE dbo.CodigosEliminacionCuenta WHERE UsuarioId=@UsuarioId;
 COMMIT; SELECT 'OK' Resultado,@UsuarioId UsuarioId;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_AdminEliminaciones @Estado TINYINT=NULL,@UsuarioId INT=NULL AS
BEGIN
 SET NOCOUNT ON;
 SELECT s.Id,s.UsuarioId,s.Rol,u.NombreCompleto,u.Email,s.Motivo,s.Estado,s.FechaSolicitud,s.FechaEjecucion,s.FechaCancelacion,s.CanceladaPor,s.Origen,s.NotaAdmin
 FROM dbo.SolicitudesEliminacionCuenta s JOIN dbo.Usuarios u ON u.Id=s.UsuarioId
 WHERE (@Estado IS NULL OR s.Estado=@Estado) AND (@UsuarioId IS NULL OR s.UsuarioId=@UsuarioId)
 ORDER BY s.FechaSolicitud DESC,s.Id DESC;
END
GO
-- Mantiene fuera de búsquedas/mapa incluso si un cliente antiguo intenta reactivar GPS/horario.
CREATE OR ALTER TRIGGER dbo.tr_Eliminacion_Perfil ON dbo.PerfilCuidador AFTER INSERT,UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF TRIGGER_NESTLEVEL(OBJECT_ID('dbo.tr_Eliminacion_Perfil'))>1 RETURN;
 UPDATE p SET Disponible=0,HorarioAutomatico=0,Latitud=NULL,Longitud=NULL
 FROM dbo.PerfilCuidador p JOIN inserted i ON i.UsuarioId=p.UsuarioId
 WHERE EXISTS(SELECT 1 FROM dbo.SolicitudesEliminacionCuenta s WHERE s.UsuarioId=p.UsuarioId AND s.Estado IN(1,3));
END
GO
CREATE OR ALTER TRIGGER dbo.tr_Eliminacion_Usuarios ON dbo.Usuarios AFTER UPDATE AS
BEGIN
 SET NOCOUNT ON;
 IF TRIGGER_NESTLEVEL(OBJECT_ID('dbo.tr_Eliminacion_Usuarios'))>1 RETURN;
 UPDATE u SET IsActive=0 FROM dbo.Usuarios u JOIN inserted i ON i.Id=u.Id
 WHERE u.IsActive=1 AND EXISTS(SELECT 1 FROM dbo.SolicitudesEliminacionCuenta s WHERE s.UsuarioId=u.Id AND s.Estado IN(1,3));
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_EliminacionesVencidas @Ahora DATETIME2 AS
BEGIN
 SET NOCOUNT ON;
 SELECT UsuarioId FROM dbo.SolicitudesEliminacionCuenta WHERE Estado=1 AND FechaEjecucion<=@Ahora;
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
 -- Revisión final: si surgió una obligación durante el plazo, se pospone la anonimización.
 DECLARE @B TABLE(Codigo VARCHAR(40),Detalle NVARCHAR(300),ReferenciaId INT,Bloquea BIT);
 INSERT @B EXEC dbo.sp_RevisarEliminacionCuenta @UsuarioId;
 IF EXISTS(SELECT 1 FROM @B WHERE Bloquea=1) BEGIN ROLLBACK; SELECT 'BLOQUEOS_PENDIENTES' Resultado; RETURN; END;
 INSERT dbo.ArchivosEliminacionCuenta(UsuarioId,Url)
 SELECT @UsuarioId,UrlArchivo FROM dbo.DocumentosCuidador WHERE CuidadorId=@UsuarioId AND UrlArchivo IS NOT NULL
 UNION SELECT @UsuarioId,FotoUrl FROM dbo.Usuarios WHERE Id=@UsuarioId AND FotoUrl IS NOT NULL
 UNION SELECT @UsuarioId,ComprobanteUrl FROM dbo.DomiciliosContratacion WHERE ClienteId=@UsuarioId
 UNION SELECT @UsuarioId,CedulaUrl FROM dbo.PermisoContratacion WHERE ClienteId=@UsuarioId AND CedulaUrl IS NOT NULL
 UNION SELECT @UsuarioId,ComprobanteUrl FROM dbo.DeudasCliente WHERE ClienteId=@UsuarioId AND ComprobanteUrl IS NOT NULL;
 DELETE dbo.DocumentosCuidador WHERE CuidadorId=@UsuarioId;
 DELETE dbo.UbicacionesHistorial WHERE CuidadorId=@UsuarioId;
 DELETE dbo.DomiciliosContratacion WHERE ClienteId=@UsuarioId;
 -- Contratos.UbicacionId es NOT NULL y FK: conservar solo las filas referenciadas, SIN dirección/GPS.
 DELETE ub FROM dbo.UbicacionesCliente ub WHERE ClienteId=@UsuarioId
 AND NOT EXISTS(SELECT 1 FROM dbo.Contratos c WHERE c.UbicacionId=ub.Id);
 UPDATE dbo.UbicacionesCliente SET Nombre=N'Ubicación eliminada',Direccion=N'Eliminada',Latitud=0,Longitud=0 WHERE ClienteId=@UsuarioId;
 UPDATE dbo.Usuarios SET NombreCompleto=N'Usuario eliminado',Email=CONCAT('eliminado+',Id,'@cuidapp.invalid'),
 Telefono='',DocumentoIdentidad=CONCAT('ELIM-',Id),FotoUrl=NULL,PasswordHash=@PasswordHash,IsActive=0 WHERE Id=@UsuarioId;
 UPDATE dbo.PerfilCliente SET DireccionPrincipal=NULL,ContactoEmergenciaNombre=NULL,ContactoEmergenciaTelefono=NULL WHERE UsuarioId=@UsuarioId;
 UPDATE dbo.PerfilCuidador SET Disponible=0,HorarioAutomatico=0,Latitud=NULL,Longitud=NULL,Bio=NULL,MetodoCobro=NULL WHERE UsuarioId=@UsuarioId;
 UPDATE dbo.PermisoContratacion SET CedulaUrl=NULL,Referencia=NULL,Motivo=N'Usuario eliminado',ComentarioAdmin=NULL WHERE ClienteId=@UsuarioId;
 UPDATE dbo.DeudasCliente SET ComprobanteUrl=NULL,NotaCliente=NULL,NotaAdmin=NULL WHERE ClienteId=@UsuarioId;
 -- Conserva los registros históricos; retira los datos personales estructurados que no son el PDF firmado.
 UPDATE dbo.Trabajos SET Direccion=N'Eliminada',Latitud=NULL,Longitud=NULL,Notas=NULL WHERE ClienteId=@UsuarioId;
 UPDATE dbo.ContratoFirmas SET Correo=NULL,Ip=NULL,Latitud=NULL,Longitud=NULL WHERE UsuarioId=@UsuarioId;
 DELETE dbo.CodigosEliminacionCuenta WHERE UsuarioId=@UsuarioId;
 DELETE dbo.ContratoCodigos WHERE UsuarioId=@UsuarioId;
 DELETE dbo.ContratoCodigoFallos WHERE UsuarioId=@UsuarioId;
 IF OBJECT_ID('dbo.SeguimientosCompartidos') IS NOT NULL
  EXEC sp_executesql N'DELETE dbo.SeguimientosCompartidos WHERE ClienteId=@U',N'@U int',@UsuarioId;
 -- Tablas de recuperación/tokens previas no están versionadas en este repo: resolver su FK/columna real.
 DECLARE @Sql NVARCHAR(MAX)=N'';
 SELECT @Sql+=N'DELETE FROM '+QUOTENAME(SCHEMA_NAME(t.schema_id))+N'.'+QUOTENAME(t.name)+
 N' WHERE '+QUOTENAME(c.name)+N'=@U;'
 FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id
 WHERE (t.name LIKE '%PasswordReset%' OR t.name LIKE '%RefreshToken%' OR t.name LIKE '%DeviceToken%' OR t.name LIKE '%PushToken%')
 AND c.name IN('UsuarioId','UserId');
 IF LEN(@Sql)>0 EXEC sp_executesql @Sql,N'@U int',@UsuarioId;
 -- PROVISIONAL: conservar pruebas y PDF firmados íntegros; retirar PII del perfil y ubicaciones.
 -- Los textos libres históricos pueden contener PII: requieren revisión del equipo antes de una política definitiva.
 UPDATE dbo.SolicitudesEliminacionCuenta SET Estado=3,Motivo=NULL,NotaAdmin=NULL WHERE Id=@Solicitud;
 COMMIT; SELECT 'OK' Resultado;
 END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_EliminacionArchivos @Id BIGINT=NULL AS
BEGIN
 SET NOCOUNT ON;
 IF @Id IS NOT NULL DELETE dbo.ArchivosEliminacionCuenta WHERE Id=@Id;
 ELSE SELECT TOP 500 Id,Url FROM dbo.ArchivosEliminacionCuenta ORDER BY Id;
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_EliminacionTokenValido @UsuarioId INT,@Correo NVARCHAR(150) AS
BEGIN
 SET NOCOUNT ON;
 SELECT IIF(EXISTS(SELECT 1 FROM dbo.Usuarios u WHERE Id=@UsuarioId AND Email=@Correo AND IsActive=1
 AND NOT EXISTS(SELECT 1 FROM dbo.SolicitudesEliminacionCuenta s WHERE s.UsuarioId=u.Id AND s.Estado IN(1,3))),1,0);
END
GO
CREATE OR ALTER PROCEDURE dbo.sp_EliminacionCodigoRevocar @UsuarioId INT,@CodigoHash VARCHAR(64) AS
BEGIN
 SET NOCOUNT ON;
 DELETE dbo.CodigosEliminacionCuenta WHERE UsuarioId=@UsuarioId AND CodigoHash=@CodigoHash;
END
GO
