"""Music sequences: standard MIDI, libultra ALSeqFile ("S1") banks and the
SDK's compressed MIDI (ALCSeq) format, converted to standard .mid files."""
from __future__ import annotations

import struct
from dataclasses import dataclass

SEQFILE_MAGIC = 0x5331  # "S1"


@dataclass
class Sequence:
    blob_index: int
    offset: int
    length: int
    kind: str  # "MIDI" | "CSeq" | "raw"
    name: str = ""


def _midi_length(data: bytes, off: int) -> int | None:
    if data[off:off + 4] != b"MThd":
        return None
    hlen = struct.unpack_from(">I", data, off + 4)[0]
    if hlen != 6:
        return None
    _fmt, ntrks, _div = struct.unpack_from(">HHH", data, off + 8)
    if ntrks == 0 or ntrks > 64:
        return None
    p = off + 14
    for _ in range(ntrks):
        if data[p:p + 4] != b"MTrk":
            return None
        ln = struct.unpack_from(">I", data, p + 4)[0]
        p += 8 + ln
        if p > len(data):
            return None
    return p - off


def find_midi(data: bytes, blob_index: int) -> list[Sequence]:
    out = []
    i = data.find(b"MThd")
    while i != -1:
        ln = _midi_length(data, i)
        if ln:
            out.append(Sequence(blob_index, i, ln, "MIDI"))
        i = data.find(b"MThd", i + 4)
    return out


def looks_like_cseq(data: bytes, off: int, length: int) -> bool:
    if length < 0x48 or off + 0x44 > len(data):
        return False
    offs = struct.unpack_from(">16I", data, off)
    div = struct.unpack_from(">I", data, off + 0x40)[0]
    used = [o for o in offs if o]
    return bool(used) and all(0x44 <= o < length for o in used) and 0 < div < 0x10000


def find_seq_files(data: bytes, blob_index: int) -> list[Sequence]:
    out = []
    i = data.find(b"S1")
    while i != -1:
        if i % 4 == 0 and i + 4 <= len(data):
            count = struct.unpack_from(">h", data, i + 2)[0]
            if 1 <= count <= 512 and i + 4 + count * 8 <= len(data):
                entries = [struct.unpack_from(">Ii", data, i + 4 + k * 8) for k in range(count)]
                limit = min(len(data) - i, 0x1000000)
                if all(0 < ln < 0x100000 and 4 + count * 8 <= o and o + ln <= limit
                       for o, ln in entries if ln):
                    found = []
                    for k, (o, ln) in enumerate(entries):
                        if not ln:
                            continue
                        start = i + o
                        if data[start:start + 4] == b"MThd":
                            kind = "MIDI"
                        elif looks_like_cseq(data, start, ln):
                            kind = "CSeq"
                        else:
                            kind = "raw"
                        found.append(Sequence(blob_index, start, ln, kind, f"seq {k}"))
                    if found and sum(s.kind != "raw" for s in found) >= max(1, len(found) // 2):
                        out.extend(found)
        i = data.find(b"S1", i + 1)
    return out


# --------------------------------------------------------------------------
#  Compressed MIDI -> standard MIDI
# --------------------------------------------------------------------------

class _CSeqTrack:
    def __init__(self, data: bytes, start: int, end: int):
        self.data, self.p, self.end = data, start, end
        self.bu_ptr = None
        self.bu_len = 0

    def byte(self) -> int:
        d = self.data
        if self.bu_ptr is not None:
            b = d[self.bu_ptr]
            self.bu_ptr += 1
            self.bu_len -= 1
            if self.bu_len <= 0:
                self.bu_ptr = None
            return b
        if self.p >= self.end:
            raise EOFError
        b = d[self.p]
        self.p += 1
        if b == 0xFE:
            hi = d[self.p]
            self.p += 1
            if hi == 0xFE:
                return 0xFE
            lo = d[self.p]
            ln = d[self.p + 1]
            self.p += 2
            self.bu_ptr = self.p - ((hi << 8) | lo) - 4
            self.bu_len = ln
            if self.bu_ptr < 0 or ln == 0:
                raise EOFError
            return self.byte()
        return b

    def varlen(self) -> int:
        v = 0
        for _ in range(4):
            b = self.byte()
            v = (v << 7) | (b & 0x7F)
            if not b & 0x80:
                break
        return v


def _write_varlen(v: int) -> bytes:
    buf = [v & 0x7F]
    v >>= 7
    while v:
        buf.append((v & 0x7F) | 0x80)
        v >>= 7
    return bytes(reversed(buf))


def cseq_to_midi(data: bytes, off: int, length: int) -> bytes:
    seq = bytes(data[off:off + length])
    offs = struct.unpack_from(">16I", seq, 0)
    division = struct.unpack_from(">I", seq, 0x40)[0] or 96
    track_chunks = []
    ends = sorted(o for o in offs if o) + [len(seq)]
    for o in offs:
        if not o:
            continue
        end = next(e for e in ends if e > o)
        t = _CSeqTrack(seq, o, end)
        events: list[tuple[int, int, bytes]] = []  # (abs time, order, bytes)
        now = 0
        status = 0
        order = 0
        try:
            for _ in range(200000):
                now += t.varlen()
                b = t.byte()
                if b == 0xFF:
                    mtype = t.byte()
                    if mtype == 0x51:
                        ln = t.byte()
                        tempo = bytes(t.byte() for _ in range(ln or 3))
                        events.append((now, order, b"\xFF\x51\x03" + tempo[:3].rjust(3, b"\0")))
                    elif mtype == 0x2F:
                        break
                    elif mtype == 0x2E:
                        t.byte(); t.byte()
                        events.append((now, order, b"\xFF\x06\x09loopStart"))
                    elif mtype == 0x2D:
                        t.byte(); t.byte()
                        for _k in range(4):
                            t.byte()
                        events.append((now, order, b"\xFF\x06\x07loopEnd"))
                    else:
                        ln = t.byte()
                        payload = bytes(t.byte() for _ in range(ln))
                        events.append((now, order, b"\xFF" + bytes([mtype]) + _write_varlen(ln) + payload))
                    order += 1
                    continue
                if b & 0x80:
                    status = b
                    d1 = t.byte()
                else:
                    d1 = b
                if not status:
                    break
                kind = status & 0xF0
                d2 = 0 if kind in (0xC0, 0xD0) else t.byte()
                if kind == 0x90:
                    dur = t.varlen()
                    events.append((now, order, bytes([status, d1, d2])))
                    events.append((now + dur, -1, bytes([0x80 | (status & 0x0F), d1, 0])))
                elif kind in (0xC0, 0xD0):
                    events.append((now, order, bytes([status, d1])))
                else:
                    events.append((now, order, bytes([status, d1, d2])))
                order += 1
        except (EOFError, IndexError):
            pass
        events.sort(key=lambda e: (e[0], e[1] if e[1] >= 0 else -1))
        body = bytearray()
        last = 0
        for tm, _o, ev in events:
            body += _write_varlen(tm - last) + ev
            last = tm
        body += b"\x00\xFF\x2F\x00"
        track_chunks.append(b"MTrk" + struct.pack(">I", len(body)) + bytes(body))
    hdr = b"MThd" + struct.pack(">IHHH", 6, 1, len(track_chunks), division & 0x7FFF)
    return hdr + b"".join(track_chunks)


def sequence_to_midi(data: bytes, seq: Sequence) -> bytes | None:
    if seq.kind == "MIDI":
        return bytes(data[seq.offset:seq.offset + seq.length])
    if seq.kind == "CSeq":
        return cseq_to_midi(data, seq.offset, seq.length)
    return None
