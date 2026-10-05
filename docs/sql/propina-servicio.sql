-- Propina opcional que el cliente da al confirmar que el servicio terminó.
-- Aplicar PRIMERO en DBCuidappDev, verificar, y luego en DBCuidapp (producción).
-- Cambio aditivo: la columna tiene DEFAULT 0 y el parámetro del SP es opcional, así que la
-- API/app anteriores siguen funcionando (propina = 0).

IF COL_LENGTH('dbo.Pagos', 'Propina') IS NULL
    ALTER TABLE dbo.Pagos ADD Propina DECIMAL(10,2) NOT NULL CONSTRAINT DF_Pagos_Propina DEFAULT 0;
GO

-- El pago a un cuidador = Monto (tarifa) + Propina. Se crea al confirmar el cliente.
CREATE OR ALTER PROCEDURE dbo.sp_ConfirmarFinalizacionTrabajo
    @TrabajoId INT,
    @ClienteId INT,
    @Confirmado BIT,
    @FechaHora DATETIME2,
    @Propina DECIMAL(10,2) = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @EstadoActual INT, @CuidadorId INT, @Tarifa DECIMAL(10,2), @ClienteReal INT;

    SELECT @EstadoActual = Estado, @CuidadorId = CuidadorId, @Tarifa = Tarifa, @ClienteReal = ClienteId
    FROM Trabajos WHERE Id = @TrabajoId;

    IF @EstadoActual IS NULL OR @ClienteReal <> @ClienteId
    BEGIN
        SELECT 0 AS FilasAfectadas, 'TRABAJO_NO_ENCONTRADO' AS Motivo;
        RETURN;
    END

    IF @EstadoActual <> 7
    BEGIN
        SELECT 0 AS FilasAfectadas, 'ESTADO_INVALIDO' AS Motivo;
        RETURN;
    END

    IF @Propina IS NULL OR @Propina < 0
        SET @Propina = 0;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF @Confirmado = 1
        BEGIN
            UPDATE Trabajos SET Estado = 4, FechaFin = @FechaHora, RechazadoPorCliente = 0 WHERE Id = @TrabajoId;

            IF NOT EXISTS (SELECT 1 FROM Pagos WHERE TrabajoId = @TrabajoId)
            BEGIN
                INSERT INTO Pagos (TrabajoId, CuidadorId, Monto, Propina, Estado, FechaCreacion)
                VALUES (@TrabajoId, @CuidadorId, @Tarifa, @Propina, 1, @FechaHora);
            END
        END
        ELSE
        BEGIN
            UPDATE Trabajos SET Estado = 3, RechazadoPorCliente = 1 WHERE Id = @TrabajoId;
        END

        COMMIT TRANSACTION;
        SELECT 1 AS FilasAfectadas, 'OK' AS Motivo;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- Pagos del cuidador: agrega Propina (columna nueva al final; las anteriores no cambian).
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerPagosPorCuidador
    @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.Id, p.TrabajoId, t.TipoServicio, c.NombreCompleto AS ClienteNombre,
           p.Monto, p.Estado, p.FechaPago, p.FechaCreacion, p.Propina
    FROM Pagos p
    JOIN Trabajos t ON t.Id = p.TrabajoId
    JOIN Usuarios c ON c.Id = t.ClienteId
    WHERE p.CuidadorId = @CuidadorId
    ORDER BY p.FechaCreacion DESC;
END;
GO

-- Las ganancias incluyen la propina (Monto + Propina).
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerGananciasCuidador
    @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ISNULL((SELECT SUM(Monto + Propina) FROM Pagos WHERE CuidadorId = @CuidadorId AND Estado = 2 AND CAST(FechaPago AS DATE) = CAST(GETDATE() AS DATE)), 0) AS GanadoHoy,
        ISNULL((SELECT SUM(Monto + Propina) FROM Pagos WHERE CuidadorId = @CuidadorId AND Estado = 1), 0) AS PendientePorCobrar,
        ISNULL((SELECT SUM(Monto + Propina) FROM Pagos WHERE CuidadorId = @CuidadorId AND Estado = 2), 0) AS TotalCobrado;
END;
GO

-- Panel admin: agrega Propina.
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerPagosAdmin
    @Estado INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        p.Id,
        p.TrabajoId,
        p.CuidadorId,
        cu.NombreCompleto AS CuidadorNombre,
        t.ClienteId,
        cl.NombreCompleto AS ClienteNombre,
        t.TipoServicio,
        p.Monto,
        p.Estado,
        p.FechaCreacion,
        p.AutorizadoPorAdminId,
        adminAut.NombreCompleto AS AutorizadoPorNombre,
        p.FechaAutorizacion,
        p.AprobadoPorAdminId,
        adminApr.NombreCompleto AS AprobadoPorNombre,
        p.FechaPago,
        t.PagoDisputado,
        p.Propina
    FROM Pagos p
    INNER JOIN Trabajos t ON t.Id = p.TrabajoId
    INNER JOIN Usuarios cu ON cu.Id = p.CuidadorId
    INNER JOIN Usuarios cl ON cl.Id = t.ClienteId
    LEFT JOIN Usuarios adminAut ON adminAut.Id = p.AutorizadoPorAdminId
    LEFT JOIN Usuarios adminApr ON adminApr.Id = p.AprobadoPorAdminId
    WHERE @Estado IS NULL OR p.Estado = @Estado
    ORDER BY p.FechaCreacion DESC;
END
GO
