-- Columna NacionalidadId en Usuarios (común a cliente y cuidador).
-- Acepta NULL y no tiene valor por defecto: los usuarios existentes quedan en NULL y
-- ningún SP, registro ni pantalla cambia (ninguno usa SELECT * ni INSERT sin lista de columnas).
-- Requiere docs/sql/nacionalidades.sql (tabla Nacionalidades).
-- Idempotente.

IF COL_LENGTH(N'dbo.Usuarios', N'NacionalidadId') IS NULL
    ALTER TABLE dbo.Usuarios ADD NacionalidadId INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Usuarios_Nacionalidades')
    ALTER TABLE dbo.Usuarios ADD CONSTRAINT FK_Usuarios_Nacionalidades
        FOREIGN KEY (NacionalidadId) REFERENCES dbo.Nacionalidades (Id);
GO

-- Lectura del catálogo para el registro (solo activas, ordenadas por Id: las más comunes primero).
CREATE OR ALTER PROCEDURE sp_ObtenerNacionalidades
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Nombre, Pais, CodigoIso
    FROM Nacionalidades
    WHERE Activo = 1
    ORDER BY Id;
END;
GO
