"""libultra audio: ALBankFile (.ctl) / wave table (.tbl) parsing and VADPCM.

The N64 SDK sound bank format ("B1" banks) is used by a large share of the
library (most third-party and many first-party games).  Each bank holds
instruments -> sounds -> wavetables; samples are either raw 16-bit PCM or
Nintendo's VADPCM codec, decoded here bit-exactly.
"""
from __future__ import annotations

import struct
from dataclasses import dataclass, field

import numpy as np

BANK_MAGIC = 0x4231  # "B1"
AL_ADPCM_WAVE, AL_RAW16_WAVE = 0, 1


@dataclass
class AdpcmBook:
    order: int
    npredictors: int
    coefs: list[int]


@dataclass
class Wave:
    base: int
    length: int
    type: int
    loop_start: int = 0
    loop_end: int = 0
    loop_count: int = 0
    book: AdpcmBook | None = None


@dataclass
class Sound:
    index: int
    wave: Wave
    key_base: int = 60
    detune: int = 0
    key_min: int = 0
    key_max: int = 127
    pan: int = 64
    volume: int = 127
    attack: int = 0
    decay: int = 0
    release: int = 0


@dataclass
class Instrument:
    index: int
    volume: int
    pan: int
    sounds: list[Sound] = field(default_factory=list)
    is_percussion: bool = False


@dataclass
class Bank:
    index: int
    sample_rate: int
    instruments: list[Instrument] = field(default_factory=list)


@dataclass
class BankFile:
    blob_index: int
    offset: int  # offset of the .ctl inside the blob
    size: int
    banks: list[Bank] = field(default_factory=list)
    tbl_blob: int | None = None
    tbl_offset: int | None = None

    def all_sounds(self):
        for b in self.banks:
            for inst in b.instruments:
                for s in inst.sounds:
                    yield b, inst, s

    @property
    def max_wave_extent(self) -> int:
        return max((s.wave.base + s.wave.length for _b, _i, s in self.all_sounds()), default=0)


class _Reader:
    def __init__(self, data: bytes, base: int, limit: int):
        self.data, self.base, self.limit = data, base, limit
        self.max_seen = 0

    def check(self, off: int, size: int) -> int:
        if off < 0 or off + size > self.limit or self.base + off + size > len(self.data):
            raise ValueError(f"offset 0x{off:X} outside bank")
        self.max_seen = max(self.max_seen, off + size)
        return self.base + off

    def unpack(self, fmt: str, off: int):
        return struct.unpack_from(fmt, self.data, self.check(off, struct.calcsize(fmt)))


def _parse_wave(r: _Reader, off: int, cache: dict) -> Wave:
    if off in cache:
        return cache[off]
    base, length, wtype, _flags = r.unpack(">IIBB", off)
    (loop_off,) = r.unpack(">I", off + 12)
    book_off = r.unpack(">I", off + 16)[0] if wtype == AL_ADPCM_WAVE else 0
    if wtype not in (AL_ADPCM_WAVE, AL_RAW16_WAVE) or length == 0 or length > 0x800000:
        raise ValueError("bad wavetable")
    w = Wave(base, length, wtype)
    if loop_off:
        w.loop_start, w.loop_end, w.loop_count = r.unpack(">IIi", loop_off)
    if wtype == AL_ADPCM_WAVE:
        if not book_off:
            raise ValueError("ADPCM wave without codebook")
        order, npred = r.unpack(">ii", book_off)
        if not (1 <= order <= 8 and 1 <= npred <= 16):
            raise ValueError("bad ADPCM book")
        coefs = list(r.unpack(f">{order * npred * 8}h", book_off + 8))
        w.book = AdpcmBook(order, npred, coefs)
    cache[off] = w
    return w


def _parse_instrument(r: _Reader, off: int, idx: int, wave_cache: dict) -> Instrument:
    hdr = r.unpack(">BbBBBBBBBBBBhh", off)
    volume, pan = hdr[0], hdr[1]
    sound_count = hdr[13]
    if not (0 <= sound_count <= 128):
        raise ValueError("bad sound count")
    inst = Instrument(idx, volume, pan)
    offs = r.unpack(f">{sound_count}I", off + 16) if sound_count else ()
    for si, so in enumerate(offs):
        env_off, key_off, wave_off, s_pan, s_vol, _fl = r.unpack(">IIIBBB", so)
        wave = _parse_wave(r, wave_off, wave_cache)
        snd = Sound(si, wave, pan=s_pan, volume=s_vol)
        if key_off:
            _vmin, _vmax, kmin, kmax, kbase, detune = r.unpack(">BBBBBb", key_off)
            snd.key_base, snd.detune = kbase, detune
            if kmin <= kmax <= 127:
                snd.key_min, snd.key_max = kmin, kmax
        if env_off:
            snd.attack, snd.decay, snd.release = r.unpack(">iii", env_off)
        inst.sounds.append(snd)
    return inst


def parse_bank_file(data: bytes, off: int, blob_index: int = 0) -> BankFile | None:
    if off + 4 > len(data):
        return None
    rev, count = struct.unpack_from(">hh", data, off)
    if rev != BANK_MAGIC or not (1 <= count <= 64):
        return None
    limit = min(len(data) - off, 0x200000)
    r = _Reader(data, off, limit)
    try:
        bank_offs = r.unpack(f">{count}I", 4)
        bf = BankFile(blob_index, off, 0)
        wave_cache: dict = {}
        for bi, bo in enumerate(bank_offs):
            if bo == 0 or bo % 4:
                raise ValueError("bad bank offset")
            inst_count, _flags, _pad, rate, perc = r.unpack(">hBBiI", bo)
            if not (0 <= inst_count <= 256) or not (1000 <= rate <= 96000):
                raise ValueError("bad bank header")
            bank = Bank(bi, rate)
            inst_offs = r.unpack(f">{inst_count}I", bo + 12) if inst_count else ()
            for ii, io in enumerate(inst_offs):
                if io:
                    bank.instruments.append(_parse_instrument(r, io, ii, wave_cache))
            if perc:
                p = _parse_instrument(r, perc, len(inst_offs), wave_cache)
                p.is_percussion = True
                bank.instruments.append(p)
            bf.banks.append(bank)
    except (ValueError, struct.error):
        return None
    if not any(True for _ in bf.all_sounds()):
        return None
    bf.size = (r.max_seen + 15) & ~15
    return bf


def find_bank_files(data: bytes, blob_index: int = 0) -> list[BankFile]:
    out = []
    i = data.find(b"B1")
    while i != -1:
        if i % 4 == 0:
            bf = parse_bank_file(data, i, blob_index)
            if bf:
                out.append(bf)
                i = data.find(b"B1", i + max(bf.size, 2))
                continue
        i = data.find(b"B1", i + 1)
    return out


def _adpcm_headers_ok(arr: np.ndarray, starts: np.ndarray, sounds) -> np.ndarray:
    ok = np.ones(len(starts), dtype=bool)
    n = len(arr)
    for s in sounds:
        w = s.wave
        for frame in range(min(12, max(1, w.length // 9))):
            idx = starts + w.base + frame * 9
            valid = idx < n
            h = arr[np.minimum(idx, n - 1)]
            ok &= valid & ((h & 0x0F) < w.book.npredictors) & ((h >> 4) <= 12)
            if not ok.any():
                return ok
    return ok


def locate_tbl(bf: BankFile, blobs: list[bytes], rom_blob: int = 0) -> tuple[int, int] | None:
    """Find the wave table (.tbl) that a bank's wave offsets point into."""
    adpcm = [s for _b, _i, s in bf.all_sounds() if s.wave.type == AL_ADPCM_WAVE]
    extent = bf.max_wave_extent
    seen = set()
    uniq = []
    for s in adpcm:
        if s.wave.base not in seen:
            seen.add(s.wave.base)
            uniq.append(s)
    uniq = uniq[:24]
    after = (bf.offset + bf.size + 15) & ~15
    # 1) directly after the .ctl in the same blob (common layout)
    quick = [(bf.blob_index, after), (bf.blob_index, after + 16)]
    for blob_i, start in quick:
        d = blobs[blob_i]
        if start + extent <= len(d):
            if not uniq:
                return blob_i, start
            arr = np.frombuffer(d, dtype=np.uint8)
            if _adpcm_headers_ok(arr, np.array([start]), uniq)[0]:
                return blob_i, start
    if not uniq:
        return None
    # 2) exhaustive 16-byte aligned search in the ctl's blob, then the raw ROM
    for blob_i in dict.fromkeys([bf.blob_index, rom_blob]):
        d = blobs[blob_i]
        if len(d) < extent:
            continue
        arr = np.frombuffer(d, dtype=np.uint8)
        starts = np.arange(0, len(d) - extent + 1, 16, dtype=np.int64)
        for chunk in range(0, len(starts), 1 << 20):
            sub = starts[chunk:chunk + (1 << 20)]
            ok = _adpcm_headers_ok(arr, sub, uniq)
            hits = sub[ok]
            if len(hits):
                if blob_i == bf.blob_index:
                    # prefer the candidate closest to the ctl
                    best = hits[np.argmin(np.abs(hits - bf.offset))]
                    return blob_i, int(best)
                return blob_i, int(hits[0])
    return None


# --------------------------------------------------------------------------
#  VADPCM decoding
# --------------------------------------------------------------------------

def _expand_book(book: AdpcmBook) -> list[list[list[int]]]:
    order = book.order
    tables = []
    for p in range(book.npredictors):
        t = [[0] * (order + 8) for _ in range(8)]
        base = p * order * 8
        for j in range(order):
            for k in range(8):
                t[k][j] = book.coefs[base + j * 8 + k]
        for k in range(1, 8):
            t[k][order] = t[k - 1][order - 1]
        t[0][order] = 2048
        for k in range(1, 8):
            for j in range(k):
                t[j][k + order] = 0
            for j in range(k, 8):
                t[j][k + order] = t[j - k][order]
        tables.append(t)
    return tables


def decode_vadpcm(data: bytes, book: AdpcmBook, num_bytes: int | None = None) -> np.ndarray:
    """Decode VADPCM (9-byte frames, 16 samples each) to int16 PCM."""
    tables = _expand_book(book)
    order = book.order
    n = len(data) if num_bytes is None else min(num_bytes, len(data))
    frames = n // 9
    out = np.zeros(frames * 16, dtype=np.int16)
    state = [0] * 16
    pos = 0
    for f in range(frames):
        hdr = data[pos]
        scale = 1 << (hdr >> 4)
        pred = hdr & 0x0F
        if pred >= len(tables):
            pred = 0
        tab = tables[pred]
        ix = []
        for b in data[pos + 1:pos + 9]:
            hi, lo = b >> 4, b & 0x0F
            ix.append((hi - 16 if hi >= 8 else hi) * scale)
            ix.append((lo - 16 if lo >= 8 else lo) * scale)
        pos += 9
        cur = [0] * 16
        for j in range(2):
            in_vec = [0] * (order + 8)
            if j == 0:
                for i in range(order):
                    in_vec[i] = state[16 - order + i]
            else:
                for i in range(order):
                    in_vec[i] = cur[8 - order + i]
            for i in range(8):
                in_vec[order + i] = ix[j * 8 + i]
            for i in range(8):
                row = tab[i]
                acc = 0
                for k in range(order + 8):
                    acc += row[k] * in_vec[k]
                v = acc >> 11  # floor division, matches SDK rounding
                if v > 32767:
                    v = 32767
                elif v < -32768:
                    v = -32768
                cur[j * 8 + i] = v
        state = cur
        out[f * 16:(f + 1) * 16] = cur
    return out


def decode_wave(tbl_data: bytes, tbl_offset: int, wave: Wave) -> np.ndarray:
    raw = bytes(tbl_data[tbl_offset + wave.base: tbl_offset + wave.base + wave.length])
    if wave.type == AL_RAW16_WAVE:
        raw = raw[: len(raw) // 2 * 2]
        return np.frombuffer(raw, dtype=">i2").astype(np.int16)
    return decode_vadpcm(raw, wave.book)


def decode_raw_pcm(data: bytes, offset: int, length: int, fmt: str = "s16be") -> np.ndarray:
    """Interpret arbitrary bytes as PCM for the raw audio explorer."""
    raw = bytes(data[offset:offset + length])
    if fmt == "s16be":
        return np.frombuffer(raw[: len(raw) // 2 * 2], dtype=">i2").astype(np.int16)
    if fmt == "s16le":
        return np.frombuffer(raw[: len(raw) // 2 * 2], dtype="<i2").astype(np.int16)
    if fmt == "s8":
        return (np.frombuffer(raw, dtype=np.int8).astype(np.int16) << 8)
    if fmt == "u8":
        return ((np.frombuffer(raw, dtype=np.uint8).astype(np.int16) - 128) << 8)
    raise ValueError(fmt)
