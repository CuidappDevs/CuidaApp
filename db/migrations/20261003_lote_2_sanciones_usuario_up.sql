/*
  LOTE-2 UP: SancionesCuidador -> SancionesUsuario (ciclo de vida de sanciones).
  NO APLICAR sin autorizacion explicita (Gate G1). Fail-closed: aborta sin DDL si
  la tabla tiene filas, si Usuarios.IsActive tiene NULL o si hay dependencias SQL inesperadas.
  Despues de aplicar este script se despliegan los 5 SP de db/procedures/ y la API (coordinado).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Preflight (sin escrituras)
IF OBJECT_ID(N'dbo.SancionesCuidador', N'U') IS NULL
    THROW 50001, 'LOTE-2: dbo.SancionesCuidador no existe.', 1;
IF OBJECT_ID(N'dbo.SancionesUsuario', N'U') IS NOT NULL
    THROW 50002, 'LOTE-2: dbo.SancionesUsuario ya existe.', 1;
IF EXISTS (SELECT 1 FROM dbo.SancionesCuidador)
    THROW 50003, 'LOTE-2: SancionesCuidador contiene filas; no hay conversion definida.', 1;
IF EXISTS (SELECT 1 FROM dbo.Usuarios WHERE IsActive IS NULL)
    THROW 50004, 'LOTE-2: Usuarios.IsActive contiene NULL; decidir su estado antes de migrar.', 1;
-- Dependencias SQL internas distintas de los SP legados que se reemplazan
IF EXISTS (
    SELECT 1
    FROM sys.sql_expression_dependencies d
    JOIN sys.objects o ON o.object_id = d.referencing_id
    WHERE d.referenced_id = OBJECT_ID(N'dbo.SancionesCuidador')
      AND o.name NOT IN (N'sp_SuspenderCuidador', N'sp_ReactivarCuidador', N'sp_ObtenerSancionesCuidador'))
    THROW 50005, 'LOTE-2: existen dependencias SQL inesperadas sobre SancionesCuidador.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. Usuarios.IsActive NOT NULL (conserva/crea default 1)
    IF NOT EXISTS (
        SELECT 1 FROM sys.default_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Usuarios')
          AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Usuarios'), N'IsActive', 'ColumnId'))
        ALTER TABLE dbo.Usuarios ADD CONSTRAINT DF_Usuarios_IsActive DEFAULT (1) FOR IsActive;
    ALTER TABLE dbo.Usuarios ALTER COLUMN IsActive bit NOT NULL;

    -- 2. Retirar SP legados y renombrar tabla (la tabla esta vacia)
    DROP PROCEDURE IF EXISTS dbo.sp_SuspenderCuidador;
    DROP PROCEDURE IF EXISTS dbo.sp_ReactivarCuidador;
    DROP PROCEDURE IF EXISTS dbo.sp_ObtenerSancionesCuidador;

    EXEC sp_rename N'dbo.SancionesCuidador', N'SancionesUsuario';

    -- 3. Eliminar restricciones heredadas (PK, FK, defaults, checks, indices) con nombres arbitrarios
    DECLARE @sql nvarchar(max) = N'';
    SELECT @sql += N'ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT ' + QUOTENAME(name) + N';'
    FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.SancionesUsuario');
    SELECT @sql += N'ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT ' + QUOTENAME(name) + N';'
    FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.SancionesUsuario');
    SELECT @sql += N'ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT ' + QUOTENAME(name) + N';'
    FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.SancionesUsuario');
    SELECT @sql += N'DROP INDEX ' + QUOTENAME(i.name) + N' ON dbo.SancionesUsuario;'
    FROM sys.indexes i
    WHERE i.object_id = OBJECT_ID(N'dbo.SancionesUsuario') AND i.is_primary_key = 0 AND i.type > 0;
    EXEC sp_executesql @sql;

    SET @sql = N'';
    SELECT @sql += N'ALTER TABLE dbo.SancionesUsuario DROP CONSTRAINT ' + QUOTENAME(name) + N';'
    FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.SancionesUsuario') AND type = 'PK';
    EXEC sp_executesql @sql;

    -- 4. Columnas: retirar las legadas y crear el modelo nuevo (tabla vacia)
    IF COL_LENGTH(N'dbo.SancionesUsuario', N'FechaCreacion') IS NOT NULL
        ALTER TABLE dbo.SancionesUsuario DROP COLUMN FechaCreacion;

    ALTER TABLE dbo.SancionesUsuario ALTER COLUMN UsuarioId int NOT NULL;
    ALTER TABLE dbo.SancionesUsuario ALTER COLUMN AdminId int NOT NULL;
    ALTER TABLE dbo.SancionesUsuario ALTER COLUMN Accion varchar(20) NOT NULL;
    ALTER TABLE dbo.SancionesUsuario ALTER COLUMN Motivo nvarchar(500) NOT NULL;

    ALTER TABLE dbo.SancionesUsuario ADD
        Tipo varchar(12) NOT NULL,
        Estado varchar(20) NOT NULL CONSTRAINT DF_SancionesUsuario_Estado DEFAULT ('VIGENTE'),
        FechaInicioUtc datetime2(7) NOT NULL CONSTRAINT DF_SancionesUsuario_FechaInicioUtc DEFAULT (SYSUTCDATETIME()),
        FechaFinUtc datetime2(7) NULL,
        RevocadaPorAdminId int NULL,
        FechaRevocacionUtc datetime2(7) NULL;

    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT DF_SancionesUsuario_Accion DEFAULT ('SUSPENSION') FOR Accion;
    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT PK_SancionesUsuario PRIMARY KEY CLUSTERED (Id);

    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT FK_SancionesUsuario_Usuario
        FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios (Id);
    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT FK_SancionesUsuario_Admin
        FOREIGN KEY (AdminId) REFERENCES dbo.Usuarios (Id);
    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT FK_SancionesUsuario_AdminRevocador
        FOREIGN KEY (RevocadaPorAdminId) REFERENCES dbo.Usuarios (Id);

    EXEC sp_executesql N'
    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT CK_SancionesUsuario_Accion CHECK (Accion IN (''SUSPENSION''));
    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT CK_SancionesUsuario_Tipo CHECK (Tipo IN (''INDEFINIDA'',''TEMPORAL''));
    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT CK_SancionesUsuario_Estado CHECK (Estado IN (''VIGENTE'',''CUMPLIDA'',''REVOCADA''));
    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT CK_SancionesUsuario_Motivo CHECK (LEN(LTRIM(RTRIM(Motivo))) > 0);
    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT CK_SancionesUsuario_Duracion CHECK (
        ((Tipo = ''TEMPORAL'' AND FechaFinUtc IS NOT NULL AND FechaFinUtc > FechaInicioUtc)
         OR (Tipo = ''INDEFINIDA'' AND FechaFinUtc IS NULL)));
    ALTER TABLE dbo.SancionesUsuario ADD CONSTRAINT CK_SancionesUsuario_Revocacion CHECK (
        (Estado = ''REVOCADA'' AND RevocadaPorAdminId IS NOT NULL AND FechaRevocacionUtc IS NOT NULL)
        OR (Estado <> ''REVOCADA'' AND RevocadaPorAdminId IS NULL AND FechaRevocacionUtc IS NULL));

    CREATE UNIQUE NONCLUSTERED INDEX UX_SancionesUsuario_Usuario_Vigente
        ON dbo.SancionesUsuario (UsuarioId) WHERE Estado = ''VIGENTE'';
    CREATE NONCLUSTERED INDEX IX_SancionesUsuario_Usuario_Fecha
        ON dbo.SancionesUsuario (UsuarioId, FechaInicioUtc DESC)
        INCLUDE (Estado, Tipo, FechaFinUtc, AdminId, RevocadaPorAdminId);';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
