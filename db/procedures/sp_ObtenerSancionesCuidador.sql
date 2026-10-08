
-- Historial de sanciones de un cuidador, para mostrarlo en su ficha.
CREATE   PROCEDURE sp_ObtenerSancionesCuidador
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT s.Id, s.Accion, s.Motivo, s.FechaCreacion, a.NombreCompleto AS AdminNombre
    FROM SancionesCuidador s
    INNER JOIN Usuarios a ON a.Id = s.AdminId
    WHERE s.UsuarioId = @UsuarioId
    ORDER BY s.FechaCreacion DESC;
END
