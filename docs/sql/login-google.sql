-- Ejecutar manualmente después de deuda-cliente-eliminar-cuenta.sql.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
IF COL_LENGTH('dbo.Usuarios', 'GoogleSub') IS NULL
    ALTER TABLE dbo.Usuarios ADD GoogleSub NVARCHAR(64) COLLATE Latin1_General_100_BIN2 NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Usuarios') AND name = 'UX_Usuarios_GoogleSub')
    CREATE UNIQUE INDEX UX_Usuarios_GoogleSub ON dbo.Usuarios(GoogleSub) WHERE GoogleSub IS NOT NULL;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GoogleEnlazar @UsuarioId INT, @GoogleSub NVARCHAR(64) AS
BEGIN
    SET NOCOUNT ON;
    -- Participa en la transacción del registro o del resolvedor.
    IF EXISTS (SELECT 1 FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK)
               WHERE GoogleSub=@GoogleSub AND Id<>@UsuarioId)
        THROW 51070, 'GOOGLE_YA_ENLAZADO', 1;
    UPDATE dbo.Usuarios SET GoogleSub=@GoogleSub
    WHERE Id=@UsuarioId AND (GoogleSub IS NULL OR GoogleSub=@GoogleSub);
    IF @@ROWCOUNT<>1 THROW 51070, 'GOOGLE_YA_ENLAZADO', 1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GoogleResolver @GoogleSub NVARCHAR(64), @Email NVARCHAR(150) AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRAN;
        DECLARE @Id INT, @Correo NVARCHAR(150);
        SELECT @Id=Id, @Correo=Email FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE GoogleSub=@GoogleSub;
        IF @Id IS NULL
        BEGIN
            -- Comparación explícita insensible a mayúsculas, incluso en bases CS.
            IF (SELECT COUNT(*) FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK)
                WHERE Email COLLATE Latin1_General_100_CI_AS = @Email COLLATE Latin1_General_100_CI_AS)>1
                THROW 51070, 'GOOGLE_YA_ENLAZADO', 1;
            SELECT @Id=Id,@Correo=Email FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK)
            WHERE Email COLLATE Latin1_General_100_CI_AS = @Email COLLATE Latin1_General_100_CI_AS;
        END;
        -- Nunca reactivar ni enlazar cuentas anonimizadas.
        IF EXISTS (SELECT 1 FROM dbo.SolicitudesEliminacionCuenta WHERE UsuarioId=@Id AND Estado=3)
           OR @Correo=CONCAT('eliminado+',@Id,'@cuidapp.invalid')
        BEGIN
            COMMIT;
            SELECT CAST(NULL AS NVARCHAR(150));
            RETURN;
        END;
        IF @Id IS NOT NULL EXEC dbo.sp_GoogleEnlazar @Id,@GoogleSub;
        COMMIT;
        SELECT @Correo;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK;
        THROW;
    END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_GoogleRegistroValidar @GoogleSub NVARCHAR(64), @Email NVARCHAR(150) AS
BEGIN
    SET NOCOUNT ON;
    -- Se llama dentro de la transacción que crea al usuario.
    IF EXISTS (SELECT 1 FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK) WHERE GoogleSub=@GoogleSub)
       OR EXISTS (SELECT 1 FROM dbo.Usuarios WITH(UPDLOCK,HOLDLOCK)
                  WHERE Email COLLATE Latin1_General_100_CI_AS=@Email COLLATE Latin1_General_100_CI_AS AND GoogleSub IS NOT NULL)
        THROW 51070, 'GOOGLE_YA_ENLAZADO', 1;
END;
GO
