"""Harvest geotagged 360x180 panoramas from Wikimedia Commons.

Every result is a freely licensed equirectangular photo with a camera
location. Output: ../Assets/GeoQuest/Data/locations.json, which the Unity
builder (GeoQuest > Build World) turns into the in-world location table and
build_site.py turns into a static image host.

Usage:  python harvest_commons.py --target 3000
Be nice to Wikimedia: the script throttles itself and backs off on 429s.
"""
import argparse, html, json, math, os, re, sys, time
import requests
from geo_common import CountryLookup

API = "https://commons.wikimedia.org/w/api.php"
UA = "GeoQuestWorldBuilder/1.0 (VRChat geography game; https://github.com/bacon-guyderp/altdispenserapp)"
# Each search is run once per globe tile with nearcoord:, so every result is
# geotagged and places spread over the whole planet instead of piling up in Europe.
TEMPLATES = ["hastemplate:Pano360", 'incategory:"Spherical panoramas"', '"360x180"']


def tiles(step):
    """Centres of roughly equal-area tiles, shuffled deterministically."""
    import random
    out = []
    lat = -60 + step / 2
    while lat < 80:
        n = max(1, int(360 / (step / max(0.25, math.cos(math.radians(lat))))))
        for k in range(n):
            out.append((round(lat, 2), round(-180 + (k + 0.5) * 360 / n, 2)))
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
    cell = (math.floor(lat), math.floor(lon))
    name, iso = countries.lookup(lat, lon)
    if cells.get(cell, 0) >= a.per_cell or per_country.get(iso, 0) >= a.per_country:
        return False
    cells[cell] = cells.get(cell, 0) + 1
    per_country[iso] = per_country.get(iso, 0) + 1
    out.append({"lat": lat, "lon": lon, "title": nice_title(title), "country": name, "iso": iso,
                "author": clean(meta.get("Artist", {}).get("value"), 60) or "Unknown",
                "license": lic, "page": title,
                "src": ii.get("thumburl") or ii.get("url")})
    return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--target", type=int, default=2500, help="stop after this many accepted places")
    ap.add_argument("--tile", type=float, default=6.0, help="globe tile size in degrees")
    ap.add_argument("--per-tile", type=int, default=12, help="max places taken from one tile per query")
    ap.add_argument("--per-cell", type=int, default=2, help="max places per 1x1 degree cell")
    ap.add_argument("--per-country", type=int, default=120, help="max places per country")
    ap.add_argument("--allow-indoor", action="store_true")
    ap.add_argument("--sleep", type=float, default=1.5, help="seconds between API calls")
    ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                    "Assets", "GeoQuest", "Data", "locations.json"))
    a = ap.parse_args()

    countries = CountryLookup()
    s = requests.Session()
    s.headers["User-Agent"] = UA
    seen, cells, per_country, out = set(), {}, {}, []
    if os.path.exists(a.out):  # resume / extend an earlier harvest
        for p in json.load(open(a.out, encoding="utf-8"))["locations"]:
            out.append(p)
            seen.add(p["page"])
            cell = (math.floor(p["lat"]), math.floor(p["lon"]))
            cells[cell] = cells.get(cell, 0) + 1
            per_country[p["iso"]] = per_country.get(p["iso"], 0) + 1
        print(f"resuming with {len(out)} places")

    radius_km = int(a.tile * 111 * 0.75)
    work = [(t, q) for q in TEMPLATES for t in tiles(a.tile)]
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
            save(a.out, out)
        time.sleep(a.sleep)
    save(a.out, out)


def save(path, out):
    for i, p in enumerate(out):
        p["id"] = i
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump({"generator": "harvest_commons.py", "count": len(out), "locations": out},
                  f, ensure_ascii=False, indent=0)
    print("saved", len(out), "->", path)


if __name__ == "__main__":
    main()
