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
- `Cuidador/` — caregiver profiles, availability, GPS, documents, earnings + automatic visibility schedule (`HorarioCuidador`; `HorarioVisibilidadService` applies it every minute; in that mode manual availability changes are rejected)
- `Cliente/` — client profiles
- `Busqueda/` — nearby search (GPS-based)
- `Calificacion/` — ratings (1-5 stars)
- `UbicacionCliente/` — client saved locations
- `Admin/` — admin operations (approve/suspend caregivers, manage clients, sanctions) + `AdminOperacionesController` (dashboard, services, verification, map, ratings, finance, catalogs, audit log, mass notices) using the generic `IAdminOperacionesService` (SP → rows)
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
- SignalR events: AvisoGeneral (global, from the panel), NuevaSolicitud, TrabajoActualizado, DisponibilidadCambio, UbicacionCuidadorCambio, MensajeNuevo, ActividadAgregada, AlertaGeocerca, UsuarioEscribiendo, CuentaActualizada, PagoAprobado, TicketActualizado
- App notifications: `Services/AvisosApp.cs` builds them (banner in foreground, native in background; tapping opens `NotificacionDestino`); `Services/Recordatorios.cs` schedules local ones (1 h and 15 min before an accepted service, rating reminder 24 h after completion) that arrive even with the app closed; it also schedules the start/end notices of the caregiver's automatic schedule (`ProgramarHorario`, 7-day window renewed on every dashboard load)
- Hub methods (client → server): `Unirse(usuarioId)`, `Escribiendo(conversacionId, usuarioId, escribiendo)` (chat typing indicator, forwarded to the other participant)
- **Admin security** (`Seguridad/`): JWT is validated; only admin endpoints use `[Authorize(Policy = PoliticasAdmin.X)]` (`Admin`, `AdminOperaciones`, `AdminFinanzas`, `SuperAdmin`, from `Usuarios.NivelAdmin`: 1 Superadmin, 2 Operaciones, 3 Finanzas). Add `[AuditarAdmin]` to admin controllers so POST/PUT/DELETE are logged to `AuditoriaAdmin`. The mobile app sends no token, so never put `[Authorize]` on endpoints the app uses
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
- Static services: RealtimeService, LocationService, ServerClock, NativeNotifier, GlobalNotifier, RastreoUbicacion (sends caregiver location + battery every 20 s while visible or in service, also in background; the foreground service adds the `location` type when permission is granted), CheckinApp (full-screen "¿Estás bien?" from the panel)
- Mapbox + Leaflet for maps (WebView)
- Plugin.Maui.Audio for voice messages
- Colors defined in `Resources/Styles/Colors.xaml` as StaticResource
- Dark mode (`Services/Tema.cs`, selector `Views/Comun/SelectorTema` in both profiles: Light / Dark / Automatic). Light values live in `Colors.xaml`, dark values in `Tema.Paleta`; `Tema.Aplicar` writes them into the palette before pages are created, and changing the theme recreates the UI. Never hard-code background/text/border colors: in XAML use `{StaticResource ColorSurface|ColorBackground|ColorTextStrong|ColorTextMuted|ColorBorder|ColorSubtle|ColorPlaceholder|ColorChevron|ColorPrimary(Soft)|ColorSuccess(Soft)|ColorDanger(Soft)|ColorWarning(Soft)|ColorShadow}`, in C# `Tema.C("ColorX")`. Fixed colors are only OK for content on colored headers (white/translucent white), illustrations and brand accents. Maps use `Tema.EstiloMapa`
- Never anchor a button/footer to the bottom edge of a page (overlay with `VerticalOptions="End"`, or a bottom grid row): in this app it ends up under the gesture bar, whatever the safe-area mode. Put the main action inside the scroll content (a card at the end), followed by an empty `ContentView` spacer of `24 + BarraEstado.AltoInferior()`. Pages with a blue header stay edge-to-edge (`SafeAreaEdges="None"` + header margin `BarraEstado.Alto()`), like `CuidadoresPorServicioPage`/`SolicitarServicioPage`. Never use a transparent `BoxView` as a spacer (it renders dark on Android)
- Alerts: never use `DisplayAlert`; use `Alerta.MostrarAsync(titulo, mensaje, boton)` or `Alerta.MostrarAsync(titulo, mensaje, aceptar, cancelar)` → `bool` (`Helpers/Alerta.cs`, custom design, optional `TipoAlerta`). On Android it is a native transparent dialog, so it does not trigger OnAppearing/OnDisappearing on the page below

## Multi-idioma (MAUI)

La app está en **español (base), inglés y creol haitiano (`ht`)**. Reglas:
- **Nunca escribir texto visible fijo.** Todo texto de UI (XAML, `DisplayAlert`, `Text = ...`, notificaciones, estados) va por clave.
- XAML: `Text="{loc:T clave}"` (con `xmlns:loc="clr-namespace:CUIDAPP.Localization"`). C#: `Localizador.T("clave")` o `Localizador.F("clave", arg0, arg1)` con `{0}`, `{1}` en el texto.
- Textos que vienen de la BD (especialidades, motivos de cancelación, documentos): `Localizador.D(texto)`; la traducción vive en la clave `dato_{texto_normalizado}` (minúsculas, sin acentos, `_`). Si no hay traducción, se muestra el texto original.
- Los textos están en `CUIDAPP/Resources/Strings/{es,en}.json` (clave → texto). Agregar la clave en **todos** los idiomas y correr `python CUIDAPP/Resources/Strings/verificar_traducciones.py` (falla si falta una clave o cambian los `{n}`).
- Agregar un idioma: crear `Resources/Strings/{codigo}.json` y añadirlo a `Localizador.Idiomas`. La elección se guarda en `Preferences["Idioma"]`; el selector (`<loc:SelectorIdioma/>`) está en login y en los perfiles.
- Lo que se **guarda en la BD** o viaja al servidor (categoría de ticket, especialidad, texto de mensajes de chat) sigue en español a propósito; solo se traduce al mostrarlo.

## Blazor Admin

- Cookie-based authentication (8h, no sliding). The API JWT travels inside the cookie (claim `api_token`, plus `nivel_admin`) and every API service attaches it with `SesionAdmin.Adjuntar` (`Services/SesionAdmin.cs`); the layout logs out when the token expires
- Menu sections are filtered by admin level (Superadmin / Operaciones / Finanzas); pages check `SesionAdmin.PuedeOperar/PuedeFinanzas/EsSuperadmin`
- Pages: Dashboard (real data), SOS (persistent: loads pending from the API, history), Servicios + detalle (timeline, tasks, payment, read-only chat, cancel/complete), Mapa en vivo (Leaflet + OSM, `wwwroot/js/panel.js`), Verificación, Care Partners, Clientes, Calificaciones, Soporte, Avisos masivos, Pagos, Finanzas (CSV export), Catálogos, Auditoría, Administradores (levels), Buscar
- `PanelApiService` for the new endpoints; models in `Models/Panel/PanelModels.cs`
- `/mapa` is the command center ("Ojo de Dios"): layer chips, click a marker → side card with actions (direct notification, "¿Estás bien?" check-in, follow, call/WhatsApp, today's route replay, hide), draw an area to notify caregivers inside, heat map, alerts tab with sound. Map logic lives in `wwwroot/js/panel.js` (`cuidPanel.*`, calls back into the page via `DotNetObjectReference`)
- Shared components in `Components/Shared/`: `DialogoConfirmacion` (+ `ConfirmacionService`, toasts via `AvisoToastService`), `Paginador`, `GraficoBarras`, `Avatar`, `Estrellas`, `Esqueleto`, `EstadoVacio`. Every destructive action must go through `ConfirmacionService`
- CSS: `wwwroot/css/admin.css` (base) + `wwwroot/css/panel.css` (`pn-*` components; ease-out motion under 250 ms, hover only with a mouse, `prefers-reduced-motion`)
- Local preview against a local API: `.claude/launch.json` → `cuidapp-administrativo-local`

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
- Only admin endpoints require a JWT; endpoints used by the mobile app are still unauthenticated (the app never sends its token)
- `appsettings.Development.json` missing `EmailCredentials` — EmailService throws in Development
- Credentials committed in plaintext (DB password, Gmail app password)

### MAUI
- `ApiService.BaseUrl` hardcoded to production (conditional compilation commented out)
- `ApiService` not using DI properly (registered Singleton but instantiated with `new`)
- SSL certificate validation disabled
- JWT token stored in Preferences but never sent as Authorization header
- Android `colors.xml` still has MAUI template defaults (#512BD4)

### Blazor
- `Iniciales()` helper duplicated in some older pages (new code uses `Components/Shared/Avatar`)
- Lists (Care Partners, Clientes, Soporte, Pagos) are paginated client-side: the API still returns the full list
