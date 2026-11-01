"""Fast3D / F3DEX / F3DEX2 display-list detection and interpretation.

N64 games do not store "model files": geometry lives in RSP display lists
that the game's code links together at run-time.  This module finds display
lists by pattern (they always end in G_ENDDL), works out which microcode
family encoded them, and replays them to rebuild triangles, UVs, vertex
colours/normals and the textures that were bound while drawing.
"""
from __future__ import annotations

import struct
from dataclasses import dataclass, field

import numpy as np

from .model import Material, Mesh, Vertex
from .texture import decode_palette, decode_texture, format_name

F3D, F3DEX, F3DEX2 = "F3D", "F3DEX", "F3DEX2"
UCODES = (F3D, F3DEX, F3DEX2)

ENDDL_PATTERNS = {
    b"\xB8\x00\x00\x00\x00\x00\x00\x00": (F3D, F3DEX),
    b"\xDF\x00\x00\x00\x00\x00\x00\x00": (F3DEX2,),
}

RDP_OPS = set(range(0xE4, 0x100)) - {0xF1}
OPS = {
    F3D: {0x01, 0x03, 0x04, 0x06, 0xB4, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE,
          0xBF, 0xC0, 0xB3, 0xB2} | RDP_OPS,
    F3DEX: {0x01, 0x03, 0x04, 0x06, 0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9,
            0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC0} | RDP_OPS,
    F3DEX2: {0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8,
             0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xE0, 0xE1, 0xE2, 0xE3} | RDP_OPS,
}
VTX_OP = {F3D: 0x04, F3DEX: 0x04, F3DEX2: 0x01}
TRI1_OP = {F3D: 0xBF, F3DEX: 0xBF, F3DEX2: 0x05}
TRI2_OP = {F3DEX: 0xB1, F3DEX2: 0x06}
QUAD_OP = {F3DEX: 0xB5, F3DEX2: 0x07}
DL_OP = {F3D: 0x06, F3DEX: 0x06, F3DEX2: 0xDE}
END_OP = {F3D: 0xB8, F3DEX: 0xB8, F3DEX2: 0xDF}
TEXTURE_OP = {F3D: 0xBB, F3DEX: 0xBB, F3DEX2: 0xD7}
SETGEO_OP = {F3D: 0xB7, F3DEX: 0xB7}
CLRGEO_OP = {F3D: 0xB6, F3DEX: 0xB6}
LIGHTING_BIT = {F3D: 0x00020000, F3DEX: 0x00020000, F3DEX2: 0x00200000}
OTHERMODE_H_OP = {F3D: 0xBA, F3DEX: 0xBA, F3DEX2: 0xE3}

G_SETTIMG, G_SETTILE, G_LOADBLOCK, G_LOADTILE, G_SETTILESIZE, G_LOADTLUT = \
    0xFD, 0xF5, 0xF3, 0xF4, 0xF2, 0xF0
G_SETPRIMCOLOR, G_SETENVCOLOR = 0xFA, 0xFB


def _addr_ok(addr: int) -> bool:
    seg = addr >> 24
    return seg <= 0x0F or 0x80 <= seg <= 0x80


def _validate(ucode: str, w0: int, w1: int) -> bool:
    op = w0 >> 24
    if op not in OPS[ucode]:
        if op == 0x00 and w0 == 0 and w1 == 0:
            return True
        return False
    if op == VTX_OP[ucode]:
        if not _addr_ok(w1):
            return False
        if ucode == F3D:
            n = ((w0 >> 20) & 0xF) + 1
            v0 = (w0 >> 16) & 0xF
            return (w0 & 0xFFFF) == n * 16 and v0 + n <= 16
        if ucode == F3DEX:
            b1 = (w0 >> 16) & 0xFF
            n = (w0 >> 10) & 0x3F
            return b1 % 2 == 0 and n > 0 and (w0 & 0x3FF) == n * 16 - 1 and b1 // 2 + n <= 64
        n = (w0 >> 12) & 0xFF
        vend = (w0 >> 1) & 0x7F
        return (w0 & 0x00F00F01) == 0 and 0 < n <= vend <= 64
    if op == TRI1_OP[ucode]:
        if ucode == F3D:
            a, b, c = (w1 >> 16) & 0xFF, (w1 >> 8) & 0xFF, w1 & 0xFF
            return all(x % 10 == 0 and x < 160 for x in (a, b, c)) and (w0 & 0xFFFFFF) == 0
        idx = (w1 >> 16) & 0xFF, (w1 >> 8) & 0xFF, w1 & 0xFF
        if ucode == F3DEX2:
            idx = (w0 >> 16) & 0xFF, (w0 >> 8) & 0xFF, w0 & 0xFF
        return all(x % 2 == 0 and x < 128 for x in idx)
    if ucode in TRI2_OP and op in (TRI2_OP[ucode], QUAD_OP[ucode]):
        idx = [(w0 >> 16) & 0xFF, (w0 >> 8) & 0xFF, w0 & 0xFF,
               (w1 >> 16) & 0xFF, (w1 >> 8) & 0xFF, w1 & 0xFF]
        return all(x % 2 == 0 and x < 128 for x in idx) and (w1 >> 24) == 0
    if op == DL_OP[ucode]:
        return ((w0 >> 16) & 0xFF) in (0, 1) and (w0 & 0xFFFF) == 0 and _addr_ok(w1)
    if op == END_OP[ucode]:
        return (w0 & 0xFFFFFF) == 0 and w1 == 0
    if op == G_SETTIMG:
        return _addr_ok(w1) and (w0 & 0x0007F000) == 0
    if op == 0x00:
        return w0 == 0 and w1 == 0
    if op == 0xFF:  # G_SETCIMG
        return _addr_ok(w1) and (w0 & 0x0007F000) == 0
    if op == 0xFE:  # G_SETZIMG
        return _addr_ok(w1) and (w0 & 0x00FFFFFF) == 0
    if op == G_SETTILE:
        return (w0 & 0x00040000) == 0 and (w1 & 0xF8000000) == 0
    if op in (G_SETTILESIZE, G_LOADBLOCK, G_LOADTILE, G_LOADTLUT):
        return (w1 & 0xF8000000) == 0
    if op in (0xE6, 0xE7, 0xE8, 0xE9):  # syncs
        return (w0 & 0x00FFFFFF) == 0
    if ucode == F3DEX2:
        if op == 0x04:  # G_BRANCH_Z
            a, b = (w0 >> 12) & 0xFFF, w0 & 0xFFF
            return b % 2 == 0 and a == (b // 2) * 5
        if op == 0x03:  # G_CULLDL
            return (w0 & 0x00FF0000) == 0 and (w1 >> 16) == 0
        if op == 0xDA:  # G_MTX
            return _addr_ok(w1) and (w0 & 0x00FFFF00) == 0x00380000
        if op == 0xDC:  # G_MOVEMEM
            return _addr_ok(w1)
    else:
        if op == 0x01:  # G_MTX
            return _addr_ok(w1) and (w0 & 0xFFFF) == 0x40
        if op == 0x03:  # G_MOVEMEM
            return _addr_ok(w1)
        if op in (0xB6, 0xB7):  # geometry mode
            return (w0 & 0x00FFFFFF) == 0
    return True


@dataclass
class DisplayList:
    blob_index: int
    offset: int
    end: int
    ucode: str
    vtx_count: int = 0
    tri_count: int = 0
    calls: list[int] = field(default_factory=list)  # segmented addresses called via G_DL
    is_child: bool = False
    base: int | None = None  # resolved segment base inside the blob

    @property
    def length(self) -> int:
        return self.end - self.offset


def _words(data: bytes, off: int) -> tuple[int, int]:
    return struct.unpack_from(">II", data, off)


def _scan_ucode_from_end(data: bytes, end_off: int, ucode: str) -> DisplayList | None:
    """Walk backwards from an ENDDL to find where the display list starts."""
    start = end_off
    p = end_off - 8
    limit = max(0, end_off - 0x20000)
    while p >= limit:
        w0, w1 = _words(data, p)
        if (w0 >> 24) == END_OP[ucode] or not _validate(ucode, w0, w1):
            break
        start = p
        p -= 8
    # trim leading no-ops
    while start < end_off:
        w0, w1 = _words(data, start)
        if w0 == 0 and w1 == 0:
            start += 8
        else:
            break
    dl = DisplayList(-1, start, end_off + 8, ucode)
    vtx, tri1, dl_op = VTX_OP[ucode], TRI1_OP[ucode], DL_OP[ucode]
    tri2 = TRI2_OP.get(ucode, -1)
    quad = QUAD_OP.get(ucode, -1)
    for q in range(start, end_off, 8):
        w0, w1 = _words(data, q)
        op = w0 >> 24
        if op == vtx:
            dl.vtx_count += 1
        elif op == tri1:
            dl.tri_count += 1
        elif op in (tri2, quad):
            dl.tri_count += 2
        elif op == dl_op:
            dl.calls.append(w1)
    return dl


def _classify_f3d_or_f3dex(data: bytes, end_off: int) -> str:
    """F3D and F3DEX share opcodes; triangle index scaling tells them apart."""
    p = end_off - 8
    limit = max(0, end_off - 0x4000)
    f3d_votes = ex_votes = 0
    while p >= limit:
        w0, w1 = _words(data, p)
        op = w0 >> 24
        if op == 0xB8:
            break
        if op == 0xBF:
            idx = ((w1 >> 16) & 0xFF, (w1 >> 8) & 0xFF, w1 & 0xFF)
            if all(x % 10 == 0 for x in idx) and not all(x % 2 == 0 and x < 64 for x in idx):
                f3d_votes += 1
            elif all(x % 10 == 0 for x in idx):
                pass
            else:
                ex_votes += 1
        elif op in (0xB1, 0xB5):
            ex_votes += 1
        elif op == 0x04:
            if (w0 & 0xFFFF) % 16 == 0 and ((w0 >> 20) & 0xF) + 1 == (w0 & 0xFFFF) // 16:
                f3d_votes += 1
            elif (w0 & 0x3FF) == ((w0 >> 10) & 0x3F) * 16 - 1:
                ex_votes += 1
        p -= 8
    return F3D if f3d_votes > ex_votes else F3DEX


def find_display_lists(data: bytes, blob_index: int = 0, min_tris: int = 1) -> list[DisplayList]:
    found: dict[int, DisplayList] = {}
    for pattern, ucodes in ENDDL_PATTERNS.items():
        i = data.find(pattern)
        while i != -1:
            if i % 8 == 0 and i >= 8:
                ucode = ucodes[0] if len(ucodes) == 1 else _classify_f3d_or_f3dex(data, i)
                dl = _scan_ucode_from_end(data, i, ucode)
                if dl and dl.offset < i and (dl.tri_count >= min_tris and dl.vtx_count > 0 or dl.calls):
                    dl.blob_index = blob_index
                    prev = found.get(dl.offset)
                    if prev is None or prev.tri_count < dl.tri_count:
                        found[dl.offset] = dl
            i = data.find(pattern, i + 1)
    dls = sorted(found.values(), key=lambda d: d.offset)
    # mark display lists that are called from other display lists (sub-meshes)
    by_low = {d.offset: d for d in dls}
    for d in dls:
        for addr in d.calls:
            child = by_low.get(addr & 0xFFFFFF)
            if child is not None and child is not d:
                child.is_child = True
    # pure "container" lists with no geometry and no resolvable children are noise
    return [d for d in dls if d.tri_count > 0 or any((a & 0xFFFFFF) in by_low for a in d.calls)]


# --------------------------------------------------------------------------
#  Interpreter
# --------------------------------------------------------------------------

@dataclass
class _TileState:
    fmt: int = 0
    siz: int = 2
    line: int = 0
    tmem: int = 0
    palette: int = 0
    cms: int = 0
    cmt: int = 0
    uls: int = 0
    ult: int = 0
    lrs: int = 31 * 4
    lrt: int = 31 * 4


class DisplayListInterpreter:
    """Replays a display list to produce a :class:`Mesh`."""

    def __init__(self, data: bytes, ucode: str, base: int = 0, home_segment: int | None = None,
                 rom=None, max_commands: int = 200000):
        self.data = data
        self.ucode = ucode
        self.base = base
        self.home_segment = home_segment
        self.rom = rom
        self.max_commands = max_commands
        self.unresolved: set[int] = set()

    # -- addressing -------------------------------------------------------
    def resolve(self, addr: int) -> int | None:
        seg = addr >> 24
        off = addr & 0xFFFFFF
        if seg >= 0x80:
            # KSEG0 pointer: only resolvable for the raw ROM (boot segment)
            if self.rom is not None and len(self.data) == len(self.rom.data):
                return self.rom.entry_to_rom(addr)
            return None
        if self.home_segment is not None and seg != self.home_segment and seg != 0:
            self.unresolved.add(seg)
            return None
        p = self.base + off
        if 0 <= p < len(self.data):
            return p
        return None

    # -- execution --------------------------------------------------------
    def run(self, start: int, name: str = "mesh") -> Mesh:
        uc = self.ucode
        mesh = Mesh(name)
        data = self.data
        vbuf: list[tuple | None] = [None] * 64
        vcache: dict[tuple, int] = {}
        mat_cache: dict[tuple, int] = {}
        tiles = [_TileState() for _ in range(8)]
        timg = (0, 0, 2, 1)  # addr, fmt, siz, width
        tlut_addr = None
        tlut_ia = False
        tex_scale = (1.0, 1.0)
        render_tile = 0
        lighting = True
        geo_mode = LIGHTING_BIT[uc]
        prim = (1.0, 1.0, 1.0, 1.0)
        stack: list[int] = []
        pc = start
        steps = 0
        visited_calls = 0

        def current_material() -> int:
            t = tiles[render_tile]
            w = ((t.lrs - t.uls) >> 2) + 1
            h = ((t.lrt - t.ult) >> 2) + 1
            key = (tex_addr_for_tile, t.fmt, t.siz, w, h, tlut_addr, tlut_ia)
            if key in mat_cache:
                return mat_cache[key]
            mat = Material(f"mat_{len(mesh.materials)}", color=prim)
            if tex_addr_for_tile is not None and 0 < w <= 1024 and 0 < h <= 1024:
                fname = format_name(t.fmt, t.siz)
                pal = None
                if t.fmt == 2 and tlut_addr is not None:
                    count = 16 if t.siz == 0 else 256
                    pal_off = tlut_addr + (t.palette * 16 * 2 if t.siz == 0 else 0)
                    pal = decode_palette(data, pal_off, count, tlut_ia)
                try:
                    mat.texture = decode_texture(data, tex_addr_for_tile, w, h, fname, pal)
                    mat.texture_info = f"{fname} {w}x{h} @ 0x{tex_addr_for_tile:X}"
                    mat.name = f"tex_{tex_addr_for_tile:06X}_{fname}_{w}x{h}"
                except ValueError:
                    mat.texture = None
                mat.wrap_s = "clamp" if t.cms & 2 else ("mirror" if t.cms & 1 else "repeat")
                mat.wrap_t = "clamp" if t.cmt & 2 else ("mirror" if t.cmt & 1 else "repeat")
            mesh.materials.append(mat)
            mat_cache[key] = len(mesh.materials) - 1
            return mat_cache[key]

        tex_addr_for_tile = None

        def emit_tri(i0: int, i1: int, i2: int) -> None:
            if not (0 <= i0 < 64 and 0 <= i1 < 64 and 0 <= i2 < 64):
                return
            verts = (vbuf[i0], vbuf[i1], vbuf[i2])
            if any(v is None for v in verts):
                return
            m = current_material()
            t = tiles[render_tile]
            w = max(1, ((t.lrs - t.uls) >> 2) + 1)
            h = max(1, ((t.lrt - t.ult) >> 2) + 1)
            out = []
            for raw in verts:
                key = (raw[0], m)
                idx = vcache.get(key)
                if idx is None:
                    _addr, x, y, z, s, tt, c0, c1, c2, c3, lit = raw
                    u = (s * tex_scale[0] / 32.0 - t.uls / 4.0) / w
                    v = (tt * tex_scale[1] / 32.0 - t.ult / 4.0) / h
                    vert = Vertex(float(x), float(y), float(z), u, v)
                    if lit:
                        nx = c0 - 256 if c0 > 127 else c0
                        ny = c1 - 256 if c1 > 127 else c1
                        nz = c2 - 256 if c2 > 127 else c2
                        ln = (nx * nx + ny * ny + nz * nz) ** 0.5 or 1.0
                        vert.nx, vert.ny, vert.nz = nx / ln, ny / ln, nz / ln
                        vert.a = c3 / 255.0
                        mesh.has_normals = True
                    else:
                        vert.r, vert.g, vert.b, vert.a = c0 / 255.0, c1 / 255.0, c2 / 255.0, c3 / 255.0
                        mesh.has_colors = True
                    mesh.vertices.append(vert)
                    idx = len(mesh.vertices) - 1
                    vcache[key] = idx
                out.append(idx)
            if out[0] != out[1] and out[1] != out[2] and out[0] != out[2]:
                mesh.triangles.append((out[0], out[1], out[2], m))

        div = 10 if uc == F3D else 2
        while steps < self.max_commands:
            steps += 1
            if pc is None or pc < 0 or pc + 8 > len(data):
                if stack:
                    pc = stack.pop()
                    continue
                break
            w0, w1 = struct.unpack_from(">II", data, pc)
            pc += 8
            op = w0 >> 24

            if op == VTX_OP[uc]:
                if uc == F3D:
                    n = ((w0 >> 20) & 0xF) + 1
                    v0 = (w0 >> 16) & 0xF
                elif uc == F3DEX:
                    v0 = ((w0 >> 16) & 0xFF) // 2
                    n = (w0 >> 10) & 0x3F
                else:
                    n = (w0 >> 12) & 0xFF
                    v0 = ((w0 >> 1) & 0x7F) - n
                p = self.resolve(w1)
                if p is None:
                    continue
                for i in range(n):
                    q = p + i * 16
                    if q + 16 > len(data) or not (0 <= v0 + i < 64):
                        break
                    x, y, z, _f, s, tt, c0, c1, c2, c3 = struct.unpack_from(">hhhHhhBBBB", data, q)
                    vbuf[v0 + i] = (q, x, y, z, s, tt, c0, c1, c2, c3, lighting)
            elif op == TRI1_OP[uc]:
                src = w0 if uc == F3DEX2 else w1
                emit_tri(((src >> 16) & 0xFF) // div, ((src >> 8) & 0xFF) // div, (src & 0xFF) // div)
            elif uc in TRI2_OP and op == TRI2_OP[uc]:
                emit_tri(((w0 >> 16) & 0xFF) // 2, ((w0 >> 8) & 0xFF) // 2, (w0 & 0xFF) // 2)
                emit_tri(((w1 >> 16) & 0xFF) // 2, ((w1 >> 8) & 0xFF) // 2, (w1 & 0xFF) // 2)
            elif uc in QUAD_OP and op == QUAD_OP[uc]:
                a, b, c = ((w0 >> 16) & 0xFF) // 2, ((w0 >> 8) & 0xFF) // 2, (w0 & 0xFF) // 2
                d, e, f = ((w1 >> 16) & 0xFF) // 2, ((w1 >> 8) & 0xFF) // 2, (w1 & 0xFF) // 2
                emit_tri(a, b, c)
                emit_tri(d, e, f)
            elif op == DL_OP[uc]:
                target = self.resolve(w1)
                visited_calls += 1
                if target is None or visited_calls > 4096:
                    continue
                if ((w0 >> 16) & 0xFF) == 0:
                    if len(stack) < 32:
                        stack.append(pc)
                        pc = target
                else:
                    pc = target
            elif op == END_OP[uc]:
                if stack:
                    pc = stack.pop()
                else:
                    break
            elif op == TEXTURE_OP[uc]:
                ss = (w1 >> 16) & 0xFFFF
                ts = w1 & 0xFFFF
                tex_scale = ((ss or 0xFFFF) / 65536.0, (ts or 0xFFFF) / 65536.0)
                render_tile = (w0 >> 8) & 7
            elif uc == F3DEX2 and op == 0xD9:
                geo_mode = (geo_mode & (w0 & 0xFFFFFF)) | w1
                lighting = bool(geo_mode & LIGHTING_BIT[uc])
            elif uc in SETGEO_OP and op == SETGEO_OP[uc]:
                geo_mode |= w1
                lighting = bool(geo_mode & LIGHTING_BIT[uc])
            elif uc in CLRGEO_OP and op == CLRGEO_OP[uc]:
                geo_mode &= ~w1
                lighting = bool(geo_mode & LIGHTING_BIT[uc])
            elif op == OTHERMODE_H_OP[uc]:
                # G_MDSFT_TEXTLUT (bit 14..15): 2 = RGBA16, 3 = IA16
                if uc == F3DEX2:
                    shift = 32 - ((w0 >> 8) & 0xFF) - ((w0 & 0xFF) + 1)
                else:
                    shift = (w0 >> 8) & 0xFF
                if shift == 14:
                    tlut_ia = ((w1 >> 14) & 3) == 3
            elif op == G_SETTIMG:
                timg = (w1, (w0 >> 21) & 7, (w0 >> 19) & 3, (w0 & 0xFFF) + 1)
            elif op == G_SETTILE:
                t = tiles[(w1 >> 24) & 7]
                t.fmt = (w0 >> 21) & 7
                t.siz = (w0 >> 19) & 3
                t.line = (w0 >> 9) & 0x1FF
                t.tmem = w0 & 0x1FF
                t.palette = (w1 >> 20) & 0xF
                t.cmt = (w1 >> 18) & 3
                t.cms = (w1 >> 8) & 3
            elif op in (G_LOADBLOCK, G_LOADTILE):
                tex_addr_for_tile = self.resolve(timg[0])
            elif op == G_SETTILESIZE:
                t = tiles[(w1 >> 24) & 7]
                t.uls = (w0 >> 12) & 0xFFF
                t.ult = w0 & 0xFFF
                t.lrs = (w1 >> 12) & 0xFFF
                t.lrt = w1 & 0xFFF
            elif op == G_LOADTLUT:
                tlut_addr = self.resolve(timg[0])
            elif op == G_SETPRIMCOLOR:
                prim = (((w1 >> 24) & 0xFF) / 255.0, ((w1 >> 16) & 0xFF) / 255.0,
                        ((w1 >> 8) & 0xFF) / 255.0, (w1 & 0xFF) / 255.0)
            elif op not in OPS[uc] and not (w0 == 0 and w1 == 0):
                mesh.notes.append(f"stopped at unknown opcode 0x{op:02X} @ 0x{pc - 8:X}")
                break
        if self.unresolved:
            mesh.notes.append("unresolved segments: " + ", ".join(f"0x{s:02X}" for s in sorted(self.unresolved)))
        return mesh


# --------------------------------------------------------------------------
#  Segment base discovery
# --------------------------------------------------------------------------

def _collect_vertex_refs(data: bytes, dl: DisplayList) -> list[tuple[int, int]]:
    refs = []
    vtx = VTX_OP[dl.ucode]
    for q in range(dl.offset, dl.end - 8, 8):
        w0, w1 = _words(data, q)
        if (w0 >> 24) == vtx and (w1 >> 24) < 0x10:
            if dl.ucode == F3D:
                n = ((w0 >> 20) & 0xF) + 1
            elif dl.ucode == F3DEX:
                n = (w0 >> 10) & 0x3F
            else:
                n = (w0 >> 12) & 0xFF
            refs.append((w1, n))
    return refs


def home_segment(data: bytes, dl: DisplayList) -> int | None:
    refs = _collect_vertex_refs(data, dl)
    if not refs:
        return None
    segs = [a >> 24 for a, _n in refs]
    return max(set(segs), key=segs.count)


def _coherence(data: bytes, base: int, offs: np.ndarray) -> float:
    """Lower is better: mean nearest-neighbour spread relative to bbox."""
    pts = []
    for o in offs[:256]:
        p = base + int(o)
        if p < 0 or p + 6 > len(data):
            return 1e9
        pts.append(struct.unpack_from(">hhh", data, p))
    arr = np.asarray(pts, dtype=np.float64)
    if len(arr) < 3:
        return 0.5
    centre = np.median(arr, axis=0)
    radius = np.linalg.norm(arr - centre, axis=1)
    spread = float(np.median(radius))
    if spread < 1e-6:
        return 1e9
    outliers = float(np.mean(radius > spread * 8))
    uniq = len({tuple(p) for p in pts}) / len(pts)
    big = float(np.mean(np.abs(arr) > 20000))
    return outliers * 4 + (1.0 - uniq) * 2 + big * 2


_ZERO_CACHE: dict[int, tuple[int, np.ndarray]] = {}


def _zero_pairs(data: bytes) -> np.ndarray:
    key = id(data)
    hit = _ZERO_CACHE.get(key)
    if hit is not None and hit[0] == len(data):
        return hit[1]
    arr = np.frombuffer(data, dtype=np.uint8)
    zero = (arr[:-1] == 0) & (arr[1:] == 0)
    if len(_ZERO_CACHE) > 8:
        _ZERO_CACHE.clear()
    _ZERO_CACHE[key] = (len(data), zero)
    return zero


def guess_base(data: bytes, dl: DisplayList, prefer_zero: bool) -> int:
    """Find the blob offset that the DL's segment addresses are relative to."""
    refs = _collect_vertex_refs(data, dl)
    if not refs:
        return 0
    offs = sorted({(a & 0xFFFFFF) + i * 16 for a, n in refs for i in range(n)})
    offs_arr = np.asarray(offs, dtype=np.int64)
    max_off = int(offs_arr.max()) + 16

    min_off = int(offs_arr.min())

    def overlaps_dl(b) -> np.ndarray | bool:
        return (b + min_off < dl.end) & (b + max_off > dl.offset)

    def flags_ok(b: int) -> bool:
        if b < 0 or b + max_off > len(data) or overlaps_dl(b):
            return False
        return all(data[b + o + 6] == 0 and data[b + o + 7] == 0 for o in offs[:128])

    if prefer_zero and flags_ok(0) and _coherence(data, 0, offs_arr) < 1e8:
        return 0

    # vectorised search for bases where every referenced vertex has flag == 0
    window = 0x80000
    lo = max(0, dl.offset - window)
    hi = min(len(data) - max_off, dl.offset + window)
    if hi <= lo:
        return 0
    zero = _zero_pairs(data)
    cand = np.arange(lo - lo % 8, hi, 8, dtype=np.int64)
    ok = np.ones(len(cand), dtype=bool)
    for o in offs[:96]:
        idx = cand + o + 6
        valid = idx < len(zero)
        ok &= valid & zero[np.minimum(idx, len(zero) - 1)]
        if not ok.any():
            break
    ok &= ~overlaps_dl(cand)
    good = cand[ok]
    if len(good) == 0:
        return 0 if prefer_zero else max(0, dl.offset - max_off)
    # prefer bases close to the display list itself
    order = np.argsort(np.abs(good + max_off // 2 - dl.offset))
    best, best_score = int(good[order[0]]), 1e9
    for b in good[order[:64]]:
        s = _coherence(data, int(b), offs_arr)
        if s < best_score - 1e-9:
            best, best_score = int(b), s
    return best


def build_mesh(data: bytes, dl: DisplayList, base: int | None = None, rom=None, name: str = "mesh",
               prefer_zero: bool = True) -> Mesh:
    if base is None:
        base = dl.base if dl.base is not None else guess_base(data, dl, prefer_zero)
        dl.base = base
    seg = home_segment(data, dl)
    interp = DisplayListInterpreter(data, dl.ucode, base, seg, rom)
    mesh = interp.run(dl.offset, name)
    mesh.notes.insert(0, f"{dl.ucode} display list @ 0x{dl.offset:X}, segment base 0x{base:X}"
                      + (f", home segment 0x{seg:02X}" if seg is not None else ""))
    return mesh
