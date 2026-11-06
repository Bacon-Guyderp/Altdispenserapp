"""Harvest geotagged 360x180 panoramas from Wikimedia Commons (North Carolina by default).

Every result is a freely licensed equirectangular photo with a camera
location. Output: ../Assets/GeoQuest/Data/locations.json, which the Unity
builder (GeoQuest > Build World) turns into the in-world location table and
build_site.py turns into a static image host.

Usage:  python harvest_commons.py --target 3000
Be nice to Wikimedia: the script throttles itself and backs off on 429s.
"""
import argparse, html, json, math, os, re, sys, time
import requests
from geo_common import CountryLookup, NCLookup, NC_BOUNDS, describe_nc, load_locations, save_locations, DEFAULT_LOCATIONS

API = "https://commons.wikimedia.org/w/api.php"
UA = "GeoQuestWorldBuilder/1.0 (VRChat geography game; https://github.com/bacon-guyderp/altdispenserapp)"
# Each search is run once per globe tile with nearcoord:, so every result is
# geotagged and places spread over the whole planet instead of piling up in Europe.
TEMPLATES = ["hastemplate:Pano360", 'incategory:"Spherical panoramas"', '"360x180"']


def tiles(step, b=None):
    """Centres of roughly equal-area tiles over bounds b (default: the inhabited world)."""
    import random
    b = b or {"min_lat": -60, "max_lat": 80, "min_lon": -180, "max_lon": 180}
    out = []
    lat = b["min_lat"] + step / 2
    while lat < b["max_lat"]:
        width = b["max_lon"] - b["min_lon"]
        n = max(1, int(width / (step / max(0.25, math.cos(math.radians(lat))))))
        for k in range(n):
            out.append((round(lat, 3), round(b["min_lon"] + (k + 0.5) * width / n, 3)))
        lat += step
    random.Random(42).shuffle(out)
    return out


# Indoor shots are near-impossible to place; skip them by default.
INDOOR = re.compile(r"interior|inside|innen|indoor|church nave|\bnave\b|museum|\bhall\b|\broom\b|"
                    r"kitchen|bedroom|lobby|cave|tunnel|stairs|staircase|corridor|crypt|chapel|"
                    r"basilica|cathedral|kirche|église|iglesia|chiesa|theatre|theater|library|"
                    r"station hall|mall|gallery|atrium|inner", re.I)
FREE = re.compile(r"^(cc0|cc[- ]by(-sa)?[- ]?\d|public domain|pd|attribution|gfdl|cc by)", re.I)


def get(session, params, tries=8):
    params = dict(params, format="json", formatversion=2, maxlag=5)
    delay = 5
    for _ in range(tries):
        r = session.get(API, params=params, timeout=60)
        if r.status_code == 200 and r.headers.get("content-type", "").startswith("application/json"):
            j = r.json()
            if j.get("error", {}).get("code") != "maxlag":
                return j
        print(f"  backing off {delay}s (HTTP {r.status_code})", file=sys.stderr)
        time.sleep(delay)
        delay = min(delay * 2, 120)
    raise RuntimeError("Commons API keeps refusing; try again later")


def clean(s, limit=90):
    s = html.unescape(re.sub(r"<[^>]+>", "", s or "")).strip()
    s = re.sub(r"\s+", " ", s)
    return s[:limit]


def nice_title(t):
    t = re.sub(r"^File:", "", t)
    t = re.sub(r"\.(jpe?g|png|tiff?|webp)$", "", t, flags=re.I)
    t = re.sub(r"[_]+", " ", t)
    t = re.sub(r"\b(360|360x180|360°|equirectangular|panorama|pano|sphere|spherical)\b", "", t, flags=re.I)
    t = re.sub(r"[\s\-–,()]+$", "", re.sub(r"\s{2,}", " ", t)).strip(" -–,")
    return t[:80] or "Unknown place"


def accept(page, a, countries, seen, cells, per_country, out):
    title = page.get("title", "")
    if title in seen or not page.get("coordinates") or not page.get("imageinfo"):
        return False
    seen.add(title)
    ii = page["imageinfo"][0]
    w, h = ii.get("width", 0), ii.get("height", 1)
    if w < 2000 or not (1.9 <= w / h <= 2.1):
        return False
    if not a.allow_indoor and INDOOR.search(title):
        return False
    meta = ii.get("extmetadata", {})
    lic = clean(meta.get("LicenseShortName", {}).get("value"), 40)
    if not FREE.search(lic):
        return False
    coords = page["coordinates"]
    c = next((x for x in coords if x.get("primary")), coords[0])
    lat, lon = round(c["lat"], 5), round(c["lon"], 5)
    if lat == 0 and lon == 0:
        return False
    if a.region == "nc":
        if not countries.in_bounds(lat, lon):
            return False
        name, iso = countries.lookup(lat, lon)
        if not iso:
            return False
        cell = (round(lat * 20), round(lon * 20))  # ~5 km cells
        outside = sum(v for k, v in per_country.items() if k != "NC")
        if iso != "NC" and outside >= a.outside_fraction * max(20, len(out)):
            return False
    else:
        name, iso = countries.lookup(lat, lon)
        cell = (math.floor(lat), math.floor(lon))
        if per_country.get(iso, 0) >= a.per_country:
            return False
    if cells.get(cell, 0) >= a.per_cell:
        return False
    cells[cell] = cells.get(cell, 0) + 1
    per_country[iso] = per_country.get(iso, 0) + 1
    nice = nice_title(title)
    if a.region == "nc" and nice == "Unknown place":
        nice = describe_nc(lat, lon, name, outside=iso != "NC")
    out.append({"lat": lat, "lon": lon, "title": nice, "country": name, "iso": iso, "source": "Wikimedia Commons",
                "author": clean(meta.get("Artist", {}).get("value"), 60) or "Unknown",
                "license": lic, "page": title,
                "src": ii.get("thumburl") or ii.get("url")})
    return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--region", choices=["nc", "world"], default="nc")
    ap.add_argument("--target", type=int, default=2500, help="stop when the table holds this many places")
    ap.add_argument("--tile", type=float, default=None, help="tile size in degrees (default 0.4 for nc, 6 for world)")
    ap.add_argument("--outside-fraction", type=float, default=0.25, help="share of places outside NC (nc region)")
    ap.add_argument("--per-tile", type=int, default=12, help="max places taken from one tile per query")
    ap.add_argument("--per-cell", type=int, default=2, help="max places per cell (~5 km for nc, 1 degree for world)")
    ap.add_argument("--per-country", type=int, default=120, help="max places per country")
    ap.add_argument("--allow-indoor", action="store_true")
    ap.add_argument("--sleep", type=float, default=1.5, help="seconds between API calls")
    ap.add_argument("--out", default=DEFAULT_LOCATIONS)
    a = ap.parse_args()
    a.tile = a.tile or (0.4 if a.region == "nc" else 6.0)

    countries = NCLookup() if a.region == "nc" else CountryLookup()
    s = requests.Session()
    s.headers["User-Agent"] = UA
    seen, cells, per_country, out = set(), {}, {}, []
    for p in load_locations(a.out):  # resume / extend an earlier harvest (or a Mapillary one)
        out.append(p)
        seen.add(p["page"])
        cell = (round(p["lat"] * 20), round(p["lon"] * 20)) if a.region == "nc" else (math.floor(p["lat"]), math.floor(p["lon"]))
        cells[cell] = cells.get(cell, 0) + 1
        per_country[p["iso"]] = per_country.get(p["iso"], 0) + 1
    print(f"starting from {len(out)} places")

    radius_km = int(a.tile * 111 * 0.75)
    work = [(t, q) for q in TEMPLATES for t in tiles(a.tile, NC_BOUNDS if a.region == "nc" else None)]
    for n, ((tlat, tlon), q) in enumerate(work):
        if len(out) >= a.target:
            break
        query = f"{q} nearcoord:{radius_km}km,{tlat},{tlon}"
        j = get(s, {"action": "query", "generator": "search", "gsrsearch": query, "gsrnamespace": 6,
                    "gsrlimit": 50, "prop": "coordinates|imageinfo", "coprop": "type",
                    "coprimary": "all", "colimit": "max", "iiprop": "url|size|extmetadata",
                    "iiextmetadatafilter": "Artist|LicenseShortName", "iiurlwidth": 2048})
        took = 0
        for page in (j.get("query") or {}).get("pages", []):
            if took >= a.per_tile:
                break
            if accept(page, a, countries, seen, cells, per_country, out):
                took += 1
        if took or n % 25 == 0:
            print(f"tile {n + 1}/{len(work)} ({tlat},{tlon}) +{took} -> {len(out)} places, "
                  f"{len(per_country)} countries", flush=True)
        if n % 40 == 0:
            save_locations(a.out, out, a.region)
        time.sleep(a.sleep)
    save_locations(a.out, out, a.region)


if __name__ == "__main__":
    main()
