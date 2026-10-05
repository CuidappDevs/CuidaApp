-- Tareas opcionales que el cliente define al solicitar un servicio.
-- Aplicar PRIMERO en DBCuidappDev, verificar, y luego en DBCuidapp (producción).
-- Cambio aditivo: no modifica tablas ni SPs existentes.

IF OBJECT_ID('dbo.TareasTrabajo', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TareasTrabajo (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        TrabajoId       INT           NOT NULL,
        Descripcion     NVARCHAR(200) NOT NULL,
        Orden           INT           NOT NULL DEFAULT 0,
        Completada      BIT           NOT NULL DEFAULT 0,
        FechaCompletada DATETIME      NULL,
        CONSTRAINT FK_TareasTrabajo_Trabajos FOREIGN KEY (TrabajoId) REFERENCES dbo.Trabajos(Id)
    );
    CREATE INDEX IX_TareasTrabajo_TrabajoId ON dbo.TareasTrabajo(TrabajoId);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AgregarTareaTrabajo
    @TrabajoId INT,
    @Descripcion NVARCHAR(200),
    @Orden INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.TareasTrabajo (TrabajoId, Descripcion, Orden) VALUES (@TrabajoId, @Descripcion, @Orden);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerTareasTrabajo
    @TrabajoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, TrabajoId, Descripcion, Orden, Completada, FechaCompletada
    FROM dbo.TareasTrabajo
    WHERE TrabajoId = @TrabajoId
    ORDER BY Orden, Id;
END
GO

-- Marca una tarea como completada solo si el trabajo está En Progreso (Estado = 3)
-- y la tarea aún no estaba completada. Si no cumple, no devuelve filas.
CREATE OR ALTER PROCEDURE dbo.sp_CompletarTareaTrabajo
    @TareaId INT,
    @FechaCompletada DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE t
    SET Completada = 1, FechaCompletada = @FechaCompletada
    OUTPUT inserted.Id, inserted.TrabajoId, inserted.Descripcion, inserted.Orden, inserted.Completada, inserted.FechaCompletada
    FROM dbo.TareasTrabajo t
    INNER JOIN dbo.Trabajos j ON j.Id = t.TrabajoId
    WHERE t.Id = @TareaId AND t.Completada = 0 AND j.Estado = 3;
END
GO
