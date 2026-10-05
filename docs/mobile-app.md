# App móvil (`CUIDAPP`)

## Ícono y splash screen

- **Ícono**: se reemplazó el ícono por defecto de MAUI usando el export de easyappicon (`Resources/AppIcon/appicon.png` + `appiconfg.png`, fondo blanco). MAUI regenera automáticamente todas las densidades de Android al compilar.
- **Splash animado**: como el splash *nativo* del sistema operativo no soporta animaciones (solo puede mostrar una imagen estática), se dejó ese splash nativo en blanco liso, y se construyó un splash real como pantalla propia (`Views/Splash/SplashPage.xaml`): logo con fade + scale, tagline, tres puntos de carga pulsando, versión de la app al pie. `App.xaml.cs` muestra primero `SplashPage` y, al terminar la animación, cambia a `AppShell` (login).
- **Logo real**: se extrajo el PNG (946×890, transparente) que estaba embebido dentro de un SVG (`Resources/logoCuidapp.svg` envolvía una imagen base64) y se guardó como `Resources/Images/logo_cuidapp.png`. Se usa en el login y en el splash, envuelto en un `Grid` con margen para que no toque los bordes del marco (el diseño del logo no es simétrico dentro de su lienzo).

## Mapas y rutas (Mapbox)

- **Ruta por calles**: se reemplazó el trazado de línea recta entre cuidador y cliente por una ruta real siguiendo las calles, usando **Mapbox Directions API** (se descartó OSRM público por ser solo un servidor de demo, no apto para producción).
- **Tiles del mapa**: los tres mapas de la app (`DetalleTrabajoPage`, `ClienteDashboardPage`, `SeleccionarPuntoMapaPage`) usan el estilo `mapbox/light-v11` (claro, oficial de Mapbox) en vez de CartoDB. Se probó un estilo personalizado de Mapbox Studio pero no cargaba (el endpoint devolvía 404 — no se resolvió la causa raíz) y se revirtió al estilo oficial.
- El token usado es el **público** (`pk.`), diseñado por Mapbox para ir embebido en apps cliente — no es sensible. Se generó también un token secreto (`sk.`) que **no se usa en ningún lado** (los secretos no van en el cliente).

## Login y sesión

- **Fix de crash al calificar**: la navegación de salida tras calificar usaba `Shell.Current.GoToAsync("//RutaDestino")` — el prefijo `//` exige que el destino sea parte de la jerarquía de `ShellContent`, pero esas pantallas son rutas planas. Cambiado a `///` (tres barras), que sí funciona con rutas fuera de esa jerarquía. Afectaba a `DetalleTrabajoPage`, `ClientePerfilPage` y `DetalleServicioClientePage`.
- **Colores**: hipervínculos morados (`#391C8C`, resto de una rebranding anterior incompleta) cambiados al azul de marca (`#2563EB`) en el login.

## Mis calificaciones

Nueva pantalla (`Views/Calificacion/MisCalificacionesPage.xaml`) — historial de reseñas recibidas (promedio, comentario, quién calificó, fecha), accesible desde el perfil de cliente y de cuidador.

## Centro de soporte (nuevo)

- Botón **"Soporte / Reportar un problema"** en el perfil de cliente y de cuidador.
- `Views/Soporte/MisReportesPage.xaml` — lista de reportes con su estado.
- `Views/Soporte/NuevoReportePage.xaml` — formulario (categoría, asunto, descripción).
- `Views/Soporte/DetalleReportePage.xaml` — hilo de conversación tipo chat con el admin.

## Notas de compatibilidad

Todos los cambios de la app móvil son aditivos o de corrección de bugs — ningún endpoint de la API que ya usaba la app fue modificado en su comportamiento (los cambios de `CUIDAPP_API` en esta sesión son endpoints nuevos, no tocan los existentes que la app ya consume).

## Multi-idioma (español / inglés)

- `Localization/Localizador.cs` carga `Resources/Strings/{es,en}.json` (recursos incrustados) y aplica la cultura; el idioma se guarda en `Preferences["Idioma"]` y, si no hay uno guardado, se usa el del dispositivo (es/en, por defecto es).
- XAML usa `{loc:T clave}` (se actualiza en vivo); C# usa `Localizador.T/F/D`. Ver las reglas en `AGENTS.md`.
- Selector en la pantalla de login y en los perfiles de cliente y cuidador. Al cambiar de idioma se reconstruye el `Shell` y se vuelve al dashboard del usuario.
- Para agregar textos: añadir la clave en `es.json` y `en.json` y correr `Resources/Strings/verificar_traducciones.py`.
- Pendiente fuera de alcance: mensajes de error que devuelve la API (siguen en español) y el panel administrativo.
