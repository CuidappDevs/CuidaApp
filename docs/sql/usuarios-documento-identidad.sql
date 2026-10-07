-- Columna DocumentoIdentidad en Usuarios (número de cédula, pasaporte, etc.).
-- Acepta NULL y no tiene valor por defecto: los usuarios existentes quedan en NULL y
-- ningún SP, registro ni pantalla cambia. Por ahora nada la lee ni la escribe.
-- Idempotente.

IF COL_LENGTH(N'dbo.Usuarios', N'DocumentoIdentidad') IS NULL
    ALTER TABLE dbo.Usuarios ADD DocumentoIdentidad NVARCHAR(30) NULL;
GO
