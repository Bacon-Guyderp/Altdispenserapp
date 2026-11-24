"""Cuts the Chris and Tripp Show (Episode 1) dialogue out of the episode's own audio, one clip per line
(Assets/FirHorror/Audio/Combo/ct_NN.ogg), for the room that recreates the episode.
Usage: python make_chris_tripp.py path/to/The_Chris_and_Tripp_show_Episode_1.mp4
The lines and their times were transcribed from the episode (see LINES in make_voices.py for the captions)."""
import os, subprocess, sys

# (clip, start s, end s, who: "S" = the one in the skeleton shirt, "B" = the blond one, the exact line)
LINES = [
    ("ct_01", 10.50, 11.62, "B", "I want a cat."),
    ("ct_02", 12.40, 14.20, "B", "The house is on fire."),
    ("ct_03", 14.30, 15.35, "S", "Oh my gosh."),
    ("ct_04", 15.60, 17.55, "S", "Smoke so dee dee em dee."),
    ("ct_05", 17.55, 18.55, "S", "Foss gains."),
    ("ct_06", 18.70, 19.38, "S", "F."),
    ("ct_07", 19.38, 20.75, "S", "F R E T G."),
    ("ct_08", 22.60, 24.00, "S", "What happened here?"),
    ("ct_09", 28.30, 29.85, "B", "Someone got murdered."),
    ("ct_10", 34.65, 38.42, "S", "You do it for him and you would do it again you do it for her."),
    ("ct_11", 38.42, 40.42, "S", "That is to say you'll do it for him."),
    ("ct_12", 40.64, 41.90, "S", "Keep your stance wide."),
    ("ct_13", 41.95, 43.55, "S", "Keep your body lowered."),
    ("ct_14", 43.56, 47.36, "S", "As you're moving forward balance is the key right foot left foot."),
    ("ct_15", 47.36, 52.00, "S", "But, now go even faster and as you're moving backwards keep your eyes on me."),
    ("ct_16", 52.02, 53.00, "B", "You are weird."),
    ("ct_17", 55.25, 56.05, "S", "Diamonds."),
    ("ct_18", 57.36, 58.15, "B", "Diamonds."),
    ("ct_19", 59.56, 60.40, "B", "Diamonds."),
    ("ct_20", 69.40, 71.10, "S", "That was a weird show."),
    ("ct_21", 71.88, 72.90, "B", "I agree."),
]

if __name__ == "__main__":
    src = sys.argv[1]
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "FirHorror", "Audio", "Combo")
    for name, a, b, who, text in LINES:
        dst = os.path.join(out, name + ".ogg")
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-ss", f"{a:.2f}", "-to", f"{b:.2f}", "-i", src, "-vn", "-ac", "1", "-ar", "22050",
                        "-af", "afade=t=in:d=0.03,areverse,afade=t=in:d=0.05,areverse,apad=pad_dur=0.2", "-c:a", "libvorbis", "-q:a", "5", dst], check=True)
        print(name, who, text)
