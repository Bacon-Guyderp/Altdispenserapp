"""SoundFont 2 (.sf2) writer for libultra instrument banks.

Combined with the exported .mid sequences this lets the game's music be
played back in any SF2-capable synth / DAW with the original instruments.
"""
from __future__ import annotations

import math
import struct

import numpy as np


def _chunk(tag: bytes, data: bytes) -> bytes:
    if len(data) % 2:
        data += b"\0"
    return tag + struct.pack("<I", len(data)) + data


def _list(tag: bytes, *chunks: bytes) -> bytes:
    body = tag + b"".join(chunks)
    return b"LIST" + struct.pack("<I", len(body)) + body


def _name(s: str) -> bytes:
    return s.encode("ascii", "replace")[:19].ljust(20, b"\0")


# generator operators
GEN_KEYRANGE, GEN_INSTRUMENT, GEN_FINETUNE, GEN_SAMPLEID, GEN_SAMPLEMODES, GEN_ROOTKEY = 43, 41, 52, 53, 54, 58
GEN_PAN, GEN_ATTACK, GEN_RELEASE = 17, 34, 38


def _timecents(us: int) -> int:
    sec = max(us, 1) / 1_000_000.0
    return max(-12000, min(8000, int(round(1200 * math.log2(sec)))))


def build_sf2(bank_name: str, instruments: list[dict]) -> bytes:
    """instruments: [{name, preset, bank, zones: [{pcm, rate, root, fine, lo, hi, loop, pan,
    attack_us, release_us, name}]}]"""
    smpl = bytearray()
    shdr = bytearray()
    inst = bytearray()
    ibag = bytearray()
    igen = bytearray()
    phdr = bytearray()
    pbag = bytearray()
    pgen = bytearray()
    sample_idx = 0
    bag_i = gen_i = pbag_i = pgen_i = 0
    for ii, ins in enumerate(instruments):
        inst += _name(ins["name"]) + struct.pack("<H", bag_i)
        for z in ins["zones"]:
            pcm = np.asarray(z["pcm"], dtype="<i2")
            start = len(smpl) // 2
            smpl += pcm.tobytes() + b"\0" * 92  # 46 zero samples padding
            end = start + len(pcm)
            ls, le = (start + z["loop"][0], start + z["loop"][1]) if z.get("loop") else (start, end)
            le = min(max(le, ls + 1), end)
            shdr += _name(z["name"]) + struct.pack("<IIIIIBbHH", start, end, ls, le, int(z["rate"]),
                                                   int(z["root"]) & 0x7F, 0, 0, 1)
            gens = [(GEN_KEYRANGE, z["lo"] | (z["hi"] << 8)),
                    (GEN_PAN, int((z.get("pan", 64) - 64) / 64 * 500)),
                    (GEN_ATTACK, _timecents(z.get("attack_us", 0)) & 0xFFFF),
                    (GEN_RELEASE, _timecents(z.get("release_us", 200000)) & 0xFFFF),
                    (GEN_FINETUNE, int(z.get("fine", 0)) & 0xFFFF),
                    (GEN_ROOTKEY, int(z["root"]) & 0x7F),
                    (GEN_SAMPLEMODES, 1 if z.get("loop") else 0),
                    (GEN_SAMPLEID, sample_idx)]
            ibag += struct.pack("<HH", gen_i, 0)
            bag_i += 1
            for op, amt in gens:
                igen += struct.pack("<HH", op, amt & 0xFFFF)
                gen_i += 1
            sample_idx += 1
        phdr += _name(ins["name"]) + struct.pack("<HHHIII", ins["preset"], ins["bank"], pbag_i, 0, 0, 0)
        pbag += struct.pack("<HH", pgen_i, 0)
        pbag_i += 1
        pgen += struct.pack("<HH", GEN_INSTRUMENT, ii)
        pgen_i += 1
    # terminal records
    phdr += _name("EOP") + struct.pack("<HHHIII", 0, 0, pbag_i, 0, 0, 0)
    pbag += struct.pack("<HH", pgen_i, 0)
    pgen += struct.pack("<HH", 0, 0)
    inst += _name("EOI") + struct.pack("<H", bag_i)
    ibag += struct.pack("<HH", gen_i, 0)
    igen += struct.pack("<HH", 0, 0)
    shdr += _name("EOS") + struct.pack("<IIIIIBbHH", 0, 0, 0, 0, 0, 0, 0, 0, 0)
    mod_term = b"\0" * 10

    info = _list(b"INFO", _chunk(b"ifil", struct.pack("<HH", 2, 1)), _chunk(b"isng", b"EMU8000\0"),
                 _chunk(b"INAM", bank_name.encode("ascii", "replace")[:250] + b"\0"),
                 _chunk(b"ISFT", b"N64 Asset Studio\0"))
    sdta = _list(b"sdta", _chunk(b"smpl", bytes(smpl)))
    pdta = _list(b"pdta", _chunk(b"phdr", bytes(phdr)), _chunk(b"pbag", bytes(pbag)),
                 _chunk(b"pmod", mod_term), _chunk(b"pgen", bytes(pgen)), _chunk(b"inst", bytes(inst)),
                 _chunk(b"ibag", bytes(ibag)), _chunk(b"imod", mod_term), _chunk(b"igen", bytes(igen)),
                 _chunk(b"shdr", bytes(shdr)))
    body = b"sfbk" + info + sdta + pdta
    return b"RIFF" + struct.pack("<I", len(body)) + body


def bank_file_to_sf2(project, fi: int, path: str) -> str:
    """Convert all banks in one ALBankFile to an .sf2."""
    bf = project.bank_files[fi]
    insts = []
    for ref in project.all_sounds():
        if ref.bank_file != fi:
            continue
        bank = ref.bank
        key = (bank.index, ref.instrument.index, ref.instrument.is_percussion)
        entry = next((i for i in insts if i["_key"] == key), None)
        if entry is None:
            perc = ref.instrument.is_percussion
            entry = {"_key": key, "name": f"B{bank.index} {'Perc' if perc else 'Inst'} {ref.instrument.index}",
                     "preset": 0 if perc else min(ref.instrument.index, 127),
                     "bank": 128 if perc else bank.index, "zones": []}
            insts.append(entry)
        try:
            pcm, rate = project.decode_sound(ref)
        except ValueError:
            continue
        w = ref.sound.wave
        loop = (w.loop_start, w.loop_end) if w.loop_count and w.loop_end > w.loop_start else None
        entry["zones"].append({
            "pcm": pcm, "rate": rate, "root": ref.sound.key_base, "fine": ref.sound.detune,
            "lo": ref.sound.key_min, "hi": ref.sound.key_max, "loop": loop, "pan": ref.sound.pan,
            "attack_us": ref.sound.attack, "release_us": ref.sound.release,
            "name": f"s{ref.instrument.index}_{ref.sound.index}",
        })
    insts = [i for i in insts if i["zones"]]
    if not insts:
        raise ValueError("No decodable samples in this bank")
    data = build_sf2(f"N64 bank {fi} @ 0x{bf.offset:X}", insts)
    with open(path, "wb") as f:
        f.write(data)
    return path
