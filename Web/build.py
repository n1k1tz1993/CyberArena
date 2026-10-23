# Собирает веб-версию в один файл kiber-arena.html из Unity-ресурсов и app.js/style.css.
# Запуск: pip install pillow   затем   python3 build.py
import json, base64, os, io
from PIL import Image
HERE=os.path.dirname(os.path.abspath(__file__))+'/'
R=HERE+'../Assets/EsportsSim/Resources/'; W=HERE
roster=json.load(open(R+'roster.json',encoding='utf-8')); teams=json.load(open(R+'teams.json',encoding='utf-8'))
players=[{k:p[k] for k in ('id','nickname','skill','power','region','portrait','country') if k in p} for p in roster['players']]
data={'regions':roster['regions'],'players':players,'teams':[{k:t[k] for k in ('id','name','region','bonus','logo')} for t in teams['teams']]}
def uri(img, fmt, **kw):
    b=io.BytesIO(); img.save(b, fmt, **kw)
    return 'data:image/%s;base64,%s'%(fmt.lower(), base64.b64encode(b.getvalue()).decode())
img={}
def each(d):
    return sorted(f for f in os.listdir(R+d) if f.endswith('.png'))
for f in each('Portraits'): img[f[:-4]]=uri(Image.open(R+'Portraits/'+f).convert('RGB').resize((288,288),Image.LANCZOS),'WEBP',quality=82)
for f in each('Logos'): img[f[:-4]]=uri(Image.open(R+'Logos/'+f).convert('RGBA').resize((160,160),Image.LANCZOS),'WEBP',quality=90)
for f in each('Flags'): img['f_'+f[:-4]]=uri(Image.open(R+'Flags/'+f).convert('RGB'),'PNG',optimize=True)
for f in each('Regions'): img[f[:-4]]=uri(Image.open(R+'Regions/'+f).convert('RGBA').resize((200,200),Image.LANCZOS),'WEBP',quality=90)
img['epl']=uri(Image.open(R+'ESL/epl.png').convert('RGB'),'WEBP',quality=84)
img['esl']=uri(Image.open(R+'ESL/esl.png').convert('RGBA'),'WEBP',quality=90)
img['arena']=uri(Image.open(R+'Arena/arena_bg.png').convert('RGB'),'WEBP',quality=80)
html='''<title>Кибер Арена</title>
<link rel="preconnect" href="https://fonts.googleapis.com"><link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Russo+One&family=Rubik:wght@400;600;700&display=swap">
<meta name="theme-color" content="#0E1220"><meta name="apple-mobile-web-app-capable" content="yes"><meta name="mobile-web-app-capable" content="yes">
<style>
'''+open(W+'style.css',encoding='utf-8').read()+'''</style>
<div id="app"></div>
<script>
const DATA = '''+json.dumps(data,ensure_ascii=False)+''';
const IMG = '''+json.dumps(img)+''';
</script>
<script>
'''+open(W+'app.js',encoding='utf-8').read()+'''</script>
'''
open(W+'kiber-arena.html','w',encoding='utf-8').write(html)
# Для GitHub Pages: полноценная страница index.html в корне репозитория
page='<!doctype html>\n<html lang="ru"><head><meta charset="utf-8">\n<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">\n'+html.replace('<div id="app"></div>','</head><body>\n<div id="app"></div>',1)+'</body></html>\n'
open(HERE+'../index.html','w',encoding='utf-8').write(page)
print(len(html)/1e6,'MB', len(img),'images')
