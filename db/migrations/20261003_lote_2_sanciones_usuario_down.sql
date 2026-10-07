/*
  LOTE-2 DOWN: SancionesUsuario -> SancionesCuidador (esquema legado).
  NO APLICAR sin autorizacion explicita. Solo valido si SancionesUsuario NO tiene filas:
  el esquema legado no puede representar sanciones cumplidas/revocadas sin perdida.
  El propio script recrea los 4 SP legados (sp_SuspenderCuidador, sp_ReactivarCuidador,
  sp_ObtenerSancionesCuidador y la version original de sp_ObtenerUsuarioPorEmail).
  Hay que desplegar tambien la API anterior. Usuarios.IsActive se conserva NOT NULL.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.SancionesUsuario', N'U') IS NULL
    THROW 50011, 'LOTE-2 DOWN: dbo.SancionesUsuario no existe.', 1;
IF OBJECT_ID(N'dbo.SancionesCuidador', N'U') IS NOT NULL
    THROW 50012, 'LOTE-2 DOWN: dbo.SancionesCuidador ya existe.', 1;
IF EXISTS (SELECT 1 FROM dbo.SancionesUsuario)
    THROW 50013, 'LOTE-2 DOWN: SancionesUsuario contiene filas; reversion con perdida no permitida.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DROP PROCEDURE IF EXISTS dbo.sp_SuspenderUsuario;
    DROP PROCEDURE IF EXISTS dbo.sp_ReactivarUsuario;
    DROP PROCEDURE IF EXISTS dbo.sp_ObtenerSancionesUsuario;
    DROP PROCEDURE IF EXISTS dbo.sp_FinalizarSuspensionTemporalVencida;

    DROP INDEX UX_SancionesUsuario_Usuario_Vigente ON dbo.SancionesUsuario;
    DROP INDEX IX_SancionesUsuario_Usuario_Fecha ON dbo.SancionesUsuario;

    ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT CK_SancionesUsuario_Accion;
    ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT CK_SancionesUsuario_Tipo;
    ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT CK_SancionesUsuario_Estado;
    ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT CK_SancionesUsuario_Motivo;
    ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT CK_SancionesUsuario_Duracion;
    ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT CK_SancionesUsuario_Revocacion;
    ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT FK_SancionesUsuario_AdminRevocador;
    ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT DF_SancionesUsuario_Estado;
    ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT DF_SancionesUsuario_FechaInicioUtc;
    ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT DF_SancionesUsuario_Accion;

    ALTER TABLE dbo.SancionesUsuario DROP COLUMN Tipo, Estado, FechaInicioUtc, FechaFinUtc,
                                                RevocadaPorAdminId, FechaRevocacionUtc;
    ALTER TABLE dbo.SancionesUsuario ALTER COLUMN Motivo nvarchar(500) NULL;
    ALTER TABLE dbo.SancionesUsuario ADD FechaCreacion datetime2 NOT NULL
        CONSTRAINT DF_SancionesCuidador_FechaCreacion DEFAULT (SYSDATETIME());

    EXEC sp_rename N'dbo.PK_SancionesUsuario', N'PK_SancionesCuidador', N'OBJECT';
    EXEC sp_rename N'dbo.FK_SancionesUsuario_Usuario', N'FK_SancionesCuidador_Usuario', N'OBJECT';
    EXEC sp_rename N'dbo.FK_SancionesUsuario_Admin', N'FK_SancionesCuidador_Admin', N'OBJECT';
    EXEC sp_rename N'dbo.SancionesUsuario', N'SancionesCuidador';

    -- Restaurar las definiciones legadas (extraidas de DBCuidappDev el 2026-09-27).
    -- sp_ObtenerUsuarioPorEmail se revierte a la version sin columnas de sancion.
    EXEC (N'CREATE OR ALTER PROCEDURE dbo.sp_ObtenerUsuarioPorEmail
    @Email NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.Id, u.Email, u.PasswordHash, u.NombreCompleto, u.FotoUrl, u.RolId, u.IsActive, u.FechaCreacion, p.EstadoAprobacion
    FROM Usuarios u
    LEFT JOIN PerfilCuidador p ON p.UsuarioId = u.Id
    WHERE u.Email = @Email;
END;');

    EXEC (N'CREATE OR ALTER PROCEDURE dbo.sp_SuspenderCuidador
    @UsuarioId INT, @AdminId INT, @Motivo NVARCHAR(500), @FechaHora DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Usuarios SET IsActive = 0 WHERE Id = @UsuarioId;
    INSERT INTO SancionesCuidador (UsuarioId, AdminId, Accion, Motivo, FechaCreacion)
    VALUES (@UsuarioId, @AdminId, ''Suspendido'', @Motivo, @FechaHora);
    SELECT @@ROWCOUNT AS FilasAfectadas;
END');

    EXEC (N'CREATE OR ALTER PROCEDURE dbo.sp_ReactivarCuidador
    @UsuarioId INT, @AdminId INT, @FechaHora DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Usuarios SET IsActive = 1 WHERE Id = @UsuarioId;
    INSERT INTO SancionesCuidador (UsuarioId, AdminId, Accion, Motivo, FechaCreacion)
    VALUES (@UsuarioId, @AdminId, ''Reactivado'', NULL, @FechaHora);
    SELECT @@ROWCOUNT AS FilasAfectadas;
END');

    EXEC (N'CREATE OR ALTER PROCEDURE dbo.sp_ObtenerSancionesCuidador
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.Id, s.Accion, s.Motivo, s.FechaCreacion, a.NombreCompleto AS AdminNombre
    FROM SancionesCuidador s
    INNER JOIN Usuarios a ON a.Id = s.AdminId
    WHERE s.UsuarioId = @UsuarioId
    ORDER BY s.FechaCreacion DESC;
END');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
