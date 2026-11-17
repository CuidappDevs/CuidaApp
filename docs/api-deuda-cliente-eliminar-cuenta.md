# 6A y 6B: API, despliegue y contratos JSON

Reglas definitivas de plazo y bóveda incorporadas el 15 de noviembre de 2026; las demás reglas previas se conservan. No se ejecutó SQL ni se hicieron commits. No se editaron fuentes de `CUIDAPP/` ni `CUIDAPP_ADMINISTRATIVO/`.

## Despliegue y decisiones

1. Aplicar manualmente `docs/sql/deuda-cliente-eliminar-cuenta.sql` antes de desplegar la API. Requiere `metodo-pago-saldo.sql`, contratos fijos y sus extensiones instaladas, y `SancionesUsuario` de lote 2.
2. El script es repetible: tablas/índices condicionados y SP/triggers `CREATE OR ALTER`. No contiene datos de prueba ni se conecta a una base desde el repo.
3. `sp_FinalizarTrabajoConDeuda` envuelve el `sp_FinalizarTrabajo` instalado mediante `INSERT EXEC`, conserva su validación de PIN y horario y añade la deuda en la misma transacción. El SP instalado debe devolver exactamente `FilasAfectadas, Motivo`, como consume el servicio actual, y no usar un `INSERT EXEC` anidado. Su definición completa no está versionada aquí: validar esta compatibilidad en la base de pruebas antes de producción.
4. Total provisional: `Pagos.Monto` si ya existe, de otro modo `Trabajos.Tarifa`. No suma comisión ni una propina que el cliente decida después. `montoRecibido` omitido/null mantiene el comportamiento previo. Se permiten dos decimales, desde 0 hasta el total; límite físico `DECIMAL(10,2)`. En pagos distintos de efectivo el valor no genera deuda. La validación básica del formato monetario se aplica si se envía el campo.
5. La deuda nace al cierre con PIN (estado 7), antes de la confirmación del cliente. Una segunda finalización no duplica ni sobrescribe la deuda existente; corregirla corresponde a Finanzas. No modifica comisiones ni `sp_RecalcularPagoEfectivo`.
6. Subir comprobante deja estado 1 incluso si estaba en revisión. Objetar pone 3. Pagar/condonar cierran el registro; no se pueden abonar dos veces. Ajustar conserva estado 1/3 salvo monto cero, que pone 4. Una nota no vacía es obligatoria al condonar, ajustar o reabrir.
7. Confirmar pago acredita `-Monto` mediante movimiento `abono`, con referencia al trabajo y concepto de deuda. En este modelo saldo positivo significa que la cuidadora debe; negativo significa crédito a su favor. `Monto` se conserva al resolver como valor histórico; solo estados 1/3 suman al pendiente.
8. Triggers de inserción en `Trabajos` y `Contratos` impiden saltarse el bloqueo llamando otro SP. Devuelven 409 traducido por un filtro API. También rechazan participantes inactivos. No se reemplazan los SP de creación/propuesta ni la validación de identidad preexistente de contratos.
9. Código de seis dígitos generado criptográficamente, HMAC-SHA256 ligado a usuario y origen con la clave JWT, diez minutos, cinco intentos, un minuto entre envíos. Cada envío invalida el anterior. Si SMTP falla se revoca ese código. Orígenes app/web no intercambian códigos. La eliminación tiene límite adicional de 15 solicitudes por IP/minuto.
10. Cancelar siempre exige correo y contraseña, incluso si se suministra `usuarioId`. Recupera el estado activo anterior y respeta sanciones vigentes. No vuelve a activar disponibilidad ni horario automático.
11. El proceso horario empieza al arrancar, revisa cada cuenta por separado y vuelve a comprobar obligaciones antes de anonimizar. Nuevos bloqueos posponen la anonimización y quedan en el log; se reintenta cada hora. Cobros pendientes de cuidadora son avisos, no bloqueos.
12. Se conservan filas financieras/históricas y los PDF firmados íntegros con su hash, trasladados cifrados a la bóveda antes de anonimizar. Se retira PII estructurada de usuario, perfil, trabajos del cliente y metadatos de firma; las ubicaciones referenciadas por contratos se anonimizan en lugar de borrarse para respetar su FK. Se borran las demás ubicaciones, GPS histórico, documentos y códigos. Las filas de comprobantes de domicilio se borran; permisos y deudas pierden sus URLs/documentos personales.
13. Los PDF firmados y sus trazos se conservan por el requisito de conservar contratos firmados. Pueden contener datos personales originales. Los textos libres históricos (p. ej. reseñas/disputas/chat) también pueden contener PII: esta implementación no hace una redacción semántica. La decisión definitiva es conservar estos PDF cifrados en la bóveda; no se afirma que los documentos firmados queden sin datos personales.
14. Los archivos públicos se registran en una cola duradera antes de retirar las filas. C# borra solo rutas resueltas bajo `wwwroot/uploads`, rechaza traversal y enlaces simbólicos; los errores se reintentan. Archivos fuera de uploads no se borran. Recuperación/tokens preexistentes se eliminan por las tablas cuyo nombre contiene `PasswordReset`, `RefreshToken`, `DeviceToken` o `PushToken`, con columna `UserId`/`UsuarioId`; confirmar sus nombres reales en la base, pues sus DDL no están en el repo. También se borran códigos de contratos y enlaces de seguimiento del cliente. La renovación JWT ahora comprueba cuenta activa, correo y solicitud; los JWT ya emitidos son stateless y no se borran físicamente.
15. Los campos de fecha nuevos se expresan con la hora local RD (`UTC-4`, `HoraLocalRD.Ahora`), serializada como ISO-8601 sin sufijo de zona por tratarse de `DATETIME2`. Interpretarlos como RD, no como UTC.

## Nombres serializados y errores

Los cuerpos de DTO/objetos usan **camelCase**. Las filas genéricas del panel (`Fila`, diccionario) conservan las claves SQL en **PascalCase**. Los nombres indicados debajo son los reales en JSON. `null` se serializa; IDs y estados son números, montos números decimales, banderas booleanos. Los valores son ejemplos.

Éxito compartido de acciones, HTTP 200:

```json
{"ok":true}
```

Error de operación:

```json
{"motivo":"ESTADO_INVALIDO","message":"El estado actual no permite esta operación."}
```

404 `NO_ENCONTRADO`; 409 `ESTADO_INVALIDO`, `SOLICITUD_EXISTENTE`, `BLOQUEOS_PENDIENTES`; 429 `ESPERA_CODIGO` o `DEMASIADOS_INTENTOS`; 503 `CORREO_ERROR`; 400 `MONTO_INVALIDO`, `NOTA_REQUERIDA`, `COMPROBANTE_REQUERIDO`, `CODIGO_INVALIDO`, `ACCION_INVALIDA`. SQL inesperado: 500 `ERROR_INTERNO` con mensaje genérico en español, sin SQL.

Validación de cuerpos nuevos, HTTP 400 (las claves en `campos` dependen del campo inválido):

```json
{"motivo":"DATOS_INVALIDOS","message":"Revisa los datos obligatorios, el formato y la longitud de los campos.","campos":["Correo"]}
```

## 6A: aplicación

### PUT /api/Trabajo/finalizar

```json
{"trabajoId":101,"pin":"123456","justificacion":null,"montoRecibido":600.00}
```

200: `{"message":"Trabajo finalizado correctamente"}`. El PIN mantiene el formato que valida el SP existente; el ejemplo no cambia su longitud requerida.

400 si el monto es inválido:

```json
{"motivo":"MONTO_RECIBIDO_INVALIDO","message":"El monto recibido debe estar entre cero y el total del servicio y tener hasta dos decimales."}
```

### GET /api/DeudaCliente/cliente/{clienteId}

200:

```json
{
  "totalPendiente":400.00,
  "bloqueado":true,
  "deudas":[{
    "id":1,"trabajoId":101,"fechaServicio":"2026-11-15T00:00:00",
    "nombreCuidador":"María","montoTotal":1000.00,"montoRecibido":600.00,
    "monto":400.00,"estado":1,"comprobanteUrl":null,"notaAdmin":null,
    "fechaCreacion":"2026-11-15T14:00:00",
    "fechaLimite":"2026-11-16T14:00:00","vencida":false
  }]
}
```

Pendientes y revisión primero; después historial, cada grupo por fecha descendente. Sin deudas: `{"totalPendiente":0,"bloqueado":false,"deudas":[]}`.

### GET /api/DeudaCliente/trabajo/{trabajoId}

200: exactamente un objeto con las trece propiedades del elemento de `deudas` anterior. Si no existe: **204 sin cuerpo**.

### POST /api/DeudaCliente/{deudaId}/comprobante

```json
{"clienteId":2,"comprobanteUrl":"/uploads/usuarios/2/comprobante.jpg","nota":"Transferencia realizada"}
```

`nota` opcional/null. 200 `{"ok":true}`. Valida que la deuda pertenezca al cliente. La URL procede de `api/Upload`; no sube el archivo aquí.

### POST /api/DeudaCliente/{deudaId}/objetar

```json
{"clienteId":2,"motivo":"Entregué el total al finalizar"}
```

200 `{"ok":true}`.

### POST /api/Trabajo y POST /api/contratos

Conservan sus cuerpos y éxitos actuales. Nuevo error HTTP 409:

```json
{"motivo":"DEUDA_PENDIENTE","message":"Debes resolver tu deuda pendiente antes de solicitar un servicio o contrato.","monto":400.00}
```

Participante inactivo: 403 `{"motivo":"CUENTA_INACTIVA","message":"La cuenta no está activa."}`.

No se duplica `GET /api/PagosApp/instrucciones-transferencia`; se sigue usando el endpoint existente.

## 6A: panel

Todos exigen JWT admin y llevan auditoría de escrituras. Las constantes actuales son `PoliticasAdmin.Finanzas` y `PoliticasAdmin.Operaciones`; sus valores de policy siguen siendo `AdminFinanzas` y `AdminOperaciones`.

### GET /api/AdminDeudas?estado=1&clienteId=2

Filtros opcionales; policy `Admin`. 200 array de filas (lista vacía `[]`):

```json
[{
  "Id":1,"TrabajoId":101,"FechaServicio":"2026-11-15T00:00:00","NombreCuidador":"María",
  "MontoTotal":1000.00,"MontoRecibido":600.00,"Monto":400.00,"Estado":1,
  "ComprobanteUrl":null,"NotaAdmin":null,"FechaCreacion":"2026-11-15T14:00:00",
  "FechaLimite":"2026-11-16T14:00:00","Vencida":false,
  "ClienteId":2,"NombreCliente":"Pedro","CorreoCliente":"pedro@example.com","CuidadorId":3,
  "NotaCliente":null,"FechaResolucion":null,"AdminId":null
}]
```

### Escrituras (policy AdminFinanzas)

| Endpoint POST | Cuerpo JSON | Éxito HTTP 200 |
|---|---|---|
| `/api/AdminDeudas/{id}/confirmar-pago` | `{"nota":"Transferencia verificada"}` (`nota` opcional/null) | `{"ok":true}` |
| `/api/AdminDeudas/{id}/condonar` | `{"nota":"Acuerdo de condonación"}` | `{"ok":true}` |
| `/api/AdminDeudas/{id}/ajustar` | `{"monto":250.00,"nota":"Corrección del saldo"}` | `{"ok":true}` |
| `/api/AdminDeudas/{id}/reabrir` | `{"nota":"Objeción revisada"}` | `{"ok":true}` |

## 6B: aplicación y página pública

### GET /api/EliminarCuenta/revisar/{usuarioId}

200:

```json
{
  "puedeEliminar":false,
  "bloqueos":[{"codigo":"DEUDA_CLIENTE","detalle":"Tienes una deuda pendiente o en revisión.","referenciaId":1,"bloquea":true}],
  "solicitud":null
}
```

Con solicitud programada, `solicitud` es `{"fechaEjecucion":"2026-12-15T14:00:00"}`. Los avisos también se incluyen en `bloqueos`, con `bloquea:false`; no alteran `puedeEliminar`. Códigos: `DISPUTA_ABIERTA`, `SERVICIO_ACTIVO`, `CONTRATO_ACTIVO`, `DEUDA_CLIENTE`, `DEUDA_CUIDADOR`, `PAGO_POR_COBRAR` (aviso). `puedeEliminar` representa ausencia de bloqueos, no ausencia de una solicitud ya programada.

### POST /api/EliminarCuenta/codigo

```json
{"usuarioId":2}
```

200: `{"message":"Código enviado al correo de la cuenta.","expiraEnMinutos":10}`. Nunca devuelve el código/hash.

### POST /api/EliminarCuenta/confirmar

```json
{"usuarioId":2,"codigo":"123456","motivo":"Ya no utilizo el servicio"}
```

`motivo` opcional/null. 200: `{"fechaEjecucion":"2026-12-15T14:00:00"}`. Desactiva la cuenta en la misma transacción que crea la solicitud.

Código/confirmación/web con bloqueos devuelven HTTP 409:

```json
{"motivo":"BLOQUEOS_PENDIENTES","message":"Debes resolver los asuntos pendientes antes de eliminar la cuenta.","bloqueos":[{"codigo":"DEUDA_CLIENTE","detalle":"Tienes una deuda pendiente o en revisión.","referenciaId":1,"bloquea":true}]}
```

### POST /api/EliminarCuenta/cancelar

```json
{"correo":"pedro@example.com","contrasena":"contraseña","usuarioId":2}
```

`usuarioId` puede omitirse/null. Si se proporciona debe coincidir con las credenciales. 200 `{"ok":true}`. No admite cancelación solo por ID.

Credenciales inválidas, HTTP 401:

```json
{"motivo":"CREDENCIALES_INVALIDAS","message":"Correo o contraseña incorrectos."}
```

### POST /api/Auth/login

Conserva cuerpo `{"email":"pedro@example.com","password":"contraseña"}`. Tras verificar contraseña, una solicitud pendiente devuelve **403**, sin token:

```json
{"motivo":"CUENTA_EN_ELIMINACION","fechaEjecucion":"2026-12-15T14:00:00","usuarioId":2}
```

Las cuentas anonimizadas ya no coinciden con su correo original y fallan como credenciales inválidas. La renovación de tokens también rechaza cuentas en eliminación/inactivas.

### GET /eliminar-cuenta

200 `text/html; charset=utf-8`. Página pública española y responsive, sin dependencias externas, con formulario de correo, contraseña, motivo y código. También permite cancelar usando credenciales. Está en `CUIDAPP_API/wwwroot/eliminar-cuenta/index.html`.

### POST /api/EliminarCuenta/web

```json
{"correo":"pedro@example.com","contrasena":"contraseña","motivo":"Ya no utilizo el servicio"}
```

`motivo` opcional, se remite de nuevo al confirmar; no se guarda antes de confirmar. 200: `{"message":"Código enviado al correo de la cuenta.","expiraEnMinutos":10}`. Errores 401/409 iguales a los anteriores.

### POST /api/EliminarCuenta/web/confirmar

```json
{"correo":"pedro@example.com","contrasena":"contraseña","codigo":"123456","motivo":"Ya no utilizo el servicio"}
```

200: `{"fechaEjecucion":"2026-12-15T14:00:00"}`. Revalida credenciales, bloqueos y código; guarda origen `web`.

## 6B: panel

### GET /api/AdminEliminaciones?estado=1
### GET /api/AdminEliminaciones/usuario/{usuarioId}

Policy `Admin`. Ambos retornan 200 con array (incluye historial), sin filas `[]`. El filtro de estado es opcional en la lista:

```json
[{
  "Id":1,"UsuarioId":2,"Rol":2,"NombreCompleto":"Pedro","Email":"pedro@example.com",
  "Motivo":"Ya no utilizo el servicio","Estado":1,"FechaSolicitud":"2026-11-15T14:00:00",
  "FechaEjecucion":"2026-12-15T14:00:00","FechaCancelacion":null,"CanceladaPor":null,
  "Origen":"app","NotaAdmin":null
}]
```

### POST /api/AdminEliminaciones/{id}/cancelar

Policy `AdminOperaciones`, auditoría. Cuerpo `{"nota":"Solicitud de recuperación revisada"}`; nota obligatoria. 200 `{"ok":true}`. Guarda `canceladaPor='admin'` y nota en la solicitud; no levanta sanciones.

## SignalR

Evento existente `CuentaActualizada`:

```json
{"tipo":"deuda_cliente"}
```

Al crear deuda desde finalizar incluye además `trabajoId`. Confirmar eliminación envía `{"tipo":"cuenta_en_eliminacion","fechaEjecucion":"2026-12-15T14:00:00"}` y anuncia `DisponibilidadCambio` con `{"cuidadorId":2,"disponible":false}`; cancelar envía `{"tipo":"eliminacion_cancelada"}`. Cada destinatario recibe sus eventos en su grupo existente.

## Archivos de esta implementación

Nuevos:

- `docs/sql/deuda-cliente-eliminar-cuenta.sql`
- `docs/api-deuda-cliente-eliminar-cuenta.md`
- `CUIDAPP_API/Controllers/DeudaClienteController.cs`
- `CUIDAPP_API/Controllers/AdminDeudasController.cs`
- `CUIDAPP_API/Controllers/EliminarCuentaController.cs`
- `CUIDAPP_API/Controllers/AdminEliminacionesController.cs`
- `CUIDAPP_API/Controllers/CuentaRespuesta.cs`
- `CUIDAPP_API/DTOs/Cuenta/CuentaDtos.cs`
- `CUIDAPP_API/Interfaces/Cuenta/IEliminarCuentaService.cs`
- `CUIDAPP_API/Services/Cuenta/EliminarCuentaService.cs`
- `CUIDAPP_API/Services/Cuenta/EliminacionCuentasService.cs`
- `CUIDAPP_API/Seguridad/CuentaSqlExceptionFilter.cs`
- `CUIDAPP_API/Seguridad/CuentaValidacionAttribute.cs`
- `CUIDAPP_API/wwwroot/eliminar-cuenta/index.html`
- `CUIDAPP_API.Tests/DeudaEliminacionTests.cs`

Modificados, conservando los cambios previos del usuario:

- `CUIDAPP_API/DTOs/Trabajo/FinalizarTrabajoDto.cs`
- `CUIDAPP_API/Services/Trabajo/TrabajoService.cs`
- `CUIDAPP_API/Controllers/TrabajoController.cs`
- `CUIDAPP_API/DTOs/Auth/LoginResult.cs`
- `CUIDAPP_API/Services/Auth/AuthService.cs`
- `CUIDAPP_API/Controllers/AuthController.cs`
- `CUIDAPP_API/Program.cs`

## Validación del trabajo previo

- `dotnet build CUIDAPP_API/CUIDAPP_API.csproj --no-restore`: correcto.
- `dotnet test CUIDAPP_API.Tests/CUIDAPP_API.Tests.csproj --no-restore`: 76 pruebas correctas, cero fallos.
- `dotnet build CUIDAPP_API.slnx`: no puede restaurar por acceso denegado al `NuGet.Config` del perfil de Windows.
- `dotnet build CUIDAPP_API.slnx --no-restore`: API, pruebas y panel compilan; falla MAUI Android con `java.exe` código 2. No se modificó MAUI para resolver ese fallo.
- Persisten avisos de paquetes vulnerables (`Microsoft.OpenApi`, `System.Security.Cryptography.Pkcs`) y un aviso nullable previo de AuthService. No se cambian dependencias en este alcance.
- SQL no ejecutado, SMTP y operaciones de disco reales no invocadas; las pruebas de controladores usan dobles. Es necesaria una prueba de integración en SQL Server tras aplicar manualmente el script, incluida concurrencia/doble confirmación y compatibilidad del SP de finalización instalado.
- Análisis estático del script con `Microsoft.SqlServer.TransactSql.ScriptDom.TSql160Parser`: cero errores de sintaxis, sin conectar a SQL Server.
- JavaScript de la página pública analizado con `node`/`vm.Script`: sintaxis correcta. No se envió ningún formulario real.


## Decisiones definitivas A/B: plazo y secure_compliance_vault

### Despliegue manual y configuración

Aplicar **manualmente**, después de las migraciones previas de deuda y contratos/anexos, `docs/sql/deuda-plazo-boveda.sql`. Es idempotente y no se ejecuta al iniciar la API. Reemplaza los SP de creación/lectura/acciones de deuda, lectura de documento y anonimización conservando sus validaciones previas, y añade los SP de bóveda y avisos. No ejecutar después una versión anterior de esos SP, pues quitaría estas protecciones.

- `DeudasCliente.FechaLimite DATETIME2 NULL`: existentes = FechaCreacion + 24 horas; nuevas = @Ahora + 24 horas. Banderas BIT NOT NULL con DEFAULT 0: `AvisoRecordatorioEnviado`, `AvisoVencidaEnviado`.
- El bloqueo es inmediato y sigue aplicando a estados 1/3. El plazo no concede permiso para solicitar servicios, contratar ni eliminar la cuenta.
- `vencida` / `Vencida` es booleano: estado 1/3 y ahora estrictamente mayor que FechaLimite. Una deuda cerrada nunca está vencida. Las fechas DATETIME2 son hora RD (UTC-4), ISO-8601 sin sufijo de zona.
- El pago de la deuda entra exclusivamente por CuidaApp: transferencia y comprobante, verificación por Finanzas y crédito íntegro a la Care Partner mediante `MovimientosSaldoCuidador`. Confirmar sin comprobante devuelve 400 `COMPROBANTE_REQUERIDO`. No hay acción ni parámetro para confirmar efectivo entregado a la cuidadora. Los estados/acciones de condonación y ajuste siguen siendo decisiones financieras independientes.
- `ContratoDocumentos` conserva `Hash` SHA-256 del PDF original y recibe `EnBoveda BIT NOT NULL DEFAULT 0`, `RutaBoveda NVARCHAR(400) NULL`, `FechaBoveda DATETIME2 NULL`, `TamanoBytes BIGINT NULL` (tamaño del PDF original).
- `BovedaAccesos`: Id BIGINT identity, DocumentoId, AdminId, Fecha DATETIME2 RD, Motivo NVARCHAR(400), Ip NVARCHAR(64). Cada descarga autorizada exige registrar este acceso antes de devolver el PDF; también lleva `[AuditarAdmin]`.
- Configuración: `BovedaCumplimiento:Ruta` (default `boveda` relativo al ContentRoot, normalmente `CUIDAPP_API/boveda/`) y `BovedaCumplimiento:Clave` (32 bytes aleatorios en base64). La carpeta está en .gitignore. Development ya incluye una clave generada aleatoriamente; no se reproduce aquí. En el servidor configurar en appsettings o variables `BovedaCumplimiento__Ruta` y `BovedaCumplimiento__Clave`.
- Conservar y respaldar la clave junto con la estrategia de recuperación de la bóveda. Cambiarla sin recifrar los documentos impide abrir los existentes. Restringir permisos del directorio a la identidad de la API y mantenerlo fuera de wwwroot. No se guarda ninguna copia nueva en claro en la bóveda.

### Traslado y reintentos

El proceso horario comprueba obligaciones antes del traslado y SQL vuelve a verificarlas al anonimizar. Antes de pasar la solicitud a estado 3, todos los PDF de contratos donde participa el usuario (cliente o Care Partner, incluidos anexos de ContratoDocumentos) se cifran con AES-256-GCM. Cada archivo usa nonce aleatorio de 12 bytes y tag de 16 bytes. Formato binario v1: `nonce[12] || tag[16] || ciphertext`. El hash original no cambia.

Se escribe un temporal cifrado, se descifra y compara SHA-256, se publica sin sobrescribir, se vuelve a verificar el archivo final, se registran ruta/fecha/tamaño y solo entonces se borra el original privado. La ruta final estable por DocumentoId permite reanudar si falla el registro SQL o el borrado. Al reintentar un documento ya marcado se verifica otra vez y se elimina cualquier original remanente. Se rechazan traversal y enlaces simbólicos.

Si falta la clave, tiene formato/tamaño inválido, falla el hash, el disco o SQL, la cuenta con contratos no se anonimiza: se registra error y se reintenta en la siguiente pasada. Una cuenta sin PDF no necesita clave. El SP de anonimización rechaza con `BOVEDA_PENDIENTE` si queda un documento sin archivar. Los documentos no contienen PII en las listas de bóveda, pero el PDF descifrado conserva su contenido firmado original; los textos libres históricos mantienen la salvedad ya documentada.

### JSON exacto: deuda

GET `/api/DeudaCliente/cliente/2`, ejemplo 200:

```json
{"totalPendiente":400.00,"bloqueado":true,"deudas":[{"id":1,"trabajoId":101,"fechaServicio":"2026-11-15T00:00:00","nombreCuidador":"María","montoTotal":1000.00,"montoRecibido":600.00,"monto":400.00,"estado":1,"comprobanteUrl":null,"notaAdmin":null,"fechaCreacion":"2026-11-15T14:00:00","fechaLimite":"2026-11-16T14:00:00","vencida":false}]}
```

GET `/api/DeudaCliente/trabajo/101` devuelve exactamente el objeto de 13 propiedades del array anterior; sin deuda mantiene 204 sin cuerpo. GET cliente sin deuda mantiene `{"totalPendiente":0,"bloqueado":false,"deudas":[]}`.

GET `/api/AdminDeudas`, ejemplo 200 (claves SQL PascalCase):

```json
[{"Id":1,"TrabajoId":101,"FechaServicio":"2026-11-15T00:00:00","NombreCuidador":"María","MontoTotal":1000.00,"MontoRecibido":600.00,"Monto":400.00,"Estado":1,"ComprobanteUrl":null,"NotaAdmin":null,"FechaCreacion":"2026-11-15T14:00:00","FechaLimite":"2026-11-16T14:00:00","Vencida":false,"ClienteId":2,"NombreCliente":"Pedro","CorreoCliente":"pedro@example.com","CuidadorId":3,"NotaCliente":null,"FechaResolucion":null,"AdminId":null}]
```

Confirmar pago sin comprobante, HTTP 400:

```json
{"motivo":"COMPROBANTE_REQUERIDO","message":"Debes indicar la URL del comprobante."}
```

### JSON exacto: SignalR

`DeudasClienteAvisosService` ejecuta al arrancar y cada 15 minutos. Solo deudas abiertas: en la ventana [FechaLimite - 2 horas, FechaLimite] envía una vez el recordatorio; después del límite envía una vez vencida. Si la API estuvo apagada durante la ventana no envía un recordatorio atrasado, solo vencida. Evento `CuentaActualizada` al grupo del ClienteId:

```json
{"tipo":"deuda_recordatorio","trabajoId":101,"monto":400.00,"fechaLimite":"2026-11-16T14:00:00"}
```

```json
{"tipo":"deuda_vencida","trabajoId":101,"monto":400.00}
```

El SP selecciona una deuda con UPDLOCK/READPAST dentro de una transacción, mantiene el bloqueo durante el envío y marca la bandera antes del commit. Evita duplicados entre procesos concurrentes y que una deuda cierre entre la lectura y el envío. Si SignalR falla no se marca y se reintenta. SignalR no ofrece una transacción distribuida: una caída entre el envío y el commit puede repetir un aviso; tampoco garantiza entrega a clientes desconectados. Las banderas registran envío al hub, no lectura del cliente.

### JSON exacto: bóveda

Todos los endpoints siguientes exigen JWT, rol administrador y `NivelAdmin` 1 (Superadmin) o 6 (Legal y Dirección), mediante `PoliticasAdmin.Boveda`. Otros niveles: 403; sin autenticación: 401. Se verifica nuevamente el nivel activo en SQL al registrar el acceso.

GET `/api/admin/boveda`, 200 (sin datos personales, sin rutas, sin claves; lista vacía `[]`):

```json
[{"DocumentoId":12,"ContratoId":4,"FechaFirma":"2026-10-01T10:30:00","FechaBoveda":"2026-11-15T14:00:00","UsuariosIdEliminados":[2,3],"TamanoBytes":12345,"Sha256":"0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF"}]
```

`UsuariosIdEliminados` siempre es un array numérico con los participantes cuya solicitud está en estado 3 (puede estar vacío durante un traslado previo a completar la anonimización). FechaFirma es `ContratoDocumentos.Fecha`, fecha de generación del PDF firmado.

POST `/api/admin/boveda/12/abrir`, cuerpo obligatorio:

```json
{"motivo":"Revisión legal del contrato firmado"}
```

Motivo recortado: mínimo 10, máximo 400 caracteres. Éxito: **200 binario PDF**, `Content-Type: application/pdf`, `Content-Disposition: attachment`, nombre `Contrato-4-documento-12.pdf`; no es JSON.

409 si falla autenticación GCM o SHA-256:

```json
{"motivo":"INTEGRIDAD_FALLIDA","message":"No se pudo verificar la integridad del documento."}
```

404 si no existe o no está en bóveda:

```json
{"motivo":"NO_ENCONTRADO","message":"Documento no encontrado en la bóveda."}
```

503 por archivo/configuración no disponible:

```json
{"motivo":"BOVEDA_NO_DISPONIBLE","message":"La bóveda no está disponible temporalmente."}
```

400 por motivo que solo alcanza la longitud con espacios (el resto de errores de validación mantiene DATOS_INVALIDOS/campos):

```json
{"motivo":"MOTIVO_INVALIDO","message":"Indica un motivo de al menos 10 caracteres."}
```

GET app `/api/contratos/{id}/enlace-pdf`, GET de un enlace temporal ya emitido `/api/contratos/documento/{documentoId}`, POST panel `/api/admin/contratos/documentos/{documentoId}/descargar` y verificar: si el documento está en bóveda devuelven **410** después de la validación de identidad/enlace existente:

```json
{"motivo":"EN_BOVEDA","message":"Este contrato está en la bóveda de cumplimiento."}
```

### Archivos de esta ampliación

Creados:
- docs/sql/deuda-plazo-boveda.sql
- CUIDAPP_API/Services/Cuenta/BovedaCifrado.cs
- CUIDAPP_API/Services/Cuenta/BovedaCumplimientoService.cs
- CUIDAPP_API/Services/Cuenta/DeudasClienteAvisosService.cs
- CUIDAPP_API/Controllers/AdminBovedaController.cs
- CUIDAPP_API.Tests/BovedaCifradoTests.cs
- CUIDAPP_API.Tests/BovedaTrasladoTests.cs

Modificados (preservando cambios previos):
- .gitignore
- CUIDAPP_API/appsettings.Development.json
- CUIDAPP_API/Program.cs
- CUIDAPP_API/Seguridad/PoliticasAdmin.cs
- CUIDAPP_API/DTOs/Cuenta/CuentaDtos.cs
- CUIDAPP_API/Services/Cuenta/EliminacionCuentasService.cs
- CUIDAPP_API/Services/Contrato/ContratoService.cs
- CUIDAPP_API/Controllers/DeudaClienteController.cs
- CUIDAPP_API/Controllers/ContratoController.cs
- CUIDAPP_API/Controllers/AdminContratosController.cs
- CUIDAPP_API.Tests/DeudaEliminacionTests.cs
- docs/api-deuda-cliente-eliminar-cuenta.md

### Validación de esta ampliación

- `dotnet build CUIDAPP_API/CUIDAPP_API.csproj` y `dotnet test CUIDAPP_API.Tests`: restauración impedida por acceso denegado al NuGet.Config del perfil.
- `dotnet build CUIDAPP_API/CUIDAPP_API.csproj --no-restore`: correcto.
- `dotnet test CUIDAPP_API.Tests --no-restore`: 85 pruebas correctas, cero fallos. Incluye ida/vuelta AES, nonce diferente, tag alterado, clave incorrecta, SHA incorrecto, traslado, ausencia de clave, recuperación después de fallar registro y traversal.
- Persisten los avisos previos de paquetes y nullable de AuthService. No se ejecut? SQL ni se conect? a una base; no se inici? el host API. La integración con SQL Server/SignalR debe comprobarse tras aplicar el script manualmente.
- No se tocaron CUIDAPP/ ni CUIDAPP_ADMINISTRATIVO/ y no se hicieron commits.

## Verificación de 6A/6B en esta revisión (15 de noviembre de 2026)

La implementación de 6A/6B y la ampliación de plazo/bóveda ya estaban presentes en el árbol de trabajo al iniciar esta revisión. Se conservaron esos cambios. En esta revisión se modificaron únicamente `CUIDAPP_API/DTOs/Cuenta/CuentaDtos.cs`, `CUIDAPP_API.Tests/DeudaEliminacionTests.cs` y este informe.

Corrección: `POST /api/AdminDeudas/{id}/ajustar` exige `monto` explícito y no nulo. `{}` o `{"monto":null}` devuelven 400 `{"motivo":"DATOS_INVALIDOS","message":"Revisa los datos obligatorios, el formato y la longitud de los campos.","campos":["Monto"]}`. `{"monto":0,"nota":"Condonación revisada"}` sigue siendo válido y cierra con estado 4. Se agregaron cuatro casos de regresión para omisión, null, cero y un importe positivo.

Validación realizada en esta revisión:
- `dotnet build CUIDAPP_API.slnx`: falla durante restauración por acceso denegado a `C:\Users\MothherBrain\AppData\Roaming\NuGet\NuGet.Config`.
- `dotnet build CUIDAPP_API/CUIDAPP_API.csproj --no-restore`: correcto, cero errores; persisten avisos de dependencias y nullable.
- `dotnet test CUIDAPP_API.Tests/CUIDAPP_API.Tests.csproj --no-restore`: 89 pruebas aprobadas, cero fallos.
- No se ejecutaron scripts SQL, no se inició la API, no se enviaron correos ni se hicieron commits. No se editaron fuentes de las UI.

La versión actual incluye la ampliación previa de bóveda: su proceso horario depende también de `docs/sql/deuda-plazo-boveda.sql` y su configuración, descritas arriba. El script base por sí solo no instala los procedimientos de bóveda que utiliza el proceso actual. La integración SQL/SMTP/SignalR y la compatibilidad del SP instalado `sp_FinalizarTrabajo` quedan pendientes de validación en un entorno con la migración aplicada manualmente.
