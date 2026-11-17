-- =============================================================================
-- Invitar a mi cuidadora: un cliente con permiso para contratar invita a una persona que ya
-- trabaja con su familia (fuera de CuidApp) para tenerla bajo contrato fijo en la app.
-- La cuidadora se registra con el código (verificación y test igual que todos), queda con
-- AceptaContratos = 1 y sin disponibilidad pública. Contratos.TrabajaDesde guarda la antigüedad
-- previa, que sale como cláusula en el PDF.
-- Estados de InvitacionesCuidador: 1 pendiente, 2 aceptada (registrada), 3 cancelada.
-- =============================================================================

IF OBJECT_ID('dbo.InvitacionesCuidador') IS NULL
CREATE TABLE dbo.InvitacionesCuidador (
    Id              INT IDENTITY PRIMARY KEY,
    ClienteId       INT NOT NULL REFERENCES dbo.Usuarios(Id),
    Codigo          VARCHAR(8) NOT NULL,
    Nombre          NVARCHAR(120) NOT NULL,
    Telefono        VARCHAR(30) NULL,
    Email           NVARCHAR(150) NULL,
    TrabajaDesde    DATE NULL,
    Estado          TINYINT NOT NULL DEFAULT 1,
    FechaCreacion   DATETIME NOT NULL,
    Expira          DATETIME NOT NULL,
    CuidadorId      INT NULL REFERENCES dbo.Usuarios(Id),
    FechaAceptacion DATETIME NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_InvitacionesCuidador_Codigo')
    CREATE UNIQUE INDEX UX_InvitacionesCuidador_Codigo ON dbo.InvitacionesCuidador(Codigo);
GO
IF COL_LENGTH('dbo.Contratos', 'TrabajaDesde') IS NULL
    ALTER TABLE dbo.Contratos ADD TrabajaDesde DATE NULL;
GO

-- Crea la invitación (requiere permiso para contratar aprobado). Máximo 5 pendientes por cliente.
CREATE OR ALTER PROCEDURE dbo.sp_InvitacionCrear
    @ClienteId INT, @Codigo VARCHAR(8), @Nombre NVARCHAR(120), @Telefono VARCHAR(30), @Email NVARCHAR(150),
    @TrabajaDesde DATE, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM PermisoContratacion WHERE ClienteId = @ClienteId AND Estado = 2)
    BEGIN SELECT 'PERMISO_REQUERIDO' AS Resultado; RETURN; END
    IF (SELECT COUNT(*) FROM InvitacionesCuidador WHERE ClienteId = @ClienteId AND Estado = 1 AND Expira > @Ahora) >= 5
    BEGIN SELECT 'LIMITE_INVITACIONES' AS Resultado; RETURN; END
    IF @TrabajaDesde IS NOT NULL AND @TrabajaDesde > CAST(@Ahora AS DATE)
    BEGIN SELECT 'FECHA_INVALIDA' AS Resultado; RETURN; END
    IF EXISTS (SELECT 1 FROM InvitacionesCuidador WHERE Codigo = @Codigo)
    BEGIN SELECT 'REINTENTAR' AS Resultado; RETURN; END

    INSERT INTO InvitacionesCuidador (ClienteId, Codigo, Nombre, Telefono, Email, TrabajaDesde, Estado, FechaCreacion, Expira)
    VALUES (@ClienteId, @Codigo, @Nombre, @Telefono, @Email, @TrabajaDesde, 1, @Ahora, DATEADD(DAY, 14, @Ahora));
    SELECT 'OK' AS Resultado, CAST(SCOPE_IDENTITY() AS INT) AS Id, @Codigo AS Codigo, DATEADD(DAY, 14, @Ahora) AS Expira;
END
GO

-- Invitaciones del cliente (las vencidas salen con Estado 4 calculado).
CREATE OR ALTER PROCEDURE dbo.sp_InvitacionesPorCliente @ClienteId INT, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.Id, i.Codigo, i.Nombre, i.Telefono, i.Email, i.TrabajaDesde, i.FechaCreacion, i.Expira,
           CASE WHEN i.Estado = 1 AND i.Expira <= @Ahora THEN 4 ELSE i.Estado END AS Estado,
           i.CuidadorId, u.NombreCompleto AS CuidadorNombre, u.FotoUrl AS CuidadorFoto,
           p.EstadoAprobacion AS CuidadorAprobacion
    FROM InvitacionesCuidador i
    LEFT JOIN Usuarios u ON u.Id = i.CuidadorId
    LEFT JOIN PerfilCuidador p ON p.UsuarioId = i.CuidadorId
    WHERE i.ClienteId = @ClienteId AND i.Estado <> 3
    ORDER BY i.FechaCreacion DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InvitacionCancelar @Id INT, @ClienteId INT
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM InvitacionesCuidador WHERE Id = @Id AND ClienteId = @ClienteId AND Estado = 1)
    BEGIN SELECT 'NO_ENCONTRADO' AS Resultado; RETURN; END
    UPDATE InvitacionesCuidador SET Estado = 3 WHERE Id = @Id AND ClienteId = @ClienteId AND Estado = 1;
    SELECT CASE WHEN @@ROWCOUNT = 1 THEN 'OK' ELSE 'NO_ENCONTRADO' END AS Resultado;
END
GO

-- Para el registro: valida el código y devuelve quién invita (sin datos sensibles del cliente).
CREATE OR ALTER PROCEDURE dbo.sp_InvitacionObtener @Codigo VARCHAR(8), @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN i.Estado <> 1 THEN 'USADA' WHEN i.Expira <= @Ahora THEN 'VENCIDA' ELSE 'OK' END AS Resultado,
           i.Nombre, i.Telefono, i.Email, u.NombreCompleto AS ClienteNombre
    FROM InvitacionesCuidador i JOIN Usuarios u ON u.Id = i.ClienteId
    WHERE i.Codigo = @Codigo;
END
GO

-- Al registrarse: vincula la cuenta nueva, activa contratos fijos y deja la disponibilidad apagada.
CREATE OR ALTER PROCEDURE dbo.sp_InvitacionAceptar @Codigo VARCHAR(8), @CuidadorId INT, @Ahora DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
    DECLARE @Id INT, @ClienteId INT;
    SELECT @Id = Id, @ClienteId = ClienteId FROM InvitacionesCuidador WITH (UPDLOCK, HOLDLOCK)
    WHERE Codigo = @Codigo AND Estado = 1 AND Expira > @Ahora;
    IF @Id IS NULL BEGIN COMMIT; SELECT 'INVITACION_INVALIDA' AS Resultado; RETURN; END

    UPDATE InvitacionesCuidador SET Estado = 2, CuidadorId = @CuidadorId, FechaAceptacion = @Ahora
    WHERE Id = @Id AND Estado = 1 AND Expira > @Ahora;
    IF @@ROWCOUNT <> 1 THROW 50002, 'No se pudo consumir la invitación.', 1;
    UPDATE PerfilCuidador SET AceptaContratos = 1 WHERE UsuarioId = @CuidadorId;
    IF @@ROWCOUNT <> 1 THROW 50003, 'No se encontró el perfil de la invitada.', 1;
    COMMIT;
    SELECT 'OK' AS Resultado, @ClienteId AS ClienteId;
END
GO

-- Antigüedad previa del contrato (la pone el cliente al proponer; sale en el PDF).
CREATE OR ALTER PROCEDURE dbo.sp_ContratoTrabajaDesde @ContratoId INT, @ClienteId INT, @TrabajaDesde DATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Contratos SET TrabajaDesde = @TrabajaDesde WHERE Id = @ContratoId AND ClienteId = @ClienteId AND Estado IN (1, 2);
    SELECT CASE WHEN @@ROWCOUNT = 1 THEN 'OK' ELSE 'NO_ENCONTRADO' END AS Resultado;
END
GO

-- sp_ContratoDetalle: ahora devuelve c.TrabajaDesde.

-- Detalle (7 resultados): contrato, horario, propuestas, ausencias, firmas, documentos, trabajos.
-- @UsuarioId NULL = panel (sin control de pertenencia).
CREATE OR ALTER PROCEDURE dbo.sp_ContratoDetalle @ContratoId INT, @UsuarioId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @UsuarioId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Contratos WHERE Id = @ContratoId AND @UsuarioId IN (ClienteId, CuidadorId))
        SET @ContratoId = -1;

    SELECT c.Id, c.Estado, c.TipoServicio, c.TarifaHora, c.MetodoPago, c.FechaInicio, c.FechaFin, c.DiasPreaviso, c.Nota,
           c.FechaCreacion, c.FechaUltimaAccion, c.FechaActivacion, c.FechaTerminacion, c.TerminadoPor, c.MotivoTerminacion, c.TrabajaDesde,
           c.ClienteId, cl.NombreCompleto AS ClienteNombre, cl.Email AS ClienteEmail, cl.Telefono AS ClienteTelefono,
           cl.DocumentoIdentidad AS ClienteDocumento, cl.FotoUrl AS ClienteFoto,
           c.CuidadorId, cu.NombreCompleto AS CuidadorNombre, cu.Email AS CuidadorEmail, cu.Telefono AS CuidadorTelefono,
           cu.DocumentoIdentidad AS CuidadorDocumento, cu.FotoUrl AS CuidadorFoto,
           c.UbicacionId, ub.Nombre AS DomicilioNombre, ub.Direccion, ub.Latitud, ub.Longitud, d.QuienVive,
           CASE WHEN @UsuarioId IS NULL THEN NULL WHEN @UsuarioId = c.ClienteId THEN 'cliente' ELSE 'cuidador' END AS MiRol
    FROM Contratos c
    JOIN Usuarios cl ON cl.Id = c.ClienteId
    JOIN Usuarios cu ON cu.Id = c.CuidadorId
    JOIN UbicacionesCliente ub ON ub.Id = c.UbicacionId
    LEFT JOIN DomiciliosContratacion d ON d.UbicacionId = c.UbicacionId
    WHERE c.Id = @ContratoId;

    SELECT DiaSemana, HoraInicio, HoraFin FROM ContratoHorario WHERE ContratoId = @ContratoId ORDER BY CASE DiaSemana WHEN 0 THEN 7 ELSE DiaSemana END, HoraInicio;

    SELECT p.Version, p.PropuestoPor, u.NombreCompleto AS PropuestoPorNombre, p.TarifaHora, p.FechaInicio, p.Horario, p.Nota, p.Fecha
    FROM ContratoPropuestas p JOIN Usuarios u ON u.Id = p.PropuestoPor
    WHERE p.ContratoId = @ContratoId ORDER BY p.Version;

    SELECT a.Id, a.Fecha, a.MarcadoPor, u.NombreCompleto AS MarcadoPorNombre, a.Motivo
    FROM ContratoAusencias a JOIN Usuarios u ON u.Id = a.MarcadoPor
    WHERE a.ContratoId = @ContratoId ORDER BY a.Fecha DESC;

    SELECT f.UsuarioId, u.NombreCompleto, f.FirmaSvg, f.Fecha, f.Ip, f.Latitud, f.Longitud, f.Correo
    FROM ContratoFirmas f JOIN Usuarios u ON u.Id = f.UsuarioId
    WHERE f.ContratoId = @ContratoId ORDER BY f.Fecha;

    SELECT Id, Version, Tipo, Hash, Fecha FROM ContratoDocumentos WHERE ContratoId = @ContratoId ORDER BY Version DESC;

    SELECT TOP 60 Id, Fecha, HoraInicio, HoraFin, Estado, Tarifa FROM Trabajos
    WHERE ContratoId = @ContratoId ORDER BY Fecha DESC, HoraInicio DESC;
END

GO
