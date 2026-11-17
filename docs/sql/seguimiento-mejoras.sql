-- Seguimiento compartido: más datos para la familia (PIN de salida, hora de inicio prevista, tareas)
-- y SOS enviado por el familiar desde la página pública. Idempotente. Ejecutar con QUOTED_IDENTIFIER ON.
SET NOCOUNT ON;
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
  CAST(t.Fecha AS DATETIME)+CAST(t.HoraInicio AS DATETIME) AS InicioEsperado,
  CAST(t.Fecha AS DATETIME)+CAST(t.HoraFin AS DATETIME) AS FinEsperado,
  CAST(NULL AS NVARCHAR(200)) AS Zona,
  -- El PIN de salida solo mientras trabaja: la familia se lo da a la Care Partner al terminar.
  CASE WHEN t.Estado=3 THEN t.PinFin END AS PinSalida,
  CASE WHEN t.Estado=3 THEN p.Latitud END AS Latitud,CASE WHEN t.Estado=3 THEN p.Longitud END AS Longitud,
  CASE WHEN t.Estado=3 THEN p.UltimaUbicacion END AS UltimaUbicacion,CASE WHEN t.Estado=3 THEN p.Bateria END AS Bateria,
  CAST(CASE WHEN EXISTS(SELECT 1 FROM SOSAlertas a WHERE a.TrabajoId=t.Id AND a.Estado='Pendiente') THEN 1 ELSE 0 END AS BIT) AS SosPendiente
 FROM Trabajos t JOIN Usuarios u ON u.Id=t.CuidadorId LEFT JOIN PerfilCuidador p ON p.UsuarioId=u.Id WHERE t.Id=@TrabajoId;
 SELECT Descripcion,Completada,FechaCompletada FROM TareasTrabajo WHERE TrabajoId=@TrabajoId ORDER BY Orden;
 SELECT Descripcion,FechaHora FROM ActividadesTrabajo WHERE TrabajoId=@TrabajoId ORDER BY FechaHora;
END;
GO

-- Datos para el SOS del familiar: el servicio, quién compartió el enlace (el cliente) y la última ubicación conocida.
-- Resultado: OK, NO_DISPONIBLE (enlace vencido o servicio no activo) o YA_ENVIADA (hay un SOS pendiente de los últimos 10 min).
CREATE OR ALTER PROCEDURE dbo.sp_SeguimientoSOS @TokenHash VARBINARY(32)
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @TrabajoId INT, @ClienteId INT, @Lat FLOAT, @Lon FLOAT;
 SELECT @TrabajoId=t.Id, @ClienteId=t.ClienteId,
  @Lat=CAST(COALESCE(p.Latitud,t.Latitud,0) AS FLOAT), @Lon=CAST(COALESCE(p.Longitud,t.Longitud,0) AS FLOAT)
 FROM SeguimientosCompartidos s JOIN Trabajos t ON t.Id=s.TrabajoId LEFT JOIN PerfilCuidador p ON p.UsuarioId=t.CuidadorId
 WHERE s.TokenHash=@TokenHash AND s.Revocado=0 AND s.FechaExpira>SYSUTCDATETIME()
  AND s.FechaCreacion>DATEADD(HOUR,-24,SYSUTCDATETIME()) AND t.Estado IN (2,3);

 IF @TrabajoId IS NULL BEGIN SELECT 'NO_DISPONIBLE' AS Resultado; RETURN; END
 IF EXISTS(SELECT 1 FROM SOSAlertas WHERE TrabajoId=@TrabajoId AND Estado='Pendiente' AND Origen='Seguimiento'
           AND FechaCreacion>DATEADD(MINUTE,-10,DATEADD(HOUR,-4,SYSUTCDATETIME())))
 BEGIN SELECT 'YA_ENVIADA' AS Resultado; RETURN; END
 SELECT 'OK' AS Resultado, @TrabajoId AS TrabajoId, @ClienteId AS ClienteId, @Lat AS Latitud, @Lon AS Longitud;
END;
GO
