-- Rol "Recurrente" (Id = 4)
-- Reservado para una funcionalidad futura: cuando una persona contrata formalmente a un
-- cuidador como si fuera un empleado (servicio recurrente). Por ahora solo se crea el rol;
-- ningún registro, login ni pantalla lo usa todavía.
--
-- Se fuerza el Id 4 para que sea el mismo en DBCuidappDev y DBCuidapp.
-- Es idempotente: si el rol ya existe, no hace nada.

IF NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 4 OR Nombre = N'Recurrente')
BEGIN
    SET IDENTITY_INSERT Roles ON;
    INSERT INTO Roles (Id, Nombre) VALUES (4, N'Recurrente');
    SET IDENTITY_INSERT Roles OFF;
END;

SELECT * FROM Roles ORDER BY Id;
