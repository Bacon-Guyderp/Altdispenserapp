"""ROM loading: byte-order normalisation, header parsing, CIC detection, CRC.

Supported containers: .z64 (big endian), .v64 (16-bit byteswapped),
.n64 (32-bit little endian), and .bin/.rom/.u64 which may be any of the
three.  .d64/.ndd (64DD disk images) have no cartridge header; they are
loaded as raw data so the generic scanners can still run over them.
"""
from __future__ import annotations

import os
import struct
import zlib
from dataclasses import dataclass, field

ROM_EXTENSIONS = (".z64", ".v64", ".n64", ".bin", ".rom", ".u64", ".d64", ".ndd")

MAGIC_Z64 = b"\x80\x37\x12\x40"
MAGIC_V64 = b"\x37\x80\x40\x12"
MAGIC_N64 = b"\x40\x12\x37\x80"

# CRC32 of the IPL3 boot code (rom[0x40:0x1000]) -> CIC chip
CIC_BY_IPL3_CRC = {
    0x6170A4A1: "6101",
    0x90BB6CB5: "6102",
    0x0B050EE0: "6103",
    0x98BC2C86: "6105",
    0xACC8580A: "6106",
    0x009E9EA3: "7102",
    0x0E018159: "8303 (64DD)",
}

CIC_SEEDS = {
    "6101": 0xF8CA4DDC,
    "7102": 0xF8CA4DDC,
    "6102": 0xF8CA4DDC,
    "6103": 0xA3886759,
    "6105": 0xDF26F436,
    "6106": 0x1FEA617A,
}

REGIONS = {
    "7": "Beta", "A": "Asian (NTSC)", "B": "Brazilian", "C": "Chinese", "D": "German",
    "E": "North America", "F": "French", "G": "Gateway 64 (NTSC)", "H": "Dutch",
    "I": "Italian", "J": "Japanese", "K": "Korean", "L": "Gateway 64 (PAL)",
    "N": "Canadian", "P": "European", "S": "Spanish", "U": "Australian",
    "W": "Scandinavian", "X": "European", "Y": "European", "Z": "European",
}

MEDIA = {"N": "Cartridge", "D": "64DD disk", "C": "Cartridge for 64DD", "E": "64DD expansion",
         "Z": "Aleck64"}


def detect_byte_order(data: bytes) -> str:
    head = bytes(data[:4])
    if head == MAGIC_Z64:
        return "z64"
    if head == MAGIC_V64:
        return "v64"
    if head == MAGIC_N64:
        return "n64"
    # Some ROMs have a non-standard first word; fall back on the PI config
    # byte pattern (0x80 in the first byte for big-endian ROMs).
    if head[:1] == b"\x80":
        return "z64"
    if head[1:2] == b"\x80":
        return "v64"
    if head[3:4] == b"\x80":
        return "n64"
    return "raw"


def to_big_endian(data: bytes, order: str) -> bytearray:
    """Convert ROM data of the given byte order to native big-endian (.z64)."""
    buf = bytearray(data)
    n = len(buf)
    if order == "v64":
        n2 = n - (n % 2)
        buf[0:n2:2], buf[1:n2:2] = buf[1:n2:2], buf[0:n2:2]
    elif order == "n64":
        n4 = n - (n % 4)
        b0, b1, b2, b3 = buf[0:n4:4], buf[1:n4:4], buf[2:n4:4], buf[3:n4:4]
        buf[0:n4:4], buf[1:n4:4], buf[2:n4:4], buf[3:n4:4] = b3, b2, b1, b0
    return buf


def from_big_endian(data: bytes, order: str) -> bytes:
    """Convert big-endian data to the requested byte order (involution)."""
    return bytes(to_big_endian(data, order))


def _rol(v: int, n: int) -> int:
    n &= 31
    return ((v << n) | (v >> (32 - n))) & 0xFFFFFFFF


def calculate_crc(rom: bytes, cic: str) -> tuple[int, int] | None:
    """Recompute the two header checksums (algorithm from n64crc)."""
    if cic not in CIC_SEEDS or len(rom) < 0x101000:
        return None
    seed = CIC_SEEDS[cic]
    t1 = t2 = t3 = t4 = t5 = t6 = seed
    words = struct.unpack_from(">262144I", rom, 0x1000)
    boot = struct.unpack_from(">64I", rom, 0x750) if cic == "6105" else None
    M = 0xFFFFFFFF
    for idx, d in enumerate(words):
        if ((t6 + d) & M) < t6:
            t4 = (t4 + 1) & M
        t6 = (t6 + d) & M
        t3 ^= d
        r = _rol(d, d & 0x1F)
        t5 = (t5 + r) & M
        if t2 > d:
            t2 ^= r
        else:
            t2 ^= t6 ^ d
        if boot is not None:
            t1 = (t1 + (boot[idx & 0x3F] ^ d)) & M
        else:
            t1 = (t1 + (t5 ^ d)) & M
    if cic == "6103":
        return ((t6 ^ t4) + t3) & M, ((t5 ^ t2) + t1) & M
    if cic == "6106":
        return (t6 * t4 + t3) & M, (t5 * t2 + t1) & M
    return t6 ^ t4 ^ t3, t5 ^ t2 ^ t1


@dataclass
class RomHeader:
    pi_config: int = 0
    clock_rate: int = 0
    entry_point: int = 0
    release: int = 0
    crc1: int = 0
    crc2: int = 0
    title: str = ""
    media: str = ""
    game_id: str = ""
    region: str = ""
    version: int = 0

    @property
    def game_code(self) -> str:
        return f"{self.media}{self.game_id}{self.region}"


@dataclass
class Rom:
    path: str
    data: bytearray
    original_order: str
    header: RomHeader = field(default_factory=RomHeader)
    cic: str = "unknown"
    is_disk: bool = False

    @property
    def size(self) -> int:
        return len(self.data)

    @property
    def name(self) -> str:
        return os.path.basename(self.path)

    @property
    def display_title(self) -> str:
        return self.header.title.strip() or os.path.splitext(self.name)[0]

    def entry_to_rom(self, ram_addr: int) -> int | None:
        """Map a RAM address in the boot segment to a ROM offset."""
        ep = self.header.entry_point
        if not ep:
            return None
        off = (ram_addr - ep) + 0x1000
        if 0x1000 <= off < len(self.data):
            return off
        return None

    def rom_to_ram(self, off: int) -> int:
        return (self.header.entry_point + off - 0x1000) & 0xFFFFFFFF

    def verify_crc(self) -> tuple[bool | None, tuple[int, int] | None]:
        base_cic = self.cic.split()[0]
        calc = calculate_crc(bytes(self.data), base_cic)
        if calc is None:
            return None, None
        return calc == (self.header.crc1, self.header.crc2), calc

    def info_lines(self) -> list[tuple[str, str]]:
        h = self.header
        rows = [
            ("File", self.path),
            ("Size", f"{self.size:,} bytes ({self.size / 1048576:.2f} MiB)"),
            ("Original byte order", {"z64": "Big endian (.z64)", "v64": "Byte-swapped (.v64)",
                                     "n64": "Little endian (.n64)", "raw": "Unknown / raw data"}
             [self.original_order]),
        ]
        if self.is_disk:
            rows.append(("Type", "64DD disk image / headerless data (generic scan only)"))
            return rows
        rows += [
            ("Internal title", h.title),
            ("Game code", h.game_code),
            ("Media", MEDIA.get(h.media, h.media or "?")),
            ("Region", REGIONS.get(h.region, h.region or "?")),
            ("Version", f"1.{h.version}"),
            ("Entry point", f"0x{h.entry_point:08X}"),
            ("Clock rate", f"0x{h.clock_rate:08X}"),
            ("libultra release", f"0x{h.release:08X}"),
            ("CRC1 / CRC2", f"0x{h.crc1:08X} / 0x{h.crc2:08X}"),
            ("CIC (boot chip)", self.cic),
        ]
        return rows


def parse_header(data: bytes) -> RomHeader:
    h = RomHeader()
    if len(data) < 0x40:
        return h
    h.pi_config, h.clock_rate, h.entry_point, h.release, h.crc1, h.crc2 = \
        struct.unpack_from(">IIIIII", data, 0)
    h.title = data[0x20:0x34].decode("shift_jis", errors="replace").rstrip("\x00 ")
    h.media = chr(data[0x3B]) if 32 <= data[0x3B] < 127 else ""
    h.game_id = data[0x3C:0x3E].decode("ascii", errors="replace")
    h.region = chr(data[0x3E]) if 32 <= data[0x3E] < 127 else ""
    h.version = data[0x3F]
    return h


def detect_cic(data: bytes) -> str:
    if len(data) < 0x1000:
        return "unknown"
    crc = zlib.crc32(bytes(data[0x40:0x1000])) & 0xFFFFFFFF
    return CIC_BY_IPL3_CRC.get(crc, f"unknown (IPL3 CRC32 {crc:08X})")


def load_rom(path: str) -> Rom:
    with open(path, "rb") as f:
        raw = f.read()
    if len(raw) < 0x40:
        raise ValueError("File is too small to be a Nintendo 64 ROM")
    order = detect_byte_order(raw)
    ext = os.path.splitext(path)[1].lower()
    data = to_big_endian(raw, order) if order in ("v64", "n64") else bytearray(raw)
    rom = Rom(path=path, data=data, original_order=order)
    if order == "raw" or ext in (".d64", ".ndd"):
        rom.is_disk = True
        return rom
    rom.header = parse_header(data)
    rom.cic = detect_cic(data)
    return rom


def save_rom(rom: Rom, path: str, order: str = "z64") -> None:
    with open(path, "wb") as f:
        f.write(from_big_endian(bytes(rom.data), order))
