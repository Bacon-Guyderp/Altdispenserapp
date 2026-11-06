"""Render the North Carolina guessing map (4096x2048, linear lat/lon over NC_BOUNDS).

Counties are drawn and labelled so locals can pin precisely. Neighbouring states are
greyed. Data: US Census county lines + Natural Earth states (both public domain).
Output: Assets/GeoQuest/Textures/NCMap.png
"""
import os
from PIL import Image, ImageDraw, ImageFont
from geo_common import CITIES, NEIGHBOR_CITIES, NC_BOUNDS, load_states, load_nc_counties, polygons

W, H = 4096, 2048
SS = 2
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "GeoQuest", "Textures", "NCMap.png")
FONTS = ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "C:/Windows/Fonts/arialbd.ttf"]
OCEAN, LAND_OUT, BORDER_OUT = (92, 140, 186), (206, 204, 196), (150, 146, 138)
COUNTY = [(236, 220, 170), (205, 226, 176), (242, 204, 178), (204, 216, 236), (230, 210, 232)]


def font(size):
    for p in FONTS:
        if os.path.exists(p):
            return ImageFont.truetype(p, size)
    return ImageFont.load_default()


def P(c):
    b = NC_BOUNDS
    return ((c[0] - b["min_lon"]) / (b["max_lon"] - b["min_lon"]) * W * SS,
            (b["max_lat"] - c[1]) / (b["max_lat"] - b["min_lat"]) * H * SS)


import sys
NEIGHBORS = "--neighbor-towns" in sys.argv
# Default: a bare map with nothing but county outlines (no names, towns or titles).
# --labels brings back towns, state names and the title.
LABELS = "--labels" in sys.argv
PLAIN_FILL = (226, 214, 180)
BLANK_BG = (236, 236, 232)   # plain background around NC (no neighbours, no ocean)


def main():
    img = Image.new("RGB", (W * SS, H * SS), OCEAN if LABELS else BLANK_BG)
    d = ImageDraw.Draw(img)
    states = load_states()
    for f in states:
        if f["properties"]["name"] == "North Carolina" or not LABELS:
            continue
        for poly in polygons(f):
            d.polygon([P(c) for c in poly[0]], fill=LAND_OUT, outline=BORDER_OUT)
    counties = load_nc_counties()
    nc = [f for f in states if f["properties"]["name"] == "North Carolina"][0]
    for poly in (polygons(nc) if LABELS else []):  # fills coastal slivers the county outlines miss
        d.polygon([P(c) for c in poly[0]], fill=COUNTY[0] if LABELS else PLAIN_FILL)
    for i, f in enumerate(sorted(counties, key=lambda f: f["properties"]["COUNTY"])):
        for poly in polygons(f):
            d.polygon([P(c) for c in poly[0]], fill=COUNTY[(i * 3) % len(COUNTY)] if LABELS else PLAIN_FILL)
    for f in counties:
        for poly in polygons(f):
            d.line([P(c) for c in poly[0]], fill=(120, 100, 85), width=2 * SS)
    if LABELS:
        for f in states:  # neighbours' outlines double as NC's land borders
            if f["properties"]["name"] == "North Carolina":
                continue
            for poly in polygons(f):
                d.line([P(c) for c in poly[0]], fill=(60, 50, 45), width=5 * SS)
    else:
        # NC on its own: the state outline is every county edge that only one county has,
        # so it lines up exactly with the county lines (no mismatched coastline).
        from collections import Counter
        def key(c):
            return (round(c[0], 5), round(c[1], 5))
        segs = Counter()
        for f in counties:
            for poly in polygons(f):
                for ring in poly:
                    for a, b in zip(ring, ring[1:]):
                        segs[tuple(sorted((key(a), key(b))))] += 1
        for (a, b), n in segs.items():
            if n == 1:
                d.line([P(a), P(b)], fill=(70, 58, 48), width=4 * SS)

    # county names (small), skipped where a city label will sit
    placed = []
    def free(box):
        return all(box[2] < b[0] or box[0] > b[2] or box[3] < b[1] or box[1] > b[3] for b in placed)

    if not LABELS:
        return save(img)

    # neighbouring state names
    for name, lat, lon in [("VIRGINIA", 36.95, -78.2), ("SOUTH CAROLINA", 34.5, -80.1), ("TENNESSEE", 36.7, -83.6),
                           ("GEORGIA", 34.1, -84.2), ("ATLANTIC OCEAN", 34.3, -76.2)]:
        placed.append(d.textbbox(P((lon, lat)), name, font=font(40 * SS), anchor="mm"))
        d.text(P((lon, lat)), name, font=font(40 * SS), fill=(110, 106, 100) if "OCEAN" not in name else (200, 225, 245),
               anchor="mm")

    city_font = {1: font(34 * SS), 2: font(28 * SS), 3: font(22 * SS)}
    for name, lat, lon, k in sorted(CITIES, key=lambda c: c[3]):
        x, y = P((lon, lat))
        r = (9 if k == 1 else 7 if k == 2 else 5) * SS
        d.ellipse([x - r, y - r, x + r, y + r], fill=(180, 30, 30), outline=(255, 255, 255), width=SS * 2)
        f = city_font[k]
        box = d.textbbox((x, y - r - 4 * SS), name, font=f, anchor="mb")
        if not free(box):
            box = d.textbbox((x, y + r + 4 * SS), name, font=f, anchor="mt")
            anchor, ty = "mt", y + r + 4 * SS
        else:
            anchor, ty = "mb", y - r - 4 * SS
        placed.append(box)
        d.text((x, ty), name, font=f, anchor=anchor, fill=(30, 20, 20), stroke_width=3 * SS, stroke_fill=(255, 255, 250))

    # neighbouring towns (only useful if you harvest with --outside-fraction > 0)
    for name, lat, lon, k in sorted(NEIGHBOR_CITIES if NEIGHBORS else [], key=lambda c: c[3]):
        x, y = P((lon, lat))
        r = (7 if k == 1 else 6 if k == 2 else 4) * SS
        d.ellipse([x - r, y - r, x + r, y + r], fill=(110, 70, 70), outline=(240, 240, 235), width=SS * 2)
        f = city_font[min(3, k + 1)]
        for anchor, ty in (("mb", y - r - 4 * SS), ("mt", y + r + 4 * SS)):
            box = d.textbbox((x, ty), name, font=f, anchor=anchor)
            if free(box):
                placed.append(box)
                d.text((x, ty), name, font=f, anchor=anchor, fill=(70, 60, 60), stroke_width=3 * SS, stroke_fill=(235, 233, 226))
                break

    # County names are off by default (outlines only); pass --county-names to print them.
    cf = font(15 * SS)
    for f in (counties if "--county-names" in sys.argv else []):
        name = f["properties"]["NAME"]
        best = max(polygons(f), key=lambda p: len(p[0]))[0]
        xs = [P(c)[0] for c in best]
        ys = [P(c)[1] for c in best]
        x, y = sum(xs) / len(xs), sum(ys) / len(ys)
        box = d.textbbox((x, y), name, font=cf, anchor="mm")
        if free(box):
            placed.append(box)
            d.text((x, y), name, font=cf, anchor="mm", fill=(95, 80, 65))

    d.text((40 * SS, 40 * SS), "NORTH CAROLINA", font=font(72 * SS), fill=(40, 60, 90), anchor="la",
           stroke_width=4 * SS, stroke_fill=(230, 240, 250))
    save(img)


def save(img):
    img = img.resize((W, H), Image.LANCZOS)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT, optimize=True)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
