-- 1. Añadir columnas a Usuarios
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Usuarios]') AND name = 'NombreCompleto')
BEGIN
    ALTER TABLE dbo.Usuarios ADD NombreCompleto NVARCHAR(150) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Usuarios]') AND name = 'FotoUrl')
BEGIN
    ALTER TABLE dbo.Usuarios ADD FotoUrl NVARCHAR(500) NULL;
END
GO

-- 2. Añadir columna a PerfilCuidador
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PerfilCuidador]') AND name = 'MetodoCobro')
BEGIN
    ALTER TABLE dbo.PerfilCuidador ADD MetodoCobro NVARCHAR(50) NULL;
END
GO

-- 3. Modificar Stored Procedure sp_CrearUsuarioCliente
CREATE OR ALTER PROCEDURE sp_CrearUsuarioCliente
    @Email NVARCHAR(150),
    @PasswordHash NVARCHAR(255),
    @NombreCompleto NVARCHAR(150),
    @FotoUrl NVARCHAR(500),
    @DireccionPrincipal NVARCHAR(255),
    @ContactoEmergencia NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @NuevoUsuarioId INT;

        INSERT INTO Usuarios (Email, PasswordHash, NombreCompleto, FotoUrl, RolId, IsActive, FechaCreacion)
        VALUES (@Email, @PasswordHash, @NombreCompleto, @FotoUrl, 2, 1, GETDATE());

        SET @NuevoUsuarioId = SCOPE_IDENTITY();

        INSERT INTO PerfilCliente (UsuarioId, DireccionPrincipal, ContactoEmergencia)
        VALUES (@NuevoUsuarioId, @DireccionPrincipal, @ContactoEmergencia);

        COMMIT TRANSACTION;
        SELECT @NuevoUsuarioId AS NuevoId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 4. Modificar Stored Procedure sp_CrearUsuarioCuidador
CREATE OR ALTER PROCEDURE sp_CrearUsuarioCuidador
    @Email NVARCHAR(150),
    @PasswordHash NVARCHAR(255),
    @NombreCompleto NVARCHAR(150),
    @FotoUrl NVARCHAR(500),
    @Especialidad NVARCHAR(100),
    @TarifaHora DECIMAL(10,2),
    @Bio NVARCHAR(500),
    @MetodoCobro NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @NuevoUsuarioId INT;

        INSERT INTO Usuarios (Email, PasswordHash, NombreCompleto, FotoUrl, RolId, IsActive, FechaCreacion)
        VALUES (@Email, @PasswordHash, @NombreCompleto, @FotoUrl, 3, 1, GETDATE());

        SET @NuevoUsuarioId = SCOPE_IDENTITY();

        INSERT INTO PerfilCuidador (UsuarioId, Especialidad, TarifaHora, Bio, MetodoCobro, EstadoAprobacion)
        VALUES (@NuevoUsuarioId, @Especialidad, @TarifaHora, @Bio, @MetodoCobro, 1);

        COMMIT TRANSACTION;
        SELECT @NuevoUsuarioId AS NuevoId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
