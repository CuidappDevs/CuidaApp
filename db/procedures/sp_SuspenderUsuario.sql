/*
  LOTE-2. NO APLICAR sin autorizacion (Gate G1); requiere la migracion 20261003_lote_2_sanciones_usuario_up.sql.
  Suspende una cuenta de forma atomica: Usuarios.IsActive y SancionesUsuario se confirman juntos.
  Devuelve exactamente un result set de una fila (ver contrato 5.1).
*/
CREATE OR ALTER PROCEDURE dbo.sp_SuspenderUsuario
    @UsuarioId   int,
    @AdminId     int,
    @Motivo      nvarchar(500),
    @FechaFinUtc datetime2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @AhoraUtc datetime2(7) = SYSUTCDATETIME();
    DECLARE @Codigo varchar(40), @SancionId int = NULL, @IsActive bit = NULL,
            @Estado varchar(20) = NULL, @Fin datetime2(7) = NULL, @MotivoLimpio nvarchar(500);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @IsActive = IsActive FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK) WHERE Id = @UsuarioId;

        IF @@ROWCOUNT = 0
            SET @Codigo = 'USER_NOT_FOUND';
        ELSE IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE Id = @AdminId AND RolId = 1)
            SET @Codigo = 'ADMIN_ROLE_REQUIRED';
        ELSE
        BEGIN
            SET @MotivoLimpio = LTRIM(RTRIM(@Motivo));
            IF @MotivoLimpio IS NULL OR LEN(@MotivoLimpio) < 10 OR LEN(@MotivoLimpio) > 500
                SET @Codigo = 'INVALID_REASON';
            ELSE IF @FechaFinUtc IS NOT NULL AND @FechaFinUtc <= @AhoraUtc
                SET @Codigo = 'INVALID_END_DATE';
            ELSE
            BEGIN
                SELECT @SancionId = Id, @Estado = Estado, @Fin = FechaFinUtc
                FROM dbo.SancionesUsuario WITH (UPDLOCK, HOLDLOCK)
                WHERE UsuarioId = @UsuarioId AND Estado = 'VIGENTE';

                IF @SancionId IS NOT NULL
                    SET @Codigo = 'ALREADY_SUSPENDED';
                ELSE IF @IsActive = 0
                BEGIN
                    SET @Codigo = 'STATE_CONFLICT';
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.SancionesUsuario (UsuarioId, AdminId, Accion, Motivo, Tipo, Estado, FechaInicioUtc, FechaFinUtc)
                    VALUES (@UsuarioId, @AdminId, 'SUSPENSION', @MotivoLimpio,
                            CASE WHEN @FechaFinUtc IS NULL THEN 'INDEFINIDA' ELSE 'TEMPORAL' END,
                            'VIGENTE', @AhoraUtc, @FechaFinUtc);
                    SET @SancionId = SCOPE_IDENTITY();

                    UPDATE dbo.Usuarios SET IsActive = 0 WHERE Id = @UsuarioId;

                    SET @Codigo = 'APPLIED';
                    SET @IsActive = 0;
                    SET @Estado = 'VIGENTE';
                    SET @Fin = @FechaFinUtc;
                END
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
