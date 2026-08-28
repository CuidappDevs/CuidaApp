# CUIDAPP

CuidApp es una plataforma que conecta a clientes con cuidadores profesionales (niñeras, cuidado de adultos mayores, limpieza del hogar, entre otros servicios de cuidado) de forma rápida, segura y verificada. Permite solicitar un servicio, hacer seguimiento en tiempo real de todo el proceso, y calificar la experiencia al finalizar.

El proyecto está compuesto por dos aplicaciones:

- **`CUIDAPP_API`** — Backend en .NET, expuesto como Web API, con acceso a datos mediante ADO.NET puro y stored procedures (sin ORM).
- **`CUIDAPP`** — App móvil en .NET MAUI (Android), usada tanto por clientes como por cuidadores.

## Funcionalidades principales

- **Registro y verificación de cuidadores**, con carga de documentos (cédula, antecedentes) y estado de verificación pendiente/aprobado.
- **Solicitud de servicios** por categoría, con búsqueda de cuidadores cercanos en un mapa interactivo.
- **Múltiples servicios activos simultáneos** por cliente, cada uno con su propio detalle, PIN de acceso y seguimiento.
- **Sistema de PIN de inicio y fin de servicio**, con confirmación del cliente al finalizar (o justificación del cuidador si termina antes de lo acordado) antes de generar el pago.
- **Ruta en tiempo real por calles** entre el cuidador y la ubicación del cliente (Mapbox Directions API), con mapa visual estilo Mapbox.
- **Bitácora de actividades**: el cuidador reporta lo que va haciendo durante el servicio, visible al instante para el cliente.
- **Chat en tiempo real** entre cliente y cuidador (texto, imágenes y notas de voz), habilitado mientras el servicio está activo.
- **Notificaciones en tiempo real** (SignalR) con banner in-app en primer plano y notificaciones nativas de Android en segundo plano, más un centro de notificaciones con historial.
- **Geocerca de seguridad**: alerta al cliente si el cuidador se aleja del sitio del servicio mientras está "en progreso".
- **Sistema de calificaciones** bidireccional (cliente↔cuidador) con promedio, comentarios y una pantalla de "Mis calificaciones" con el historial de reseñas recibidas.
- **Ubicaciones guardadas del cliente**, con selección de dirección en mapa.
- **Reloj del servidor como fuente de verdad** para validaciones de fecha/hora, evitando inconsistencias por la hora del dispositivo.

## Stack técnico

- **Backend:** .NET (Web API), ADO.NET + SQL Server (stored procedures), SignalR para tiempo real.
- **App móvil:** .NET MAUI (Android), Leaflet.js + Mapbox para mapas y rutas, Plugin.Maui.Audio para notas de voz.
- **Base de datos:** SQL Server, con entornos separados de desarrollo (`DBCuidappDev`) y producción (`DBCuidapp`).
