# CuidaApp

## Project Structure

```
CUIDAPP_API/              ASP.NET Core Web API (.NET 10) — backend (14 controllers, ~58 endpoints)
CUIDAPP/                  .NET MAUI app (.NET 10) — mobile client (30 pages, 9 services)
CUIDAPP_ADMINISTRATIVO/   Blazor Server (.NET 10) — admin panel (functional)
```

## Build & Run

```bash
# Build solution
dotnet build CUIDAPP_API.slnx

# Run API (port 5258)
dotnet run --project CUIDAPP_API --launch-profile http

# Run Blazor Admin (port 5203)
dotnet run --project CUIDAPP_ADMINISTRATIVO
```

## API Architecture

**Pattern:** `Controller → IInterface → Service → Stored Procedure`

Each domain has its own folder in Controllers/, Interfaces/, Services/, DTOs/:
- `Auth/` — login, registration, forgot/reset password (roles: 1=Admin, 2=Cliente, 3=Cuidador)
- `Trabajo/` — jobs, status, cancellation, PIN, geofence, activities (15 SPs)
- `Cuidador/` — caregiver profiles, availability, GPS, documents, earnings
- `Cliente/` — client profiles
- `Busqueda/` — nearby search (GPS-based)
- `Calificacion/` — ratings (1-5 stars)
- `UbicacionCliente/` — client saved locations
- `Admin/` — admin operations (approve/suspend caregivers, manage clients, sanctions)
- `Chat/` — real-time chat (text, image, audio messages)
- `Email/` — MailKit email service (SMTP via `EmailCredentials` config)
- `Ticket/` — support tickets (create, message, status)
- `PagoAdmin/` — payment authorization/approval
- `Sistema/` — server time sync
- `Upload/` — file upload (max 10MB: jpg, png, pdf, m4a, mp3, wav, aac)

**Key conventions:**
- SQL Server only — no Entity Framework. All DB access via `SqlCommand` + stored procedures (~60 SPs + 3 raw SQL).
- Connection string in `appsettings.json` → `ConnectionStrings.DefaultConnection`
- Email config in `appsettings.json` → `EmailCredentials` (SMTP Gmail)
- Services registered as `Scoped` in `Program.cs`. `ITrabajoNotifier` is `Singleton`.
- SignalR hub at `/hubs/trabajo` — clients join group `user-{usuarioId}`
- 7 SignalR events: NuevaSolicitud, TrabajoActualizado, DisponibilidadCambio, UbicacionCuidadorCambio, MensajeNuevo, ActividadAgregada, AlertaGeocerca
- API docs at `/scalar/v1` (Scalar UI)
- Static files served from `wwwroot/uploads`
- `HoraLocalRD.Ahora` utility for server timezone (UTC-4)

**Adding a new endpoint:**
1. Create DTOs in `DTOs/{Domain}/`
2. Add methods to interface in `Interfaces/{Domain}/`
3. Implement in `Services/{Domain}/` using stored procedures
4. Create controller in `Controllers/`
5. Register DI in `Program.cs`

## MAUI App

- Uses `ApiService` for HTTP calls (instantiated with `new` in each page, not from DI)
- `BaseUrl` is currently hardcoded to production (`http://192.169.179.217/api/`)
- SignalR client in `RealtimeService` (static class)
- 30 pages organized by domain: `Views/Trabajos/`, `Views/Cliente/`, `Views/Auth/`, etc.
- Static services: RealtimeService, LocationService, ServerClock, NativeNotifier, GlobalNotifier
- Mapbox + Leaflet for maps (WebView)
- Plugin.Maui.Audio for voice messages
- Colors defined in `Resources/Styles/Colors.xaml` as StaticResource

## Blazor Admin

- Cookie-based authentication (8h expiry, sliding)
- Pages: Login, Dashboard (mock data), Care Partners, Clientes, Pagos, Soporte, Administradores
- Services: AdminAuthService, CuidadorAdminApiService, ClienteAdminApiService, PagoAdminApiService, TicketAdminApiService, AdminAccountApiService
- CSS design system in `wwwroot/css/admin.css` (685 lines)

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

Key notes: `Index.md`, `Arquitectura General.md`, `Modelo de Dominio.md`, `API Backend.md`, `MAUI App.md`, `Blazor Admin.md`, `Tablas de Base de Datos.md`, `Patrones de Programación.md`, `Configuración y Deploy.md`, `Roadmap de Desarrollo.md`

Read before making changes to understand domain context.

## Known Issues

### Critical
- `UseAuthentication()` not called in API Program.cs — JWT tokens are never validated
- No `[Authorize]` on any API endpoint — all endpoints are unauthenticated
- `appsettings.Development.json` missing `EmailCredentials` — EmailService throws in Development
- Credentials committed in plaintext (DB password, Gmail app password)

### MAUI
- `ApiService.BaseUrl` hardcoded to production (conditional compilation commented out)
- `ApiService` not using DI properly (registered Singleton but instantiated with `new`)
- SSL certificate validation disabled
- JWT token stored in Preferences but never sent as Authorization header
- Android `colors.xml` still has MAUI template defaults (#512BD4)

### Blazor
- Dashboard uses mock data, not API
- `Iniciales()` helper duplicated in 4 files
- No pagination on list pages
- No confirmation dialogs for destructive actions
