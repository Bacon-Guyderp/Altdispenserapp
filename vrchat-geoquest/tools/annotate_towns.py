"""Add each place's real town to locations.json.

Uses official US Census Bureau town boundaries (cities, towns, villages and
census-designated places: art/nc_places.json.gz, public domain). For every place:
  town     the town it is inside, or else the nearest town
  town_km  0 when inside the town limits, otherwise distance to the town's boundary
The game shows "Clayton" or "2.1 mi from Kenly" from these, in the player's units.

  python annotate_towns.py [--file locations.json]
"""
import argparse, gzip, json, math, os
import numpy as np
from geo_common import DEFAULT_LOCATIONS

HERE = os.path.dirname(os.path.abspath(__file__))
KX = 111.32 * math.cos(math.radians(35.5))   # km per degree of longitude at NC's latitude
KY = 110.57                                  # km per degree of latitude


def load_places():
    with gzip.open(os.path.join(HERE, "art", "nc_places.json.gz"), "rt", encoding="utf-8") as f:
        raw = json.load(f)["places"]
    places = []
    for p in raw:
        a, b = [], []
        for poly in p["polys"]:
            for ring in poly:
                r = np.array(ring, dtype=np.float64) * [KX, KY]
                a.append(r[:-1]); b.append(r[1:])
        a, b = np.vstack(a), np.vstack(b)
        places.append((p["name"], a, b, a.min(0), a.max(0)))
    return places


def locate(x, y, places, reach=40.0):
    """(town, km): km is 0 inside the town limits, else distance to the nearest town's boundary."""
    best, best_d = None, 1e9
    p = np.array([x, y])
    for name, a, b, lo, hi in places:
        if x < lo[0] - reach or x > hi[0] + reach or y < lo[1] - reach or y > hi[1] + reach:
            continue
        # inside? even-odd ray cast over every ring (so holes count correctly)
        cond = (a[:, 1] > y) != (b[:, 1] > y)
        if cond.any():
            xi = a[cond, 0] + (y - a[cond, 1]) * (b[cond, 0] - a[cond, 0]) / (b[cond, 1] - a[cond, 1])
            if (np.count_nonzero(xi > x) % 2) == 1:
                return name, 0.0
        ab = b - a
        t = np.clip(((p - a) * ab).sum(1) / np.maximum((ab * ab).sum(1), 1e-12), 0, 1)
        d = np.sqrt((((a + ab * t[:, None]) - p) ** 2).sum(1)).min()
        if d < best_d:
            best, best_d = name, d
    if best is None:
        return locate(x, y, places, reach * 4) if reach < 500 else ("", -1.0)
    return best, round(float(best_d), 2)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--file", default=DEFAULT_LOCATIONS)
    a = ap.parse_args()
    places = load_places()
    with open(a.file, encoding="utf-8") as f:
        meta = json.load(f)
    inside = 0
    for p in meta["locations"]:
        town, km = locate(p["lon"] * KX, p["lat"] * KY, places)
        p["town"], p["town_km"] = town, km
        inside += km == 0
    with open(a.file, "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=0)
    n = len(meta["locations"])
    print(f"{n} places: {inside} inside a town's limits, {n - inside} outside (nearest town + distance)")


if __name__ == "__main__":
    main()
