# Revisión individual de documentos

Aplicar manualmente `sql/revision-documentos-vencimiento.sql` después de `calidad-documentos-comision-disputas.sql` y `parciales-piloto.sql`. Es idempotente, usa QUOTED_IDENTIFIER ON y comillas simples para cadenas. No se ejecutó SQL.

## Contratos JSON

Los DTO y objetos anónimos se serializan en camelCase. GET /api/admin/documentos/{cuidadorId} devuelve 200:

```json
[{"id":12,"tipoDocumento":"Cedula","urlArchivo":"uploads/cedula.jpg","estado":1,"observacionesAdmin":null,"fechaSubida":"2026-11-16T10:00:00","requiereVencimiento":true,"fechaVencimiento":null}]
```

fechaVencimiento es una fecha yyyy-MM-dd o null (DateOnly).

PUT /api/admin/vencimientos/{documentoId}/revisar recibe:

```json
{"aprobar":true,"fecha":"2027-11-16","observaciones":null}
```

Admite documentos en estado 1 o 3. Aprobar guarda estado 2, fecha y reinicia avisos; limpia observaciones. Rechazar guarda estado 3, motivo recortado y conserva la fecha anterior. Éxito 200: `{"message":"Documento aprobado."}` o `{"message":"Documento rechazado."}`. No aprueba la cuenta ni activa visibilidad.

PUT /api/admin/vencimientos/{documentoId} recibe `{"fecha":"2027-11-16"}`. Éxito 200: `{"message":"Fecha de vencimiento guardada."}`. No cambia el estado y reinicia avisos.

Errores 400:

```json
{"motivo":"FECHA_VENCIMIENTO_REQUERIDA","message":"Pon la fecha de vencimiento del documento antes de aprobarlo."}
{"motivo":"FECHA_VENCIMIENTO_INVALIDA","message":"La fecha de vencimiento debe ser posterior a hoy."}
{"motivo":"MOTIVO_REQUERIDO","message":"Indica el motivo del rechazo."}
```

Fecha requerida aplica al aprobar o fijar fecha de un tipo del catálogo. Cualquier fecha proporcionada debe ser posterior a hoy. Motivo requerido aplica al rechazo sin observaciones. Se conservan los errores existentes NO_ENCONTRADO (404) y ESTADO_INVALIDO (409).

PUT /api/admin/aprobar-cuidador recibe `{"cuidadorId":42,"nuevoEstado":2,"observaciones":null}`. Éxito 200: `{"message":"Estado actualizado correctamente"}`. Bloqueo general 409:

```json
{"motivo":"DOCUMENTOS_SIN_REVISAR","message":"Revisa cada documento antes de aprobar la cuenta.","faltantes":["Cedula (sin fecha)","Curso (sin revisar)","Pasaporte (vencido)","CartaAntecedentes (rechazado)"]}
```

Se comprueban todos los registros. Prioridad por documento: rechazado, sin revisar, sin fecha, vencido. Las dos últimas razones aplican a tipos del catálogo. La validación migratoria conserva prioridad y formato 409 actual:

```json
{"code":"DOCUMENTOS_MIGRATORIOS_FALTANTES","faltantes":["Pasaporte","Estatus migratorio"]}
```

GET /api/admin/cuidadores/{usuarioId}/migratorio conserva esExtranjero, nacionalidad, estatusId, estatus, documentos (tipo, estado, vencimiento, url, presente), faltantes y puedeAprobar. Para una extranjera con estatus inactivo, faltantes incluye Estatus migratorio y puedeAprobar=false.

POST /api/auth/register/cuidador con estatus indicado inexistente o inactivo devuelve 400:

```json
{"motivo":"ESTATUS_MIGRATORIO_INVALIDO","message":"Ese estatus migratorio no está disponible."}
```

La extranjera que omite estatus conserva ESTATUS_MIGRATORIO_REQUERIDO. GET /api/nacionalidad/estatus-migratorio solo devuelve Activo=1.

## Decisiones

Catálogo obligatorio: Cedula, CartaAntecedentes, Certificado Apostillado, Certificado médico, Certificado de Buena Conducta (PGR), Carnet de residencia, Carnet PNRE, Visa de trabajo y Pasaporte. Comparación CI_AS con LTRIM/RTRIM: ignora mayúsculas y espacios extremos, conserva acentos. Los demás tipos permiten null o fecha futura opcional. El registro y reemplazo desde la app no admiten fecha del usuario.

Se desactiva Extranjero en proceso. Las reglas se validan dentro de transacciones SQL con bloqueos. La aprobación de cuenta solo llega al procedimiento anterior cuando todos los documentos ya están aprobados; no llega con pendientes para aprobarlos en bloque. Rechazar/revocar mantiene el procedimiento anterior. Se mantienen políticas y auditoría.

Hoy usa HoraLocalRD (UTC-4), con respaldo equivalente en SQL. Una fecha igual a hoy ya es inválida/vencida. El trabajo horario conserva avisos a 30 y 7 días, rechazo y ocultamiento; recorre todos los tipos, incluidos migratorios y fechas opcionales. Aprobar o fijar fecha reinicia AvisoVencimiento.

## Validación

- Build y test normales: restauración bloqueada por acceso al NuGet.Config del usuario.
- `dotnet build CUIDAPP_API/CUIDAPP_API.csproj --no-restore`: correcto.
- `dotnet test CUIDAPP_API.Tests --no-restore`: 113 pruebas correctas.
- Pruebas nuevas: traducción de errores SQL de fecha requerida/inválida, motivo obligatorio, bloqueo de cuenta y validación de estatus inactivo.
- Las pruebas de controladores usan dobles de SQL. No prueban ejecución ni concurrencia del script en SQL Server. Pendiente validar la migración en una base autorizada.
- Advertencias existentes de NuGet y nullable en AuthService.
