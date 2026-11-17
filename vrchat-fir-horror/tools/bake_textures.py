"""Bakes every texture into the Unity package as data, so nothing depends on Unity importing PNGs.

  python bake_textures.py      (needs Pillow)

Reads tools/textures/*.png, caps them at 512 px (1024 for the skeleton), and writes them, PNG-encoded
and base64'd, one per line, to Assets/FirHorror/Data/textures_baked.txt:

    name | wrap (repeat/clamp) | width | height | base64 PNG

FirWorldBuilder.BakeTextures() decodes each line inside Unity, builds mipmaps, compresses for the current
platform (DXT1 on PC, ASTC 6x6 on Android/Quest) and saves it as a Texture2D asset in Generated/.
"""
import base64, io, os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "textures")
OUT = os.path.join(os.path.dirname(HERE), "Assets", "FirHorror", "Data", "textures_baked.txt")
CAP = {"skeleton": 1024}


def main():
    lines = ["# Fir Warning textures, baked by tools/bake_textures.py. name | wrap | width | height | base64 PNG"]
    total = 0
    for f in sorted(os.listdir(SRC)):
        if not f.endswith(".png"):
            continue
        name = f[:-4]
        im = Image.open(os.path.join(SRC, f)).convert("RGB")
        cap = CAP.get(name, 512)
        if max(im.size) > cap:
            s = cap / max(im.size)
            im = im.resize((max(4, int(im.width * s) // 4 * 4), max(4, int(im.height * s) // 4 * 4)), Image.LANCZOS)
        if im.width % 4 or im.height % 4:   # block compression wants multiples of 4
            im = im.resize((im.width // 4 * 4, im.height // 4 * 4), Image.LANCZOS)
        buf = io.BytesIO()
        im.save(buf, "PNG", optimize=True)
        wrap = "clamp" if name.startswith("poster") or name == "window_snow" else "repeat"
        lines.append(f"{name}|{wrap}|{im.width}|{im.height}|{base64.b64encode(buf.getvalue()).decode('ascii')}")
        total += len(buf.getvalue())
        print(f"{name:24s} {im.width}x{im.height}  {len(buf.getvalue()) // 1024} KB")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w") as fh:
        fh.write("\n".join(lines) + "\n")
    print(f"{len(lines) - 1} textures, {total // 1024} KB of PNG, {os.path.getsize(OUT) // 1024} KB baked")


if __name__ == "__main__":
    main()
