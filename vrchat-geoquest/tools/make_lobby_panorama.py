"""Render the lobby 'sky' (equirectangular, 2048x1024) shown between games."""
import math, os, random
from PIL import Image, ImageDraw, ImageFont

W, H = 2048, 1024
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "GeoQuest", "Textures", "LobbyPanorama.png")
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

img = Image.new("RGB", (W, H))
px = img.load()
top, hor, gnd = (12, 16, 48), (230, 120, 80), (20, 22, 30)
for y in range(H):
    t = y / (H / 2)
    if y < H // 2:
        k = t ** 2.2
        c = tuple(int(top[i] + (hor[i] - top[i]) * k) for i in range(3))
    else:
        k = min(1, (y - H / 2) / (H * 0.08))
        c = tuple(int(hor[i] * (1 - k) * 0.6 + gnd[i] * (0.4 + 0.6 * k)) for i in range(3))
    for x in range(W):
        px[x, y] = c
d = ImageDraw.Draw(img)
random.seed(7)
for _ in range(900):
    x, y = random.randrange(W), int(random.random() ** 1.6 * H * 0.42)
    b = random.randint(150, 255)
    d.point((x, y), fill=(b, b, b))
# faint latitude/longitude rings on the "floor" of the sky dome
for lon in range(0, 360, 15):
    x = lon / 360 * W
    d.line([(x, H // 2 + 6), (x, H)], fill=(60, 50, 60), width=1)
for k in range(1, 6):
    y = H // 2 + k * k * 12
    d.line([(0, y), (W, y)], fill=(60, 50, 60), width=1)


def text_at(u, elev, s, size, fill):
    f = ImageFont.truetype(FONT, size) if os.path.exists(FONT) else ImageFont.load_default()
    x, y = u * W, (0.5 - elev / 180) * H
    for dx in (0, W, -W):
        d.text((x + dx, y), s, font=f, fill=fill, anchor="mm", stroke_width=size // 14, stroke_fill=(20, 10, 30))


text_at(0.5, 38, "GEOQUEST", 110, (255, 236, 200))
text_at(0.5, 25, "Where in the world are you?", 34, (255, 220, 190))
text_at(0.0, 22, "Photos: Wikimedia Commons contributors", 28, (255, 220, 190))
img.save(OUT, optimize=True)
print("wrote", OUT)
