-- ============================================================================
-- Horario automático de visibilidad del Care Partner.
-- El cuidador elige entre modo manual (interruptor, como siempre) o modo horario:
-- con el modo horario activo, el servidor lo pone visible / oculto según sus franjas
-- (cada minuto) y el interruptor manual queda bloqueado.
-- DiaSemana: 0 domingo, 1 lunes … 6 sábado (igual que DayOfWeek de .NET).
-- Idempotente. Requiere QUOTED_IDENTIFIER ON.
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH('dbo.PerfilCuidador', 'HorarioAutomatico') IS NULL
    ALTER TABLE dbo.PerfilCuidador ADD HorarioAutomatico BIT NOT NULL CONSTRAINT DF_PerfilCuidador_HorarioAutomatico DEFAULT 0;
GO

IF OBJECT_ID('dbo.HorarioCuidador') IS NULL
BEGIN
    CREATE TABLE dbo.HorarioCuidador (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        CuidadorId  INT     NOT NULL,              -- Usuarios.Id
        DiaSemana   TINYINT NOT NULL CHECK (DiaSemana BETWEEN 0 AND 6),
        HoraInicio  TIME(0) NOT NULL,
        HoraFin     TIME(0) NOT NULL,
        CONSTRAINT FK_HorarioCuidador_Usuarios FOREIGN KEY (CuidadorId) REFERENCES dbo.Usuarios (Id),
        CONSTRAINT CK_HorarioCuidador_Rango CHECK (HoraFin > HoraInicio)
    );
    CREATE INDEX IX_HorarioCuidador_Cuidador ON dbo.HorarioCuidador (CuidadorId, DiaSemana);
END
GO

-- Configuración del cuidador (2 resultados: modo y franjas).
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerHorarioCuidador @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(HorarioAutomatico, 0) AS Activo, Disponible FROM PerfilCuidador WHERE UsuarioId = @CuidadorId;
    SELECT DiaSemana, HoraInicio, HoraFin FROM HorarioCuidador WHERE CuidadorId = @CuidadorId ORDER BY DiaSemana, HoraInicio;
END
GO

-- Guarda el modo y reemplaza las franjas. @Franjas es JSON: [{"DiaSemana":1,"HoraInicio":"08:00","HoraFin":"17:00"}, ...]
CREATE OR ALTER PROCEDURE dbo.sp_GuardarHorarioCuidador
    @CuidadorId INT, @Activo BIT, @Franjas NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM PerfilCuidador WHERE UsuarioId = @CuidadorId) BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END

    DECLARE @F TABLE (DiaSemana TINYINT, HoraInicio TIME(0), HoraFin TIME(0));
    INSERT INTO @F
    SELECT DiaSemana, TRY_CAST(HoraInicio AS TIME(0)), TRY_CAST(HoraFin AS TIME(0))
    FROM OPENJSON(ISNULL(@Franjas, N'[]')) WITH (DiaSemana TINYINT, HoraInicio VARCHAR(8), HoraFin VARCHAR(8));

    IF EXISTS (SELECT 1 FROM @F WHERE DiaSemana NOT BETWEEN 0 AND 6 OR HoraInicio IS NULL OR HoraFin IS NULL OR HoraFin <= HoraInicio)
    BEGIN SELECT 'FRANJA_INVALIDA' AS Resultado; RETURN; END
    -- Franjas del mismo día que se pisan.
    IF EXISTS (SELECT 1 FROM @F a JOIN @F b ON a.DiaSemana = b.DiaSemana AND a.HoraInicio < b.HoraInicio AND a.HoraFin > b.HoraInicio)
    BEGIN SELECT 'FRANJAS_SOLAPADAS' AS Resultado; RETURN; END
    IF @Activo = 1 AND NOT EXISTS (SELECT 1 FROM @F)
    BEGIN SELECT 'SIN_FRANJAS' AS Resultado; RETURN; END

    BEGIN TRAN;
        DELETE FROM HorarioCuidador WHERE CuidadorId = @CuidadorId;
        INSERT INTO HorarioCuidador (CuidadorId, DiaSemana, HoraInicio, HoraFin)
        SELECT @CuidadorId, DiaSemana, HoraInicio, HoraFin FROM @F;
        UPDATE PerfilCuidador SET HorarioAutomatico = @Activo WHERE UsuarioId = @CuidadorId;
    COMMIT;
    SELECT 'OK' AS Resultado;
END
GO

-- El interruptor manual solo funciona en modo manual (y solo un cuidador aprobado puede ponerse visible).
CREATE OR ALTER PROCEDURE dbo.sp_ActualizarDisponibilidadCuidador
    @CuidadorId INT,
    @Disponible BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE PerfilCuidador SET Disponible = @Disponible
    WHERE UsuarioId = @CuidadorId AND HorarioAutomatico = 0 AND (@Disponible = 0 OR EstadoAprobacion = 2);
    SELECT @@ROWCOUNT AS FilasAfectadas;
END;
GO

-- Lo corre la API cada minuto: aplica el horario a los cuidadores en modo automático y devuelve
-- solo los que cambiaron (para avisar por SignalR). @Ahora en hora de República Dominicana.
CREATE OR ALTER PROCEDURE dbo.sp_AplicarHorariosCuidadores @Ahora DATETIME, @CuidadorId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    -- 1900-01-07 fue domingo: así el día no depende de SET DATEFIRST.
    DECLARE @Dia TINYINT = DATEDIFF(DAY, '19000107', @Ahora) % 7;
    DECLARE @Hora TIME(0) = CAST(@Ahora AS TIME(0));

    DECLARE @Cambios TABLE (CuidadorId INT, Disponible BIT);

    UPDATE pc
    SET Disponible = d.Debe
    OUTPUT inserted.UsuarioId, inserted.Disponible INTO @Cambios
    FROM PerfilCuidador pc
    JOIN Usuarios u ON u.Id = pc.UsuarioId
    CROSS APPLY (SELECT CAST(IIF(pc.EstadoAprobacion = 2 AND u.IsActive = 1 AND EXISTS (
                     SELECT 1 FROM HorarioCuidador h
                     WHERE h.CuidadorId = pc.UsuarioId AND h.DiaSemana = @Dia AND @Hora >= h.HoraInicio AND @Hora < h.HoraFin), 1, 0) AS BIT) AS Debe) d
    WHERE pc.HorarioAutomatico = 1
      AND (@CuidadorId IS NULL OR pc.UsuarioId = @CuidadorId)
      AND pc.Disponible <> d.Debe;

    SELECT CuidadorId, Disponible FROM @Cambios;
END
GO
