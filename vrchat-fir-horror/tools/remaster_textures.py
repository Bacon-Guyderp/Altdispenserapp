"""The remastered surface textures for Fir Warning (called by make_textures.py).

Each one is 1024 px, tiles seamlessly, and covers 2 m of wall or floor (the builder's boxes tile
every 2 m), so the bricks, planks and tiles come out at real-world sizes. Everything is painted
from noise and simple shapes: no photos, no licences.

The talking trees' needles and the window picture are left exactly as they were.
"""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

S = 1024
rng = np.random.default_rng(2026)


# ---------------------------------------------------------------- helpers (all tileable)

def noise(scale, octaves=5, size=S, persistence=0.5):
    """Tileable value noise in [0,1]: `scale` cells across at the coarsest octave."""
    acc = np.zeros((size, size)); amp = 1.0; total = 0.0
    for o in range(octaves):
        cells = max(1, int(scale * 2 ** o))
        if cells >= size:
            break
        g = rng.random((cells, cells))
        g = np.pad(g, ((0, 1), (0, 1)), mode="wrap")
        img = Image.fromarray(g.astype(np.float32), mode="F").resize(((cells + 1) * size // cells,) * 2, Image.BICUBIC)
        a = np.asarray(img, float)[:size, :size]
        acc += a * amp; total += amp; amp *= persistence
    n = acc / total
    return (n - n.min()) / (n.max() - n.min() + 1e-9)


def streaks(size=S, horizontal=True, density=0.6):
    """Fine brushed/grain streaks: noise stretched along one axis."""
    if horizontal:
        a = rng.random((size, 16))
        img = Image.fromarray((a * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    else:
        a = rng.random((16, size))
        img = Image.fromarray((a * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    return np.asarray(img, float) / 255 * density + (1 - density) * 0.5


def colour(n, dark, light):
    d, l = np.array(dark, float), np.array(light, float)
    return d + (l - d) * n[..., None]


def shade(a, f):
    return a * (f[..., None] if f.ndim == 2 else f)


def out(a):
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def wrap_draw(img, fn):
    """Calls fn(draw, dx, dy) for the 9 wrapped copies so shapes crossing an edge tile."""
    d = ImageDraw.Draw(img)
    for dx in (-S, 0, S):
        for dy in (-S, 0, S):
            fn(d, dx, dy)


def crack(img, x, y, length, col, width=2):
    pts = [(x, y)]
    ang = rng.uniform(0, 2 * np.pi)
    for _ in range(length):
        ang += rng.normal(0, 0.45)
        x += np.cos(ang) * 6; y += np.sin(ang) * 6
        pts.append((x, y))
    wrap_draw(img, lambda d, dx, dy: d.line([(p[0] + dx, p[1] + dy) for p in pts], fill=col, width=width))


def ao_edges(mask_dist, width):
    """0 at a seam, 1 a little way in: soft darkening along mortar lines and plank gaps."""
    return np.clip(mask_dist / width, 0, 1) ** 0.6


# ---------------------------------------------------------------- the textures

def brick():
    # running bond: 8 bricks across (25 cm each), 32 courses (6.25 cm), 1 cm mortar
    bw, bh, mort = 128, 32, 5
    y, x = np.mgrid[0:S, 0:S]
    row = y // bh
    xo = (x + (row % 2) * (bw // 2)) % S
    col = xo // bw
    lx, ly = xo % bw, y % bh
    edge = np.minimum(np.minimum(lx, bw - 1 - lx), np.minimum(ly, bh - 1 - ly)).astype(float)
    in_brick = edge >= mort / 2
    ids = (row * 97 + col * 13) % 251
    tone = rng.uniform(0.75, 1.15, 251)[ids]
    hue = rng.uniform(-1, 1, 251)[ids]
    n = noise(24, 5)
    base = colour(n, (118, 42, 28), (178, 78, 50))
    base[..., 0] *= tone * (1 + 0.06 * hue); base[..., 1] *= tone * (1 - 0.05 * hue); base[..., 2] *= tone
    pores = noise(160, 2)
    base = shade(base, 0.88 + 0.24 * pores)
    # bevelled brick edges: a touch darker toward the mortar
    base = shade(base, 0.72 + 0.28 * ao_edges(edge - mort / 2, 6))
    mortar = colour(noise(64, 3), (92, 86, 78), (140, 134, 122))
    a = np.where(in_brick[..., None], base, mortar)
    # soot and damp toward the bottom of some patches
    grime = noise(3, 4)
    a = shade(a, 1 - 0.28 * np.clip((grime - 0.55) * 3, 0, 1))
    img = out(a)
    for _ in range(40):   # chipped corners
        cx, cy = rng.integers(0, S, 2)
        r = rng.integers(3, 8)
        wrap_draw(img, lambda d, dx, dy: d.ellipse([cx - r + dx, cy - r + dy, cx + r + dx, cy + r + dy], fill=(104, 96, 86)))
    return img


def wood_floor():
    # 8 planks across (25 cm), staggered butt joints, each with its own tone and grain
    pw = 128
    y, x = np.mgrid[0:S, 0:S]
    plank = x // pw
    a = np.zeros((S, S, 3))
    for p in range(S // pw):
        tone = rng.uniform(0.78, 1.18)
        warm = rng.uniform(-0.06, 0.06)
        g = rng.random((S // 64, 4))
        grain = np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((pw, S), Image.BICUBIC), float) / 255
        ring = (np.sin((np.arange(pw)[None, :] * rng.uniform(0.25, 0.6)) + grain * rng.uniform(2.5, 4.5)) + 1) / 2
        fine = np.asarray(Image.fromarray((rng.random((S // 16, 40)) * 255).astype(np.uint8)).resize((pw, S), Image.BICUBIC), float) / 255
        n = 0.45 * ring + 0.35 * grain + 0.2 * fine
        c = colour(n, (62, 36, 20), (142, 92, 52)) * tone
        c[..., 0] *= 1 + warm; c[..., 2] *= 1 - warm
        # knots
        for _ in range(rng.integers(0, 3)):
            ky, kx = rng.integers(0, S), rng.integers(20, pw - 20)
            yy, xx = np.mgrid[0:S, 0:pw]
            dy = (yy - ky + S // 2) % S - S // 2
            r = np.sqrt(((xx - kx) * 1.6) ** 2 + dy ** 2)
            c = shade(c, 1 - 0.45 * np.exp(-(r / 9) ** 2) - 0.12 * np.exp(-(r / 22) ** 2) * (np.sin(r * 0.7) > 0))
        a[:, p * pw:(p + 1) * pw] = c
        # butt joints
        for j in range(2):
            jy = (rng.integers(0, S) + j * S // 2) % S
            a[max(0, jy - 1):jy + 2, p * pw:(p + 1) * pw] *= 0.4
    # gaps between planks and a soft bevel either side
    lx = x % pw
    edge = np.minimum(lx, pw - 1 - lx).astype(float)
    a = shade(a, 0.35 + 0.65 * ao_edges(edge, 4))
    # wear and a satin sheen down the middle of each plank
    wear = noise(4, 4)
    a = shade(a, 0.92 + 0.12 * wear)
    return out(a)


def concrete():
    n = noise(6, 6)
    a = colour(n, (78, 82, 80), (132, 134, 126))
    agg = noise(200, 2)
    a = shade(a, 0.9 + 0.2 * (agg > 0.62) * (agg - 0.62) * 3 + 0.05 * agg)
    pits = rng.random((S, S)) > 0.9965
    pits = np.asarray(Image.fromarray((pits * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3)), float) / 255
    a = shade(a, 1 - 0.35 * pits)
    stain = noise(2, 4)
    a = shade(a, 1 - 0.25 * np.clip((stain - 0.5) * 2.5, 0, 1))
    img = out(a)
    for _ in range(5):
        crack(img, *rng.integers(0, S, 2), int(rng.integers(20, 60)), (52, 54, 52), 2)
    # formwork seam and tie holes, very faint
    d = ImageDraw.Draw(img)
    d.line([(0, S // 2), (S, S // 2)], fill=(96, 98, 94), width=2)
    for tx in (S // 4, 3 * S // 4):
        for ty in (S // 4, 3 * S // 4):
            d.ellipse([tx - 7, ty - 7, tx + 7, ty + 7], fill=(70, 72, 70))
            d.ellipse([tx - 4, ty - 4, tx + 4, ty + 4], fill=(48, 50, 50))
    return img


def metal():
    base = colour(noise(4, 5) * 0.5 + streaks(S, True, 1.0) * 0.5, (58, 63, 68), (118, 124, 130))
    img = out(base)
    d = ImageDraw.Draw(img)
    # 4 panels per tile (50 cm), seams, rivets
    for p in range(0, S, 256):
        d.line([(0, p), (S, p)], fill=(34, 37, 40), width=4)
        d.line([(0, p + 3), (S, p + 3)], fill=(140, 146, 150), width=1)
        for rx in range(16, S, 64):
            for ry in (p + 14, p + 242):
                d.ellipse([rx - 6, ry - 6, rx + 6, ry + 6], fill=(46, 50, 54))
                d.ellipse([rx - 4, ry - 5, rx + 3, ry + 2], fill=(150, 156, 160))
    for _ in range(120):   # scratches
        x, y = rng.integers(0, S, 2); l = rng.uniform(10, 60); ang = rng.normal(0, 0.25)
        col = tuple(int(c) for c in rng.integers(120, 170, 3))
        wrap_draw(img, lambda dd, dx, dy: dd.line([(x + dx, y + dy), (x + dx + np.cos(ang) * l, y + dy + np.sin(ang) * l)], fill=col, width=1))
    a = np.asarray(img, float)
    rust = noise(6, 5)
    rc = np.array([110, 60, 30], float)
    m = np.clip((rust - 0.74) * 4, 0, 1)[..., None]
    a = a * (1 - m * 0.7) + rc * m * 0.7
    return out(a)


def ceiling():
    # 3 x 3 acoustic tiles per tile (about 67 cm), aluminium T-bar grid, fissured surface
    t = S // 3 + 1
    n = noise(40, 3)
    a = colour(n, (168, 166, 158), (206, 204, 196))
    fiss = noise(90, 3)
    a = shade(a, 1 - 0.3 * (fiss < 0.22))
    holes = rng.random((S, S)) > 0.992
    a = shade(a, 1 - 0.4 * holes)
    y, x = np.mgrid[0:S, 0:S]
    lx = np.minimum(x % t, t - 1 - x % t); ly = np.minimum(y % t, t - 1 - y % t)
    e = np.minimum(lx, ly).astype(float)
    a = shade(a, 0.78 + 0.22 * ao_edges(e - 5, 10))
    grid = e < 5
    a = np.where(grid[..., None], np.array([214, 214, 210], float), a)
    water = noise(2, 4)
    m = np.clip((water - 0.7) * 4, 0, 1)[..., None]
    a = a * (1 - m * 0.35) + np.array([150, 120, 70], float) * m * 0.35
    return out(a)


def carpet(dark, light, motif):
    fib = noise(300, 2)
    n = noise(12, 5)
    a = colour(n * 0.6 + fib * 0.4, dark, light)
    if motif == "diamond":   # a woven diamond lattice (the hotel/lobby carpet)
        y, x = np.mgrid[0:S, 0:S]
        u = ((x + y) % 128) - 64; v = ((x - y) % 128) - 64
        line = (np.abs(u) < 3) | (np.abs(v) < 3)
        a = shade(a, 1 - 0.22 * line)
        dot = (np.abs(u) < 10) & (np.abs(v) < 10)
        a = np.where(dot[..., None], a * 0.6 + np.array([200, 150, 60], float) * 0.4, a)
    else:                    # loop-pile carpet tiles, alternating direction (the offices)
        y, x = np.mgrid[0:S, 0:S]
        tx, ty = x // 256, y // 256
        horiz = streaks(S, True, 1.0); vert = streaks(S, False, 1.0)
        dirn = np.where(((tx + ty) % 2 == 0), horiz, vert)
        a = shade(a, 0.85 + 0.3 * dirn)
        e = np.minimum(np.minimum(x % 256, 255 - x % 256), np.minimum(y % 256, 255 - y % 256)).astype(float)
        a = shade(a, 0.8 + 0.2 * ao_edges(e, 3))
    wear = noise(3, 4)
    a = shade(a, 0.9 + 0.15 * wear)
    return out(a)


def wallpaper():
    # the original pattern (red and green stripes, little smiling firs), repainted at twice the size on
    # a textured paper with a faint damask, seams every 50 cm, and the old water stains
    base = colour(noise(8, 5), (172, 142, 98), (232, 208, 156))
    y, x = np.mgrid[0:S, 0:S]
    dam = (np.sin(x / S * 2 * np.pi * 16) * np.sin(y / S * 2 * np.pi * 8) + np.sin((x + y) / S * 2 * np.pi * 8)) * 0.5
    base = shade(base, 0.97 + 0.04 * dam + 0.06 * (noise(220, 2) - 0.5))
    img = out(base)
    d = ImageDraw.Draw(img)
    for x0 in range(0, S, 128):
        d.rectangle([x0, 0, x0 + 17, S], fill=(186, 28, 38))
        d.rectangle([x0 + 60, 0, x0 + 67, S], fill=(32, 128, 56))
        d.line([(x0 + 2, 0), (x0 + 2, S)], fill=(220, 90, 90), width=1)
    for y0 in range(64, S, 256):
        for x0 in range(32, S, 128):
            yy = y0 + (128 if (x0 // 128) % 2 else 0)
            d.polygon([(x0 + 32, yy - 28), (x0 + 8, yy + 24), (x0 + 56, yy + 24)], fill=(28, 120, 52))
            d.polygon([(x0 + 32, yy - 28), (x0 + 20, yy - 2), (x0 + 44, yy - 2)], fill=(40, 140, 62))
            d.rectangle([x0 + 29, yy + 24, x0 + 35, yy + 32], fill=(90, 55, 30))
            d.ellipse([x0 + 22, yy - 4, x0 + 28, yy + 2], fill=(30, 20, 20))
            d.ellipse([x0 + 36, yy - 4, x0 + 42, yy + 2], fill=(30, 20, 20))
            d.arc([x0 + 22, yy + 2, x0 + 42, yy + 18], 0, 180, fill=(150, 30, 30), width=3)
    a = np.asarray(img, float)
    for sx in range(0, S, 256):   # paper seams
        a[:, sx:sx + 2] *= 0.82
    grime = noise(3, 4)
    a = shade(a, 1 - 0.45 * np.clip((grime - 0.5) * 2.2, 0, 1))
    for _ in range(10):   # water stains running down from the top
        x0 = rng.integers(0, S - 60); w = rng.integers(20, 60); h = rng.integers(200, S)
        prof = np.linspace(0.62, 1.0, h)[:, None]
        edge = np.ones(w); edge[:4] = edge[-4:] = 0.94
        a[:h, x0:x0 + w] *= (prof * edge[None, :])[..., None]
    return out(a)


def paper():
    n = noise(6, 4)
    a = colour(n, (226, 219, 198), (242, 237, 220))
    fib = streaks(S, True, 1.0) * 0.5 + streaks(S, False, 1.0) * 0.5
    a = shade(a, 0.96 + 0.08 * fib)
    flecks = rng.random((S, S)) > 0.998
    a = shade(a, 1 - 0.3 * flecks)
    return out(a)


def cardboard(font, BOLD_IT, BOLD, MONO):
    n = noise(6, 5)
    a = colour(n, (146, 104, 62), (196, 156, 104))
    y = np.arange(S)[None, :]
    a = shade(a, 0.95 + 0.05 * np.sin(y / S * 2 * np.pi * 96))   # the corrugation showing through
    a = shade(a, 0.94 + 0.08 * noise(200, 2))
    img = out(a)
    d = ImageDraw.Draw(img)
    d.rectangle([40, 40, S - 40, S - 40], outline=(92, 62, 36), width=8)
    d.line([(0, 6), (S, 6)], fill=(120, 84, 48), width=4)   # tape
    d.rectangle([S // 2 - 60, 0, S // 2 + 60, S], fill=(176, 150, 106))
    d.text((S // 2, 240), "DOUGLAS FIR", font=font(BOLD_IT, 116), fill=(30, 90, 40), anchor="mm")
    d.text((S // 2, 360), "THE TALKING TREE", font=font(BOLD, 68), fill=(150, 30, 30), anchor="mm")
    d.text((S // 2, 520), "SINGS! TALKS! MOVES!", font=font(BOLD, 56), fill=(40, 30, 20), anchor="mm")
    d.text((S // 2, 660), "EVERGREEN NOVELTY CORP.", font=font(MONO, 44), fill=(60, 40, 25), anchor="mm")
    d.text((S // 2, 800), "THIS SIDE UP  ^", font=font(BOLD, 52), fill=(60, 40, 25), anchor="mm")
    d.text((S // 2, 900), "LOT 1996-%04d" % rng.integers(1, 9999), font=font(MONO, 40), fill=(60, 40, 25), anchor="mm")
    a = np.asarray(img, float)
    scuff = noise(5, 4)
    a = shade(a, 1 - 0.25 * np.clip((scuff - 0.55) * 3, 0, 1))
    return out(a)


def make_all(save, font, BOLD_IT, BOLD, MONO):
    save(brick(), "brick")
    save(wood_floor(), "wood_floor")
    save(concrete(), "concrete")
    save(metal(), "metal")
    save(ceiling(), "ceiling")
    save(carpet((92, 8, 16), (176, 26, 38), "diamond"), "carpet_red")
    save(carpet((20, 36, 80), (60, 92, 158), "tiles"), "office_carpet")
    save(wallpaper(), "wallpaper")
    save(paper(), "paper")
    save(cardboard(font, BOLD_IT, BOLD, MONO), "cardboard")


REMASTERED = ["brick", "wood_floor", "concrete", "metal", "ceiling", "carpet_red", "office_carpet", "wallpaper", "paper", "cardboard"]
