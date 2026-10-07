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

## Chat: indicador de "está escribiendo"

- Hub `/hubs/trabajo`: nuevo método `Escribiendo(conversacionId, usuarioId, escribiendo)`. El cliente lo invoca mientras el usuario escribe (como máximo cada 3 s) y con `false` al dejar de escribir o al enviar.
- El servidor busca los participantes de la conversación (cliente y cuidador del trabajo; se cachean en memoria) y reenvía al **otro** el evento `UsuarioEscribiendo` `{ conversacionId, usuarioId, escribiendo }`. Si quien avisa no es participante, no se envía nada.
- No usa base de datos nueva ni stored procedures.

## Tipos de trabajo dinámicos (registro de cuidadores)

- Nuevo `GET /api/tipotrabajo` → `[{ id, nombre, descripcion, icono, activo }]` (todos, activos primero). SP `sp_ObtenerTiposTrabajos` (script en `docs/sql/tipos-trabajos.sql`).
- La app arma el paso "¿Qué trabajo haces?" con esta lista. Los tipos con `activo = false` se muestran como "No disponible" y no se pueden elegir. Si el endpoint falla, la app usa las 3 opciones de siempre.
- Sin cambios en el registro: se sigue enviando `Especialidad` como **texto** (el `Nombre` del tipo) y `sp_CrearUsuarioCuidador` no cambia. Por eso el `Nombre` en `TiposTrabajos` debe coincidir con el texto que se quiere guardar en `PerfilCuidador.Especialidad`.

## Registro: nacionalidad, documento de identidad, teléfono y documentos opcionales

- BD: tabla `Nacionalidades`; columnas en `Usuarios`: `NacionalidadId INT NULL` (FK), `DocumentoIdentidad NVARCHAR(30) NULL`, `Telefono NVARCHAR(20) NULL`.
- Scripts, en este orden: `docs/sql/nacionalidades.sql`, `usuarios-nacionalidad.sql` (incluye `sp_ObtenerNacionalidades`), `usuarios-documento-identidad.sql`, `usuarios-telefono.sql`, `registro-sps.sql`. Correrlos **antes** de publicar el API.
- Nuevo `GET /api/nacionalidad` → `[{ id, nombre, pais, codigoIso }]` (solo activas).
- `POST /api/auth/register/cliente` y `/register/cuidador` aceptan, todos opcionales: `nacionalidadId`, `documentoIdentidad` y `telefono`. Solo el cuidador acepta además `documentosExtra: [{ tipoDocumento, urlArchivo }]`.
- `sp_CrearUsuarioCliente` / `sp_CrearUsuarioCuidador` reciben `@NacionalidadId`, `@DocumentoIdentidad` y `@Telefono`, y el del cuidador también `@DocumentosExtra` (JSON, leído con `OPENJSON`). Todos son opcionales (`= NULL`): una llamada vieja funciona igual.
- Documentos del cuidador:
  - Si la nacionalidad no es dominicana, el documento de `CedulaUrl` se registra en `DocumentosCuidador` como `Pasaporte`; si es dominicana, como `Cedula`.
  - Los extra quedan con `Estado = 1` (pendiente). Aprobar al cuidador los aprueba todos, igual que los demás.
  - Tipos de los extra: `Certificado Apostillado` y `Declaración Jurada` (solo para extranjeros), `Certificado médico`, `Certificado profesional`, `Curso o capacitación` y `Carta de recomendación`.
- App:
  - Contraseña con mínimo 6 caracteres, al menos un número y un carácter especial, y un campo para confirmarla.
  - Teléfono obligatorio (7 a 15 dígitos) en "Cuéntanos sobre ti".
  - Sección de documentos opcionales.
  - Aviso en el paso de cobro: un texto si el método requiere validación extra y otro si es efectivo.

## Reemplazar un documento rechazado

- Nuevo `PUT /api/cuidador/documentos/{documentoId}/reemplazar`, con cuerpo `{ cuidadorId, urlArchivo }`. Antes, la app sube el archivo con `api/upload`. Responde 400 si el documento no es de ese cuidador o no está rechazado.
- SP `sp_ReemplazarDocumentoCuidador` (`docs/sql/reemplazar-documento.sql`):
  - El documento vuelve a pendiente (`Estado 1`) con el archivo nuevo y sin la observación anterior.
  - Si es cédula, pasaporte o carta de antecedentes, también actualiza la URL en `PerfilCuidador`.
  - Si la cuenta estaba rechazada (`EstadoAprobacion 3`) y ya no le quedan documentos rechazados, vuelve a pendiente (1).
- App: en "Documentos en revisión", cada documento rechazado tiene el botón "Subir otro documento".

## Notificaciones: eventos nuevos en tiempo real

Todos se envían al grupo `user-{usuarioId}`, y si falla el envío no se interrumpe la operación:
- `CuentaActualizada` `{ cuidadorId, estado }`: el admin aprueba (2) o rechaza (3) al cuidador (`AdminService.ActualizarEstadoCuidadorAsync`).
- `PagoAprobado` `{ pagoId, trabajoId, monto }`: el monto incluye la propina. Se envía al cuidador cuando el admin aprueba el pago (`PagoAdminService.AprobarPagoAsync`).
- `TicketActualizado` `{ ticketId, respuesta, estado, asunto }`: se envía al dueño del reporte cuando el admin responde (`respuesta = true`) o cambia el estado.

No hay SPs ni tablas nuevas. Hay que publicar el API.
