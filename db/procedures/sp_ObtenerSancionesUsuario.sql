/*
  LOTE-2. NO APLICAR sin autorizacion (Gate G1); requiere la migracion 20261003_lote_2_sanciones_usuario_up.sql.
  Historial de sanciones de un usuario (contrato 5.3), mas reciente primero.
  Una temporal vencida aun no materializada conserva Estado = VIGENTE y EstaVigente = 0.
*/
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerSancionesUsuario
    @UsuarioId int
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AhoraUtc datetime2(7) = SYSUTCDATETIME();

    SELECT s.Id, s.UsuarioId, s.AdminId, a.NombreCompleto AS AdminNombre, s.Accion, s.Motivo,
           s.Tipo, s.Estado, s.FechaInicioUtc, s.FechaFinUtc,
           s.RevocadaPorAdminId, r.NombreCompleto AS RevocadaPorAdminNombre, s.FechaRevocacionUtc,
           CAST(CASE WHEN s.Estado = 'VIGENTE' AND (s.FechaFinUtc IS NULL OR s.FechaFinUtc > @AhoraUtc)
                     THEN 1 ELSE 0 END AS bit) AS EstaVigente
    FROM dbo.SancionesUsuario s
    INNER JOIN dbo.Usuarios a ON a.Id = s.AdminId
    LEFT JOIN dbo.Usuarios r ON r.Id = s.RevocadaPorAdminId
    WHERE s.UsuarioId = @UsuarioId
    ORDER BY s.FechaInicioUtc DESC;
END;
