-- Registro: guarda Nacionalidad, Documento de identidad (cédula / pasaporte) y Teléfono en Usuarios,
-- y (cuidador) los documentos opcionales extra en DocumentosCuidador.
-- Requiere: nacionalidades.sql, usuarios-nacionalidad.sql, usuarios-documento-identidad.sql y usuarios-telefono.sql.
--
-- Solo se agregan parámetros OPCIONALES al final (= NULL): una llamada sin ellos
-- (app o API viejos) se comporta exactamente igual que antes.
-- @DocumentosExtra: JSON [{"tipoDocumento":"Certificado médico","urlArchivo":"..."}] (usa OPENJSON,
-- requiere nivel de compatibilidad 130 o más; DBCuidappDev está en 160).
-- El documento de identidad (CedulaUrl) se registra como 'Pasaporte' si la nacionalidad no es dominicana.
--
-- OJO: está basado en la versión de DBCuidappDev. Antes de correrlo en producción, comparar
-- con OBJECT_DEFINITION(OBJECT_ID('sp_CrearUsuarioCuidador')) de DBCuidapp por si difiere.

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
    @Telefono NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @NuevoUsuarioId INT;
        INSERT INTO Usuarios (Email, PasswordHash, NombreCompleto, FotoUrl, RolId, IsActive, FechaCreacion, NacionalidadId, DocumentoIdentidad, Telefono)
        VALUES (@Email, @PasswordHash, @NombreCompleto, @FotoUrl, 2, 1, GETDATE(), @NacionalidadId, NULLIF(LTRIM(RTRIM(@DocumentoIdentidad)), N''), NULLIF(LTRIM(RTRIM(@Telefono)), N''));
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
    @DocumentosExtra NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
        DECLARE @NuevoUsuarioId INT;

        INSERT INTO Usuarios (Email, PasswordHash, NombreCompleto, FotoUrl, RolId, IsActive, FechaCreacion, NacionalidadId, DocumentoIdentidad, Telefono)
        VALUES (@Email, @PasswordHash, @NombreCompleto, @FotoUrl, 3, 1, GETDATE(), @NacionalidadId, NULLIF(LTRIM(RTRIM(@DocumentoIdentidad)), N''), NULLIF(LTRIM(RTRIM(@Telefono)), N''));
        SET @NuevoUsuarioId = SCOPE_IDENTITY();

        INSERT INTO PerfilCuidador (UsuarioId, Especialidad, TarifaHora, Bio, MetodoCobro, CedulaUrl, CartaAntecedentesUrl, EstadoAprobacion)
        VALUES (@NuevoUsuarioId, @Especialidad, @TarifaHora, @Bio, @MetodoCobro, @CedulaUrl, @CartaAntecedentesUrl, 1);

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

-- Ya no se usa: la nacionalidad ahora la guardan los SPs de registro.
DROP PROCEDURE IF EXISTS sp_ActualizarNacionalidadUsuario;
GO
