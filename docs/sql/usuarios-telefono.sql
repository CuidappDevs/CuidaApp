-- Columna Telefono en Usuarios (cliente y cuidador; se pide en el paso "Cuéntanos sobre ti").
-- Acepta NULL y no tiene valor por defecto: los usuarios existentes quedan en NULL.
-- Idempotente.

IF COL_LENGTH(N'dbo.Usuarios', N'Telefono') IS NULL
    ALTER TABLE dbo.Usuarios ADD Telefono NVARCHAR(20) NULL;
GO
