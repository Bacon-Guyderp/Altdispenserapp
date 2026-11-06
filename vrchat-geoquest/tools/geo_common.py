"""Shared helpers: Natural Earth country lookup (pure Python, no shapely)."""
import json, math, os, urllib.request

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


# ---------------------------------------------------------------- North Carolina

ADMIN1_URL = ("https://raw.githubusercontent.com/nvkelso/natural-earth-vector/"
              "master/geojson/ne_50m_admin_1_states_provinces.geojson")
# US Census county boundaries (public domain), packaged as GeoJSON by plotly.
COUNTIES_URL = "https://raw.githubusercontent.com/plotly/datasets/master/geojson-counties-fips.json"

# The in-world map covers exactly this box. It is 2:1 on the ground at NC's latitude
# (9.6 deg lon * cos 35.2 deg = 7.84 "km-equal" degrees, / 3.9 deg lat = 2.0), so the
# board stays a 2x1 m rectangle without stretching the state.
NC_BOUNDS = {"min_lon": -84.7, "max_lon": -75.1, "min_lat": 33.25, "max_lat": 37.15}
NC_SCORE_KM = 90.0  # 5000 * e^(-km/90): 10 km ~ 4,500 pts, 50 km ~ 2,900, 200 km ~ 540


def _cached(name, url):
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, name)
    if not os.path.exists(path):
        req = urllib.request.Request(url, headers={"User-Agent": "GeoQuestBuilder/1.0"})
        with urllib.request.urlopen(req, timeout=180) as r, open(path, "wb") as f:
            f.write(r.read())
    with open(path, encoding="utf-8") as f:
        return json.load(f)["features"]


def load_states():
    return [f for f in _cached("admin1.geojson", ADMIN1_URL) if f["properties"].get("iso_a2") == "US"]


def load_nc_counties():
    return [f for f in _cached("counties.geojson", COUNTIES_URL) if f["properties"].get("STATE") == "37"]


class _PolyIndex:
    def __init__(self, feats, label):
        self.items = []
        for f in feats:
            for poly in polygons(f):
                xs = [c[0] for c in poly[0]]
                ys = [c[1] for c in poly[0]]
                self.items.append(((min(xs), min(ys), max(xs), max(ys)), poly, label(f)))

    def lookup(self, lat, lon):
        for (x0, y0, x1, y1), poly, lab in self.items:
            if x0 <= lon <= x1 and y0 <= lat <= y1 and _in_ring(lon, lat, poly[0]):
                if not any(_in_ring(lon, lat, h) for h in poly[1:]):
                    return lab
        return None


class NCLookup:
    """Answers 'is this in North Carolina, and which county / state is it?'"""

    def __init__(self):
        self.counties = _PolyIndex(load_nc_counties(), lambda f: f["properties"]["NAME"] + " County")
        self.states = _PolyIndex(load_states(), lambda f: f["properties"]["name"])

    def lookup(self, lat, lon):
        """Returns (place label, region key). Key is 'NC' inside the state, else the state name."""
        county = self.counties.lookup(lat, lon)
        if county:
            return county + ", NC", "NC"
        state = self.states.lookup(lat, lon)
        if state == "North Carolina":  # coastline slivers the county outlines miss
            return "North Carolina", "NC"
        return (state or ""), (state or "")

    def in_bounds(self, lat, lon):
        b = NC_BOUNDS
        return b["min_lat"] <= lat <= b["max_lat"] and b["min_lon"] <= lon <= b["max_lon"]


# ---------------------------------------------------------------- locations.json

def load_locations(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding="utf-8") as f:
        return json.load(f).get("locations", [])


def save_locations(path, out, region="nc"):
    """ids are positions in the table; 'file' is the stable photo name on the site."""
    import hashlib
    for i, p in enumerate(out):
        p["id"] = i
        if not p.get("key"):
            p["key"] = "c" + hashlib.md5(p["page"].encode("utf-8")).hexdigest()[:12]
        p["file"] = p["key"] + ".jpg"
    meta = {"generator": "geoquest", "region": region, "count": len(out), "locations": out}
    if region == "nc":
        meta["bounds"] = NC_BOUNDS
        meta["score_km"] = NC_SCORE_KM
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=0)
    print("saved", len(out), "->", path, flush=True)


DEFAULT_LOCATIONS = os.path.join(HERE, "..", "Assets", "GeoQuest", "Data", "locations.json")


CITIES = [  # name, lat, lon, size class (1 = big)
    ("Charlotte", 35.227, -80.843, 1), ("Raleigh", 35.780, -78.639, 1), ("Greensboro", 36.073, -79.792, 1),
    ("Durham", 35.994, -78.899, 2), ("Winston-Salem", 36.100, -80.244, 2), ("Fayetteville", 35.053, -78.878, 2),
    ("Wilmington", 34.226, -77.945, 1), ("Asheville", 35.595, -82.551, 1), ("Greenville", 35.613, -77.366, 2),
    ("Cary", 35.792, -78.781, 3), ("High Point", 35.956, -80.005, 3), ("Concord", 35.409, -80.579, 3),
    ("Gastonia", 35.262, -81.187, 3), ("Jacksonville", 34.754, -77.430, 2), ("Chapel Hill", 35.913, -79.056, 3),
    ("Rocky Mount", 35.938, -77.791, 3), ("Boone", 36.217, -81.675, 2), ("Hickory", 35.733, -81.341, 2),
    ("New Bern", 35.108, -77.044, 2), ("Elizabeth City", 36.295, -76.251, 2), ("Nags Head", 35.957, -75.624, 3),
    ("Cape Hatteras", 35.251, -75.529, 3), ("Murphy", 35.088, -84.035, 3), ("Lumberton", 34.618, -79.009, 3),
    ("Goldsboro", 35.385, -77.993, 3), ("Salisbury", 35.671, -80.474, 3), ("Burlington", 36.096, -79.437, 3),
    ("Wilson", 35.721, -77.916, 3), ("Morehead City", 34.723, -76.726, 3), ("Sanford", 35.480, -79.180, 3),
    ("Southern Pines", 35.174, -79.392, 3), ("Statesville", 35.783, -80.887, 3), ("Morganton", 35.745, -81.685, 3),
    ("Franklin", 35.182, -83.382, 3), ("Bryson City", 35.431, -83.448, 3), ("Mount Airy", 36.499, -80.607, 3),
    ("Roanoke Rapids", 36.461, -77.654, 3), ("Southport", 33.921, -78.020, 3), ("Ocracoke", 35.115, -75.981, 3),
]


# Towns just over the line, labelled on the map and used to describe out-of-state places.
NEIGHBOR_CITIES = [
    ("Danville, VA", 36.586, -79.395, 2), ("Martinsville, VA", 36.691, -79.873, 3), ("South Boston, VA", 36.699, -78.901, 3),
    ("Norfolk, VA", 36.851, -76.286, 1), ("Virginia Beach, VA", 36.853, -75.978, 2), ("Suffolk, VA", 36.728, -76.583, 3),
    ("Emporia, VA", 36.686, -77.542, 3), ("Galax, VA", 36.661, -80.924, 3), ("Abingdon, VA", 36.710, -81.975, 3),
    ("Bristol, TN/VA", 36.596, -82.188, 2), ("Johnson City, TN", 36.313, -82.353, 2), ("Kingsport, TN", 36.548, -82.562, 3),
    ("Knoxville, TN", 35.961, -83.921, 1), ("Gatlinburg, TN", 35.714, -83.511, 3), ("Morristown, TN", 36.214, -83.295, 3),
    ("Blairsville, GA", 34.876, -83.958, 3), ("Clayton, GA", 34.878, -83.401, 3),
    ("Gainesville, GA", 34.298, -83.824, 2), ("Athens, GA", 33.961, -83.378, 2), ("Toccoa, GA", 34.577, -83.332, 3),
    ("Greenville, SC", 34.853, -82.394, 1), ("Spartanburg, SC", 34.950, -81.932, 2), ("Rock Hill, SC", 34.925, -81.025, 2),
    ("Columbia, SC", 34.000, -81.035, 1), ("Florence, SC", 34.195, -79.763, 2), ("Myrtle Beach, SC", 33.689, -78.887, 1),
    ("Anderson, SC", 34.503, -82.650, 3), ("Clemson, SC", 34.683, -82.837, 3), ("Sumter, SC", 33.920, -80.341, 3),
    ("Conway, SC", 33.836, -79.048, 3), ("Lancaster, SC", 34.721, -80.771, 3), ("Gaffney, SC", 35.072, -81.650, 3),
    ("Georgetown, SC", 33.377, -79.294, 3), ("Camden, SC", 34.246, -80.607, 3), ("Orangeburg, SC", 33.491, -80.856, 3),
    ("Aiken, SC", 33.560, -81.720, 3),
]


def describe_nc(lat, lon, label, outside=False):
    """'12 km from Raleigh' style title for a spot (neighbouring-state towns when outside NC)."""
    best, bd, best_size = None, 1e9, 3
    for name, clat, clon, size in (NEIGHBOR_CITIES if outside else CITIES):
        km = math.hypot((clat - lat) * 111, (clon - lon) * 111 * math.cos(math.radians(lat)))
        if km * (0.6, 0.8, 1.0)[size - 1] < bd * (0.6, 0.8, 1.0)[best_size - 1] if best else True:
            best, bd, best_size = name, km, size
    if bd < 3:
        return best
    if bd < (150 if outside else 60):
        return f"{round(bd)} km from {best}"
    return label or "North Carolina"
