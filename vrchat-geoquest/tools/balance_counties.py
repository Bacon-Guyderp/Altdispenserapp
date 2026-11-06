"""Cap how many places any one county (or state) can have, so the game doesn't
turn into "guess which part of Raleigh". Thins each over-full county by repeatedly
dropping the place with the closest neighbour, which keeps the survivors spread out.

  python balance_counties.py --max-per-county 200
"""
import argparse, collections, math
from geo_common import load_locations, save_locations, DEFAULT_LOCATIONS


def thin(places, keep):
    pts = list(places)
    while len(pts) > keep:
        best_i, best_d = 0, 1e18
        for i, p in enumerate(pts):
            c = math.cos(math.radians(p["lat"]))
            d = min(((p["lat"] - q["lat"]) ** 2 + ((p["lon"] - q["lon"]) * c) ** 2)
                    for j, q in enumerate(pts) if j != i)
            if d < best_d:
                best_i, best_d = i, d
        pts.pop(best_i)
    return pts


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--max-per-county", type=int, default=200)
    ap.add_argument("--file", default=DEFAULT_LOCATIONS)
    a = ap.parse_args()
    locs = load_locations(a.file)
    groups = collections.defaultdict(list)
    for p in locs:
        groups[p.get("country") or "?"].append(p)
    out = []
    for name, ps in groups.items():
        if len(ps) > a.max_per_county:
            print(f"{name}: {len(ps)} -> {a.max_per_county}")
            ps = thin(ps, a.max_per_county)
        out.extend(ps)
    out.sort(key=lambda p: p["key"])
    save_locations(a.file, out)


if __name__ == "__main__":
    main()
