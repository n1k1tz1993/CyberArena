# Рисовалка ЗАГЛУШЕК: игрок в тёмной форме на фоне цвета региона
# и логотипы-заглушки. Используется из make_placeholders.py. Для настоящих игроков не нужна.
import json, os, sys
from PIL import Image, ImageDraw, ImageChops, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, '..', 'Assets', 'EsportsSim', 'Resources')
K = 2            # суперсэмплинг
S = 512          # итоговый размер
W = S * K

def sc(*v): return [x * K for x in v]

def rgb(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))

def shade(h, f):
    return tuple(max(0, min(255, int(c * f))) for c in rgb(h))

def canvas(mode='RGBA'):
    return Image.new(mode, (W, W), (0, 0, 0, 0) if mode == 'RGBA' else 0)

def down(im): return im.resize((S, S), Image.LANCZOS)

# ---------------------------------------------------------------- форма
TORSO = (28, 318, 484, 820)     # плечи/корпус
INNER = (122, 300, 390, 900)    # всё, что вне — рукава (2-й цвет)
VNECK = [(212, 318), (256, 388), (300, 318)]
LOGO_CENTER, LOGO_SIZE = (256, 446), 96   # дублируется в UIKit.cs

def mask_ellipse(box):
    m = canvas('L'); ImageDraw.Draw(m).ellipse(sc(*box), fill=255); return m

def mask_poly(pts, width=0):
    m = canvas('L'); d = ImageDraw.Draw(m)
    p = [tuple(sc(*q)) for q in pts]
    if width: d.line(p, fill=255, width=width * K, joint='curve')
    else: d.polygon(p, fill=255)
    return m

def as_layer(mask, color=(255, 255, 255)):
    im = Image.new('RGBA', (W, W), color + (0,)); im.putalpha(mask); return down(im)

def uniform_layers():
    torso = mask_ellipse(TORSO)
    inner = mask_ellipse(INNER)
    v = mask_poly(VNECK)
    body = ImageChops.subtract(torso, v)
    main = ImageChops.multiply(body, inner)
    second = ImageChops.subtract(body, inner)
    # отделка: окантовка выреза + кант по шву рукава
    trim = mask_poly([(196, 312), (256, 404), (316, 312)], width=16)
    seam = canvas('L'); ImageDraw.Draw(seam).ellipse(sc(*INNER), outline=255, width=9 * K)
    trim = ImageChops.multiply(ImageChops.lighter(trim, seam), body)
    # тень: низ темнее, тень под вырезом
    sh = canvas('L'); d = ImageDraw.Draw(sh)
    for y in range(W):
        t = max(0, (y / K - 380) / 132)
        d.line([(0, y), (W, y)], fill=int(70 * t))
    d.polygon([tuple(sc(*q)) for q in [(222, 330), (256, 400), (290, 330), (256, 372)]], fill=60)
    sh = ImageChops.multiply(sh, body)
    return dict(jersey_main=as_layer(main), jersey_second=as_layer(second),
                jersey_third=as_layer(trim), jersey_shade=as_layer(sh, (0, 0, 0)))

def stripes():
    m = canvas('L'); d = ImageDraw.Draw(m)
    for k in range(-W, W * 2, 90 * K):
        d.polygon([(k, 0), (k + 45 * K, 0), (k + 45 * K - W, W), (k - W, W)], fill=34)
    return as_layer(m)

# ---------------------------------------------------------------- голова
HEADSET = "#16181F"
HEADSET_ACC = "#5A6178"

def head_layer(f):
    im = canvas(); d = ImageDraw.Draw(im)
    skin, hc = f['skin'], f['hairColor']
    bc = f.get('beardColor') or hc
    hair, beard = f.get('hair', 'short'), f.get('beard', 'none')
    dark = shade(skin, 0.82)

    # волосы сзади
    if hair == 'long':
        d.rounded_rectangle(sc(150, 90, 362, 372), radius=90 * K, fill=hc)
    if hair == 'curly':
        d.ellipse(sc(134, 30, 378, 250), fill=hc)
    # шея и уши
    d.rectangle(sc(206, 236, 306, 380), fill=dark)
    d.ellipse(sc(152, 158, 184, 214), fill=skin)
    d.ellipse(sc(328, 158, 360, 214), fill=skin)
    # голова
    d.ellipse(sc(166, 62, 346, 292), fill=skin)
    # борода
    if beard == 'stubble':
        b = Image.new('RGBA', (W, W), (0, 0, 0, 0)); ImageDraw.Draw(b).chord(sc(172, 150, 340, 296), 15, 165, fill=rgb(bc) + (80,))
        im.alpha_composite(b)
    elif beard == 'full':
        d.chord(sc(166, 100, 346, 304), 0, 180, fill=bc)
        d.rectangle(sc(166, 170, 180, 205), fill=bc); d.rectangle(sc(332, 170, 346, 205), fill=bc)
    elif beard == 'goatee':
        d.ellipse(sc(232, 238, 280, 300), fill=bc)
    elif beard == 'chinstrap':
        d.arc(sc(170, 100, 342, 298), 5, 175, fill=bc, width=14 * K)
    if beard in ('full', 'goatee', 'mustache'):
        d.polygon([tuple(sc(*q)) for q in [(226, 228), (256, 222), (286, 228), (280, 240), (256, 234), (232, 240)]], fill=bc)
    # лицо
    eye = f.get('eyes', '#1B1B22')
    d.ellipse(sc(206, 176, 226, 196), fill=eye); d.ellipse(sc(286, 176, 306, 196), fill=eye)
    d.ellipse(sc(212, 179, 218, 185), fill="#FFFFFF"); d.ellipse(sc(292, 179, 298, 185), fill="#FFFFFF")
    d.line(sc(198, 164, 230, 158), fill=hc if hair != 'bald' else dark, width=7 * K)
    d.line(sc(282, 158, 314, 164), fill=hc if hair != 'bald' else dark, width=7 * K)
    d.line(sc(256, 192, 249, 222, 261, 224), fill=dark, width=4 * K, joint='curve')
    mouth_y = 246 if beard not in ('full', 'goatee', 'mustache') else 250
    d.arc(sc(234, mouth_y - 18, 278, mouth_y + 6), 25, 155, fill="#5A2E2E", width=5 * K)
    if f.get('glasses'):
        for x0 in (194, 270):
            d.rounded_rectangle(sc(x0, 166, x0 + 48, 204), radius=10 * K, outline="#111111", width=5 * K)
        d.line(sc(242, 180, 270, 180), fill="#111111", width=5 * K)
    # волосы спереди
    if hair == 'short':
        d.chord(sc(160, 52, 352, 232), 180, 360, fill=hc)
        d.polygon([tuple(sc(*q)) for q in [(176, 142), (230, 118), (300, 126), (338, 142), (330, 112), (180, 112)]], fill=hc)
    elif hair == 'buzz':
        b = Image.new('RGBA', (W, W), (0, 0, 0, 0)); ImageDraw.Draw(b).chord(sc(164, 58, 348, 236), 180, 360, fill=rgb(hc) + (200,))
        im.alpha_composite(b)
    elif hair == 'sidepart':
        d.chord(sc(158, 48, 354, 236), 180, 360, fill=hc)
        d.polygon([tuple(sc(*q)) for q in [(170, 142), (196, 96), (300, 80), (344, 120), (346, 160), (268, 112), (220, 120)]], fill=hc)
    elif hair == 'long':
        d.chord(sc(158, 50, 354, 240), 180, 360, fill=hc)
        d.rectangle(sc(158, 140, 186, 300), fill=hc); d.rectangle(sc(326, 140, 354, 300), fill=hc)
    elif hair == 'mohawk':
        d.rounded_rectangle(sc(234, 22, 278, 150), radius=20 * K, fill=hc)
    elif hair == 'curly':
        for cx in range(176, 346, 30):
            d.ellipse(sc(cx - 24, 70, cx + 24, 130), fill=hc)
    elif hair == 'spiky':
        pts = [(160, 150)]
        for i, x in enumerate(range(166, 352, 26)):
            pts += [(x, 96 if i % 2 else 112), (x + 13, 30 + (i % 3) * 14)]
        pts += [(352, 150), (346, 100), (166, 100)]
        d.chord(sc(160, 54, 352, 232), 180, 360, fill=hc)
        d.polygon([tuple(sc(*q)) for q in pts], fill=hc)
    elif hair == 'bald':
        d.ellipse(sc(206, 80, 250, 106), fill=shade(skin, 1.12))
    # гарнитура
    d.arc(sc(140, 36, 372, 300), 192, 348, fill=HEADSET, width=18 * K)
    d.rounded_rectangle(sc(138, 148, 182, 232), radius=14 * K, fill=HEADSET)
    d.rounded_rectangle(sc(330, 148, 374, 232), radius=14 * K, fill=HEADSET)
    d.rounded_rectangle(sc(146, 162, 174, 218), radius=8 * K, fill=HEADSET_ACC)
    d.line(sc(168, 226, 214, 262), fill=HEADSET, width=8 * K)
    d.ellipse(sc(206, 252, 230, 274), fill=HEADSET)
    return down(im)

# ---------------------------------------------------------------- логотипы-заглушки
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

def logo(team, idx):
    L = 512
    im = Image.new('RGBA', (L, L), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    c1, c2, c3 = team['colors']
    shape = idx % 4
    if shape == 0:   # щит
        p = [(256, 20), (476, 90), (450, 330), (256, 492), (62, 330), (36, 90)]
        d.polygon(p, fill=c3); d.polygon([(256, 56), (440, 116), (418, 316), (256, 452), (94, 316), (72, 116)], fill=c2)
    elif shape == 1:  # круг
        d.ellipse((16, 16, 496, 496), fill=c3); d.ellipse((52, 52, 460, 460), fill=c2)
    elif shape == 2:  # шестиугольник
        import math
        def hexp(r): return [(256 + r * math.cos(math.radians(a)), 256 + r * math.sin(math.radians(a))) for a in range(-90, 270, 60)]
        d.polygon(hexp(246), fill=c3); d.polygon(hexp(204), fill=c2)
    else:  # ромб
        d.polygon([(256, 10), (502, 256), (256, 502), (10, 256)], fill=c3)
        d.polygon([(256, 64), (448, 256), (256, 448), (64, 256)], fill=c2)
    d.polygon([(150, 300), (256, 150), (362, 300)], fill=c1)
    num = str(idx + 1)
    font = ImageFont.truetype(FONT, 150 if len(num) == 1 else 120)
    d.text((256, 330), num, font=font, fill=c3, anchor='mm', stroke_width=10, stroke_fill=c2)
    return im.resize((256, 256), Image.LANCZOS)


def placeholder_player(face, region_color):
    """Заглушка игрока: лицо + тёмная форма на фоне цвета региона."""
    base = rgb(region_color)
    im = Image.new('RGBA', (S, S), tuple(int(c * 0.5) for c in base) + (255,))
    im.alpha_composite(stripes())
    im.alpha_composite(head_layer(face))
    L = uniform_layers()
    for name, col in (('jersey_main', (44, 50, 72)), ('jersey_second', (34, 39, 58)), ('jersey_third', (90, 100, 130))):
        lay = Image.new('RGBA', (S, S), col + (0,)); lay.putalpha(L[name].getchannel('A')); im.alpha_composite(lay)
    im.alpha_composite(L['jersey_shade'])
    return im.convert('RGB')
