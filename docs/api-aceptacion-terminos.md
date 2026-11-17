# Aceptación de Términos y Condiciones

POST `/api/Auth/register/cliente` y `/api/Auth/register/cuidador` reciben el campo opcional `terminosVersion` (máximo 20 caracteres). Es opcional para deserialización; omitirlo impide crear la cuenta. La comparación es exacta con `Terminos:VersionVigente`, configurada en ambos appsettings como `2026-11-16`.

Agregar a los JSON de registro existentes:

```json
{"terminosVersion":"2026-11-16"}
```

Sin versión, null, vacía o solo espacios: HTTP 400, JSON exacto:

```json
{"motivo":"TERMINOS_REQUERIDOS","message":"Debes aceptar los Términos y Condiciones."}
```

Versión diferente: HTTP 409, JSON exacto:

```json
{"motivo":"TERMINOS_DESACTUALIZADOS","message":"Hay una versión nueva de los Términos y Condiciones. Vuelve a abrir el registro."}
```

La versión vigente permite continuar las validaciones existentes; no garantiza que el resto del registro sea válido. Más de 20 caracteres provoca la respuesta de validación de modelos de ASP.NET Core. Los errores de términos se comprueban antes de Google y SQL.

El servicio pasa `@TerminosVersion` y `@TerminosAceptadosEn` (`HoraLocalRD.Ahora`) a ambos SP de creación. El camino Google de `login-google.sql` usa esos mismos SP y conserva su transacción y enlace; iniciar sesión con Google no modifica la aceptación previa.

## SQL y despliegue

Aplicar manualmente `docs/sql/aceptacion-terminos.sql` después de `registro-sps.sql`, `parciales-piloto.sql` y `login-google.sql`, antes de desplegar la API. No se ha ejecutado SQL durante este cambio.

El script idempotente agrega `Usuarios.TerminosVersion NVARCHAR(20) NULL` y `TerminosAceptadosEn DATETIME2 NULL`. Conserva el registro de perfiles, documentos y estatus migratorio. Sus parámetros nuevos tienen valor NULL por compatibilidad con llamadas antiguas; estas no constituyen aceptación. La fecha alternativa del SP usa UTC-4. Las cuentas históricas permanecen con NULL.

Las definiciones de `sp_ObtenerClienteAdminDetalle` y `sp_ObtenerCuidadorAdminDetalle` no están versionadas en el repositorio. El script lee sus definiciones instaladas y usa CREATE OR ALTER para agregar únicamente las dos proyecciones, conservando el resto. Requiere permiso para leer las definiciones y alterarlas; falla explícitamente si faltan o si el primer SELECT usa TOP/DISTINCT. Revisar estas definiciones antes del despliegue. No contiene cadenas delimitadas con comillas dobles.

Los detalles administrativos devuelven `terminosVersion` y `terminosAceptadosEn` (nullable), solo lectura; no se agregan a DTOs de edición. Ejemplo de los campos añadidos:

```json
{"terminosVersion":"2026-11-16","terminosAceptadosEn":"2026-11-16T10:30:00"}
```

La fecha representa hora local RD sin offset (DATETIME2). No se modifican las interfaces MAUI ni Blazor.

## Verificación

Pruebas unitarias para omisión/null/vacío/espacios, versión anterior y vigente, ambos roles y camino Google, respuesta exacta y límite de longitud. No requieren base de datos. La versión vigente se verifica hasta la siguiente validación de Google, sin insertar usuarios.

Resultados: `dotnet build CUIDAPP_API/CUIDAPP_API.csproj` y `dotnet test CUIDAPP_API.Tests` fallaron durante restauración por acceso denegado al NuGet.Config del usuario. Con `--no-restore`, compilación correcta (0 errores) y 107 pruebas aprobadas, 0 fallidas. Se mantienen avisos de paquetes y uno de nulabilidad del código existente.
