"""Minimal binary FBX 7.4 writer (static textured meshes).

Written against the layout produced by Autodesk's SDK / Blender's exporter:
a header, a tree of node records, a null record and the standard footer.
"""
from __future__ import annotations

import os
import struct
import zlib

import numpy as np

from ..model import Mesh

_HEAD = b"Kaydara FBX Binary  \x00\x1a\x00"
_SENTINEL = b"\x00" * 13
_FILE_ID = b"\x28\xb3\x2a\xeb\xb6\x24\xcc\xc2\xbf\xc8\xb0\x2a\xa9\x2b\xfc\xf1"
_FOOT_ID = b"\xfa\xbc\xab\x09\xd0\xc8\xd4\x66\xb1\x76\xfb\x83\x1c\xf7\x26\x7e"
_FOOT_MAGIC = b"\xf8\x5a\x8c\x6a\xde\xf5\xd9\x7e\xec\xe9\x0c\xe3\x75\x8f\x29\x0b"
VERSION = 7400


class Node:
    def __init__(self, name: str, *props, children=None):
        self.name = name.encode()
        self.props = list(props)
        self.children: list[Node] = list(children or [])

    def add(self, name: str, *props) -> "Node":
        n = Node(name, *props)
        self.children.append(n)
        return n

    # ---------------------------------------------------------------
    @staticmethod
    def _prop(p) -> bytes:
        if isinstance(p, tuple):  # explicit (typecode, value)
            code, val = p
            if code in "ilfdb":
                arr = np.asarray(val, dtype={"i": "<i4", "l": "<i8", "f": "<f4", "d": "<f8", "b": "u1"}[code])
                raw = arr.tobytes()
                if len(raw) > 128:
                    comp = zlib.compress(raw)
                    return code.encode() + struct.pack("<III", len(arr), 1, len(comp)) + comp
                return code.encode() + struct.pack("<III", len(arr), 0, len(raw)) + raw
            if code == "L":
                return b"L" + struct.pack("<q", val)
            if code == "I":
                return b"I" + struct.pack("<i", val)
            if code == "D":
                return b"D" + struct.pack("<d", val)
            if code == "F":
                return b"F" + struct.pack("<f", val)
            if code == "Y":
                return b"Y" + struct.pack("<h", val)
            if code == "C":
                return b"C" + struct.pack("<B", 1 if val else 0)
            if code == "R":
                return b"R" + struct.pack("<I", len(val)) + bytes(val)
            if code == "S":
                b = val if isinstance(val, bytes) else val.encode()
                return b"S" + struct.pack("<I", len(b)) + b
            raise ValueError(code)
        if isinstance(p, bool):
            return b"C" + struct.pack("<B", 1 if p else 0)
        if isinstance(p, int):
            return (b"I" + struct.pack("<i", p)) if -2**31 <= p < 2**31 else (b"L" + struct.pack("<q", p))
        if isinstance(p, float):
            return b"D" + struct.pack("<d", p)
        if isinstance(p, (str, bytes)):
            b = p if isinstance(p, bytes) else p.encode()
            return b"S" + struct.pack("<I", len(b)) + b
        raise TypeError(type(p))

    def write(self, buf: bytearray) -> None:
        start = len(buf)
        props = b"".join(self._prop(p) for p in self.props)
        buf += struct.pack("<III", 0, len(self.props), len(props))
        buf += struct.pack("<B", len(self.name)) + self.name + props
        if self.children or not self.props:
            for c in self.children:
                c.write(buf)
            buf += _SENTINEL
        struct.pack_into("<I", buf, start, len(buf))


def _p70(parent: Node, rows: list[tuple]) -> Node:
    p = parent.add("Properties70")
    for r in rows:
        p.add("P", *r)
    return p


def _name(n: str, cls: str) -> tuple:
    return ("S", n.encode() + b"\x00\x01" + cls.encode())


def write_fbx(mesh: Mesh, path: str, scale: float, textures: dict[int, str]) -> None:
    uid = [100000]

    def new_id() -> int:
        uid[0] += 1
        return uid[0]

    root: list[Node] = []
    hx = Node("FBXHeaderExtension")
    hx.add("FBXHeaderVersion", 1003)
    hx.add("FBXVersion", VERSION)
    hx.add("EncryptionType", 0)
    ts = hx.add("CreationTimeStamp")
    for k, v in (("Version", 1000), ("Year", 1970), ("Month", 1), ("Day", 1), ("Hour", 10),
                 ("Minute", 0), ("Second", 0), ("Millisecond", 0)):
        ts.add(k, v)
    hx.add("Creator", "N64 Asset Studio")
    root.append(hx)
    root.append(Node("FileId", ("R", _FILE_ID)))
    root.append(Node("CreationTime", "1970-01-01 10:00:00:000"))
    root.append(Node("Creator", "N64 Asset Studio"))

    gs = Node("GlobalSettings")
    gs.add("Version", 1000)
    _p70(gs, [
        ("UpAxis", "int", "Integer", "", 1), ("UpAxisSign", "int", "Integer", "", 1),
        ("FrontAxis", "int", "Integer", "", 2), ("FrontAxisSign", "int", "Integer", "", 1),
        ("CoordAxis", "int", "Integer", "", 0), ("CoordAxisSign", "int", "Integer", "", 1),
        ("OriginalUpAxis", "int", "Integer", "", 1), ("OriginalUpAxisSign", "int", "Integer", "", 1),
        ("UnitScaleFactor", "double", "Number", "", 100.0),
        ("OriginalUnitScaleFactor", "double", "Number", "", 100.0),
    ])
    root.append(gs)

    doc_id = new_id()
    docs = Node("Documents")
    docs.add("Count", 1)
    d = docs.add("Document", ("L", doc_id), "Scene", "Scene")
    _p70(d, [])
    d.add("RootNode", ("L", 0))
    root.append(docs)
    root.append(Node("References"))

    n_tex = len(textures)
    n_mat = len(mesh.materials)
    defs = Node("Definitions")
    defs.add("Version", 100)
    defs.add("Count", 3 + n_mat + 2 * n_tex)
    for typ, cnt in (("GlobalSettings", 1), ("Model", 1), ("Geometry", 1), ("Material", n_mat),
                     ("Texture", n_tex), ("Video", n_tex)):
        if cnt:
            defs.add("ObjectType", typ).add("Count", cnt)
    root.append(defs)

    objects = Node("Objects")
    conns = Node("Connections")

    # --- geometry ---------------------------------------------------
    verts = np.array([(v.x, v.y, v.z) for v in mesh.vertices], dtype=np.float64).reshape(-1, 3) * scale
    tris = np.array([t[:3] for t in mesh.triangles], dtype=np.int32).reshape(-1, 3)
    mats = np.array([t[3] for t in mesh.triangles], dtype=np.int32)
    pvi = tris.copy()
    pvi[:, 2] = -pvi[:, 2] - 1
    flat = tris.reshape(-1)
    normals = np.array([(v.nx, v.ny, v.nz) for v in mesh.vertices], dtype=np.float64).reshape(-1, 3)
    uvs = np.array([(v.u, 1.0 - v.v) for v in mesh.vertices], dtype=np.float64).reshape(-1, 2)
    cols = np.array([(v.r, v.g, v.b, v.a) for v in mesh.vertices], dtype=np.float64).reshape(-1, 4)

    geo_id = new_id()
    geo = objects.add("Geometry", ("L", geo_id), _name(mesh.name, "Geometry"), "Mesh")
    _p70(geo, [])
    geo.add("GeometryVersion", 124)
    geo.add("Vertices", ("d", verts.reshape(-1)))
    geo.add("PolygonVertexIndex", ("i", pvi.reshape(-1)))
    ln = geo.add("LayerElementNormal", 0)
    ln.add("Version", 101)
    ln.add("Name", "")
    ln.add("MappingInformationType", "ByPolygonVertex")
    ln.add("ReferenceInformationType", "Direct")
    ln.add("Normals", ("d", normals[flat].reshape(-1) if len(flat) else np.zeros(0)))
    lu = geo.add("LayerElementUV", 0)
    lu.add("Version", 101)
    lu.add("Name", "UVMap")
    lu.add("MappingInformationType", "ByPolygonVertex")
    lu.add("ReferenceInformationType", "IndexToDirect")
    lu.add("UV", ("d", uvs.reshape(-1)))
    lu.add("UVIndex", ("i", flat))
    if mesh.has_colors:
        lc = geo.add("LayerElementColor", 0)
        lc.add("Version", 101)
        lc.add("Name", "Col")
        lc.add("MappingInformationType", "ByPolygonVertex")
        lc.add("ReferenceInformationType", "IndexToDirect")
        lc.add("Colors", ("d", cols.reshape(-1)))
        lc.add("ColorIndex", ("i", flat))
    lm = geo.add("LayerElementMaterial", 0)
    lm.add("Version", 101)
    lm.add("Name", "")
    lm.add("MappingInformationType", "ByPolygon" if n_mat > 1 else "AllSame")
    lm.add("ReferenceInformationType", "IndexToDirect")
    lm.add("Materials", ("i", mats if n_mat > 1 else np.zeros(1, np.int32)))
    layer = geo.add("Layer", 0)
    layer.add("Version", 100)
    elems = ["LayerElementNormal", "LayerElementUV", "LayerElementMaterial"]
    if mesh.has_colors:
        elems.append("LayerElementColor")
    for e in elems:
        le = layer.add("LayerElement")
        le.add("Type", e)
        le.add("TypedIndex", 0)

    # --- model ------------------------------------------------------
    model_id = new_id()
    model = objects.add("Model", ("L", model_id), _name(mesh.name, "Model"), "Mesh")
    model.add("Version", 232)
    _p70(model, [
        ("Lcl Translation", "Lcl Translation", "", "A", 0.0, 0.0, 0.0),
        ("Lcl Rotation", "Lcl Rotation", "", "A", 0.0, 0.0, 0.0),
        ("Lcl Scaling", "Lcl Scaling", "", "A", 1.0, 1.0, 1.0),
        ("DefaultAttributeIndex", "int", "Integer", "", 0),
    ])
    model.add("Shading", ("C", True))
    model.add("Culling", "CullingOff")
    conns.add("C", "OO", ("L", model_id), ("L", 0))
    conns.add("C", "OO", ("L", geo_id), ("L", model_id))

    folder = os.path.dirname(os.path.abspath(path))
    for i, m in enumerate(mesh.materials):
        mat_id = new_id()
        mat = objects.add("Material", ("L", mat_id), _name(m.name, "Material"), "")
        mat.add("Version", 102)
        mat.add("ShadingModel", "lambert")
        mat.add("MultiLayer", 0)
        _p70(mat, [
            ("DiffuseColor", "Color", "", "A", float(m.color[0]), float(m.color[1]), float(m.color[2])),
            ("Diffuse", "Vector3D", "Vector", "", float(m.color[0]), float(m.color[1]), float(m.color[2])),
            ("Opacity", "double", "Number", "", float(m.color[3])),
        ])
        conns.add("C", "OO", ("L", mat_id), ("L", model_id))
        if i in textures:
            fn = textures[i]
            full = os.path.join(folder, fn)
            tex_id, vid_id = new_id(), new_id()
            vid = objects.add("Video", ("L", vid_id), _name(m.name, "Video"), "Clip")
            vid.add("Type", "Clip")
            _p70(vid, [("Path", "KString", "XRefUrl", "", full)])
            vid.add("UseMipMap", 0)
            vid.add("Filename", full)
            vid.add("RelativeFilename", fn)
            tex = objects.add("Texture", ("L", tex_id), _name(m.name, "Texture"), "")
            tex.add("Type", "TextureVideoClip")
            tex.add("Version", 202)
            tex.add("TextureName", _name(m.name, "Texture"))
            _p70(tex, [("UseMaterial", "bool", "", "", 1)])
            tex.add("Media", _name(m.name, "Video"))
            tex.add("FileName", full)
            tex.add("RelativeFilename", fn)
            tex.add("ModelUVTranslation", 0.0, 0.0)
            tex.add("ModelUVScaling", 1.0, 1.0)
            tex.add("Texture_Alpha_Source", "None")
            tex.add("Cropping", 0, 0, 0, 0)
            conns.add("C", "OO", ("L", vid_id), ("L", tex_id))
            conns.add("C", "OP", ("L", tex_id), ("L", mat_id), "DiffuseColor")

    root.append(objects)
    root.append(conns)
    takes = Node("Takes")
    takes.add("Current", "")
    root.append(takes)

    buf = bytearray(_HEAD + struct.pack("<I", VERSION))
    for n in root:
        n.write(buf)
    buf += _SENTINEL
    buf += _FOOT_ID + b"\x00" * 4
    ofs = len(buf) & 0x0F
    pad = ((ofs + 15) & ~0x0F) - ofs
    buf += b"\x00" * (pad or 16)
    buf += struct.pack("<I", VERSION) + b"\x00" * 120 + _FOOT_MAGIC
    with open(path, "wb") as f:
        f.write(buf)
