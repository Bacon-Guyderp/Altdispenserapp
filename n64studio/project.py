"""A loaded ROM plus every asset discovered in it."""
from __future__ import annotations

import struct
import time
from dataclasses import dataclass, field

import numpy as np
from PIL import Image

from . import audio, f3d, games, sequence
from .compression import scan_compressed
from .mips import find_functions
from .model import Mesh
from .rom import Rom, load_rom
from .texture import decode_palette, decode_texture, format_name


@dataclass
class Blob:
    """A contiguous chunk of game data: the ROM itself or a decompressed file."""
    index: int
    name: str
    data: bytes
    rom_offset: int
    kind: str
    compressed_size: int = 0


@dataclass
class ModelAsset:
    index: int
    blob_index: int
    dl: f3d.DisplayList
    name: str
    base_override: int | None = None


@dataclass
class TextureAsset:
    index: int
    blob_index: int
    offset: int
    width: int
    height: int
    fmt: str
    tlut_offset: int | None = None
    tlut_ia: bool = False
    palette_index: int = 0
    name: str = ""

    @property
    def key(self):
        return (self.blob_index, self.offset, self.width, self.height, self.fmt, self.tlut_offset)


@dataclass
class SoundRef:
    bank_file: int
    bank: audio.Bank
    instrument: audio.Instrument
    sound: audio.Sound
    name: str


@dataclass
class ScanOptions:
    deep_compression: bool = True
    zelda_tables: bool = True
    scan_rom_display_lists: bool = True
    enumerate_textures: bool = True
    texture_time_budget: float = 90.0


@dataclass
class Project:
    rom: Rom
    blobs: list[Blob] = field(default_factory=list)
    models: list[ModelAsset] = field(default_factory=list)
    textures: list[TextureAsset] = field(default_factory=list)
    bank_files: list[audio.BankFile] = field(default_factory=list)
    sequences: list[sequence.Sequence] = field(default_factory=list)
    functions: list[int] = field(default_factory=list)
    log: list[str] = field(default_factory=list)
    _mesh_cache: dict = field(default_factory=dict)
    _pcm_cache: dict = field(default_factory=dict)

    @classmethod
    def open(cls, path: str) -> "Project":
        return cls(load_rom(path))

    # ------------------------------------------------------------------
    def scan(self, progress=None, options: ScanOptions | None = None) -> None:
        opt = options or ScanOptions()
        prog = progress or (lambda f, m: None)
        rom = self.rom
        data = bytes(rom.data)
        self.blobs = [Blob(0, "ROM (raw)", data, 0, "rom", len(data))]
        covered: list[tuple[int, int]] = []

        def sub(lo, hi):
            return lambda f, m: prog(lo + (hi - lo) * f, m)

        if opt.zelda_tables and not rom.is_disk:
            prog(0.01, "Looking for Zelda 64 file table")
            files = games.find_zelda_dmadata(data, sub(0.01, 0.15))
            for tf in files:
                kind = "Yaz0" if tf.compressed else "file"
                self.blobs.append(Blob(len(self.blobs), f"file {tf.index:04d} ({kind} @ 0x{tf.rom_start:08X})",
                                       tf.data, tf.rom_start, kind, tf.rom_end - tf.rom_start))
                covered.append((tf.rom_start, tf.rom_end))
            if files:
                self.log.append(f"Zelda 64 dmadata table: {len(files)} files")

        prog(0.15, "Scanning for compressed data")
        blocks = scan_compressed(data, sub(0.15, 0.45), deep=opt.deep_compression)
        covered.sort()
        for b in blocks:
            if any(lo <= b.offset < hi for lo, hi in covered):
                continue
            self.blobs.append(Blob(len(self.blobs), f"{b.kind} @ 0x{b.offset:08X}", b.data, b.offset,
                                   b.kind, b.compressed_size))
        # one level of nesting (archives inside compressed files)
        for blob in list(self.blobs[1:]):
            for b in scan_compressed(blob.data, None, deep=False):
                if b.offset == 0:
                    continue
                self.blobs.append(Blob(len(self.blobs), f"{b.kind} @ 0x{b.offset:X} in [{blob.name}]",
                                       b.data, blob.rom_offset, b.kind, b.compressed_size))
        self.log.append(f"{len(self.blobs) - 1} compressed/table files")

        prog(0.45, "Searching for display lists (3D models)")
        self.models = []
        n = len(self.blobs)
        for bi, blob in enumerate(self.blobs):
            if bi == 0 and not opt.scan_rom_display_lists:
                continue
            if bi % 16 == 0:
                prog(0.45 + 0.25 * bi / n, f"Searching for display lists ({bi}/{n})")
            for dl in f3d.find_display_lists(blob.data, bi):
                self.models.append(ModelAsset(len(self.models), bi, dl,
                                              f"{dl.ucode} DL @ 0x{dl.offset:X}"))

        prog(0.70, "Searching for audio banks and sequences")
        self.bank_files = []
        self.sequences = []
        for bi, blob in enumerate(self.blobs):
            self.bank_files += audio.find_bank_files(blob.data, bi)
            self.sequences += sequence.find_seq_files(blob.data, bi)
            self.sequences += sequence.find_midi(blob.data, bi)
        # de-duplicate MIDIs that are also inside an S1 bank
        seen = set()
        uniq = []
        for s in self.sequences:
            k = (s.blob_index, s.offset)
            if k not in seen:
                seen.add(k)
                uniq.append(s)
        self.sequences = uniq
        for i, s in enumerate(self.sequences):
            if not s.name:
                s.name = f"{s.kind} @ 0x{s.offset:X}"

        if not rom.is_disk and rom.header.entry_point:
            prog(0.75, "Finding code functions")
            try:
                self.functions = find_functions(data, 0x1000, min(0x100000, len(data) - 0x1000),
                                                rom.header.entry_point)
            except struct.error:
                self.functions = []

        self.textures = []
        if opt.enumerate_textures:
            self._enumerate_textures(sub(0.78, 1.0), opt.texture_time_budget)
        prog(1.0, "Done")
        self.log.append(f"{len(self.models)} display lists, {len(self.textures)} textures, "
                        f"{sum(len(list(b.all_sounds())) for b in self.bank_files)} sounds in "
                        f"{len(self.bank_files)} banks, {len(self.sequences)} sequences, "
                        f"{len(self.functions)} functions")

    # ------------------------------------------------------------------
    def _enumerate_textures(self, prog, budget: float) -> None:
        start = time.time()
        seen = {}
        total = len(self.models) or 1
        for i, m in enumerate(self.models):
            if time.time() - start > budget:
                self.log.append("texture enumeration stopped (time budget); open models to find more")
                break
            if i % 32 == 0:
                prog(i / total, f"Collecting textures ({i}/{total})")
            for t in self._textures_in_dl(m):
                if t.key not in seen:
                    t.index = len(self.textures)
                    seen[t.key] = t
                    self.textures.append(t)

    def _textures_in_dl(self, m: ModelAsset) -> list[TextureAsset]:
        blob = self.blobs[m.blob_index]
        data = blob.data
        dl = m.dl
        if dl.base is None:
            dl.base = f3d.guess_base(data, dl, prefer_zero=m.blob_index != 0)
        base = m.base_override if m.base_override is not None else dl.base
        home = f3d.home_segment(data, dl)
        interp = f3d.DisplayListInterpreter(data, dl.ucode, base, home, self.rom)
        out = []
        timg_addr = None
        tile = {}
        tlut = None
        loaded = None
        for q in range(dl.offset, dl.end, 8):
            w0, w1 = struct.unpack_from(">II", data, q)
            op = w0 >> 24
            if op == f3d.G_SETTIMG:
                timg_addr = w1
            elif op in (f3d.G_LOADBLOCK, f3d.G_LOADTILE):
                loaded = interp.resolve(timg_addr) if timg_addr is not None else None
            elif op == f3d.G_LOADTLUT:
                tlut = interp.resolve(timg_addr) if timg_addr is not None else None
            elif op == f3d.G_SETTILE:
                t = (w1 >> 24) & 7
                tile[t] = ((w0 >> 21) & 7, (w0 >> 19) & 3, (w1 >> 20) & 0xF)
            elif op == f3d.G_SETTILESIZE:
                t = (w1 >> 24) & 7
                if t == 7 or loaded is None or t not in tile:
                    continue
                w = (((w1 >> 12) & 0xFFF) - ((w0 >> 12) & 0xFFF)) // 4 + 1
                h = ((w1 & 0xFFF) - (w0 & 0xFFF)) // 4 + 1
                fmt, siz, pal = tile[t]
                if not (1 <= w <= 1024 and 1 <= h <= 1024) or fmt == 1:
                    continue
                name = format_name(fmt, siz)
                if name not in ("RGBA16", "RGBA32", "CI4", "CI8", "IA4", "IA8", "IA16", "I4", "I8"):
                    continue
                out.append(TextureAsset(0, m.blob_index, loaded, w, h, name,
                                        tlut if fmt == 2 else None, False, pal,
                                        f"{name} {w}x{h} @ 0x{loaded:X}"))
        return out

    # ------------------------------------------------------------------
    def get_mesh(self, m: ModelAsset) -> Mesh:
        key = (m.index, m.base_override)
        if key not in self._mesh_cache:
            blob = self.blobs[m.blob_index]
            mesh = f3d.build_mesh(blob.data, m.dl, m.base_override, self.rom,
                                  name=self.safe_name(m), prefer_zero=m.blob_index != 0)
            if not mesh.has_normals:
                mesh.compute_normals()
            self._mesh_cache[key] = mesh
        return self._mesh_cache[key]

    def get_blob_mesh(self, blob_index: int, roots_only: bool = True) -> Mesh | None:
        """All display lists of one file merged into a single model."""
        mesh = None
        for m in self.models:
            if m.blob_index != blob_index or (roots_only and m.dl.is_child):
                continue
            part = self.get_mesh(m)
            mesh = part if mesh is None else mesh.merged_with(part, f"file_{blob_index}")
        return mesh

    def safe_name(self, m: ModelAsset) -> str:
        return f"model_b{m.blob_index}_{m.dl.offset:06X}"

    def texture_image(self, t: TextureAsset) -> Image.Image:
        data = self.blobs[t.blob_index].data
        pal = None
        if t.fmt in ("CI4", "CI8") and t.tlut_offset is not None:
            count = 16 if t.fmt == "CI4" else 256
            off = t.tlut_offset + (t.palette_index * 32 if t.fmt == "CI4" else 0)
            pal = decode_palette(data, off, count, t.tlut_ia)
        return decode_texture(data, t.offset, t.width, t.height, t.fmt, pal)

    # ------------------------------------------------------------------
    def all_sounds(self) -> list[SoundRef]:
        out = []
        for fi, bf in enumerate(self.bank_files):
            for bank, inst, snd in bf.all_sounds():
                kind = "perc" if inst.is_percussion else "inst"
                out.append(SoundRef(fi, bank, inst, snd,
                                    f"bankfile{fi}_bank{bank.index}_{kind}{inst.index}_snd{snd.index}"))
        return out

    def locate_tbl(self, fi: int) -> tuple[int, int] | None:
        bf = self.bank_files[fi]
        if bf.tbl_offset is None:
            res = audio.locate_tbl(bf, [b.data for b in self.blobs], 0)
            if res:
                bf.tbl_blob, bf.tbl_offset = res
        if bf.tbl_offset is None:
            return None
        return bf.tbl_blob, bf.tbl_offset

    def decode_sound(self, ref: SoundRef) -> tuple[np.ndarray, int]:
        key = (ref.bank_file, ref.sound.wave.base, ref.sound.wave.length)
        rate = ref.bank.sample_rate or 22050
        if key in self._pcm_cache:
            return self._pcm_cache[key], rate
        loc = self.locate_tbl(ref.bank_file)
        if loc is None:
            raise ValueError("Could not locate the wave table (.tbl) for this bank. "
                             "Set it manually in the Audio panel.")
        tb, to = loc
        pcm = audio.decode_wave(self.blobs[tb].data, to, ref.sound.wave)
        self._pcm_cache[key] = pcm
        return pcm, rate

    def sequence_midi(self, s: sequence.Sequence) -> bytes | None:
        return sequence.sequence_to_midi(self.blobs[s.blob_index].data, s)

    def sequence_raw(self, s: sequence.Sequence) -> bytes:
        return bytes(self.blobs[s.blob_index].data[s.offset:s.offset + s.length])
