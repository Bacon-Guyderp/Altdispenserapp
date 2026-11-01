"""Generates assets/icon.ico (run once; the result is committed)."""
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))


def make(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    s = size / 64
    colors = [(230, 40, 40), (40, 160, 70), (30, 90, 200), (240, 190, 30)]
    pts = [(32, 4), (60, 32), (32, 60), (4, 32)]
    for i, c in enumerate(colors):
        a, b = pts[i], pts[(i + 1) % 4]
        d.polygon([(32 * s, 32 * s), (a[0] * s, a[1] * s), (b[0] * s, b[1] * s)], fill=c)
    try:
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", int(22 * s))
    except OSError:
        font = ImageFont.load_default()
    d.text((32 * s, 33 * s), "64", fill="white", font=font, anchor="mm", stroke_width=max(1, int(2 * s)),
           stroke_fill=(20, 20, 20))
    return img


if __name__ == "__main__":
    out = os.path.join(HERE, "..", "assets", "icon.ico")
    make(256).save(out, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    make(256).save(os.path.join(HERE, "..", "assets", "icon.png"))
    print("wrote", out)
