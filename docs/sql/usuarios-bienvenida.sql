-- Bienvenida animada de la app (onboarding): se muestra UNA vez por cuenta, en cualquier teléfono.
-- BienvenidaVista = 1 cuando el usuario ya la vio (o la saltó).
-- Los usuarios que ya existen al correr el script se marcan como vistos: solo la ven las cuentas nuevas.
-- Idempotente.

IF COL_LENGTH(N'dbo.Usuarios', N'BienvenidaVista') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios ADD BienvenidaVista BIT NOT NULL CONSTRAINT DF_Usuarios_BienvenidaVista DEFAULT (0);
    EXEC('UPDATE dbo.Usuarios SET BienvenidaVista = 1');
END;
GO

CREATE OR ALTER PROCEDURE sp_ObtenerBienvenidaVista
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT BienvenidaVista FROM Usuarios WHERE Id = @UsuarioId;
END;
GO

CREATE OR ALTER PROCEDURE sp_MarcarBienvenidaVista
    @UsuarioId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Usuarios SET BienvenidaVista = 1 WHERE Id = @UsuarioId;
    SELECT @@ROWCOUNT AS FilasAfectadas;
END;
GO
