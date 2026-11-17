using System.Net;
namespace CUIDAPP_ADMINISTRATIVO.Services;

// Página pública de seguimiento compartido (la familia la abre desde WhatsApp, sin iniciar sesión).
// No pasa por los componentes del panel ni por su sesión: HTML propio + /css/seguimiento.css + /js/seguimiento.js.
public static class SeguimientoPublicoEndpoints
{
    public static void MapSeguimientoPublico(this WebApplication app)
    {
        app.MapGet("/seguimiento/{token}", (string token, HttpContext http, IConfiguration config) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            // "origin" (no la ruta con el token): Mapbox lo necesita para validar la clave, y el token no sale de la página.
            http.Response.Headers["Referrer-Policy"] = "strict-origin";
            var safe = WebUtility.HtmlEncode(token);
            var mapbox = WebUtility.HtmlEncode(config["MapboxToken"] ?? "");
            return Results.Content("""
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<meta name="referrer" content="strict-origin">
<meta name="robots" content="noindex,nofollow">
<meta name="theme-color" content="#1C4D96">
<title>Seguimiento del servicio · CuidaApp</title>
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css">
<link rel="stylesheet" href="/css/seguimiento.css">
</head>
<body>
<header class="sg-top">
  <div class="sg-top-in">
    <div class="sg-marca">
      <span class="sg-logo" aria-hidden="true"><svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M9 12.5 11 14.5 15.5 10"/><path d="M12 3 4.5 6v5.5c0 4.4 3.1 8 7.5 9.5 4.4-1.5 7.5-5.1 7.5-9.5V6z"/></svg></span>
      <span><b>CuidaApp</b><small>Seguimiento del servicio</small></span>
    </div>
    <span id="estado" class="sg-estado" aria-live="polite"><i></i><span>Cargando…</span></span>
  </div>
</header>

<main class="sg" id="contenido">
  <div id="cargando" class="sg-cargando" role="status"><span class="sg-spin" aria-hidden="true"></span>Cargando el servicio…</div>

  <section id="detalle" class="sg-grid" hidden>
    <!-- Care Partner + SOS -->
    <article class="sg-card sg-persona" style="grid-area:persona">
      <div class="sg-persona-cab">
        <span class="sg-avatar"><img id="foto" alt="" hidden><span id="iniciales" aria-hidden="true"></span></span>
        <div class="sg-persona-txt">
          <h1 id="nombre"></h1>
          <p id="tipo"></p>
          <p id="telefonoTxt" class="sg-tel"></p>
        </div>
      </div>
      <div class="sg-acciones">
        <a id="llamar" class="sg-btn sg-btn-pri"><svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" aria-hidden="true"><path d="M6.6 10.8a15.1 15.1 0 0 0 6.6 6.6l2.2-2.2c.3-.3.7-.4 1-.2 1.1.4 2.3.6 3.6.6.6 0 1 .4 1 1V20c0 .6-.4 1-1 1A17 17 0 0 1 3 4c0-.6.4-1 1-1h3.5c.6 0 1 .4 1 1 0 1.3.2 2.5.6 3.6.1.3 0 .7-.2 1z"/></svg>Llamar</a>
        <a id="whatsapp" class="sg-btn sg-btn-wa" rel="noreferrer" target="_blank"><svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" aria-hidden="true"><path d="M12 2a10 10 0 0 0-8.6 15.1L2 22l5-1.3A10 10 0 1 0 12 2zm5.2 14.1c-.2.6-1.3 1.2-1.8 1.3-.5.1-1 .1-3.3-.8-2.8-1.1-4.5-3.9-4.7-4.1-.1-.2-1.1-1.5-1.1-2.9s.7-2 1-2.3c.2-.3.5-.3.7-.3h.5c.2 0 .4 0 .6.5l.8 2c.1.2.1.4 0 .5l-.4.6-.4.4c-.1.2-.3.3-.1.6.2.3.8 1.4 1.8 2.2 1.2 1.1 2.2 1.4 2.5 1.6.3.1.5.1.7-.1l.9-1.1c.2-.3.4-.2.7-.1l1.9.9c.3.1.5.2.5.3.1.2.1.6-.1 1.1z"/></svg>WhatsApp</a>
      </div>
      <button type="button" id="btnSos" class="sg-btn sg-btn-sos">
        <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><path d="M12 9v4m0 4h.01"/></svg>
        Emergencia · SOS
      </button>
      <p id="sosEstado" class="sg-sos-estado" hidden></p>
    </article>

    <!-- Ubicación (en PC queda fija a la derecha) -->
    <article class="sg-card sg-mapa-card" id="tarjetaMapa" style="grid-area:mapa">
      <div class="sg-card-cab">
        <h2>Ubicación</h2>
        <span id="vivo" class="sg-vivo" hidden><i></i><span>En vivo</span></span>
      </div>
      <div id="mapa" class="sg-mapa" hidden></div>
      <div id="sinMapa" class="sg-sinmapa" hidden>
        <svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 21s-7-6.2-7-11.5A7 7 0 0 1 19 9.5C19 14.8 12 21 12 21z"/><circle cx="12" cy="9.5" r="2.5"/></svg>
        <p id="sinMapaTxt">La ubicación se comparte mientras el servicio está en curso.</p>
      </div>
      <div class="sg-meta">
        <span id="actualizacion"></span>
        <span id="bateria" class="sg-bateria" hidden></span>
      </div>
    </article>

    <!-- Datos rápidos: tiempo trabajando y PIN de salida -->
    <div class="sg-datos" style="grid-area:datos">
      <article class="sg-card sg-dato">
        <span class="sg-dato-ico"><svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="13" r="8"/><path d="M12 9v4l2.5 2.5M9 2h6"/></svg></span>
        <span class="sg-dato-lbl">Tiempo trabajando</span>
        <b id="tiempoTrabajando" class="sg-dato-val">—</b>
        <span id="tiempoDetalle" class="sg-dato-sub"></span>
      </article>
      <article class="sg-card sg-dato" id="tarjetaPin">
        <span class="sg-dato-ico es-pin"><svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="5" y="11" width="14" height="9" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/></svg></span>
        <span class="sg-dato-lbl">PIN de salida</span>
        <button type="button" id="pin" class="sg-pin" aria-label="Mostrar el PIN de salida"><span id="pinTxt">• • • •</span><svg id="pinOjo" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/></svg></button>
        <span class="sg-dato-sub">Dáselo solo cuando termine el servicio</span>
      </article>
    </div>

    <!-- Servicio -->
    <article class="sg-card" style="grid-area:servicio">
      <div class="sg-card-cab"><h2>Servicio</h2><span id="horario" class="sg-muted"></span></div>
      <div id="avance" class="sg-avance" hidden>
        <div class="sg-barra"><span id="barra"></span></div>
        <div class="sg-avance-txt"><span id="transcurrido"></span><span id="restante"></span></div>
      </div>
      <ol id="timeline" class="sg-linea"></ol>
    </article>

    <!-- Tareas y actividades -->
    <div style="grid-area:tareas">
      <article class="sg-card" id="tarjetaTareas" hidden>
        <div class="sg-card-cab"><h2>Tareas</h2><span id="tareasCuenta" class="sg-chip"></span></div>
        <ul id="tareas" class="sg-tareas"></ul>
      </article>
      <article class="sg-card" id="tarjetaActividades" hidden>
        <div class="sg-card-cab"><h2>Actividades</h2></div>
        <ul id="actividades" class="sg-actividades"></ul>
      </article>
      <p class="sg-aviso">
        <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M12 8v5m0 3h.01"/></svg>
        En una emergencia llama al 9-1-1. El botón SOS avisa al equipo de CuidaApp, pero no sustituye los servicios de emergencia.
      </p>
    </div>
  </section>

  <!-- Ventana del SOS -->
  <div id="sosVentana" class="sg-velo" hidden>
    <div class="sg-dialogo" role="dialog" aria-modal="true" aria-labelledby="sosTitulo">
      <span class="sg-dialogo-ico" aria-hidden="true"><svg viewBox="0 0 24 24" width="30" height="30" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"/><path d="M12 9v4m0 4h.01"/></svg></span>
      <h2 id="sosTitulo">¿Necesitas ayuda?</h2>
      <p>Si alguien está en peligro, llama primero al 9-1-1. También puedes avisar al equipo de CuidaApp: verán la ubicación del servicio y te contactarán.</p>
      <a class="sg-btn sg-btn-sos" href="tel:911">Llamar al 9-1-1</a>
      <button type="button" id="sosAvisar" class="sg-btn sg-btn-borde">Avisar a CuidaApp</button>
      <button type="button" id="sosCancelar" class="sg-btn sg-btn-quieto">Cancelar</button>
    </div>
  </div>

  <section id="error" class="sg-card sg-error" hidden>
    <svg viewBox="0 0 24 24" width="40" height="40" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="5" y="11" width="14" height="9" rx="2"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/></svg>
    <h1>Seguimiento no disponible</h1>
    <p>El enlace venció o la familia dejó de compartirlo.</p>
  </section>
</main>
<footer class="sg-pie">CuidaApp · Este enlace es privado: no lo compartas con desconocidos.</footer>

<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
<script src="/js/seguimiento.js" defer data-mapbox="
""" + mapbox + "\" data-token=\"" + safe + "\"></script>\n</body>\n</html>", "text/html; charset=utf-8");
        }).AllowAnonymous();

        app.MapGet("/seguimiento-datos/{token}", async (string token, IHttpClientFactory factory, IConfiguration config, HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (token.Length != 43) return Results.NotFound();
            using var client = factory.CreateClient();
            using var response = await client.GetAsync(new Uri(new Uri(config["ApiBaseUrl"]!), "seguimiento/" + Uri.EscapeDataString(token)), http.RequestAborted);
            if (!response.IsSuccessStatusCode) return Results.NotFound();
            return Results.Content(await response.Content.ReadAsStringAsync(), "application/json");
        }).AllowAnonymous();

        // SOS del familiar: se reenvía a la API, que crea la alerta en el centro de mando.
        app.MapPost("/seguimiento-sos/{token}", async (string token, IHttpClientFactory factory, IConfiguration config, HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (token.Length != 43) return Results.NotFound();
            using var client = factory.CreateClient();
            using var response = await client.PostAsync(new Uri(new Uri(config["ApiBaseUrl"]!), "seguimiento/" + Uri.EscapeDataString(token) + "/sos"), null, http.RequestAborted);
            return Results.Content(await response.Content.ReadAsStringAsync(), "application/json", statusCode: (int)response.StatusCode);
        }).AllowAnonymous().DisableAntiforgery();
    }
}
