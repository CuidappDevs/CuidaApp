-- Reemplazar un documento RECHAZADO del cuidador para que se vuelva a evaluar.
-- - Solo actúa si el documento es de ese cuidador y está rechazado (Estado = 3); si no, devuelve 0.
-- - El documento vuelve a "pendiente" (Estado 1) con el archivo nuevo y sin la observación anterior.
-- - Cédula/pasaporte y carta de antecedentes también actualizan su URL en PerfilCuidador.
-- - Si la cuenta estaba rechazada y ya no le queda ningún documento rechazado, vuelve a "pendiente".
-- Idempotente (CREATE OR ALTER).

CREATE OR ALTER PROCEDURE sp_ReemplazarDocumentoCuidador
    @DocumentoId INT,
    @CuidadorId INT,
    @UrlArchivo NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Tipo NVARCHAR(100);
    SELECT @Tipo = TipoDocumento FROM DocumentosCuidador
    WHERE Id = @DocumentoId AND CuidadorId = @CuidadorId AND Estado = 3;

    IF @Tipo IS NULL
    BEGIN
        SELECT 0 AS FilasAfectadas;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        -- El WHERE repite Estado = 3 por si dos envíos llegan a la vez.

        UPDATE DocumentosCuidador
        SET UrlArchivo = @UrlArchivo, Estado = 1, ObservacionesAdmin = NULL, FechaSubida = GETDATE()
        WHERE Id = @DocumentoId AND Estado = 3;

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 0 AS FilasAfectadas;
            RETURN;
        END

        IF @Tipo IN ('Cedula', 'Pasaporte')
            UPDATE PerfilCuidador SET CedulaUrl = @UrlArchivo WHERE UsuarioId = @CuidadorId;
        ELSE IF @Tipo = 'CartaAntecedentes'
            UPDATE PerfilCuidador SET CartaAntecedentesUrl = @UrlArchivo WHERE UsuarioId = @CuidadorId;

        IF NOT EXISTS (SELECT 1 FROM DocumentosCuidador WHERE CuidadorId = @CuidadorId AND Estado = 3)
            UPDATE PerfilCuidador SET EstadoAprobacion = 1 WHERE UsuarioId = @CuidadorId AND EstadoAprobacion = 3;

        COMMIT TRANSACTION;
        SELECT 1 AS FilasAfectadas;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
