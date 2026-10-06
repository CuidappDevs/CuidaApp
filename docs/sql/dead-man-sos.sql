-- Alerta automática "hombre muerto" (caída + inmovilidad sin confirmar) y contacto de emergencia del cuidador.
-- Aplicar PRIMERO en DBCuidappDev, verificar, y luego en DBCuidapp (producción).
-- Cambio aditivo: columnas nuevas con NULL/DEFAULT y parámetros opcionales; lo existente sigue funcionando.

-- 1) Contacto de emergencia (familiar) del cuidador. El cliente ya tenía nombre y teléfono en PerfilCliente.
IF COL_LENGTH('dbo.PerfilCuidador', 'ContactoEmergenciaNombre') IS NULL
    ALTER TABLE dbo.PerfilCuidador ADD
        ContactoEmergenciaNombre   NVARCHAR(100) NULL,
        ContactoEmergenciaTelefono NVARCHAR(30)  NULL,
        ContactoEmergenciaEmail    NVARCHAR(150) NULL;
GO

-- 2) Origen de la alerta: 'Manual' (botón SOS) o 'Automatica' (detección de caída sin confirmación).
IF COL_LENGTH('dbo.SOSAlertas', 'Origen') IS NULL
    ALTER TABLE dbo.SOSAlertas ADD Origen VARCHAR(20) NOT NULL CONSTRAINT DF_SOSAlertas_Origen DEFAULT 'Manual';
GO

-- 3) Crear alerta (agrega @Origen opcional y devuelve Origen + contacto de emergencia del usuario).
CREATE OR ALTER PROCEDURE dbo.sp_CrearSOSAlerta
    @TrabajoId INT, @UsuarioId INT, @TipoUsuario VARCHAR(20),
    @Latitud FLOAT, @Longitud FLOAT,
    @Motivo NVARCHAR(500) = NULL, @FechaCreacion DATETIME,
    @Origen VARCHAR(20) = 'Manual'
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO SOSAlertas (TrabajoId, UsuarioId, TipoUsuario, Latitud, Longitud, Motivo, Estado, FechaCreacion, Origen)
    VALUES (@TrabajoId, @UsuarioId, @TipoUsuario, @Latitud, @Longitud, @Motivo, 'Pendiente', @FechaCreacion, @Origen);

    SELECT a.Id, a.TrabajoId, a.UsuarioId, a.TipoUsuario,
           u.NombreCompleto AS NombreUsuario, u.Email AS EmailUsuario,
           a.Latitud, a.Longitud, a.Motivo, a.Estado, a.FechaCreacion, a.Origen,
           COALESCE(pq.ContactoEmergenciaNombre, pc.ContactoEmergenciaNombre)     AS ContactoNombre,
           COALESCE(pq.ContactoEmergenciaTelefono, pc.ContactoEmergenciaTelefono) AS ContactoTelefono,
           pq.ContactoEmergenciaEmail                                             AS ContactoEmail
    FROM SOSAlertas a
    INNER JOIN Usuarios u ON a.UsuarioId = u.Id
    LEFT JOIN PerfilCuidador pq ON pq.UsuarioId = a.UsuarioId
    LEFT JOIN PerfilCliente  pc ON pc.UsuarioId = a.UsuarioId
    WHERE a.Id = SCOPE_IDENTITY();
END;
GO

-- 4) Alertas pendientes para el panel (agrega Origen y contacto).
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerSOSAlertasPendientes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.Id, a.TrabajoId, a.UsuarioId, a.TipoUsuario,
           u.NombreCompleto AS NombreUsuario, u.Email AS EmailUsuario,
           a.Latitud, a.Longitud, a.Motivo, a.Estado, a.FechaCreacion,
           a.FechaAtencion, a.AtendidoPor, a.Origen,
           COALESCE(pq.ContactoEmergenciaNombre, pc.ContactoEmergenciaNombre)     AS ContactoNombre,
           COALESCE(pq.ContactoEmergenciaTelefono, pc.ContactoEmergenciaTelefono) AS ContactoTelefono,
           pq.ContactoEmergenciaEmail                                             AS ContactoEmail
    FROM SOSAlertas a
    INNER JOIN Usuarios u ON a.UsuarioId = u.Id
    LEFT JOIN PerfilCuidador pq ON pq.UsuarioId = a.UsuarioId
    LEFT JOIN PerfilCliente  pc ON pc.UsuarioId = a.UsuarioId
    WHERE a.Estado = 'Pendiente'
    ORDER BY a.FechaCreacion DESC;
END;
GO

-- 5) Contacto de emergencia del cuidador: leer / guardar.
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerContactoEmergenciaCuidador
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ContactoEmergenciaNombre AS Nombre, ContactoEmergenciaTelefono AS Telefono, ContactoEmergenciaEmail AS Email
    FROM PerfilCuidador WHERE UsuarioId = @UsuarioId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_GuardarContactoEmergenciaCuidador
    @UsuarioId INT, @Nombre NVARCHAR(100), @Telefono NVARCHAR(30) = NULL, @Email NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE PerfilCuidador
    SET ContactoEmergenciaNombre = @Nombre,
        ContactoEmergenciaTelefono = @Telefono,
        ContactoEmergenciaEmail = @Email
    WHERE UsuarioId = @UsuarioId;
    SELECT @@ROWCOUNT AS FilasAfectadas;
END;
GO
