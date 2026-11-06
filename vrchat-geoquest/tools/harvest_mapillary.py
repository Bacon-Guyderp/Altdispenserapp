"""Harvest 360° street-level panoramas in North Carolina from Mapillary.

Mapillary imagery is CC BY-SA 4.0 and has lots of 360° coverage along NC roads,
which is exactly what a GeoGuessr-style game wants. Needs a free client token:
  mapillary.com/dashboard/developers  ->  register an app  ->  copy the "Client Token" (MLY|...)
  export MAPILLARY_TOKEN='MLY|...'
  python harvest_mapillary.py --target 2500

Points are sampled uniformly over the state (so you get back roads and small towns,
not just Charlotte), and no two places are closer than --min-spacing-km.
By default 75% of places are in NC and 25% (--outside-fraction) come from the
neighbouring parts of VA, SC, TN and GA that the in-world map shows.
"""
import argparse, math, os, random, sys, time
import requests
from geo_common import describe_nc, NCLookup, NC_BOUNDS, load_locations, save_locations, DEFAULT_LOCATIONS

API = "https://graph.mapillary.com/images"


def query(s, token, lat, lon, half):
    params = {"fields": "id,computed_geometry,geometry,thumb_2048_url,creator,captured_at,is_pano",
              "bbox": f"{lon - half},{lat - half},{lon + half},{lat + half}",
              "is_pano": "true", "limit": 25}
    delay = 5
    for _ in range(6):
        try:
            r = s.get(API, params=params, headers={"Authorization": "OAuth " + token}, timeout=60)
            if r.status_code == 200:
                return r.json().get("data", [])
            if r.status_code in (401, 403):
                sys.exit(f"Mapillary refused the token (HTTP {r.status_code}): {r.text[:200]}")
            print(f"  HTTP {r.status_code}, waiting {delay}s", file=sys.stderr)
        except requests.RequestException as e:
            print(f"  {e}, waiting {delay}s", file=sys.stderr)
        time.sleep(delay)
        delay = min(delay * 2, 120)
    return []


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--target", type=int, default=2500)
    ap.add_argument("--outside-fraction", type=float, default=0.25,
                    help="share of places from neighbouring states inside the map area")
    ap.add_argument("--min-spacing-km", type=float, default=3.0)
    ap.add_argument("--search-km", type=float, default=4.0, help="half-size of each search box")
    ap.add_argument("--sleep", type=float, default=0.15)
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
    spaced = [(p["lat"], p["lon"]) for p in out]
    outside = sum(1 for p in out if p.get("iso") != "NC")
    s = requests.Session()
    s.headers["User-Agent"] = "GeoQuestWorldBuilder/1.0"
    b = NC_BOUNDS
    half = a.search_km / 111.0
    grid = {}  # spacing check via a coarse hash grid

    def gkey(lat, lon):
        return (int(lat / 0.05), int(lon / 0.05))

    for lat, lon in spaced:
        grid.setdefault(gkey(lat, lon), []).append((lat, lon))

    def too_close(lat, lon):
        gy, gx = gkey(lat, lon)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                for (y, x) in grid.get((gy + dy, gx + dx), []):
                    km = math.hypot((y - lat) * 111, (x - lon) * 111 * math.cos(math.radians(lat)))
                    if km < a.min_spacing_km:
                        return True
        return False

    attempts = 0
    while len(out) < a.target and attempts < a.target * 25:
        attempts += 1
        want_outside = outside < a.outside_fraction * len(out)
        lat = rng.uniform(b["min_lat"], b["max_lat"])
        lon = rng.uniform(b["min_lon"], b["max_lon"])
        label, key = nc.lookup(lat, lon)
        if not key or (key == "NC") == want_outside:
            continue  # ocean, or the wrong side of the border for this pick
        imgs = query(s, token, lat, lon, half)
        time.sleep(a.sleep)
        rng.shuffle(imgs)
        for im in imgs:
            geo = im.get("computed_geometry") or im.get("geometry")
            if not geo or not im.get("thumb_2048_url") or im.get("is_pano") is False:
                continue
            ilon, ilat = geo["coordinates"][0], geo["coordinates"][1]
            k = "m" + str(im["id"])
            if k in have or too_close(ilat, ilon) or not nc.in_bounds(ilat, ilon):
                continue
            label, key = nc.lookup(ilat, ilon)
            if not key:
                continue
            user = (im.get("creator") or {}).get("username") or "Mapillary contributor"
            out.append({"key": k, "lat": round(ilat, 6), "lon": round(ilon, 6),
                        "title": describe_nc(ilat, ilon, label, outside=key != "NC"),
                        "country": label if key == "NC" else label + " (not NC!)", "iso": key,
                        "author": user, "license": "CC BY-SA 4.0",
                        "page": f"https://www.mapillary.com/app/?pKey={im['id']}",
                        "src": im["thumb_2048_url"], "source": "Mapillary"})
            have.add(k)
            grid.setdefault(gkey(ilat, ilon), []).append((ilat, ilon))
            if key != "NC":
                outside += 1
            break
        if attempts % 50 == 0:
            print(f"{attempts} searches -> {len(out)} places ({outside} outside NC)", flush=True)
        if attempts % 500 == 0:
            save_locations(a.out, out)
    save_locations(a.out, out)


if __name__ == "__main__":
    main()
