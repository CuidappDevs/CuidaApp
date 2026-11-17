// Seguimiento compartido: consulta el servicio cada 10 s mientras está activo y pinta la página.
(() => {
  'use strict';
  const script = document.querySelector('script[data-token]');
  const token = script.dataset.token.trim();
  const mapbox = (script.dataset.mapbox || '').trim();
  const $ = id => document.getElementById(id);
  const oscuro = window.matchMedia('(prefers-color-scheme: dark)');
  let mapa, marcador, capa, timer, ultimo, centrado = false, pinVisible = false;

  // Mapa de Mapbox (la misma clave del panel y de la app); sin clave, OpenStreetMap como respaldo.
  const mosaicos = () => mapbox
    ? `https://api.mapbox.com/styles/v1/mapbox/${oscuro.matches ? 'dark-v11' : 'streets-v12'}/tiles/{z}/{x}/{y}{r}?access_token=${encodeURIComponent(mapbox)}`
    : 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
  const opcionesMosaicos = () => mapbox
    ? { maxZoom: 20, tileSize: 512, zoomOffset: -1, attribution: '© <a href="https://www.mapbox.com/about/maps/" target="_blank" rel="noreferrer">Mapbox</a> © <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">OpenStreetMap</a>' }
    : { maxZoom: 19, attribution: '© OpenStreetMap' };

  const ESTADOS = {
    2: ['Programado', 'es-programado'],
    3: ['Trabajando ahora', 'es-trabajando'],
    7: ['Esperando confirmación', 'es-confirmar'],
    4: ['Terminado', 'es-terminado']
  };

  const fecha = v => v ? new Date(v) : null;
  const hora = d => d ? d.toLocaleTimeString('es-DO', { hour: 'numeric', minute: '2-digit' }) : '';
  const dia = d => {
    if (!d) return '';
    const hoy = new Date(), ayer = new Date(Date.now() - 864e5);
    if (d.toDateString() === hoy.toDateString()) return 'Hoy, ' + hora(d);
    if (d.toDateString() === ayer.toDateString()) return 'Ayer, ' + hora(d);
    return d.toLocaleDateString('es-DO', { day: 'numeric', month: 'short' }) + ', ' + hora(d);
  };
  const hace = d => {
    const s = Math.max(0, Math.round((Date.now() - d) / 1000));
    if (s < 45) return 'hace unos segundos';
    const m = Math.round(s / 60);
    if (m < 60) return m === 1 ? 'hace 1 minuto' : `hace ${m} minutos`;
    const h = Math.round(m / 60);
    return h === 1 ? 'hace 1 hora' : `hace ${h} horas`;
  };
  const duracion = ms => {
    const m = Math.max(0, Math.round(ms / 60000)), h = Math.floor(m / 60), r = m % 60;
    return h ? `${h} h${r ? ` ${r} min` : ''}` : `${r} min`;
  };
  const iniciales = n => (n || '').split(/\s+/).filter(Boolean).slice(0, 2).map(p => p[0].toUpperCase()).join('');

  // Teléfono dominicano de 10 dígitos (809/829/849) → con el código de país 1.
  const telefono = t => {
    let d = (t || '').replace(/\D/g, '');
    if (d.length === 10) d = '1' + d;
    return d;
  };

  function mostrarError() {
    $('cargando').hidden = true;
    $('detalle').hidden = true;
    $('error').hidden = false;
    pintarEstado(null);
    if (mapa) { mapa.remove(); mapa = null; marcador = null; }
    clearTimeout(timer);
  }

  function pintarEstado(estado) {
    const [texto, clase] = ESTADOS[estado] || ['No disponible', 'es-terminado'];
    const el = $('estado');
    el.className = 'sg-estado ' + clase;
    el.querySelector('span').textContent = texto;
  }

  function pintarPersona(s) {
    $('nombre').textContent = s.NombreCompleto || 'Care Partner';
    $('tipo').textContent = s.TipoServicio || '';
    const foto = $('foto');
    if (s.FotoUrl) { foto.src = s.FotoUrl; foto.alt = s.NombreCompleto || ''; foto.hidden = false; $('iniciales').hidden = true; }
    else { foto.hidden = true; $('iniciales').hidden = false; $('iniciales').textContent = iniciales(s.NombreCompleto); }
    const tel = telefono(s.Telefono);
    const d10 = tel.length === 11 ? tel.slice(1) : '';
    $('telefonoTxt').textContent = d10 ? `(${d10.slice(0, 3)}) ${d10.slice(3, 6)}-${d10.slice(6)}` : (s.Telefono || '');
    $('llamar').href = tel ? 'tel:+' + tel : '#';
    $('whatsapp').href = tel ? 'https://wa.me/' + tel : '#';
    $('llamar').setAttribute('aria-label', 'Llamar a ' + (s.NombreCompleto || 'la Care Partner'));
    $('whatsapp').setAttribute('aria-label', 'Escribir por WhatsApp a ' + (s.NombreCompleto || 'la Care Partner'));
  }

  function iconoMarcador(s) {
    const contenido = s.FotoUrl
      ? `<img src="${encodeURI(s.FotoUrl)}" alt="">`
      : `<span>${iniciales(s.NombreCompleto)}</span>`;
    return L.divIcon({
      className: '', iconSize: [46, 46], iconAnchor: [23, 23],
      html: `<div style="position:relative"><span class="sg-marcador-onda"></span><div class="sg-marcador">${contenido}</div></div>`
    });
  }

  function pintarMapa(s) {
    const hay = s.Estado === 3 && s.Latitud != null && s.Longitud != null;
    $('mapa').hidden = !hay;
    $('sinMapa').hidden = hay;
    $('vivo').hidden = !hay;

    if (!hay) {
      $('sinMapaTxt').textContent = s.Estado === 2
        ? 'Verás su ubicación en cuanto empiece el servicio.'
        : s.Estado === 4 || s.Estado === 7 ? 'El servicio terminó: la ubicación ya no se comparte.'
        : 'La ubicación se comparte mientras el servicio está en curso.';
      if (mapa) { mapa.remove(); mapa = null; marcador = null; centrado = false; }
      return;
    }

    const punto = [s.Latitud, s.Longitud];
    if (!mapa) {
      mapa = L.map('mapa', { zoomControl: false, attributionControl: true }).setView(punto, 16);
      L.control.zoom({ position: 'bottomright' }).addTo(mapa);
      capa = L.tileLayer(mosaicos(), opcionesMosaicos()).addTo(mapa);
      marcador = L.marker(punto, { icon: iconoMarcador(s), keyboard: false }).addTo(mapa);
      centrado = true;
      // Si la familia mueve el mapa, deja de recentrarse solo en cada actualización.
      mapa.on('dragstart', () => { centrado = false; });
    } else {
      marcador.setLatLng(punto);
      if (centrado) mapa.panTo(punto, { animate: true });
    }
  }

  function pintarActualizacion(s) {
    const d = fecha(s.UltimaUbicacion);
    const vivo = $('vivo');
    if (d && s.Estado === 3) {
      const viejo = Date.now() - d > 3 * 60000;
      vivo.classList.toggle('es-viejo', viejo);
      vivo.querySelector('span').textContent = viejo ? 'Sin señal' : 'En vivo';
      $('actualizacion').textContent = 'Actualizado ' + hace(d);
    } else {
      $('actualizacion').textContent = '';
    }
    const b = $('bateria');
    if (s.Bateria != null && s.Estado === 3) {
      const n = Math.max(0, Math.min(100, Math.round(s.Bateria)));
      b.hidden = false;
      b.classList.toggle('es-baja', n <= 20);
      b.innerHTML = `<b><span style="width:${n}%"></span></b>${n}%`;
      b.title = 'Batería del teléfono de la Care Partner';
    } else b.hidden = true;
  }

  // Tiempo trabajando (en vivo) y PIN de salida (oculto hasta que lo toquen).
  function pintarDatos(s) {
    const inicio = fecha(s.FechaInicioReal), fin = fecha(s.FinEsperado), terminado = fecha(s.FechaFin);
    if (inicio) {
      const hasta = terminado || (s.Estado === 3 ? new Date() : null) || inicio;
      $('tiempoTrabajando').textContent = duracion(hasta - inicio);
      $('tiempoDetalle').textContent = 'Desde las ' + hora(inicio) + (fin ? ' · hasta las ' + hora(fin) : '');
    } else {
      const previsto = fecha(s.InicioEsperado);
      $('tiempoTrabajando').textContent = '—';
      $('tiempoDetalle').textContent = previsto ? 'Empieza a las ' + hora(previsto) : 'Aún no empieza';
    }
    const pin = s.PinSalida ? String(s.PinSalida) : '';
    $('tarjetaPin').hidden = !pin;
    $('pinTxt').textContent = pin ? (pinVisible ? pin.split('').join(' ') : '• • • •') : '';
    $('pin').setAttribute('aria-label', pinVisible ? 'Ocultar el PIN de salida' : 'Mostrar el PIN de salida');
    $('pin').dataset.pin = pin;
  }

  $('pin').addEventListener('click', () => {
    pinVisible = !pinVisible;
    if (ultimo) pintarDatos(ultimo);
  });

  // ---------- SOS: llamar al 9-1-1 o avisar al centro de mando de CuidaApp
  const ventana = $('sosVentana');
  const abrirSos = () => { ventana.hidden = false; $('sosAvisar').focus(); };
  const cerrarSos = () => { ventana.hidden = true; $('btnSos').focus(); };
  $('btnSos').addEventListener('click', abrirSos);
  $('sosCancelar').addEventListener('click', cerrarSos);
  ventana.addEventListener('click', e => { if (e.target === ventana) cerrarSos(); });
  document.addEventListener('keydown', e => { if (e.key === 'Escape' && !ventana.hidden) cerrarSos(); });

  function mostrarSos(texto, error) {
    const p = $('sosEstado');
    p.hidden = false;
    p.textContent = texto;
    p.classList.toggle('es-error', !!error);
  }

  $('sosAvisar').addEventListener('click', async () => {
    const b = $('sosAvisar');
    b.disabled = true; b.textContent = 'Enviando aviso…';
    try {
      const r = await fetch('/seguimiento-sos/' + encodeURIComponent(token), { method: 'POST', cache: 'no-store', credentials: 'omit' });
      const d = await r.json().catch(() => ({}));
      cerrarSos();
      if (r.ok) mostrarSos(d.message || 'Avisamos al equipo de CuidaApp. Te contactarán en breve.');
      else mostrarSos(d.message || 'No se pudo enviar el aviso. Llama al 9-1-1 si es una emergencia.', true);
    } catch {
      cerrarSos();
      mostrarSos('Sin conexión: no se pudo enviar el aviso. Llama al 9-1-1 si es una emergencia.', true);
    } finally {
      b.disabled = false; b.textContent = 'Avisar a CuidaApp';
    }
  });

  function pintarServicio(s) {
    const inicio = fecha(s.FechaInicioReal), fin = fecha(s.FinEsperado);
    $('horario').textContent = fin ? 'Hasta las ' + hora(fin) : '';

    const avance = $('avance');
    if (inicio && fin && s.Estado === 3) {
      const total = fin - inicio, pasado = Date.now() - inicio;
      avance.hidden = false;
      $('barra').style.width = Math.max(2, Math.min(100, pasado / total * 100)) + '%';
      $('transcurrido').textContent = duracion(pasado) + ' trabajando';
      $('restante').textContent = Date.now() < fin ? 'Faltan ' + duracion(fin - Date.now()) : 'Ya pasó la hora prevista';
    } else avance.hidden = true;

    const pasos = [
      ['Solicitud enviada', s.FechaCreacion],
      ['Llegó al domicilio', s.FechaLlegada],
      ['Empezó el servicio', s.FechaInicioReal],
      ['Terminó el servicio', s.FechaFin]
    ];
    const lista = $('timeline');
    lista.replaceChildren();
    const ultimoHecho = pasos.reduce((i, p, n) => p[1] ? n : i, -1);
    pasos.forEach(([texto, v], n) => {
      const li = document.createElement('li');
      li.className = v ? (n === ultimoHecho && s.Estado === 3 ? 'actual' : 'hecho') : 'pendiente';
      const b = document.createElement('b'); b.textContent = texto;
      const sp = document.createElement('span');
      sp.textContent = v ? dia(fecha(v)) : (n === 3 && fin ? 'Previsto ' + hora(fin) : 'Pendiente');
      li.append(b, sp);
      lista.append(li);
    });
  }

  function pintarTareas(d) {
    const tareas = d.tareas || [], actividades = d.actividades || [];
    $('tarjetaTareas').hidden = tareas.length === 0;
    $('tarjetaActividades').hidden = actividades.length === 0;

    const hechas = tareas.filter(t => t.Completada).length;
    $('tareasCuenta').textContent = `${hechas} de ${tareas.length}`;
    const ul = $('tareas');
    ul.replaceChildren();
    for (const t of tareas) {
      const li = document.createElement('li');
      if (t.Completada) li.className = 'hecha';
      li.innerHTML = `<span class="sg-check" aria-hidden="true">${t.Completada ? '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12.5 10 17 19 7"/></svg>' : ''}</span>`;
      const txt = document.createElement('span');
      txt.className = 'sg-tarea-txt';
      txt.textContent = t.Descripcion;
      li.append(txt);
      li.setAttribute('aria-label', (t.Completada ? 'Hecha: ' : 'Pendiente: ') + t.Descripcion);
      ul.append(li);
    }

    const ua = $('actividades');
    ua.replaceChildren();
    for (const a of actividades) {
      const li = document.createElement('li');
      li.textContent = a.Descripcion;
      const f = fecha(a.Fecha || a.FechaCreacion || a.FechaRegistro);
      if (f) { const sm = document.createElement('small'); sm.textContent = dia(f); li.append(sm); }
      ua.append(li);
    }
  }

  async function consultar() {
    try {
      const r = await fetch('/seguimiento-datos/' + encodeURIComponent(token), { cache: 'no-store', credentials: 'omit' });
      if (!r.ok) { mostrarError(); return; }
      const d = await r.json(), s = d.servicio;
      ultimo = s;
      $('cargando').hidden = true;
      $('error').hidden = true;
      $('detalle').hidden = false;
      pintarEstado(s.Estado);
      pintarPersona(s);
      pintarMapa(s);
      pintarActualizacion(s);
      pintarDatos(s);
      pintarServicio(s);
      pintarTareas(d);
      $('btnSos').hidden = ![2, 3].includes(s.Estado);
      if (s.SosPendiente && $('sosEstado').hidden) mostrarSos('Hay un aviso de emergencia abierto: el equipo de CuidaApp ya está atendiendo.');
      if ([2, 3, 7].includes(s.Estado)) timer = setTimeout(consultar, 10000);
    } catch {
      $('actualizacion').textContent = 'Sin conexión. Reintentando…';
      timer = setTimeout(consultar, 10000);
    }
  }

  // El "hace X minutos" se mantiene al día entre consultas.
  setInterval(() => { if (ultimo) { pintarActualizacion(ultimo); pintarDatos(ultimo); } }, 30000);
  oscuro.addEventListener?.('change', () => { if (capa) capa.setUrl(mosaicos()); });
  window.addEventListener('pagehide', () => clearTimeout(timer));
  consultar();
})();
