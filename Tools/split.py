import sys, numpy as np
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
im = Image.open(src).convert('RGB'); a = np.asarray(im).astype(int)
nonwhite = (a.min(axis=2) < 235)
rows = np.where(nonwhite.mean(axis=1) > 0.02)[0]; y0, y1 = rows.min(), rows.max() + 1
band = nonwhite[y0:y1]
colfill = band.mean(axis=0)
# разделители — столбцы почти без рисунка
cols = colfill > 0.03
segs, start = [], None
for x, v in enumerate(cols):
    if v and start is None: start = x
    if not v and start is not None:
        if x - start > 60: segs.append((start, x))
        start = None
if start is not None and len(cols) - start > 60: segs.append((start, len(cols)))
# слипшиеся фигуры: делим широкий участок по самому «пустому» столбцу
med = sorted(x1 - x0 for x0, x1 in segs)[len(segs) // 2]
fixed = []
for x0, x1 in segs:
    if x1 - x0 > med * 1.6:
        mid = (x0 + x1) // 2; lo, hi = mid - (x1 - x0) // 5, mid + (x1 - x0) // 5
        # смотрим только нижнюю половину (одежда), где фигуры соприкасаются плечами
        lower = nonwhite[y0 + (y1 - y0) // 2:y1, lo:hi].mean(axis=0)
        cut = lo + int(lower.argmin())
        fixed += [(x0, cut), (cut, x1)]
    else: fixed.append((x0, x1))
segs = fixed
print('band', y0, y1, 'segments', segs)
bg = (255, 255, 255)
for i, (x0, x1) in enumerate(segs, 1):
    fig = im.crop((x0, y0, x1, y1))
    w, h = fig.size; side = max(w, h)
    sq = Image.new('RGB', (side, side), bg)
    sq.paste(fig, ((side - w) // 2, side - h))   # фигура по низу, плечи у нижнего края
    sq.save(f'{out}/p{80 + i:03d}.png')
