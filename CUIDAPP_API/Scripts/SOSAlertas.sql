-- =============================================
-- Tabla SOSAlertas para emergencias
-- Base de datos: DBCuidappDev
-- =============================================

USE [DBCuidappDev];
GO

CREATE TABLE SOSAlertas (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TrabajoId INT NOT NULL,
    UsuarioId INT NOT NULL,
    TipoUsuario VARCHAR(20) NOT NULL, -- 'Cliente' o 'Cuidador'
    Latitud FLOAT NOT NULL,
    Longitud FLOAT NOT NULL,
    Motivo NVARCHAR(500) NULL,
    Estado VARCHAR(20) NOT NULL DEFAULT 'Pendiente', -- Pendiente, Atendida, Descartada
    FechaCreacion DATETIME NOT NULL,
    FechaAtencion DATETIME NULL,
    AtendidoPor NVARCHAR(200) NULL,
    CONSTRAINT FK_SOSAlertas_Trabajos FOREIGN KEY (TrabajoId) REFERENCES Trabajos(Id),
    CONSTRAINT FK_SOSAlertas_Usuarios FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id)
);

GO

-- =============================================
-- SP: Crear alerta SOS
-- =============================================
CREATE PROCEDURE sp_CrearSOSAlerta
    @TrabajoId INT,
    @UsuarioId INT,
    @TipoUsuario VARCHAR(20),
    @Latitud FLOAT,
    @Longitud FLOAT,
    @Motivo NVARCHAR(500) = NULL,
    @FechaCreacion DATETIME
AS
BEGIN
    INSERT INTO SOSAlertas (TrabajoId, UsuarioId, TipoUsuario, Latitud, Longitud, Motivo, Estado, FechaCreacion)
    VALUES (@TrabajoId, @UsuarioId, @TipoUsuario, @Latitud, @Longitud, @Motivo, 'Pendiente', @FechaCreacion);

    SELECT a.Id, a.TrabajoId, a.UsuarioId, a.TipoUsuario,
           u.NombreCompleto AS NombreUsuario,
           u.Email AS EmailUsuario,
           a.Latitud, a.Longitud, a.Motivo, a.Estado, a.FechaCreacion
    FROM SOSAlertas a
    INNER JOIN Usuarios u ON a.UsuarioId = u.Id
    WHERE a.Id = SCOPE_IDENTITY();
END;

GO

-- =============================================
-- SP: Obtener alertas pendientes
-- =============================================
CREATE PROCEDURE sp_ObtenerSOSAlertasPendientes
AS
BEGIN
    SELECT a.Id, a.TrabajoId, a.UsuarioId, a.TipoUsuario,
           u.NombreCompleto AS NombreUsuario,
           u.Email AS EmailUsuario,
           a.Latitud, a.Longitud, a.Motivo, a.Estado, a.FechaCreacion,
           a.FechaAtencion, a.AtendidoPor
    FROM SOSAlertas a
    INNER JOIN Usuarios u ON a.UsuarioId = u.Id
    WHERE a.Estado = 'Pendiente'
    ORDER BY a.FechaCreacion DESC;
END;

GO

-- =============================================
-- SP: Atender alerta SOS
-- =============================================
CREATE PROCEDURE sp_AtenderSOSAlerta
    @Id INT,
    @AtendidoPor NVARCHAR(200),
    @FechaAtencion DATETIME
AS
BEGIN
    UPDATE SOSAlertas
    SET Estado = 'Atendida',
        AtendidoPor = @AtendidoPor,
        FechaAtencion = @FechaAtencion
    WHERE Id = @Id AND Estado = 'Pendiente';
END;

GO
