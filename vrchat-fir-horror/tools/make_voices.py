"""Records the two skeleton voice lines for the Living Room closet easter egg.

  python make_voices.py     (needs espeak-ng, the MBROLA us2/us3 voices, and ffmpeg)
                            Debian/Ubuntu: apt install espeak-ng mbrola mbrola-us2 mbrola-us3

Writes Assets/FirHorror/Audio/skeleton_white.ogg and skeleton_red.ogg, plus one line per comedy
gag in Assets/FirHorror/Data/gags.txt, the rainbow skeletons' lines to Audio/Rainbow/rb_<colour>.ogg (--rainbow: only those), and the combination acts' lines to Audio/Combo/<name>.ogg (--combo: only those), to Assets/FirHorror/Audio/Gags/gag_<id>.ogg and each skeleton routine
in Data/skeleton_gags.txt to Audio/Gags/skel_<id>.ogg (mono Vorbis).
Gag voices need mbrola-us1 too.
"""
import os, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets", "FirHorror", "Audio")

# every skeleton line is played 43% louder than the tree and narrator gag voices
SKELETON_BOOST = 1.43

LINES = [
    # name, MBROLA voice, words/min, pitch (0-99), text, ffmpeg filter (pitch shift + closet echo)
    ("skeleton_white", "mb-us2", 118, 32, "A skeleton.",
     f"asetrate=16000*0.93,aresample=22050,aecho=0.8:0.6:45:0.22,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled"),
    ("skeleton_red", "mb-us3", 150, 62, "Red skeleton. Ha!",
     f"asetrate=16000*1.06,aresample=22050,aecho=0.8:0.6:40:0.2,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled"),
]


# gag voice presets: MBROLA voice, words/min, pitch (0-99), playback-rate shift
PRESETS = {
    "tree": ("mb-us3", 150, 55, 1.0),
    "tiny": ("mb-us3", 165, 75, 1.3),
    "big": ("mb-us2", 125, 25, 0.85),
    "narrator": ("mb-us1", 145, 45, 1.0),
    "lady": ("mb-us1", 150, 65, 1.05),
    "old": ("mb-us2", 110, 40, 0.95),
    "skel": ("mb-us2", 125, 32, 0.93),
    "skelred": ("mb-us3", 150, 62, 1.06),
    "skelgreen": ("mb-us3", 140, 72, 1.12),
}


# the rainbow of skeletons (red reuses skeleton_red): name, MBROLA voice, words/min, pitch, rate shift, text
RAINBOW = [
    ("orange", "mb-us3", 150, 58, 1.08, "Knock knock. Orange you glad I'm just bones?"),
    ("yellow", "mb-us2", 135, 60, 1.12, "I'm not yellow. I just haven't got the guts."),
    ("green", "mb-us3", 120, 40, 0.92, "Feeling a little green. Too many pine needles."),
    ("blue", "mb-us2", 105, 20, 0.86, "I've got the blues. Nobody to dance with."),
    ("indigo", "mb-us1", 145, 50, 1.0, "Indigo? Nobody even knows what colour that is."),
    ("violet", "mb-us1", 160, 78, 1.18, "Sorry. I'm a shrinking violet. I go to pieces."),
    # the second line-up (south wall)
    ("pink", "mb-us1", 165, 80, 1.15, "Stop it, I'm ticklish! Tickled pink, and I haven't even got skin."),
    ("teal", "mb-us2", 100, 30, 0.9, "Ooooh. I'm the teal skeleton. Teal death do us part."),
    ("gold", "mb-us2", 130, 22, 0.88, "Gold skeleton. Worth my weight in gold. I weigh nothing!"),
    ("silver", "mb-us3", 110, 35, 0.95, "Silver skeleton. I came second in the skeleton contest."),
    ("black", "mb-us2", 115, 15, 0.84, "Black skeleton. I'm the one hiding in your closet. Boo."),
    ("brown", "mb-us1", 140, 45, 0.98, "Brown skeleton. I'm not hungry. I've got no stomach for it."),
    ("glow", "mb-us3", 135, 70, 1.1, "I glow in the dark. Someone turn off the lights. Please?"),
    ("rainbow", "mb-us1", 175, 70, 1.12, "I'm every colour! I just couldn't decide."),
    ("tiny", "mb-us3", 160, 95, 1.42, "Hi! I'm just a little skele-tiny!"),
    ("giant", "mb-us2", 100, 5, 0.74, "I'm not fat. I'm big-boned."),
]


# combination acts: clip name, voices (several = a chorus, mixed), text. Voice presets match the skeletons.
SKV = {"red": ("mb-us3", 150, 62, 1.06), "orange": ("mb-us3", 150, 58, 1.08), "yellow": ("mb-us2", 135, 60, 1.12),
       "green": ("mb-us3", 120, 40, 0.92), "blue": ("mb-us2", 105, 20, 0.86), "indigo": ("mb-us1", 145, 50, 1.0),
       "violet": ("mb-us1", 160, 78, 1.18), "gold": ("mb-us2", 130, 22, 0.88), "silver": ("mb-us3", 110, 35, 0.95),
       "brown": ("mb-us1", 140, 45, 0.98), "tiny": ("mb-us3", 160, 95, 1.42), "giant": ("mb-us2", 100, 5, 0.74),
       "pink": ("mb-us1", 165, 80, 1.15), "teal": ("mb-us2", 100, 30, 0.9), "black": ("mb-us2", 115, 15, 0.84)}
COMBO = [
    ("traffic_green", ["green"], "Green means go!"),
    ("traffic_yellow", ["yellow"], "Yellow means slow down."),
    ("traffic_red", ["red"], "And red means... red skeleton. Ha!"),
    ("knock_1", ["orange"], "Knock knock."),
    ("knock_2", ["yellow"], "Who's there?"),
    ("knock_3", ["orange"], "Orange."),
    ("knock_4", ["yellow"], "Orange who?"),
    ("knock_5", ["orange"], "Orange you glad I didn't say banana?"),
    ("knock_6", ["yellow"], "That's not even a skeleton joke."),
    ("bonetones_1", ["blue", "indigo", "violet"], "Doo wop, doo wop, doo wop."),
    ("bonetones_2", ["indigo"], "We're the Bone Tones!"),
    ("bonetones_3", ["blue", "indigo", "violet"], "Shoo be doo wah. Oooooh!"),
    ("medal_gold", ["gold"], "First place!"),
    ("medal_silver", ["silver"], "Second. Again."),
    ("medal_brown", ["brown"], "Third! That's bronze, right? Close enough."),
    ("upsy_1", ["tiny"], "Pick me up! Pick me up!"),
    ("upsy_2", ["giant"], "Upsy daisy!"),
    ("upsy_3", ["tiny"], "Wheeeeee!"),
    ("upsy_4", ["giant"], "Oof. You're heavier than you look."),
    ("upsy_5", ["tiny"], "I'm big boned too!"),
    ("finale_1", ["red", "gold", "blue", "violet", "giant", "tiny", "pink", "black"], "We are the skeleton crew!"),
    ("finale_2", ["red", "gold", "blue", "violet", "giant", "tiny", "pink", "black"], "Thank you, good night!"),
]


# Bonetown (the expansion): every resident, band member and wanderer has its own voice and line.
# name, MBROLA voice, words/min, pitch, rate shift, text  ->  Audio/Town/<name>.ogg
TOWN = [
    # residents, at home
    ("res_mint", "mb-us3", 165, 78, 1.15, "I just moved in! I haven't even unpacked my spare ribs."),
    ("res_peach", "mb-us1", 140, 60, 1.05, "I'm practising for the town dance. My partner is a broom. He's very quiet."),
    ("res_lavender", "mb-us1", 95, 40, 0.92, "Five more minutes. I've only been asleep for two hundred years."),
    ("res_coral", "mb-us2", 140, 30, 0.9, "I work out every single day. Look at these bones. All bone. No muscle. Still proud."),
    ("res_plum", "mb-us2", 120, 45, 0.95, "I keep eating, but nothing ever stays down. Or in."),
    ("res_sky", "mb-us3", 170, 70, 1.12, "I'm writing a song! It's called Ribcage Rock. It's mostly xylophone."),
    ("res_navy", "mb-us2", 120, 20, 0.86, "Shh! I'm hiding from my landlord. I owe him a skull."),
    ("res_cream", "mb-us1", 150, 75, 1.1, "I tripped over the rug. Again. Could somebody... pick me up? All of me?"),
    ("res_rust", "mb-us3", 150, 50, 1.0, "Welcome to the snack shack! Today's special is soup. It goes right through you."),
    ("res_olive", "mb-us1", 135, 50, 0.98, "Breaking news! Local skeleton loses his temper. Also his arm. More at eleven."),
    # the band, each pressed on its own
    ("band_scarlet", "mb-us3", 150, 62, 1.06, "I play the trumpet. Well. I blow through my ribs."),
    ("band_mustard", "mb-us2", 135, 40, 0.95, "I'm on drums. And these are my own drumsticks. Literally."),
    ("band_emerald", "mb-us3", 125, 45, 0.94, "Guitar! I only know one chord. A spinal chord."),
    ("band_cobalt", "mb-us1", 145, 55, 1.0, "I'm the singer. Great voice, for a guy with no throat."),
    # the wanderers: they stop to chat
    ("wan_lime", "mb-us2", 110, 35, 0.93, "Out for my evening stroll. Want to come? I'm very slow. And very dead."),
    ("wan_magenta", "mb-us1", 185, 80, 1.18, "Can't stop! I'm late for a very important... nothing! Bye!"),
    ("wan_turquoise", "mb-us3", 170, 75, 1.15, "Tag! You're it! ...Oh. You're not playing. That's fine."),
    ("wan_crimson", "mb-us3", 175, 65, 1.1, "You can't catch me! I'm not even running from anything!"),
    ("wan_amber", "mb-us1", 130, 55, 1.0, "Lovely day for a walk. I left my legs on, just in case."),
    ("wan_ivory", "mb-us2", 125, 50, 0.97, "Hello, neighbour! Have you met everyone? Everyone's bones."),
    ("wan_cocoa", "mb-us2", 150, 30, 0.9, "I jog every morning. Lose weight? I haven't got any to lose!"),
    ("wan_denim", "mb-us1", 140, 45, 0.96, "Ooh, a visitor! We don't get many living ones."),
    ("wan_neon", "mb-us3", 180, 85, 1.2, "Wheee! Running is the best! I never get out of breath. No lungs!"),
    ("wan_bubblegum", "mb-us1", 160, 85, 1.16, "Shopping day! I'm buying a new skull. This one's a bit hollow."),
]
SKV.update({"scarlet": ("mb-us3", 150, 62, 1.06), "mustard": ("mb-us2", 135, 40, 0.95),
            "emerald": ("mb-us3", 125, 45, 0.94), "cobalt": ("mb-us1", 145, 55, 1.0)})
COMBO += [
    ("squabble_1", ["scarlet"], "You took my femur!"),
    ("squabble_2", ["cobalt"], "It's MY femur!"),
    ("squabble_3", ["scarlet"], "Then whose femur am I standing on?"),
    ("squabble_4", ["cobalt"], "Mine. Sorry."),
    ("squabble_5", ["scarlet", "cobalt"], "Bone buddies! Hug it out!"),
    ("band_1", ["scarlet", "mustard", "emerald", "cobalt"], "A one, a two, a one two three four!"),
    ("band_2", ["scarlet"], "Doot doot da doot!"),
    ("band_3", ["cobalt"], "We are the bones! The bones of Bonetown!"),
    ("band_4", ["scarlet", "mustard", "emerald", "cobalt"], "Thank you, Bonetown! Good night!"),
    ("propose_1", ["emerald"], "Mustard. Will you be my neighbour?"),
    ("propose_2", ["mustard"], "I'm already your neighbour."),
    ("propose_3", ["emerald"], "Then will you be my BEST neighbour?"),
    ("propose_4", ["mustard"], "Yes! Yes! A thousand times, yes!"),
    ("town_finale_1", ["scarlet", "mustard", "emerald", "cobalt"], "Welcome to Bonetown!"),
    ("town_finale_2", ["scarlet", "mustard", "emerald", "cobalt"], "Population: us!"),
]


def town_lines():
    for name, v, speed, pitch, rate, text in TOWN:
        yield ("Town/" + name, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled")


def make_combo(tmp):
    """Each combo line; a line with several voices is rendered once per voice and mixed, slightly staggered."""
    os.makedirs(os.path.join(OUT, "Combo"), exist_ok=True)
    for name, voices, text in COMBO:
        parts = []
        for k, who in enumerate(voices):
            v, speed, pitch, rate = SKV[who]
            wav = os.path.join(tmp, f"{name}_{k}.wav")
            subprocess.run(["espeak-ng", "-v", v, "-s", str(speed), "-p", str(pitch), "-w", wav, text], check=True)
            parts.append((wav, rate, k * 35))
        out = os.path.join(OUT, "Combo", name + ".ogg")
        args = ["ffmpeg", "-y", "-loglevel", "error"]
        for wav, _, _ in parts: args += ["-i", wav]
        filt = [f"[{i}:a]asetrate=16000*{rate},aresample=22050,adelay={d}|{d}[p{i}]" for i, (_, rate, d) in enumerate(parts)]
        mix = "".join(f"[p{i}]" for i in range(len(parts))) + f"amix=inputs={len(parts)}:normalize=0,volume={(1.8 if len(parts) == 1 else 0.9) * SKELETON_BOOST:.3f},aecho=0.8:0.5:35:0.15,alimiter=limit=0.95:level=disabled,apad=pad_dur=0.25[a]"
        args += ["-filter_complex", ";".join(filt + [mix]), "-map", "[a]", "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", out]
        subprocess.run(args, check=True)
        print("Combo/" + name, os.path.getsize(out) // 1024, "KB")


def rainbow_lines():
    for colour, v, speed, pitch, rate, text in RAINBOW:
        yield ("Rainbow/rb_" + colour, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled")


def gag_lines():
    for fname in ("gags.txt", "town_gags.txt"):
      path = os.path.join(os.path.dirname(OUT), "Data", fname)
      for line in open(path, encoding="utf-8"):
        if not line.strip() or line.startswith("#"):
            continue
        f = line.rstrip("\n").split("|")
        gid, voice, text = f[0], f[6], f[7]
        v, speed, pitch, rate = PRESETS[voice]
        yield ("Gags/gag_" + gid, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:30:0.12,volume=1.8")


def skeleton_lines():
    path = os.path.join(os.path.dirname(OUT), "Data", "skeleton_gags.txt")
    for line in open(path, encoding="utf-8"):
        if not line.strip() or line.startswith("#"):
            continue
        f = line.rstrip("\n").split("|")
        v, speed, pitch, rate = PRESETS[f[7]]
        yield ("Gags/skel_" + f[0], v, speed, pitch, f[9],
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled")


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(os.path.join(OUT, "Gags"), exist_ok=True)
    os.makedirs(os.path.join(OUT, "Rainbow"), exist_ok=True)
    only_rainbow = "--rainbow" in sys.argv
    os.makedirs(os.path.join(OUT, "Town"), exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        if "--combo" in sys.argv:
            make_combo(tmp)
            return
        if "--town" in sys.argv:   # only the expansion: its voices, its gags (ids starting B), and the combo lines
            todo = list(town_lines()) + [g for g in gag_lines() if g[0].startswith("Gags/gag_B")]
            for name, voice, speed, pitch, text, filt in todo:
                wav = os.path.join(tmp, name.replace("/", "_") + ".wav")
                subprocess.run(["espeak-ng", "-v", voice, "-s", str(speed), "-p", str(pitch), "-w", wav, text], check=True)
                out = os.path.join(OUT, name + ".ogg")
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-af", filt + ",apad=pad_dur=0.25",
                                "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", out], check=True)
                print(name, os.path.getsize(out) // 1024, "KB")
            make_combo(tmp)
            return
        todo = list(rainbow_lines()) if only_rainbow else list(LINES) + list(gag_lines()) + list(skeleton_lines()) + list(rainbow_lines()) + list(town_lines())
        for name, voice, speed, pitch, text, filt in todo:
            wav = os.path.join(tmp, name.replace("/", "_") + ".wav")
            subprocess.run(["espeak-ng", "-v", voice, "-s", str(speed), "-p", str(pitch), "-w", wav, text], check=True)
            out = os.path.join(OUT, name + ".ogg")
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-af", filt + ",apad=pad_dur=0.25",
                            "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", out], check=True)
            print(name, os.path.getsize(out) // 1024, "KB")
        if not only_rainbow:
            make_combo(tmp)


if __name__ == "__main__":
    main()
