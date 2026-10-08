// Utilidades del panel: centro de mando (mapa en vivo con Leaflet) y descarga de CSV.
window.cuidPanel = (function () {
    const mapas = {};
    const RADIO_GEOCERCA_M = 300;   // igual que la app (DetalleTrabajoPage.RadioGeocercaKm)
    const VELOCIDAD_KMH = 25;       // estimación urbana para el tiempo de llegada (línea recta)

    function esc(t) {
        return String(t ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    }

    function icono(color, texto, extra) {
        return L.divIcon({
            className: "",
            html: `<div class="pn-marker ${extra || ""}" style="background:${color}">${esc(texto)}</div>`,
            iconSize: [30, 30],
            iconAnchor: [15, 15],
            popupAnchor: [0, -14]
        });
    }

    function iniciales(nombre) {
        return (nombre || "?").split(" ").filter(Boolean).map(p => p[0]).slice(0, 2).join("").toUpperCase();
    }

    function distanciaKm(a, b) {
        const R = 6371, rad = Math.PI / 180;
        const dLat = (b[0] - a[0]) * rad, dLng = (b[1] - a[1]) * rad;
        const h = Math.sin(dLat / 2) ** 2 + Math.cos(a[0] * rad) * Math.cos(b[0] * rad) * Math.sin(dLng / 2) ** 2;
        return 2 * R * Math.asin(Math.sqrt(h));
    }

    // Desliza un marcador a su nueva posición (en lugar de saltar).
    function deslizar(marcador, destino, ms, alMover) {
        const desde = marcador.getLatLng();
        const ini = performance.now();
        cancelAnimationFrame(marcador._anim);
        const paso = ahora => {
            const t = Math.min(1, (ahora - ini) / ms);
            const e = 1 - Math.pow(1 - t, 3);
            const ll = [desde.lat + (destino[0] - desde.lat) * e, desde.lng + (destino[1] - desde.lng) * e];
            marcador.setLatLng(ll);
            if (alMover) alMover(ll);
            if (t < 1) marcador._anim = requestAnimationFrame(paso);
        };
        marcador._anim = requestAnimationFrame(paso);
    }

    function elementos(datos) {
        const lista = [];
        (datos.servicios || []).forEach(s => lista.push({
            capa: "servicios", clave: "s" + s.id, id: s.id, tipo: "servicio", ll: [s.latitud, s.longitud], color: "#0253A5", texto: "S", z: 0,
            tooltip: `${esc(s.tipoServicio)} · ${esc(s.clienteNombre)}`
        }));
        (datos.cuidadores || []).forEach(c => lista.push({
            capa: "cuidadores", clave: "c" + c.usuarioId, id: c.usuarioId, tipo: "persona", ll: [c.latitud, c.longitud],
            color: c.enServicio ? "#0E7490" : "#059669", texto: iniciales(c.nombre), z: 500,
            extra: (c.bateria != null && c.bateria <= 15) ? "bateria-baja" : "",
            tooltip: `${esc(c.nombre)} · ${c.enServicio ? "En servicio" : "Disponible"}${c.bateria != null ? " · batería " + c.bateria + "%" : ""}`
        }));
        (datos.sos || []).forEach(a => lista.push({
            capa: "sos", clave: "sos" + a.id, id: a.id, tipo: "sos", ll: [a.latitud, a.longitud], color: "#DC2626", texto: "!", extra: "sos", z: 1000,
            tooltip: `SOS · ${esc(a.nombre)} (${esc(a.tipoUsuario)})`
        }));
        return lista;
    }

    function crear(id, el, dotnet) {
        const mapa = L.map(el, { zoomControl: true, attributionControl: true }).setView([18.4861, -69.9312], 12);
        L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(mapa);
        const g = {};
        ["calor", "geocercas", "rutas", "estelas", "servicios", "cuidadores", "sos", "dibujo", "replay"].forEach(n => g[n] = L.layerGroup().addTo(mapa));
        const m = { mapa, g, marcadores: {}, rutas: {}, estelas: {}, _contenedor: el, centrado: false, primeraVez: true, dotnet,
                    visibles: { cuidadores: true, servicios: true, sos: true, rutas: true, geocercas: true, estelas: true, calor: false },
                    siguiendo: null, datos: null };
        // Si el usuario arrastra el mapa, deja de seguir.
        mapa.on("dragstart", () => { if (m.siguiendo) { m.siguiendo = null; dotnet?.invokeMethodAsync("DejoDeSeguir"); } });
        mapas[id] = m;
        return m;
    }

    function dibujarRutasYGeocercas(m) {
        m.g.rutas.clearLayers(); m.g.geocercas.clearLayers(); m.rutas = {};
        (m.datos?.servicios || []).forEach(s => {
            const casa = [s.latitud, s.longitud];
            // Geocerca: verde si el Care Partner está dentro del radio, roja si está en curso y fuera.
            const enCurso = s.estado === 3 || s.estado === 7;
            const cuid = s.cuidadorLatitud != null ? [s.cuidadorLatitud, s.cuidadorLongitud] : null;
            const dentro = cuid ? distanciaKm(cuid, casa) * 1000 <= RADIO_GEOCERCA_M : true;
            const color = enCurso && !dentro ? "#DC2626" : "#059669";
            L.circle(casa, { radius: RADIO_GEOCERCA_M, color, weight: 1.5, fillColor: color, fillOpacity: enCurso && !dentro ? .14 : .06, dashArray: enCurso && !dentro ? "4 4" : null })
                .bindTooltip(enCurso && !dentro ? "Fuera de la zona del servicio" : "Zona del servicio (300 m)").addTo(m.g.geocercas);
            // Ruta Care Partner → casa del cliente con tiempo estimado (servicios aceptados que aún no empiezan).
            if (cuid && s.estado === 2) {
                const linea = L.polyline([cuid, casa], { color: "#0253A5", weight: 3, opacity: .75, dashArray: "8 8" }).addTo(m.g.rutas);
                const km = distanciaKm(cuid, casa);
                linea.bindTooltip(`${km.toFixed(1)} km · llegada aprox. ${Math.max(1, Math.round(km / VELOCIDAD_KMH * 60))} min`, { permanent: true, direction: "center", className: "pn-ruta-tip" });
                m.rutas[s.cuidadorId] = { linea, casa };
            }
        });
    }

    function actualizarRuta(m, cuidadorId, ll) {
        const r = m.rutas[cuidadorId];
        if (!r) return;
        r.linea.setLatLngs([ll, r.casa]);
        const km = distanciaKm(ll, r.casa);
        r.linea.setTooltipContent(`${km.toFixed(1)} km · llegada aprox. ${Math.max(1, Math.round(km / VELOCIDAD_KMH * 60))} min`);
    }

    function aplicarVisibilidad(m) {
        Object.entries(m.visibles).forEach(([capa, ver]) => {
            const grupo = m.g[capa];
            if (!grupo) return;
            if (ver && !m.mapa.hasLayer(grupo)) grupo.addTo(m.mapa);
            if (!ver && m.mapa.hasLayer(grupo)) m.mapa.removeLayer(grupo);
        });
    }

    // Punto dentro de polígono (ray casting).
    function dentroDe(pt, poli) {
        let dentro = false;
        for (let i = 0, j = poli.length - 1; i < poli.length; j = i++) {
            const [yi, xi] = [poli[i].lat, poli[i].lng], [yj, xj] = [poli[j].lat, poli[j].lng];
            if (((yi > pt[0]) !== (yj > pt[0])) && (pt[1] < (xj - xi) * (pt[0] - yi) / (yj - yi) + xi)) dentro = !dentro;
        }
        return dentro;
    }

    return {
        // ---------- Mapa principal
        mapa(id, datos, centrar, dotnet) {
            const el = document.getElementById(id);
            if (!el || !window.L) return;
            let m = mapas[id];
            if (!m || m._contenedor !== el) m = crear(id, el, dotnet);
            m.datos = datos;

            const nuevos = elementos(datos);
            const vigentes = new Set(nuevos.map(n => n.clave));

            Object.keys(m.marcadores).forEach(clave => {
                if (vigentes.has(clave)) return;
                const mk = m.marcadores[clave];
                delete m.marcadores[clave];
                mk.getElement()?.querySelector(".pn-marker")?.classList.add("is-saliendo");
                setTimeout(() => mk.remove(), 260);
            });

            nuevos.forEach(n => {
                const firma = n.color + n.texto + (n.extra || "");
                let mk = m.marcadores[n.clave];
                if (!mk) {
                    const extra = (n.extra || "") + (m.primeraVez ? "" : " is-nuevo");
                    mk = L.marker(n.ll, { icon: icono(n.color, n.texto, extra), zIndexOffset: n.z })
                        .bindTooltip(n.tooltip, { direction: "top", offset: [0, -14] })
                        .addTo(m.g[n.capa]);
                    mk.on("click", () => m.dotnet?.invokeMethodAsync("Seleccionar", n.tipo, n.id));
                    mk._firma = firma;
                    m.marcadores[n.clave] = mk;
                    return;
                }
                const actual = mk.getLatLng();
                if (Math.abs(actual.lat - n.ll[0]) > 1e-7 || Math.abs(actual.lng - n.ll[1]) > 1e-7) deslizar(mk, n.ll, 700);
                if (mk._firma !== firma) { mk.setIcon(icono(n.color, n.texto, n.extra)); mk._firma = firma; }
                mk.setTooltipContent(n.tooltip);
            });

            dibujarRutasYGeocercas(m);

            const puntos = nuevos.map(n => n.ll);
            if ((centrar || !m.centrado) && puntos.length) {
                m.mapa.fitBounds(puntos, { padding: [48, 48], maxZoom: 15 });
                m.centrado = true;
            }
            m.primeraVez = false;
            setTimeout(() => m.mapa.invalidateSize(), 50);
        },

        // Ubicación en vivo de un Care Partner (sin recargar todo).
        mover(id, cuidadorId, lat, lng) {
            const m = mapas[id];
            const mk = m?.marcadores["c" + cuidadorId];
            if (!mk) return;
            // Estela: se agrega el punto nuevo.
            const est = m.estelas[cuidadorId];
            if (est) est.addLatLng([lat, lng]);
            deslizar(mk, [lat, lng], 900, ll => actualizarRuta(m, cuidadorId, ll));
            if (m.siguiendo === cuidadorId) m.mapa.panTo([lat, lng], { animate: true, duration: 0.9 });
        },

        capas(id, visibles) {
            const m = mapas[id];
            if (!m) return;
            Object.assign(m.visibles, visibles);
            aplicarVisibilidad(m);
        },

        estelas(id, puntos) {
            const m = mapas[id];
            if (!m) return;
            m.g.estelas.clearLayers(); m.estelas = {};
            const porCuidador = {};
            (puntos || []).forEach(p => (porCuidador[p.cuidadorId] ||= []).push([p.latitud, p.longitud]));
            Object.entries(porCuidador).forEach(([cid, pts]) => {
                if (pts.length < 2) return;
                m.estelas[cid] = L.polyline(pts, { color: "#0E7490", weight: 4, opacity: .45, lineCap: "round", lineJoin: "round" }).addTo(m.g.estelas);
            });
        },

        calor(id, puntos) {
            const m = mapas[id];
            if (!m || !L.heatLayer) return;
            m.g.calor.clearLayers();
            const max = Math.max(1, ...(puntos || []).map(p => p.peso));
            L.heatLayer((puntos || []).map(p => [p.latitud, p.longitud, p.peso / max]), { radius: 28, blur: 22, maxZoom: 14 }).addTo(m.g.calor);
        },

        enfocar(id, lat, lng, zoom) {
            const m = mapas[id];
            if (m) m.mapa.flyTo([lat, lng], zoom || 16, { duration: 0.6 });
        },

        seguir(id, cuidadorId) {
            const m = mapas[id];
            if (!m) return;
            m.siguiendo = cuidadorId;
            const mk = m.marcadores["c" + cuidadorId];
            if (mk) m.mapa.flyTo(mk.getLatLng(), Math.max(m.mapa.getZoom(), 16), { duration: 0.6 });
        },

        dejarDeSeguir(id) { if (mapas[id]) mapas[id].siguiendo = null; },

        resaltar(id, clave) {
            const m = mapas[id];
            Object.values(m?.marcadores || {}).forEach(mk => mk.getElement()?.querySelector(".pn-marker")?.classList.remove("is-elegido"));
            m?.marcadores[clave]?.getElement()?.querySelector(".pn-marker")?.classList.add("is-elegido");
        },

        // ---------- Dibujar un área: clics para marcar puntos, doble clic (o clic en el primero) para cerrar.
        dibujarArea(id) {
            const m = mapas[id];
            if (!m) return;
            m.g.dibujo.clearLayers();
            const puntos = [];
            const linea = L.polyline([], { color: "#7C3AED", weight: 2, dashArray: "6 6" }).addTo(m.g.dibujo);
            m.mapa.doubleClickZoom.disable();
            m._contenedor.classList.add("pn-dibujando");
            const terminar = () => {
                m.mapa.off("click", alClic); m.mapa.off("dblclick", terminar);
                m.mapa.doubleClickZoom.enable();
                m._contenedor.classList.remove("pn-dibujando");
                if (puntos.length < 3) { m.g.dibujo.clearLayers(); m.dotnet?.invokeMethodAsync("AreaCancelada"); return; }
                m.g.dibujo.clearLayers();
                const poli = L.polygon(puntos, { color: "#7C3AED", weight: 2, fillColor: "#7C3AED", fillOpacity: .12 }).addTo(m.g.dibujo);
                const latlngs = poli.getLatLngs()[0];
                const ids = (m.datos?.cuidadores || []).filter(c => dentroDe([c.latitud, c.longitud], latlngs)).map(c => c.usuarioId);
                m.dotnet?.invokeMethodAsync("AreaDibujada", ids);
            };
            const alClic = e => {
                if (puntos.length >= 3 && m.mapa.latLngToContainerPoint(puntos[0]).distanceTo(e.containerPoint) < 14) { terminar(); return; }
                puntos.push(e.latlng);
                linea.setLatLngs(puntos);
                L.circleMarker(e.latlng, { radius: 4, color: "#7C3AED", fillOpacity: 1 }).addTo(m.g.dibujo);
            };
            m.mapa.on("click", alClic);
            m.mapa.on("dblclick", terminar);
            m._terminarDibujo = terminar;
        },

        limpiarArea(id) {
            const m = mapas[id];
            if (!m) return;
            m.g.dibujo.clearLayers();
        },

        // ---------- Reproducir un recorrido (servicio o jornada)
        reproducir(id, puntos, dotnet) {
            const m = mapas[id];
            if (!m) return;
            this.detenerReproduccion(id);
            if (!puntos || puntos.length < 2) return;
            const ll = puntos.map(p => [p.latitud, p.longitud]);
            const tiempos = puntos.map(p => new Date(p.fecha).getTime());
            const ruta = L.polyline(ll, { color: "#94A3B8", weight: 4, opacity: .7 }).addTo(m.g.replay);
            const hecho = L.polyline([ll[0]], { color: "#7C3AED", weight: 5 }).addTo(m.g.replay);
            L.circleMarker(ll[0], { radius: 6, color: "#059669", fillOpacity: 1 }).bindTooltip("Inicio").addTo(m.g.replay);
            L.circleMarker(ll[ll.length - 1], { radius: 6, color: "#DC2626", fillOpacity: 1 }).bindTooltip("Fin").addTo(m.g.replay);
            const mk = L.marker(ll[0], { icon: icono("#7C3AED", "▶"), zIndexOffset: 2000 }).addTo(m.g.replay);
            m.mapa.fitBounds(ruta.getBounds(), { padding: [60, 60] });
            const total = tiempos[tiempos.length - 1] - tiempos[0] || 1;
            const r = m.replay = { ll, tiempos, total, hecho, mk, prog: 0, vel: 60, jugando: true, ultimo: performance.now(), dotnet };
            const paso = ahora => {
                if (m.replay !== r) return;
                const dt = ahora - r.ultimo; r.ultimo = ahora;
                if (r.jugando) r.prog = Math.min(1, r.prog + (dt * r.vel) / total);
                this._pintarReplay(r);
                if (r.prog >= 1) r.jugando = false;
                r.raf = requestAnimationFrame(paso);
            };
            r.raf = requestAnimationFrame(paso);
        },

        _pintarReplay(r) {
            const t = r.tiempos[0] + r.prog * r.total;
            let i = r.tiempos.findIndex(x => x >= t);
            if (i === -1) i = r.tiempos.length - 1;
            if (i <= 0) i = 1;
            const f = (t - r.tiempos[i - 1]) / Math.max(1, r.tiempos[i] - r.tiempos[i - 1]);
            const a = r.ll[i - 1], b = r.ll[i];
            const pos = [a[0] + (b[0] - a[0]) * Math.min(1, Math.max(0, f)), a[1] + (b[1] - a[1]) * Math.min(1, Math.max(0, f))];
            r.mk.setLatLng(pos);
            r.hecho.setLatLngs(r.ll.slice(0, i).concat([pos]));
            const ahora = performance.now();
            if (!r._aviso || ahora - r._aviso > 200) {
                r._aviso = ahora;
                r.dotnet?.invokeMethodAsync("ProgresoReplay", r.prog, new Date(t).toISOString(), r.jugando);
            }
        },

        controlReproduccion(id, accion, valor) {
            const r = mapas[id]?.replay;
            if (!r) return;
            if (accion === "play") { if (r.prog >= 1) r.prog = 0; r.jugando = true; }
            if (accion === "pausa") r.jugando = false;
            if (accion === "velocidad") r.vel = valor;
            if (accion === "ir") { r.prog = Math.min(1, Math.max(0, valor)); this._pintarReplay(r); }
        },

        detenerReproduccion(id) {
            const m = mapas[id];
            if (!m?.replay) return;
            cancelAnimationFrame(m.replay.raf);
            m.replay = null;
            m.g.replay.clearLayers();
        },

        // ---------- Varios
        pantallaCompleta(selector) {
            const el = document.querySelector(selector);
            if (!el) return;
            if (document.fullscreenElement) document.exitFullscreen();
            else el.requestFullscreen?.();
            setTimeout(() => Object.values(mapas).forEach(m => m.mapa.invalidateSize()), 300);
        },

        // Tono corto de alerta (Web Audio, sin archivos). Los navegadores lo permiten tras la primera interacción.
        sonar(tipo) {
            try {
                const ctx = this._audio ||= new (window.AudioContext || window.webkitAudioContext)();
                const notas = tipo === "sos" ? [880, 660, 880, 660] : [740, 990];
                notas.forEach((f, k) => {
                    const o = ctx.createOscillator(), g = ctx.createGain();
                    o.type = "sine"; o.frequency.value = f;
                    g.gain.setValueAtTime(0.0001, ctx.currentTime + k * 0.18);
                    g.gain.exponentialRampToValueAtTime(0.25, ctx.currentTime + k * 0.18 + 0.02);
                    g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + k * 0.18 + 0.16);
                    o.connect(g).connect(ctx.destination);
                    o.start(ctx.currentTime + k * 0.18); o.stop(ctx.currentTime + k * 0.18 + 0.17);
                });
            } catch { }
        },

        destruir(id) {
            const m = mapas[id];
            if (!m) return;
            this.detenerReproduccion(id);
            m.mapa.remove();
            delete mapas[id];
        },

        descargar(nombre, contenido, tipo) {
            const blob = new Blob(["﻿" + contenido], { type: tipo || "text/csv;charset=utf-8" });
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url; a.download = nombre;
            document.body.appendChild(a); a.click(); a.remove();
            setTimeout(() => URL.revokeObjectURL(url), 1000);
        }
    };
})();
