"""Synthesise the XenoDial sound effects as small mono WAVs (no samples, no licences).

  python make_sounds.py      writes Assets/XenoDial/Audio/*.wav
"""
import math, os, random, struct, wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets", "XenoDial", "Audio")


def write(name, samples):
    os.makedirs(OUT, exist_ok=True)
    peak = max(1e-6, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(32000 * 0.9 * s / peak)) for s in samples))


def env(t, dur, attack=0.01, release=0.15):
    a = min(1.0, t / attack) if attack > 0 else 1.0
    r = min(1.0, (dur - t) / release) if release > 0 else 1.0
    return max(0.0, min(a, r))


def tone(dur, f0, f1=None, kind="sine", attack=0.01, release=0.15, vib=0.0):
    f1 = f0 if f1 is None else f1
    out, ph = [], 0.0
    n = int(dur * RATE)
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (t / dur) * (1 + vib * math.sin(2 * math.pi * 7 * t))
        ph += 2 * math.pi * f / RATE
        if kind == "sine":
            s = math.sin(ph)
        elif kind == "square":
            s = 1.0 if math.sin(ph) > 0 else -1.0
        else:  # saw
            s = 2 * ((ph / (2 * math.pi)) % 1.0) - 1
        out.append(s * env(t, dur, attack, release))
    return out


def noise(dur, attack=0.005, release=0.2, lowpass=0.2, seed=1):
    rnd = random.Random(seed)
    out, y = [], 0.0
    n = int(dur * RATE)
    for i in range(n):
        t = i / RATE
        y += lowpass * (rnd.uniform(-1, 1) - y)
        out.append(y * env(t, dur, attack, release))
    return out


def mix(*tracks):
    n = max(len(t) for t, _ in tracks)
    out = [0.0] * n
    for t, g in tracks:
        for i, s in enumerate(t):
            out[i] += s * g
    return out


def seq(*parts):
    out = []
    for p in parts:
        out += p
    return out


def main():
    # Transform: rising sweep + shimmer + whoosh
    write("shift", mix((tone(0.9, 180, 1400, "saw", 0.02, 0.35), 0.35),
                       (tone(0.9, 360, 2800, "sine", 0.02, 0.4, vib=0.02), 0.5),
                       (noise(0.9, 0.3, 0.5, 0.08), 0.6)))
    # Turn back: falling sweep
    write("revert", mix((tone(0.7, 1200, 160, "saw", 0.01, 0.3), 0.35), (tone(0.7, 900, 120, "sine"), 0.5)))
    # Dial click and select blip
    write("click", tone(0.05, 1800, 1500, "square", 0.001, 0.04))
    write("open", seq(tone(0.07, 880, 880, "sine", 0.002, 0.03), tone(0.09, 1320, 1320, "sine", 0.002, 0.06)))
    # Time-out warning beep
    write("beep", tone(0.12, 1760, 1760, "square", 0.002, 0.03))
    # Powers
    write("fire", mix((noise(0.45, 0.01, 0.35, 0.25, seed=2), 0.8), (tone(0.45, 300, 90, "saw", 0.005, 0.3), 0.3)))
    write("slam", mix((noise(0.8, 0.002, 0.7, 0.05, seed=3), 1.0), (tone(0.8, 90, 35, "sine", 0.002, 0.6), 0.9)))
    write("dash", mix((noise(0.4, 0.02, 0.3, 0.4, seed=4), 0.6), (tone(0.4, 500, 2400, "sine", 0.01, 0.3), 0.3)))
    write("flap", noise(0.3, 0.03, 0.25, 0.06, seed=5))
    write("hop", tone(0.18, 500, 1300, "sine", 0.005, 0.1))
    write("crystal", mix((tone(0.6, 2093, 2093, "sine", 0.002, 0.55), 0.5), (tone(0.6, 3136, 3136, "sine", 0.002, 0.5), 0.3),
                         (tone(0.6, 1568, 1568, "sine", 0.002, 0.5), 0.3)))
    write("smash", mix((noise(0.5, 0.001, 0.45, 0.35, seed=6), 0.9), (tone(0.5, 220, 60, "square", 0.001, 0.4), 0.2)))
    write("lightning", mix((noise(0.5, 0.001, 0.45, 0.9, seed=7), 0.7), (tone(0.5, 2400, 300, "square", 0.001, 0.4), 0.25)))
    write("blink", mix((tone(0.35, 400, 3000, "sine", 0.005, 0.2), 0.5), (tone(0.35, 3000, 400, "sine", 0.005, 0.2), 0.4)))
    write("thrust", noise(0.45, 0.05, 0.2, 0.15, seed=8))
    write("grapple", mix((tone(0.3, 900, 200, "saw", 0.001, 0.2), 0.4), (noise(0.3, 0.001, 0.25, 0.5, seed=9), 0.3)))
    write("launch", mix((tone(0.6, 120, 900, "sine", 0.005, 0.3), 0.6), (noise(0.6, 0.01, 0.5, 0.1, seed=10), 0.5)))
    write("spin", mix((noise(0.6, 0.05, 0.4, 0.2, seed=11), 0.6), (tone(0.6, 300, 300, "sine", 0.05, 0.4, vib=0.3), 0.4)))
    write("blast", mix((noise(1.0, 0.002, 0.9, 0.08, seed=12), 1.0), (tone(1.0, 70, 30, "sine", 0.002, 0.8), 0.9)))
    # Bell at the top of the tower, and the vault / race finish fanfare
    write("bell", mix((tone(2.0, 660, 660, "sine", 0.002, 1.9), 0.6), (tone(2.0, 1650, 1650, "sine", 0.002, 1.2), 0.25),
                      (tone(2.0, 2640, 2640, "sine", 0.002, 0.8), 0.15)))
    write("win", seq(tone(0.12, 784, 784, "square", 0.005, 0.04), tone(0.12, 988, 988, "square", 0.005, 0.04),
                     tone(0.35, 1175, 1175, "square", 0.005, 0.25)))
    print("wrote sounds to", OUT)


if __name__ == "__main__":
    main()
