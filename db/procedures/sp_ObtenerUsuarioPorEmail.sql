/*
  LOTE-2. NO APLICAR sin autorizacion (Gate G1); requiere la migracion 20261003_lote_2_sanciones_usuario_up.sql.
  Solo lectura (contrato 5.4). Las columnas Sancion* corresponden a la unica fila VIGENTE,
  incluso si su fecha ya vencio; la materializacion ocurre en sp_FinalizarSuspensionTemporalVencida.
*/
CREATE OR ALTER PROCEDURE dbo.sp_ObtenerUsuarioPorEmail
    @Email NVARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT u.Id, u.Email, u.PasswordHash, u.NombreCompleto, u.FotoUrl, u.RolId, u.IsActive,
           u.FechaCreacion, p.EstadoAprobacion,
           s.Id             AS SancionId,
           s.Estado         AS SancionEstado,
           s.Motivo         AS SancionMotivo,
           s.Tipo           AS SancionTipo,
           s.FechaFinUtc    AS SancionFechaFinUtc
    FROM dbo.Usuarios u
    LEFT JOIN dbo.PerfilCuidador p ON p.UsuarioId = u.Id
    LEFT JOIN dbo.SancionesUsuario s ON s.UsuarioId = u.Id AND s.Estado = 'VIGENTE'
    WHERE u.Email = @Email;
END;
