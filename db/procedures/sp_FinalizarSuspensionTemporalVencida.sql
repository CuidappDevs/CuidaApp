/*
  LOTE-2. NO APLICAR sin autorizacion (Gate G1); requiere la migracion 20261003_lote_2_sanciones_usuario_up.sql.
  Solo AuthService, DESPUES de verificar el password. Cierra como CUMPLIDA una suspension temporal
  vencida y reactiva la cuenta en una sola transaccion, usando el reloj de la base (contrato 5.5).
*/
CREATE OR ALTER PROCEDURE dbo.sp_FinalizarSuspensionTemporalVencida
    @UsuarioId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @AhoraUtc datetime2(7) = SYSUTCDATETIME();
    DECLARE @Codigo varchar(40), @SancionId int = NULL, @IsActive bit = NULL,
            @Estado varchar(20) = NULL, @Fin datetime2(7) = NULL, @Tipo varchar(12) = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @IsActive = IsActive FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK) WHERE Id = @UsuarioId;

        IF @@ROWCOUNT = 0
            SET @Codigo = 'USER_NOT_FOUND';
        ELSE
        BEGIN
            SELECT @SancionId = Id, @Tipo = Tipo, @Fin = FechaFinUtc, @Estado = Estado
            FROM dbo.SancionesUsuario WITH (UPDLOCK, HOLDLOCK)
            WHERE UsuarioId = @UsuarioId AND Estado = 'VIGENTE';

            IF @SancionId IS NULL AND @IsActive = 1
            BEGIN
                SET @Codigo = 'ALREADY_ACTIVE';
            END
            ELSE IF @SancionId IS NULL OR @IsActive = 1
                SET @Codigo = 'STATE_CONFLICT';
            ELSE IF @Tipo <> 'TEMPORAL' OR @Fin > @AhoraUtc
                SET @Codigo = 'NOT_DUE';
            ELSE
            BEGIN
                UPDATE dbo.SancionesUsuario SET Estado = 'CUMPLIDA' WHERE Id = @SancionId;
                UPDATE dbo.Usuarios SET IsActive = 1 WHERE Id = @UsuarioId;
                SET @Codigo = 'APPLIED';
                SET @IsActive = 1;
                SET @Estado = 'CUMPLIDA';
            END
        END

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT CAST(@Codigo AS varchar(40)) AS ResultadoCodigo,
           @UsuarioId                   AS UsuarioId,
           @SancionId                   AS SancionId,
           @IsActive                    AS IsActive,
           @Estado                      AS EstadoSancion,
           @Fin                         AS FechaFinUtc;
END;
