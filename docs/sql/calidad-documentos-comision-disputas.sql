-- ============================================================================
-- Panel: calidad de Care Partners (semáforo), documentos que vencen, comisión de la
-- plataforma + método de pago del servicio, y disputas como flujo propio.
-- Correr DESPUÉS de panel-admin.sql, horario-cuidador.sql y centro-mando.sql:
-- reemplaza sp_ConfirmarFinalizacionTrabajo, sp_AdminCompletarTrabajo, sp_AdminTrabajoDetalle,
-- sp_AdminFinanzas, sp_ObtenerPagosAdmin, sp_ReemplazarDocumentoCuidador,
-- sp_ActualizarDisponibilidadCuidador y sp_AplicarHorariosCuidadores.
-- Idempotente. Requiere QUOTED_IDENTIFIER ON.
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ============================================================================
-- 1. COMISIÓN Y MÉTODO DE PAGO
-- ============================================================================
IF OBJECT_ID('dbo.ConfiguracionPlataforma') IS NULL
    CREATE TABLE dbo.ConfiguracionPlataforma (
        Clave               NVARCHAR(60)  NOT NULL PRIMARY KEY,
        Valor               NVARCHAR(200) NOT NULL,
        FechaActualizacion  DATETIME      NOT NULL DEFAULT GETDATE(),
        AdminId             INT           NULL
    );
GO
-- Arranca en 0 %: la administración la fija desde Finanzas.
IF NOT EXISTS (SELECT 1 FROM dbo.ConfiguracionPlataforma WHERE Clave = 'ComisionPorcentaje')
    INSERT INTO dbo.ConfiguracionPlataforma (Clave, Valor) VALUES ('ComisionPorcentaje', '0');
GO
IF COL_LENGTH('dbo.TiposTrabajos', 'ComisionPorcentaje') IS NULL
    ALTER TABLE dbo.TiposTrabajos ADD ComisionPorcentaje DECIMAL(5,2) NULL;   -- NULL = usa la general
GO
-- Porcentaje con el que se liquidó cada pago (si luego cambia la comisión, los pagos viejos no cambian).
-- Los pagos que ya existían se hicieron sin comisión: quedan en 0 % para que los reportes viejos no cambien.
IF COL_LENGTH('dbo.Pagos', 'ComisionPorcentaje') IS NULL
BEGIN
    ALTER TABLE dbo.Pagos ADD ComisionPorcentaje DECIMAL(5,2) NULL;
    EXEC('UPDATE dbo.Pagos SET ComisionPorcentaje = 0');
END
GO
-- Cómo pagó el cliente: 'efectivo', 'tarjeta' o 'transferencia' (NULL = sin registrar).
IF COL_LENGTH('dbo.Trabajos', 'MetodoPago') IS NULL
    ALTER TABLE dbo.Trabajos ADD MetodoPago VARCHAR(20) NULL;
GO

-- Comisión vigente para un tipo de servicio (la del tipo o, si no tiene, la general).
CREATE OR ALTER FUNCTION dbo.fn_ComisionServicio (@TipoServicio NVARCHAR(100))
RETURNS DECIMAL(5,2)
AS
BEGIN
    DECLARE @Pct DECIMAL(5,2) = (SELECT TOP 1 ComisionPorcentaje FROM dbo.TiposTrabajos WHERE Nombre = @TipoServicio AND ComisionPorcentaje IS NOT NULL);
    IF @Pct IS NULL
        SET @Pct = (SELECT TRY_CAST(Valor AS DECIMAL(5,2)) FROM dbo.ConfiguracionPlataforma WHERE Clave = 'ComisionPorcentaje');
    RETURN ISNULL(@Pct, 0);
END
GO

-- El pago a un cuidador = Monto (tarifa) + Propina. Se crea al confirmar el cliente.
-- (Igual que antes + guarda la comisión vigente.)
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

-- ---------- Dar por completado un servicio desde soporte: crea el pago pendiente si no existe
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
    COMMIT;
    SELECT 'OK' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId;
END
GO

-- ---------- Detalle de un servicio (igual que antes + método de pago y comisión del pago)
CREATE OR ALTER PROCEDURE dbo.sp_AdminTrabajoDetalle @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.Id, t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas,
           t.Latitud, t.Longitud, t.FechaCreacion, t.FechaInicioReal, t.FechaFin, t.JustificacionFinalizacion,
           t.RechazadoPorCliente, t.PagoDisputado, t.MetodoPago,
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
    SELECT p.Id, p.Monto, p.Propina, p.Estado, p.FechaCreacion, p.FechaAutorizacion, p.FechaPago,
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

-- ---------- Registrar cómo pagó el cliente
CREATE OR ALTER PROCEDURE dbo.sp_AdminFijarMetodoPago @TrabajoId INT, @MetodoPago VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @MetodoPago IS NOT NULL AND @MetodoPago NOT IN ('efectivo', 'tarjeta', 'transferencia')
    BEGIN SELECT 'METODO_INVALIDO' AS Resultado; RETURN; END
    UPDATE Trabajos SET MetodoPago = @MetodoPago WHERE Id = @TrabajoId;
    SELECT IIF(@@ROWCOUNT = 0, 'NO_ENCONTRADO', 'OK') AS Resultado;
END
GO

-- ---------- Comisión: lectura y cambios
CREATE OR ALTER PROCEDURE dbo.sp_AdminObtenerComisiones
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TRY_CAST(Valor AS DECIMAL(5,2)) AS Porcentaje, FechaActualizacion, ISNULL(u.NombreCompleto, u.Email) AS AdminNombre
    FROM ConfiguracionPlataforma c LEFT JOIN Usuarios u ON u.Id = c.AdminId
    WHERE Clave = 'ComisionPorcentaje';
    SELECT Id, Nombre, Activo, ComisionPorcentaje FROM TiposTrabajos ORDER BY Nombre;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminGuardarComisionGeneral @Porcentaje DECIMAL(5,2), @AdminId INT, @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    IF @Porcentaje IS NULL OR @Porcentaje < 0 OR @Porcentaje > 100 BEGIN SELECT 'PORCENTAJE_INVALIDO' AS Resultado; RETURN; END
    UPDATE ConfiguracionPlataforma SET Valor = CAST(@Porcentaje AS NVARCHAR(20)), AdminId = @AdminId, FechaActualizacion = @Fecha
    WHERE Clave = 'ComisionPorcentaje';
    IF @@ROWCOUNT = 0
        INSERT INTO ConfiguracionPlataforma (Clave, Valor, AdminId, FechaActualizacion) VALUES ('ComisionPorcentaje', CAST(@Porcentaje AS NVARCHAR(20)), @AdminId, @Fecha);
    SELECT 'OK' AS Resultado;
END
GO

-- @Porcentaje NULL = el tipo vuelve a usar la comisión general.
CREATE OR ALTER PROCEDURE dbo.sp_AdminGuardarComisionTipo @TipoId INT, @Porcentaje DECIMAL(5,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Porcentaje IS NOT NULL AND (@Porcentaje < 0 OR @Porcentaje > 100) BEGIN SELECT 'PORCENTAJE_INVALIDO' AS Resultado; RETURN; END
    UPDATE TiposTrabajos SET ComisionPorcentaje = @Porcentaje WHERE Id = @TipoId;
    SELECT IIF(@@ROWCOUNT = 0, 'NO_ENCONTRADO', 'OK') AS Resultado;
END
GO

-- ---------- Finanzas del período (4 resultados: totales, por Care Partner, por día, por método de pago)
-- La comisión se cobra sobre la tarifa; la propina va íntegra al Care Partner.
CREATE OR ALTER PROCEDURE dbo.sp_AdminFinanzas @Desde DATE, @Hasta DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Servicios completados del período con su comisión (la del pago o, si aún no hay pago, la vigente).
    SELECT t.Id, t.CuidadorId, t.Tarifa, t.Fecha, t.MetodoPago, t.PagoDisputado,
           ISNULL(p.Propina, 0) AS Propina, p.Estado AS EstadoPago, ISNULL(p.Monto, t.Tarifa) AS Monto,
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
        (SELECT ISNULL(SUM(Monto - ROUND(Monto * Pct / 100, 2) + Propina), 0) FROM #S WHERE EstadoPago = 2) AS Pagado,
        (SELECT ISNULL(SUM(Monto - ROUND(Monto * Pct / 100, 2) + Propina), 0) FROM #S WHERE EstadoPago IN (1, 3)) AS PorPagar,
        (SELECT COUNT(*) FROM #S WHERE PagoDisputado = 1) AS Disputados,
        (SELECT COUNT(*) FROM #S WHERE MetodoPago IS NULL) AS SinMetodo,
        dbo.fn_ComisionServicio(NULL) AS ComisionGeneral;

    SELECT u.Id AS UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, pc.MetodoCobro,
           COUNT(*) AS Servicios,
           SUM(s.Tarifa) AS Facturado,
           SUM(s.Propina) AS Propinas,
           SUM(ROUND(s.Monto * s.Pct / 100, 2)) AS Comision,
           SUM(IIF(s.EstadoPago = 2, s.Monto - ROUND(s.Monto * s.Pct / 100, 2) + s.Propina, 0)) AS Pagado,
           SUM(IIF(s.EstadoPago IN (1, 3), s.Monto - ROUND(s.Monto * s.Pct / 100, 2) + s.Propina, 0)) AS PorPagar
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

-- ---------- Pagos del panel (igual que antes + comisión y neto a pagar al Care Partner)
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
        p.Propina,
        k.Pct AS ComisionPorcentaje,
        ROUND(p.Monto * k.Pct / 100, 2) AS Comision,
        p.Monto - ROUND(p.Monto * k.Pct / 100, 2) + p.Propina AS NetoCuidador,
        t.MetodoPago
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

-- ============================================================================
-- 2. DOCUMENTOS QUE VENCEN
-- ============================================================================
IF COL_LENGTH('dbo.DocumentosCuidador', 'FechaVencimiento') IS NULL
    ALTER TABLE dbo.DocumentosCuidador ADD
        FechaVencimiento  DATE    NULL,
        AvisoVencimiento  TINYINT NOT NULL CONSTRAINT DF_DocumentosCuidador_AvisoVencimiento DEFAULT 0;  -- 0 ninguno, 1 aviso 30 días, 2 aviso 7 días, 3 vencido
GO
-- Los avisos directos del sistema (vencimientos, disputas) no tienen administrador.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AvisosDirectos') AND name = 'AdminId' AND is_nullable = 0)
    ALTER TABLE dbo.AvisosDirectos ALTER COLUMN AdminId INT NULL;
GO

-- Un documento con fecha de vencimiento pasada bloquea la visibilidad hasta que se apruebe uno vigente.
CREATE OR ALTER FUNCTION dbo.fn_TieneDocumentoVencido (@CuidadorId INT, @Hoy DATE)
RETURNS BIT
AS
BEGIN
    RETURN IIF(EXISTS (SELECT 1 FROM dbo.DocumentosCuidador WHERE CuidadorId = @CuidadorId AND FechaVencimiento < @Hoy), 1, 0);
END
GO

-- Interruptor manual de visibilidad (igual que antes + no deja ponerse visible con un documento vencido
-- ni con la cuenta suspendida).
CREATE OR ALTER PROCEDURE dbo.sp_ActualizarDisponibilidadCuidador
    @CuidadorId INT,
    @Disponible BIT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hoy DATE = CAST(DATEADD(HOUR, -4, GETUTCDATE()) AS DATE);
    UPDATE PerfilCuidador SET Disponible = @Disponible
    WHERE UsuarioId = @CuidadorId AND HorarioAutomatico = 0
      AND (@Disponible = 0 OR (EstadoAprobacion = 2 AND dbo.fn_TieneDocumentoVencido(@CuidadorId, @Hoy) = 0
                               AND EXISTS (SELECT 1 FROM Usuarios u WHERE u.Id = @CuidadorId AND u.IsActive = 1)));
    SELECT @@ROWCOUNT AS FilasAfectadas;
END;
GO

-- Horario automático (igual que antes + con un documento vencido queda oculto).
CREATE OR ALTER PROCEDURE dbo.sp_AplicarHorariosCuidadores @Ahora DATETIME, @CuidadorId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    -- 1900-01-07 fue domingo: así el día no depende de SET DATEFIRST.
    DECLARE @Dia TINYINT = DATEDIFF(DAY, '19000107', @Ahora) % 7;
    DECLARE @Hora TIME(0) = CAST(@Ahora AS TIME(0));
    DECLARE @Hoy DATE = CAST(@Ahora AS DATE);

    DECLARE @Cambios TABLE (CuidadorId INT, Disponible BIT);

    UPDATE pc
    SET Disponible = d.Debe
    OUTPUT inserted.UsuarioId, inserted.Disponible INTO @Cambios
    FROM PerfilCuidador pc
    JOIN Usuarios u ON u.Id = pc.UsuarioId
    CROSS APPLY (SELECT CAST(IIF(pc.EstadoAprobacion = 2 AND u.IsActive = 1
                     AND dbo.fn_TieneDocumentoVencido(pc.UsuarioId, @Hoy) = 0
                     AND EXISTS (
                     SELECT 1 FROM HorarioCuidador h
                     WHERE h.CuidadorId = pc.UsuarioId AND h.DiaSemana = @Dia AND @Hora >= h.HoraInicio AND @Hora < h.HoraFin), 1, 0) AS BIT) AS Debe) d
    WHERE pc.HorarioAutomatico = 1
      AND (@CuidadorId IS NULL OR pc.UsuarioId = @CuidadorId)
      AND pc.Disponible <> d.Debe;

    SELECT CuidadorId, Disponible FROM @Cambios;
END
GO

-- Reemplazar un documento RECHAZADO (también los vencidos, que pasan a rechazados).
-- Igual que antes; la fecha de vencimiento vieja se queda hasta que la administración apruebe el nuevo.
CREATE OR ALTER PROCEDURE dbo.sp_ReemplazarDocumentoCuidador
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

-- ---------- Lista para el panel: documentos de Care Partners aprobados (con o sin fecha) y renovaciones por revisar
CREATE OR ALTER PROCEDURE dbo.sp_AdminDocumentosVencimiento @Hoy DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT d.Id, d.CuidadorId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, u.FotoUrl, u.Telefono,
           d.TipoDocumento, d.UrlArchivo, d.Estado, d.ObservacionesAdmin, d.FechaSubida, d.FechaVencimiento,
           DATEDIFF(DAY, @Hoy, d.FechaVencimiento) AS DiasRestantes, d.AvisoVencimiento,
           pc.Disponible
    FROM DocumentosCuidador d
    JOIN Usuarios u ON u.Id = d.CuidadorId
    JOIN PerfilCuidador pc ON pc.UsuarioId = d.CuidadorId
    WHERE u.IsActive = 1 AND pc.EstadoAprobacion = 2
    ORDER BY IIF(d.FechaVencimiento IS NULL, 1, 0), d.FechaVencimiento, Nombre;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminFijarVencimientoDocumento @DocumentoId INT, @Fecha DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE DocumentosCuidador SET FechaVencimiento = @Fecha, AvisoVencimiento = 0 WHERE Id = @DocumentoId;
    SELECT IIF(@@ROWCOUNT = 0, 'NO_ENCONTRADO', 'OK') AS Resultado;
END
GO

-- Revisar un documento renovado: aprobar (con su nueva fecha) o rechazar con observación.
CREATE OR ALTER PROCEDURE dbo.sp_AdminRevisarDocumento @DocumentoId INT, @Aprobar BIT, @Fecha DATE = NULL, @Observacion NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @CuidadorId INT = (SELECT CuidadorId FROM DocumentosCuidador WHERE Id = @DocumentoId);
    IF @CuidadorId IS NULL BEGIN SELECT 'NO_ENCONTRADO' AS Resultado, NULL AS CuidadorId; RETURN; END

    IF @Aprobar = 1
        UPDATE DocumentosCuidador SET Estado = 2, ObservacionesAdmin = NULL, FechaVencimiento = @Fecha, AvisoVencimiento = 0 WHERE Id = @DocumentoId;
    ELSE
        UPDATE DocumentosCuidador SET Estado = 3, ObservacionesAdmin = @Observacion WHERE Id = @DocumentoId;

    SELECT 'OK' AS Resultado, @CuidadorId AS CuidadorId;
END
GO

-- Lo corre la API cada hora. Marca avisos de 30 y 7 días y, al vencer, pasa el documento a rechazado
-- (así el Care Partner lo puede reemplazar desde la app) y oculta al Care Partner.
-- Devuelve lo que hay que avisar: Nivel 1 = 30 días, 2 = 7 días, 3 = vencido.
CREATE OR ALTER PROCEDURE dbo.sp_ProcesarVencimientosDocumentos @Hoy DATE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Avisos TABLE (DocumentoId INT, CuidadorId INT, TipoDocumento NVARCHAR(100), FechaVencimiento DATE, Nivel TINYINT);

    UPDATE DocumentosCuidador
    SET Estado = 3, AvisoVencimiento = 3,
        ObservacionesAdmin = N'Venció el ' + CONVERT(NVARCHAR(10), FechaVencimiento, 103) + N'. Sube uno vigente.'
    OUTPUT inserted.Id, inserted.CuidadorId, inserted.TipoDocumento, inserted.FechaVencimiento, 3 INTO @Avisos
    WHERE FechaVencimiento < @Hoy AND AvisoVencimiento < 3 AND Estado = 2;

    UPDATE DocumentosCuidador SET AvisoVencimiento = 2
    OUTPUT inserted.Id, inserted.CuidadorId, inserted.TipoDocumento, inserted.FechaVencimiento, 2 INTO @Avisos
    WHERE Estado = 2 AND FechaVencimiento >= @Hoy AND DATEDIFF(DAY, @Hoy, FechaVencimiento) <= 7 AND AvisoVencimiento < 2;

    UPDATE DocumentosCuidador SET AvisoVencimiento = 1
    OUTPUT inserted.Id, inserted.CuidadorId, inserted.TipoDocumento, inserted.FechaVencimiento, 1 INTO @Avisos
    WHERE Estado = 2 AND DATEDIFF(DAY, @Hoy, FechaVencimiento) BETWEEN 8 AND 30 AND AvisoVencimiento < 1;

    -- Ocultar a quien tenga algo vencido (avisa la API por SignalR con la columna Oculto).
    DECLARE @Ocultos TABLE (CuidadorId INT);
    UPDATE PerfilCuidador SET Disponible = 0
    OUTPUT inserted.UsuarioId INTO @Ocultos
    WHERE Disponible = 1 AND dbo.fn_TieneDocumentoVencido(UsuarioId, @Hoy) = 1;

    SELECT a.DocumentoId, a.CuidadorId, a.TipoDocumento, a.FechaVencimiento, a.Nivel,
           CAST(IIF(o.CuidadorId IS NULL, 0, 1) AS BIT) AS Oculto
    FROM @Avisos a LEFT JOIN (SELECT DISTINCT CuidadorId FROM @Ocultos) o ON o.CuidadorId = a.CuidadorId
    UNION ALL
    -- Ocultos sin aviso nuevo (p. ej. se pusieron visibles por otra vía): solo para el evento de SignalR.
    SELECT NULL, o.CuidadorId, NULL, NULL, 0, CAST(1 AS BIT)
    FROM (SELECT DISTINCT CuidadorId FROM @Ocultos) o
    WHERE NOT EXISTS (SELECT 1 FROM @Avisos a WHERE a.CuidadorId = o.CuidadorId);
END
GO

-- ============================================================================
-- 3. CALIDAD DE CARE PARTNERS (semáforo)
-- ============================================================================
-- Puntaje 0–100 por período. Resta: cancelación propia 15, no se presentó 20, rechazo del final por
-- el cliente 10, llegada tarde (>10 min) 6, solicitud rechazada 3, y la nota bajo 4.5 (×20 por punto).
-- Rojo < 60 (o 3+ cancelaciones, o nota < 3.5 con 3+ reseñas), amarillo < 80, verde el resto.
CREATE OR ALTER PROCEDURE dbo.sp_AdminCalidadCuidadores @Desde DATE, @Hasta DATE, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    WITH T AS (
        SELECT t.CuidadorId,
               COUNT(*) AS Solicitudes,
               SUM(IIF(t.Estado = 4, 1, 0)) AS Completados,
               SUM(IIF(t.Estado = 5 AND t.MotivoCancelacionId IS NOT NULL, 1, 0)) AS Cancelados,
               SUM(IIF(t.Estado = 6, 1, 0)) AS Rechazados,
               SUM(IIF(t.Estado = 2 AND DATEADD(MINUTE, 60, CAST(t.Fecha AS DATETIME) + CAST(t.HoraInicio AS DATETIME)) < @Ahora, 1, 0)) AS NoSePresento,
               SUM(IIF(t.FechaInicioReal > DATEADD(MINUTE, 10, CAST(t.Fecha AS DATETIME) + CAST(t.HoraInicio AS DATETIME)), 1, 0)) AS Tarde,
               SUM(IIF(t.RechazadoPorCliente = 1 OR t.PagoDisputado = 1, 1, 0)) AS Disputas
        FROM Trabajos t
        WHERE t.Fecha BETWEEN @Desde AND @Hasta
        GROUP BY t.CuidadorId
    ), C AS (
        SELECT c.CalificadoId, COUNT(*) AS Resenas, AVG(CAST(c.Puntuacion AS DECIMAL(4,2))) AS Nota, SUM(IIF(c.Puntuacion <= 2, 1, 0)) AS Malas
        FROM Calificaciones c
        WHERE CAST(c.FechaCreacion AS DATE) BETWEEN @Desde AND @Hasta
        GROUP BY c.CalificadoId
    ), G AS (
        SELECT CalificadoId, AVG(CAST(Puntuacion AS DECIMAL(4,2))) AS NotaGeneral FROM Calificaciones GROUP BY CalificadoId
    ), S AS (
        SELECT t.CuidadorId, COUNT(*) AS Sos
        FROM SOSAlertas s JOIN Trabajos t ON t.Id = s.TrabajoId
        WHERE CAST(s.FechaCreacion AS DATE) BETWEEN @Desde AND @Hasta
        GROUP BY t.CuidadorId
    ), R AS (
        SELECT u.Id AS UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, u.FotoUrl, u.Telefono, pc.Especialidad, pc.Disponible,
               ISNULL(T.Solicitudes, 0) AS Solicitudes, ISNULL(T.Completados, 0) AS Completados, ISNULL(T.Cancelados, 0) AS Cancelados,
               ISNULL(T.Rechazados, 0) AS Rechazados, ISNULL(T.NoSePresento, 0) AS NoSePresento, ISNULL(T.Tarde, 0) AS Tarde,
               ISNULL(T.Disputas, 0) AS Disputas, ISNULL(S.Sos, 0) AS Sos,
               ISNULL(C.Resenas, 0) AS Resenas, C.Nota, ISNULL(C.Malas, 0) AS Malas, G.NotaGeneral,
               (SELECT COUNT(*) FROM DocumentosCuidador d WHERE d.CuidadorId = u.Id AND d.FechaVencimiento < CAST(@Ahora AS DATE)) AS DocsVencidos
        FROM Usuarios u
        JOIN PerfilCuidador pc ON pc.UsuarioId = u.Id
        LEFT JOIN T ON T.CuidadorId = u.Id
        LEFT JOIN C ON C.CalificadoId = u.Id
        LEFT JOIN G ON G.CalificadoId = u.Id
        LEFT JOIN S ON S.CuidadorId = u.Id
        WHERE u.RolId = 3 AND u.IsActive = 1 AND pc.EstadoAprobacion = 2
    )
    SELECT R.*, p.Puntaje,
           CASE WHEN p.Puntaje < 60 OR R.Cancelados >= 3 OR (R.Nota < 3.5 AND R.Resenas >= 3) OR R.NoSePresento >= 2 THEN 'rojo'
                WHEN p.Puntaje < 80 OR R.DocsVencidos > 0 THEN 'amarillo'
                ELSE 'verde' END AS Semaforo,
           CAST(IIF(R.Solicitudes = 0, NULL, 100.0 * (R.Solicitudes - R.Rechazados) / R.Solicitudes) AS DECIMAL(5,1)) AS TasaAceptacion
    FROM R
    CROSS APPLY (SELECT CAST(IIF(v.Bruto < 0, 0, v.Bruto) AS INT) AS Puntaje
                 FROM (SELECT 100 - 15 * R.Cancelados - 20 * R.NoSePresento - 10 * R.Disputas - 6 * R.Tarde - 3 * R.Rechazados
                              - IIF(R.Nota IS NOT NULL AND R.Nota < 4.5, (4.5 - R.Nota) * 20, 0) AS Bruto) v) p
    ORDER BY p.Puntaje, R.Nombre;
END
GO

-- Incidencias de un Care Partner en el período (lo que explica su puntaje).
CREATE OR ALTER PROCEDURE dbo.sp_AdminCalidadDetalle @CuidadorId INT, @Desde DATE, @Hasta DATE, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SELECT x.TrabajoId, x.Fecha, x.Tipo, x.Detalle, ISNULL(c.NombreCompleto, c.Email) AS ClienteNombre
    FROM (
        SELECT t.Id AS TrabajoId, CAST(t.Fecha AS DATETIME) + CAST(t.HoraInicio AS DATETIME) AS Fecha, t.ClienteId,
               CASE WHEN t.Estado = 5 AND t.MotivoCancelacionId IS NOT NULL THEN 'cancelo'
                    WHEN t.Estado = 6 THEN 'rechazo'
                    WHEN t.Estado = 2 AND DATEADD(MINUTE, 60, CAST(t.Fecha AS DATETIME) + CAST(t.HoraInicio AS DATETIME)) < @Ahora THEN 'no_se_presento'
                    WHEN t.RechazadoPorCliente = 1 OR t.PagoDisputado = 1 THEN 'disputa'
                    WHEN t.FechaInicioReal > DATEADD(MINUTE, 10, CAST(t.Fecha AS DATETIME) + CAST(t.HoraInicio AS DATETIME)) THEN 'tarde' END AS Tipo,
               CASE WHEN t.Estado = 5 THEN ISNULL(m.Descripcion, t.MotivoCancelacionTexto)
                    WHEN t.FechaInicioReal IS NOT NULL THEN CONCAT(DATEDIFF(MINUTE, CAST(t.Fecha AS DATETIME) + CAST(t.HoraInicio AS DATETIME), t.FechaInicioReal), N' min después de la hora')
                    ELSE NULL END AS Detalle
        FROM Trabajos t LEFT JOIN MotivosCancelacion m ON m.Id = t.MotivoCancelacionId
        WHERE t.CuidadorId = @CuidadorId AND t.Fecha BETWEEN @Desde AND @Hasta
        UNION ALL
        SELECT c.TrabajoId, c.FechaCreacion, c.CalificadorId, 'resena_baja', CONCAT(c.Puntuacion, N' ★', IIF(c.Comentario IS NULL, N'', N' · ' + c.Comentario))
        FROM Calificaciones c
        WHERE c.CalificadoId = @CuidadorId AND c.Puntuacion <= 2 AND CAST(c.FechaCreacion AS DATE) BETWEEN @Desde AND @Hasta
    ) x
    LEFT JOIN Usuarios c ON c.Id = x.ClienteId
    WHERE x.Tipo IS NOT NULL
    ORDER BY x.Fecha DESC;
END
GO

-- ============================================================================
-- 4. DISPUTAS
-- ============================================================================
IF OBJECT_ID('dbo.Disputas') IS NULL
BEGIN
    CREATE TABLE dbo.Disputas (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        TrabajoId           INT            NOT NULL,
        Origen              VARCHAR(12)    NOT NULL,   -- 'rechazo' (el cliente rechazó el final), 'sin_pago' (cerrado sin pago), 'manual'
        Estado              TINYINT        NOT NULL DEFAULT 1,  -- 1 abierta, 2 resuelta
        Motivo              NVARCHAR(400)  NULL,
        Resolucion          VARCHAR(12)    NULL,       -- 'completo', 'parcial', 'sin_pago', 'acuerdo', 'cancelado'
        MontoFinal          DECIMAL(10,2)  NULL,
        Nota                NVARCHAR(1000) NULL,
        AbiertaPorAdminId   INT            NULL,
        ResueltaPorAdminId  INT            NULL,
        FechaApertura       DATETIME       NOT NULL,
        FechaResolucion     DATETIME       NULL
    );
    CREATE UNIQUE INDEX UX_Disputas_Trabajo ON dbo.Disputas (TrabajoId);
END
GO

-- Crea los casos que faltan desde las marcas del servicio y cierra solos los que las partes ya resolvieron.
CREATE OR ALTER PROCEDURE dbo.sp_SincronizarDisputas @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Disputas (TrabajoId, Origen, Motivo, FechaApertura)
    SELECT t.Id, IIF(t.PagoDisputado = 1, 'sin_pago', 'rechazo'),
           IIF(t.PagoDisputado = 1, N'El Care Partner cerró el servicio sin pago tras el rechazo del cliente.', N'El cliente rechazó la finalización del servicio.'),
           ISNULL(t.FechaFin, @Ahora)
    FROM Trabajos t
    WHERE (t.PagoDisputado = 1 OR (t.RechazadoPorCliente = 1 AND t.Estado = 3))
      AND NOT EXISTS (SELECT 1 FROM Disputas d WHERE d.TrabajoId = t.Id);

    -- Rechazo que terminó con el cliente confirmando (el pago ya existe) o servicio cancelado.
    UPDATE d SET Estado = 2, FechaResolucion = @Ahora,
           Resolucion = IIF(t.Estado = 5, 'cancelado', 'acuerdo'),
           Nota = IIF(t.Estado = 5, N'El servicio se canceló.', N'El cliente confirmó la finalización.')
    FROM Disputas d JOIN Trabajos t ON t.Id = d.TrabajoId
    WHERE d.Estado = 1 AND d.Origen = 'rechazo' AND t.PagoDisputado = 0
      AND (t.Estado = 5 OR (t.Estado = 4 AND t.RechazadoPorCliente = 0));
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminDisputas @Estado TINYINT = NULL, @Id INT = NULL, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.sp_SincronizarDisputas @Ahora;

    SELECT d.Id, d.TrabajoId, d.Origen, d.Estado, d.Motivo, d.Resolucion, d.MontoFinal, d.Nota, d.FechaApertura, d.FechaResolucion,
           ISNULL(ar.NombreCompleto, ar.Email) AS ResueltaPorNombre, ISNULL(aa.NombreCompleto, aa.Email) AS AbiertaPorNombre,
           t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Estado AS EstadoServicio, t.Tarifa, t.JustificacionFinalizacion, t.MetodoPago,
           t.FechaInicioReal, t.FechaFin,
           t.ClienteId, ISNULL(c.NombreCompleto, c.Email) AS ClienteNombre, c.FotoUrl AS ClienteFoto, c.Telefono AS ClienteTelefono,
           t.CuidadorId, ISNULL(q.NombreCompleto, q.Email) AS CuidadorNombre, q.FotoUrl AS CuidadorFoto, q.Telefono AS CuidadorTelefono,
           p.Monto AS PagoMonto, p.Propina AS PagoPropina, p.Estado AS PagoEstado,
           DATEDIFF(DAY, d.FechaApertura, ISNULL(d.FechaResolucion, @Ahora)) AS DiasAbierta,
           (SELECT COUNT(*) FROM UbicacionesHistorial h WHERE h.TrabajoId = t.Id) AS PuntosGps,
           (SELECT COUNT(*) FROM TareasTrabajo x WHERE x.TrabajoId = t.Id) AS Tareas,
           (SELECT COUNT(*) FROM TareasTrabajo x WHERE x.TrabajoId = t.Id AND x.Completada = 1) AS TareasHechas
    FROM Disputas d
    JOIN Trabajos t ON t.Id = d.TrabajoId
    JOIN Usuarios c ON c.Id = t.ClienteId
    JOIN Usuarios q ON q.Id = t.CuidadorId
    LEFT JOIN Pagos p ON p.TrabajoId = t.Id
    LEFT JOIN Usuarios ar ON ar.Id = d.ResueltaPorAdminId
    LEFT JOIN Usuarios aa ON aa.Id = d.AbiertaPorAdminId
    WHERE (@Estado IS NULL OR d.Estado = @Estado) AND (@Id IS NULL OR d.Id = @Id)
    ORDER BY d.Estado, d.FechaApertura DESC;
END
GO

-- Abrir un caso a mano (p. ej. un reclamo por soporte de un servicio ya completado).
CREATE OR ALTER PROCEDURE dbo.sp_AdminAbrirDisputa @TrabajoId INT, @Motivo NVARCHAR(400), @AdminId INT, @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Estado INT = (SELECT Estado FROM Trabajos WHERE Id = @TrabajoId);
    IF @Estado IS NULL BEGIN SELECT 'NO_ENCONTRADO' AS Resultado, NULL AS Id; RETURN; END
    IF @Estado NOT IN (3, 4, 7) BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado, NULL AS Id; RETURN; END
    DECLARE @Existente INT = (SELECT Id FROM Disputas WHERE TrabajoId = @TrabajoId);
    IF @Existente IS NOT NULL
    BEGIN
        UPDATE Disputas SET Estado = 1, Resolucion = NULL, FechaResolucion = NULL, ResueltaPorAdminId = NULL,
               Motivo = @Motivo, AbiertaPorAdminId = @AdminId
        WHERE Id = @Existente AND Estado = 2;
        SELECT 'OK' AS Resultado, @Existente AS Id;
        RETURN;
    END
    INSERT INTO Disputas (TrabajoId, Origen, Motivo, AbiertaPorAdminId, FechaApertura) VALUES (@TrabajoId, 'manual', @Motivo, @AdminId, @Fecha);
    SELECT 'OK' AS Resultado, CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
GO

-- Resolver: el servicio queda completado y el pago del Care Partner queda con el monto decidido
-- ('completo' = tarifa, 'parcial' = @Monto, 'sin_pago' = se quita el pago pendiente).
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
            DELETE FROM Pagos WHERE Id = @PagoId AND Estado IN (1, 3);
        ELSE IF @PagoId IS NULL
            INSERT INTO Pagos (TrabajoId, CuidadorId, Monto, Propina, Estado, FechaCreacion, ComisionPorcentaje)
            VALUES (@TrabajoId, @CuidadorId, @MontoFinal, 0, 1, @Fecha, dbo.fn_ComisionServicio(@Tipo));
        ELSE IF @PagoEstado <> 2
            UPDATE Pagos SET Monto = @MontoFinal WHERE Id = @PagoId;

        UPDATE Disputas SET Estado = 2, Resolucion = @Resolucion, MontoFinal = @MontoFinal, Nota = @Nota,
               ResueltaPorAdminId = @AdminId, FechaResolucion = @Fecha
        WHERE Id = @DisputaId;
    COMMIT;

    SELECT 'OK' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId, @TrabajoId AS TrabajoId, @MontoFinal AS MontoFinal;
END
GO

-- ============================================================================
-- 5. FICHAS: servicios de una persona y suspensión
-- ============================================================================
-- Últimos 100 servicios de un cliente o Care Partner (fichas del panel).
CREATE OR ALTER PROCEDURE dbo.sp_AdminServiciosUsuario @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 100 t.Id, t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa,
           t.ClienteId, ISNULL(c.NombreCompleto, c.Email) AS ClienteNombre,
           t.CuidadorId, ISNULL(q.NombreCompleto, q.Email) AS CuidadorNombre,
           t.PagoDisputado, t.RechazadoPorCliente,
           CAST(IIF(EXISTS (SELECT 1 FROM SOSAlertas s WHERE s.TrabajoId = t.Id), 1, 0) AS BIT) AS TieneSos
    FROM Trabajos t
    JOIN Usuarios c ON c.Id = t.ClienteId
    JOIN Usuarios q ON q.Id = t.CuidadorId
    WHERE t.ClienteId = @UsuarioId OR t.CuidadorId = @UsuarioId
    ORDER BY t.Fecha DESC, t.HoraInicio DESC, t.Id DESC;
END
GO

-- Al suspender una cuenta de Care Partner: sale del mapa (el horario automático ya no la pone
-- visible porque exige IsActive = 1).
CREATE OR ALTER PROCEDURE dbo.sp_OcultarUsuarioSuspendido @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE PerfilCuidador SET Disponible = 0 WHERE UsuarioId = @UsuarioId AND Disponible = 1;
    SELECT @@ROWCOUNT AS Filas;
END
GO
