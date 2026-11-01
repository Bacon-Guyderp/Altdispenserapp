"""Decompressors for the compression schemes used by N64 games.

* Yaz0  - Nintendo (Zelda OoT/MM, Paper Mario, ...)
* Yay0  - Nintendo (Mario Party, Pokemon Stadium, DK64 bits, ...)
* MIO0  - Nintendo (Super Mario 64, Mario Kart 64, F-Zero X, ...)
* gzip / zlib / raw deflate - many third parties (DK64, Rare's 0x1172/0x1173
  "rarezip" used by GoldenEye, Banjo, Perfect Dark, Conker, ...)

Every decompressor returns ``(data, compressed_length)``.
"""
from __future__ import annotations

import struct
import zlib
from dataclasses import dataclass

MAX_OUT = 16 * 1024 * 1024  # sanity cap for a single decompressed file


class DecompressionError(Exception):
    pass


def _copy_back(out: bytearray, dist: int, n: int) -> None:
    start = len(out) - dist
    if start < 0:
        raise DecompressionError("back-reference before start of output")
    if dist >= n:
        out += out[start:start + n]
    else:
        pattern = out[start:]
        out += (pattern * (n // dist + 1))[:n]


def yaz0_decompress(src: bytes, off: int = 0) -> tuple[bytes, int]:
    if src[off:off + 4] != b"Yaz0":
        raise DecompressionError("missing Yaz0 magic")
    size = struct.unpack_from(">I", src, off + 4)[0]
    if size == 0 or size > MAX_OUT:
        raise DecompressionError("implausible Yaz0 size")
    out = bytearray()
    p = off + 16
    n_src = len(src)
    try:
        while len(out) < size:
            code = src[p]
            p += 1
            for bit in range(7, -1, -1):
                if len(out) >= size:
                    break
                if code & (1 << bit):
                    out.append(src[p])
                    p += 1
                else:
                    b1 = src[p]
                    b2 = src[p + 1]
                    p += 2
                    dist = (((b1 & 0x0F) << 8) | b2) + 1
                    n = b1 >> 4
                    if n == 0:
                        n = src[p] + 0x12
                        p += 1
                    else:
                        n += 2
                    _copy_back(out, dist, n)
            if p > n_src:
                raise DecompressionError("truncated")
    except IndexError as e:
        raise DecompressionError("truncated Yaz0 stream") from e
    return bytes(out[:size]), p - off


def yay0_decompress(src: bytes, off: int = 0) -> tuple[bytes, int]:
    if src[off:off + 4] != b"Yay0":
        raise DecompressionError("missing Yay0 magic")
    size, link_off, chunk_off = struct.unpack_from(">III", src, off + 4)
    if size == 0 or size > MAX_OUT or link_off >= len(src) - off or chunk_off >= len(src) - off:
        raise DecompressionError("implausible Yay0 header")
    out = bytearray()
    mask_p = off + 16
    link_p = off + link_off
    chunk_p = off + chunk_off
    mask = 0
    bits = 0
    try:
        while len(out) < size:
            if bits == 0:
                mask = struct.unpack_from(">I", src, mask_p)[0]
                mask_p += 4
                bits = 32
            if mask & 0x80000000:
                out.append(src[chunk_p])
                chunk_p += 1
            else:
                v = (src[link_p] << 8) | src[link_p + 1]
                link_p += 2
                dist = (v & 0xFFF) + 1
                n = v >> 12
                if n == 0:
                    n = src[chunk_p] + 0x12
                    chunk_p += 1
                else:
                    n += 2
                _copy_back(out, dist, n)
            mask = (mask << 1) & 0xFFFFFFFF
            bits -= 1
    except (IndexError, struct.error) as e:
        raise DecompressionError("truncated Yay0 stream") from e
    end = max(mask_p, link_p, chunk_p) - off
    return bytes(out[:size]), end


def mio0_decompress(src: bytes, off: int = 0) -> tuple[bytes, int]:
    if src[off:off + 4] != b"MIO0":
        raise DecompressionError("missing MIO0 magic")
    size, comp_off, uncomp_off = struct.unpack_from(">III", src, off + 4)
    if size == 0 or size > MAX_OUT or comp_off >= len(src) - off or uncomp_off >= len(src) - off:
        raise DecompressionError("implausible MIO0 header")
    out = bytearray()
    flag_p = off + 16
    comp_p = off + comp_off
    unc_p = off + uncomp_off
    flag = 0
    bits = 0
    try:
        while len(out) < size:
            if bits == 0:
                flag = src[flag_p]
                flag_p += 1
                bits = 8
            if flag & 0x80:
                out.append(src[unc_p])
                unc_p += 1
            else:
                v = (src[comp_p] << 8) | src[comp_p + 1]
                comp_p += 2
                _copy_back(out, (v & 0xFFF) + 1, (v >> 12) + 3)
            flag = (flag << 1) & 0xFF
            bits -= 1
    except IndexError as e:
        raise DecompressionError("truncated MIO0 stream") from e
    end = max(flag_p, comp_p, unc_p) - off
    return bytes(out[:size]), end


def _inflate(src: bytes, start: int, wbits: int) -> tuple[bytes, int]:
    d = zlib.decompressobj(wbits)
    chunk = bytes(src[start:start + 4 * 1024 * 1024])
    try:
        out = d.decompress(chunk, MAX_OUT)
    except zlib.error as e:
        raise DecompressionError(str(e)) from e
    if not d.eof:
        raise DecompressionError("deflate stream did not terminate")
    used = len(chunk) - len(d.unused_data)
    return out, used


def gzip_decompress(src: bytes, off: int = 0) -> tuple[bytes, int]:
    return _inflate(src, off, 31)


def zlib_decompress(src: bytes, off: int = 0) -> tuple[bytes, int]:
    return _inflate(src, off, 15)


def rare_decompress(src: bytes, off: int = 0) -> tuple[bytes, int]:
    """Rare's 0x1172/0x1173 header + raw deflate.  The size field is either
    3 bytes (GoldenEye / Perfect Dark) or 4 bytes (Banjo-Kazooie)."""
    if src[off] != 0x11 or src[off + 1] not in (0x72, 0x73):
        raise DecompressionError("missing Rare header")
    for size_len in (3, 4):
        size = int.from_bytes(src[off + 2:off + 2 + size_len], "big")
        if not (16 <= size <= MAX_OUT):
            continue
        try:
            out, used = _inflate(src, off + 2 + size_len, -15)
        except DecompressionError:
            continue
        if len(out) == size:
            return out, used + 2 + size_len
    raise DecompressionError("not a Rare deflate block")


DECOMPRESSORS = {
    "Yaz0": yaz0_decompress,
    "Yay0": yay0_decompress,
    "MIO0": mio0_decompress,
    "gzip": gzip_decompress,
    "zlib": zlib_decompress,
    "Rare": rare_decompress,
}


@dataclass
class CompressedBlock:
    kind: str
    offset: int
    compressed_size: int
    data: bytes


def _find_all(data: bytes, needle: bytes, align: int = 1):
    i = data.find(needle)
    while i != -1:
        if i % align == 0:
            yield i
        i = data.find(needle, i + 1)


def scan_compressed(data: bytes, progress=None, deep: bool = True) -> list[CompressedBlock]:
    """Locate and decompress every recognised compressed block in ``data``."""
    candidates: list[tuple[int, str]] = []
    for magic in (b"Yaz0", b"Yay0", b"MIO0"):
        for i in _find_all(data, magic, 1):
            candidates.append((i, magic.decode()))
    for i in _find_all(data, b"\x1f\x8b\x08", 1):
        candidates.append((i, "gzip"))
    if deep:
        for lead in (b"\x78\x9c", b"\x78\xda", b"\x78\x01", b"\x78\x5e"):
            for i in _find_all(data, lead, 2):
                candidates.append((i, "zlib"))
        for lead in (b"\x11\x72", b"\x11\x73"):
            for i in _find_all(data, lead, 2):
                candidates.append((i, "Rare"))
    candidates.sort()

    blocks: list[CompressedBlock] = []
    covered_until = 0
    total = len(candidates) or 1
    for idx, (off, kind) in enumerate(candidates):
        if progress and idx % 64 == 0:
            progress(idx / total, f"Decompressing ({kind} @ 0x{off:X})")
        if off < covered_until:
            continue
        try:
            out, used = DECOMPRESSORS[kind](data, off)
        except (DecompressionError, IndexError, struct.error, ValueError):
            continue
        if kind in ("zlib", "Rare", "gzip") and len(out) < 64:
            continue
        blocks.append(CompressedBlock(kind, off, used, out))
        covered_until = off + used
    return blocks
