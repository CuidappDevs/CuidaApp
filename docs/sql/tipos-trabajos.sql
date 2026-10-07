-- Tipos de trabajo del registro de cuidadores (paso "¿Qué trabajo haces?")
--
-- La app arma las opciones desde la tabla TiposTrabajos (los datos se cargan/editan a mano
-- en la tabla; este script solo crea el procedimiento de lectura).
--
-- El Nombre del tipo elegido se guarda tal cual como texto en PerfilCuidador.Especialidad
-- (igual que antes de este cambio): por eso no se toca sp_CrearUsuarioCuidador.
-- Icono: clave que la app traduce a un ícono vectorial
--   ("limpieza", "ninos", "adulto_mayor", "hospital", "cocina"; otra clave = ícono genérico).
-- Activo = 0: la opción aparece en la app como "No disponible" y no se puede elegir.
--
-- Ejecutar con sqlcmd -f 65001 (UTF-8) para no dañar acentos ni la ñ.

CREATE OR ALTER PROCEDURE sp_ObtenerTiposTrabajos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Nombre, Descripcion, Icono, Activo
    FROM TiposTrabajos
    ORDER BY Activo DESC, Id;
END;
GO
