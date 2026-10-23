# Рисует значок приложения (icon-512/192/180.png в корне репозитория). Запуск: python3 make_icon.py
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter
HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, '..', '..')
FONT = '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf'
S = 1024
im = Image.new('RGB', (S, S), (14, 18, 32))
glow = Image.new('L', (S, S), 0); ImageDraw.Draw(glow).ellipse((180, 160, 844, 824), fill=120)
glow = glow.filter(ImageFilter.GaussianBlur(120))
im.paste(Image.new('RGB', (S, S), (46, 230, 214)), (0, 0), glow.point(lambda v: int(v * 0.35)))
d = ImageDraw.Draw(im)
d.ellipse((150, 150, 874, 874), outline=(46, 230, 214), width=34)
d.arc((150, 150, 874, 874), 200, 250, fill=(255, 84, 104), width=34)
d.arc((150, 150, 874, 874), 20, 70, fill=(77, 141, 255), width=34)
d.text((S / 2, S / 2 + 8), 'КА', font=ImageFont.truetype(FONT, 330), fill=(238, 241, 250), anchor='mm')
d.text((S / 2, S / 2 + 225), 'АРЕНА', font=ImageFont.truetype(FONT, 62), fill=(46, 230, 214), anchor='mm')
for n in (512, 192, 180):
    im.resize((n, n), Image.LANCZOS).save(os.path.join(OUT, f'icon-{n}.png'), optimize=True)
