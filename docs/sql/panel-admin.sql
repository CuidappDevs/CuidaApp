-- ============================================================================
-- Panel administrativo: operación diaria, catálogos, auditoría, avisos y niveles.
-- Idempotente (se puede correr varias veces). Requiere QUOTED_IDENTIFIER ON.
-- Estados de Trabajos: 1 Pendiente, 2 Aceptado, 3 En progreso, 4 Completado,
--                      5 Cancelado, 6 Rechazado, 7 Terminado (espera confirmación)
-- Estados de Pagos:    1 Pendiente, 3 Autorizado, 2 Pagado
-- ============================================================================
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- Nivel del administrador: 1 Superadmin, 2 Operaciones/soporte, 3 Finanzas
IF COL_LENGTH('dbo.Usuarios', 'NivelAdmin') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios ADD NivelAdmin TINYINT NULL;
END
GO
-- Los administradores que ya existían conservan todos los permisos.
UPDATE dbo.Usuarios SET NivelAdmin = 1 WHERE RolId = 1 AND NivelAdmin IS NULL;
GO

-- ---------- Bitácora de acciones del panel
IF OBJECT_ID('dbo.AuditoriaAdmin') IS NULL
BEGIN
    CREATE TABLE dbo.AuditoriaAdmin (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        AdminId     INT            NOT NULL,
        Accion      NVARCHAR(120)  NOT NULL,
        Entidad     NVARCHAR(60)   NULL,
        EntidadId   NVARCHAR(40)   NULL,
        Detalle     NVARCHAR(400)  NULL,
        Exito       BIT            NOT NULL DEFAULT 1,
        Fecha       DATETIME       NOT NULL
    );
    CREATE INDEX IX_AuditoriaAdmin_Fecha ON dbo.AuditoriaAdmin (Fecha DESC);
END
GO

-- ---------- Avisos masivos enviados desde el panel
IF OBJECT_ID('dbo.AvisosAdmin') IS NULL
BEGIN
    CREATE TABLE dbo.AvisosAdmin (
        Id        INT IDENTITY(1,1) PRIMARY KEY,
        AdminId   INT            NOT NULL,
        Destino   TINYINT        NOT NULL,   -- 0 todos, 2 clientes, 3 cuidadores
        Titulo    NVARCHAR(120)  NOT NULL,
        Mensaje   NVARCHAR(500)  NOT NULL,
        Fecha     DATETIME       NOT NULL
    );
END
GO

-- ---------- Administradores (agrega el nivel)
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerUsuariosAdmin
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id AS UsuarioId, NombreCompleto, Email, FechaCreacion, IsActive, ISNULL(NivelAdmin, 2) AS NivelAdmin
    FROM Usuarios
    WHERE RolId = 1
    ORDER BY FechaCreacion DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminObtenerNivel @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(NivelAdmin, 2) AS NivelAdmin FROM Usuarios WHERE Id = @UsuarioId AND RolId = 1;
END
GO

-- No deja quitar el último superadmin activo.
CREATE OR ALTER PROCEDURE dbo.sp_AdminCambiarNivel @UsuarioId INT, @Nivel TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    IF @Nivel NOT IN (1, 2, 3) BEGIN SELECT 'NIVEL_INVALIDO' AS Resultado; RETURN; END
    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @UsuarioId AND RolId = 1) BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    IF @Nivel <> 1 AND ISNULL((SELECT NivelAdmin FROM Usuarios WHERE Id = @UsuarioId), 2) = 1
       AND (SELECT COUNT(*) FROM Usuarios WHERE RolId = 1 AND IsActive = 1 AND NivelAdmin = 1) <= 1
    BEGIN SELECT 'ULTIMO_SUPERADMIN' AS Resultado; RETURN; END

    UPDATE Usuarios SET NivelAdmin = @Nivel WHERE Id = @UsuarioId;
    SELECT 'OK' AS Resultado;
END
GO

-- ---------- Auditoría
CREATE OR ALTER PROCEDURE dbo.sp_AdminRegistrarAuditoria
    @AdminId INT, @Accion NVARCHAR(120), @Entidad NVARCHAR(60) = NULL, @EntidadId NVARCHAR(40) = NULL,
    @Detalle NVARCHAR(400) = NULL, @Exito BIT = 1, @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO AuditoriaAdmin (AdminId, Accion, Entidad, EntidadId, Detalle, Exito, Fecha)
    VALUES (@AdminId, @Accion, @Entidad, @EntidadId, @Detalle, @Exito, @Fecha);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminObtenerAuditoria
    @AdminId INT = NULL, @Buscar NVARCHAR(100) = NULL, @Pagina INT = 1, @Tamano INT = 25
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.Id, a.AdminId, ISNULL(u.NombreCompleto, u.Email) AS AdminNombre, a.Accion, a.Entidad, a.EntidadId,
           a.Detalle, a.Exito, a.Fecha, COUNT(*) OVER () AS TotalFilas
    FROM AuditoriaAdmin a
    LEFT JOIN Usuarios u ON u.Id = a.AdminId
    WHERE (@AdminId IS NULL OR a.AdminId = @AdminId)
      AND (@Buscar IS NULL OR a.Accion LIKE '%' + @Buscar + '%' OR a.Detalle LIKE '%' + @Buscar + '%'
           OR a.EntidadId = @Buscar OR u.NombreCompleto LIKE '%' + @Buscar + '%')
    ORDER BY a.Fecha DESC, a.Id DESC
    OFFSET (@Pagina - 1) * @Tamano ROWS FETCH NEXT @Tamano ROWS ONLY;
END
GO

-- ---------- Avisos
CREATE OR ALTER PROCEDURE dbo.sp_AdminRegistrarAviso
    @AdminId INT, @Destino TINYINT, @Titulo NVARCHAR(120), @Mensaje NVARCHAR(500), @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO AvisosAdmin (AdminId, Destino, Titulo, Mensaje, Fecha) VALUES (@AdminId, @Destino, @Titulo, @Mensaje, @Fecha);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminObtenerAvisos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 50 a.Id, a.Destino, a.Titulo, a.Mensaje, a.Fecha, ISNULL(u.NombreCompleto, u.Email) AS AdminNombre
    FROM AvisosAdmin a LEFT JOIN Usuarios u ON u.Id = a.AdminId
    ORDER BY a.Fecha DESC;
END
GO

-- ---------- Dashboard (4 resultados: indicadores, serie de 14 días, actividad, verificación)
CREATE OR ALTER PROCEDURE dbo.sp_AdminDashboard @Hoy DATE
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Hace7 DATE = DATEADD(DAY, -6, @Hoy), @Hace14 DATE = DATEADD(DAY, -13, @Hoy);

    SELECT
        (SELECT COUNT(*) FROM Trabajos WHERE Fecha = @Hoy AND Estado NOT IN (5, 6))                         AS ServiciosHoy,
        (SELECT COUNT(*) FROM Trabajos WHERE Estado IN (3, 7))                                               AS ServiciosEnCurso,
        (SELECT COUNT(*) FROM Trabajos WHERE Estado = 1)                                                     AS ServiciosPorAceptar,
        (SELECT COUNT(*) FROM PerfilCuidador pc JOIN Usuarios u ON u.Id = pc.UsuarioId
            WHERE pc.Disponible = 1 AND pc.EstadoAprobacion = 2 AND u.IsActive = 1)                          AS CuidadoresVisibles,
        (SELECT COUNT(*) FROM PerfilCuidador pc JOIN Usuarios u ON u.Id = pc.UsuarioId
            WHERE ISNULL(pc.EstadoAprobacion, 1) = 1 AND u.IsActive = 1)                                     AS CuidadoresPorVerificar,
        (SELECT COUNT(*) FROM Usuarios WHERE RolId = 3)                                                      AS CuidadoresTotal,
        (SELECT COUNT(*) FROM Usuarios WHERE RolId = 2)                                                      AS ClientesTotal,
        (SELECT COUNT(*) FROM Usuarios WHERE RolId IN (2, 3) AND CAST(FechaCreacion AS DATE) >= @Hace7)      AS RegistrosSemana,
        (SELECT COUNT(*) FROM SOSAlertas WHERE Estado = 'Pendiente')                                         AS SosPendientes,
        (SELECT COUNT(*) FROM Tickets WHERE Estado IN (1, 2))                                                AS TicketsAbiertos,
        (SELECT COUNT(*) FROM Pagos WHERE Estado = 1)                                                        AS PagosPorAutorizar,
        (SELECT COUNT(*) FROM Pagos WHERE Estado = 3)                                                        AS PagosPorEnviar,
        (SELECT ISNULL(SUM(Tarifa), 0) FROM Trabajos WHERE Estado = 4 AND Fecha BETWEEN @Hace7 AND @Hoy)     AS IngresosSemana,
        (SELECT ISNULL(SUM(Tarifa), 0) FROM Trabajos WHERE Estado = 4 AND Fecha BETWEEN @Hace14 AND DATEADD(DAY, -7, @Hoy)) AS IngresosSemanaAnterior,
        (SELECT CAST(ISNULL(AVG(CAST(c.Puntuacion AS DECIMAL(5,2))), 0) AS DECIMAL(5,2))
            FROM Calificaciones c JOIN Usuarios u ON u.Id = c.CalificadoId WHERE u.RolId = 3)                AS CalificacionPromedio;

    ;WITH Dias AS (
        SELECT @Hace14 AS Dia UNION ALL SELECT DATEADD(DAY, 1, Dia) FROM Dias WHERE Dia < @Hoy
    )
    SELECT d.Dia,
           (SELECT COUNT(*) FROM Trabajos t WHERE t.Fecha = d.Dia AND t.Estado NOT IN (5, 6)) AS Servicios,
           (SELECT COUNT(*) FROM Trabajos t WHERE t.Fecha = d.Dia AND t.Estado = 4)           AS Completados,
           (SELECT ISNULL(SUM(t.Tarifa), 0) FROM Trabajos t WHERE t.Fecha = d.Dia AND t.Estado = 4) AS Ingresos
    FROM Dias d ORDER BY d.Dia;

    SELECT TOP 12 Tipo, Texto, Fecha, Enlace FROM (
        SELECT 'registro' AS Tipo,
               ISNULL(NombreCompleto, Email) + IIF(RolId = 3, N' se registró como Care Partner', N' se registró como cliente') AS Texto,
               FechaCreacion AS Fecha, IIF(RolId = 3, 'care-partners/' + CAST(Id AS VARCHAR), 'clientes/' + CAST(Id AS VARCHAR)) AS Enlace
        FROM Usuarios WHERE RolId IN (2, 3) AND FechaCreacion IS NOT NULL
        UNION ALL
        SELECT 'servicio', N'Nuevo servicio: ' + t.TipoServicio + N' para ' + ISNULL(c.NombreCompleto, c.Email),
               t.FechaCreacion, 'servicios/' + CAST(t.Id AS VARCHAR)
        FROM Trabajos t JOIN Usuarios c ON c.Id = t.ClienteId
        UNION ALL
        SELECT 'sos', N'Alerta SOS de ' + ISNULL(u.NombreCompleto, u.Email), a.FechaCreacion, 'sos-alertas'
        FROM SOSAlertas a JOIN Usuarios u ON u.Id = a.UsuarioId
        UNION ALL
        SELECT 'ticket', N'Reporte: ' + t.Asunto, t.FechaCreacion, 'soporte/' + CAST(t.Id AS VARCHAR)
        FROM Tickets t
    ) x ORDER BY Fecha DESC;

    SELECT TOP 5 u.Id AS UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, u.FotoUrl, u.FechaCreacion,
           (SELECT COUNT(*) FROM DocumentosCuidador d WHERE d.CuidadorId = u.Id AND ISNULL(d.Estado, 0) NOT IN (2, 3)) AS DocsPendientes,
           (SELECT COUNT(*) FROM DocumentosCuidador d WHERE d.CuidadorId = u.Id AND d.Estado = 3) AS DocsRechazados
    FROM Usuarios u JOIN PerfilCuidador pc ON pc.UsuarioId = u.Id
    WHERE ISNULL(pc.EstadoAprobacion, 1) = 1 AND u.IsActive = 1
    ORDER BY u.FechaCreacion;
END
GO

-- ---------- Servicios (lista paginada)
CREATE OR ALTER PROCEDURE dbo.sp_AdminTrabajos
    @Estado INT = NULL, @Desde DATE = NULL, @Hasta DATE = NULL, @Buscar NVARCHAR(100) = NULL,
    @Pagina INT = 1, @Tamano INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.Id, t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa,
           t.ClienteId, ISNULL(c.NombreCompleto, c.Email) AS ClienteNombre,
           t.CuidadorId, ISNULL(q.NombreCompleto, q.Email) AS CuidadorNombre,
           t.PagoDisputado, t.RechazadoPorCliente, t.FechaCreacion,
           CAST(IIF(EXISTS (SELECT 1 FROM SOSAlertas s WHERE s.TrabajoId = t.Id), 1, 0) AS BIT) AS TieneSos,
           COUNT(*) OVER () AS TotalFilas
    FROM Trabajos t
    JOIN Usuarios c ON c.Id = t.ClienteId
    JOIN Usuarios q ON q.Id = t.CuidadorId
    WHERE (@Estado IS NULL OR t.Estado = @Estado)
      AND (@Desde IS NULL OR t.Fecha >= @Desde)
      AND (@Hasta IS NULL OR t.Fecha <= @Hasta)
      AND (@Buscar IS NULL OR c.NombreCompleto LIKE '%' + @Buscar + '%' OR q.NombreCompleto LIKE '%' + @Buscar + '%'
           OR t.TipoServicio LIKE '%' + @Buscar + '%' OR CAST(t.Id AS VARCHAR) = @Buscar)
    ORDER BY t.Fecha DESC, t.HoraInicio DESC, t.Id DESC
    OFFSET (@Pagina - 1) * @Tamano ROWS FETCH NEXT @Tamano ROWS ONLY;
END
GO

-- ---------- Detalle de un servicio (8 resultados)
CREATE OR ALTER PROCEDURE dbo.sp_AdminTrabajoDetalle @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT t.Id, t.TipoServicio, t.Fecha, t.HoraInicio, t.HoraFin, t.Direccion, t.Estado, t.Tarifa, t.Notas,
           t.Latitud, t.Longitud, t.FechaCreacion, t.FechaInicioReal, t.FechaFin, t.JustificacionFinalizacion,
           t.RechazadoPorCliente, t.PagoDisputado,
           ISNULL(m.Descripcion, t.MotivoCancelacionTexto) AS MotivoCancelacion, t.MotivoCancelacionTexto,
           t.ClienteId, ISNULL(c.NombreCompleto, c.Email) AS ClienteNombre, c.Email AS ClienteEmail, c.Telefono AS ClienteTelefono, c.FotoUrl AS ClienteFoto,
           t.CuidadorId, ISNULL(q.NombreCompleto, q.Email) AS CuidadorNombre, q.Email AS CuidadorEmail, q.Telefono AS CuidadorTelefono, q.FotoUrl AS CuidadorFoto
    FROM Trabajos t
    JOIN Usuarios c ON c.Id = t.ClienteId
    JOIN Usuarios q ON q.Id = t.CuidadorId
    LEFT JOIN MotivosCancelacion m ON m.Id = t.MotivoCancelacionId
    WHERE t.Id = @TrabajoId;

    SELECT Id, Descripcion, Orden, Completada, FechaCompletada FROM TareasTrabajo WHERE TrabajoId = @TrabajoId ORDER BY Orden;
    SELECT Id, Descripcion, FechaHora FROM ActividadesTrabajo WHERE TrabajoId = @TrabajoId ORDER BY FechaHora;
    SELECT Id, Monto, Propina, Estado, FechaCreacion, FechaAutorizacion, FechaPago FROM Pagos WHERE TrabajoId = @TrabajoId;
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

-- ---------- Cancelar un servicio desde soporte
CREATE OR ALTER PROCEDURE dbo.sp_AdminCancelarTrabajo @TrabajoId INT, @Motivo NVARCHAR(400)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Estado INT, @ClienteId INT, @CuidadorId INT;
    SELECT @Estado = Estado, @ClienteId = ClienteId, @CuidadorId = CuidadorId FROM Trabajos WHERE Id = @TrabajoId;
    IF @Estado IS NULL BEGIN SELECT 'NO_ENCONTRADO' AS Resultado, NULL AS ClienteId, NULL AS CuidadorId; RETURN; END
    IF @Estado NOT IN (1, 2, 3, 7) BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId; RETURN; END

    UPDATE Trabajos SET Estado = 5, MotivoCancelacionId = NULL, MotivoCancelacionTexto = N'Cancelado por soporte: ' + @Motivo
    WHERE Id = @TrabajoId;
    SELECT 'OK' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId;
END
GO

-- ---------- Dar por completado un servicio (disputas): crea el pago pendiente si no existe
CREATE OR ALTER PROCEDURE dbo.sp_AdminCompletarTrabajo @TrabajoId INT, @Justificacion NVARCHAR(400), @Fecha DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Estado INT, @ClienteId INT, @CuidadorId INT, @Tarifa DECIMAL(10,2);
    SELECT @Estado = Estado, @ClienteId = ClienteId, @CuidadorId = CuidadorId, @Tarifa = Tarifa FROM Trabajos WHERE Id = @TrabajoId;
    IF @Estado IS NULL BEGIN SELECT 'NO_ENCONTRADO' AS Resultado, NULL AS ClienteId, NULL AS CuidadorId; RETURN; END
    IF @Estado NOT IN (3, 7) BEGIN SELECT 'ESTADO_INVALIDO' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId; RETURN; END

    BEGIN TRAN;
        UPDATE Trabajos SET Estado = 4, FechaFin = ISNULL(FechaFin, @Fecha),
               JustificacionFinalizacion = N'Completado por soporte: ' + @Justificacion
        WHERE Id = @TrabajoId;
        IF NOT EXISTS (SELECT 1 FROM Pagos WHERE TrabajoId = @TrabajoId)
            INSERT INTO Pagos (TrabajoId, CuidadorId, Monto, Propina, Estado, FechaCreacion)
            VALUES (@TrabajoId, @CuidadorId, @Tarifa, 0, 1, @Fecha);
    COMMIT;
    SELECT 'OK' AS Resultado, @ClienteId AS ClienteId, @CuidadorId AS CuidadorId;
END
GO

-- ---------- Cola de verificación de Care Partners
CREATE OR ALTER PROCEDURE dbo.sp_AdminColaVerificacion
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.Id AS UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, u.Email, u.FotoUrl, u.Telefono, u.FechaCreacion,
           ISNULL(pc.EstadoAprobacion, 1) AS EstadoAprobacion, pc.Especialidad,
           (SELECT COUNT(*) FROM DocumentosCuidador d WHERE d.CuidadorId = u.Id) AS DocsTotal,
           (SELECT COUNT(*) FROM DocumentosCuidador d WHERE d.CuidadorId = u.Id AND ISNULL(d.Estado, 0) NOT IN (2, 3)) AS DocsPendientes,
           (SELECT COUNT(*) FROM DocumentosCuidador d WHERE d.CuidadorId = u.Id AND d.Estado = 2) AS DocsAprobados,
           (SELECT COUNT(*) FROM DocumentosCuidador d WHERE d.CuidadorId = u.Id AND d.Estado = 3) AS DocsRechazados,
           (SELECT MAX(d.FechaSubida) FROM DocumentosCuidador d WHERE d.CuidadorId = u.Id) AS UltimaSubida
    FROM Usuarios u JOIN PerfilCuidador pc ON pc.UsuarioId = u.Id
    WHERE u.RolId = 3 AND u.IsActive = 1 AND ISNULL(pc.EstadoAprobacion, 1) IN (1, 3)
    ORDER BY ISNULL(pc.EstadoAprobacion, 1), u.FechaCreacion;
END
GO

-- ---------- Mapa en vivo: sp_AdminMapa vive en centro-mando.sql (necesita sus columnas de ubicación).

-- ---------- Calificaciones (lista y resumen por Care Partner)
CREATE OR ALTER PROCEDURE dbo.sp_AdminCalificaciones @MaxPuntuacion INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 200 c.Id, c.TrabajoId, c.Puntuacion, c.Comentario, c.FechaCreacion,
           c.CalificadorId, ISNULL(a.NombreCompleto, a.Email) AS CalificadorNombre, a.RolId AS CalificadorRol,
           c.CalificadoId, ISNULL(b.NombreCompleto, b.Email) AS CalificadoNombre, b.RolId AS CalificadoRol
    FROM Calificaciones c JOIN Usuarios a ON a.Id = c.CalificadorId JOIN Usuarios b ON b.Id = c.CalificadoId
    WHERE (@MaxPuntuacion IS NULL OR c.Puntuacion <= @MaxPuntuacion)
    ORDER BY c.FechaCreacion DESC;

    SELECT u.Id AS UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, u.FotoUrl,
           CAST(AVG(CAST(c.Puntuacion AS DECIMAL(5,2))) AS DECIMAL(5,2)) AS Promedio, COUNT(*) AS Total,
           SUM(IIF(c.Puntuacion <= 2, 1, 0)) AS Bajas
    FROM Calificaciones c JOIN Usuarios u ON u.Id = c.CalificadoId
    WHERE u.RolId = 3
    GROUP BY u.Id, u.NombreCompleto, u.Email, u.FotoUrl
    ORDER BY Promedio, Total DESC;
END
GO

-- ---------- Finanzas del período (3 resultados: totales, por Care Partner, por día)
CREATE OR ALTER PROCEDURE dbo.sp_AdminFinanzas @Desde DATE, @Hasta DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        (SELECT COUNT(*) FROM Trabajos WHERE Estado = 4 AND Fecha BETWEEN @Desde AND @Hasta)                       AS ServiciosCompletados,
        (SELECT COUNT(*) FROM Trabajos WHERE Estado = 5 AND Fecha BETWEEN @Desde AND @Hasta)                       AS ServiciosCancelados,
        (SELECT ISNULL(SUM(Tarifa), 0) FROM Trabajos WHERE Estado = 4 AND Fecha BETWEEN @Desde AND @Hasta)         AS Facturado,
        (SELECT ISNULL(SUM(p.Propina), 0) FROM Pagos p JOIN Trabajos t ON t.Id = p.TrabajoId WHERE t.Fecha BETWEEN @Desde AND @Hasta) AS Propinas,
        (SELECT ISNULL(SUM(p.Monto + p.Propina), 0) FROM Pagos p JOIN Trabajos t ON t.Id = p.TrabajoId WHERE p.Estado = 2 AND t.Fecha BETWEEN @Desde AND @Hasta) AS Pagado,
        (SELECT ISNULL(SUM(p.Monto + p.Propina), 0) FROM Pagos p JOIN Trabajos t ON t.Id = p.TrabajoId WHERE p.Estado IN (1, 3) AND t.Fecha BETWEEN @Desde AND @Hasta) AS PorPagar,
        (SELECT COUNT(*) FROM Trabajos WHERE PagoDisputado = 1 AND Fecha BETWEEN @Desde AND @Hasta)                AS Disputados;

    SELECT u.Id AS UsuarioId, ISNULL(u.NombreCompleto, u.Email) AS Nombre, pc.MetodoCobro,
           COUNT(DISTINCT t.Id) AS Servicios,
           ISNULL(SUM(t.Tarifa), 0) AS Facturado,
           ISNULL(SUM(p.Propina), 0) AS Propinas,
           ISNULL(SUM(IIF(p.Estado = 2, p.Monto + p.Propina, 0)), 0) AS Pagado,
           ISNULL(SUM(IIF(p.Estado IN (1, 3), p.Monto + p.Propina, 0)), 0) AS PorPagar
    FROM Trabajos t
    JOIN Usuarios u ON u.Id = t.CuidadorId
    LEFT JOIN PerfilCuidador pc ON pc.UsuarioId = u.Id
    LEFT JOIN Pagos p ON p.TrabajoId = t.Id
    WHERE t.Estado = 4 AND t.Fecha BETWEEN @Desde AND @Hasta
    GROUP BY u.Id, u.NombreCompleto, u.Email, pc.MetodoCobro
    ORDER BY Facturado DESC;

    SELECT t.Fecha AS Dia, COUNT(*) AS Servicios, SUM(t.Tarifa) AS Facturado
    FROM Trabajos t WHERE t.Estado = 4 AND t.Fecha BETWEEN @Desde AND @Hasta
    GROUP BY t.Fecha ORDER BY t.Fecha;
END
GO

-- ---------- SOS: historial (atendidas y descartadas)
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerSOSHistorial @Top INT = 100
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top) a.Id, a.TrabajoId, a.UsuarioId, a.TipoUsuario, a.Latitud, a.Longitud, a.Motivo, a.Estado,
           a.FechaCreacion, a.FechaAtencion, a.AtendidoPor, a.Origen,
           u.NombreCompleto AS NombreUsuario, u.Email AS EmailUsuario,
           COALESCE(pq.ContactoEmergenciaNombre, pc.ContactoEmergenciaNombre) AS ContactoNombre,
           COALESCE(pq.ContactoEmergenciaTelefono, pc.ContactoEmergenciaTelefono) AS ContactoTelefono,
           pq.ContactoEmergenciaEmail AS ContactoEmail
    FROM SOSAlertas a
    JOIN Usuarios u ON u.Id = a.UsuarioId
    LEFT JOIN PerfilCuidador pq ON pq.UsuarioId = a.UsuarioId
    LEFT JOIN PerfilCliente pc ON pc.UsuarioId = a.UsuarioId
    WHERE a.Estado <> 'Pendiente'
    ORDER BY a.FechaCreacion DESC;
END
GO

-- ---------- Catálogos
CREATE OR ALTER PROCEDURE dbo.sp_AdminGuardarTipoTrabajo
    @Id INT = NULL, @Nombre NVARCHAR(100), @Descripcion NVARCHAR(500) = NULL, @Icono NVARCHAR(100) = NULL, @Activo BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM TiposTrabajos WHERE Nombre = @Nombre AND Id <> ISNULL(@Id, 0)) BEGIN SELECT 'DUPLICADO' AS Resultado; RETURN; END
    IF @Id IS NULL
        INSERT INTO TiposTrabajos (Nombre, Descripcion, Icono, Activo, FechaCreacion) VALUES (@Nombre, @Descripcion, @Icono, @Activo, SYSDATETIME());
    ELSE
        UPDATE TiposTrabajos SET Nombre = @Nombre, Descripcion = @Descripcion, Icono = @Icono, Activo = @Activo WHERE Id = @Id;
    SELECT 'OK' AS Resultado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminObtenerMotivosCancelacion
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Descripcion, Activo, OrdenVisual FROM MotivosCancelacion ORDER BY OrdenVisual, Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminGuardarMotivoCancelacion
    @Id INT = NULL, @Descripcion NVARCHAR(200), @Activo BIT = 1, @OrdenVisual INT = 0
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM MotivosCancelacion WHERE Descripcion = @Descripcion AND Id <> ISNULL(@Id, 0)) BEGIN SELECT 'DUPLICADO' AS Resultado; RETURN; END
    IF @Id IS NULL
        INSERT INTO MotivosCancelacion (Descripcion, Activo, OrdenVisual) VALUES (@Descripcion, @Activo, @OrdenVisual);
    ELSE
        UPDATE MotivosCancelacion SET Descripcion = @Descripcion, Activo = @Activo, OrdenVisual = @OrdenVisual WHERE Id = @Id;
    SELECT 'OK' AS Resultado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminObtenerNacionalidades
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Nombre, Pais, CodigoIso, Activo,
           (SELECT COUNT(*) FROM Usuarios u WHERE u.NacionalidadId = n.Id) AS Usuarios
    FROM Nacionalidades n ORDER BY Nombre;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminGuardarNacionalidad
    @Id INT = NULL, @Nombre NVARCHAR(100), @Pais NVARCHAR(100), @CodigoIso CHAR(2), @Activo BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM Nacionalidades WHERE (Nombre = @Nombre OR CodigoIso = @CodigoIso) AND Id <> ISNULL(@Id, 0)) BEGIN SELECT 'DUPLICADO' AS Resultado; RETURN; END
    IF @Id IS NULL
        INSERT INTO Nacionalidades (Nombre, Pais, CodigoIso, Activo) VALUES (@Nombre, @Pais, UPPER(@CodigoIso), @Activo);
    ELSE
        UPDATE Nacionalidades SET Nombre = @Nombre, Pais = @Pais, CodigoIso = UPPER(@CodigoIso), Activo = @Activo WHERE Id = @Id;
    SELECT 'OK' AS Resultado;
END
GO

-- Estatus migratorio con sus documentos requeridos (2 resultados)
CREATE OR ALTER PROCEDURE dbo.sp_AdminObtenerEstatusMigratorio
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Nombre, Activo FROM EstatusMigratorio ORDER BY Id;
    SELECT Id, EstatusMigratorioId, TipoDocumento FROM RequisitosDocumentoPorEstatus ORDER BY EstatusMigratorioId, Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminGuardarEstatusMigratorio @Id INT = NULL, @Nombre NVARCHAR(100), @Activo BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM EstatusMigratorio WHERE Nombre = @Nombre AND Id <> ISNULL(@Id, 0)) BEGIN SELECT 'DUPLICADO' AS Resultado; RETURN; END
    IF @Id IS NULL
        INSERT INTO EstatusMigratorio (Nombre, Activo) VALUES (@Nombre, @Activo);
    ELSE
        UPDATE EstatusMigratorio SET Nombre = @Nombre, Activo = @Activo WHERE Id = @Id;
    SELECT 'OK' AS Resultado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminAgregarRequisito @EstatusMigratorioId INT, @TipoDocumento NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM RequisitosDocumentoPorEstatus WHERE EstatusMigratorioId = @EstatusMigratorioId AND TipoDocumento = @TipoDocumento)
    BEGIN SELECT 'DUPLICADO' AS Resultado; RETURN; END
    INSERT INTO RequisitosDocumentoPorEstatus (EstatusMigratorioId, TipoDocumento) VALUES (@EstatusMigratorioId, @TipoDocumento);
    SELECT 'OK' AS Resultado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminQuitarRequisito @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM RequisitosDocumentoPorEstatus WHERE Id = @Id;
    SELECT IIF(@@ROWCOUNT > 0, 'OK', 'NO_ENCONTRADO') AS Resultado;
END
GO

-- ---------- Avisos: idioma de destino, reenviar y eliminar del historial
IF COL_LENGTH('dbo.AvisosAdmin', 'Idioma') IS NULL
    ALTER TABLE dbo.AvisosAdmin ADD Idioma VARCHAR(5) NULL;   -- NULL todos los idiomas; 'es', 'en' o 'ht'
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminRegistrarAviso
    @AdminId INT, @Destino TINYINT, @Titulo NVARCHAR(120), @Mensaje NVARCHAR(500), @Fecha DATETIME, @Idioma VARCHAR(5) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO AvisosAdmin (AdminId, Destino, Idioma, Titulo, Mensaje, Fecha) VALUES (@AdminId, @Destino, @Idioma, @Titulo, @Mensaje, @Fecha);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminObtenerAvisos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 50 a.Id, a.Destino, a.Idioma, a.Titulo, a.Mensaje, a.Fecha, ISNULL(u.NombreCompleto, u.Email) AS AdminNombre
    FROM AvisosAdmin a LEFT JOIN Usuarios u ON u.Id = a.AdminId
    ORDER BY a.Fecha DESC, a.Id DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminObtenerAviso @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Destino, Idioma, Titulo, Mensaje FROM AvisosAdmin WHERE Id = @Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AdminEliminarAviso @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM AvisosAdmin WHERE Id = @Id;
    SELECT IIF(@@ROWCOUNT > 0, 'OK', 'NO_ENCONTRADO') AS Resultado;
END
GO
