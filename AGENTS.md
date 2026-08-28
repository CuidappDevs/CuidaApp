# CuidaApp

## Project Structure

```
CUIDAPP_API/        ASP.NET Core Web API (.NET 10) — backend
CUIDAPP/            .NET MAUI app (.NET 10) — mobile client
CUIDAPP_ADMINISTRATIVO/  Blazor Server (.NET 10) — admin panel (minimal)
```

## Build & Run

```bash
# Build solution
dotnet build CUIDAPP_API.slnx

# Run API (port 5258)
dotnet run --project CUIDAPP_API --launch-profile http

# Run Blazor Admin
dotnet run --project CUIDAPP_ADMINISTRATIVO
```

## API Architecture

**Pattern:** `Controller → IInterface → Service → Stored Procedure`

Each domain has its own folder in Controllers/, Interfaces/, Services/, DTOs/:
- `Trabajo/` — jobs, status, cancellation
- `Cuidador/` — caregiver profiles, availability
- `Cliente/` — client profiles
- `Auth/` — login, registration (roles: 1=Admin, 2=Cliente, 3=Cuidador)
- `Busqueda/` — nearby search
- `Calificacion/` — ratings
- `UbicacionCliente/` — client locations
- `Admin/` — admin operations
- `Realtime/` — SignalR notification service

**Key conventions:**
- SQL Server only — no Entity Framework. All DB access via `SqlCommand` + stored procedures.
- Connection string in `appsettings.json` → `ConnectionStrings.DefaultConnection`
- Services registered as `Scoped` in `Program.cs`. `ITrabajoNotifier` is `Singleton`.
- SignalR hub at `/hubs/trabajo` — clients join group `user-{usuarioId}`
- API docs at `/scalar/v1` (Scalar UI)
- Static files served from `wwwroot/uploads`

**Adding a new endpoint:**
1. Create DTOs in `DTOs/{Domain}/`
2. Add methods to interface in `Interfaces/{Domain}/`
3. Implement in `Services/{Domain}/` using stored procedures
4. Create controller in `Controllers/`
5. Register DI in `Program.cs`

## MAUI App

- Uses `ApiService` for HTTP calls (no DI — instantiated directly)
- `BaseUrl` is compile-time conditional: Android emulator → `10.0.2.2:5258`, Debug → `localhost:5258`, Release → `192.169.179.217`
- SignalR client in `RealtimeService`
- Views organized by domain: `Views/Trabajos/`, `Views/Cliente/`, etc.

## DB Access Pattern

```csharp
using var connection = new SqlConnection(_connectionString);
using var command = new SqlCommand("sp_StoredProcedureName", connection);
command.CommandType = CommandType.StoredProcedure;
command.Parameters.AddWithValue("@Param", value);
await connection.OpenAsync();
var result = await command.ExecuteScalarAsync();
```

## Obsidian Notes

Architecture docs in vault: `C:\Users\Miguel\Downloads\Worlds Library\Akashic Records\Proyectos\CuidaApp\`

Key notes: `Index.md`, `Arquitectura General.md`, `Modelo de Dominio.md`, `API Backend.md`, `MAUI App.md`

Read before making changes to understand domain context.
