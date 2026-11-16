"""Cuts the skeleton OBJ model into the rigid parts the closet skeletons animate with.

  python make_skeleton_parts.py path/to/Skeleton/skeleton.obj

The model is a static T-pose (about 12.9 units tall, facing -Z, Blender OBJ). This script:
  * converts it to Unity's left-handed axes, scales it to 1.74 m and turns it to face +Z,
  * splits it at the joints into 10 parts: pelvis (root), torso, skull, jaw, upper arms,
    forearms (with hands) and legs,
  * swings the arms down from the T-pose to hang at the sides, so the closet animation's
    shoulder and elbow angles work unchanged,
  * writes every part relative to its own pivot to Assets/FirHorror/Models/skeleton_parts.txt
    and copies the colour texture to Assets/FirHorror/Textures/skeleton.png.

FirWorldBuilder.SkeletonRig reads the text file and builds one Mesh per part.

Format:  part <name> <parent> <px> <py> <pz>   pivot position relative to the parent's pivot
         v <x> <y> <z> <u> <v>                 vertex relative to the part's pivot
         t <a> <b> <c>                         triangle (Unity winding)
"""
import math, os, shutil, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
HEIGHT = 1.74

# joint layout in the model's own units (measured from the T-pose)
ELBOW_X = 3.23      # |x| of the elbows
SHOULDER_X = 1.30   # |x| where the arm leaves the shoulder
ARM_MIN_Y = 9.6     # arm vertices sit above this
JAW_Y = 11.6        # the gap between the teeth
HIP = (0.8, 6.5)    # |x|, y of the hip joints
HANG = 82.0         # degrees the arms swing down from the T-pose (90 would press them into the ribs)


def load(path):
    V, VT, faces = [], [], []
    obj = None
    for line in open(path, encoding="utf-8", errors="replace"):
        p = line.split()
        if not p:
            continue
        if p[0] == "o":
            obj = p[1]
        elif p[0] == "v":
            V.append([float(x) for x in p[1:4]])
        elif p[0] == "vt":
            VT.append([float(x) for x in p[1:3]])
        elif p[0] == "f":
            idx = []
            for c in p[1:]:
                s = c.split("/")
                idx.append((int(s[0]) - 1, int(s[1]) - 1 if len(s) > 1 and s[1] else -1))
            for k in range(1, len(idx) - 1):          # fan-triangulate any polygons
                faces.append((obj, idx[0], idx[k], idx[k + 1]))
    return np.array(V), np.array(VT) if VT else np.zeros((1, 2)), faces


def classify(obj, v):
    """Which part a vertex belongs to, in model units (original axes)."""
    x, y, z = v
    if obj.lower().startswith("skull"):
        return "jaw" if y < JAW_Y else "skull"
    if y > ARM_MIN_Y and abs(x) > SHOULDER_X:
        side = "L" if x < 0 else "R"
        return ("fore" if abs(x) > ELBOW_X else "upper") + side
    if y < 6.0 or (y < 6.8 and abs(x) > 0.68):
        return "leg" + ("L" if x < 0 else "R")
    if y > 8.0 or (y > 7.55 and abs(x) < 0.45):
        return "torso"
    return "pelvis"


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    src = sys.argv[1]
    V, VT, faces = load(src)
    floor = V[:, 1].min()
    scale = HEIGHT / (V[:, 1].max() - floor)

    def to_unity(v):            # model units -> metres, Unity axes, facing +Z
        return np.array([v[0] * scale, (v[1] - floor) * scale, -v[2] * scale])

    # each triangle goes to the part that most of its corners belong to
    part_of_tri = []
    for obj, a, b, c in faces:
        votes = [classify(obj, V[i]) for i, _ in (a, b, c)]
        part_of_tri.append(max(set(votes), key=votes.count))

    def centre(part, near=None):
        pts = np.array([to_unity(V[i]) for (obj, a, b, c), p in zip(faces, part_of_tri) if p == part for i, _ in (a, b, c)])
        return pts.mean(0) if near is None else pts[np.argsort(np.linalg.norm(pts - near, axis=1))[:40]].mean(0)

    # pivots in root space (metres, Unity axes)
    torso_y = 7.6
    piv = {"pelvis": np.zeros(3)}
    spine = centre("torso", to_unity([0, torso_y, 0]))
    piv["torso"] = np.array([0, (torso_y - floor) * scale, spine[2]])
    neck = centre("skull", to_unity([0, 11.7, -0.15]))
    piv["skull"] = np.array([0, (11.7 - floor) * scale, neck[2]])
    piv["jaw"] = np.array([0, (11.75 - floor) * scale, -(-0.6) * scale])        # just in front of the ear (TMJ)
    for side, sx in (("L", -1), ("R", 1)):
        arm_z = centre("upper" + side)[2]
        piv["upper" + side] = np.array([sx * SHOULDER_X * scale, (10.42 - floor) * scale, arm_z])
        piv["leg" + side] = np.array([sx * HIP[0] * scale, (HIP[1] - floor) * scale, centre("leg" + side, to_unity([sx * HIP[0], HIP[1], 0]))[2]])

    # swing the arms down: rotate about Z around the shoulder (left +HANG, right -HANG)
    def rot_z(p, deg):
        r = math.radians(deg)
        return np.array([p[0] * math.cos(r) - p[1] * math.sin(r), p[0] * math.sin(r) + p[1] * math.cos(r), p[2]])

    def arm_pose(side, p):      # root-space point on an arm -> its hanging position
        sh = piv["upper" + side]
        return sh + rot_z(p - sh, HANG if side == "L" else -HANG)

    for side, sx in (("L", -1), ("R", 1)):
        elbow_tpose = np.array([sx * ELBOW_X * scale, (10.42 - floor) * scale, piv["upper" + side][2]])
        piv["fore" + side] = arm_pose(side, elbow_tpose)

    parent = {"pelvis": None, "torso": "pelvis", "skull": "torso", "jaw": "skull", "upperL": "torso", "upperR": "torso",
              "foreL": "upperL", "foreR": "upperR", "legL": "pelvis", "legR": "pelvis"}
    order = ["pelvis", "torso", "skull", "jaw", "upperL", "upperR", "foreL", "foreR", "legL", "legR"]

    out_lines = ["# Skeleton parts for Fir Warning (made by tools/make_skeleton_parts.py). Units: metres, Unity axes, facing +Z."]
    total = 0
    for name in order:
        verts, index, tris = [], {}, []
        for (obj, a, b, c), p in zip(faces, part_of_tri):
            if p != name:
                continue
            ids = []
            for vi, ti in (a, c, b):            # mirrored axes, so reverse the winding
                key = (vi, ti)
                if key not in index:
                    pos = to_unity(V[vi])
                    if name.startswith(("upper", "fore")):
                        pos = arm_pose(name[-1], pos)
                    uv = VT[ti] if ti >= 0 else (0.0, 0.0)
                    index[key] = len(verts)
                    verts.append((pos - piv[name], uv))
                ids.append(index[key])
            tris.append(ids)
        rel = piv[name] - (piv[parent[name]] if parent[name] else 0)
        out_lines.append(f"part {name} {parent[name] or '-'} {rel[0]:.5f} {rel[1]:.5f} {rel[2]:.5f}")
        out_lines += [f"v {p[0]:.5f} {p[1]:.5f} {p[2]:.5f} {uv[0]:.5f} {uv[1]:.5f}" for p, uv in verts]
        out_lines += [f"t {a} {b} {c}" for a, b, c in tris]
        total += len(tris)
        print(f"{name:7s} {len(verts):5d} verts {len(tris):5d} tris  pivot {np.round(piv[name], 3)}")

    models = os.path.join(PROJECT, "Assets", "FirHorror", "Models")
    os.makedirs(models, exist_ok=True)
    with open(os.path.join(models, "skeleton_parts.txt"), "w") as f:
        f.write("\n".join(out_lines) + "\n")
    tex = os.path.join(os.path.dirname(src), "skeleton.png")
    if os.path.exists(tex):
        shutil.copyfile(tex, os.path.join(PROJECT, "Assets", "FirHorror", "Textures", "skeleton.png"))
    print(f"{total} triangles written")


if __name__ == "__main__":
    main()
