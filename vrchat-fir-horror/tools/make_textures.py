"""Paints every texture in Fir Warning procedurally into Assets/FirHorror/Textures.

  python make_textures.py      (needs numpy and Pillow)

All textures are small (256-1024 px) so they compress to almost nothing on Quest (ASTC).
The posters are original in-world props for the fictional "Evergreen Novelty Corp."
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets", "FirHorror", "Textures")
rng = np.random.default_rng(1225)

FONT_DIRS = ["/usr/share/fonts/truetype/freefont", "/usr/share/fonts/truetype/dejavu",
             "/usr/share/fonts/truetype/liberation", "C:/Windows/Fonts", "/Library/Fonts"]


def font(names, size):
    for d in FONT_DIRS:
        for n in names:
            p = os.path.join(d, n)
            if os.path.exists(p):
                return ImageFont.truetype(p, size)
    return ImageFont.load_default()


BOLD_IT = ["FreeSansBoldOblique.ttf", "DejaVuSans-BoldOblique.ttf", "LiberationSans-BoldItalic.ttf", "arialbi.ttf"]
BOLD = ["FreeSansBold.ttf", "DejaVuSans-Bold.ttf", "LiberationSans-Bold.ttf", "arialbd.ttf"]
MONO = ["FreeMono.ttf", "DejaVuSansMono.ttf", "LiberationMono-Regular.ttf", "cour.ttf"]
SCRAWL = ["FreeSerifBoldItalic.ttf", "DejaVuSerif-BoldItalic.ttf", "LiberationSerif-BoldItalic.ttf"]


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.convert("RGB").save(os.path.join(OUT, name + ".png"), optimize=True)
    print(name)


def noise_img(size, scale, octaves=4):
    """Tileable value noise in [0,1]."""
    acc = np.zeros((size, size))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cells = scale * 2 ** o
        g = rng.random((cells, cells))
        g = np.pad(g, ((0, 1), (0, 1)), mode="wrap")
        img = Image.fromarray((g * 255).astype(np.uint8)).resize((size + size // cells, size + size // cells), Image.BICUBIC)
        acc += np.asarray(img, dtype=float)[:size, :size] / 255 * amp
        total += amp
        amp *= 0.5
    return acc / total


def tint(n, dark, light):
    d, l = np.array(dark, float), np.array(light, float)
    return Image.fromarray((d + (l - d) * n[..., None]).clip(0, 255).astype(np.uint8))


def grime(img, strength=0.35):
    n = noise_img(max(img.width, img.height), 3)[:img.height, :img.width]
    a = np.asarray(img, float) * (1 - strength * (n[..., None] > 0.55) * (n[..., None] - 0.55) * 2.2)
    return Image.fromarray(a.clip(0, 255).astype(np.uint8))


def needles():
    s = 512
    img = tint(noise_img(s, 8), (12, 45, 18), (40, 105, 45))
    d = ImageDraw.Draw(img)
    for _ in range(5000):
        x, y = rng.integers(0, s, 2)
        a = rng.uniform(-0.6, 0.6) + np.pi / 2
        l = rng.uniform(8, 22)
        c = tuple(int(v) for v in rng.integers([10, 50, 15], [60, 140, 60]))
        for dx in (0, s, -s):
            for dy in (0, s, -s):
                d.line([(x + dx, y + dy), (x + dx + np.cos(a) * l, y + dy + np.sin(a) * l)], fill=c, width=2)
    save(img, "needles")


def wallpaper():
    s = 512
    base = tint(noise_img(s, 4), (150, 135, 100), (205, 195, 160))
    d = ImageDraw.Draw(base)
    for x in range(0, s, 64):
        d.rectangle([x, 0, x + 6, s], fill=(150, 40, 40))
        d.rectangle([x + 30, 0, x + 32, s], fill=(70, 110, 70))
    for y in range(32, s, 128):
        for x in range(16, s, 64):
            yy = y + (64 if (x // 64) % 2 else 0)
            d.polygon([(x + 16, yy - 14), (x + 4, yy + 12), (x + 28, yy + 12)], fill=(60, 105, 60))
            d.ellipse([x + 11, yy - 2, x + 14, yy + 1], fill=(30, 20, 20))
            d.ellipse([x + 18, yy - 2, x + 21, yy + 1], fill=(30, 20, 20))
            d.arc([x + 11, yy + 1, x + 21, yy + 9], 0, 180, fill=(150, 30, 30), width=2)
    img = grime(base, 0.6)
    # water stains streaking down
    a = np.asarray(img, float)
    for _ in range(8):
        x = rng.integers(0, s - 30)
        w = rng.integers(10, 30)
        h = rng.integers(100, s)
        a[:h, x:x + w] *= np.linspace(0.65, 1.0, h)[:, None, None]
    save(Image.fromarray(a.clip(0, 255).astype(np.uint8)), "wallpaper")


def simple(name, dark, light, scale=6, size=512, lines=None):
    img = grime(tint(noise_img(size, scale), dark, light), 0.4)
    if lines:
        d = ImageDraw.Draw(img)
        step, col, vertical = lines
        for p in range(0, size, step):
            if vertical:
                d.line([(p, 0), (p, size)], fill=col, width=3)
            else:
                d.line([(0, p), (size, p)], fill=col, width=3)
    save(img, name)


def wood_floor():
    s = 512
    n = noise_img(s, 4)
    grain = (np.sin(np.linspace(0, 60, s)[None, :] + n * 8) + 1) / 2
    img = tint(grain * 0.5 + n * 0.5, (55, 32, 18), (125, 80, 45))
    d = ImageDraw.Draw(img)
    for x in range(0, s, 64):
        d.line([(x, 0), (x, s)], fill=(30, 18, 10), width=3)
        off = rng.integers(0, s)
        d.line([(x, off), (x + 64, off)], fill=(30, 18, 10), width=3)
    save(grime(img, 0.4), "wood_floor")


def cardboard():
    s = 512
    img = grime(tint(noise_img(s, 6), (140, 100, 60), (190, 150, 100)), 0.3)
    d = ImageDraw.Draw(img)
    d.rectangle([20, 20, s - 20, s - 20], outline=(90, 60, 35), width=4)
    d.text((s // 2, 120), "DOUGLAS FIR", font=font(BOLD_IT, 58), fill=(30, 90, 40), anchor="mm")
    d.text((s // 2, 180), "THE TALKING TREE", font=font(BOLD, 34), fill=(150, 30, 30), anchor="mm")
    d.text((s // 2, 260), "SINGS! TALKS! MOVES!", font=font(BOLD, 28), fill=(40, 30, 20), anchor="mm")
    d.text((s // 2, 330), "EVERGREEN NOVELTY CORP.", font=font(MONO, 22), fill=(60, 40, 25), anchor="mm")
    d.text((s // 2, 400), "THIS SIDE UP  ^", font=font(BOLD, 26), fill=(60, 40, 25), anchor="mm")
    lot = "LOT 1996-%04d" % rng.integers(1, 9999)
    d.text((s // 2, 450), lot, font=font(MONO, 20), fill=(60, 40, 25), anchor="mm")
    save(img, "cardboard")


def poster(name, w, h, bg, blocks, scrawl=None, face=False):
    img = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(img)
    if face:  # a cartoon fir with a face, original drawing
        cx, top = w // 2, int(h * 0.38)
        for k in range(4):
            yy = top + k * 70
            d.polygon([(cx, yy - 60), (cx - 90 - k * 40, yy + 80), (cx + 90 + k * 40, yy + 80)], fill=(30, 110, 45))
        d.ellipse([cx - 55, top + 90, cx - 20, top + 125], fill="white"); d.ellipse([cx - 45, top + 100, cx - 30, top + 115], fill="black")
        d.ellipse([cx + 20, top + 90, cx + 55, top + 125], fill="white"); d.ellipse([cx + 30, top + 100, cx + 45, top + 115], fill="black")
        d.chord([cx - 40, top + 150, cx + 40, top + 220], 0, 180, fill=(170, 20, 30))
    for (y, text, fnt, size, col) in blocks:
        f = font(fnt, size)
        for i, line in enumerate(text.split("\n")):
            yy = int(y * h) + i * int(size * 1.15)
            d.text((w // 2 + 3, yy + 3), line, font=f, fill=(0, 0, 0), anchor="mm")
            d.text((w // 2, yy), line, font=f, fill=col, anchor="mm")
    img = grime(img.filter(ImageFilter.GaussianBlur(0.6)), 0.5)
    if scrawl:
        d = ImageDraw.Draw(img)
        f = font(SCRAWL, int(w * 0.075))
        layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
        ld = ImageDraw.Draw(layer)
        ld.text((w // 2, int(h * 0.8)), scrawl, font=f, fill=(120, 0, 0, 230), anchor="mm")
        layer = layer.rotate(rng.uniform(-12, 12), resample=Image.BICUBIC)
        img = Image.alpha_composite(img.convert("RGBA"), layer)
    save(img, name)


def window_snow():
    w, h = 512, 512
    img = Image.new("RGB", (w, h), (25, 30, 45))
    a = np.asarray(img, float)
    a *= np.linspace(0.6, 1.3, h)[:, None, None]
    img = Image.fromarray(a.clip(0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    for _ in range(14):
        x = rng.integers(0, w); y = rng.integers(200, 380); s = rng.integers(40, 110)
        d.polygon([(x, y - s * 2), (x - s // 2, y + 40), (x + s // 2, y + 40)], fill=(150, 160, 170))
    d.rectangle([0, 400, w, h], fill=(210, 215, 225))
    for _ in range(700):
        x, y = rng.integers(0, w), rng.integers(0, h)
        r = rng.uniform(0.6, 2.2)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(235, 238, 245))
    # something standing out in the snow
    d.polygon([(330, 260), (300, 400), (360, 400)], fill=(15, 35, 20))
    d.ellipse([318, 300, 326, 308], fill=(255, 255, 230)); d.ellipse([334, 300, 342, 308], fill=(255, 255, 230))
    save(img, "window_snow")


def paper():
    simple("paper", (205, 198, 175), (240, 235, 215), scale=3, size=256)


def main():
    needles(); wallpaper(); wood_floor(); cardboard(); paper(); window_snow()
    simple("concrete", (55, 55, 55), (110, 108, 104), scale=6)
    simple("carpet_red", (70, 10, 15), (125, 25, 30), scale=16)
    simple("ceiling", (120, 118, 110), (160, 158, 150), scale=5, lines=(128, (90, 88, 80), True))
    simple("metal", (45, 50, 55), (95, 100, 105), scale=10, lines=(64, (35, 38, 40), False))
    simple("office_carpet", (35, 40, 55), (70, 78, 95), scale=24)
    simple("brick", (80, 35, 25), (140, 70, 50), scale=8, lines=(32, (60, 55, 50), False))
    poster("poster_warning", 512, 720, (238, 236, 225), [
        (0.10, "WARNING:", BOLD_IT, 78, (205, 25, 30)),
        (0.24, "Don't get fooled by\nIMITATION\ntalking trees!", BOLD_IT, 48, (30, 140, 50)),
        (0.58, "Unauthorized copies\nwill not be tolerated.", BOLD_IT, 38, (30, 140, 50)),
        (0.90, "Evergreen Novelty Corp. 1996", MONO, 22, (40, 40, 40))])
    poster("poster_warning_scrawl", 512, 720, (238, 236, 225), [
        (0.10, "WARNING:", BOLD_IT, 78, (205, 25, 30)),
        (0.24, "Don't get fooled by\nIMITATION\ntalking trees!", BOLD_IT, 48, (30, 140, 50)),
        (0.58, "Violators will be\nprosecuted.", BOLD_IT, 38, (30, 140, 50))], scrawl="IT KNOWS WHICH\nONE YOU ARE")
    poster("poster_original", 512, 720, (120, 20, 25), [
        (0.08, "PLAY IT SAFE -", BOLD_IT, 52, (255, 255, 255)),
        (0.16, "BUY ONLY THE ORIGINAL!", BOLD_IT, 34, (255, 255, 255)),
        (0.93, "He sings! He talks! He watches!", BOLD, 26, (255, 220, 120))], face=True)
    poster("poster_recall", 512, 720, (230, 228, 215), [
        (0.08, "PRODUCT RECALL", BOLD, 54, (20, 20, 20)),
        (0.30, "Model DF-96 'imitation'\nunits shipped in error.\n\nDo NOT leave units\npowered overnight.\nDo NOT turn your back\non a unit that is\nfacing you.", MONO, 28, (20, 20, 20)),
        (0.88, "Return all units to the\nRecall Floor for disposal.", BOLD, 26, (160, 20, 20))])
    poster("poster_safety", 512, 720, (240, 200, 40), [
        (0.10, "SAFETY FIRST", BOLD, 60, (20, 20, 20)),
        (0.45, "If a tree moves\nwhile you are\nnot looking at it,\n\nLOOK AT IT.", BOLD, 44, (20, 20, 20)),
        (0.88, "Days without incident: 0", MONO, 26, (20, 20, 20))])


if __name__ == "__main__":
    main()
