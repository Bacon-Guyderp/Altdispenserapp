"""Records the two skeleton voice lines for the Living Room closet easter egg.

  python make_voices.py     (needs espeak-ng, the MBROLA us2/us3 voices, and ffmpeg)
                            Debian/Ubuntu: apt install espeak-ng mbrola mbrola-us2 mbrola-us3

Writes Assets/FirHorror/Audio/skeleton_white.ogg and skeleton_red.ogg, plus one line per comedy
gag in Assets/FirHorror/Data/gags.txt to Assets/FirHorror/Audio/Gags/gag_<id>.ogg (mono Vorbis).
Gag voices need mbrola-us1 too.
"""
import os, subprocess, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets", "FirHorror", "Audio")

LINES = [
    # name, MBROLA voice, words/min, pitch (0-99), text, ffmpeg filter (pitch shift + closet echo)
    ("skeleton_white", "mb-us2", 118, 32, "A skeleton.",
     "asetrate=16000*0.93,aresample=22050,aecho=0.8:0.6:45:0.22,volume=1.8"),
    ("skeleton_red", "mb-us3", 150, 62, "Red skeleton. Ha!",
     "asetrate=16000*1.06,aresample=22050,aecho=0.8:0.6:40:0.2,volume=1.8"),
]


# gag voice presets: MBROLA voice, words/min, pitch (0-99), playback-rate shift
PRESETS = {
    "tree": ("mb-us3", 150, 55, 1.0),
    "tiny": ("mb-us3", 165, 75, 1.3),
    "big": ("mb-us2", 125, 25, 0.85),
    "narrator": ("mb-us1", 145, 45, 1.0),
    "lady": ("mb-us1", 150, 65, 1.05),
    "old": ("mb-us2", 110, 40, 0.95),
}


def gag_lines():
    path = os.path.join(os.path.dirname(OUT), "Data", "gags.txt")
    for line in open(path, encoding="utf-8"):
        if not line.strip() or line.startswith("#"):
            continue
        f = line.rstrip("\n").split("|")
        gid, voice, text = f[0], f[6], f[7]
        v, speed, pitch, rate = PRESETS[voice]
        yield ("Gags/gag_" + gid, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:30:0.12,volume=1.8")


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(os.path.join(OUT, "Gags"), exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        for name, voice, speed, pitch, text, filt in list(LINES) + list(gag_lines()):
            wav = os.path.join(tmp, name.replace("/", "_") + ".wav")
            subprocess.run(["espeak-ng", "-v", voice, "-s", str(speed), "-p", str(pitch), "-w", wav, text], check=True)
            out = os.path.join(OUT, name + ".ogg")
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-af", filt + ",apad=pad_dur=0.25",
                            "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", out], check=True)
            print(name, os.path.getsize(out) // 1024, "KB")


if __name__ == "__main__":
    main()
