"""Verifica que todos los idiomas tengan las mismas claves y los mismos {placeholders} que es.json.

Uso (desde cualquier carpeta):  python CUIDAPP/Resources/Strings/verificar_traducciones.py
Sale con código 1 si falta alguna clave o no coinciden los placeholders.
"""
import glob, json, os, re, sys

carpeta = os.path.dirname(os.path.abspath(__file__))
base = json.load(open(os.path.join(carpeta, 'es.json'), encoding='utf-8'))
holes = lambda t: sorted(re.findall(r'\{\d+', t))
fallos = 0

for ruta in sorted(glob.glob(os.path.join(carpeta, '*.json'))):
    codigo = os.path.basename(ruta)[:-5]
    if codigo == 'es':
        continue
    otro = json.load(open(ruta, encoding='utf-8'))
    faltan = [k for k in base if k not in otro]
    sobran = [k for k in otro if k not in base]
    distintos = [k for k in base if k in otro and holes(base[k]) != holes(otro[k])]
    print(f'[{codigo}] claves: {len(otro)}/{len(base)} | faltan: {len(faltan)} | sobran: {len(sobran)} | placeholders distintos: {len(distintos)}')
    for k in faltan: print('   falta  :', k, '->', base[k][:60])
    for k in sobran: print('   sobra  :', k)
    for k in distintos: print('   {n} !=  :', k)
    fallos += len(faltan) + len(sobran) + len(distintos)

sys.exit(1 if fallos else 0)
