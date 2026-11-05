"""Shared helpers: Natural Earth country lookup (pure Python, no shapely)."""
import json, os, urllib.request

NE_URL = ("https://raw.githubusercontent.com/nvkelso/natural-earth-vector/"
          "master/geojson/ne_50m_admin_0_countries.geojson")
HERE = os.path.dirname(os.path.abspath(__file__))
CACHE = os.path.join(HERE, ".cache")


def load_countries():
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, "ne_50m_admin_0_countries.geojson")
    if not os.path.exists(path):
        req = urllib.request.Request(NE_URL, headers={"User-Agent": "GeoQuestBuilder/1.0"})
        with urllib.request.urlopen(req, timeout=120) as r, open(path, "wb") as f:
            f.write(r.read())
    with open(path, encoding="utf-8") as f:
        return json.load(f)["features"]


def polygons(feature):
    g = feature["geometry"]
    if g["type"] == "Polygon":
        return [g["coordinates"]]
    if g["type"] == "MultiPolygon":
        return g["coordinates"]
    return []


def _in_ring(lon, lat, ring):
    inside = False
    j = len(ring) - 1
    for i in range(len(ring)):
        xi, yi = ring[i][0], ring[i][1]
        xj, yj = ring[j][0], ring[j][1]
        if (yi > lat) != (yj > lat) and lon < (xj - xi) * (lat - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return inside


class CountryLookup:
    def __init__(self):
        self.items = []
        for f in load_countries():
            p = f["properties"]
            for poly in polygons(f):
                xs = [c[0] for c in poly[0]]
                ys = [c[1] for c in poly[0]]
                self.items.append(((min(xs), min(ys), max(xs), max(ys)), poly,
                                   p.get("NAME") or "", p.get("ISO_A2_EH") or p.get("ISO_A2") or ""))

    def lookup(self, lat, lon):
        for (x0, y0, x1, y1), poly, name, iso in self.items:
            if x0 <= lon <= x1 and y0 <= lat <= y1 and _in_ring(lon, lat, poly[0]):
                if not any(_in_ring(lon, lat, h) for h in poly[1:]):
                    return name, iso
        return "", ""
