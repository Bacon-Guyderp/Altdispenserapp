"""Image export via Pillow (every format Pillow can write)."""
from __future__ import annotations

import os

from PIL import Image

_CANDIDATES = {
    "png": "PNG", "bmp": "BMP", "tga": "TGA", "tif": "TIFF", "gif": "GIF", "webp": "WEBP",
    "jpg": "JPEG", "dds": "DDS", "ico": "ICO", "ppm": "PPM", "pcx": "PCX", "qoi": "QOI",
}


def _supported() -> dict[str, str]:
    Image.init()
    out = {}
    for ext, fmt in _CANDIDATES.items():
        if fmt in Image.SAVE:
            out[ext] = fmt
    return out


IMAGE_FORMATS = _supported()


def save_image(img: Image.Image, path: str, fmt: str | None = None, scale: int = 1) -> str:
    ext = (fmt or os.path.splitext(path)[1].lstrip(".")).lower()
    if ext == "jpeg":
        ext = "jpg"
    if ext == "tiff":
        ext = "tif"
    pil_fmt = IMAGE_FORMATS.get(ext)
    if pil_fmt is None:
        raise ValueError(f"unsupported image format {ext}")
    if not path.lower().endswith("." + ext):
        path = os.path.splitext(path)[0] + "." + ext
    if scale > 1:
        img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    out = img
    if pil_fmt in ("JPEG", "PPM", "PCX"):
        bg = Image.new("RGB", img.size, (0, 0, 0))
        bg.paste(img, mask=img.getchannel("A"))
        out = bg
    elif pil_fmt == "ICO":
        out = img.resize((min(img.width, 256), min(img.height, 256)), Image.NEAREST)
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    out.save(path, pil_fmt)
    return path
