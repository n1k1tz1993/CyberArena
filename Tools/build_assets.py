# Готовит картинки для игры из исходников:
#
#   Tools/source/players/<portrait>.png|jpg|webp   фото/картинка игрока
#   Tools/source/logos/<logo>.png|jpg|webp         логотип команды
#   Tools/source/regions/r1..r5.png                логотип региона (лиги)
#
# Результат:
#   Resources/Portraits/<portrait>.png   квадрат 512x512 (вертикальные картинки обрезаются снизу, чтобы лицо осталось)
#   Resources/Logos/<logo>.png           квадрат 256x256: эмблема логотипа (надпись рядом/снизу отрезается,
#                                        если она отделена пустой полосой; иначе логотип целиком)
#   Resources/Regions/r1..r5.png         то же для логотипов регионов
#   Tools/preview.png                    все игроки с логотипами команд — для проверки
#
# Запуск:  pip install pillow numpy scipy   затем   python3 build_assets.py
# Портреты настоящих игроков выравниваются по глазам (EYE_Y, EYE_DIST); если глаза найдены неверно —
# пропиши игроку в roster.json "eyes": [x, y, расстояние] в пикселях исходной картинки.
import json, os
import numpy as np
from PIL import Image
from scipy import ndimage

# Выравнивание портретов: у всех игроков одинаковые размер головы и уровень глаз.
EYE_Y = 0.40      # линия глаз — на 40% высоты портрета
EYE_DIST = 0.14   # расстояние между глазами — 14% ширины портрета

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, '..', 'Assets', 'EsportsSim', 'Resources')
SRC_PLAYERS = os.path.join(HERE, 'source', 'players')
SRC_LOGOS = os.path.join(HERE, 'source', 'logos')
EXTS = ('.png', '.jpg', '.jpeg', '.webp')

def find(folder, name):
    for e in EXTS:
        p = os.path.join(folder, name + e)
        if os.path.exists(p): return p
    return None

def crop_square(img, size):
    """Вертикальная — берём верхний квадрат (голова), горизонтальная — центр."""
    img = img.convert('RGBA')
    w, h = img.size
    if h > w: img = img.crop((0, 0, w, w))
    elif w > h: img = img.crop(((w - h) // 2, 0, (w - h) // 2 + h, h))
    return img.resize((size, size), Image.LANCZOS)

def pad_square(img, size):
    img = img.convert('RGBA')
    w, h = img.size; m = max(w, h)
    c = Image.new('RGBA', (m, m), (0, 0, 0, 0))
    c.paste(img, ((m - w) // 2, (m - h) // 2))
    return c.resize((size, size), Image.LANCZOS)

def trim(img):
    bbox = img.getchannel('A').point(lambda v: 255 if v > 20 else 0).getbbox()
    return img.crop(bbox) if bbox else img

def segments(profile, min_gap):
    """Участки с содержимым, разделённые пустыми полосами длиной >= min_gap."""
    segs, start, empty = [], None, 0
    for i, v in enumerate(profile):
        if v:
            if start is None: start = i
            empty = 0
            end = i
        elif start is not None:
            empty += 1
            if empty >= min_gap:
                segs.append((start, end + 1)); start = None
    if start is not None: segs.append((start, end + 1))
    return segs

def emblem(img):
    """Из «эмблема + надпись» оставляет эмблему (первый участок слева/сверху, если он почти квадратный)."""
    img = trim(img.convert('RGBA'))
    a = np.asarray(img)[..., 3] > 20
    h, w = a.shape
    for axis, length in ((0, w), (1, h)):   # сначала ищем разрыв по столбцам (надпись справа), потом по строкам (снизу)
        prof = a.any(axis=axis)
        other = h if axis == 0 else w
        segs = segments(prof, max(3, int(other * (0.04 if axis == 0 else 0.02))))
        if len(segs) >= 2:
            s0, e0 = segs[0]
            part = img.crop((s0, 0, e0, h)) if axis == 0 else img.crop((0, s0, w, e0))
            part = trim(part)
            pw, ph = part.size
            if (e0 - s0) >= 0.1 * length and 0.45 <= pw / ph <= 2.2:
                return part
    return img

# ---------- поиск глаз (пара тёмных пятен внутри лица) ----------
def skin_mask(a):
    r,g,b=a[...,0],a[...,1],a[...,2]; rr=np.maximum(r,1)
    return (r>70)&(r>=g)&(g>=b-8)&(g/rr>0.55)&(g/rr<0.95)&(b/rr>0.3)&(b/rr<0.9)&((r-b)>15)
def find_eyes(im):
    im=im.convert('RGB'); W=im.size[0]
    a=np.asarray(im.resize((400,400))).astype(float); s=400
    skin=skin_mask(a)
    skin=ndimage.binary_closing(skin,iterations=2)
    lab,n=ndimage.label(skin)
    if n==0: return None
    sizes=ndimage.sum(skin,lab,range(1,n+1)); comp=lab==(int(np.argmax(sizes))+1)
    filled=ndimage.binary_fill_holes(comp)
    lum=0.299*a[...,0]+0.587*a[...,1]+0.114*a[...,2]
    dark=(lum<75)&filled&~comp
    dl,dn=ndimage.label(dark)
    if dn<2: return None
    objs=ndimage.find_objects(dl); blobs=[]
    ys,xs=np.where(filled); ftop,fbot=ys.min(),ys.max(); fcx=xs.mean()
    for i,sl in enumerate(objs):
        area=(dl[sl]==i+1).sum()
        if area<6: continue
        cy=(sl[0].start+sl[0].stop)/2; cx=(sl[1].start+sl[1].stop)/2
        hgt=sl[0].stop-sl[0].start; wid=sl[1].stop-sl[1].start
        blobs.append((cx,cy,area,wid,hgt))
    best=None
    for i in range(len(blobs)):
        for j in range(i+1,len(blobs)):
            (x1,y1,a1,w1,h1),(x2,y2,a2,w2,h2)=blobs[i],blobs[j]
            dx=abs(x1-x2); dy=abs(y1-y2)
            if dx<s*0.08 or dx>s*0.32 or dy>s*0.03: continue
            if max(a1,a2)>4*min(a1,a2): continue
            mid=(x1+x2)/2
            if abs(mid-fcx)>s*0.12: continue
            # глаза: компактные (не вытянутые брови), предпочитаем более тёмные/крупные и ниже бровей
            score=min(a1,a2) - 3*dy - 0.5*abs(mid-fcx)
            asp=max(w1/max(h1,1),w2/max(h2,1))
            if asp>4.5: score-=30   # брови длинные и тонкие
            if best is None or score>best[0]: best=(score,mid,(y1+y2)/2,dx)
    if not best: return None
    k=W/s
    return dict(cx=best[1]*k, y=best[2]*k, iod=best[3]*k)

def normalize(img, eyes, size=512):
    """Масштабирует и сдвигает портрет так, чтобы глаза были на EYE_Y, а расстояние между ними — EYE_DIST.
    Всё, что выходит за пределы исходной картинки, — белое."""
    cx, y, iod = eyes
    k = (EYE_DIST * size) / iod
    src = img.convert('RGB').resize((max(1, round(img.size[0] * k)), max(1, round(img.size[1] * k))), Image.LANCZOS)
    out = Image.new('RGB', (size, size), (255, 255, 255))
    out.paste(src, (round(size / 2 - cx * k), round(EYE_Y * size - y * k)))
    return out

def main():
    roster = json.load(open(os.path.join(RES, 'roster.json'), encoding='utf-8'))
    teams = json.load(open(os.path.join(RES, 'teams.json'), encoding='utf-8'))
    for d in ('Portraits', 'Logos'): os.makedirs(os.path.join(RES, d), exist_ok=True)

    for p in roster['players']:
        src = find(SRC_PLAYERS, p['portrait'])
        if src:
            img = Image.open(src)
            # «eyes» в roster.json: [центр X, Y глаз, расстояние между глазами] в пикселях исходника — ручная подсказка
            eyes = p.get('eyes')
            if not eyes and p.get('real') and not p.get('ready'):
                f = find_eyes(img)
                eyes = [f['cx'], f['y'], f['iod']] if f else None
            # ready: портрет уже выровнен (новый единый стиль) — только приводим к 512x512
            if p.get('ready'): out = img.convert('RGB').resize((512, 512), Image.LANCZOS)
            else: out = normalize(img, eyes) if eyes else crop_square(img, 512)
            out.save(os.path.join(RES, 'Portraits', p['portrait'] + '.png'), optimize=True)
        else: print('нет картинки игрока:', p['portrait'])
    for t in teams['teams']:
        src = find(SRC_LOGOS, t['logo'])
        if src:
            img = Image.open(src)
            # fullLogo: логотип целиком (нужно, если составы одной команды различаются только надписью)
            img = trim(img.convert('RGBA')) if t.get('fullLogo') else emblem(img)
            pad_square(img, 256).save(os.path.join(RES, 'Logos', t['logo'] + '.png'), optimize=True)
        else: print('нет логотипа:', t['logo'])
    os.makedirs(os.path.join(RES, 'Regions'), exist_ok=True)
    for r in range(1, 6):
        src = find(os.path.join(HERE, 'source', 'regions'), 'r%d' % r)
        if src: pad_square(emblem(Image.open(src)), 256).save(os.path.join(RES, 'Regions', 'r%d.png' % r), optimize=True)
    preview(roster, teams)

def preview(roster, teams):
    tmap = {t['id']: t for t in teams['teams']}
    cell = 200
    players = roster['players']
    sheet = Image.new('RGB', (cell * 5, cell * ((len(players) + 4) // 5)), (14, 18, 32))
    for k, p in enumerate(players):
        ip = os.path.join(RES, 'Portraits', p['portrait'] + '.png')
        if not os.path.exists(ip): continue
        img = Image.open(ip).convert('RGBA').resize((cell, cell), Image.LANCZOS)
        t = tmap.get(p['region'] * 10 + 1 + k % 5)
        lp = t and os.path.join(RES, 'Logos', t['logo'] + '.png')
        if lp and os.path.exists(lp):
            lg = Image.open(lp).convert('RGBA').resize((56, 56), Image.LANCZOS)
            img.alpha_composite(lg, (cell - 62, cell - 62))
        sheet.paste(img.convert('RGB'), ((k % 5) * cell, (k // 5) * cell))
    out = os.path.join(HERE, 'preview.png')
    sheet.save(out)
    print('Превью:', out)

if __name__ == '__main__':
    main()
