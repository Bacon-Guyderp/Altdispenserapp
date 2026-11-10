"""County picker data for the in-world map (same lon/lat frame as NCMap.png):

  Textures/CountyIds.png        1024x512, each pixel = county index (0-99), 255 = not NC
  Textures/CountySelection.png  128x1, pixel i = white if county i is in play (edited at runtime)
  Data/counties.json            county names, in index order
"""
import json, os
from PIL import Image, ImageDraw
from geo_common import NC_BOUNDS as B, load_nc_counties, polygons

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(HERE, "..", "Assets", "GeoQuest")
W, H = 1024, 512


def main():
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
    with open(os.path.join(ASSETS, "Data", "counties.json"), "w", encoding="utf-8") as f:
        json.dump({"names": names}, f, indent=0)
    print("wrote county ids, selection texture and", len(names), "names")


if __name__ == "__main__":
    main()
