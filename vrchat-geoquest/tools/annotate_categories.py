"""Sort every place into a category for the in-game Categories filter.

  highway   on an interstate or US highway (add NC routes with --road-types IUS) (US Census TIGER/Line
            primary + secondary roads, art/nc_highways.json.gz, public domain)
  town      inside a city / town / village / CDP's limits (town_km == 0, from annotate_towns.py)
  remote    "middle of nowhere": outside towns and at least --remote-km from the nearest one
  country   everything else: country roads outside towns

Highway wins over town (an interstate through Raleigh counts as highway).
Run after annotate_towns.py.   python annotate_categories.py [--file locations.json]
"""
import argparse, collections, gzip, json, math, os
import numpy as np
from geo_common import DEFAULT_LOCATIONS

HERE = os.path.dirname(os.path.abspath(__file__))
KX = 111.32 * math.cos(math.radians(35.5))
KY = 110.57
CELL = 0.5   # km grid for the road index


def load_roads(types):
    with gzip.open(os.path.join(HERE, "art", "nc_highways.json.gz"), "rt", encoding="utf-8") as f:
        lines = [ln for ln in json.load(f)["lines"] if ln["t"] in types]
    a, b = [], []
    for ln in lines:
        p = np.array(ln["p"], dtype=np.float64) * [KX, KY]
        a.append(p[:-1]); b.append(p[1:])
    a, b = np.vstack(a), np.vstack(b)
    grid = collections.defaultdict(list)
    lo = np.floor(np.minimum(a, b) / CELL).astype(int)
    hi = np.floor(np.maximum(a, b) / CELL).astype(int)
    for i in range(len(a)):
        for gx in range(lo[i, 0], hi[i, 0] + 1):
            for gy in range(lo[i, 1], hi[i, 1] + 1):
                grid[(gx, gy)].append(i)
    return a, b, grid


def road_km(x, y, a, b, grid):
    gx, gy = int(math.floor(x / CELL)), int(math.floor(y / CELL))
    idx = set()
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            idx.update(grid.get((gx + dx, gy + dy), ()))
    if not idx:
        return 99.0
    idx = np.fromiter(idx, int)
    A, B = a[idx], b[idx]
    ab = B - A
    p = np.array([x, y])
    t = np.clip(((p - A) * ab).sum(1) / np.maximum((ab * ab).sum(1), 1e-12), 0, 1)
    return float(np.sqrt((((A + ab * t[:, None]) - p) ** 2).sum(1)).min())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--file", default=DEFAULT_LOCATIONS)
    ap.add_argument("--highway-m", type=float, default=40, help="max distance from a highway's centre line")
    ap.add_argument("--road-types", default="IU",
                    help="which roads count as highways: I = interstates, U = US highways, S = NC highways")
    ap.add_argument("--remote-km", type=float, default=8, help="min distance from any town for 'middle of nowhere'")
    a = ap.parse_args()
    ra, rb, grid = load_roads(set(a.road_types))
    with open(a.file, encoding="utf-8") as f:
        meta = json.load(f)
    counts = collections.Counter()
    for p in meta["locations"]:
        d = road_km(p["lon"] * KX, p["lat"] * KY, ra, rb, grid)
        tk = p.get("town_km", -1)
        if d * 1000 <= a.highway_m:
            cat = "highway"
        elif tk == 0:
            cat = "town"
        elif tk is not None and tk >= a.remote_km:
            cat = "remote"
        else:
            cat = "country"
        p["cat"] = cat
        counts[(cat, p.get("kind", "pano"))] += 1
    with open(a.file, "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=0)
    for c in ("highway", "town", "country", "remote"):
        print(f"{c:8s} {counts[(c, 'pano')]:5d} 360°  {counts[(c, 'flat')]:5d} flat")


if __name__ == "__main__":
    main()
