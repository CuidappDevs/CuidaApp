-- Aplicar despues de registro-sps.sql, parciales-piloto.sql y login-google.sql.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF COL_LENGTH('dbo.Usuarios', 'TerminosVersion') IS NULL
    ALTER TABLE dbo.Usuarios ADD TerminosVersion NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Usuarios', 'TerminosAceptadosEn') IS NULL
    ALTER TABLE dbo.Usuarios ADD TerminosAceptadosEn DATETIME2 NULL;
GO
CREATE OR ALTER PROCEDURE sp_CrearUsuarioCliente
    @Email NVARCHAR(150),
    @PasswordHash NVARCHAR(255),
    @NombreCompleto NVARCHAR(150),
    @FotoUrl NVARCHAR(500),
    @DireccionPrincipal NVARCHAR(255),
    @ContactoEmergenciaNombre NVARCHAR(150),
    @ContactoEmergenciaTelefono NVARCHAR(50),
    @NacionalidadId INT = NULL,
    @DocumentoIdentidad NVARCHAR(30) = NULL,
    @Telefono NVARCHAR(20) = NULL,
    @TerminosVersion NVARCHAR(20) = NULL,
    @TerminosAceptadosEn DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @NuevoUsuarioId INT;
        INSERT INTO Usuarios (Email, PasswordHash, NombreCompleto, FotoUrl, RolId, IsActive, FechaCreacion, NacionalidadId, DocumentoIdentidad, Telefono, TerminosVersion, TerminosAceptadosEn)
        VALUES (@Email, @PasswordHash, @NombreCompleto, @FotoUrl, 2, 1, GETDATE(), @NacionalidadId, NULLIF(LTRIM(RTRIM(@DocumentoIdentidad)), N''), NULLIF(LTRIM(RTRIM(@Telefono)), N''), @TerminosVersion, CASE WHEN @TerminosVersion IS NOT NULL THEN COALESCE(@TerminosAceptadosEn, CONVERT(DATETIME2, SWITCHOFFSET(SYSDATETIMEOFFSET(), '-04:00'))) END);
        SET @NuevoUsuarioId = SCOPE_IDENTITY();
        INSERT INTO PerfilCliente (UsuarioId, DireccionPrincipal, ContactoEmergenciaNombre, ContactoEmergenciaTelefono)
        VALUES (@NuevoUsuarioId, @DireccionPrincipal, @ContactoEmergenciaNombre, @ContactoEmergenciaTelefono);
        COMMIT TRANSACTION;
        SELECT @NuevoUsuarioId AS NuevoId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
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
    @DocumentosExtra NVARCHAR(MAX) = NULL, @EstatusMigratorioId int = NULL,
    @TerminosVersion NVARCHAR(20) = NULL,
    @TerminosAceptadosEn DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @NuevoUsuarioId INT;

        INSERT INTO Usuarios (Email, PasswordHash, NombreCompleto, FotoUrl, RolId, IsActive, FechaCreacion, NacionalidadId, DocumentoIdentidad, Telefono, TerminosVersion, TerminosAceptadosEn)
        VALUES (@Email, @PasswordHash, @NombreCompleto, @FotoUrl, 3, 1, GETDATE(), @NacionalidadId, NULLIF(LTRIM(RTRIM(@DocumentoIdentidad)), N''), NULLIF(LTRIM(RTRIM(@Telefono)), N''), @TerminosVersion, CASE WHEN @TerminosVersion IS NOT NULL THEN COALESCE(@TerminosAceptadosEn, CONVERT(DATETIME2, SWITCHOFFSET(SYSDATETIMEOFFSET(), '-04:00'))) END);
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

-- Los detalles admin no tienen definición fuente en este repositorio.
-- Conservar íntegra la definición instalada, agregando únicamente dos proyecciones.
-- Se comprueban ambas definiciones antes de alterar cualquiera.
DECLARE @Cambios TABLE (Nombre SYSNAME, Definicion NVARCHAR(MAX));
DECLARE @Nombre SYSNAME, @Sql NVARCHAR(MAX), @Select INT, @Proc INT;
DECLARE detalles CURSOR LOCAL FAST_FORWARD FOR
    SELECT Nombre FROM (VALUES (N'sp_ObtenerClienteAdminDetalle'), (N'sp_ObtenerCuidadorAdminDetalle')) p(Nombre);
OPEN detalles;
FETCH NEXT FROM detalles INTO @Nombre;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = OBJECT_DEFINITION(OBJECT_ID(N'dbo.' + @Nombre));
    IF @Sql IS NULL THROW 51080, 'Falta un SP de detalle admin o no se puede leer su definición.', 1;
    IF CHARINDEX(N'TerminosAceptadosEn', @Sql COLLATE Latin1_General_100_CI_AS) = 0
    BEGIN
        SET @Select = CHARINDEX(N'SELECT', @Sql COLLATE Latin1_General_100_CI_AS);
        IF @Select = 0 THROW 51080, 'El detalle admin no contiene SELECT; revisar manualmente.', 1;
        IF LTRIM(SUBSTRING(@Sql, @Select + 6, 30)) LIKE N'TOP%'
           OR LTRIM(SUBSTRING(@Sql, @Select + 6, 30)) LIKE N'DISTINCT%'
            THROW 51080, 'El detalle admin requiere adaptar la proyección TOP/DISTINCT.', 1;
        SET @Sql = STUFF(@Sql, @Select + 6, 0,
            N' (SELECT TerminosVersion FROM dbo.Usuarios WHERE Id=@UsuarioId) AS TerminosVersion,
               (SELECT TerminosAceptadosEn FROM dbo.Usuarios WHERE Id=@UsuarioId) AS TerminosAceptadosEn, ');
        SET @Proc = CHARINDEX(N'PROCEDURE', @Sql COLLATE Latin1_General_100_CI_AS);
        IF @Proc = 0 THROW 51080, 'No se reconoce la definición del detalle admin.', 1;
        SET @Sql = N'CREATE OR ALTER ' + SUBSTRING(@Sql, @Proc, LEN(@Sql));
        INSERT INTO @Cambios VALUES (@Nombre, @Sql);
    END;
    FETCH NEXT FROM detalles INTO @Nombre;
END;
CLOSE detalles;
DEALLOCATE detalles;
BEGIN TRY
    BEGIN TRANSACTION;
    WHILE EXISTS (SELECT 1 FROM @Cambios)
    BEGIN
        SELECT TOP (1) @Nombre=Nombre, @Sql=Definicion FROM @Cambios;
        EXEC sys.sp_executesql @Sql;
        DELETE FROM @Cambios WHERE Nombre=@Nombre;
    END;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
GO
