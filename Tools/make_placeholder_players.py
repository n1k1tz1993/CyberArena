# Создаёт ЗАГЛУШКИ игроков: 20 на каждый регион (100), только ник + скилл + сила,
# и картинки-заглушки в source/players. Команды (teams.json) и логотипы НЕ трогает.
# Когда появятся настоящие игроки — этот скрипт больше не нужен.
import json, os, random, sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from placeholder_art import placeholder_player
RES = os.path.join(HERE, '..', 'Assets', 'EsportsSim', 'Resources')
RC = ["#FF6B6B", "#FFB84D", "#4CD97B", "#4DB8FF", "#B57BFF"]
SKINS = ["#F5D6B8", "#F2C9A0", "#E0AC7E", "#C68B5E", "#9C6644", "#7A4E33"]
HAIRC = ["#1A1A1A", "#2B1D14", "#5A3A22", "#8C3B2A", "#D9B26A", "#E8E2D6", "#3A2A5A"]
HAIR = ["short", "buzz", "sidepart", "long", "mohawk", "curly", "bald", "spiky"]
BEARD = ["none", "none", "none", "stubble", "full", "goatee", "mustache", "chinstrap"]
A = ["Shadow","Blaze","Frost","Viper","Ghost","Storm","Nova","Pulse","Raven","Titan","Echo","Zephyr","Onyx","Vortex","Cipher","Rogue","Spectre","Falcon","Wolf","Hydra"]
B = ["", "X", "Z", "77", "99", "Jr", "One", "Kid", "Pro", "TV"]
SKILLS = ["Удар из тени","Ледяная тюрьма","Цепная молния","Огненный шторм","Призрачный шаг","Землетрясение","Электроимпульс",
          "Дыхание дракона","Сверхновая","Молниеносный рывок","Последний клинок","Несокрушимость","Зеркальный клон","Гипноз",
          "Атака с высоты","Танец клинков","Песчаная буря","Ярость берсерка","Точный выстрел","Щит времени"]
r = random.Random(2026)
names, players = set(), []
os.makedirs(os.path.join(HERE, 'source', 'players'), exist_ok=True)
for reg in range(5):
    for k in range(20):
        while True:
            n = r.choice(A) + r.choice(B)
            if n not in names: names.add(n); break
        pid = reg * 20 + k + 1
        hc = r.choice(HAIRC)
        face = dict(skin=r.choice(SKINS), hair=r.choice(HAIR), hairColor=hc, beard=r.choice(BEARD), beardColor=hc,
                    eyes=r.choice(["#2B1D14", "#3B6EA5", "#4A7A3A", "#1B1B22"]), glasses=r.random() < 0.15)
        p = dict(id=pid, nickname=n, skill=r.choice(SKILLS), power=r.randint(50, 75), region=reg, portrait="p%03d" % pid)
        players.append(p)
        placeholder_player(face, RC[reg]).save(os.path.join(HERE, 'source', 'players', p['portrait'] + '.png'))
path = os.path.join(RES, 'roster.json')
regions = json.load(open(path, encoding='utf-8'))['regions']
json.dump(dict(regions=regions, players=players), open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
print('players', len(players))
