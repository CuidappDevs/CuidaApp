-- Aplicar despues de contratos-fijos y caja-negra-grabacion. No ejecutado por Codex.
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerTrabajosPorCuidador
    @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.Id, t.ContratoId, t.ClienteId, c.NombreCompleto AS ClienteNombre, c.FotoUrl AS ClienteFotoUrl,
           t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas, t.FechaCreacion,
           t.Latitud, t.Longitud, t.RechazadoPorCliente, t.PagoDisputado, t.MetodoPago
    FROM Trabajos t
    JOIN Usuarios c ON c.Id = t.ClienteId
    WHERE t.CuidadorId = @CuidadorId
    ORDER BY t.Fecha DESC, t.HoraInicio DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerTrabajoPorId
    @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        t.Id, t.ContratoId, t.ClienteId, t.CuidadorId, u.NombreCompleto AS CuidadorNombre, u.FotoUrl AS CuidadorFotoUrl,
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
        t.Id, t.ContratoId, t.ClienteId, t.CuidadorId, u.NombreCompleto AS CuidadorNombre, u.FotoUrl AS CuidadorFotoUrl,
        t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas, t.FechaCreacion,
        t.PinInicio, t.PinFin, t.FechaInicioReal, t.JustificacionFinalizacion, t.RechazadoPorCliente, t.MetodoPago
    FROM Trabajos t
    JOIN Usuarios u ON u.Id = t.CuidadorId
    WHERE t.ClienteId = @ClienteId AND t.ContratoId IS NULL
      AND (
            t.Estado IN (1,2,3,7)
            OR (t.Estado = 4 AND t.FechaCreacion >= DATEADD(DAY, -3, GETDATE())
                AND NOT EXISTS (SELECT 1 FROM Calificaciones c WHERE c.TrabajoId = t.Id AND c.CalificadorId = @ClienteId))
          )
    ORDER BY t.FechaCreacion DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_MarcarGrabacionServicio @TrabajoId INT, @CuidadorId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Trabajos SET GrabacionActiva = 1 WHERE Id = @TrabajoId AND CuidadorId = @CuidadorId AND ContratoId IS NULL AND Estado IN (3, 7);
    SELECT IIF(@@ROWCOUNT = 0, 'NO_ENCONTRADO', 'OK') AS Resultado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarSegmentoGrabacion
    @TrabajoId INT, @CuidadorId INT, @UrlArchivo NVARCHAR(400), @DuracionSeg INT, @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    -- Solo acepta segmentos de un servicio real de ese Care Partner (en curso o recién terminado).
    IF NOT EXISTS (SELECT 1 FROM Trabajos WHERE Id = @TrabajoId AND CuidadorId = @CuidadorId AND ContratoId IS NULL AND Estado IN (3, 4, 7))
    BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END

    DECLARE @Orden INT = (SELECT ISNULL(MAX(Orden), -1) + 1 FROM GrabacionesServicio WHERE TrabajoId = @TrabajoId);
    INSERT INTO GrabacionesServicio (TrabajoId, CuidadorId, UrlArchivo, Orden, DuracionSeg, Fecha)
    VALUES (@TrabajoId, @CuidadorId, @UrlArchivo, @Orden, @DuracionSeg, @Fecha);
    SELECT 'OK' AS Resultado;
END
GO


CREATE OR ALTER PROCEDURE dbo.sp_ObtenerTrabajoActivoPorCliente
    @ClienteId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (1)
        t.Id, t.ContratoId, t.ClienteId, t.CuidadorId, u.NombreCompleto AS CuidadorNombre, u.FotoUrl AS CuidadorFotoUrl,
        t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas, t.FechaCreacion,
        t.PinInicio, t.PinFin, t.FechaInicioReal, t.JustificacionFinalizacion, t.RechazadoPorCliente, t.MetodoPago
    FROM Trabajos t
    JOIN Usuarios u ON u.Id = t.CuidadorId
    WHERE t.ClienteId = @ClienteId AND t.ContratoId IS NULL
      AND (
            t.Estado IN (1,2,3,7)
            OR (t.Estado = 4 AND t.FechaCreacion >= DATEADD(DAY, -3, GETDATE())
                AND NOT EXISTS (SELECT 1 FROM Calificaciones c WHERE c.TrabajoId = t.Id AND c.CalificadorId = @ClienteId))
          )
    ORDER BY t.FechaCreacion DESC;
END;
GO

IF OBJECT_ID('dbo.SeguimientosCompartidos') IS NULL
CREATE TABLE dbo.SeguimientosCompartidos (
 Id INT IDENTITY PRIMARY KEY, TrabajoId INT NOT NULL REFERENCES Trabajos(Id), ClienteId INT NOT NULL REFERENCES Usuarios(Id),
 TokenHash VARBINARY(32) NOT NULL UNIQUE, FechaCreacion DATETIME2 NOT NULL, FechaExpira DATETIME2 NOT NULL,
 Revocado BIT NOT NULL DEFAULT 0, FechaRevocado DATETIME2 NULL, UltimoAcceso DATETIME2 NULL, Accesos INT NOT NULL DEFAULT 0);
GO
CREATE OR ALTER PROCEDURE dbo.sp_SeguimientoCrear @TrabajoId INT, @ClienteId INT, @TokenHash VARBINARY(32)
AS
BEGIN
 SET NOCOUNT ON;
 INSERT SeguimientosCompartidos(TrabajoId,ClienteId,TokenHash,FechaCreacion,FechaExpira)
 OUTPUT inserted.Id, inserted.FechaExpira
 SELECT Id,ClienteId,@TokenHash,SYSUTCDATETIME(),DATEADD(HOUR,24,SYSUTCDATETIME()) FROM Trabajos
 WHERE Id=@TrabajoId AND ClienteId=@ClienteId AND Estado IN (2,3);
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_SeguimientoListar @TrabajoId INT,@ClienteId INT
AS
BEGIN
 SET NOCOUNT ON;
 SELECT s.Id,s.FechaCreacion,s.FechaExpira,s.UltimoAcceso,s.Accesos FROM SeguimientosCompartidos s
 JOIN Trabajos t ON t.Id=s.TrabajoId AND t.ClienteId=@ClienteId
 WHERE s.TrabajoId=@TrabajoId AND s.ClienteId=@ClienteId AND s.Revocado=0 AND s.FechaExpira>SYSUTCDATETIME() AND t.Estado IN (2,3);
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_SeguimientoRevocar @Id INT,@ClienteId INT
AS
BEGIN
 SET NOCOUNT ON;
 UPDATE SeguimientosCompartidos SET Revocado=1,FechaRevocado=SYSUTCDATETIME()
 OUTPUT inserted.Id WHERE Id=@Id AND ClienteId=@ClienteId;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_SeguimientoPublico @TokenHash VARBINARY(32)
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @TrabajoId INT;
 UPDATE s SET UltimoAcceso=SYSUTCDATETIME(),Accesos=Accesos+1,@TrabajoId=s.TrabajoId
 FROM SeguimientosCompartidos s JOIN Trabajos t ON t.Id=s.TrabajoId
 WHERE s.TokenHash=@TokenHash AND s.Revocado=0 AND s.FechaExpira>SYSUTCDATETIME()
 AND s.FechaCreacion>DATEADD(HOUR,-24,SYSUTCDATETIME()) AND t.Estado IN (2,3);
 SELECT u.NombreCompleto,u.FotoUrl,u.Telefono,t.TipoServicio,t.Estado,t.FechaCreacion,t.FechaLlegada,t.FechaInicioReal,t.FechaFin,
 CAST(t.Fecha AS DATETIME)+CAST(t.HoraFin AS DATETIME) AS FinEsperado,
 CAST(NULL AS NVARCHAR(200)) AS Zona,
 CASE WHEN t.Estado=3 THEN p.Latitud END AS Latitud,CASE WHEN t.Estado=3 THEN p.Longitud END AS Longitud,
 CASE WHEN t.Estado=3 THEN p.UltimaUbicacion END AS UltimaUbicacion,CASE WHEN t.Estado=3 THEN p.Bateria END AS Bateria
 FROM Trabajos t JOIN Usuarios u ON u.Id=t.CuidadorId LEFT JOIN PerfilCuidador p ON p.UsuarioId=u.Id WHERE t.Id=@TrabajoId;
 SELECT Descripcion,Completada,FechaCompletada FROM TareasTrabajo WHERE TrabajoId=@TrabajoId ORDER BY Orden;
 SELECT Descripcion,FechaHora FROM ActividadesTrabajo WHERE TrabajoId=@TrabajoId ORDER BY FechaHora;
END;
GO
CREATE OR ALTER PROCEDURE dbo.sp_ContratosResumenDashboard @UsuarioId INT,@Hoy DATE
AS
BEGIN
 SET NOCOUNT ON;
 SELECT c.Id AS ContratoId,c.Estado AS ContratoEstado,u.NombreCompleto,u.FotoUrl,
 hoy.Id AS TrabajoId,COALESCE(hoy.HoraInicio,horario.HoraInicio) AS HoraInicio,COALESCE(hoy.HoraFin,horario.HoraFin) AS HoraFin,
 CASE WHEN c.Estado=5 THEN 'pausado' WHEN a.Id IS NOT NULL THEN 'ausencia'
 WHEN hoy.Estado=3 THEN 'trabajando' WHEN hoy.Estado IN(4,7) THEN 'terminado'
 WHEN hoy.Id IS NOT NULL OR horario.HoraInicio IS NOT NULL THEN 'programado' ELSE 'dia_libre' END AS EstadoHoy,
 prox.Fecha AS ProximaFecha,prox.HoraInicio AS ProximaHora,prox.HoraFin AS ProximaFin
 FROM Contratos c JOIN Usuarios u ON u.Id=CASE WHEN c.ClienteId=@UsuarioId THEN c.CuidadorId ELSE c.ClienteId END
 OUTER APPLY(SELECT TOP 1 t.* FROM Trabajos t WHERE t.ContratoId=c.Id AND t.Fecha=@Hoy ORDER BY CASE WHEN t.Estado IN(3,7) THEN 0 WHEN t.Estado=2 THEN 1 ELSE 2 END,t.HoraInicio) hoy
 OUTER APPLY(SELECT TOP 1 h.HoraInicio,h.HoraFin FROM ContratoHorario h WHERE h.ContratoId=c.Id AND h.DiaSemana=((DATEDIFF(DAY,CONVERT(DATE,'19000107'),@Hoy)%7+7)%7) ORDER BY h.HoraInicio) horario
 OUTER APPLY(SELECT TOP 1 DATEADD(DAY,d.n,@Hoy) AS Fecha,h.HoraInicio,h.HoraFin
 FROM (VALUES(0),(1),(2),(3),(4),(5),(6),(7)) d(n) JOIN ContratoHorario h ON h.ContratoId=c.Id
 AND h.DiaSemana=((DATEDIFF(DAY,CONVERT(DATE,'19000107'),DATEADD(DAY,d.n,@Hoy))%7+7)%7)
 WHERE (d.n>0 OR h.HoraInicio>COALESCE(hoy.HoraInicio,horario.HoraInicio))
 AND NOT EXISTS(SELECT 1 FROM ContratoAusencias x WHERE x.ContratoId=c.Id AND x.Fecha=DATEADD(DAY,d.n,@Hoy))
 ORDER BY d.n,h.HoraInicio) prox
 LEFT JOIN ContratoAusencias a ON a.ContratoId=c.Id AND a.Fecha=@Hoy
 WHERE (c.ClienteId=@UsuarioId OR c.CuidadorId=@UsuarioId) AND c.Estado IN(4,5);
END;
GO
