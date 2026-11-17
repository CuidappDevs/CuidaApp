# Continuar con Google

## Configuración y despliegue

Google.Apis.Auth 1.78.0 (estable). Ambos appsettings contienen:

```json
{"Google":{"ClientIdWeb":"898865790800-05q4u9h3hn78atr83ekpjm3d9qfjhmcn.apps.googleusercontent.com"}}
```

Aplicar manualmente `docs/sql/login-google.sql` antes de desplegar la API.
Requiere Usuarios, SolicitudesEliminacionCuenta (deuda-cliente-eliminar-cuenta.sql)
y los SP actuales del login/registro. No sustituye los SP existentes: agrega la
columna, índice único filtrado y tres SP de resolución, comprobación y enlace.
Es idempotente y compatible con QUOTED_IDENTIFIER ON. No se ejecutó SQL.

## POST /api/Auth/google

Solicitud (todos los contratos JSON usan camelCase):

```json
{"idToken":"ID_TOKEN_DE_GOOGLE"}
```

Enviar un ID token, no un access token. GoogleJsonWebSignature.ValidateAsync
valida firma, emisor, expiración y audiencia Google:ClientIdWeb. Se exige
email_verified=true. IValidadorGoogle permite sustituir la verificación en pruebas.

200: el mismo AuthResponseDto del login por contraseña:

```json
{"token":"JWT_CUIDAAPP","email":"persona@example.com","nombreCompleto":"Persona","fotoUrl":null,"userId":7,"rolId":2,"estadoAprobacion":null}
```

Valores ilustrativos; nombreCompleto, fotoUrl y estadoAprobacion pueden ser null.
EstadoAprobacion conserva su significado actual para cuidadores.

404: no existe una cuenta utilizable; nombre y fotoUrl vienen del token y pueden ser null:

```json
{"motivo":"REGISTRO_REQUERIDO","email":"persona@example.com","nombre":"Persona","fotoUrl":"https://example.com/foto"}
```

401: token inválido, expirado o con audiencia incorrecta:

```json
{"motivo":"GOOGLE_TOKEN_INVALIDO","message":"El token de Google no es válido o ha expirado."}
```

401: correo no verificado:

```json
{"motivo":"GOOGLE_CORREO_NO_VERIFICADO","message":"Google no ha verificado este correo."}
```

409: conflicto del vínculo:

```json
{"motivo":"GOOGLE_YA_ENLAZADO","message":"La identidad de Google ya está enlazada a otra cuenta."}
```

423: suspensión, idéntica al login normal (motivo, tipo y fecha dependen de la sanción):

```json
{"code":"ACCOUNT_SUSPENDED","message":"Tu cuenta está suspendida.","errors":null,"suspension":{"motivo":"Motivo de sanción","tipo":"TEMPORAL","fechaFinUtc":"2026-12-01T00:00:00+00:00"}}
```

fechaFinUtc puede ser null; tipo también puede ser INDEFINIDA.
423: cuenta inactiva:

```json
{"code":"ACCOUNT_INACTIVE","message":"Tu cuenta no está activa.","errors":null,"suspension":null}
```

403: eliminación pendiente (fecha con el formato DateTime del login existente):

```json
{"motivo":"CUENTA_EN_ELIMINACION","fechaEjecucion":"2026-12-01T00:00:00","usuarioId":7}
```

Si la cuenta desaparece entre la resolución y la lectura común, se conserva el 401 actual:

```json
{"code":"INVALID_CREDENTIALS","message":"Credenciales inválidas.","errors":null,"suspension":null}
```

Errores internos: 500, sin revelar detalles SQL:

```json
{"code":"INTERNAL_ERROR","message":"Error interno del servidor.","errors":null,"suspension":null}
```

## Registro

POST /api/Auth/register/cliente y POST /api/Auth/register/cuidador admiten
`googleIdToken` opcional. No hay otro endpoint de registro.

Cliente con Google, sin contraseña:

```json
{
  "email":"persona@example.com",
  "googleIdToken":"ID_TOKEN_DE_GOOGLE",
  "nombreCompleto":"Persona",
  "fotoUrl":null,
  "nacionalidadId":1,
  "documentoIdentidad":"DOCUMENTO",
  "telefono":"8095550100",
  "direccionPrincipal":"Dirección",
  "contactoEmergenciaNombre":"Contacto",
  "contactoEmergenciaTelefono":"8095550101"
}
```

Cuidador (los requisitos del tipo de trabajo y nacionalidad siguen vigentes):

```json
{
  "email":"persona@example.com",
  "googleIdToken":"ID_TOKEN_DE_GOOGLE",
  "nombreCompleto":"Persona",
  "fotoUrl":null,
  "nacionalidadId":1,
  "documentoIdentidad":"DOCUMENTO",
  "telefono":"8095550100",
  "estatusMigratorioId":null,
  "documentosExtra":[],
  "especialidad":"Especialidad configurada",
  "tarifaHora":300,
  "bio":null,
  "metodoCobro":null,
  "cedulaUrl":null,
  "cartaAntecedentesUrl":null,
  "testToken":"00000000-0000-0000-0000-000000000001",
  "codigoInvitacion":null
}
```

Cada documentoExtra conserva la forma `{"tipoDocumento":"Tipo","urlArchivo":"URL"}`.
password puede enviarse como cadena; si se omite o está vacío con Google se generan
48 bytes criptográficamente aleatorios en Base64 y se aplica el SHA-256 actual.
Nunca se devuelve esa contraseña.

El correo debe coincidir con el del token, sin distinguir mayúsculas; si difiere, 400:

```json
{"motivo":"GOOGLE_CORREO_DISTINTO","message":"El correo debe coincidir con el de Google."}
```

Errores Google 401 y 409: mismos cuerpos anteriores. Respuestas 200 existentes:

```json
{"message":"Cliente registrado con éxito","userId":7}
```

```json
{"message":"Cuidador registrado con éxito","userId":8}
```

Se conserva 409 cuando falta aprobar el test:

```json
{"code":"TEST_REQUERIDO","message":"Falta aprobar el test psicológico.","errors":null,"suspension":null}
```

También permanecen las validaciones migratorias y de invitación existentes.
Sin googleIdToken (omitido o null), no se valida Google ni se ejecutan sus SP y
se conserva el comportamiento actual de contraseña/registro. Una cadena vacía
se considera un intento Google inválido.

## Decisiones y concurrencia

- GoogleSub es la identidad estable, con comparación binaria y unicidad filtrada.
- Primero se busca por GoogleSub; después por correo con collation explícita
  insensible a mayúsculas. Un correo con otro GoogleSub provoca conflicto;
  varias coincidencias por correo se rechazan para evitar ambigüedad.
- Solo se enlaza automáticamente después de validar el token y verificar el
  correo. Nunca se sobrescribe otro GoogleSub, ni perfiles ni fotografías.
- El correo canónico local pasa al flujo común del login, que conserva suspensión,
  reactivación temporal, eliminación pendiente, nivel administrativo, JWT y AuthResponse.
- Las cuentas anonimizadas se excluyen por eliminación ejecutada o correo de
  anonimización. No se reactivan aunque conserven un GoogleSub histórico.
- Resolución/enlace usan transacción con UPDLOCK/HOLDLOCK e índice único.
  Registro y enlace son atómicos: un conflicto revierte todo el registro.
- No cambia la autenticación móvil ni agrega autorización a sus endpoints.
- Las pruebas unitarias usan un doble de IValidadorGoogle y sustituyen los puntos
  de acceso SQL/emisión JWT. No prueban ejecución real ni concurrencia de SQL Server;
  el script queda pendiente de aplicar y validar en ese entorno.
