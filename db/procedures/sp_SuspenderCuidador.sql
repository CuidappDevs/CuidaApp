
CREATE   PROCEDURE sp_SuspenderCuidador
    @UsuarioId INT,
    @AdminId INT,
    @Motivo NVARCHAR(500),
    @FechaHora DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Usuarios SET IsActive = 0 WHERE Id = @UsuarioId;

    INSERT INTO SancionesCuidador (UsuarioId, AdminId, Accion, Motivo, FechaCreacion)
    VALUES (@UsuarioId, @AdminId, 'Suspendido', @Motivo, @FechaHora);

    SELECT @@ROWCOUNT AS FilasAfectadas;
END
