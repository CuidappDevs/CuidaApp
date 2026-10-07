-- Datos para los correos de recibo al completar un servicio (cliente y cuidador).
-- 1) Servicio + participantes + pago   2) Tareas   3) Cantidad de actividades reportadas
-- Solo lectura. Idempotente (CREATE OR ALTER).

CREATE OR ALTER PROCEDURE sp_ObtenerDatosReciboTrabajo
    @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT t.Id, t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.FechaInicioReal, t.FechaFin,
           t.Direccion, t.Tarifa, t.JustificacionFinalizacion,
           c.NombreCompleto AS ClienteNombre, c.Email AS ClienteEmail,
           k.NombreCompleto AS CuidadorNombre, k.Email AS CuidadorEmail,
           ISNULL(p.Monto, t.Tarifa) AS Monto, ISNULL(p.Propina, 0) AS Propina
    FROM Trabajos t
    JOIN Usuarios c ON c.Id = t.ClienteId
    JOIN Usuarios k ON k.Id = t.CuidadorId
    OUTER APPLY (SELECT TOP 1 Monto, Propina FROM Pagos WHERE TrabajoId = t.Id ORDER BY Id DESC) p
    WHERE t.Id = @TrabajoId;

    SELECT Descripcion, Completada, FechaCompletada
    FROM TareasTrabajo
    WHERE TrabajoId = @TrabajoId
    ORDER BY Orden, Id;

    SELECT COUNT(*) AS Actividades FROM ActividadesTrabajo WHERE TrabajoId = @TrabajoId;
END;
GO
