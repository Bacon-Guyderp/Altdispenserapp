"""Audio writers: WAV, AIFF, FLAC, AU, raw PCM natively, plus any format
ffmpeg can produce (OGG, MP3, OPUS, M4A, WMA, ...) when ffmpeg is installed."""
from __future__ import annotations

import os
import shutil
import struct
import subprocess
import tempfile
import wave

import numpy as np

NATIVE_AUDIO = {
    "wav": "WAV (PCM 16-bit)",
    "aiff": "AIFF (PCM 16-bit)",
    "flac": "FLAC (lossless)",
    "au": "Sun AU",
    "raw": "Raw PCM s16le",
}
FFMPEG_AUDIO = {
    "ogg": "Ogg Vorbis", "mp3": "MP3", "opus": "Opus", "m4a": "AAC (.m4a)", "wma": "WMA",
}

_ffmpeg_path: str | None = None


def set_ffmpeg_path(p: str | None) -> None:
    global _ffmpeg_path
    _ffmpeg_path = p or None


def find_ffmpeg() -> str | None:
    if _ffmpeg_path and os.path.isfile(_ffmpeg_path):
        return _ffmpeg_path
    return shutil.which("ffmpeg")


def audio_formats() -> dict[str, str]:
    out = dict(NATIVE_AUDIO)
    if find_ffmpeg():
        out.update(FFMPEG_AUDIO)
    return out


def wav_bytes(pcm: np.ndarray, rate: int) -> bytes:
    import io
    bio = io.BytesIO()
    with wave.open(bio, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(int(rate))
        w.writeframes(np.asarray(pcm, dtype="<i2").tobytes())
    return bio.getvalue()


def _ieee_extended(v: float) -> bytes:
    import math
    if v == 0:
        return b"\0" * 10
    m, e = math.frexp(v)
    e += 16382
    mant = int(m * (1 << 64))
    return struct.pack(">HQ", e, mant)


def _aiff(pcm: np.ndarray, rate: int) -> bytes:
    data = np.asarray(pcm, dtype=">i2").tobytes()
    comm = struct.pack(">hIh", 1, len(pcm), 16) + _ieee_extended(float(rate))
    ssnd = struct.pack(">II", 0, 0) + data
    body = b"AIFF" + b"COMM" + struct.pack(">I", len(comm)) + comm + b"SSND" + struct.pack(">I", len(ssnd)) + ssnd
    return b"FORM" + struct.pack(">I", len(body)) + body


def _au(pcm: np.ndarray, rate: int) -> bytes:
    data = np.asarray(pcm, dtype=">i2").tobytes()
    return struct.pack(">4sIIIII", b".snd", 24, len(data), 3, int(rate), 1) + data


# --------------------------------------------------------------- FLAC ----
def _crc8(data: bytes) -> int:
    crc = 0
    for b in data:
        crc ^= b
        for _ in range(8):
            crc = ((crc << 1) ^ 0x07) & 0xFF if crc & 0x80 else (crc << 1) & 0xFF
    return crc


_CRC16 = []
for _i in range(256):
    _c = _i << 8
    for _ in range(8):
        _c = ((_c << 1) ^ 0x8005) & 0xFFFF if _c & 0x8000 else (_c << 1) & 0xFFFF
    _CRC16.append(_c)


def _crc16(data: bytes) -> int:
    crc = 0
    for b in data:
        crc = ((crc << 8) & 0xFFFF) ^ _CRC16[(crc >> 8) ^ b]
    return crc


def _utf8_num(n: int) -> bytes:
    if n < 0x80:
        return bytes([n])
    if n < 0x800:
        return bytes([0xC0 | (n >> 6), 0x80 | (n & 0x3F)])
    if n < 0x10000:
        return bytes([0xE0 | (n >> 12), 0x80 | ((n >> 6) & 0x3F), 0x80 | (n & 0x3F)])
    return bytes([0xF0 | (n >> 18), 0x80 | ((n >> 12) & 0x3F), 0x80 | ((n >> 6) & 0x3F), 0x80 | (n & 0x3F)])


def _flac(pcm: np.ndarray, rate: int) -> bytes:
    """FLAC using VERBATIM subframes - valid, lossless, needs no encoder."""
    pcm = np.asarray(pcm, dtype=np.int16)
    block = 4096
    total = len(pcm)
    rate = int(rate)
    streaminfo = struct.pack(">HH", block, block) + b"\0\0\0" + b"\0\0\0"
    # 20 bits rate, 3 bits channels-1, 5 bits bps-1, 36 bits total samples
    v = (rate << 44) | (0 << 41) | (15 << 36) | total
    streaminfo += v.to_bytes(8, "big") + b"\0" * 16
    out = bytearray(b"fLaC")
    out += bytes([0x80]) + len(streaminfo).to_bytes(3, "big") + streaminfo
    for fi, start in enumerate(range(0, total, block)):
        chunk = pcm[start:start + block]
        n = len(chunk)
        hdr = bytearray(b"\xFF\xF8")
        # block size code 7 (8-bit n-1 at end), sample rate code 0 (from streaminfo)
        hdr.append((0x7 << 4) | 0x0)
        # channel assignment 0 (mono), sample size code 4 (16 bit)
        hdr.append((0 << 4) | (4 << 1))
        hdr += _utf8_num(fi)
        if n - 1 < 256:
            hdr[2] = (0x6 << 4)
            hdr.append(n - 1)
        else:
            hdr += struct.pack(">H", n - 1)
        hdr.append(_crc8(bytes(hdr)))
        frame = bytes(hdr) + b"\x02" + chunk.astype(">i2").tobytes()
        out += frame + struct.pack(">H", _crc16(frame))
    return bytes(out)


def save_audio(pcm: np.ndarray, rate: int, path: str, fmt: str | None = None) -> str:
    ext = (fmt or os.path.splitext(path)[1].lstrip(".")).lower()
    if ext == "aif":
        ext = "aiff"
    if not path.lower().endswith("." + ext):
        path = os.path.splitext(path)[0] + "." + ext
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    pcm = np.asarray(pcm, dtype=np.int16)
    if ext == "wav":
        data = wav_bytes(pcm, rate)
    elif ext == "aiff":
        data = _aiff(pcm, rate)
    elif ext == "flac":
        data = _flac(pcm, rate)
    elif ext == "au":
        data = _au(pcm, rate)
    elif ext == "raw":
        data = pcm.astype("<i2").tobytes()
    elif ext in FFMPEG_AUDIO:
        ff = find_ffmpeg()
        if not ff:
            raise RuntimeError(f"Exporting .{ext} needs ffmpeg (install it or set its path in Settings)")
        with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
            tmp.write(wav_bytes(pcm, rate))
            tmp_path = tmp.name
        try:
            flags = 0x08000000 if os.name == "nt" else 0  # CREATE_NO_WINDOW
            subprocess.run([ff, "-y", "-loglevel", "error", "-i", tmp_path, path], check=True,
                           creationflags=flags)
        finally:
            os.unlink(tmp_path)
        return path
    else:
        raise ValueError(f"unsupported audio format {ext}")
    with open(path, "wb") as f:
        f.write(data)
    return path
