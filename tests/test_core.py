"""Core tests - run with:  python -m pytest tests  (or python tests/test_core.py)"""
from __future__ import annotations

import json
import os
import struct
import sys
import tarfile
import tempfile
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
sys.path.insert(0, HERE)

from make_test_rom import (BOOK, adpcm_frames, build_rom, mio0_compress,  # noqa: E402
                           yay0_compress, yaz0_compress)
from n64studio import audio, compression, games, mips, rom as romlib  # noqa: E402
from n64studio.export import batch, images, models, soundfont, sounds, unity  # noqa: E402
from n64studio.project import Project  # noqa: E402

_PROJECT = None


def _write_rom(ext: str) -> str:
    data, _ = build_rom()
    d = tempfile.mkdtemp()
    p = os.path.join(d, f"test.{ext}")
    with open(p, "wb") as f:
        f.write(romlib.from_big_endian(data, ext))
    return p


def project() -> Project:
    global _PROJECT
    if _PROJECT is None:
        _PROJECT = Project.open(_write_rom("z64"))
        _PROJECT.scan()
    return _PROJECT


# ------------------------------------------------------------------------
def test_byte_orders():
    big, _ = build_rom()
    for ext in ("z64", "v64", "n64"):
        r = romlib.load_rom(_write_rom(ext))
        assert r.original_order == ext
        assert bytes(r.data) == big
        assert r.header.title == "N64STUDIO TEST"
        assert r.header.game_code == "NTSE"
        assert r.header.entry_point == 0x80000400


def test_compression_roundtrip():
    payload = (b"hello N64 world " * 50) + bytes(range(256)) * 4 + b"\0" * 700
    for comp, dec in ((yaz0_compress, compression.yaz0_decompress),
                      (yay0_compress, compression.yay0_decompress),
                      (mio0_compress, compression.mio0_decompress)):
        c = comp(payload)
        out, used = dec(c)
        assert out == payload
        assert used <= len(c)
        assert len(c) < len(payload)


def test_rare_and_gzip():
    import gzip
    import zlib
    payload = bytes(range(200)) * 10
    raw = zlib.compressobj(9, zlib.DEFLATED, -15)
    body = raw.compress(payload) + raw.flush()
    blk = b"\x11\x72" + len(payload).to_bytes(3, "big") + body
    out, used = compression.rare_decompress(b"\0\0" + blk, 2)
    assert out == payload and used == len(blk)
    g = gzip.compress(payload)
    found = compression.scan_compressed(b"\xAA" * 16 + g + b"\xBB" * 16)
    assert any(b.kind == "gzip" and b.data == payload for b in found)


def _reference_vadpcm(data: bytes, coefs: list[int]) -> list[int]:
    """Straight port of the order-2 decoder used by the N64 decomp tools."""
    out = []
    last = [0] * 8
    for f in range(len(data) // 9):
        hdr = data[f * 9]
        scale = hdr >> 4
        pred = hdr & 0xF
        p1 = coefs[pred * 16: pred * 16 + 8]
        p2 = coefs[pred * 16 + 8: pred * 16 + 16]
        body = data[f * 9 + 1: f * 9 + 9]
        for half in range(2):
            tmp = []
            for b in body[half * 4: half * 4 + 4]:
                for nib in (b >> 4, b & 0xF):
                    v = nib - 16 if nib >= 8 else nib
                    tmp.append(v << scale)
            cur = []
            for i in range(8):
                total = p1[i] * last[6] + p2[i] * last[7]
                for x in range(i - 1, -1, -1):
                    total += tmp[(i - 1) - x] * p2[x]
                v = ((tmp[i] << 11) + total) >> 11
                cur.append(max(-32768, min(32767, v)))
            last = cur
            out += cur
    return out


def test_vadpcm_matches_reference():
    data = adpcm_frames(30)
    book = audio.AdpcmBook(2, 2, BOOK)
    ours = audio.decode_vadpcm(data, book).tolist()
    assert ours == _reference_vadpcm(data, BOOK)


def test_texture_formats():
    from n64studio.texture import decode_texture
    rgba16 = struct.pack(">H", (31 << 11) | 1) * 4
    img = decode_texture(rgba16, 0, 2, 2, "RGBA16")
    assert img.getpixel((0, 0)) == (255, 0, 0, 255)
    ia8 = bytes([0xF0]) * 4
    assert decode_texture(ia8, 0, 2, 2, "IA8").getpixel((1, 1)) == (255, 255, 255, 0)
    i4 = bytes([0xF0, 0x0F])
    im = decode_texture(i4, 0, 2, 2, "I4")
    assert im.getpixel((0, 0))[0] == 255 and im.getpixel((1, 0))[0] == 0


def test_scan_finds_everything():
    p = project()
    _, info = build_rom()
    kinds = sorted(b.kind for b in p.blobs[1:])
    assert kinds == ["MIO0", "Yay0", "Yaz0"]
    ucodes = sorted(m.dl.ucode for m in p.models)
    assert ucodes == ["F3D", "F3DEX", "F3DEX2", "F3DEX2"]
    for m in p.models:
        mesh = p.get_mesh(m)
        assert len(mesh.triangles) in (12, 2)
        xs = sorted({abs(v.x) for v in mesh.vertices})
        assert xs == [100.0], (m.name, xs)
    raw = next(m for m in p.models if m.blob_index == 0)
    assert raw.dl.offset == info["raw_dl"] and raw.dl.base == info["raw_obj_off"]
    assert {t.fmt for t in p.textures} == {"RGBA16", "CI4"}
    ci = next(t for t in p.textures if t.fmt == "CI4")
    img = p.texture_image(ci)
    assert img.size == (16, 16) and len(img.getcolors()) == 16


def test_audio_and_tbl():
    p = project()
    _, info = build_rom()
    assert len(p.bank_files) == 1
    assert p.locate_tbl(0) == (0, info["tbl"])
    refs = p.all_sounds()
    assert len(refs) == 2
    pcm, rate = p.decode_sound(refs[1])
    assert rate == 22050 and pcm[0] == -10000 and len(pcm) == 64


def test_cseq_to_midi():
    p = project()
    cseq = next(s for s in p.sequences if s.kind == "CSeq")
    midi = p.sequence_midi(cseq)
    assert midi[:4] == b"MThd"
    trk = midi[midi.index(b"MTrk") + 8:]
    note_ons = sum(1 for i in range(len(trk) - 2) if trk[i] == 0x90 and trk[i + 2] == 0x64)
    assert note_ons == 5  # 3 literal notes + 2 copied by the back-reference
    assert b"\xFF\x51\x03\x07\xA1\x20" in trk
    assert any(s.kind == "MIDI" for s in p.sequences)


def test_mips():
    ins = mips.decode(0x27BDFFE8, 0x80000400)
    assert ins.text() == "addiu    sp, sp, -0x18"
    assert mips.decode(0x0C000108, 0x80000408).target == 0x80000420
    assert mips.decode(0x03E00008, 0).pseudo == "return;"
    p = project()
    assert 0x80000420 in p.functions
    c = mips.function_to_pseudo_c(p.rom.data, 0x1000, 0x80000400)
    assert "func_80000420();" in c and "return;" in c


def test_zelda_dmadata():
    payloads = [bytes([i]) * 0x200 + b"OBJECT%03d" % i for i in range(25)]
    rom = bytearray(0x1060)
    table_off = len(rom)
    entries = 2 + len(payloads)
    rom += b"\0" * (entries * 16 + 16)
    vrom = 0x1060 + entries * 16 + 16
    rows = [(0, 0x1060, 0, 0), (0x1060, vrom, 0x1060, 0)]
    for i, pl in enumerate(payloads):
        start = len(rom)
        if i % 2:
            c = yaz0_compress(pl)
            rom += c
            rows.append((vrom, vrom + len(pl), start, start + len(c)))
        else:
            rom += pl
            rows.append((vrom, vrom + len(pl), start, 0))
        while len(rom) % 16:
            rom.append(0)
        vrom += len(pl)
    for i, r in enumerate(rows):
        struct.pack_into(">IIII", rom, table_off + i * 16, *r)
    out = games.find_zelda_dmadata(bytes(rom))
    by_index = {f.index: f for f in out}
    assert len(out) == entries
    for i, pl in enumerate(payloads):
        assert by_index[i + 2].data == pl


def test_exports_and_formats():
    p = project()
    mesh = p.get_mesh(next(m for m in p.models if m.dl.ucode == "F3DEX2"))
    out = tempfile.mkdtemp()
    for fmt in models.MODEL_FORMATS:
        files = models.export_model(mesh, os.path.join(out, fmt, "cube." + fmt), fmt)
        assert all(os.path.getsize(f) > 0 for f in files)
    gltf = json.load(open(os.path.join(out, "gltf", "cube.gltf")))
    assert gltf["accessors"][0]["count"] == len(mesh.vertices)
    with open(os.path.join(out, "glb", "cube.glb"), "rb") as f:
        assert f.read(4) == b"glTF"
    with open(os.path.join(out, "fbx", "cube.fbx"), "rb") as f:
        assert f.read(20) == b"Kaydara FBX Binary  "
    img = p.texture_image(p.textures[0])
    for ext in images.IMAGE_FORMATS:
        assert os.path.exists(images.save_image(img, os.path.join(out, "img", "t." + ext), ext))
    pcm = np.array([0, 1000, -1000, 32767, -32768] * 2000, dtype=np.int16)
    for ext in sounds.NATIVE_AUDIO:
        assert os.path.getsize(sounds.save_audio(pcm, 22050, os.path.join(out, "snd", "s." + ext), ext)) > 0
    with wave.open(os.path.join(out, "snd", "s.wav")) as wv:
        assert wv.getframerate() == 22050 and wv.getnframes() == len(pcm)
    sf2 = soundfont.bank_file_to_sf2(p, 0, os.path.join(out, "bank.sf2"))
    with open(sf2, "rb") as f:
        head = f.read(12)
    assert head[:4] == b"RIFF" and head[8:12] == b"sfbk"


def test_flac_decodes_with_ffmpeg():
    if not sounds.find_ffmpeg():
        return
    import subprocess
    pcm = (np.sin(np.arange(10000) / 10) * 20000).astype(np.int16)
    d = tempfile.mkdtemp()
    path = sounds.save_audio(pcm, 32000, os.path.join(d, "x.flac"), "flac")
    raw = subprocess.run(["ffmpeg", "-loglevel", "error", "-i", path, "-f", "s16le", "-"],
                         capture_output=True, check=True).stdout
    assert np.array_equal(np.frombuffer(raw, "<i2"), pcm)


def test_batch_and_unitypackage():
    p = project()
    out = tempfile.mkdtemp()
    opt = batch.BatchOptions(model_format="obj", unity_package=True, code=True, raw_files=True)
    report = batch.export_all(p, out, opt)
    assert any("Unity package" in r for r in report), report
    pkg = os.path.join(out, "N64STUDIO_TEST.unitypackage")
    with tarfile.open(pkg) as tar:
        names = tar.getnames()
        paths = [tar.extractfile(n).read().decode() for n in names if n.endswith("/pathname")]
    assert any(x.endswith(".fbx") for x in paths)
    assert any(x.endswith(".wav") for x in paths)
    assert all(x.startswith("Assets/N64") for x in paths)
    assert any(x.startswith("Assets/N64/N64STUDIO_TEST/Models/") for x in paths)
    assert unity._guid("a") != unity._guid("b")


if __name__ == "__main__":
    failed = 0
    for name, fn in sorted(globals().items()):
        if name.startswith("test_") and callable(fn):
            try:
                fn()
                print("PASS", name)
            except Exception as e:  # noqa: BLE001
                failed += 1
                import traceback
                traceback.print_exc()
                print("FAIL", name, e)
    sys.exit(1 if failed else 0)
