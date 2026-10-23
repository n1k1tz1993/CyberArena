import sys, numpy as np
from PIL import Image
src, out, first, ncols = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
im = Image.open(src).convert('RGB'); a = np.asarray(im).astype(int)
nonwhite = a.min(axis=2) < 235
def segs(prof, thr, minlen):
    res, st = [], None
    for i, v in enumerate(prof > thr):
        if v and st is None: st = i
        if not v and st is not None:
            if i - st >= minlen: res.append((st, i))
            st = None
    if st is not None and len(prof) - st >= minlen: res.append((st, len(prof)))
    return res
rows = segs(nonwhite.mean(axis=1), 0.02, 100)
print('rows', rows)
k = first
for (y0, y1) in rows:
    band = nonwhite[y0:y1]
    cols = segs(band.mean(axis=0), 0.03, 60)
    if len(cols) != ncols:  # фигуры слиплись — режем ровной сеткой по ширине рисунка
        xs = np.where(band.mean(axis=0) > 0.03)[0]; x0, x1 = xs.min(), xs.max() + 1
        cols = [(int(x0 + (x1 - x0) * i / ncols), int(x0 + (x1 - x0) * (i + 1) / ncols)) for i in range(ncols)]
    print(' cols', cols)
    for (x0, x1) in cols:
        fig = im.crop((x0, y0, x1, y1)); w, h = fig.size; side = max(w, h)
        sq = Image.new('RGB', (side, side), (255, 255, 255)); sq.paste(fig, ((side - w) // 2, side - h))
        sq.save(f'{out}/p{k:03d}.png'); k += 1
