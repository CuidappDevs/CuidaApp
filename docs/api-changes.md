# Cambios en `CUIDAPP_API`

Todo lo listado aquí requiere **republicar la API** (ver [deployment.md](./deployment.md)) para que tome efecto en producción.

## Centro de mando del panel ("Ojo de Dios")

SQL: `docs/sql/centro-mando.sql` (tablas `UbicacionesHistorial` y `AvisosDirectos`, columnas `PerfilCuidador.UltimaUbicacion` y `Bateria`, nueva versión de `sp_ActualizarUbicacionCuidador` y `sp_AdminMapa`). Correr **después** de `panel-admin.sql` y `horario-cuidador.sql`.

| Método | Ruta | Nivel | Qué hace |
|---|---|---|---|
| GET | `/api/admin/mapa/ficha/{usuarioId}` | Admin | Persona, servicio en curso/próximo y avisos recientes |
| GET | `/api/admin/mapa/estelas?minutos=` | Admin | Puntos recientes para dibujar estelas |
| GET | `/api/admin/mapa/recorrido?trabajoId=` o `?cuidadorId&desde&hasta` | Admin | Recorrido para reproducir |
| GET | `/api/admin/mapa/demanda?dias=` | Admin | Mapa de calor de servicios pedidos |
| GET | `/api/admin/mapa/alertas` | Admin | Servicios que no empezaron a tiempo y Care Partners en servicio sin señal |
| POST | `/api/admin/mapa/notificar` `{ usuarioIds, titulo, mensaje }` | Operaciones | Notificación directa (evento `AvisoDirecto` al grupo del usuario) |
| POST | `/api/admin/mapa/checkin` `{ usuarioId, minutos, mensaje }` | Operaciones | "¿Estás bien?"; sin respuesta a tiempo → SOS |
| POST | `/api/admin/mapa/ocultar/{id}` | Operaciones | Oculta al Care Partner y apaga su horario automático |
| POST | `/api/usuario/{usuarioId}/checkin/{avisoId}` `{ estaBien }` | (app) | Respuesta al "¿Estás bien?"; "ayuda" → SOS |

- `PUT /api/cuidador/ubicacion` acepta `bateria` (opcional) y guarda historial mientras el cuidador está visible o en servicio (30 días).
- El servicio de fondo (`HorarioVisibilidadService`) también convierte cada minuto los "¿Estás bien?" vencidos en SOS y emite `CheckinRespondido`.

## Horario automático de visibilidad del Care Partner

SQL: `docs/sql/horario-cuidador.sql` (columna `PerfilCuidador.HorarioAutomatico`, tabla `HorarioCuidador`, SPs `sp_ObtenerHorarioCuidador`, `sp_GuardarHorarioCuidador`, `sp_AplicarHorariosCuidadores` y nueva versión de `sp_ActualizarDisponibilidadCuidador`).

| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/api/cuidador/{id}/horario` | `{ activo, franjas: [{ diaSemana, horaInicio, horaFin }] }` (0 domingo … 6 sábado, "HH:mm") |
| PUT | `/api/cuidador/{id}/horario` | Guarda modo y franjas; si se activa, se aplica al momento. Errores: `SIN_FRANJAS`, `FRANJAS_SOLAPADAS`, `FRANJA_INVALIDA` |

- `Services/Cuidador/HorarioVisibilidadService.cs` (BackgroundService): cada minuto pone visible/oculto a quienes tienen el modo activo y emite `DisponibilidadCambio` (con `PorHorario = true`).
- Con el modo activo, `PUT /api/cuidador/disponibilidad` ya no cambia nada para ese cuidador (el interruptor manual queda bloqueado).

## Seguridad del panel: JWT obligatorio en endpoints de administración

- `Seguridad/PoliticasAdmin.cs`: la API ahora **valida el JWT** (`UseAuthentication` + JwtBearer) y define 4 políticas:
  `Admin` (cualquier administrador), `AdminOperaciones` (niveles 1 y 2), `AdminFinanzas` (niveles 1 y 3) y `SuperAdmin` (nivel 1).
- Solo exigen token: `AdminController`, `PagoAdminController`, el nuevo `AdminOperacionesController`, `GET /api/ticket`,
  `PUT /api/ticket/{id}/estado`, `GET /api/sos/pendientes|historial|{id}` y `PUT /api/sos/{id}/atender|descartar`.
  **La app móvil no envía token y no usa ninguno de esos endpoints**, así que sigue funcionando igual.
- Niveles de administrador (`Usuarios.NivelAdmin`): 1 Superadmin, 2 Operaciones/soporte, 3 Finanzas. El login de un
  administrador agrega el claim `nivel_admin` y el token dura 8 h (el de la app sigue en 2 h).
- `Seguridad/AuditarAdminAttribute.cs`: toda acción del panel que cambia datos (POST/PUT/DELETE) queda en `AuditoriaAdmin`
  (quién, qué, registro afectado, motivo y si salió bien).
- **El panel administrativo y la API se tienen que publicar juntos**: un panel viejo contra la API nueva recibe 401.

## Operación diaria del panel (`AdminOperacionesController`, ruta `api/admin`)

SQL: `docs/sql/panel-admin.sql` (tablas `AuditoriaAdmin`, `AvisosAdmin`, columna `Usuarios.NivelAdmin` y los SP `sp_Admin*`).

| Método | Ruta | Nivel | Qué hace |
|---|---|---|---|
| GET | `/api/admin/dashboard` | Admin | Indicadores, serie de 14 días, actividad reciente y cola de verificación |
| GET | `/api/admin/servicios?estado&desde&hasta&buscar&pagina&tamano` | Admin | Lista paginada de servicios |
| GET | `/api/admin/servicios/{id}` | Admin | Detalle: tareas, bitácora, pago, calificaciones, chat, SOS y reportes |
| PUT | `/api/admin/servicios/{id}/cancelar` `{ motivo }` | Operaciones | Cancela (estados 1, 2, 3, 7) y avisa a los dos por SignalR |
| PUT | `/api/admin/servicios/{id}/completar` `{ motivo }` | Operaciones | Da por completado (3, 7) y crea el pago pendiente |
| GET | `/api/admin/verificacion` | Admin | Care Partners por verificar con conteo de documentos |
| GET | `/api/admin/mapa` | Admin | Care Partners visibles, servicios en curso y SOS con coordenadas |
| GET | `/api/admin/calificaciones?max=` | Admin | Reseñas y promedio por Care Partner |
| GET | `/api/admin/finanzas?desde&hasta` | Finanzas | Totales, por Care Partner y por día |
| GET | `/api/admin/auditoria?buscar&pagina&tamano` | SuperAdmin | Bitácora del panel |
| PUT | `/api/admin/administradores/{id}/nivel` `{ nivel }` | SuperAdmin | Cambia el nivel (no deja quitar el último superadmin) |
| GET/POST | `/api/admin/avisos` `{ destino, idioma, titulo, mensaje }` | POST: Operaciones | Aviso masivo: evento SignalR global `AvisoGeneral` (destino 0 todos, 2 clientes, 3 cuidadores; idioma null todos, `es`, `en`, `ht`) |
| POST | `/api/admin/avisos/{id}/reenviar` | Operaciones | Reenvía un aviso del historial (queda como envío nuevo) |
| DELETE | `/api/admin/avisos/{id}` | Operaciones | Lo quita del historial (no de los teléfonos) |
| GET/POST | `/api/admin/catalogos/tipos-servicio` | POST: Operaciones | Tipos de servicio |
| GET/POST | `/api/admin/catalogos/motivos-cancelacion` | POST: Operaciones | Motivos de cancelación |
| GET/POST | `/api/admin/catalogos/nacionalidades` | POST: Operaciones | Nacionalidades |
| GET/POST | `/api/admin/catalogos/estatus-migratorio` | POST: Operaciones | Estatus migratorio + documentos requeridos |
| POST/DELETE | `/api/admin/catalogos/requisitos[/{id}]` | Operaciones | Agrega o quita un documento requerido |
| GET | `/api/sos/historial?top=` | Admin | Alertas SOS atendidas o descartadas |

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

## Recibos por correo al completar un servicio

- Cuando el cliente **confirma** que el servicio terminó (`PUT api/trabajo/confirmar-finalizacion` con `confirmado = true`), el servidor envía en segundo plano dos correos (`Services/Recibo/ReciboService.cs`):
  - **Cliente:** total pagado (servicio + propina), tareas hechas y no hechas, detalles (horario programado, inicio y fin reales con PIN, duración, dirección, cuidador), nota de finalización, invitación a calificar y ayuda.
  - **Cuidador:** lo que ganó (pago por el servicio + propina), aviso de que el pago queda pendiente de aprobación, tareas completadas y detalles del servicio.
- No se envían si el servicio termina forzado o sin cobro, ni si se cancela.
- SP `sp_ObtenerDatosReciboTrabajo` (`docs/sql/recibo-servicio.sql`), de solo lectura.
- Los íconos son PNG servidos por el API en `wwwroot/email/` (Gmail y Outlook no muestran SVG). Su URL base sale de `UrlPublica` en `appsettings.json`.
- Si el envío falla, no afecta la confirmación: solo queda en el log.

## Correo de bienvenida al registrarse

- Al crear la cuenta (`POST api/auth/register/cliente` y `/register/cuidador`), el servidor envía en segundo plano un correo de bienvenida (`Services/Email/BienvenidaService.cs`), con el mismo diseño de los recibos:
  - **Cliente:** cuenta lista, "así de fácil funciona" en 4 pasos, por qué es seguro (verificación y PIN), y tarjetas de direcciones y soporte.
  - **Cuidador:** registro recibido, una línea de progreso (cuenta creada ✔ → documentos en revisión, *en curso* → cuenta aprobada → empieza a trabajar) y cómo seguir el estado de cada documento en la app y volver a subir los rechazados.
- El diseño común está en `Services/Email/PlantillaCorreo.cs`, del que heredan `ReciboService` y `BienvenidaService`.
- Los íconos nuevos están en `wwwroot/email/`: `hero_hogar`, `hero_revision`, `pendiente` y `paso`.
- No hay tablas ni SPs nuevos. Si el envío falla, no afecta el registro.

## Bienvenida animada (una vez por cuenta)

- BD: columna `Usuarios.BienvenidaVista BIT NOT NULL DEFAULT 0`, más los SPs `sp_ObtenerBienvenidaVista` y `sp_MarcarBienvenidaVista` (`docs/sql/usuarios-bienvenida.sql`). Al correr el script, **los usuarios existentes quedan marcados como vistos**: solo la ven las cuentas nuevas.
- Endpoints nuevos: `GET api/usuario/{id}/bienvenida` → `{ bienvenidaVista }` y `PUT api/usuario/{id}/bienvenida` (la marca como vista).
- App:
  - Al entrar al panel (el cliente siempre; el cuidador solo cuando ya está aprobado), `Services/BienvenidaApp.cs` consulta al servidor y, si no la vio, abre `Views/Comun/BienvenidaPage`. Al terminar o saltar se marca en el servidor, así que no vuelve a salir en ningún teléfono. Si no hay conexión, no se muestra y se intenta la próxima vez.
  - Animaciones Lottie en `CUIDAPP/Resources/Raw/bienvenida/*.json`, reproducidas con `SkiaSharp.Extended.UI.Maui` (`SKLottieView`). Las animaciones de marca se generan con `docs/lottie_bienvenida_gen.py`.

## Visibilidad solo con la cuenta aprobada

- `sp_ActualizarDisponibilidadCuidador` (`docs/sql/disponibilidad-solo-aprobados.sql`) solo deja ponerse **Disponible** a un cuidador con `EstadoAprobacion = 2`. Siempre puede ponerse no disponible. El script además apaga la visibilidad de los cuidadores que hoy están disponibles sin estar aprobados.
- La búsqueda de cuidadores (mapa, lista por servicio y servicios cercanos) sigue exigiendo la cuenta aprobada.
- App: el cuidador con todos los documentos aprobados pero la cuenta pendiente entra a su panel con el interruptor **apagado**. Si intenta activarlo, ve "Tu perfil está en validación" y la notificación fija no ofrece el botón "Hacerme visible". Cuando la administración aprueba la cuenta (evento `CuentaActualizada`), se destraba.
