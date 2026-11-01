"""N64 RDP texture decoding (RGBA16/32, IA4/8/16, I4/8, CI4/8, YUV16)."""
from __future__ import annotations

import numpy as np
from PIL import Image

# G_IM_FMT_*
FMT_RGBA, FMT_YUV, FMT_CI, FMT_IA, FMT_I = range(5)
FMT_NAMES = {FMT_RGBA: "RGBA", FMT_YUV: "YUV", FMT_CI: "CI", FMT_IA: "IA", FMT_I: "I"}
SIZ_BITS = {0: 4, 1: 8, 2: 16, 3: 32}

# Combinations the RDP can actually sample from
TEXTURE_FORMATS = [
    "RGBA16", "RGBA32", "CI4", "CI8", "IA4", "IA8", "IA16", "I4", "I8", "YUV16",
]


def format_name(fmt: int, siz: int) -> str:
    return f"{FMT_NAMES.get(fmt, '?')}{SIZ_BITS.get(siz, 0)}"


def parse_format(name: str) -> tuple[int, int]:
    name = name.upper()
    for f, fname in FMT_NAMES.items():
        if name.startswith(fname) and name[len(fname):].isdigit():
            bits = int(name[len(fname):])
            for s, b in SIZ_BITS.items():
                if b == bits:
                    return f, s
    raise ValueError(f"unknown texture format {name}")


def texture_byte_size(width: int, height: int, fmt_name: str) -> int:
    bits = int("".join(c for c in fmt_name if c.isdigit()))
    return (width * height * bits + 7) // 8


def _rgba16(raw: np.ndarray) -> np.ndarray:
    v = raw.astype(np.uint16)
    r = ((v >> 11) & 0x1F) * 255 // 31
    g = ((v >> 6) & 0x1F) * 255 // 31
    b = ((v >> 1) & 0x1F) * 255 // 31
    a = (v & 1) * 255
    return np.stack([r, g, b, a], axis=-1).astype(np.uint8)


def _ia16(raw: np.ndarray) -> np.ndarray:
    v = raw.astype(np.uint16)
    i = (v >> 8).astype(np.uint8)
    a = (v & 0xFF).astype(np.uint8)
    return np.stack([i, i, i, a], axis=-1)


def _nibbles(buf: np.ndarray) -> np.ndarray:
    out = np.empty(buf.size * 2, dtype=np.uint8)
    out[0::2] = buf >> 4
    out[1::2] = buf & 0x0F
    return out


def decode_palette(data: bytes, offset: int, count: int, ia: bool = False) -> np.ndarray:
    raw = np.frombuffer(bytes(data[offset:offset + count * 2]).ljust(count * 2, b"\0"), dtype=">u2")
    return _ia16(raw) if ia else _rgba16(raw)


def grayscale_palette(count: int) -> np.ndarray:
    ramp = np.linspace(0, 255, count).astype(np.uint8)
    return np.stack([ramp, ramp, ramp, np.full(count, 255, np.uint8)], axis=-1)


def decode_texture(data: bytes, offset: int, width: int, height: int, fmt_name: str,
                   palette: np.ndarray | None = None) -> Image.Image:
    """Decode a texture into an RGBA PIL image.  Missing bytes are zero-filled."""
    fmt_name = fmt_name.upper()
    width = max(1, int(width))
    height = max(1, int(height))
    n = width * height
    size = texture_byte_size(width, height, fmt_name)
    buf = bytes(data[offset:offset + size]).ljust(size, b"\0")
    raw8 = np.frombuffer(buf, dtype=np.uint8)

    if fmt_name == "RGBA16":
        px = _rgba16(np.frombuffer(buf, dtype=">u2"))
    elif fmt_name == "RGBA32":
        px = raw8.reshape(-1, 4)
    elif fmt_name == "IA16":
        px = _ia16(np.frombuffer(buf, dtype=">u2"))
    elif fmt_name == "IA8":
        i = (raw8 >> 4) * 17
        a = (raw8 & 0xF) * 17
        px = np.stack([i, i, i, a], axis=-1).astype(np.uint8)
    elif fmt_name == "IA4":
        nb = _nibbles(raw8)
        i = ((nb >> 1) * 255 // 7).astype(np.uint8)
        a = ((nb & 1) * 255).astype(np.uint8)
        px = np.stack([i, i, i, a], axis=-1)
    elif fmt_name == "I8":
        px = np.stack([raw8, raw8, raw8, np.full_like(raw8, 255)], axis=-1)
    elif fmt_name == "I4":
        nb = (_nibbles(raw8) * 17).astype(np.uint8)
        px = np.stack([nb, nb, nb, np.full_like(nb, 255)], axis=-1)
    elif fmt_name in ("CI4", "CI8"):
        idx = _nibbles(raw8) if fmt_name == "CI4" else raw8
        count = 16 if fmt_name == "CI4" else 256
        pal = palette if palette is not None and len(palette) else grayscale_palette(count)
        if len(pal) < count:
            pal = np.concatenate([pal, np.zeros((count - len(pal), 4), np.uint8)])
        px = pal[np.minimum(idx, count - 1)]
    elif fmt_name == "YUV16":
        # pairs of pixels: U Y0 V Y1
        q = raw8.reshape(-1, 4).astype(np.float32)
        u, y0, v, y1 = q[:, 0] - 128, q[:, 1], q[:, 2] - 128, q[:, 3]
        out = []
        for y in (y0, y1):
            r = y + 1.402 * v
            g = y - 0.344 * u - 0.714 * v
            b = y + 1.772 * u
            out.append(np.stack([r, g, b, np.full_like(r, 255)], -1))
        px = np.clip(np.stack(out, 1).reshape(-1, 4), 0, 255).astype(np.uint8)
    else:
        raise ValueError(f"unsupported texture format {fmt_name}")

    px = px[:n]
    if len(px) < n:
        px = np.concatenate([px, np.zeros((n - len(px), 4), np.uint8)])
    return Image.fromarray(np.ascontiguousarray(px.reshape(height, width, 4)), "RGBA")
