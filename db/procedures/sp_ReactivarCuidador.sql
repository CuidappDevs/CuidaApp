
CREATE   PROCEDURE sp_ReactivarCuidador
    @UsuarioId INT,
    @AdminId INT,
    @FechaHora DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Usuarios SET IsActive = 1 WHERE Id = @UsuarioId;

    INSERT INTO SancionesCuidador (UsuarioId, AdminId, Accion, Motivo, FechaCreacion)
    VALUES (@UsuarioId, @AdminId, 'Reactivado', NULL, @FechaHora);

    SELECT @@ROWCOUNT AS FilasAfectadas;
END
