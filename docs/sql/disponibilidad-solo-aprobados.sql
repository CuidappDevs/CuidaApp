-- Solo un cuidador con la cuenta APROBADA (EstadoAprobacion = 2) puede ponerse "Disponible".
-- Con la cuenta pendiente puede entrar a su panel (si sus documentos están aprobados), pero no ser visible.
-- Siempre puede ponerse NO disponible. Devuelve 0 filas si se intenta activar sin estar aprobado.
-- Además apaga la visibilidad de los cuidadores que hoy están disponibles sin estar aprobados.
-- Idempotente.

CREATE OR ALTER PROCEDURE sp_ActualizarDisponibilidadCuidador
    @CuidadorId INT,
    @Disponible BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE PerfilCuidador SET Disponible = @Disponible
    WHERE UsuarioId = @CuidadorId AND (@Disponible = 0 OR EstadoAprobacion = 2);
    SELECT @@ROWCOUNT AS FilasAfectadas;
END;
GO

UPDATE PerfilCuidador SET Disponible = 0 WHERE Disponible = 1 AND EstadoAprobacion <> 2;
GO
