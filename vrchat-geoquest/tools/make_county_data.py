"""County picker data for the in-world map (same lon/lat frame as NCMap.png):

  Textures/CountyIds.png        1024x512, each pixel = county index (0-99), 255 = not NC
  Textures/CountySelection.png  128x1, pixel i = white if county i is in play (edited at runtime)
  Data/counties.json            county names in index order, plus the county lookup grid baked in
                                (512x256, base64, bottom row first) so Unity never has to read the texture

  python make_county_data.py              rebuild everything (downloads county shapes)
  python make_county_data.py --grid-only  just re-bake the grid from the existing CountyIds.png
"""
import argparse, base64, json, os
from PIL import Image, ImageDraw
from geo_common import NC_BOUNDS as B, load_nc_counties, polygons

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(HERE, "..", "Assets", "GeoQuest")
W, H = 1024, 512
GW, GH = 512, 256


def write_counties_json(names):
    """counties.json with the names and the lookup grid sampled exactly like the Unity builder used to."""
    ids = Image.open(os.path.join(ASSETS, "Textures", "CountyIds.png")).convert("RGB")
    iw, ih = ids.size
    px = ids.load()
    grid = bytearray(GW * GH)
    for y in range(GH):                      # y = 0 is the southern (bottom) row, like Texture2D.GetPixels
        row = ih - 1 - (y * ih // GH)
        for x in range(GW):
            grid[y * GW + x] = px[x * iw // GW, row][0]
    with open(os.path.join(ASSETS, "Data", "counties.json"), "w", encoding="utf-8") as f:
        json.dump({"names": names, "gridW": GW, "gridH": GH,
                   "grid": base64.b64encode(bytes(grid)).decode("ascii")}, f, indent=0)
    return sum(1 for g in grid if g < len(names))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--grid-only", action="store_true")
    if ap.parse_args().grid_only:
        with open(os.path.join(ASSETS, "Data", "counties.json"), encoding="utf-8") as f:
            names = json.load(f)["names"]
        print("baked grid:", write_counties_json(names), "NC cells")
        return
    feats = sorted(load_nc_counties(), key=lambda f: f["properties"]["NAME"])
    names = [f["properties"]["NAME"] for f in feats]
    assert len(names) == 100
    img = Image.new("L", (W, H), 255)
    d = ImageDraw.Draw(img)
    P = lambda c: ((c[0] - B["min_lon"]) / (B["max_lon"] - B["min_lon"]) * W,
                   (B["max_lat"] - c[1]) / (B["max_lat"] - B["min_lat"]) * H)
    for i, f in enumerate(feats):
        for poly in polygons(f):
            d.polygon([P(c) for c in poly[0]], fill=i, outline=i)
    img.convert("RGB").save(os.path.join(ASSETS, "Textures", "CountyIds.png"), optimize=True)
    Image.new("RGBA", (128, 1), (255, 255, 255, 255)).save(os.path.join(ASSETS, "Textures", "CountySelection.png"))
    write_counties_json(names)
    print("wrote county ids, selection texture and", len(names), "names")


if __name__ == "__main__":
    main()
