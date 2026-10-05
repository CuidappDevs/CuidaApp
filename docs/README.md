# Documentación de CuidApp

Este directorio documenta el trabajo realizado sobre los tres proyectos de la solución:

- **`CUIDAPP_API`** — Backend (.NET, ADO.NET + stored procedures, sin ORM).
- **`CUIDAPP`** — App móvil (.NET MAUI, Android).
- **`CUIDAPP_ADMINISTRATIVO`** — Panel administrativo (Blazor Server).

## Índice

1. [api-changes.md](./api-changes.md) — Todos los endpoints, servicios y DTOs nuevos en `CUIDAPP_API`.
2. [database.md](./database.md) — Tablas y stored procedures creados, con las bases donde se aplicaron (dev/producción).
3. [admin-panel.md](./admin-panel.md) — Módulos del panel administrativo: qué hace cada uno y cómo funciona el login.
4. [mobile-app.md](./mobile-app.md) — Cambios en la app móvil: ícono, splash, mapas, calificaciones, soporte.
5. [deployment.md](./deployment.md) — Cómo publicar `CUIDAPP_API` desde Visual Studio directo al servidor (Web Deploy).
6. [roadmap.md](./roadmap.md) — Módulos del panel administrativo pendientes de construir.

## Convenciones seguidas durante el desarrollo

- **Todo cambio de base de datos se aplicó primero en `DBCuidappDev`, se verificó con datos reales, y luego en `DBCuidapp` (producción)** — nunca al revés.
- **Los cambios de esquema son aditivos**: se agregaron columnas/tablas/SPs nuevos; no se modificó el significado de nada que ya estuviera en uso por la app móvil o la API en producción.
- Cuando se tocó `CUIDAPP_API`, se avisó explícitamente que hace falta republicar en el servidor (ver [deployment.md](./deployment.md)) — los cambios de solo `CUIDAPP_ADMINISTRATIVO` no requieren tocar la API.
- Los datos de prueba usados para verificar un flujo (usuarios, pagos, tickets) se crearon y se borraron dentro de la misma sesión — no quedan residuos en la base.
