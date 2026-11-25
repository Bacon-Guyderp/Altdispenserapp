"""Writes Assets/FirHorror/Data/wings_more.txt from the batch files (a.py, b.py, c.py, d.py): three more wings for every room."""
import importlib.util, os
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "FirHorror", "Data", "wings_more.txt")
HEX = ["#ffcc80", "#90caf9", "#a5d6a7", "#f48fb1", "#ce93d8", "#fff59d", "#80cbc4", "#bcaaa4", "#ffab91", "#b0bec5", "#e6ee9c", "#81d4fa"]
ACTS = [3, 9, 16, 7, 20, 11, 18, 4, 21, 10, 19, 22]
SIZE = {"cube": "0.8,0.8,0.8", "sph": "0.7,0.7,0.7", "cyl": "0.6,0.5,0.6", "cap": "0.5,1,0.5"}
AMOUNT = {"pop": "1.5", "drop": "2.5", "slide": "3", "fly": "4"}
def load(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(HERE, name + ".py"))
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m); return m
def main():
    rows, n, k = ["# three more wings for every room (written by tools/wings4/gen.py)"], 0, 0
    titles = set()
    for b in ("a", "b", "c", "d"):
        if not os.path.exists(os.path.join(HERE, b + ".py")): continue
        m = load(b)
        for area, (floor, wall, ceil, h), wings in m.AREAS:
            assert len(wings) == 3, area
            for title, sub, style, accent, folk, gags in wings:
                assert title not in titles, title; titles.add(title)
                assert len(folk) == 3 and len(gags) == 3, title
                rows.append("|".join(["W", area, title, sub, floor, wall, ceil, str(h), style, accent]))
                for name, voice, line in folk:
                    rows.append("|".join(["F", name, HEX[k % len(HEX)], str(ACTS[k % len(ACTS)]), voice, line])); k += 1
                for name, kind, shape, mat, voice, line in gags:
                    lift = "0" if kind in ("pop", "drop", "slide", "fly") else "1"
                    rows.append("|".join(["G", name, kind, shape, SIZE[shape], mat, AMOUNT.get(kind, "2"), lift, voice, line]))
                n += 1
    open(OUT, "w", encoding="utf-8").write("\n".join(rows) + "\n")
    print(n, "wings ->", OUT)
if __name__ == "__main__":
    main()
