#!/usr/bin/env python3
"""Converts the uploaded baby model (stewie_griffin.obj + its PNGs) into Models/stewie_parts.txt and
tools/textures/stewie_*.png, the same way the skeleton model is stored, so Unity never has to import the OBJ.

  python make_stewie.py <folder with stewie_griffin.obj>

Output format: 'sub <texture>' starts a part; 'v x y z u v' a vertex; 't a b c' a triangle (indices within the part).
OBJ is right-handed and Unity left-handed, so x is mirrored and triangle winding reversed (as Unity's importer does).
The material that is fully transparent apart from its alpha cut-outs (eyelashes and hair tufts) is left out:
the world's shader is opaque.
"""
import os, shutil, sys
from PIL import Image

src = sys.argv[1]
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets", "FirHorror", "Models", "stewie_parts.txt")
TEX = os.path.join(HERE, "textures")

# OBJ material -> texture name in the package
NAMES = {"Tex_0125_1_dds": "stewie_body", "Tex_0047_1_dds": "stewie_head", "Tex_0130_1_dds": "stewie_trim", "Tex_0129_1_dds": "stewie_shoes"}
SKIP = {"Tex_0161_1_dds"}

v, vt, faces, cur = [], [], {}, None
for line in open(os.path.join(src, "stewie_griffin.obj"), encoding="utf-8", errors="replace"):
    p = line.split()
    if not p: continue
    if p[0] == "v": v.append(tuple(float(x) for x in p[1:4]))
    elif p[0] == "vt": vt.append(tuple(float(x) for x in p[1:3]))
    elif p[0] == "usemtl": cur = p[1]
    elif p[0] == "f":
        idx = []
        for c in p[1:]:
            a = c.split("/")
            idx.append((int(a[0]) - 1, int(a[1]) - 1 if len(a) > 1 and a[1] else -1))
        for k in range(1, len(idx) - 1):   # fan-triangulate
            faces.setdefault(cur, []).append((idx[0], idx[k], idx[k + 1]))

lines = ["# the uploaded baby model, converted by tools/make_stewie.py (metres, Unity axes)"]
for mat, tris in faces.items():
    if mat in SKIP or mat not in NAMES: continue
    remap, verts, out = {}, [], []
    for tri in tris:
        ids = []
        for vi, ti in tri:
            key = (vi, ti)
            if key not in remap:
                remap[key] = len(verts)
                x, y, z = v[vi]
                u, w = vt[ti] if ti >= 0 else (0.0, 0.0)
                verts.append((-x, y, z, u, w))
            ids.append(remap[key])
        out.append((ids[0], ids[2], ids[1]))   # mirrored, so reverse the winding
    lines.append("sub " + NAMES[mat])
    lines += ["v %.5f %.5f %.5f %.5f %.5f" % q for q in verts]
    lines += ["t %d %d %d" % t for t in out]
    print(NAMES[mat], len(verts), "vertices", len(out), "triangles")
open(OUT, "w").write("\n".join(lines) + "\n")

# textures (their alpha isn't used)
mtl = {}
for line in open(os.path.join(src, "stewie_griffin.mtl"), encoding="utf-8", errors="replace"):
    p = line.split()
    if p and p[0] == "newmtl": cur = p[1]
    elif p and p[0] == "map_Kd": mtl[cur] = p[1]
for mat, name in NAMES.items():
    Image.open(os.path.join(src, os.path.basename(mtl[mat]))).convert("RGB").save(os.path.join(TEX, name + ".png"))
print("wrote", OUT)
