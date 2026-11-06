"""Harvest 360° street-level panoramas in North Carolina from Mapillary.

Mapillary imagery is CC BY-SA 4.0 and has lots of 360° coverage along NC roads,
which is exactly what a GeoGuessr-style game wants. Needs a free client token:
  mapillary.com/dashboard/developers  ->  register an app  ->  copy the "Client Token" (MLY|...)
  export MAPILLARY_TOKEN='MLY|...'
  python harvest_mapillary.py --target 5000

How it works: Mapillary's area search times out in cities, so we read its coverage
vector tiles instead (zoom 14, about 2 km across). Random tiles are drawn uniformly
over the state, so you get back roads and small towns as well as Charlotte. The
360° photos listed in a tile are filtered for spacing, and then each pick is
looked up for its download link, exact position and photographer.

Every place is inside North Carolina by default. --outside-fraction 0.25 would
mix in the neighbouring parts of VA, SC, TN and GA that the map shows.
"""
import argparse, math, os, random, sys, threading, time
from concurrent.futures import ThreadPoolExecutor
import requests
import mapbox_vector_tile
from geo_common import describe_nc, NCLookup, NC_BOUNDS, load_locations, save_locations, DEFAULT_LOCATIONS

GRAPH = "https://graph.mapillary.com/"
TILES = "https://tiles.mapillary.com/maps/vtp/mly1_public/2/{z}/{x}/{y}"
Z = 14
_local = threading.local()


def session():
    if not hasattr(_local, "s"):
        _local.s = requests.Session()
        _local.s.headers["User-Agent"] = "GeoQuestWorldBuilder/1.0"
    return _local.s


def get(url, token, params=None, binary=False):
    delay = 3
    for _ in range(6):
        try:
            r = session().get(url, params=dict(params or {}, access_token=token), timeout=90)
            if r.status_code == 200:
                return r.content if binary else r.json()
            if r.status_code in (401, 403):
                sys.exit(f"Mapillary refused the token (HTTP {r.status_code}): {r.text[:200]}")
            if r.status_code in (400, 404):
                return None
            print(f"  HTTP {r.status_code} {r.text[:80]}; retry in {delay}s", file=sys.stderr)
        except requests.RequestException as e:
            print(f"  {e}; retry in {delay}s", file=sys.stderr)
        time.sleep(delay)
        delay = min(delay * 2, 60)
    return None


def tile_of(lat, lon):
    n = 2 ** Z
    x = int((lon + 180) / 360 * n)
    y = int((1 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2 * n)
    return x, y


def tile_point(x, y, px, py, extent):
    """Tile-local point (origin bottom-left, as mapbox_vector_tile decodes) -> lat, lon."""
    n = 2 ** Z
    fx = x + px / extent
    fy = y + 1 - py / extent
    lon = fx / n * 360 - 180
    lat = math.degrees(math.atan(math.sinh(math.pi * (1 - 2 * fy / n))))
    return lat, lon


def panos_in_tile(token, x, y):
    data = get(TILES.format(z=Z, x=x, y=y), token, binary=True)
    if not data:
        return []
    layer = mapbox_vector_tile.decode(data).get("image")
    if not layer:
        return []
    extent = layer.get("extent", 4096)
    out = []
    for f in layer["features"]:
        p = f["properties"]
        if not p.get("is_pano") or (p.get("quality_score") or 1) < 0.4:
            continue
        px, py = f["geometry"]["coordinates"][:2]
        lat, lon = tile_point(x, y, px, py, extent)
        out.append((str(p["id"]), lat, lon))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--target", type=int, default=5000)
    ap.add_argument("--outside-fraction", type=float, default=0.0,
                    help="share of places from neighbouring states inside the map area")
    ap.add_argument("--min-spacing-km", type=float, default=0.8)
    ap.add_argument("--per-tile", type=int, default=6, help="max places from one ~2 km tile")
    ap.add_argument("--workers", type=int, default=8)
    ap.add_argument("--refill", action="store_true",
                    help="second pass: only revisit tiles that already gave places (use with smaller spacing)")
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--out", default=DEFAULT_LOCATIONS)
    a = ap.parse_args()
    token = os.environ.get("MAPILLARY_TOKEN", "").strip()
    if not token:
        sys.exit("Set MAPILLARY_TOKEN (free client token from mapillary.com/dashboard/developers).")

    nc = NCLookup()
    rng = random.Random(a.seed)
    out = load_locations(a.out)
    have = {p["key"] for p in out}
    outside = sum(1 for p in out if p.get("iso") != "NC")
    grid = {}

    def gkey(lat, lon):
        return (int(lat / 0.05), int(lon / 0.05))

    def too_close(lat, lon):
        gy, gx = gkey(lat, lon)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                for (y, x) in grid.get((gy + dy, gx + dx), []):
                    if math.hypot((y - lat) * 111, (x - lon) * 111 * math.cos(math.radians(lat))) < a.min_spacing_km:
                        return True
        return False

    for p in out:
        grid.setdefault(gkey(p["lat"], p["lon"]), []).append((p["lat"], p["lon"]))

    # Every z14 tile whose centre is on land inside the map box, NC first.
    b = NC_BOUNDS
    x0, y0 = tile_of(b["max_lat"], b["min_lon"])
    x1, y1 = tile_of(b["min_lat"], b["max_lon"])
    nc_tiles, out_tiles = [], []
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            lat, lon = tile_point(x, y, 0.5, 0.5, 1)
            _, key = nc.lookup(lat, lon)
            if key == "NC":
                nc_tiles.append((x, y))
            elif key:
                out_tiles.append((x, y))
    if a.refill:
        known = {tile_of(p["lat"], p["lon"]) for p in out}
        nc_tiles = [t for t in nc_tiles if t in known]
        out_tiles = [t for t in out_tiles if t in known]
    rng.shuffle(nc_tiles)
    rng.shuffle(out_tiles)
    print(f"{len(nc_tiles)} NC tiles, {len(out_tiles)} neighbouring tiles; starting from {len(out)} places", flush=True)

    def wanted_outside():
        return a.outside_fraction > 0 and outside < a.outside_fraction * max(1, len(out))

    searched = 0
    pool = ThreadPoolExecutor(a.workers)
    queue_nc, queue_out = iter(nc_tiles), iter(out_tiles)
    while len(out) < a.target:
        src = queue_out if wanted_outside() else queue_nc
        batch = [t for t, _ in zip(src, range(a.workers * 4))]
        if not batch:
            print("ran out of tiles")
            break
        results = pool.map(lambda t: (t, panos_in_tile(token, *t)), batch)
        picks = []
        for (tx, ty), panos in results:
            searched += 1
            rng.shuffle(panos)
            took = 0
            for pid, lat, lon in panos:
                if took >= a.per_tile:
                    break
                k = "m" + pid
                if k in have or too_close(lat, lon):
                    continue
                label, key = nc.lookup(lat, lon)
                if not key or (key != "NC") != wanted_outside():
                    continue
                grid.setdefault(gkey(lat, lon), []).append((lat, lon))
                have.add(k)
                picks.append((pid, label, key))
                took += 1
        details = pool.map(lambda pk: (pk, get(GRAPH + pk[0], token, {
            "fields": "id,thumb_2048_url,computed_geometry,geometry,creator,captured_at"})), picks)
        for (pid, label, key), d in details:
            if not d or not d.get("thumb_2048_url"):
                continue
            geo = d.get("computed_geometry") or d.get("geometry")
            lon, lat = geo["coordinates"][0], geo["coordinates"][1]
            label, key = nc.lookup(lat, lon)
            if not key or (a.outside_fraction == 0 and key != "NC"):
                continue
            out.append({"key": "m" + pid, "lat": round(lat, 6), "lon": round(lon, 6),
                        "title": describe_nc(lat, lon, label, outside=key != "NC"),
                        "country": label if key == "NC" else label + " (not NC!)", "iso": key,
                        "author": (d.get("creator") or {}).get("username") or "Mapillary contributor",
                        "license": "CC BY-SA 4.0", "page": f"https://www.mapillary.com/app/?pKey={pid}",
                        "src": d["thumb_2048_url"], "source": "Mapillary",
                        "captured": time.strftime("%Y-%m", time.gmtime((d.get("captured_at") or 0) / 1000))})
            if key != "NC":
                outside += 1
            if len(out) >= a.target:
                break
        print(f"{searched} tiles searched -> {len(out)} places", flush=True)
        if searched % 400 < a.workers * 4:
            save_locations(a.out, out)
    save_locations(a.out, out)


if __name__ == "__main__":
    main()
