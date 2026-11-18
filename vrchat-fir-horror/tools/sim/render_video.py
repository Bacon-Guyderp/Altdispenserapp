#!/usr/bin/env python3
"""Renders a simulated Fir Warning playthrough (a trace from sim_test.cs) as a top-down MP4.

    FIR_TRACE=trace.jsonl mono sim.exe        # see run_video.sh
    python3 render_video.py trace.jsonl out.mp4

Everything shown comes from the real game scripts running in the simulator: positions, the hunting
tree, doors, items, hats, the furnace, each player's subtitles, scares and teleports. Players walk on
scripted routes (the test is the one "playing"); the world reacts on its own.
"""
import json, math, re, subprocess, sys
from PIL import Image, ImageDraw, ImageFont

W, H, FPS, EVERY = 1280, 720, 30, 2          # draw every 2nd simulation step (20/s) at 30 fps = 3x speed
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
f11, f13, f15, f18, f24 = (ImageFont.truetype(FONT, s) for s in (11, 13, 15, 18, 24))
b13, b15, b20, b40 = (ImageFont.truetype(BOLD, s) for s in (13, 15, 20, 40))

BG, PANEL, LINE, TEXT, DIM = (12, 12, 16), (24, 24, 30), (70, 70, 82), (230, 230, 235), (140, 140, 150)
PCOL = {"Alice": (90, 170, 255), "Bob": (255, 160, 60), "Carla": (240, 90, 220)}
ROOMS = [("Showroom", 0, 20, 16, (90, 30, 30)), ("Stockroom", 20, 56, 24, (35, 70, 45)), ("Voice Lab", 56, 80, 16, (65, 35, 90)),
         ("Offices", 80, 104, 20, (35, 55, 95)), ("Living Room", 104, 120, 14, (95, 70, 30)), ("Recall Floor", 120, 150, 22, (110, 55, 25)),
         ("Exit", 150, 160, 10, (70, 70, 70))]
ITEMCOL = {1: (255, 220, 60), 2: (110, 220, 255), 3: (255, 110, 200), 9: (255, 70, 60)}
HATCOL = [(220, 30, 40), (40, 180, 70), (60, 110, 240)]

# map placement: the factory strip (z along x) and the lobby panel
FX0, FX1, FY = 20, 1260, 345
FS = (FX1 - FX0) / 160.0                      # px per metre
LX0, LY0, LS = 22, 60, 14                   # lobby panel: x -67..-53, z -6..6


def fpt(x, z):  # factory metres -> pixels (z right, +x up)
    return FX0 + z * FS, FY - x * FS


def lpt(x, z):  # lobby metres -> pixels (north wall at the top)
    return LX0 + (x + 67) * LS, LY0 + (6 - z) * LS


def pt(x, z):
    return lpt(x, z) if x < -30 else fpt(x, z)


def strip(s):
    return re.sub(r"<[^>]+>", "", s or "").replace("\n", " ").strip()


def wrap(text, font, width, draw):
    words, lines, cur = text.split(), [], ""
    for w in words:
        t = (cur + " " + w).strip()
        if draw.textlength(t, font=font) <= width: cur = t
        else:
            if cur: lines.append(cur)
            cur = w
    if cur: lines.append(cur)
    return lines


def tree(d, x, y, s, fill, eyes=None):
    d.polygon([(x - s * 0.6, y + s * 0.5), (x + s * 0.6, y + s * 0.5), (x, y - s * 0.7)], fill=fill, outline=(10, 30, 15))
    if eyes:
        d.ellipse((x - s * 0.25 - 2, y - 2, x - s * 0.25 + 2, y + 2), fill=eyes)
        d.ellipse((x + s * 0.25 - 2, y - 2, x + s * 0.25 + 2, y + 2), fill=eyes)


def main(trace_path, out_path):
    frames = [json.loads(l) for l in open(trace_path)]
    ff = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
                           "-i", "-", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "22", "-preset", "medium", "-movflags", "+faststart", out_path],
                          stdin=subprocess.PIPE)
    log, rings, banner_until, banner = [], [], -1, ""
    last_pos, trails = {}, {}
    for i, fr in enumerate(frames):
        t = fr["t"]
        for n in fr["notes"]:
            log.append(n)
            if n.startswith("== "): banner, banner_until = n[3:], t + 4.0
        for e in fr["tp"]:
            m = re.match(r"(\w+) teleport: (\w+) -> \(([-\d.]+), ([-\d.]+), ([-\d.]+)\)", e)
            if m:
                who, x, z = m.group(2), float(m.group(3)), float(m.group(5))
                rings.append((x, z, PCOL.get(who, TEXT), t, last_pos.get(who)))
        for p in fr["players"]:
            name, x, z = p[0], p[1], p[2]
            tr = trails.setdefault(name, [])
            if not tr or (abs(tr[-1][0] - x) + abs(tr[-1][1] - z) > 0.25):
                if tr and (abs(tr[-1][0] - x) + abs(tr[-1][1] - z) > 4): tr.clear()   # teleported: start a new trail
                tr.append((x, z))
                del tr[:-40]
            last_pos[name] = (x, z)
        if i % EVERY: continue

        img = Image.new("RGB", (W, H), BG)
        d = ImageDraw.Draw(img)

        # ---- header
        d.rectangle((0, 0, W, 44), fill=PANEL)
        d.text((16, 9), "FIR WARNING", font=b20, fill=(220, 40, 40))
        d.text((180, 13), "simulated playthrough: the real game scripts, three simulated players, 0.15 s network delay", font=f13, fill=DIM)
        mm, ss = divmod(int(t), 60)
        d.text((W - 16, 12), f"{mm}:{ss:02d}  (shown at 3x)", font=f15, fill=TEXT, anchor="ra")

        # ---- top band: lobby panel, status
        d.rectangle((LX0 - 4, LY0 - 4, LX0 + 14 * LS + 4, LY0 + 12 * LS + 4), outline=LINE, fill=(40, 22, 22))
        d.text((LX0 + 14 * LS + 8, LY0), "LOBBY", font=b13, fill=DIM)
        for (x, z, lab, col) in [(-62.2, 5.6, "START", (40, 160, 70)), (-60, 5.6, "JOIN", (70, 90, 130)), (-57.8, 5.6, "RESET", (160, 70, 40))]:
            px, py = lpt(x, z); d.rectangle((px - 15, py - 5, px + 15, py + 5), fill=col); d.text((px, py), lab, font=f11, fill=TEXT, anchor="mm")
        for k in range(7):
            px, py = lpt(-65.95 if k % 2 == 0 else -64.4, 5.6 - (k // 2) * 0.75)
            d.rectangle((px - 8, py - 3, px + 8, py + 3), fill=(70, 90, 130))
        px, py = lpt(-65.2, 2.4); d.text((px, py), "room select", font=f11, fill=DIM, anchor="mm")
        px, py = lpt(-60, -5.2); d.text((px, py), "☠", font=f15, fill=(230, 60, 50), anchor="mm")

        sx = 280
        phase = fr["phase"]
        chap = {0: "lobby: nobody playing", 7: "ESCAPED"}.get(phase, f"chapter {phase}: {ROOMS[phase - 1][0]}" if 1 <= phase <= 6 else "")
        d.text((sx, 58), "GAME STATE (synced)", font=b13, fill=DIM)
        d.text((sx, 76), chap, font=b20, fill=TEXT)
        d.text((sx, 104), "Lobby board: " + strip(fr["lobby"])[:95], font=f13, fill=DIM)
        obj = strip(fr["obj"])
        d.text((sx, 124), ("Objective: " + obj) if obj else "", font=f15, fill=(255, 215, 130))
        if fr["dark"] > 0.5:
            d.text((sx, 150), "LIGHTS OUT", font=b20, fill=(120, 255, 140))
        gag = fr["gag"]
        if gag:
            d.text((sx + 160, 150), ["", "Comedy gag playing for everyone", "Skeleton routine playing for everyone",
                                     "RED SKELETON closet scene playing for everyone"][gag], font=b15, fill=(255, 120, 120))
        # section banner
        if t < banner_until:
            d.text((sx, 182), banner, font=b20, fill=(255, 255, 255))
        else:
            d.text((sx, 186), banner, font=f15, fill=DIM)

        # ---- factory map
        dark = fr["dark"] > 0.5
        for k, (name, z0, z1, w, col) in enumerate(ROOMS):
            x0, y0 = fpt(w / 2, z0); x1, y1 = fpt(-w / 2, z1)
            c = tuple(int(v * (0.35 if dark else 1)) for v in col)
            d.rectangle((x0, y0, x1, y1), fill=c, outline=LINE)
            d.text(((x0 + x1) / 2, FY - 12 * FS - 6), name, font=f13, fill=DIM, anchor="ms")
        for k, closed in enumerate(fr["doors"]):
            z = ROOMS[k][2]
            (x0, y0), (x1, y1) = fpt(1.2, z), fpt(-1.2, z)
            d.line((x0, y0, x1, y1), fill=(230, 50, 50) if closed else (80, 230, 110), width=4 if closed else 2)
        for k in range(6):   # back-to-lobby teleporters
            px, py = fpt(2.1, ROOMS[k][1] + 0.2); d.rectangle((px - 2, py - 4, px + 4, py + 4), fill=(200, 90, 50))
        px, py = fpt(-3, 159.6); d.rectangle((px - 2, py - 4, px + 4, py + 4), fill=(200, 90, 50))
        for k in range(4):
            px, py = fpt(-5.6, 6 + 2 * k); tree(d, px, py, 9, (40, 120, 60))
        # hat trees and their hats
        for s in range(3):
            px, py = fpt(-4 + 4 * s, 116); tree(d, px, py, 12, (40, 120, 60))
            for c in range(3):
                if fr["hatsOn"][s * 3 + c]: d.ellipse((px - 4, py - 13, px + 4, py - 5), fill=HATCOL[c])
        px, py = fpt(6, 110); d.rectangle((px - 5, py - 7, px + 5, py + 7), fill=(120, 80, 40), outline=(200, 40, 40))
        px, py = fpt(-7.5, 145.4)
        d.rectangle((px - 10, py - 7, px + 10, py + 7), fill=(255, 140, 30) if fr["fire"] else (60, 50, 45), outline=(150, 150, 150))
        d.text((px, py + 10), "furnace", font=f11, fill=DIM, anchor="ma")
        for (x, z, ch) in fr["items"]:
            px, py = fpt(x, z); col = ITEMCOL.get(ch, TEXT)
            d.polygon([(px, py - 4), (px + 4, py), (px, py + 4), (px - 4, py)], fill=col)
        for (x, z, kind, size) in fr["carry"]:
            px, py = pt(x, z)
            if kind == 1: tree(d, px, py, 7, (90, 160, 60))
            else: d.ellipse((px - 4, py - 4, px + 4, py + 4), fill=HATCOL[size], outline=(255, 255, 255))
        sx_, sz_, sact, sstun = fr["stalker"]
        if sact:
            px, py = fpt(sx_, sz_)
            tree(d, px, py, 20, (20, 70, 30), eyes=(255, 40, 30))
            d.text((px, py + 12), "STUNNED" if sstun else "hunting tree", font=b13 if sstun else f11, fill=(255, 230, 90) if sstun else (255, 120, 110), anchor="ma")

        # teleport rings
        for (x, z, col, t0, frm) in list(rings):
            age = t - t0
            if age > 0.9: rings.remove((x, z, col, t0, frm)); continue
            px, py = pt(x, z); r = 6 + 40 * age
            d.ellipse((px - r, py - r, px + r, py + r), outline=col, width=3)
            if age < 0.5: d.text((px, py - r - 4), "teleport", font=b13, fill=col, anchor="ms")

        # players
        for p in fr["players"]:
            name, x, z, yaw, scare, behind, sub = p
            col = PCOL.get(name, TEXT)
            tr = trails.get(name, [])
            if len(tr) > 1 and (x < -30) == (tr[0][0] < -30):
                d.line([pt(a, b) for a, b in tr], fill=tuple(int(v * 0.5) for v in col), width=2)
            px, py = pt(x, z)
            a = math.radians(yaw)
            if x < -30: dx, dy = math.sin(a), -math.cos(a)
            else: dx, dy = math.cos(a), -math.sin(a)
            d.line((px, py, px + dx * 16, py + dy * 16), fill=col, width=2)
            d.ellipse((px - 6, py - 6, px + 6, py + 6), fill=col, outline=(255, 255, 255))
            d.text((px + 8, py - 18), name, font=b13, fill=col)
            if scare in (1, 2):
                for k in range(10):
                    aa = k * math.pi / 5
                    d.line((px + math.cos(aa) * 9, py + math.sin(aa) * 9, px + math.cos(aa) * 20, py + math.sin(aa) * 20), fill=(255, 40, 40), width=2)
                d.text((px, py + 22), "JUMPSCARE", font=b13, fill=(255, 60, 60), anchor="ma")
            elif behind:
                d.text((px, py + 10), "something behind...", font=f11, fill=(200, 140, 255), anchor="ma")

        # ---- bottom: what each player sees, and the checks
        d.rectangle((0, 440, W, H), fill=PANEL)
        d.text((20, 448), "WHAT EACH PLAYER'S HUD SAYS", font=b13, fill=DIM)
        y = 470
        for p in fr["players"]:
            name, x, z, yaw, scare, behind, sub = p
            col = PCOL.get(name, TEXT)
            d.rectangle((20, y, 640, y + 76), outline=col, fill=(18, 18, 22))
            where = "lobby" if x < -30 else next((r[0] for r in ROOMS if r[1] <= z < r[2]), "?")
            state = "  -  JUMPSCARE!" if scare in (1, 2) else ""
            d.text((30, y + 6), f"{name}  -  {where}{state}", font=b15, fill=col)
            lines = wrap(strip(sub), f13, 595, d)[:2]
            for k, ln in enumerate(lines): d.text((30, y + 30 + k * 18), ln, font=f13, fill=TEXT)
            y += 82
        d.text((660, 448), "CHECKS (live)", font=b13, fill=DIM)
        shown = log[-13:]
        for k, n in enumerate(shown):
            yy = 470 + k * 18
            if n.startswith("== "): d.text((660, yy), n[3:][:70], font=b13, fill=(255, 255, 255))
            else:
                ok = n.startswith("PASS")
                d.text((660, yy), ("✓ " if ok else "✗ ") + n[5:][:78], font=f13, fill=(110, 230, 130) if ok else (255, 90, 90))
        npass = sum(1 for n in log if n.startswith("PASS")); nfail = sum(1 for n in log if n.startswith("FAIL"))
        d.text((W - 16, 448), f"{npass} passed, {nfail} failed", font=b13, fill=(110, 230, 130) if nfail == 0 else (255, 90, 90), anchor="ra")

        ff.stdin.write(img.tobytes())
    # end card
    img = Image.new("RGB", (W, H), BG); d = ImageDraw.Draw(img)
    npass = sum(1 for n in log if n.startswith("PASS")); nfail = sum(1 for n in log if n.startswith("FAIL"))
    d.text((W / 2, 280), "FIR WARNING", font=b40, fill=(220, 40, 40), anchor="mm")
    d.text((W / 2, 340), f"simulated playthrough: {npass} checks passed, {nfail} failed", font=f24, fill=TEXT, anchor="mm")
    d.text((W / 2, 380), "every teleporter, START/Join, three players, all six chapters, reset, replay, the master leaving", font=f15, fill=DIM, anchor="mm")
    for _ in range(FPS * 4): ff.stdin.write(img.tobytes())
    ff.stdin.close(); ff.wait()


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
