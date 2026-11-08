"""Synthesises every sound in Fir Warning from scratch (no samples, no licences to worry about)
and writes small mono .ogg files into Assets/FirHorror/Audio.

  python make_audio.py            (needs numpy and ffmpeg on PATH)

The songs use "O Tannenbaum" (traditional, public domain). Everything is mono 22.05 kHz
Vorbis, so the whole soundtrack stays around 1-2 MB, which keeps the Quest upload small.
"""
import os, subprocess, tempfile, wave
import numpy as np

SR = 22050
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets", "FirHorror", "Audio")
rng = np.random.default_rng(1996)


def t(sec):
    return np.arange(int(sec * SR)) / SR


def norm(x, peak=0.9):
    m = np.max(np.abs(x)) or 1.0
    return x / m * peak


def env(n, a=0.01, r=0.1):
    e = np.ones(n)
    na, nr = max(1, int(a * SR)), max(1, int(r * SR))
    e[:na] = np.linspace(0, 1, na)
    e[-nr:] *= np.linspace(1, 0, nr)
    return e


def lowpass(x, cutoff):
    """One-pole low-pass, cheap and good enough for rumble and muffling."""
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def bandpass(x, f, q=8.0):
    """Biquad band-pass (RBJ cookbook)."""
    w = 2 * np.pi * f / SR
    alpha = np.sin(w) / (2 * q)
    b0, b1, b2 = alpha, 0, -alpha
    a0, a1, a2 = 1 + alpha, -2 * np.cos(w), 1 - alpha
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        o = (b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, v, y1, o
        y[i] = o
    return y


def noise(sec):
    return rng.uniform(-1, 1, int(sec * SR))


def saw(freq_curve):
    ph = np.cumsum(freq_curve) / SR
    return 2 * (ph % 1.0) - 1


def sine(freq_curve):
    return np.sin(2 * np.pi * np.cumsum(freq_curve) / SR)


VOWELS = {  # formant centres (Hz) for a cartoonish animatronic voice
    "a": (800, 1200, 2500), "o": (500, 900, 2400), "e": (500, 1900, 2600),
    "i": (320, 2300, 3000), "u": (350, 700, 2400), "h": (600, 1500, 2500),
}


def voice(freq_curve, vowel_seq, buzz=1.0):
    """Source-filter speech-ish synthesis: a saw buzz through three moving formants."""
    src = saw(freq_curve) * buzz + noise(len(freq_curve) / SR) * 0.08
    n = len(src)
    out = np.zeros(n)
    seg = max(1, n // max(1, len(vowel_seq)))
    for k, v in enumerate(vowel_seq):
        lo, hi = k * seg, n if k == len(vowel_seq) - 1 else (k + 1) * seg
        part = src[lo:hi]
        f1, f2, f3 = VOWELS[v]
        y = bandpass(part, f1, 5) * 1.0 + bandpass(part, f2, 7) * 0.6 + bandpass(part, f3, 9) * 0.3
        out[lo:hi] = y * env(len(y), 0.01, 0.02)
    return out


MIDI = lambda m: 440.0 * 2 ** ((m - 69) / 12)
# O Tannenbaum (public domain): (midi note, beats)
TANNENBAUM = [(60, 1), (65, .75), (65, .25), (65, 1.5), (67, .5), (69, .75), (69, .25), (69, 1.5), (69, .5),
              (67, .5), (69, .5), (70, 1), (64, 1), (67, 1), (65, 1), (0, 1),
              (72, .5), (72, .5), (69, .5), (74, 1.5), (72, .5), (72, .5), (70, .5), (70, 1.5),
              (70, .5), (70, .5), (67, .5), (72, 1.5), (70, .5), (70, .5), (69, .5), (69, 1.5), (0, .5)]


def music_box(bpm=84, detune=0.0, wobble=0.0):
    beat = 60 / bpm
    parts = []
    for i, (m, b) in enumerate(TANNENBAUM):
        n = int(b * beat * SR)
        if m == 0:
            parts.append(np.zeros(n)); continue
        f = MIDI(m) * 2 * (1 + detune * rng.uniform(-1, 1))
        tt = np.arange(n) / SR
        fc = f * (1 + wobble * np.sin(2 * np.pi * 0.7 * (tt + i)))
        tone = sine(fc) + 0.4 * sine(fc * 2.01) + 0.2 * sine(fc * 4.2)
        parts.append(tone * np.exp(-tt * 4.5))
    x = np.concatenate(parts)
    return x


def sung_melody(bpm=70, pitch=1.0, vowels="ooaoaoaoaeoaoaoaoaeoaoaoaeoaoaoa", wobble=0.02):
    beat = 60 / bpm
    parts = []
    for i, (m, b) in enumerate(TANNENBAUM):
        n = int(b * beat * SR)
        if m == 0:
            parts.append(np.zeros(n)); continue
        tt = np.arange(n) / SR
        f = MIDI(m - 12) * pitch * (1 + wobble * np.sin(2 * np.pi * 5.5 * tt)) * (1 + 0.01 * rng.uniform(-1, 1))
        parts.append(voice(f, vowels[i % len(vowels)]) * env(n, 0.02, 0.08))
    return np.concatenate(parts)


def scream(sec=1.6, base=900, seed=0):
    r = np.random.default_rng(seed)
    tt = t(sec)
    f = base * (1 + 0.6 * np.sin(2 * np.pi * r.uniform(5, 11) * tt)) * np.linspace(1.3, 0.7, len(tt))
    s = voice(f, "aaei", buzz=1.0) * 1.4 + saw(f * 1.51) * 0.25
    hit = sine(np.linspace(90, 30, len(tt))) * np.exp(-tt * 6) * 1.2
    nz = lowpass(noise(sec), 3000) * np.exp(-tt * 3) * 0.8
    return np.tanh(2.5 * (s * env(len(tt), 0.005, 0.4) + hit + nz))


def write(name, x, loop=False):
    x = norm(x)
    if loop:  # crossfade the tail into the head so the loop has no click
        n = int(0.5 * SR)
        x[:n] = x[:n] * np.linspace(0, 1, n) + x[-n:] * np.linspace(1, 0, n)
        x = x[:-n]
    pcm = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    os.makedirs(OUT, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        wav = os.path.join(tmp, name + ".wav")
        with wave.open(wav, "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
        out = os.path.join(OUT, name + ".ogg")
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", "3", out], check=True)
    print(f"{name}.ogg  {len(x) / SR:5.1f}s  {os.path.getsize(out) // 1024} KB")


def main():
    # ---- loops
    tt = t(32)
    drone = sine(np.full(len(tt), 55)) * 0.5 + sine(np.full(len(tt), 55.7)) * 0.4 + sine(np.full(len(tt), 82.4)) * 0.2
    wind = lowpass(noise(32), 400) * (0.6 + 0.4 * np.sin(2 * np.pi * tt / 8))
    write("amb_drone", drone * 0.6 + wind * 2.5, loop=True)

    hum = sine(np.full(len(tt), 60)) * 0.3 + sine(np.full(len(tt), 120)) * 0.2 + lowpass(noise(32), 200) * 1.5
    for k in range(6):  # distant creaks
        at = int(rng.uniform(1, 30) * SR)
        cr = bandpass(noise(0.6), rng.uniform(300, 700), 30) * np.exp(-t(0.6) * 5) * 3
        hum[at:at + len(cr)] += cr[:len(hum) - at]
    write("amb_warehouse", hum, loop=True)

    furn = lowpass(noise(20), 250) * 3 + lowpass(noise(20), 900) * (0.5 + 0.5 * np.sin(2 * np.pi * t(20) * 0.3))
    write("amb_furnace", furn, loop=True)

    write("music_box", music_box(84, 0.0, 0.0))
    write("music_box_broken", music_box(62, 0.03, 0.025) * np.linspace(1, 0.6, len(music_box(62, 0.03, 0.025))))
    write("tree_sing", sung_melody(76, 1.0, wobble=0.015))
    write("tree_sing_wrong", sung_melody(52, 0.82, wobble=0.06))

    # ---- voices
    parts = []
    for _ in range(9):
        d = rng.uniform(0.12, 0.3)
        f = np.full(int(d * SR), 0.0) + rng.uniform(150, 230) * np.linspace(1.1, 0.9, int(d * SR))
        parts += [voice(f, rng.choice(list("aoeiu"))), np.zeros(int(rng.uniform(0.02, 0.1) * SR))]
    write("tree_talk", np.concatenate(parts))
    parts = []
    for k in range(3):
        f = np.linspace(140, 110, int(0.32 * SR)) * 0.8
        parts += [voice(f, "ho"), np.zeros(int(0.12 * SR))]
    write("tree_laugh", np.concatenate(parts))
    w = bandpass(noise(4), 2500, 2) * (0.5 + 0.5 * np.abs(np.sin(2 * np.pi * t(4) * 2.3))) + voice(np.full(int(4 * SR), 95.0), "huhaho", 0.15) * 0.6
    write("whisper", w * env(len(w), 0.4, 0.8))

    # ---- stings and one-shots
    for k, base in enumerate([900, 650, 1200]):
        write(f"scream{k + 1}", scream(1.6 + 0.3 * k, base, seed=k))
    write("scare_hit", np.tanh(3 * (sine(np.linspace(120, 25, int(1.2 * SR))) * np.exp(-t(1.2) * 3)
                                    + lowpass(noise(1.2), 5000) * np.exp(-t(1.2) * 9))))
    sv = saw(400 + 120 * np.sin(2 * np.pi * 3 * t(0.9))) * 0.4
    write("servo", lowpass(sv, 1800) * env(len(sv), 0.05, 0.2))
    cr = sum(bandpass(noise(1.0), f, 25) for f in (280, 410, 620)) * (0.4 + 0.6 * np.abs(np.sin(2 * np.pi * 7 * t(1.0))))
    write("creak", cr * env(len(cr), 0.05, 0.3))
    ru = bandpass(noise(0.8), 4000, 1.5) * (0.3 + np.abs(np.sin(2 * np.pi * 9 * t(0.8))))
    write("rustle", ru * env(len(ru), 0.02, 0.3))
    ch = sum(sine(np.full(int(0.8 * SR), MIDI(m))) * np.exp(-t(0.8) * 5) for m in (84, 88, 91))
    write("pickup", ch)
    bz = saw(np.full(int(0.7 * SR), 110.0)) + saw(np.full(int(0.7 * SR), 116.0))
    write("wrong", lowpass(bz, 1500) * env(len(bz), 0.01, 0.05))
    clunk = sine(np.linspace(160, 60, int(0.4 * SR))) * np.exp(-t(0.4) * 12) + lowpass(noise(0.4), 1200) * np.exp(-t(0.4) * 20)
    bell = sum(sine(np.full(int(1.0 * SR), MIDI(m))) * np.exp(-t(1.0) * 3) for m in (79, 86))
    write("unlock", np.concatenate([clunk, bell * 0.5]))
    ring1 = sine(np.full(int(1.0 * SR), 440.0)) * sine(np.full(int(1.0 * SR), 20.0)) + sine(np.full(int(1.0 * SR), 480.0)) * 0.5
    write("phone", np.concatenate([ring1, np.zeros(int(1.0 * SR)), ring1, np.zeros(int(0.5 * SR))]))
    beat = sine(np.linspace(70, 45, int(0.15 * SR))) * np.exp(-t(0.15) * 25)
    hb = np.zeros(int(0.9 * SR)); hb[:len(beat)] += beat; hb[int(0.25 * SR):int(0.25 * SR) + len(beat)] += beat * 0.7
    write("heartbeat", np.tile(hb, 4))
    st = noise(1.5) * (0.5 + 0.5 * (rng.random(int(1.5 * SR)) > 0.97))
    write("static", st * env(len(st), 0.01, 0.2))
    whoosh = bandpass(noise(1.2), 800, 1.2) * np.sin(np.pi * np.linspace(0, 1, int(1.2 * SR))) ** 2
    write("whoosh", whoosh)
    write("ending", music_box(96, 0.0, 0.0)[: int(14 * SR)])


if __name__ == "__main__":
    main()
