# Cambios en `CUIDAPP_API`

Todo lo listado aquí requiere **republicar la API** (ver [deployment.md](./deployment.md)) para que tome efecto en producción.

## Servicio de correo (`EmailService`)

- `Interfaces/Email/IEmailService.cs` + `Services/Email/EmailService.cs` — envío de correo vía MailKit, usando las credenciales SMTP de `appsettings.json` (sección `EmailCredentials`, Gmail con contraseña de aplicación).
- `Controllers/EmailController.cs` → **`POST /api/email/enviar`**:
  ```json
  { "destinatario": "correo1@x.com, correo2@x.com", "asunto": "...", "cuerpoHtml": "<h1>...</h1>" }
  ```
  Acepta múltiples destinatarios separados por coma o punto y coma.

## Pagos y finanzas (`PagoAdminController`)

Nuevo, aditivo — no toca `CuidadorService.ObtenerPagosAsync` (usado por la app móvil).

| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/api/pagoadmin?estado=` | Lista pagos (1=Pendiente, 3=Autorizado, 2=Pagado) |
| POST | `/api/pagoadmin/{id}/autorizar` | Pendiente → Autorizado |
| POST | `/api/pagoadmin/{id}/aprobar` | Autorizado → Pagado |

## Centro de soporte (`TicketController`)

Usado tanto por la app móvil (crear/ver mis reportes) como por el panel (gestionar todos).

| Método | Ruta | Quién lo usa |
|---|---|---|
| POST | `/api/ticket` | App móvil — crear reporte |
| GET | `/api/ticket/usuario/{usuarioId}` | App móvil — "Mis reportes" |
| GET | `/api/ticket?estado=` | Panel — listado |
| GET | `/api/ticket/{id}` | Ambos — detalle + hilo de mensajes |
| POST | `/api/ticket/{id}/mensaje` | Ambos — responder |
| PUT | `/api/ticket/{id}/estado` | Panel — cambiar estado |

## Care Partners, Clientes y Administradores (`AdminController`, extendido)

El `AdminController` ya existía con aprobar/rechazar verificación de documentos. Se le agregaron:

**Care Partners**
- `GET /api/admin/cuidadores?estado=` — listado completo (no solo pendientes).
- `GET /api/admin/cuidadores/{id}` — ficha con promedio de calificación y trabajos completados.
- `PUT /api/admin/cuidadores/{id}/info` — editar nombre, especialidad, tarifa, bio, método de cobro.
- `PUT /api/admin/cuidadores/{id}/suspender` / `/reactivar` — impedir/restaurar acceso (bloquea login).
- `GET /api/admin/cuidadores/{id}/sanciones` — historial de suspensiones.

**Clientes** (mismo patrón, mismo backend genérico de suspensión)
- `GET /api/admin/clientes?activo=`
- `GET /api/admin/clientes/{id}`
- `PUT /api/admin/clientes/{id}/info`
- `PUT /api/admin/clientes/{id}/suspender` / `/reactivar`
- `GET /api/admin/clientes/{id}/sanciones`

**Administradores**
- `POST /api/admin/administradores` — crea cuenta con `RolId=1`. Usa el mismo hash de contraseña (SHA256) que `auth/login`, así que la cuenta puede loguearse de inmediato.
- `GET /api/admin/administradores` — listado.
- `PUT /api/admin/administradores/{id}/suspender` / `/reactivar`.

## Endpoints preexistentes reutilizados sin cambios

- `GET /api/admin/cuidadores-pendientes`
- `GET /api/admin/documentos/{cuidadorId}` — devuelve rutas relativas (`/uploads/...`); el cliente arma la URL completa con el origen del servidor.
- `PUT /api/admin/aprobar-cuidador` — aprobar/rechazar verificación de documentos (distinto de suspender la cuenta).

## Autenticación (`auth/login`)

No se modificó. Tanto la app móvil como el panel administrativo llaman al mismo endpoint; el panel simplemente rechaza en el cliente (y ahora también podría hacerlo en servidor si se agrega una política) cualquier respuesta con `RolId != 1`.

## Tareas del servicio (checklist opcional)

El cliente puede definir tareas al solicitar el servicio; el cuidador las marca mientras trabaja y el cliente recibe el evento SignalR `TareaCompletada`. Requiere aplicar [`sql/tareas-trabajo.sql`](./sql/tareas-trabajo.sql) (primero en `DBCuidappDev`).

| Método | Ruta | Qué hace |
|---|---|---|
| POST | `/api/trabajo` | `CrearTrabajoDto` acepta `tareas: string[]` opcional (máx. 20, 200 caracteres c/u) |
| GET | `/api/trabajo/{id}/tareas` | Lista las tareas del trabajo |
| POST | `/api/trabajo/tareas/{tareaId}/completar` | Marca la tarea (solo si el trabajo está En Progreso) y notifica al cliente |


## Propina del cliente (bono al finalizar)

El cliente puede dejar una propina opcional al confirmar que el servicio terminó (montos rápidos RD$100 / 150 / 200 u otro monto). Requiere aplicar [`sql/propina-servicio.sql`](./sql/propina-servicio.sql) (primero en `DBCuidappDev`).

- `PUT /api/trabajo/confirmar-finalizacion` acepta `propina` (decimal, opcional). Se valida entre 0 y 50,000; solo aplica si `confirmado = true`. Error 400 `PROPINA_INVALIDA` si está fuera de rango.
- `Pagos.Propina` (DECIMAL, default 0). El pago al cuidador = `Monto` (tarifa) + `Propina`; `sp_ObtenerGananciasCuidador` ya suma ambos.
- `PagoDto` (cuidador) y `PagoAdminDto` (panel) exponen `Propina`.
- SignalR: evento `PropinaRecibida { trabajoId, monto }` al cuidador cuando recibe una propina.
- Compatibilidad: el parámetro `@Propina` del SP es opcional, así que una API/app anteriores siguen funcionando (propina = 0).


## Alerta automática "hombre muerto" (caída + inmovilidad)

Requiere aplicar [`sql/dead-man-sos.sql`](./sql/dead-man-sos.sql) (primero en `DBCuidappDev`).

- `POST /api/sos/dead-man-triggered` `{ trabajoId, usuarioId, latitud, longitud, impactoG?, segundosInmovil? }`. Solo se acepta si el servicio es del cuidador y está **En Progreso** (400 en otro caso). Crea una alerta `Origen = 'Automatica'`, emite `AlertaSOS` al panel y envía un correo al familiar si el cuidador registró uno. Si ya hay una alerta automática pendiente para ese trabajo no crea otra.
- `SOSAlertas.Origen` (`Manual` | `Automatica`) y `SOSAlertaDto` / evento `AlertaSOS` incluyen `Origen` y el contacto de emergencia (`ContactoNombre/Telefono/Email`).
- `PerfilCuidador` tiene contacto de emergencia (`ContactoEmergenciaNombre/Telefono/Email`): `GET|PUT /api/cuidador/{id}/contacto-emergencia` (requiere nombre y teléfono o correo).
- Limitación: no hay proveedor de SMS/WhatsApp; al familiar se le avisa solo por correo, y el panel muestra su teléfono para que el admin lo llame.
