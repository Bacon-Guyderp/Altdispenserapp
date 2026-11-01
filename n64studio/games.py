"""Game-specific file-system knowledge.

Most N64 games have no standard file system, so the generic scanners do
the heavy lifting.  Where a well-known file table exists we use it to get
exact file boundaries (better than pattern scanning).
"""
from __future__ import annotations

import struct
from dataclasses import dataclass

from .compression import DecompressionError, yaz0_decompress

# First two entries of the Zelda 64 engine "dmadata" table (makerom + boot)
_DMADATA_HEAD = b"\x00\x00\x00\x00\x00\x00\x10\x60\x00\x00\x00\x00\x00\x00\x00\x00"


@dataclass
class TableFile:
    index: int
    vrom: int
    rom_start: int
    rom_end: int
    compressed: bool
    data: bytes
    name: str = ""


def find_zelda_dmadata(rom: bytes, progress=None) -> list[TableFile]:
    """Ocarina of Time / Majora's Mask style dmadata table."""
    pos = rom.find(_DMADATA_HEAD)
    while pos != -1:
        if pos % 16 == 0:
            files = _read_dmadata(rom, pos, progress)
            if files and len(files) > 20:
                return files
        pos = rom.find(_DMADATA_HEAD, pos + 16)
    return []


def _read_dmadata(rom: bytes, pos: int, progress) -> list[TableFile]:
    entries = []
    p = pos
    while p + 16 <= len(rom):
        vs, ve, ps, pe = struct.unpack_from(">IIII", rom, p)
        if vs == 0 and ve == 0 and len(entries) > 0:
            break
        if ve < vs or ve - vs > 0x1000000 or len(entries) > 4096:
            return []
        entries.append((vs, ve, ps, pe))
        p += 16
    files = []
    for idx, (vs, ve, ps, pe) in enumerate(entries):
        if progress and idx % 32 == 0:
            progress(idx / max(1, len(entries)), f"Reading Zelda file table ({idx}/{len(entries)})")
        if ps == 0xFFFFFFFF or ve == vs:
            continue
        if pe == 0:
            data = bytes(rom[ps:ps + (ve - vs)])
            files.append(TableFile(idx, vs, ps, ps + len(data), False, data))
        else:
            try:
                data, _used = yaz0_decompress(rom, ps)
            except (DecompressionError, IndexError, struct.error):
                continue
            files.append(TableFile(idx, vs, ps, pe, True, data))
    return files
