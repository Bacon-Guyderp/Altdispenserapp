"""Turn a tightly cropped NC county-outline image into the in-world map texture.

The source (art/nc_county_outlines.png) is in Web-Mercator-style coordinates and
cropped to North Carolina's outline. This fits it to NC's real extent, then
resamples it into the map's own frame (linear lon/lat over NC_BOUNDS, 4096x2048)
so pins and scoring line up. The background stays white.

  python warp_outline_map.py [--src art/nc_county_outlines.png]
"""
import argparse, math, os
import numpy as np
from PIL import Image
from scipy import ndimage
from geo_common import NC_BOUNDS, load_nc_counties, polygons

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "GeoQuest", "Textures", "NCMap.png")


def merc_y(lat):
    return np.degrees(np.log(np.tan(np.pi / 4 + np.radians(lat) / 2)))


def fit(img, W, H):
    """Find the lon / Mercator-y extent of the image by lining up real county borders
    (US Census) with its drawn lines: start from NC's bounding box, then nudge the four
    edges to minimise the distance from border points to the nearest drawn line."""
    from scipy import optimize
    dist = ndimage.distance_transform_edt(~(img.mean(axis=2) < 200))
    pts = []
    for f in load_nc_counties():
        for p in polygons(f):
            r = p[0]
            for a, b in zip(r, r[1:]):
                for t in (0, 0.33, 0.66):
                    pts.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    pts = np.array(pts)
    lon, my = pts[:, 0], merc_y(pts[:, 1])
    L0, L1, M0, M1 = lon.min(), lon.max(), my.min(), my.max()

    def err(q):
        X = (lon - (L0 + q[0])) / ((L1 + q[1]) - (L0 + q[0])) * (W - 1)
        Y = ((M1 + q[3]) - my) / ((M1 + q[3]) - (M0 + q[2])) * (H - 1)
        return np.mean(np.minimum(ndimage.map_coordinates(dist, [Y, X], order=1, mode="nearest"), 30))

    simplex = np.vstack([np.zeros(4), np.eye(4) * 0.05])
    q = optimize.minimize(err, np.zeros(4), method="Nelder-Mead",
                          options={"xatol": 1e-4, "fatol": 1e-3, "maxiter": 2000, "initial_simplex": simplex}).x
    print(f"fit: mean distance {err(np.zeros(4)):.1f}px -> {err(q):.1f}px")
    return L0 + q[0], L1 + q[1], M0 + q[2], M1 + q[3]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=os.path.join(HERE, "art", "nc_county_outlines.png"))
    ap.add_argument("--width", type=int, default=4096)
    a = ap.parse_args()

    src = Image.open(a.src).convert("RGBA")
    white = Image.new("RGBA", src.size, (255, 255, 255, 255))
    white.alpha_composite(src)
    img = np.asarray(white.convert("RGB"), dtype=np.float32)
    H, W = img.shape[:2]

    lon0, lon1, my0, my1 = fit(img, W, H)
    b = NC_BOUNDS
    OW, OH = a.width, a.width // 2
    lon = b["min_lon"] + (np.arange(OW) + 0.5) / OW * (b["max_lon"] - b["min_lon"])
    lat = b["max_lat"] - (np.arange(OH) + 0.5) / OH * (b["max_lat"] - b["min_lat"])
    LON, LAT = np.meshgrid(lon, lat)
    sx = (LON - lon0) / (lon1 - lon0) * (W - 1)
    sy = (my1 - merc_y(LAT)) / (my1 - my0) * (H - 1)
    out = np.empty((OH, OW, 3), dtype=np.float32)
    for ch in range(3):
        out[..., ch] = ndimage.map_coordinates(img[..., ch], [sy, sx], order=1, mode="constant", cval=255.0)
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(OUT, optimize=True)
    print("wrote", OUT, (OW, OH))


if __name__ == "__main__":
    main()
