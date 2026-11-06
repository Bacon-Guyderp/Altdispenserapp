"""Render the in-world guessing map (equirectangular, public-domain Natural Earth data).

Output: Assets/GeoQuest/Textures/WorldMap.png (4096x2048 by default).
The projection is plate carree: x = (lon+180)/360*W, y = (90-lat)/180*H,
which is exactly what MapBoard.cs assumes.
"""
import argparse, os
from PIL import Image, ImageDraw, ImageFont
from geo_common import load_countries, polygons

PALETTE = [(0, 0, 0), (233, 214, 175), (205, 225, 178), (240, 200, 180),
           (200, 214, 232), (238, 230, 160), (214, 196, 226), (190, 222, 210)]
OCEAN = (68, 116, 160)
BORDER = (70, 60, 50)
GRID = (96, 140, 180)
FONT_CANDIDATES = ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                   "C:/Windows/Fonts/arialbd.ttf", "/Library/Fonts/Arial Bold.ttf"]


def font(size):
    for p in FONT_CANDIDATES:
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--width", type=int, default=4096)
    ap.add_argument("--out", default=os.path.join(os.path.dirname(__file__), "..",
                    "Assets", "GeoQuest", "Textures", "WorldMap.png"))
    a = ap.parse_args()
    ss = 2
    W, H = a.width * ss, a.width // 2 * ss
    img = Image.new("RGB", (W, H), OCEAN)
    d = ImageDraw.Draw(img)
    for lon in range(-180, 181, 15):
        x = (lon + 180) / 360 * W
        d.line([(x, 0), (x, H)], fill=GRID, width=ss * (2 if lon == 0 else 1))
    for lat in range(-90, 91, 15):
        y = (90 - lat) / 180 * H
        d.line([(0, y), (W, y)], fill=GRID, width=ss * (2 if lat == 0 else 1))

    feats = load_countries()
    P = lambda c: ((c[0] + 180) / 360 * W, (90 - c[1]) / 180 * H)
    for f in feats:
        col = PALETTE[int(f["properties"].get("MAPCOLOR7") or 1) % len(PALETTE)]
        for poly in polygons(f):
            d.polygon([P(c) for c in poly[0]], fill=col)
            for hole in poly[1:]:
                d.polygon([P(c) for c in hole], fill=OCEAN)
    for f in feats:
        for poly in polygons(f):
            for ring in poly:
                d.line([P(c) for c in ring], fill=BORDER, width=ss)

    # Country labels, biggest countries first; skip any label that would overlap another.
    placed = []
    for f in sorted(feats, key=lambda f: (f["properties"].get("LABELRANK", 9), -f["properties"].get("POP_EST", 0))):
        p = f["properties"]
        rank = p.get("LABELRANK", 9)
        if rank > 7:
            continue
        size = {1: 26, 2: 22, 3: 18, 4: 15, 5: 13, 6: 11, 7: 10}.get(rank, 10) * ss * a.width // 4096
        name = p.get("NAME") or ""
        x, y = P((p["LABEL_X"], p["LABEL_Y"]))
        fnt = font(size)
        x0, y0, x1, y1 = d.textbbox((x, y), name, font=fnt, anchor="mm")
        box = (x0 - 2 * ss, y0 - 2 * ss, x1 + 2 * ss, y1 + 2 * ss)
        if any(not (box[2] < b[0] or box[0] > b[2] or box[3] < b[1] or box[1] > b[3]) for b in placed):
            continue
        placed.append(box)
        d.text((x, y), name, font=fnt, fill=(40, 34, 30), anchor="mm",
               stroke_width=max(1, size // 7), stroke_fill=(250, 248, 240))
    img = img.resize((W // ss, H // ss), Image.LANCZOS)
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    img.save(a.out, optimize=True)
    print("wrote", a.out, img.size)


if __name__ == "__main__":
    main()
