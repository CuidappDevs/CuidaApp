/*
  LOTE-2 - Verificacion reproducible de sp_SuspenderUsuario / sp_ReactivarUsuario /
  sp_FinalizarSuspensionTemporalVencida y coherencia con Usuarios.IsActive.

  REQUIERE AUTORIZACION EXPLICITA (Gate G2) para ejecutarse con @Ejecutar = 1.
  Por defecto (@Ejecutar = 0) solo informa y NO escribe nada.
  Con @Ejecutar = 1 crea unicamente usuarios sinteticos con una etiqueta unica dentro de una
  transaccion externa. EL ROLLBACK FINAL ES OBLIGATORIO: no se debe cambiar a COMMIT.
  No usa usuarios reales ni imprime motivos, correos o contrasenas.
  Supone que Usuarios acepta INSERT con (Email, PasswordHash, NombreCompleto, RolId, IsActive);
  si hubiera otras columnas NOT NULL sin default el script falla y hace rollback.
*/
SET NOCOUNT ON;

DECLARE @Ejecutar bit = 0;   -- 1 solo con autorizacion G2

IF @Ejecutar = 0
BEGIN
    SELECT 'Modo seguro: sin escrituras. Cambie @Ejecutar a 1 solo con autorizacion G2.' AS Info;
    RETURN;
END;

IF OBJECT_ID(N'dbo.SancionesUsuario', N'U') IS NULL
   OR OBJECT_ID(N'dbo.sp_SuspenderUsuario', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_ReactivarUsuario', N'P') IS NULL
   OR OBJECT_ID(N'dbo.sp_FinalizarSuspensionTemporalVencida', N'P') IS NULL
    THROW 50021, 'LOTE-2 verificacion: migracion/SP no aplicados (G1 pendiente).', 1;

DECLARE @Tag varchar(20) = 'L2T' + REPLACE(CONVERT(varchar(36), NEWID()), '-', '');
DECLARE @Resultados TABLE (Caso varchar(40), CodigoEsperado varchar(40), CodigoObtenido varchar(40),
                           EstadoEsperado varchar(20), EstadoObtenido varchar(20), Resultado varchar(4));
CREATE TABLE #r (ResultadoCodigo varchar(40), UsuarioId int, SancionId int NULL, IsActive bit NULL,
                 EstadoSancion varchar(20) NULL, FechaFinUtc datetime2(7) NULL);

BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @Admin int, @Cli int, @Cli2 int, @Cli3 int, @Cli4 int, @Ahora datetime2(7) = SYSUTCDATETIME();
    DECLARE @Motivo nvarchar(500) = N'Motivo sintetico de prueba';
    DECLARE @Futuro datetime2(7) = DATEADD(DAY, 1, @Ahora);

    INSERT INTO dbo.Usuarios (Email, PasswordHash, NombreCompleto, RolId, IsActive)
    VALUES (@Tag + 'a@test.invalid', 'x', @Tag + 'admin', 1, 1);  SET @Admin = SCOPE_IDENTITY();
    INSERT INTO dbo.Usuarios (Email, PasswordHash, NombreCompleto, RolId, IsActive)
    VALUES (@Tag + 'c1@test.invalid', 'x', @Tag + 'c1', 2, 1);    SET @Cli = SCOPE_IDENTITY();
    INSERT INTO dbo.Usuarios (Email, PasswordHash, NombreCompleto, RolId, IsActive)
    VALUES (@Tag + 'c2@test.invalid', 'x', @Tag + 'c2', 2, 1);    SET @Cli2 = SCOPE_IDENTITY();
    INSERT INTO dbo.Usuarios (Email, PasswordHash, NombreCompleto, RolId, IsActive)
    VALUES (@Tag + 'c3@test.invalid', 'x', @Tag + 'c3', 2, 0);    SET @Cli3 = SCOPE_IDENTITY();
    INSERT INTO dbo.Usuarios (Email, PasswordHash, NombreCompleto, RolId, IsActive)
    VALUES (@Tag + 'c4@test.invalid', 'x', @Tag + 'c4', 2, 1);    SET @Cli4 = SCOPE_IDENTITY();

    -- 1. suspension indefinida aplicada
    DELETE FROM #r; INSERT #r EXEC dbo.sp_SuspenderUsuario @Cli, @Admin, @Motivo, NULL;
    INSERT @Resultados SELECT '1 indefinida', 'APPLIED', ResultadoCodigo, 'VIGENTE', EstadoSancion,
        CASE WHEN ResultadoCodigo = 'APPLIED' AND EstadoSancion = 'VIGENTE' AND IsActive = 0 THEN 'PASS' ELSE 'FAIL' END FROM #r;

    -- 2. segunda suspension
    DELETE FROM #r; INSERT #r EXEC dbo.sp_SuspenderUsuario @Cli, @Admin, @Motivo, NULL;
    INSERT @Resultados SELECT '2 duplicada', 'ALREADY_SUSPENDED', ResultadoCodigo, 'VIGENTE', EstadoSancion,
        CASE WHEN ResultadoCodigo = 'ALREADY_SUSPENDED' AND (SELECT COUNT(*) FROM dbo.SancionesUsuario WHERE UsuarioId = @Cli AND Estado = 'VIGENTE') = 1 THEN 'PASS' ELSE 'FAIL' END FROM #r;

    -- 3. reactivacion manual
    DELETE FROM #r; INSERT #r EXEC dbo.sp_ReactivarUsuario @Cli, @Admin;
    INSERT @Resultados SELECT '3 reactivar vigente', 'APPLIED', ResultadoCodigo, 'REVOCADA', EstadoSancion,
        CASE WHEN ResultadoCodigo = 'APPLIED' AND EstadoSancion = 'REVOCADA' AND IsActive = 1
              AND EXISTS (SELECT 1 FROM dbo.SancionesUsuario WHERE UsuarioId = @Cli AND Estado = 'REVOCADA' AND RevocadaPorAdminId = @Admin) THEN 'PASS' ELSE 'FAIL' END FROM #r;

    -- 4. reactivar cuenta ya activa
    DELETE FROM #r; INSERT #r EXEC dbo.sp_ReactivarUsuario @Cli, @Admin;
    INSERT @Resultados SELECT '4 ya activa', 'ALREADY_ACTIVE', ResultadoCodigo, NULL, EstadoSancion,
        CASE WHEN ResultadoCodigo = 'ALREADY_ACTIVE' THEN 'PASS' ELSE 'FAIL' END FROM #r;

    -- 5. suspension temporal futura
    DELETE FROM #r; INSERT #r EXEC dbo.sp_SuspenderUsuario @Cli2, @Admin, @Motivo, @Futuro;
    INSERT @Resultados SELECT '5 temporal futura', 'APPLIED', ResultadoCodigo, 'VIGENTE', EstadoSancion,
        CASE WHEN ResultadoCodigo = 'APPLIED' AND FechaFinUtc = @Futuro AND IsActive = 0
              AND EXISTS (SELECT 1 FROM dbo.SancionesUsuario WHERE UsuarioId = @Cli2 AND Tipo = 'TEMPORAL') THEN 'PASS' ELSE 'FAIL' END FROM #r;

    -- 6. reactivacion manual de temporal aun vigente
    DELETE FROM #r; INSERT #r EXEC dbo.sp_ReactivarUsuario @Cli2, @Admin;
    INSERT @Resultados SELECT '6 reactivar temporal', 'APPLIED', ResultadoCodigo, 'REVOCADA', EstadoSancion,
        CASE WHEN ResultadoCodigo = 'APPLIED' AND EstadoSancion = 'REVOCADA' AND IsActive = 1 THEN 'PASS' ELSE 'FAIL' END FROM #r;

    -- 7. vencimiento temporal (sancion sintetica ya vencida insertada directamente)
    INSERT INTO dbo.SancionesUsuario (UsuarioId, AdminId, Accion, Motivo, Tipo, Estado, FechaInicioUtc, FechaFinUtc)
    VALUES (@Cli4, @Admin, 'SUSPENSION', @Motivo, 'TEMPORAL', 'VIGENTE', DATEADD(HOUR, -2, @Ahora), DATEADD(HOUR, -1, @Ahora));
    UPDATE dbo.Usuarios SET IsActive = 0 WHERE Id = @Cli4;
    DELETE FROM #r; INSERT #r EXEC dbo.sp_FinalizarSuspensionTemporalVencida @Cli4;
    INSERT @Resultados SELECT '7 vencimiento', 'APPLIED', ResultadoCodigo, 'CUMPLIDA', EstadoSancion,
        CASE WHEN ResultadoCodigo = 'APPLIED' AND EstadoSancion = 'CUMPLIDA' AND IsActive = 1
              AND (SELECT IsActive FROM dbo.Usuarios WHERE Id = @Cli4) = 1 THEN 'PASS' ELSE 'FAIL' END FROM #r;

    -- 8a. usuario inexistente
    DELETE FROM #r; INSERT #r EXEC dbo.sp_SuspenderUsuario -1, @Admin, @Motivo, NULL;
    INSERT @Resultados SELECT '8a inexistente', 'USER_NOT_FOUND', ResultadoCodigo, NULL, EstadoSancion,
        CASE WHEN ResultadoCodigo = 'USER_NOT_FOUND' THEN 'PASS' ELSE 'FAIL' END FROM #r;
    -- 8b. autor sin rol administrador (un cliente sintetico)
    DELETE FROM #r; INSERT #r EXEC dbo.sp_SuspenderUsuario @Cli, @Cli2, @Motivo, NULL;
    INSERT @Resultados SELECT '8b autor no admin', 'ADMIN_ROLE_REQUIRED', ResultadoCodigo, NULL, EstadoSancion,
        CASE WHEN ResultadoCodigo = 'ADMIN_ROLE_REQUIRED' THEN 'PASS' ELSE 'FAIL' END FROM #r;

    -- 9a. inactivo sin sancion vigente (@Cli3 se creo inactivo)
    DELETE FROM #r; INSERT #r EXEC dbo.sp_ReactivarUsuario @Cli3, @Admin;
    INSERT @Resultados SELECT '9a inactivo sin sancion', 'STATE_CONFLICT', ResultadoCodigo, NULL, EstadoSancion,
        CASE WHEN ResultadoCodigo = 'STATE_CONFLICT' THEN 'PASS' ELSE 'FAIL' END FROM #r;
    -- 9b. activo con sancion vigente
    INSERT INTO dbo.SancionesUsuario (UsuarioId, AdminId, Accion, Motivo, Tipo, Estado, FechaInicioUtc, FechaFinUtc)
    VALUES (@Cli, @Admin, 'SUSPENSION', @Motivo, 'INDEFINIDA', 'VIGENTE', @Ahora, NULL);  -- @Cli esta activo
    DELETE FROM #r; INSERT #r EXEC dbo.sp_ReactivarUsuario @Cli, @Admin;
    INSERT @Resultados SELECT '9b activo con sancion', 'STATE_CONFLICT', ResultadoCodigo, 'VIGENTE', EstadoSancion,
        CASE WHEN ResultadoCodigo = 'STATE_CONFLICT' THEN 'PASS' ELSE 'FAIL' END FROM #r;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DROP TABLE IF EXISTS #r;
    THROW;
END CATCH;

SELECT Caso, CodigoEsperado, CodigoObtenido, EstadoEsperado, EstadoObtenido, Resultado AS [PASS/FAIL]
FROM @Resultados;  -- la variable de tabla sobrevive al rollback

-- ROLLBACK OBLIGATORIO: no sustituir por COMMIT.
ROLLBACK TRANSACTION;
DROP TABLE IF EXISTS #r;
