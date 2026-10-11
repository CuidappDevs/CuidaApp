-- ============================================================================
-- Método de pago elegido por el cliente al pedir + saldo de comisiones de efectivo del Care Partner.
--  * El cliente elige al pedir: 'efectivo' (le paga directo al Care Partner) o 'transferencia'
--    (le paga a CuidApp, que luego le transfiere al Care Partner su parte).
--  * Efectivo: el pago queda en Estado 4 ("Cobrado en efectivo", sin transferir) y la comisión
--    pasa al saldo que el Care Partner le debe a CuidApp.
--  * Ese saldo se descuenta solo al autorizar su próximo pago por transferencia; también se puede
--    registrar un abono desde el panel.
--  * Si el saldo llega al tope (RD$ 2,000 por defecto), no puede aceptar servicios en efectivo.
-- Correr DESPUÉS de caja-negra-grabacion.sql. Idempotente. Requiere QUOTED_IDENTIFIER ON.
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ConfiguracionPlataforma WHERE Clave = 'TopeDeudaEfectivo')
    INSERT INTO dbo.ConfiguracionPlataforma (Clave, Valor) VALUES ('TopeDeudaEfectivo', '2000');
IF NOT EXISTS (SELECT 1 FROM dbo.ConfiguracionPlataforma WHERE Clave = 'InstruccionesTransferencia')
    INSERT INTO dbo.ConfiguracionPlataforma (Clave, Valor) VALUES ('InstruccionesTransferencia', '');
GO
-- Valor de la configuración puede ser un texto largo (instrucciones de transferencia).
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ConfiguracionPlataforma') AND name = 'Valor' AND max_length <> -1 AND max_length < 2000)
    ALTER TABLE dbo.ConfiguracionPlataforma ALTER COLUMN Valor NVARCHAR(1000) NOT NULL;
GO

-- Lo que se descontó de la deuda de efectivo al autorizar este pago por transferencia.
IF COL_LENGTH('dbo.Pagos', 'DescuentoSaldo') IS NULL
    ALTER TABLE dbo.Pagos ADD DescuentoSaldo DECIMAL(10,2) NOT NULL CONSTRAINT DF_Pagos_DescuentoSaldo DEFAULT 0;
GO

-- Movimientos del saldo del Care Partner con CuidApp. Monto > 0 = debe; < 0 = abono/descuento.
IF OBJECT_ID('dbo.MovimientosSaldoCuidador') IS NULL
BEGIN
    CREATE TABLE dbo.MovimientosSaldoCuidador (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        CuidadorId  INT            NOT NULL,
        TrabajoId   INT            NULL,
        PagoId      INT            NULL,
        Tipo        VARCHAR(20)    NOT NULL,   -- 'comision_efectivo', 'descuento_pago', 'abono'
        Monto       DECIMAL(10,2)  NOT NULL,
        Nota        NVARCHAR(300)  NULL,
        AdminId     INT            NULL,
        Fecha       DATETIME       NOT NULL
    );
    CREATE INDEX IX_MovimientosSaldo_Cuidador ON dbo.MovimientosSaldoCuidador (CuidadorId, Fecha DESC);
    CREATE INDEX IX_MovimientosSaldo_Trabajo ON dbo.MovimientosSaldoCuidador (TrabajoId) WHERE TrabajoId IS NOT NULL;
END
GO

CREATE OR ALTER FUNCTION dbo.fn_SaldoCuidador (@CuidadorId INT)
RETURNS DECIMAL(10,2)
AS
BEGIN
    RETURN ISNULL((SELECT SUM(Monto) FROM dbo.MovimientosSaldoCuidador WHERE CuidadorId = @CuidadorId), 0);
END
GO

CREATE OR ALTER FUNCTION dbo.fn_TopeDeudaEfectivo ()
RETURNS DECIMAL(10,2)
AS
BEGIN
    RETURN ISNULL((SELECT TRY_CAST(Valor AS DECIMAL(10,2)) FROM dbo.ConfiguracionPlataforma WHERE Clave = 'TopeDeudaEfectivo'), 2000);
END
GO

-- Puede tomar servicios en efectivo mientras su deuda esté por debajo del tope.
CREATE OR ALTER FUNCTION dbo.fn_PuedeAceptarEfectivo (@CuidadorId INT)
RETURNS BIT
AS
BEGIN
    RETURN IIF(dbo.fn_SaldoCuidador(@CuidadorId) < dbo.fn_TopeDeudaEfectivo(), 1, 0);
END
GO

-- ============================================================================
-- Crear el servicio con el método de pago elegido por el cliente
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_CrearTrabajo
    @ClienteId INT,
    @CuidadorId INT,
    @TipoServicio NVARCHAR(100),
    @Fecha DATE,
    @HoraInicio TIME,
    @HoraFin TIME,
    @Direccion NVARCHAR(255),
    @Tarifa DECIMAL(10,2),
    @Latitud DECIMAL(9,6) = NULL,
    @Longitud DECIMAL(9,6) = NULL,
    @FechaCreacion DATETIME2,
    @MetodoPago VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @MetodoPago NOT IN ('efectivo', 'transferencia', 'tarjeta') SET @MetodoPago = NULL;
    DECLARE @Pin CHAR(4) = RIGHT('0000' + CAST(ABS(CHECKSUM(NEWID())) % 9000 + 1000 AS VARCHAR(4)), 4);
    DECLARE @PinSalida CHAR(4) = RIGHT('0000' + CAST(ABS(CHECKSUM(NEWID())) % 9000 + 1000 AS VARCHAR(4)), 4);

    INSERT INTO Trabajos (ClienteId, CuidadorId, TipoServicio, Fecha, HoraInicio, HoraFin, Direccion, Estado, Tarifa, Latitud, Longitud, PinInicio, PinFin, FechaCreacion, MetodoPago)
    VALUES (@ClienteId, @CuidadorId, @TipoServicio, @Fecha, @HoraInicio, @HoraFin, @Direccion, 1, @Tarifa, @Latitud, @Longitud, @Pin, @PinSalida, @FechaCreacion, @MetodoPago);

    SELECT SCOPE_IDENTITY() AS NuevoId;
END;
GO

-- Aceptar/rechazar (igual que antes) + no deja aceptar un servicio en efectivo con la deuda en el tope.
CREATE OR ALTER PROCEDURE dbo.sp_ActualizarEstadoTrabajo
    @TrabajoId INT,
    @NuevoEstado INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @NuevoEstado = 3
    BEGIN
        RAISERROR('El estado En Progreso solo puede activarse mediante sp_IniciarTrabajo con PIN.', 16, 1);
        RETURN;
    END
    IF @NuevoEstado = 4
    BEGIN
        RAISERROR('El estado Completado solo puede activarse mediante sp_FinalizarTrabajo con PIN de salida.', 16, 1);
        RETURN;
    END

    IF @NuevoEstado = 2 AND EXISTS (SELECT 1 FROM Trabajos t WHERE t.Id = @TrabajoId AND t.MetodoPago = 'efectivo'
                                     AND dbo.fn_PuedeAceptarEfectivo(t.CuidadorId) = 0)
    BEGIN
        SELECT 0 AS FilasAfectadas, 'DEUDA_EFECTIVO' AS Motivo;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;
        UPDATE Trabajos SET Estado = @NuevoEstado WHERE Id = @TrabajoId;
        DECLARE @Filas INT = @@ROWCOUNT;
        COMMIT TRANSACTION;
        SELECT @Filas AS FilasAfectadas, CAST(NULL AS VARCHAR(30)) AS Motivo;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- ============================================================================
-- El pago sigue al método: efectivo -> Estado 4 + comisión al saldo; si no, Estado 1.
-- Lo llaman todos los lugares que crean o cambian un pago.
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_RecalcularPagoEfectivo @TrabajoId INT, @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Metodo VARCHAR(20), @CuidadorId INT, @Tipo NVARCHAR(100);
    SELECT @Metodo = MetodoPago, @CuidadorId = CuidadorId, @Tipo = TipoServicio FROM Trabajos WHERE Id = @TrabajoId;

    DECLARE @PagoId INT, @Estado INT, @Monto DECIMAL(10,2), @Pct DECIMAL(5,2);
    SELECT @PagoId = Id, @Estado = Estado, @Monto = Monto, @Pct = ISNULL(ComisionPorcentaje, dbo.fn_ComisionServicio(@Tipo))
    FROM Pagos WHERE TrabajoId = @TrabajoId;

    IF @PagoId IS NOT NULL AND @Metodo = 'efectivo' AND @Estado = 1
        UPDATE Pagos SET Estado = 4 WHERE Id = @PagoId;
    ELSE IF @PagoId IS NOT NULL AND ISNULL(@Metodo, '') <> 'efectivo' AND @Estado = 4
        UPDATE Pagos SET Estado = 1 WHERE Id = @PagoId;

    -- La comisión adeudada de este servicio siempre refleja lo actual (se conserva su fecha original).
    DECLARE @FechaMov DATETIME = ISNULL((SELECT TOP 1 Fecha FROM MovimientosSaldoCuidador WHERE TrabajoId = @TrabajoId AND Tipo = 'comision_efectivo'), @Fecha);
    DELETE FROM MovimientosSaldoCuidador WHERE TrabajoId = @TrabajoId AND Tipo = 'comision_efectivo';

    IF @PagoId IS NOT NULL AND EXISTS (SELECT 1 FROM Pagos WHERE Id = @PagoId AND Estado = 4)
    BEGIN
        DECLARE @Comision DECIMAL(10,2) = ROUND(@Monto * @Pct / 100, 2);
        IF @Comision > 0
            INSERT INTO MovimientosSaldoCuidador (CuidadorId, TrabajoId, PagoId, Tipo, Monto, Nota, Fecha)
            VALUES (@CuidadorId, @TrabajoId, @PagoId, 'comision_efectivo', @Comision, N'Comisión de un servicio cobrado en efectivo', @FechaMov);
    END
END
GO

-- Confirmación del cliente (igual que antes) + recalcula el pago según el método.
CREATE OR ALTER PROCEDURE dbo.sp_ConfirmarFinalizacionTrabajo
    @TrabajoId INT,
    @ClienteId INT,
    @Confirmado BIT,
    @FechaHora DATETIME2,
    @Propina DECIMAL(10,2) = 0
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @EstadoActual INT, @CuidadorId INT, @Tarifa DECIMAL(10,2), @ClienteReal INT, @Tipo NVARCHAR(100);

    SELECT @EstadoActual = Estado, @CuidadorId = CuidadorId, @Tarifa = Tarifa, @ClienteReal = ClienteId, @Tipo = TipoServicio
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
                INSERT INTO Pagos (TrabajoId, CuidadorId, Monto, Propina, Estado, FechaCreacion, ComisionPorcentaje)
                VALUES (@TrabajoId, @CuidadorId, @Tarifa, @Propina, 1, @FechaHora, dbo.fn_ComisionServicio(@Tipo));
            END
            DECLARE @F DATETIME = CAST(@FechaHora AS DATETIME);
            EXEC dbo.sp_RecalcularPagoEfectivo @TrabajoId, @F;
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

-- Completar desde soporte (igual que antes) + recalcula el pago según el método.
CREATE OR ALTER PROCEDURE dbo.sp_AdminCompletarTrabajo @TrabajoId INT, @Justificacion NVARCHAR(400), @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Estado INT, @ClienteId INT, @CuidadorId INT, @Tarifa DECIMAL(10,2), @Tipo NVARCHAR(100);
    SELECT @Estado = Estado, @ClienteId = ClienteId, @CuidadorId = CuidadorId, @Tarifa = Tarifa, @Tipo = TipoServicio FROM Trabajos WHERE Id = @TrabajoId;
    IF @Estado IS NULL BEGIN SELECT 'NO_ENCONTRADO' AS Resultado, NULL AS ClienteId, NULL AS CuidadorId; RETURN; END
    IF @Estado NOT IN (3, 7) BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId; RETURN; END

    BEGIN TRAN;
        UPDATE Trabajos SET Estado = 4, FechaFin = ISNULL(FechaFin, @Fecha),
               JustificacionFinalizacion = N'Completado por soporte: ' + @Justificacion
        WHERE Id = @TrabajoId;
        IF NOT EXISTS (SELECT 1 FROM Pagos WHERE TrabajoId = @TrabajoId)
            INSERT INTO Pagos (TrabajoId, CuidadorId, Monto, Propina, Estado, FechaCreacion, ComisionPorcentaje)
            VALUES (@TrabajoId, @CuidadorId, @Tarifa, 0, 1, @Fecha, dbo.fn_ComisionServicio(@Tipo));
        EXEC dbo.sp_RecalcularPagoEfectivo @TrabajoId, @Fecha;
    COMMIT;
    SELECT 'OK' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId;
END
GO

-- Corregir el método desde el panel: el pago (si aún no se transfirió) y el saldo se ajustan solos.
CREATE OR ALTER PROCEDURE dbo.sp_AdminFijarMetodoPago @TrabajoId INT, @MetodoPago VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @MetodoPago IS NOT NULL AND @MetodoPago NOT IN ('efectivo', 'tarjeta', 'transferencia')
    BEGIN SELECT 'METODO_INVALIDO' AS Resultado; RETURN; END
    UPDATE Trabajos SET MetodoPago = @MetodoPago WHERE Id = @TrabajoId;
    IF @@ROWCOUNT = 0 BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    DECLARE @Ahora DATETIME = DATEADD(HOUR, -4, GETUTCDATE());
    EXEC dbo.sp_RecalcularPagoEfectivo @TrabajoId, @Ahora;
    SELECT 'OK' AS Resultado;
END
GO

-- Resolver disputa (igual que antes; un pago "cobrado en efectivo" cuenta como no transferido) + recalcula.
CREATE OR ALTER PROCEDURE dbo.sp_AdminResolverDisputa
    @DisputaId INT, @Resolucion VARCHAR(12), @Monto DECIMAL(10,2) = NULL, @Nota NVARCHAR(1000), @AdminId INT, @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TrabajoId INT, @EstadoDisputa TINYINT;
    SELECT @TrabajoId = TrabajoId, @EstadoDisputa = Estado FROM Disputas WHERE Id = @DisputaId;
    IF @TrabajoId IS NULL BEGIN SELECT 'NO_ENCONTRADO' AS Resultado, NULL AS ClienteId, NULL AS CuidadorId, NULL AS TrabajoId, NULL AS MontoFinal; RETURN; END

    DECLARE @Estado INT, @ClienteId INT, @CuidadorId INT, @Tarifa DECIMAL(10,2), @Tipo NVARCHAR(100);
    SELECT @Estado = Estado, @ClienteId = ClienteId, @CuidadorId = CuidadorId, @Tarifa = Tarifa, @Tipo = TipoServicio FROM Trabajos WHERE Id = @TrabajoId;

    IF @EstadoDisputa <> 1 OR @Estado NOT IN (3, 4, 7)
    BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId, @TrabajoId AS TrabajoId, NULL AS MontoFinal; RETURN; END

    DECLARE @MontoFinal DECIMAL(10,2) = CASE @Resolucion WHEN 'completo' THEN @Tarifa WHEN 'sin_pago' THEN 0 WHEN 'parcial' THEN @Monto END;
    IF @MontoFinal IS NULL OR @MontoFinal < 0 OR @MontoFinal > @Tarifa OR (@Resolucion = 'parcial' AND (@MontoFinal = 0 OR @MontoFinal = @Tarifa))
    BEGIN SELECT 'MONTO_INVALIDO' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId, @TrabajoId AS TrabajoId, NULL AS MontoFinal; RETURN; END

    DECLARE @PagoId INT, @PagoEstado INT, @PagoMonto DECIMAL(10,2);
    SELECT @PagoId = Id, @PagoEstado = Estado, @PagoMonto = Monto FROM Pagos WHERE TrabajoId = @TrabajoId;
    IF @PagoEstado = 2 AND @PagoMonto <> @MontoFinal
    BEGIN SELECT 'PAGO_REALIZADO' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId, @TrabajoId AS TrabajoId, NULL AS MontoFinal; RETURN; END

    BEGIN TRAN;
        UPDATE Trabajos SET Estado = 4, FechaFin = ISNULL(FechaFin, @Fecha),
               JustificacionFinalizacion = ISNULL(JustificacionFinalizacion, N'Cerrado por resolución de disputa.')
        WHERE Id = @TrabajoId;

        IF @MontoFinal = 0
            DELETE FROM Pagos WHERE Id = @PagoId AND Estado IN (1, 3, 4);
        ELSE IF @PagoId IS NULL
            INSERT INTO Pagos (TrabajoId, CuidadorId, Monto, Propina, Estado, FechaCreacion, ComisionPorcentaje)
            VALUES (@TrabajoId, @CuidadorId, @MontoFinal, 0, 1, @Fecha, dbo.fn_ComisionServicio(@Tipo));
        ELSE IF @PagoEstado <> 2
            UPDATE Pagos SET Monto = @MontoFinal WHERE Id = @PagoId;

        EXEC dbo.sp_RecalcularPagoEfectivo @TrabajoId, @Fecha;

        UPDATE Disputas SET Estado = 2, Resolucion = @Resolucion, MontoFinal = @MontoFinal, Nota = @Nota,
               ResueltaPorAdminId = @AdminId, FechaResolucion = @Fecha
        WHERE Id = @DisputaId;
    COMMIT;

    SELECT 'OK' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId, @TrabajoId AS TrabajoId, @MontoFinal AS MontoFinal;
END
GO

-- ============================================================================
-- Autorizar un pago por transferencia: descuenta la deuda de efectivo del Care Partner.
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_AutorizarPago
    @PagoId INT,
    @AdminId INT,
    @FechaHora DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @CuidadorId INT, @Monto DECIMAL(10,2), @Propina DECIMAL(10,2), @Pct DECIMAL(5,2), @TrabajoId INT;
    SELECT @CuidadorId = p.CuidadorId, @Monto = p.Monto, @Propina = p.Propina, @TrabajoId = p.TrabajoId,
           @Pct = ISNULL(p.ComisionPorcentaje, dbo.fn_ComisionServicio(t.TipoServicio))
    FROM Pagos p JOIN Trabajos t ON t.Id = p.TrabajoId
    WHERE p.Id = @PagoId AND p.Estado = 1;

    IF @CuidadorId IS NULL BEGIN SELECT 0 AS FilasAfectadas; RETURN; END

    DECLARE @Neto DECIMAL(10,2) = @Monto - ROUND(@Monto * @Pct / 100, 2) + @Propina;
    DECLARE @Saldo DECIMAL(10,2) = dbo.fn_SaldoCuidador(@CuidadorId);
    DECLARE @Descuento DECIMAL(10,2) = CASE WHEN @Saldo <= 0 OR @Neto <= 0 THEN 0 WHEN @Saldo < @Neto THEN @Saldo ELSE @Neto END;

    BEGIN TRAN;
        UPDATE Pagos
        SET Estado = 3, AutorizadoPorAdminId = @AdminId, FechaAutorizacion = @FechaHora, DescuentoSaldo = @Descuento
        WHERE Id = @PagoId AND Estado = 1;
        DECLARE @Filas INT = @@ROWCOUNT;
        IF @Filas = 1 AND @Descuento > 0
            INSERT INTO MovimientosSaldoCuidador (CuidadorId, TrabajoId, PagoId, Tipo, Monto, Nota, AdminId, Fecha)
            VALUES (@CuidadorId, @TrabajoId, @PagoId, 'descuento_pago', -@Descuento, N'Descontado del pago por transferencia', @AdminId, @FechaHora);
    COMMIT;
    SELECT @Filas AS FilasAfectadas;
END
GO

-- ============================================================================
-- Lecturas que ahora incluyen método, comisión, descuento y saldo
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerPagosAdmin
    @Estado INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.Id, p.TrabajoId, p.CuidadorId, cu.NombreCompleto AS CuidadorNombre,
        t.ClienteId, cl.NombreCompleto AS ClienteNombre, t.TipoServicio,
        p.Monto, p.Estado, p.FechaCreacion,
        p.AutorizadoPorAdminId, adminAut.NombreCompleto AS AutorizadoPorNombre, p.FechaAutorizacion,
        p.AprobadoPorAdminId, adminApr.NombreCompleto AS AprobadoPorNombre, p.FechaPago,
        t.PagoDisputado, p.Propina,
        k.Pct AS ComisionPorcentaje,
        ROUND(p.Monto * k.Pct / 100, 2) AS Comision,
        p.DescuentoSaldo,
        p.Monto - ROUND(p.Monto * k.Pct / 100, 2) + p.Propina - p.DescuentoSaldo AS NetoCuidador,
        t.MetodoPago,
        dbo.fn_SaldoCuidador(p.CuidadorId) AS SaldoCuidador
    FROM Pagos p
    INNER JOIN Trabajos t ON t.Id = p.TrabajoId
    INNER JOIN Usuarios cu ON cu.Id = p.CuidadorId
    INNER JOIN Usuarios cl ON cl.Id = t.ClienteId
    LEFT JOIN Usuarios adminAut ON adminAut.Id = p.AutorizadoPorAdminId
    LEFT JOIN Usuarios adminApr ON adminApr.Id = p.AprobadoPorAdminId
    CROSS APPLY (SELECT ISNULL(p.ComisionPorcentaje, dbo.fn_ComisionServicio(t.TipoServicio)) AS Pct) k
    WHERE @Estado IS NULL OR p.Estado = @Estado
    ORDER BY p.FechaCreacion DESC;
END
GO

-- Finanzas (igual que antes) + efectivo y comisiones por cobrar. "Pagado"/"Por pagar" son transferencias.
CREATE OR ALTER PROCEDURE dbo.sp_AdminFinanzas @Desde DATE, @Hasta DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT t.Id, t.CuidadorId, t.Tarifa, t.Fecha, t.MetodoPago, t.PagoDisputado,
           ISNULL(p.Propina, 0) AS Propina, p.Estado AS EstadoPago, ISNULL(p.Monto, t.Tarifa) AS Monto,
           ISNULL(p.DescuentoSaldo, 0) AS Descuento,
           ISNULL(p.ComisionPorcentaje, dbo.fn_ComisionServicio(t.TipoServicio)) AS Pct
    INTO #S
    FROM Trabajos t LEFT JOIN Pagos p ON p.TrabajoId = t.Id
    WHERE t.Estado = 4 AND t.Fecha BETWEEN @Desde AND @Hasta;

    SELECT
        (SELECT COUNT(*) FROM #S) AS ServiciosCompletados,
        (SELECT COUNT(*) FROM Trabajos WHERE Estado = 5 AND Fecha BETWEEN @Desde AND @Hasta) AS ServiciosCancelados,
        (SELECT ISNULL(SUM(Tarifa), 0) FROM #S) AS Facturado,
        (SELECT ISNULL(SUM(Propina), 0) FROM #S) AS Propinas,
        (SELECT ISNULL(SUM(ROUND(Monto * Pct / 100, 2)), 0) FROM #S) AS Comision,
        (SELECT ISNULL(SUM(Monto - ROUND(Monto * Pct / 100, 2) + Propina - Descuento), 0) FROM #S WHERE EstadoPago = 2) AS Pagado,
        (SELECT ISNULL(SUM(Monto - ROUND(Monto * Pct / 100, 2) + Propina - Descuento), 0) FROM #S WHERE EstadoPago IN (1, 3)) AS PorPagar,
        (SELECT COUNT(*) FROM #S WHERE PagoDisputado = 1) AS Disputados,
        (SELECT COUNT(*) FROM #S WHERE MetodoPago IS NULL) AS SinMetodo,
        dbo.fn_ComisionServicio(NULL) AS ComisionGeneral,
        (SELECT ISNULL(SUM(Monto + Propina), 0) FROM #S WHERE EstadoPago = 4) AS CobradoEfectivo,
        (SELECT ISNULL(SUM(s.Saldo), 0) FROM (SELECT SUM(Monto) AS Saldo FROM MovimientosSaldoCuidador GROUP BY CuidadorId) s WHERE s.Saldo > 0) AS ComisionPorCobrar,
        dbo.fn_TopeDeudaEfectivo() AS TopeDeudaEfectivo;

    SELECT u.Id AS UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, pc.MetodoCobro,
           COUNT(*) AS Servicios,
           SUM(s.Tarifa) AS Facturado,
           SUM(s.Propina) AS Propinas,
           SUM(ROUND(s.Monto * s.Pct / 100, 2)) AS Comision,
           SUM(IIF(s.EstadoPago = 2, s.Monto - ROUND(s.Monto * s.Pct / 100, 2) + s.Propina - s.Descuento, 0)) AS Pagado,
           SUM(IIF(s.EstadoPago IN (1, 3), s.Monto - ROUND(s.Monto * s.Pct / 100, 2) + s.Propina - s.Descuento, 0)) AS PorPagar,
           SUM(IIF(s.EstadoPago = 4, s.Monto + s.Propina, 0)) AS Efectivo,
           dbo.fn_SaldoCuidador(u.Id) AS Saldo
    FROM #S s
    JOIN Usuarios u ON u.Id = s.CuidadorId
    LEFT JOIN PerfilCuidador pc ON pc.UsuarioId = u.Id
    GROUP BY u.Id, u.NombreCompleto, u.Email, pc.MetodoCobro
    ORDER BY Facturado DESC;

    SELECT Fecha AS Dia, COUNT(*) AS Servicios, SUM(Tarifa) AS Facturado FROM #S GROUP BY Fecha ORDER BY Fecha;

    SELECT ISNULL(MetodoPago, 'sin_registrar') AS Metodo, COUNT(*) AS Servicios, SUM(Tarifa) AS Facturado
    FROM #S GROUP BY MetodoPago ORDER BY Facturado DESC;
END
GO

-- Detalle del servicio (igual que en caja-negra-grabacion.sql + DescuentoSaldo del pago)
CREATE OR ALTER PROCEDURE dbo.sp_AdminTrabajoDetalle @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.Id, t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas,
           t.Latitud, t.Longitud, t.FechaCreacion, t.FechaLlegada, t.FechaInicioReal, t.FechaFin, t.JustificacionFinalizacion,
           t.RechazadoPorCliente, t.PagoDisputado, t.MetodoPago, t.GrabacionActiva,
           ISNULL(m.Descripcion, t.MotivoCancelacionTexto) AS MotivoCancelacion, t.MotivoCancelacionTexto,
           t.ClienteId, ISNULL(c.NombreCompleto, c.Email) AS ClienteNombre, c.Email AS ClienteEmail, c.Telefono AS ClienteTelefono, c.FotoUrl AS ClienteFoto,
           t.CuidadorId, ISNULL(q.NombreCompleto, q.Email) AS CuidadorNombre, q.Email AS CuidadorEmail, q.Telefono AS CuidadorTelefono, q.FotoUrl AS CuidadorFoto,
           (SELECT TOP 1 d.Id FROM Disputas d WHERE d.TrabajoId = t.Id) AS DisputaId
    FROM Trabajos t
    JOIN Usuarios c ON c.Id = t.ClienteId
    JOIN Usuarios q ON q.Id = t.CuidadorId
    LEFT JOIN MotivosCancelacion m ON m.Id = t.MotivoCancelacionId
    WHERE t.Id = @TrabajoId;

    SELECT Id, Descripcion, Orden, Completada, FechaCompletada FROM TareasTrabajo WHERE TrabajoId = @TrabajoId ORDER BY Orden;
    SELECT Id, Descripcion, FechaHora FROM ActividadesTrabajo WHERE TrabajoId = @TrabajoId ORDER BY FechaHora;
    SELECT p.Id, p.Monto, p.Propina, p.Estado, p.FechaCreacion, p.FechaAutorizacion, p.FechaPago, p.DescuentoSaldo,
           ISNULL(p.ComisionPorcentaje, dbo.fn_ComisionServicio(t.TipoServicio)) AS ComisionPorcentaje
    FROM Pagos p JOIN Trabajos t ON t.Id = p.TrabajoId WHERE p.TrabajoId = @TrabajoId;
    SELECT c.Id, c.CalificadorId, ISNULL(u.NombreCompleto, u.Email) AS CalificadorNombre, c.Puntuacion, c.Comentario, c.FechaCreacion
    FROM Calificaciones c JOIN Usuarios u ON u.Id = c.CalificadorId WHERE c.TrabajoId = @TrabajoId;
    SELECT m.Id, m.RemitenteId, ISNULL(u.NombreCompleto, u.Email) AS RemitenteNombre, m.Contenido, m.Tipo, m.UrlArchivo,
           m.DuracionSegundos, m.FechaEnvio, m.Eliminado
    FROM Mensajes m JOIN Conversaciones cv ON cv.ID = m.ConversacionId JOIN Usuarios u ON u.Id = m.RemitenteId
    WHERE cv.TrabajoID = @TrabajoId ORDER BY m.FechaEnvio;
    SELECT Id, TipoUsuario, Motivo, Estado, Origen, FechaCreacion, AtendidoPor FROM SOSAlertas WHERE TrabajoId = @TrabajoId ORDER BY FechaCreacion;
    SELECT Id, Asunto, Categoria, Estado, FechaCreacion FROM Tickets WHERE TrabajoId = @TrabajoId ORDER BY FechaCreacion;
END
GO

-- ---------- App: servicios del Care Partner (igual que antes + MetodoPago)
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerTrabajosPorCuidador
    @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.Id, t.ClienteId, c.NombreCompleto AS ClienteNombre, c.FotoUrl AS ClienteFotoUrl,
           t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas, t.FechaCreacion,
           t.Latitud, t.Longitud, t.RechazadoPorCliente, t.PagoDisputado, t.MetodoPago
    FROM Trabajos t
    JOIN Usuarios c ON c.Id = t.ClienteId
    WHERE t.CuidadorId = @CuidadorId
    ORDER BY t.Fecha DESC, t.HoraInicio DESC;
END;
GO

-- ---------- App: servicio del cliente (igual que antes + MetodoPago)
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerTrabajoPorId
    @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        t.Id, t.ClienteId, t.CuidadorId, u.NombreCompleto AS CuidadorNombre, u.FotoUrl AS CuidadorFotoUrl,
        t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas, t.FechaCreacion,
        t.PinInicio, t.PinFin, t.FechaInicioReal, t.JustificacionFinalizacion, t.RechazadoPorCliente, t.MetodoPago
    FROM Trabajos t
    JOIN Usuarios u ON u.Id = t.CuidadorId
    WHERE t.Id = @TrabajoId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerTrabajosActivosPorCliente
    @ClienteId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        t.Id, t.ClienteId, t.CuidadorId, u.NombreCompleto AS CuidadorNombre, u.FotoUrl AS CuidadorFotoUrl,
        t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas, t.FechaCreacion,
        t.PinInicio, t.PinFin, t.FechaInicioReal, t.JustificacionFinalizacion, t.RechazadoPorCliente, t.MetodoPago
    FROM Trabajos t
    JOIN Usuarios u ON u.Id = t.CuidadorId
    WHERE t.ClienteId = @ClienteId
      AND (
            t.Estado IN (1,2,3,7)
            OR (t.Estado = 4 AND t.FechaCreacion >= DATEADD(DAY, -3, GETDATE())
                AND NOT EXISTS (SELECT 1 FROM Calificaciones c WHERE c.TrabajoId = t.Id AND c.CalificadorId = @ClienteId))
          )
    ORDER BY t.FechaCreacion DESC;
END;
GO

-- ---------- App: búsqueda (igual que antes + si puede tomar servicios en efectivo ahora)
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerCuidadoresPorServicio
    @Especialidad NVARCHAR(100),
    @Latitud DECIMAL(9,6),
    @Longitud DECIMAL(9,6),
    @RadioKm DECIMAL(9,2)
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Distancias AS (
        SELECT
            u.Id, u.NombreCompleto, u.FotoUrl,
            p.Especialidad, p.TarifaHora, p.Bio, p.Latitud, p.Longitud,
            dbo.fn_PuedeAceptarEfectivo(u.Id) AS PuedeAceptarEfectivo,
            (6371 * ACOS(
                CASE WHEN ABS(SIN(RADIANS(@Latitud)) * SIN(RADIANS(p.Latitud))
                    + COS(RADIANS(@Latitud)) * COS(RADIANS(p.Latitud)) * COS(RADIANS(p.Longitud - @Longitud))) > 1
                     THEN SIGN(SIN(RADIANS(@Latitud)) * SIN(RADIANS(p.Latitud))
                    + COS(RADIANS(@Latitud)) * COS(RADIANS(p.Latitud)) * COS(RADIANS(p.Longitud - @Longitud)))
                     ELSE SIN(RADIANS(@Latitud)) * SIN(RADIANS(p.Latitud))
                    + COS(RADIANS(@Latitud)) * COS(RADIANS(p.Latitud)) * COS(RADIANS(p.Longitud - @Longitud))
                END
            )) AS DistanciaKm
        FROM PerfilCuidador p
        JOIN Usuarios u ON u.Id = p.UsuarioId
        WHERE p.EstadoAprobacion = 2
          AND p.Disponible = 1
          AND p.Especialidad = @Especialidad
          AND p.Latitud IS NOT NULL AND p.Longitud IS NOT NULL
    )
    SELECT * FROM Distancias
    WHERE DistanciaKm <= @RadioKm
    ORDER BY DistanciaKm;
END;
GO

-- ---------- App: pagos y ganancias del Care Partner (netos de comisión; efectivo cuenta como cobrado)
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerPagosPorCuidador
    @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.Id, p.TrabajoId, t.TipoServicio, c.NombreCompleto AS ClienteNombre,
           p.Monto, p.Estado, p.FechaPago, p.FechaCreacion, p.Propina,
           ROUND(p.Monto * k.Pct / 100, 2) AS Comision, p.DescuentoSaldo, t.MetodoPago,
           IIF(p.Estado = 4, p.Monto + p.Propina, p.Monto - ROUND(p.Monto * k.Pct / 100, 2) + p.Propina - p.DescuentoSaldo) AS Neto
    FROM Pagos p
    JOIN Trabajos t ON t.Id = p.TrabajoId
    JOIN Usuarios c ON c.Id = t.ClienteId
    CROSS APPLY (SELECT ISNULL(p.ComisionPorcentaje, dbo.fn_ComisionServicio(t.TipoServicio)) AS Pct) k
    WHERE p.CuidadorId = @CuidadorId
    ORDER BY p.FechaCreacion DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerGananciasCuidador
    @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(DATEADD(HOUR, -4, GETUTCDATE()) AS DATE);
    WITH N AS (
        SELECT p.Estado, p.FechaPago, p.FechaCreacion,
               IIF(p.Estado = 4, p.Monto + p.Propina,
                   p.Monto - ROUND(p.Monto * ISNULL(p.ComisionPorcentaje, dbo.fn_ComisionServicio(t.TipoServicio)) / 100, 2) + p.Propina - p.DescuentoSaldo) AS Neto
        FROM Pagos p JOIN Trabajos t ON t.Id = p.TrabajoId
        WHERE p.CuidadorId = @CuidadorId
    )
    SELECT
        ISNULL((SELECT SUM(Neto) FROM N WHERE (Estado = 2 AND CAST(FechaPago AS DATE) = @Hoy) OR (Estado = 4 AND CAST(FechaCreacion AS DATE) = @Hoy)), 0) AS GanadoHoy,
        ISNULL((SELECT SUM(Neto) FROM N WHERE Estado IN (1, 3)), 0) AS PendientePorCobrar,
        ISNULL((SELECT SUM(Neto) FROM N WHERE Estado IN (2, 4)), 0) AS TotalCobrado;
END;
GO

-- ---------- App: saldo del Care Partner con CuidApp
CREATE OR ALTER PROCEDURE dbo.sp_SaldoCuidador @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT dbo.fn_SaldoCuidador(@CuidadorId) AS Saldo, dbo.fn_TopeDeudaEfectivo() AS Tope,
           dbo.fn_PuedeAceptarEfectivo(@CuidadorId) AS PuedeAceptarEfectivo;
    SELECT TOP 30 m.Id, m.Tipo, m.Monto, m.Nota, m.Fecha, m.TrabajoId, t.TipoServicio
    FROM MovimientosSaldoCuidador m LEFT JOIN Trabajos t ON t.Id = m.TrabajoId
    WHERE m.CuidadorId = @CuidadorId
    ORDER BY m.Fecha DESC, m.Id DESC;
END
GO

-- ---------- Instrucciones para pagar por transferencia (las edita Finanzas; las ve el cliente)
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerInstruccionesTransferencia
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL((SELECT Valor FROM dbo.ConfiguracionPlataforma WHERE Clave = 'InstruccionesTransferencia'), '') AS Instrucciones;
END
GO

-- ---------- Panel: saldos, abonos y configuración
CREATE OR ALTER PROCEDURE dbo.sp_AdminSaldosCuidadores
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.Id AS UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, u.FotoUrl, u.Telefono,
           SUM(m.Monto) AS Saldo,
           SUM(IIF(m.Tipo = 'comision_efectivo', 1, 0)) AS ServiciosEfectivo,
           MAX(m.Fecha) AS UltimoMovimiento,
           dbo.fn_PuedeAceptarEfectivo(u.Id) AS PuedeAceptarEfectivo
    FROM MovimientosSaldoCuidador m
    JOIN Usuarios u ON u.Id = m.CuidadorId
    GROUP BY u.Id, u.NombreCompleto, u.Email, u.FotoUrl, u.Telefono
    HAVING SUM(m.Monto) <> 0
    ORDER BY SUM(m.Monto) DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminMovimientosSaldo @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT m.Id, m.Tipo, m.Monto, m.Nota, m.Fecha, m.TrabajoId, m.PagoId, t.TipoServicio,
           ISNULL(a.NombreCompleto, a.Email) AS AdminNombre
    FROM MovimientosSaldoCuidador m
    LEFT JOIN Trabajos t ON t.Id = m.TrabajoId
    LEFT JOIN Usuarios a ON a.Id = m.AdminId
    WHERE m.CuidadorId = @CuidadorId
    ORDER BY m.Fecha DESC, m.Id DESC;
END
GO

-- El Care Partner pagó (parte de) su deuda directo a CuidApp.
CREATE OR ALTER PROCEDURE dbo.sp_AdminRegistrarAbono @CuidadorId INT, @Monto DECIMAL(10,2), @Nota NVARCHAR(300), @AdminId INT, @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Saldo DECIMAL(10,2) = dbo.fn_SaldoCuidador(@CuidadorId);
    IF @Monto IS NULL OR @Monto <= 0 OR @Monto > @Saldo BEGIN SELECT 'MONTO_INVALIDO' AS Resultado; RETURN; END
    INSERT INTO MovimientosSaldoCuidador (CuidadorId, Tipo, Monto, Nota, AdminId, Fecha)
    VALUES (@CuidadorId, 'abono', -@Monto, ISNULL(NULLIF(LTRIM(RTRIM(@Nota)), ''), N'Abono registrado por la administración'), @AdminId, @Fecha);
    SELECT 'OK' AS Resultado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminObtenerConfigPagos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT dbo.fn_TopeDeudaEfectivo() AS TopeDeudaEfectivo,
           ISNULL((SELECT Valor FROM dbo.ConfiguracionPlataforma WHERE Clave = 'InstruccionesTransferencia'), '') AS InstruccionesTransferencia;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminGuardarConfigPagos @TopeDeudaEfectivo DECIMAL(10,2), @InstruccionesTransferencia NVARCHAR(1000), @AdminId INT, @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    IF @TopeDeudaEfectivo IS NULL OR @TopeDeudaEfectivo < 0 BEGIN SELECT 'MONTO_INVALIDO' AS Resultado; RETURN; END
    UPDATE dbo.ConfiguracionPlataforma SET Valor = CAST(@TopeDeudaEfectivo AS NVARCHAR(20)), AdminId = @AdminId, FechaActualizacion = @Fecha WHERE Clave = 'TopeDeudaEfectivo';
    UPDATE dbo.ConfiguracionPlataforma SET Valor = ISNULL(@InstruccionesTransferencia, ''), AdminId = @AdminId, FechaActualizacion = @Fecha WHERE Clave = 'InstruccionesTransferencia';
    SELECT 'OK' AS Resultado;
END
GO
