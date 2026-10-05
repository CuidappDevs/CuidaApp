# Cambios de base de datos

Todo lo listado aquí está aplicado en **`DBCuidappDev`** y **`DBCuidapp`** (producción). No existen migraciones automáticas — los scripts se corrieron manualmente vía `sqlcmd` contra ambas bases.

## Tablas nuevas

| Tabla | Para qué | Notas |
|---|---|---|
| `Pagos` (extendida) | Autorización/aprobación de pagos a Care Partners | Ya existía; se agregaron columnas `AutorizadoPorAdminId`, `FechaAutorizacion`, `AprobadoPorAdminId`. `Estado`: 1=Pendiente, 3=Autorizado (nuevo), 2=Pagado (sin cambios de significado). |
| `Tickets` | Centro de soporte | `Estado`: 1=Abierto, 2=En proceso, 3=Resuelto. |
| `TicketMensajes` | Hilo de conversación de cada ticket | `EsAdmin` distingue mensajes del usuario vs. del staff. |
| `SancionesCuidador` | Auditoría de suspensión/reactivación de cuentas | Pese al nombre, es **genérica**: solo depende de `UsuarioId` contra `Usuarios`, sin filtrar por rol. Se reutiliza igual para Care Partners, Clientes y Administradores. |

## Stored procedures nuevos, por módulo

### Pagos y finanzas
- `sp_ObtenerPagosAdmin` — lista de pagos con datos de trabajo/cliente/cuidador, filtrable por estado.
- `sp_AutorizarPago` — Pendiente → Autorizado.
- `sp_AprobarPago` — Autorizado → Pagado.

### Centro de soporte
- `sp_CrearTicket` — crea el ticket + su primer mensaje.
- `sp_ObtenerTicketsPorUsuario` — "Mis reportes" en la app móvil.
- `sp_ObtenerTicketsAdmin` — listado para el panel, filtrable por estado.
- `sp_ObtenerTicketDetalle` / `sp_ObtenerMensajesTicket` — ficha + hilo.
- `sp_AgregarMensajeTicket` — agrega mensaje; si el autor es admin y el ticket estaba "Abierto", lo pasa automáticamente a "En proceso".
- `sp_ActualizarEstadoTicket` — cambio de estado manual.

### Care Partners (cuidadores) — panel admin
- `sp_ObtenerCuidadoresAdmin` / `sp_ObtenerCuidadorAdminDetalle` — listado y ficha completos (a diferencia de `sp_ObtenerCuidadoresPendientes`, que ya existía y solo trae pendientes).
- `sp_ActualizarInfoCuidador` — edición de nombre, especialidad, tarifa, bio, método de cobro.
- `sp_SuspenderCuidador` / `sp_ReactivarCuidador` / `sp_ObtenerSancionesCuidador` — sistema de suspensión (ver tabla `SancionesCuidador` arriba).

### Clientes — panel admin
- `sp_ObtenerClientesAdmin` / `sp_ObtenerClienteAdminDetalle` — listado y ficha.
- `sp_ActualizarInfoCliente` — edición de nombre, dirección, contacto de emergencia.
- Suspender/reactivar/sanciones: **reutiliza los mismos SPs de Care Partners** (son genéricos).

### Administradores
- `sp_CrearUsuarioAdmin` — crea un usuario con `RolId=1`; valida correo duplicado.
- `sp_ObtenerUsuariosAdmin` — lista las cuentas con acceso al panel.
- Suspender/reactivar: reutiliza los mismos SPs genéricos.

## Preexistentes que se reutilizaron sin modificar

Estos ya existían en la API (probablemente de una sesión anterior) y **no se tocaron**, solo se conectaron a una interfaz real por primera vez:

- `sp_ObtenerCuidadoresPendientes`
- `sp_ObtenerDocumentosPorCuidador` (tabla real: `DocumentosCuidador`)
- `sp_ActualizarEstadoCuidador` (aprobar/rechazar verificación de documentos — Estado 1/2/3, distinto del `EstadoAprobacion` de suspensión de cuenta)

## Cómo verificar qué SPs existen

```sql
SELECT name FROM sys.procedures WHERE name LIKE 'sp_%' ORDER BY name;
```

## Nota sobre `IsActive`

`Usuarios.IsActive` ya bloqueaba el login (`AuthService.LoginAsync` lo comprobaba desde antes de esta sesión). El sistema de suspensión/baneo construido en esta sesión **reutiliza ese campo** en vez de crear un mecanismo nuevo — suspender a alguien simplemente pone `IsActive=0`, lo que ya le impide iniciar sesión en cualquiera de las tres apps.
