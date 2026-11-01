"""Builds a synthetic N64 ROM that exercises every decoder in N64 Asset Studio.

Run directly to write ``test.z64`` (and .v64/.n64 variants) for manual GUI
testing:  python tests/make_test_rom.py out_dir
"""
from __future__ import annotations

import os
import struct
import sys

# ----------------------------------------------------------------- LZ ----


def _lz_tokens(data: bytes, max_len: int, min_len: int = 3, window: int = 0x1000):
    """Greedy LZ77 producing ('lit', b) / ('ref', dist, n) tokens."""
    i = 0
    n = len(data)
    head: dict[bytes, list[int]] = {}
    while i < n:
        best_len, best_dist = 0, 0
        key = data[i:i + 3]
        if len(key) == 3:
            for j in reversed(head.get(key, [])[-64:]):
                dist = i - j
                if dist > window:
                    break
                ln = 0
                while ln < max_len and i + ln < n and data[j + ln] == data[i + ln]:
                    ln += 1
                if ln > best_len:
                    best_len, best_dist = ln, dist
        if best_len >= min_len:
            yield ("ref", best_dist, best_len)
            for k in range(best_len):
                kk = data[i + k:i + k + 3]
                head.setdefault(kk, []).append(i + k)
            i += best_len
        else:
            yield ("lit", data[i])
            head.setdefault(key, []).append(i)
            i += 1


def yaz0_compress(data: bytes) -> bytes:
    out = bytearray(b"Yaz0" + struct.pack(">I", len(data)) + b"\0" * 8)
    toks = list(_lz_tokens(data, 0x111))
    for g in range(0, len(toks), 8):
        group = toks[g:g + 8]
        code = 0
        body = bytearray()
        for bi, t in enumerate(group):
            if t[0] == "lit":
                code |= 0x80 >> bi
                body.append(t[1])
            else:
                dist, ln = t[1] - 1, t[2]
                if ln >= 0x12:
                    body += bytes([dist >> 8, dist & 0xFF, ln - 0x12])
                else:
                    body += bytes([((ln - 2) << 4) | (dist >> 8), dist & 0xFF])
        out.append(code)
        out += body
    return bytes(out)


def yay0_compress(data: bytes) -> bytes:
    toks = list(_lz_tokens(data, 0x111))
    masks, links, chunks = [], bytearray(), bytearray()
    mask = 0
    bits = 0
    for t in toks:
        mask <<= 1
        if t[0] == "lit":
            mask |= 1
            chunks.append(t[1])
        else:
            dist, ln = t[1] - 1, t[2]
            if ln >= 0x12:
                links += struct.pack(">H", dist)
                chunks.append(ln - 0x12)
            else:
                links += struct.pack(">H", ((ln - 2) << 12) | dist)
        bits += 1
        if bits == 32:
            masks.append(mask)
            mask, bits = 0, 0
    if bits:
        masks.append(mask << (32 - bits))
    mask_bytes = b"".join(struct.pack(">I", m) for m in masks)
    link_off = 16 + len(mask_bytes)
    chunk_off = link_off + len(links)
    return b"Yay0" + struct.pack(">III", len(data), link_off, chunk_off) + mask_bytes + bytes(links) + bytes(chunks)


def mio0_compress(data: bytes) -> bytes:
    toks = list(_lz_tokens(data, 18))
    flags = bytearray()
    comp, unc = bytearray(), bytearray()
    cur = 0
    bits = 0
    for t in toks:
        cur <<= 1
        if t[0] == "lit":
            cur |= 1
            unc.append(t[1])
        else:
            comp += struct.pack(">H", ((t[2] - 3) << 12) | (t[1] - 1))
        bits += 1
        if bits == 8:
            flags.append(cur)
            cur, bits = 0, 0
    if bits:
        flags.append((cur << (8 - bits)) & 0xFF)
    while len(flags) % 4:
        flags.append(0)
    comp_off = 16 + len(flags)
    unc_off = comp_off + len(comp)
    return b"MIO0" + struct.pack(">III", len(data), comp_off, unc_off) + bytes(flags) + bytes(comp) + bytes(unc)


# ------------------------------------------------------------- models ----

CUBE = [(-100, -100, -100), (100, -100, -100), (100, 100, -100), (-100, 100, -100),
        (-100, -100, 100), (100, -100, 100), (100, 100, 100), (-100, 100, 100)]
CUBE_TRIS = [(0, 1, 2), (0, 2, 3), (4, 6, 5), (4, 7, 6), (0, 4, 5), (0, 5, 1),
             (3, 2, 6), (3, 6, 7), (1, 5, 6), (1, 6, 2), (0, 3, 7), (0, 7, 4)]


def vertices(points, color=True) -> bytes:
    out = bytearray()
    for i, (x, y, z) in enumerate(points):
        s = (i & 1) * 32 * 32
        t = ((i >> 1) & 1) * 32 * 32
        rgba = (255, (i * 30) & 255, 64, 255) if color else (0, 127, 0, 255)
        out += struct.pack(">hhhHhhBBBB", x, y, z, 0, s, t, *rgba)
    return bytes(out)


def checker_rgba16(w=32, h=32) -> bytes:
    out = bytearray()
    for y in range(h):
        for x in range(w):
            on = ((x // 4) + (y // 4)) % 2
            r, g, b = (31, 0, 0) if on else (31, 31, 31)
            out += struct.pack(">H", (r << 11) | (g << 6) | (b << 1) | 1)
    return bytes(out)


def ci4_texture(w=16, h=16) -> tuple[bytes, bytes]:
    pix = bytearray()
    for y in range(h):
        for x in range(0, w, 2):
            a = (x + y) % 16
            b = (x + 1 + y) % 16
            pix.append((a << 4) | b)
    pal = b"".join(struct.pack(">H", ((i * 2) << 11) | ((31 - i * 2) << 6) | (i << 1) | 1) for i in range(16))
    return bytes(pix), pal


def w(op_w0: int, w1: int) -> bytes:
    return struct.pack(">II", op_w0 & 0xFFFFFFFF, w1 & 0xFFFFFFFF)


def build_f3dex2_object(seg: int = 0x06) -> tuple[bytes, int]:
    """Texture, vertices then a F3DEX2 display list using segment ``seg``."""
    tex = checker_rgba16()
    vtx = vertices(CUBE, color=False)
    tex_off = 0x10
    vtx_off = tex_off + len(tex)
    dl_off = vtx_off + len(vtx)
    S = seg << 24
    dl = bytearray()
    dl += w(0xE7000000, 0)                                   # pipesync
    dl += w(0xD9000000 | 0xFFFFFF, 0x00200000 | 0x04)        # geometrymode: lighting on
    dl += w(0xD7000002, 0xFFFFFFFF)                          # texture on, scale 1
    dl += w(0xFD100000, S | tex_off)                         # settimg RGBA16 w=1
    dl += w(0xF5100000, 0x07000000)                          # settile 7 rgba16
    dl += w(0xE6000000, 0)
    dl += w(0xF3000000, 0x073FF100)                          # loadblock
    dl += w(0xE7000000, 0)
    dl += w(0xF5101000, 0x00000000)                          # settile 0 rgba16 line 8
    dl += w(0xF2000000, 0x0007C07C)                          # settilesize 32x32
    dl += w(0x01000000 | (8 << 12) | (8 << 1), S | vtx_off)  # vtx 8 @0
    for k in range(0, len(CUBE_TRIS), 2):
        a, b = CUBE_TRIS[k], CUBE_TRIS[k + 1]
        dl += w(0x06000000 | (a[0] * 2 << 16) | (a[1] * 2 << 8) | a[2] * 2,
                (b[0] * 2 << 16) | (b[1] * 2 << 8) | b[2] * 2)
    dl += w(0xDF000000, 0)
    blob = b"\0" * tex_off + tex + vtx + bytes(dl)
    return blob, dl_off


def build_f3dex_ci4_object(seg: int = 0x07) -> tuple[bytes, int]:
    pix, pal = ci4_texture()
    vtx = vertices(CUBE[:4], color=True)
    pal_off, pix_off = 0x10, 0x30
    vtx_off = pix_off + len(pix)
    dl_off = vtx_off + len(vtx)
    S = seg << 24
    dl = bytearray()
    dl += w(0xB6000000, 0x00020000)                          # clear lighting
    dl += w(0xBB000001, 0xFFFFFFFF)                          # texture
    dl += w(0xBA000E02, 0x00008000)                          # othermode_h TT = RGBA16
    dl += w(0xFD100000, S | pal_off)                         # settimg palette
    dl += w(0xF5000100, 0x07000000)
    dl += w(0xF0000000, 0x0703C000)                          # loadtlut 16
    dl += w(0xFD500000, S | pix_off)                         # settimg CI16-ish load
    dl += w(0xF5500000, 0x07000000)
    dl += w(0xF3000000, 0x0707F800)
    dl += w(0xF5400200, 0x00000000)                          # settile 0 CI4
    dl += w(0xF2000000, 0x0003C03C)                          # 16x16
    dl += w(0x04000000 | (0 << 16) | (4 << 10) | (4 * 16 - 1), S | vtx_off)
    dl += w(0xB1000000 | (0 << 16) | (2 << 8) | 4, (0 << 16) | (4 << 8) | 6)  # tri2
    dl += w(0xB8000000, 0)
    return b"\0" * pal_off + pal + pix + vtx + bytes(dl), dl_off


def build_f3d_object(seg: int = 0x04) -> tuple[bytes, int]:
    vtx = vertices(CUBE, color=True)
    vtx_off = 0x20
    dl_off = vtx_off + len(vtx)
    S = seg << 24
    dl = bytearray()
    dl += w(0xB6000000, 0x00020000)
    dl += w(0x04000000 | (7 << 20) | (0 << 16) | 0x80, S | vtx_off)  # 8 verts
    for a, b, c in CUBE_TRIS:
        dl += w(0xBF000000, (a * 10 << 16) | (b * 10 << 8) | c * 10)
    dl += w(0xB8000000, 0)
    # Some fake prefix data before vertices so base != file start inside the ROM
    return b"\x12\x34" * 0x10 + vtx + bytes(dl), dl_off


# -------------------------------------------------------------- audio ----

BOOK = [  # order 2, 2 predictors -> 32 s16
    -2000, -1500, -1000, -600, -300, -100, 0, 50,
    3000, 2500, 2000, 1500, 1000, 600, 300, 100,
    0, 0, 0, 0, 0, 0, 0, 0,
    2048, 0, 0, 0, 0, 0, 0, 0,
]


def adpcm_frames(n: int = 40) -> bytes:
    out = bytearray()
    for f in range(n):
        out.append(((f % 5) << 4) | (f % 2))
        for k in range(8):
            out.append(((f * 3 + k * 5) & 0xFF) ^ 0x5A)
    return bytes(out)


def build_ctl_tbl() -> tuple[bytes, bytes]:
    wave_data = adpcm_frames()
    raw16 = struct.pack(">64h", *[int(10000 * ((i % 16) - 8) / 8) for i in range(64)])
    tbl = wave_data + raw16
    ctl = bytearray()
    # layout offsets
    BANKFILE = 0
    BANK = 0x10
    INST = 0x20
    SOUND0 = 0x40
    SOUND1 = 0x50
    ENV = 0x60
    KEY = 0x70
    WAVE0 = 0x80
    WAVE1 = 0x98
    BOOKO = 0xB0
    LOOP = 0x130
    ctl = bytearray(0x160)
    struct.pack_into(">hhI", ctl, BANKFILE, 0x4231, 1, BANK)
    struct.pack_into(">hBBiII", ctl, BANK, 1, 0, 0, 22050, 0, INST)
    struct.pack_into(">BbBBBBBBBBBBhhII", ctl, INST, 127, 64, 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 200, 2, SOUND0, SOUND1)
    struct.pack_into(">IIIBBB", ctl, SOUND0, ENV, KEY, WAVE0, 64, 127, 0)
    struct.pack_into(">IIIBBB", ctl, SOUND1, ENV, KEY, WAVE1, 64, 127, 0)
    struct.pack_into(">iiiBB", ctl, ENV, 1000, 100000, 50000, 127, 100)
    struct.pack_into(">BBBBBb", ctl, KEY, 0, 127, 0, 127, 60, 0)
    struct.pack_into(">IIBBxxII", ctl, WAVE0, 0, len(wave_data), 0, 0, LOOP, BOOKO)
    struct.pack_into(">IIBBxxI", ctl, WAVE1, len(wave_data), len(raw16), 1, 0, 0)
    struct.pack_into(">ii32h", ctl, BOOKO, 2, 2, *BOOK)
    struct.pack_into(">IIi", ctl, LOOP, 16, 600, -1)
    return bytes(ctl), tbl


def build_cseq() -> bytes:
    track = bytearray()
    track += b"\x00\xFF\x51\x03\x07\xA1\x20"            # tempo
    track += b"\x00\xC0\x05"                            # program change
    notes = b"\x00\x90\x3C\x64\x60" + b"\x60\x3E\x64\x60" + b"\x60\x40\x64\x60"
    track += notes
    # back-reference repeating the 3 note events after the first one (13 bytes)
    pos_fe = len(track)
    back = pos_fe - (7 + 3 + 5)  # distance from FE to start of the 2nd note event
    track += bytes([0xFE, back >> 8, back & 0xFF, 8])  # copy 8 bytes: 2 note events
    track += b"\x60\xFF\x2F"
    hdr = struct.pack(">16I", 0x44, *([0] * 15)) + struct.pack(">I", 96)
    return hdr + bytes(track)


def build_seqfile(cseq: bytes) -> bytes:
    hdr = struct.pack(">hhIi", 0x5331, 1, 0x10, len(cseq)) + b"\0" * 4
    return hdr + cseq


def build_midi() -> bytes:
    trk = b"\x00\x90\x3C\x40\x60\x80\x3C\x00\x00\xFF\x2F\x00"
    return b"MThd" + struct.pack(">IHHH", 6, 0, 1, 96) + b"MTrk" + struct.pack(">I", len(trk)) + trk


# --------------------------------------------------------------- code ----

def build_code() -> bytes:
    ins = [
        0x27BDFFE8,  # addiu sp, sp, -0x18
        0xAFBF0014,  # sw ra, 0x14(sp)
        0x0C000108,  # jal 0x80000420
        0x00000000,
        0x8FBF0014,  # lw ra, 0x14(sp)
        0x03E00008,  # jr ra
        0x27BD0018,  # addiu sp, sp, 0x18
        0x00000000,
    ]
    code = b"".join(struct.pack(">I", i) for i in ins)
    code = code.ljust(0x20, b"\0")
    code += struct.pack(">IIII", 0x3C028000, 0x24420010, 0x03E00008, 0x00000000)  # lui/addiu/jr
    return code


# ---------------------------------------------------------------- ROM ----

def align(buf: bytearray, a: int = 16) -> None:
    while len(buf) % a:
        buf.append(0)


def build_rom() -> tuple[bytes, dict]:
    info = {}
    rom = bytearray(0x1000)
    struct.pack_into(">IIIIII", rom, 0, 0x80371240, 0x0000000F, 0x80000400, 0x0000144B, 0, 0)
    rom[0x20:0x34] = b"N64STUDIO TEST".ljust(20, b" ")
    rom[0x3B:0x40] = b"NTSE\x00"
    rom += build_code()
    rom += b"\0" * (0x2000 - len(rom))

    # Yaz0 object (F3DEX2, segment 6)
    obj, dl = build_f3dex2_object()
    info["yaz0_dl"] = dl
    info["yaz0_off"] = len(rom)
    rom += yaz0_compress(obj)
    align(rom)
    # MIO0 object (F3DEX + CI4, segment 7)
    obj2, dl2 = build_f3dex_ci4_object()
    info["mio0_dl"] = dl2
    info["mio0_off"] = len(rom)
    rom += mio0_compress(obj2)
    align(rom)
    # Yay0 copy of the F3DEX2 object
    info["yay0_off"] = len(rom)
    rom += yay0_compress(obj)
    align(rom)
    # uncompressed F3D object straight in ROM (segment 4)
    obj3, dl3 = build_f3d_object()
    info["raw_obj_off"] = len(rom)
    info["raw_dl"] = len(rom) + dl3
    rom += obj3
    align(rom)
    # audio bank + wave table
    ctl, tbl = build_ctl_tbl()
    info["ctl"] = len(rom)
    rom += ctl
    align(rom)
    info["tbl"] = len(rom)
    rom += tbl
    align(rom)
    # sequences
    info["sbk"] = len(rom)
    rom += build_seqfile(build_cseq())
    align(rom)
    info["midi"] = len(rom)
    rom += build_midi()
    align(rom, 0x100)
    rom += b"\xFF" * (0x40000 - len(rom) % 0x40000)
    return bytes(rom), info


def main(out_dir: str) -> None:
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
    from n64studio.rom import from_big_endian

    os.makedirs(out_dir, exist_ok=True)
    rom, _info = build_rom()
    for ext in ("z64", "v64", "n64"):
        with open(os.path.join(out_dir, f"test.{ext}"), "wb") as f:
            f.write(from_big_endian(rom, ext))
    print("wrote test ROMs to", out_dir)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
