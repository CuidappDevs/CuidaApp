"""Generador de animaciones Lottie de marca para la bienvenida de CuidApp.
Construye capas de forma (trazos que se dibujan, rebotes, órbitas, partículas) a partir de los íconos SVG de la app."""
import json, math, os, re, random

SALIDA = r'C:\Users\carlo\source\repos\CUIDAPP_API\CUIDAPP\Resources\Raw\bienvenida'
os.makedirs(SALIDA, exist_ok=True)
W = H = 512
FR = 30

def hexc(h, a=1.0):
    h = h.lstrip('#'); return [int(h[i:i+2], 16) / 255 for i in (0, 2, 4)] + [a]

AZUL, AZUL2, AZUL_SUAVE, VERDE, VERDE_SUAVE, AMBAR, ROJO, BLANCO, CIELO = \
    '#1C4D96', '#3D74CF', '#DCE8F8', '#2E7D32', '#DDF2E3', '#F5B83D', '#E5484D', '#FFFFFF', '#6FAEE8'

# ---------------------------------------------------------------- SVG path -> Lottie
def svg_a_lottie(d, escala=1.0, dx=0.0, dy=0.0):
    tokens = re.findall(r'[MmLlHhVvCcSsQqTtZz]|-?\d*\.?\d+(?:e-?\d+)?', d)
    subs, cur, pos, start, last_c, cmd = [], None, [0, 0], [0, 0], None, None
    i = 0
    def num():
        nonlocal i
        v = float(tokens[i]); i += 1; return v
    def nuevo(p):
        nonlocal cur
        cur = {'v': [p], 'i': [[0, 0]], 'o': [[0, 0]], 'c': False}; subs.append(cur)
    def linea(p):
        cur['v'].append(p); cur['i'].append([0, 0]); cur['o'].append([0, 0])
    def curva(c1, c2, p):
        a = cur['v'][-1]
        cur['o'][-1] = [c1[0] - a[0], c1[1] - a[1]]
        cur['v'].append(p); cur['i'].append([c2[0] - p[0], c2[1] - p[1]]); cur['o'].append([0, 0])
    while i < len(tokens):
        if re.match(r'[A-Za-z]', tokens[i]):
            cmd = tokens[i]; i += 1
        rel = cmd.islower(); C = cmd.upper()
        o = pos if rel else [0, 0]
        if C == 'M':
            p = [o[0] + num(), o[1] + num()]; nuevo(p); pos = p; start = p; cmd = 'l' if rel else 'L'; last_c = None
        elif C == 'L':
            p = [o[0] + num(), o[1] + num()]; linea(p); pos = p; last_c = None
        elif C == 'H':
            x = num(); p = [(pos[0] + x) if rel else x, pos[1]]; linea(p); pos = p; last_c = None
        elif C == 'V':
            y = num(); p = [pos[0], (pos[1] + y) if rel else y]; linea(p); pos = p; last_c = None
        elif C == 'C':
            c1 = [o[0] + num(), o[1] + num()]; c2 = [o[0] + num(), o[1] + num()]; p = [o[0] + num(), o[1] + num()]
            curva(c1, c2, p); pos = p; last_c = c2
        elif C == 'S':
            c1 = [2 * pos[0] - last_c[0], 2 * pos[1] - last_c[1]] if last_c else pos[:]
            c2 = [o[0] + num(), o[1] + num()]; p = [o[0] + num(), o[1] + num()]
            curva(c1, c2, p); pos = p; last_c = c2
        elif C == 'Q':
            q = [o[0] + num(), o[1] + num()]; p = [o[0] + num(), o[1] + num()]
            c1 = [pos[0] + 2 / 3 * (q[0] - pos[0]), pos[1] + 2 / 3 * (q[1] - pos[1])]
            c2 = [p[0] + 2 / 3 * (q[0] - p[0]), p[1] + 2 / 3 * (q[1] - p[1])]
            curva(c1, c2, p); pos = p; last_c = None
        elif C == 'Z':
            if cur:
                v0, vl = cur['v'][0], cur['v'][-1]
                if abs(v0[0] - vl[0]) < 1e-6 and abs(v0[1] - vl[1]) < 1e-6 and len(cur['v']) > 1:
                    cur['i'][0] = cur['i'][-1]; cur['v'].pop(); cur['i'].pop(); cur['o'].pop()
                cur['c'] = True
            pos = start; last_c = None
        else:
            i += 1
    out = []
    for s in subs:
        out.append({'c': s['c'],
                    'v': [[p[0] * escala + dx, p[1] * escala + dy] for p in s['v']],
                    'i': [[p[0] * escala, p[1] * escala] for p in s['i']],
                    'o': [[p[0] * escala, p[1] * escala] for p in s['o']]})
    return out

# ---------------------------------------------------------------- keyframes
EASE_OUT = ({'x': [0.22], 'y': [1]}, {'x': [0.36], 'y': [0]})
EASE_IO = ({'x': [0.65], 'y': [0]}, {'x': [0.35], 'y': [1]})
BACK = ({'x': [0.34], 'y': [1.56]}, {'x': [0.64], 'y': [0]})

def kf(pares, ease=EASE_OUT):
    """pares: [(frame, valor)] -> propiedad animada."""
    if len(pares) == 1:
        v = pares[0][1]; return {'a': 0, 'k': v}
    k = []
    for n, (t, v) in enumerate(pares):
        v = v if isinstance(v, list) else [v]
        e = {'t': t, 's': v}
        if n < len(pares) - 1:
            e['i'], e['o'] = ease[0], ease[1]
            # escalar componentes del easing al número de dimensiones
            dim = len(v)
            e['i'] = {'x': ease[0]['x'] * dim, 'y': ease[0]['y'] * dim}
            e['o'] = {'x': ease[1]['x'] * dim, 'y': ease[1]['y'] * dim}
        k.append(e)
    return {'a': 1, 'k': k}

def est(v):
    return {'a': 0, 'k': v}

def tr(p=None, a=None, s=None, r=None, o=None):
    return {'ty': 'tr', 'p': p or est([0, 0]), 'a': a or est([0, 0]), 's': s or est([100, 100]),
            'r': r or est(0), 'o': o or est(100), 'sk': est(0), 'sa': est(0)}

def fill(color):
    c = hexc(color) if isinstance(color, str) else color
    # Lottie ignora el alfa del color: la transparencia va en la opacidad del relleno.
    return {'ty': 'fl', 'c': est(c[:3] + [1]), 'o': est(round(c[3] * 100)), 'r': 1}
def fill_kf(c): return {'ty': 'fl', 'c': c, 'o': est(100), 'r': 1}
def stroke(color, w, cap=2): return {'ty': 'st', 'c': est(hexc(color)), 'o': est(100), 'w': est(w), 'lc': cap, 'lj': 2, 'ml': 4}
def trim(e): return {'ty': 'tm', 's': est(0), 'e': e, 'o': est(0), 'm': 1}

def path_shapes(d, escala, dx, dy):
    return [{'ty': 'sh', 'ks': est(s)} for s in svg_a_lottie(d, escala, dx, dy)]

def elipse(cx, cy, w, h=None, s_kf=None):
    return {'ty': 'el', 'p': est([cx, cy]), 's': s_kf or est([w, h if h else w])}

def rect(cx, cy, w, h, r=0):
    return {'ty': 'rc', 'p': est([cx, cy]), 's': est([w, h]), 'r': est(r)}

def grupo(items, transform=None, nombre='g'):
    return {'ty': 'gr', 'nm': nombre, 'it': items + [transform or tr()]}

_ind = [0]
def capa(shapes, ks=None, ip=0, op=None, nombre='capa'):
    _ind[0] += 1
    # Se escriben de abajo hacia arriba; en Lottie lo primero de la lista se dibuja encima.
    shapes = list(reversed(shapes))
    return {'ddd': 0, 'ind': _ind[0], 'ty': 4, 'nm': nombre, 'sr': 1,
            'ks': ks or {'o': est(100), 'r': est(0), 'p': est([0, 0, 0]), 'a': est([0, 0, 0]), 's': est([100, 100, 100])},
            'ao': 0, 'shapes': shapes, 'ip': ip, 'op': op if op is not None else 9999, 'st': 0, 'bm': 0}

def ks(p=None, a=None, s=None, r=None, o=None):
    return {'o': o or est(100), 'r': r or est(0), 'p': p or est([0, 0, 0]), 'a': a or est([0, 0, 0]), 's': s or est([100, 100, 100])}

def animacion(nombre, capas, duracion):
    _ind[0] = 0
    datos = {'v': '5.7.0', 'fr': FR, 'ip': 0, 'op': duracion, 'w': W, 'h': H, 'nm': nombre, 'ddd': 0, 'assets': [],
             'layers': list(reversed(capas))}  # primera de la lista = fondo
    for n, c in enumerate(datos['layers']):
        c['ind'] = n + 1
        c['op'] = duracion
    open(os.path.join(SALIDA, nombre + '.json'), 'w', encoding='utf-8').write(json.dumps(datos, separators=(',', ':')))

# ---------------------------------------------------------------- piezas reutilizables
def pop(t0, dur=14, desde=0, hasta=100, sobre=112):
    """Escala con rebote: 0 -> sobre -> hasta."""
    return kf([(t0, [desde, desde, 100]), (t0 + dur * 0.65, [sobre, sobre, 100]), (t0 + dur, [hasta, hasta, 100])])

def halo(cx, cy, r, color, t0, periodo, total):
    """Anillos que se expanden y desvanecen en bucle (sensación de vida)."""
    capas = []
    for k in range(2):
        ti = t0 + k * periodo / 2
        frames_s, frames_o = [], []
        t = ti
        while t < total:
            frames_s += [(t, [60, 60, 100]), (t + periodo, [150, 150, 100])]
            frames_o += [(t, 55), (t + periodo, 0)]
            t += periodo
        if not frames_s:
            continue
        capas.append(capa([grupo([elipse(0, 0, r * 2), fill(color)])],
                          ks(p=est([cx, cy, 0]), s=kf(frames_s, EASE_OUT), o=kf(frames_o, EASE_OUT)), nombre='halo'))
    return capas

def disco(cx, cy, r, color, t0=0, nombre='disco'):
    return capa([grupo([elipse(0, 0, r * 2), fill(color)])], ks(p=est([cx, cy, 0]), s=pop(t0, 16)), nombre=nombre)

def icono(d, cx, cy, tam, color, t0=0, nombre='icono', rot=None):
    esc = tam / 24
    shapes = path_shapes(d, esc, -12 * esc, -12 * esc) + [fill(color)]
    return capa([grupo(shapes)], ks(p=est([cx, cy, 0]), s=pop(t0, 16), r=rot or est(0)), nombre=nombre)

def flotar(cx, cy, amp, periodo, total, fase=0):
    frames, t, arriba = [], fase, True
    frames.append((0, [cx, cy, 0]))
    while t < total:
        frames.append((t + periodo / 2, [cx, cy - amp if arriba else cy + amp * 0.3, 0]))
        arriba = not arriba; t += periodo / 2
    return kf(frames, EASE_IO)

def confeti(total, n=26, semilla=7, colores=(AZUL2, AMBAR, VERDE, CIELO, ROJO)):
    random.seed(semilla)
    capas = []
    for k in range(n):
        x = random.uniform(40, W - 40); t0 = random.uniform(0, 40); dur = random.uniform(55, 85)
        c = random.choice(colores); w, h = random.uniform(10, 18), random.uniform(6, 10)
        giro = random.choice([-1, 1]) * random.uniform(300, 720)
        capas.append(capa([grupo([rect(0, 0, w, h, 2), fill(c)])],
                          ks(p=kf([(t0, [x, -30, 0]), (t0 + dur, [x + random.uniform(-60, 60), H + 40, 0])], EASE_IO),
                             r=kf([(t0, 0), (t0 + dur, giro)], EASE_IO),
                             o=kf([(t0, 100), (t0 + dur * 0.8, 100), (t0 + dur, 0)])), nombre='confeti'))
    return capas

def destellos(cx, cy, radio, t0, total, n=6, color=AMBAR):
    capas = []
    estrella = "M12 2L14.4 9.6L22 12L14.4 14.4L12 22L9.6 14.4L2 12L9.6 9.6Z"
    for k in range(n):
        ang = 2 * math.pi * k / n + 0.3
        x, y = cx + radio * math.cos(ang), cy + radio * math.sin(ang)
        tk = t0 + k * 4
        esc = 18 / 24
        capas.append(capa([grupo(path_shapes(estrella, esc, -12 * esc, -12 * esc) + [fill(color)])],
                          ks(p=est([x, y, 0]),
                             s=kf([(tk, [0, 0, 100]), (tk + 8, [120, 120, 100]), (tk + 18, [0, 0, 100])]),
                             r=kf([(tk, 0), (tk + 18, 90)])), nombre='destello'))
    return capas

# ---------------------------------------------------------------- íconos (Material, 24x24)
I = {
 'escudo': "M12 1L3 5V11C3 16.55 6.84 21.74 12 23C17.16 21.74 21 16.55 21 11V5L12 1Z",
 'check': "M6 12.5L10.2 16.5L18 8.5",
 'casa': "M10 20V14H14V20H19V12H22L12 3L2 12H5V20H10Z",
 'pin': "M12 2C8.13 2 5 5.13 5 9C5 14.25 12 22 12 22S19 14.25 19 9C19 5.13 15.87 2 12 2ZM12 11.5C10.62 11.5 9.5 10.38 9.5 9S10.62 6.5 12 6.5 14.5 7.62 14.5 9 13.38 11.5 12 11.5Z",
 'estrella': "M12 17.27L18.18 21L16.54 14.73L22 9.24L15.81 8.62L12 2L8.19 8.62L2 9.24L7.46 14.73L5.82 21L12 17.27Z",
 'limpieza': None, 'ninos': None, 'adulto': None, 'cocina': None,
 'campana': "M12 22C13.1 22 14 21.1 14 20H10C10 21.1 10.89 22 12 22ZM18 16V11C18 7.93 16.36 5.36 13.5 4.68V4C13.5 3.17 12.83 2.5 12 2.5S10.5 3.17 10.5 4V4.68C7.63 5.36 6 7.92 6 11V16L4 18V19H20V18L18 16Z",
 'billetera': "M21 18V19C21 20.1 20.1 21 19 21H5C3.89 21 3 20.1 3 19V5C3 3.9 3.89 3 5 3H19C20.1 3 21 3.9 21 5V6H12C10.89 6 10 6.9 10 8V16C10 17.1 10.89 18 12 18H21ZM12 16H22V8H12V16ZM16 13.5C15.17 13.5 14.5 12.83 14.5 12S15.17 10.5 16 10.5 17.5 11.17 17.5 12 16.83 13.5 16 13.5Z",
 'corazon': "M12 21.35L10.55 20.03C5.4 15.36 2 12.28 2 8.5C2 5.42 4.42 3 7.5 3C9.24 3 10.91 3.81 12 5.09C13.09 3.81 14.76 3 16.5 3C19.58 3 22 5.42 22 8.5C22 12.28 18.6 15.36 13.45 20.04L12 21.35Z",
}
reg = open(r'C:\Users\carlo\source\repos\CUIDAPP_API\CUIDAPP\Views\Registro\RegistroPage.xaml.cs', encoding='utf-8').read()
def serv(clave): return re.search(r'"' + clave + r'"(?: or "[^"]+")? => "([^"]+)"', reg).group(1)
I['limpieza'], I['ninos'], I['adulto'], I['cocina'] = serv('limpieza'), serv('ninos'), serv('adulto_mayor'), serv('cocina')

# Logo CuidApp: 4 ganchos (trazos) en el lienzo 1000x1000 del ícono de notificación
LOGO = ["M65,300 C60,120 160,60 300,60 C370,60 420,90 460,130 L640,310",
        "M325,260 L140,445 C60,525 60,660 140,740 C200,800 260,820 340,820",
        "M881,585 C886,765 786,825 646,825 C576,825 526,795 486,755 L306,575",
        "M621,625 L806,440 C886,360 886,225 806,145 C746,85 686,65 606,65"]

def logo_dibujandose(cx, cy, tam, t0, color=AZUL, grosor=115):
    esc = tam / 1000
    capas = []
    for k, d in enumerate(LOGO):
        tk = t0 + k * 7
        shapes = path_shapes(d, esc, cx - 473 * esc, cy - 442 * esc) + \
                 [trim(kf([(tk, 0), (tk + 22, 100)], EASE_IO)), stroke(color, grosor * esc, cap=1)]
        capas.append(capa([grupo(shapes)], nombre=f'logo{k}'))
    return capas

# ================================================================= ANIMACIONES
T = 120  # 4 s en bucle

def a_bienvenida(nombre, con_confeti):
    c = []
    c += halo(256, 256, 150, AZUL_SUAVE, 30, 60, T)
    c.append(disco(256, 256, 150, BLANCO, 0))
    c += logo_dibujandose(256, 250, 210, 10)
    c += destellos(256, 256, 190, 45, T)
    if con_confeti:
        c += confeti(T)
    animacion(nombre, c, T)

def a_servicios():
    c = []
    c += halo(256, 256, 70, AZUL_SUAVE, 20, 60, T)
    c.append(disco(256, 256, 70, AZUL, 0))
    c.append(icono(I['casa'], 256, 256, 64, BLANCO, 6, 'casa'))
    claves = ['limpieza', 'ninos', 'adulto', 'cocina']
    for k, cl in enumerate(claves):
        ang0 = 360 * k / 4
        # órbita: grupo rotando alrededor del centro, ícono contra-rotando para mantenerse derecho
        esc = 44 / 24
        burbuja = grupo([elipse(0, 0, 92), fill(BLANCO)], tr(p=est([0, -165])))
        ic = grupo(path_shapes(I[cl], esc, -12 * esc, -12 * esc) + [fill(AZUL)],
                   tr(p=est([0, -165]), r=kf([(0, -ang0), (T, -ang0 - 360)], ({'x': [0], 'y': [0]}, {'x': [1], 'y': [1]}))))
        sombra = grupo([elipse(0, 4, 92), fill(hexc('#0A2F41', 0.10))], tr(p=est([0, -165])))
        t0 = 8 + k * 6
        c.append(capa([sombra, burbuja, ic],
                      ks(p=est([256, 256, 0]), r=kf([(0, ang0), (T, ang0 + 360)], ({'x': [0], 'y': [0]}, {'x': [1], 'y': [1]})),
                         s=pop(t0, 16)), nombre=f'orbita_{cl}'))
    animacion('servicios', c, T)

def a_verificado():
    c = []
    c += halo(256, 256, 160, VERDE_SUAVE, 40, 60, T)
    esc = 260 / 24
    off = -12 * esc
    contorno = grupo(path_shapes(I['escudo'], esc, off, off) + [trim(kf([(0, 0), (28, 100)], EASE_IO)), stroke(AZUL, 14)])
    relleno = capa([grupo(path_shapes(I['escudo'], esc, off, off) + [fill(AZUL)])],
                   ks(p=est([256, 256, 0]), o=kf([(22, 0), (34, 100)]), s=kf([(22, [92, 92, 100]), (34, [100, 100, 100])])), nombre='relleno')
    c.append(capa([contorno], ks(p=est([256, 256, 0])), nombre='contorno'))
    c.append(relleno)
    chk = grupo(path_shapes(I['check'], esc, off, off) + [trim(kf([(34, 0), (50, 100)], EASE_OUT)), stroke(BLANCO, 26)])
    c.append(capa([chk], ks(p=est([256, 256, 0])), nombre='check'))
    c += destellos(256, 256, 175, 50, T, color=VERDE)
    animacion('verificado', c, T)

def a_seguimiento():
    c = []
    ruta = "M90 400 C150 300 230 420 290 300 C340 200 400 260 420 170"
    c.append(capa([grupo([rect(256, 256, 440, 360, 36), fill(AZUL_SUAVE)])], nombre='mapa'))
    # calles
    for d in ["M50 150 L462 220", "M60 330 L470 300", "M200 80 L240 440", "M360 90 L330 430"]:
        c.append(capa([grupo(path_shapes(d, 1, 0, 0) + [stroke(BLANCO, 16)])], nombre='calle'))
    c.append(capa([grupo(path_shapes(ruta, 1, 0, 0) + [trim(kf([(6, 0), (70, 100)], EASE_IO)), stroke(AZUL, 10)])], nombre='ruta'))
    c.append(icono(I['casa'], 420, 150, 64, AZUL, 4, 'casa'))
    # el pin recorre la ruta (posición por keyframes sobre puntos de la curva)
    pts = [(90, 400), (175, 345), (255, 352), (290, 300), (345, 215), (395, 225), (420, 205)]
    frames = [(6 + n * (64 / (len(pts) - 1)), [x, y - 28, 0]) for n, (x, y) in enumerate(pts)]
    frames.append((T - 14, frames[-1][1])); frames.append((T, [90, 372, 0]))
    esc = 58 / 24
    c.append(capa([grupo(path_shapes(I['pin'], esc, -12 * esc, -12 * esc) + [fill(ROJO)])],
                  ks(p=kf(frames, EASE_IO), s=pop(0, 12)), nombre='pin'))
    # PIN que aparece dígito a dígito
    for k in range(4):
        tk = 74 + k * 6
        c.append(capa([grupo([rect(0, 0, 46, 56, 12), fill(BLANCO)]), grupo([elipse(0, 0, 14), fill(AZUL)], tr(s=pop(tk + 2, 10)))],
                      ks(p=est([166 + k * 60, 470, 0]), s=pop(tk, 12)), nombre=f'pin{k}'))
    animacion('seguimiento', c, T)

def a_califica():
    c = []
    c += halo(256, 230, 120, AZUL_SUAVE, 40, 60, T)
    c.append(disco(256, 230, 120, BLANCO, 0))
    c.append(icono(I['corazon'], 256, 232, 110, ROJO, 8, 'corazon'))
    for k in range(5):
        tk = 18 + k * 7
        c.append(icono(I['estrella'], 96 + k * 80, 420, 58, AMBAR, tk, f'estrella{k}'))
    random.seed(3)
    for k in range(9):  # monedas cayendo
        x = random.uniform(80, 432); t0 = 30 + random.uniform(0, 50); dur = 40
        moneda = [grupo([elipse(0, 0, 34), fill(AMBAR)]), grupo([elipse(0, 0, 20), fill('#FFD27A')])]
        c.append(capa(moneda, ks(p=kf([(t0, [x, -30, 0]), (t0 + dur, [x, 300, 0])], EASE_IO),
                                 s=kf([(t0, [100, 100, 100]), (t0 + dur / 2, [30, 100, 100]), (t0 + dur, [100, 100, 100])], EASE_IO),
                                 o=kf([(t0, 100), (t0 + dur * 0.85, 100), (t0 + dur, 0)])), nombre='moneda'))
    animacion('califica', c, T)

def a_disponible():
    c = []
    c += halo(256, 256, 130, VERDE_SUAVE, 40, 50, T)
    # interruptor: pista que cambia de gris a verde y perilla que se desliza
    pista_color = kf([(18, hexc('#CBD5E1')), (30, hexc(VERDE))], EASE_IO)
    c.append(capa([grupo([rect(0, 0, 300, 150, 75), fill_kf(pista_color)])], ks(p=est([256, 256, 0]), s=pop(0, 14)), nombre='pista'))
    c.append(capa([grupo([elipse(0, 4, 122), fill(hexc('#0A2F41', 0.15))]), grupo([elipse(0, 0, 122), fill(BLANCO)])],
                  ks(p=kf([(18, [181, 256, 0]), (32, [331, 256, 0])], BACK), s=pop(6, 14)), nombre='perilla'))
    esc = 44 / 24
    chk = grupo(path_shapes(I['check'], esc, -12 * esc, -12 * esc) + [trim(kf([(32, 0), (44, 100)])), stroke(VERDE, 8)])
    c.append(capa([chk], ks(p=kf([(18, [181, 256, 0]), (32, [331, 256, 0])], BACK)), nombre='check'))
    animacion('disponible', c, T)

def a_solicitud():
    c = []
    # teléfono
    c.append(capa([grupo([rect(0, 0, 230, 380, 40), fill(AZUL)]), grupo([rect(0, 0, 200, 330, 26), fill(BLANCO)]),
                   grupo([rect(0, -175, 70, 10, 5), fill(AZUL2)])], ks(p=est([256, 270, 0]), s=pop(0, 14)), nombre='telefono'))
    # tarjeta de notificación que cae y rebota
    tarjeta = [grupo([rect(0, 4, 280, 96, 22), fill(hexc('#0A2F41', 0.15))]), grupo([rect(0, 0, 280, 96, 22), fill(BLANCO)]),
               grupo([elipse(-96, 0, 56), fill(AZUL_SUAVE)]),
               grupo([rect(10, -14, 140, 14, 7), fill(AZUL)]), grupo([rect(-10, 12, 100, 12, 6), fill('#CBD5E1')])]
    c.append(capa(tarjeta, ks(p=kf([(14, [256, -80, 0]), (30, [256, 170, 0]), (36, [256, 150, 0]), (42, [256, 165, 0])], EASE_OUT)), nombre='tarjeta'))
    esc = 30 / 24
    c.append(capa([grupo(path_shapes(I['campana'], esc, -12 * esc, -12 * esc) + [fill(AZUL)])],
                  ks(p=kf([(14, [160, -80, 0]), (30, [160, 170, 0]), (36, [160, 150, 0]), (42, [160, 165, 0])], EASE_OUT),
                     r=kf([(42, 0), (46, 18), (50, -16), (54, 10), (58, 0)])), nombre='campana'))
    # insignia que late
    c.append(capa([grupo([elipse(0, 0, 40), fill(ROJO)])],
                  ks(p=est([382, 118, 0]), s=kf([(44, [0, 0, 100]), (52, [120, 120, 100]), (58, [100, 100, 100]),
                                                (80, [100, 100, 100]), (86, [125, 125, 100]), (92, [100, 100, 100])])), nombre='insignia'))
    animacion('solicitud', c, T)

def a_pin():
    c = []
    c.append(capa([grupo([rect(0, 0, 400, 140, 34), fill(AZUL)])], ks(p=est([256, 130, 0]), s=pop(0, 14)), nombre='caja'))
    for k in range(4):
        tk = 20 + k * 10
        c.append(capa([grupo([elipse(0, 0, 40), fill(hexc(BLANCO, 0.25))]), grupo([elipse(0, 0, 40), fill(BLANCO)], tr(s=pop(tk, 10)))],
                      ks(p=est([142 + k * 76, 130, 0])), nombre=f'punto{k}'))
    # teclado: teclas que se "presionan" al ritmo de los puntos
    orden = [(1, 0), (0, 1), (2, 1), (1, 2)]
    for fila in range(3):
        for col in range(3):
            x, y = 156 + col * 100, 270 + fila * 80
            presiones = [20 + n * 10 for n, oc in enumerate(orden) if oc == (col, fila)]
            if presiones:
                t = presiones[0]
                esc = kf([(t - 4, [100, 100, 100]), (t, [86, 86, 100]), (t + 6, [100, 100, 100])])
                color = kf([(t - 4, hexc(AZUL_SUAVE)), (t, hexc(AZUL2)), (t + 8, hexc(AZUL_SUAVE))])
                c.append(capa([grupo([elipse(0, 0, 66), fill_kf(color)])], ks(p=est([x, y, 0]), s=esc), nombre='tecla'))
            else:
                c.append(capa([grupo([elipse(0, 0, 66), fill(AZUL_SUAVE)])], ks(p=est([x, y, 0]), s=pop(4 + col * 2 + fila * 2, 12)), nombre='tecla'))
    animacion('pin', c, T)

def a_cobro():
    c = []
    c += halo(256, 300, 120, VERDE_SUAVE, 50, 60, T)
    esc = 220 / 24
    c.append(capa([grupo(path_shapes(I['billetera'], esc, -12 * esc, -12 * esc) + [fill(VERDE)])],
                  ks(p=est([256, 310, 0]), s=kf([(0, [0, 0, 100]), (12, [110, 110, 100]), (18, [100, 100, 100]),
                                                (40, [100, 100, 100]), (44, [106, 94, 100]), (48, [100, 100, 100]),
                                                (60, [100, 100, 100]), (64, [106, 94, 100]), (68, [100, 100, 100])])), nombre='billetera'))
    for k in range(5):
        t0 = 16 + k * 10; x = 210 + (k % 3) * 45
        moneda = [grupo([elipse(0, 0, 52), fill(AMBAR)]), grupo([elipse(0, 0, 32), fill('#FFD27A')]),
                  grupo([rect(0, 0, 6, 18, 3), fill(AMBAR)])]
        c.append(capa(moneda, ks(p=kf([(t0, [x, -40, 0]), (t0 + 22, [x, 250, 0])], EASE_IO),
                                 r=kf([(t0, 0), (t0 + 22, 200)]),
                                 o=kf([(t0, 100), (t0 + 20, 100), (t0 + 24, 0)])), nombre='moneda'))
    c += destellos(256, 300, 160, 70, T, color=AMBAR)
    animacion('cobro', c, T)

def a_seguridad():
    c = []
    c += halo(256, 256, 120, '#FDE2E2', 0, 40, T)
    c.append(disco(256, 256, 120, ROJO, 0))
    late = kf([(t, [100, 100, 100]) if n % 2 == 0 else (t, [112, 112, 100]) for n, t in enumerate(range(10, T, 10))], EASE_IO)
    texto_sos = [grupo([rect(-46, 0, 18, 70, 9), fill(BLANCO)]), grupo([rect(0, 0, 18, 70, 9), fill(BLANCO)]), grupo([rect(46, 0, 18, 70, 9), fill(BLANCO)])]
    esc = 120 / 24
    c.append(capa([grupo(path_shapes(I['corazon'], esc, -12 * esc, -12 * esc) + [fill(BLANCO)])], ks(p=est([256, 256, 0]), s=late), nombre='corazon'))
    animacion('seguridad', c, T)

def a_ayuda():
    """Centro de ayuda: globo de chat que aparece con puntos "escribiendo" y una respuesta con check."""
    c = []
    c += halo(256, 250, 150, AZUL_SUAVE, 30, 60, T)
    # Globo grande (pregunta) con su colita
    globo = [grupo([rect(0, 0, 300, 190, 60), fill(AZUL)]),
             grupo(path_shapes("M-70 80 L-110 140 L-20 92 Z", 1, 0, 0) + [fill(AZUL)])]
    c.append(capa(globo, ks(p=est([236, 210, 0]), s=pop(0, 16)), nombre='globo'))
    # Tres puntos que rebotan en ola (bucle)
    for k in range(3):
        frames = []
        t = 14 + k * 5
        while t < T:
            frames += [(t, [180 + k * 56, 210, 0]), (t + 7, [180 + k * 56, 188, 0]), (t + 14, [180 + k * 56, 210, 0])]
            t += 30
        c.append(capa([grupo([elipse(0, 0, 34), fill(BLANCO)])],
                      ks(p=kf(frames, EASE_IO), s=pop(10 + k * 3, 12)), nombre=f'punto{k}'))
    # Globo de respuesta con check
    resp = [grupo([elipse(0, 4, 120), fill(hexc('#0A2F41', 0.12))]), grupo([elipse(0, 0, 120), fill(BLANCO)])]
    c.append(capa(resp, ks(p=est([360, 352, 0]), s=pop(34, 16)), nombre='respuesta'))
    esc = 70 / 24
    chk = grupo(path_shapes(I['check'], esc, -12 * esc, -12 * esc) + [trim(kf([(46, 0), (60, 100)], EASE_OUT)), stroke(VERDE, 11)])
    c.append(capa([chk], ks(p=est([360, 352, 0])), nombre='check'))
    c += destellos(300, 280, 200, 58, T, n=5, color=AMBAR)
    animacion('ayuda', c, T)

a_bienvenida('bienvenida_cliente', con_confeti=False)
a_bienvenida('bienvenida_cuidador', con_confeti=True)
a_servicios(); a_verificado(); a_seguimiento(); a_califica()
a_disponible(); a_solicitud(); a_pin(); a_cobro(); a_seguridad(); a_ayuda()
for f in sorted(os.listdir(SALIDA)):
    print(f'{os.path.getsize(os.path.join(SALIDA, f)):>7}  {f}')
