# Panel administrativo (`CUIDAPP_ADMINISTRATIVO`)

Blazor Server, .NET 10. Publicado en el mismo servidor que la API, dentro de IIS.

## Autenticación

El login es real (no simulado): llama a `auth/login` en `CUIDAPP_API` — el mismo endpoint que usa la app móvil — y **rechaza cualquier cuenta con `RolId != 1`**.

**Cómo sobrevive un refresh de página** (esto era un problema real que se resolvió): Blazor Server corre sobre un circuito de SignalR; un componente interactivo no puede escribir cookies de respuesta HTTP directamente. La solución:

1. `Login.razor` valida credenciales contra la API y, si es admin, guarda el resultado en `PendingLoginStore` (un diccionario en memoria con código de un solo uso, expira a los 2 minutos).
2. Navega con `forceLoad: true` a `/account/login-complete?code=...` — esto es una **request HTTP real**, no navegación de Blazor.
3. Ese endpoint mínimo (en `Program.cs`) recupera el código, arma una cookie de autenticación de ASP.NET Core (`CuidappAdminAuth`, HttpOnly, 8 horas, sliding expiration) con los claims del usuario, y redirige a `/dashboard`.
4. Como es una cookie de navegador normal, **sobrevive a un refresh completo** — cada nueva carga de página llega con la cookie, y Blazor la lee vía `AuthenticationState`.
5. Logout: `GET /account/logout` limpia la cookie.

Cada página protegida (`AdminLayout.razor`) verifica `AuthenticationState` en `OnInitializedAsync` y redirige a `/` si no hay sesión o el rol no es 1.

## Módulos construidos

| Módulo | Ruta | Qué permite |
|---|---|---|
| Dashboard | `/dashboard` | Métricas generales (parcialmente con datos de ejemplo — ver [roadmap.md](./roadmap.md)) |
| Care Partners | `/care-partners`, `/care-partners/{id}` | Listado real filtrable (Pendientes/Verificadas/Rechazadas/Todas), ficha con documentos reales (ver/descargar), **Impedir acceso** (verificación de documentos + suspensión de cuenta separadas), **Editar información** |
| Clientes | `/clientes`, `/clientes/{id}` | Mismo patrón que Care Partners: listado, ficha, editar información, impedir acceso |
| Pagos y finanzas | `/pagos` | Flujo Pendiente → Autorizado → Pagado, con quién autorizó/aprobó y cuándo |
| Soporte | `/soporte`, `/soporte/{id}` | Tickets de clientes y Care Partners, hilo de mensajes, cambio de estado |
| Administradores | `/administradores` | Crear cuentas con acceso al panel, suspender/reactivar (no se puede auto-suspender) |

## Diseño

Sistema de diseño propio en `wwwroot/css/admin.css` (clases `adm-*`, variables `--cuid-blue` etc.) — coherente en todas las pantallas. Se limpiaron los restos de la plantilla por defecto de Blazor (`Counter.razor`, `Weather.razor`, `MainLayout.razor`, `NavMenu.razor` — eliminados por no usarse y no seguir el sistema de diseño).

## Configuración de entorno (`ApiBaseUrl`)

- `appsettings.json` (producción): `http://192.169.179.217/api/` — la misma API real que usa la app móvil.
- `appsettings.Development.json`: **sin `ApiBaseUrl` propio** — hereda el de producción a propósito, para que correr el panel en local (`dotnet run`) siempre apunte a la API real, no a una copia local. Si necesitas apuntar a tu API local mientras desarrollas, agrega temporalmente `"ApiBaseUrl": "http://localhost:5258/api/"` a `appsettings.Development.json` y revirtiéndolo después.

## Patrón de servicios HTTP

Cada módulo tiene su propio `HttpClient` tipado registrado en `Program.cs` (`AdminAuthService`, `PagoAdminApiService`, `TicketAdminApiService`, `CuidadorAdminApiService`, `ClienteAdminApiService`, `AdminAccountApiService`), todos apuntando al mismo `ApiBaseUrl`. Cada uno expone un `ServerOrigin` cuando necesita armar URLs completas de archivos (fotos, documentos) a partir de las rutas relativas que devuelve la API.
