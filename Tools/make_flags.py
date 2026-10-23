# Рисует упрощённые флаги стран: Resources/Flags/<код>.png (120x80, код — ISO 2 буквы в нижнем регистре).
# В roster.json у игрока поле "country": "KR". Свой флаг можно просто положить PNG-файлом с нужным кодом.
import os, math
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'Assets', 'EsportsSim', 'Resources', 'Flags')
W, H, K = 120, 80, 4   # итоговый размер и суперсэмплинг
w, h = W * K, H * K

def new(bg='#FFFFFF'):
    im = Image.new('RGB', (w, h), bg); return im, ImageDraw.Draw(im)

def hstripes(*cols, weights=None):
    im, d = new(); weights = weights or [1] * len(cols); tot = sum(weights); y = 0
    for c, wt in zip(cols, weights):
        y2 = y + h * wt / tot; d.rectangle((0, y, w, y2), fill=c); y = y2
    return im, d

def vstripes(*cols):
    im, d = new(); n = len(cols)
    for i, c in enumerate(cols): d.rectangle((w * i / n, 0, w * (i + 1) / n, h), fill=c)
    return im, d

def star(d, cx, cy, r, fill, rot=-90):
    pts = []
    for i in range(10):
        rr = r if i % 2 == 0 else r * 0.4
        a = math.radians(rot + i * 36)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    d.polygon(pts, fill=fill)

def nordic(bg, cross, inner=None):
    im, d = new(bg); t = h * 0.2; x = w * 0.36
    d.rectangle((x - t / 2, 0, x + t / 2, h), fill=cross); d.rectangle((0, h / 2 - t / 2, w, h / 2 + t / 2), fill=cross)
    if inner:
        t2 = t * 0.5
        d.rectangle((x - t2 / 2, 0, x + t2 / 2, h), fill=inner); d.rectangle((0, h / 2 - t2 / 2, w, h / 2 + t2 / 2), fill=inner)
    return im

def kr():
    im, d = new('#FFFFFF'); cx, cy, r = w / 2, h / 2, h * 0.25
    d.pieslice((cx - r, cy - r, cx + r, cy + r), 180 + 34, 34, fill='#CD2E3A')
    d.pieslice((cx - r, cy - r, cx + r, cy + r), 34, 180 + 34, fill='#0047A0')
    a = math.radians(34); dx, dy = r / 2 * math.cos(a), r / 2 * math.sin(a)
    d.ellipse((cx - dx - r / 2, cy - dy - r / 2, cx - dx + r / 2, cy - dy + r / 2), fill='#CD2E3A')
    d.ellipse((cx + dx - r / 2, cy + dy - r / 2, cx + dx + r / 2, cy + dy + r / 2), fill='#0047A0')
    for (px, py, ang) in [(0.2, 0.22, 34), (0.8, 0.78, 34), (0.8, 0.22, -34), (0.2, 0.78, -34)]:
        bar = Image.new('RGBA', (int(h * 0.22), int(h * 0.2)), (0, 0, 0, 0)); bd = ImageDraw.Draw(bar)
        for k in range(3): bd.rectangle((0, k * h * 0.075, h * 0.22, k * h * 0.075 + h * 0.045), fill='#000000')
        bar = bar.rotate(ang if px < 0.5 else -ang, expand=True)
        im.paste(bar, (int(w * px - bar.size[0] / 2), int(h * py - bar.size[1] / 2)), bar)
    return im

def us():
    im, d = hstripes(*(['#B22234', '#FFFFFF'] * 6 + ['#B22234']))
    d.rectangle((0, 0, w * 0.4, h * 7 / 13), fill='#3C3B6E')
    for i in range(5):
        for j in range(6): d.ellipse((w * 0.4 * (j + 0.5) / 6 - 5, h * 7 / 13 * (i + 0.5) / 5 - 5, w * 0.4 * (j + 0.5) / 6 + 5, h * 7 / 13 * (i + 0.5) / 5 + 5), fill='#FFFFFF')
    return im

def cn():
    im, d = new('#EE1C25'); star(d, w * 0.17, h * 0.27, h * 0.15, '#FFFF00')
    for (x, y) in [(0.33, 0.1), (0.4, 0.2), (0.4, 0.35), (0.33, 0.45)]: star(d, w * x, h * y, h * 0.05, '#FFFF00')
    return im

def tw():
    im, d = new('#FE0000'); d.rectangle((0, 0, w / 2, h / 2), fill='#000095')
    cx, cy = w / 4, h / 4
    for i in range(12):
        a = math.radians(i * 30); d.polygon([(cx + h * 0.17 * math.cos(a), cy + h * 0.17 * math.sin(a)),
                                              (cx + h * 0.09 * math.cos(a + 0.26), cy + h * 0.09 * math.sin(a + 0.26)),
                                              (cx + h * 0.09 * math.cos(a - 0.26), cy + h * 0.09 * math.sin(a - 0.26))], fill='#FFFFFF')
    d.ellipse((cx - h * 0.1, cy - h * 0.1, cx + h * 0.1, cy + h * 0.1), fill='#FFFFFF')
    return im

def jp():
    im, d = new('#FFFFFF'); r = h * 0.3; d.ellipse((w / 2 - r, h / 2 - r, w / 2 + r, h / 2 + r), fill='#BC002D'); return im

def vn():
    im, d = new('#DA251D'); star(d, w / 2, h / 2, h * 0.3, '#FFFF00'); return im

def tr():
    im, d = new('#E30A17'); r = h * 0.25
    d.ellipse((w * 0.36 - r, h / 2 - r, w * 0.36 + r, h / 2 + r), fill='#FFFFFF')
    r2 = r * 0.8; d.ellipse((w * 0.39 - r2, h / 2 - r2, w * 0.39 + r2, h / 2 + r2), fill='#E30A17')
    star(d, w * 0.53, h / 2, h * 0.12, '#FFFFFF', rot=180); return im

def br():
    im, d = new('#009C3B'); d.polygon([(w * 0.08, h / 2), (w / 2, h * 0.1), (w * 0.92, h / 2), (w / 2, h * 0.9)], fill='#FFDF00')
    r = h * 0.22; d.ellipse((w / 2 - r, h / 2 - r, w / 2 + r, h / 2 + r), fill='#002776'); return im

def gb():
    im, d = new('#012169')
    d.line((0, 0, w, h), fill='#FFFFFF', width=int(h * 0.2)); d.line((0, h, w, 0), fill='#FFFFFF', width=int(h * 0.2))
    d.line((0, 0, w, h), fill='#C8102E', width=int(h * 0.07)); d.line((0, h, w, 0), fill='#C8102E', width=int(h * 0.07))
    d.rectangle((w / 2 - h * 0.17, 0, w / 2 + h * 0.17, h), fill='#FFFFFF'); d.rectangle((0, h / 2 - h * 0.17, w, h / 2 + h * 0.17), fill='#FFFFFF')
    d.rectangle((w / 2 - h * 0.1, 0, w / 2 + h * 0.1, h), fill='#C8102E'); d.rectangle((0, h / 2 - h * 0.1, w, h / 2 + h * 0.1), fill='#C8102E')
    return im

def ca():
    im, d = new('#FFFFFF'); d.rectangle((0, 0, w * 0.25, h), fill='#D52B1E'); d.rectangle((w * 0.75, 0, w, h), fill='#D52B1E')
    # кленовый лист: точки в долях от центральной полосы (x: -1..1, y: -1..1 вниз)
    leaf = [(0, -1), (0.12, -0.72), (0.3, -0.8), (0.24, -0.3), (0.55, -0.6), (0.62, -0.45), (0.9, -0.5), (0.8, -0.22),
            (0.92, -0.12), (0.5, 0.25), (0.55, 0.42), (0.06, 0.36), (0.06, 0.8), (-0.06, 0.8), (-0.06, 0.36),
            (-0.55, 0.42), (-0.5, 0.25), (-0.92, -0.12), (-0.8, -0.22), (-0.9, -0.5), (-0.62, -0.45), (-0.55, -0.6),
            (-0.24, -0.3), (-0.3, -0.8), (-0.12, -0.72)]
    cx, cy, r = w / 2, h * 0.48, h * 0.38
    d.polygon([(cx + x * r, cy + y * r) for x, y in leaf], fill='#D52B1E')
    return im

def au():
    im, d = new('#00008B'); sub = gb().resize((int(w / 2), int(h / 2))); im.paste(sub, (0, 0)); d = ImageDraw.Draw(im)
    star(d, w * 0.25, h * 0.75, h * 0.12, '#FFFFFF')
    for (x, y) in [(0.75, 0.2), (0.62, 0.45), (0.88, 0.4), (0.75, 0.82)]: star(d, w * x, h * y, h * 0.06, '#FFFFFF')
    return im

def kz():
    im, d = new('#00AFCA'); r = h * 0.18; d.ellipse((w / 2 - r, h * 0.42 - r, w / 2 + r, h * 0.42 + r), fill='#FEC50C')
    d.rectangle((w * 0.05, h * 0.1, w * 0.1, h * 0.9), fill='#FEC50C'); return im

def by():
    im, d = hstripes('#C8313E', '#4AA657', weights=[2, 1]); d.rectangle((0, 0, w * 0.1, h), fill='#FFFFFF')
    for k in range(8): d.rectangle((w * 0.02, h * k / 8 + h * 0.03, w * 0.08, h * k / 8 + h * 0.09), fill='#C8313E')
    return im

def il():
    im, d = new('#FFFFFF'); d.rectangle((0, h * 0.1, w, h * 0.25), fill='#0038B8'); d.rectangle((0, h * 0.75, w, h * 0.9), fill='#0038B8')
    r = h * 0.17; cx, cy = w / 2, h / 2
    d.polygon([(cx, cy - r), (cx + r * 0.87, cy + r / 2), (cx - r * 0.87, cy + r / 2)], outline='#0038B8', width=3 * K)
    d.polygon([(cx, cy + r), (cx + r * 0.87, cy - r / 2), (cx - r * 0.87, cy - r / 2)], outline='#0038B8', width=3 * K)
    return im

def cl():
    im, d = hstripes('#FFFFFF', '#D52B1E'); d.rectangle((0, 0, h / 2, h / 2), fill='#0039A6')
    star(d, h / 4, h / 4, h * 0.12, '#FFFFFF'); return im

def md():
    im, d = vstripes('#0046AE', '#FFD200', '#CC092F')
    # упрощённый герб: коричневый щит с жёлтой окантовкой
    cx, cy, r = w / 2, h / 2, h * 0.2
    d.polygon([(cx - r, cy - r), (cx + r, cy - r), (cx + r, cy + r * 0.3), (cx, cy + r * 1.1), (cx - r, cy + r * 0.3)], fill='#8A5A22')
    d.polygon([(cx - r * 0.6, cy - r * 0.6), (cx + r * 0.6, cy - r * 0.6), (cx + r * 0.6, cy + r * 0.15), (cx, cy + r * 0.7), (cx - r * 0.6, cy + r * 0.15)], fill='#CC092F')
    return im

def uz():
    im, d = hstripes('#0099B5', '#CE1126', '#FFFFFF', '#CE1126', '#1EB53A', weights=[10, 1, 10, 1, 10])
    r = h * 0.12; cx, cy = w * 0.13, h * 0.17
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill='#FFFFFF')
    d.ellipse((cx - r + r * 0.4, cy - r, cx + r + r * 0.4, cy + r), fill='#0099B5')
    for i in range(3):
        for j in range(3): d.ellipse((w * (0.25 + i * 0.07) - 3 * K, h * (0.08 + j * 0.09) - 3 * K, w * (0.25 + i * 0.07) + 3 * K, h * (0.08 + j * 0.09) + 3 * K), fill='#FFFFFF')
    return im

def sk():
    im, d = hstripes('#FFFFFF', '#0B4EA2', '#EE1C25')
    # упрощённый герб: красный щит с белым двойным крестом и синими холмами
    x0, x1, y0, y1 = w * 0.18, w * 0.42, h * 0.2, h * 0.82
    d.rounded_rectangle((x0 - 3 * K, y0 - 3 * K, x1 + 3 * K, y1 + 3 * K), radius=10 * K, fill='#FFFFFF')
    d.rounded_rectangle((x0, y0, x1, y1), radius=8 * K, fill='#EE1C25')
    cx = (x0 + x1) / 2; t = 3 * K
    d.rectangle((cx - t, y0 + h * 0.06, cx + t, y1 - h * 0.14), fill='#FFFFFF')
    d.rectangle((cx - w * 0.06, y0 + h * 0.14, cx + w * 0.06, y0 + h * 0.14 + 2 * t), fill='#FFFFFF')
    d.rectangle((cx - w * 0.08, y0 + h * 0.26, cx + w * 0.08, y0 + h * 0.26 + 2 * t), fill='#FFFFFF')
    d.ellipse((x0, y1 - h * 0.18, x1, y1 + h * 0.06), fill='#0B4EA2')
    return im

def sg():
    im, d = hstripes('#EF3340', '#FFFFFF')
    r = h * 0.17; cx, cy = w * 0.2, h * 0.25
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill='#FFFFFF')
    d.ellipse((cx - r + r * 0.35, cy - r, cx + r + r * 0.35, cy + r), fill='#EF3340')
    import math
    for i in range(5):
        a = math.radians(-90 + i * 72)
        star(d, w * 0.31 + h * 0.08 * math.cos(a), cy + h * 0.08 * math.sin(a), h * 0.035, '#FFFFFF')
    return im

def ws():
    im, d = new('#CE1126'); d.rectangle((0, 0, w / 2, h / 2), fill='#002B7F')
    for (x, y, r) in [(0.25, 0.08, 0.06), (0.33, 0.19, 0.06), (0.17, 0.22, 0.06), (0.25, 0.38, 0.07), (0.29, 0.27, 0.035)]:
        star(d, w * x, h * y * 1.0, h * r, '#FFFFFF')
    return im

FLAGS = {
    'ws': ws,
    'sg': sg,
    'sk': sk,
    'uz': uz,
    'md': md,
    'cl': cl,
    'kr': kr, 'us': us, 'cn': cn, 'tw': tw, 'jp': jp, 'vn': vn, 'tr': tr, 'br': br, 'gb': gb, 'ca': ca, 'au': au,
    'kz': kz, 'by': by, 'il': il,
    'ru': lambda: hstripes('#FFFFFF', '#0039A6', '#D52B1E')[0],
    'ua': lambda: hstripes('#0057B7', '#FFD700')[0],
    'de': lambda: hstripes('#000000', '#DD0000', '#FFCE00')[0],
    'fr': lambda: vstripes('#0055A4', '#FFFFFF', '#EF4135')[0],
    'it': lambda: vstripes('#009246', '#FFFFFF', '#CE2B37')[0],
    'be': lambda: vstripes('#000000', '#FDDA24', '#EF3340')[0],
    'ie': lambda: vstripes('#169B62', '#FFFFFF', '#FF883E')[0],
    'ro': lambda: vstripes('#002B7F', '#FCD116', '#CE1126')[0],
    'nl': lambda: hstripes('#AE1C28', '#FFFFFF', '#21468B')[0],
    'pl': lambda: hstripes('#FFFFFF', '#DC143C')[0],
    'es': lambda: hstripes('#AA151B', '#F1BF00', '#AA151B', weights=[1, 2, 1])[0],
    'at': lambda: hstripes('#ED2939', '#FFFFFF', '#ED2939')[0],
    'hu': lambda: hstripes('#CE2939', '#FFFFFF', '#477050')[0],
    'bg': lambda: hstripes('#FFFFFF', '#00966E', '#D62612')[0],
    'lt': lambda: hstripes('#FDB913', '#006A44', '#C1272D')[0],
    'ee': lambda: hstripes('#0072CE', '#000000', '#FFFFFF')[0],
    'lv': lambda: hstripes('#9E3039', '#FFFFFF', '#9E3039', weights=[2, 1, 2])[0],
    'am': lambda: hstripes('#D90012', '#0033A0', '#F2A800')[0],
    'ar': lambda: hstripes('#74ACDF', '#FFFFFF', '#74ACDF')[0],
    'se': lambda: nordic('#006AA7', '#FECC00'),
    'dk': lambda: nordic('#C8102E', '#FFFFFF'),
    'fi': lambda: nordic('#FFFFFF', '#002F6C'),
    'no': lambda: nordic('#BA0C2F', '#FFFFFF', '#00205B'),
    'is': lambda: nordic('#02529C', '#FFFFFF', '#DC1E35'),
}

def main():
    os.makedirs(OUT, exist_ok=True)
    for code, fn in FLAGS.items():
        fn().resize((W, H), Image.LANCZOS).save(os.path.join(OUT, code + '.png'), optimize=True)
    print('flags:', len(FLAGS))

if __name__ == '__main__':
    main()
