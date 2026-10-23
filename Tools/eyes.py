import sys, json, numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
import os; HERE=os.path.dirname(os.path.abspath(__file__)); SRC=HERE+'/source/players/'
R=HERE+'/../Assets/EsportsSim/Resources/roster.json'
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
if __name__=='__main__':
    ro=json.load(open(R,encoding='utf-8'))
    real=[p for p in ro['players'] if p.get('real')]
    cell=150; cols=10
    sheet=Image.new('RGB',(cols*cell,((len(real)+cols-1)//cols)*cell),(14,18,32)); d=ImageDraw.Draw(sheet)
    miss=[]
    for i,p in enumerate(real):
        im=Image.open(SRC+p['portrait']+'.png').convert('RGB')
        f=find_eyes(im); sc=cell/im.size[0]; x=(i%cols)*cell; y=(i//cols)*cell
        sheet.paste(im.resize((cell,cell)),(x,y))
        if f:
            for sx in (-1,1):
                ex=x+(f['cx']+sx*f['iod']/2)*sc; ey=y+f['y']*sc
                d.ellipse((ex-5,ey-5,ex+5,ey+5),outline=(255,0,0),width=2)
        else: miss.append(p['nickname'])
        d.text((x+4,y+2),p['nickname'][:12],fill=(255,0,255))
    sheet.save('/tmp/claude-0/-home-claude/99cef231-998b-51af-9fd7-d95b9a1ecc6c/scratchpad/eyes.png')
    print('miss',miss)
