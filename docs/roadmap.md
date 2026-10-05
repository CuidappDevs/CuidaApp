# Módulos pendientes del panel administrativo

Ya construidos: Dashboard (parcial), Care Partners, Clientes, Pagos y finanzas, Soporte, Administradores.

Pendientes, en orden de impacto sugerido (los primeros son los que hoy solo se pueden resolver tocando la base de datos directamente):

1. **Trabajos / Servicios en curso** — vista de todos los trabajos por estado (pendiente, aceptado, en progreso, esperando confirmación, completado, cancelado, disputado), con capacidad de intervenir manualmente.
2. **Disputas** — cola específica para casos con `PagoDisputado=1` o `RechazadoPorCliente=1`, con la justificación y evidencia para que un admin decida.
3. **Geolocalización en vivo / Mapa de operaciones** — dónde están los cuidadores activos ahora y las alertas de geocerca en tiempo real.
4. **Auditoría / Bitácora general de acciones administrativas** — hoy existe parcialmente (`SancionesCuidador`, `AutorizadoPorAdminId`/`AprobadoPorAdminId` en `Pagos`), pero no hay un log centralizado de todo lo que hace cada admin.
5. **Calificaciones y reseñas (moderación)** — ver todas las calificaciones, detectar patrones de abuso, ocultar una reseña puntual.
6. **Reportes y analítica** — categorías más solicitadas, zonas con más demanda, tasa de cancelación, tiempo de respuesta.
7. **Notificaciones y comunicados masivos** — aprovechando el `EmailService` ya construido, enviar avisos a todos los cuidadores o clientes.
8. **Configuración de la plataforma** — categorías de servicio, tarifas mínimas/máximas, radio de geocerca (300m hardcodeado hoy).
9. **Chat / Mensajería (supervisión)** — revisar una conversación puntual, solo ante un reporte (sensible — no debería ser vigilancia general).

## Otras ideas discutidas (no priorizadas)

- Favoritos y servicios recurrentes en la app móvil.
- Sistema de referidos.
- Badges/insignias de confianza para cuidadores.
- Verificación en dos niveles (básica vs. premium).
- Firebase Cloud Messaging (notificaciones 100% confiables en background — hoy dependen de un foreground service, menos confiable).
- Backups automáticos de la base de datos.
- Logs centralizados / monitoreo (hoy solo `Console.WriteLine`).
- Tests automatizados (el proyecto no tiene ninguno todavía).
- Rate limiting en la API (ej. `auth/login` no tiene límite de intentos).
