"""3D model writers: OBJ+MTL, glTF 2.0 (.gltf/.glb), COLLADA (.dae),
binary FBX 7.4 (.fbx), PLY, STL (binary)."""
from __future__ import annotations

import io
import json
import os
import re
import struct
from xml.sax.saxutils import escape

import numpy as np

from ..model import Material, Mesh
from . import fbx as fbx_writer

MODEL_FORMATS = {
    "glb": "glTF 2.0 binary (.glb) - Blender, Unity, Godot, Unreal, three.js",
    "gltf": "glTF 2.0 (.gltf + .bin + textures)",
    "fbx": "Autodesk FBX 7.4 binary (.fbx) - Blender, Unity, Unreal, Maya, 3ds Max",
    "obj": "Wavefront OBJ (.obj + .mtl + textures)",
    "dae": "COLLADA (.dae) - Blender, SketchUp, Maya",
    "ply": "Stanford PLY (.ply, vertex colours)",
    "stl": "STL binary (.stl, geometry only - 3D printing)",
}


def safe(name: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", name).strip("_") or "asset"


def texture_filename(mat: Material) -> str:
    return safe(mat.name) + ".png"


def _write_textures(mesh: Mesh, folder: str) -> dict[int, str]:
    out = {}
    for i, m in enumerate(mesh.materials):
        if m.texture is not None:
            fn = texture_filename(m)
            m.texture.save(os.path.join(folder, fn))
            out[i] = fn
    return out


def _arrays(mesh: Mesh, scale: float):
    pos = np.array([(v.x, v.y, v.z) for v in mesh.vertices], dtype=np.float32) * scale
    nrm = np.array([(v.nx, v.ny, v.nz) for v in mesh.vertices], dtype=np.float32)
    uv = np.array([(v.u, v.v) for v in mesh.vertices], dtype=np.float32)
    col = np.array([(v.r, v.g, v.b, v.a) for v in mesh.vertices], dtype=np.float32)
    if len(pos) == 0:
        pos = np.zeros((0, 3), np.float32)
        nrm = np.zeros((0, 3), np.float32)
        uv = np.zeros((0, 2), np.float32)
        col = np.zeros((0, 4), np.float32)
    return pos, nrm, uv, col


# ---------------------------------------------------------------- OBJ ----
def export_obj(mesh: Mesh, path: str, scale: float = 1.0) -> list[str]:
    folder = os.path.dirname(os.path.abspath(path))
    base = os.path.splitext(os.path.basename(path))[0]
    tex = _write_textures(mesh, folder)
    mtl_name = base + ".mtl"
    with open(os.path.join(folder, mtl_name), "w", encoding="utf-8") as f:
        f.write("# Exported by N64 Asset Studio\n")
        for i, m in enumerate(mesh.materials):
            f.write(f"\nnewmtl {safe(m.name)}\nKa 0 0 0\nKd {m.color[0]:.4f} {m.color[1]:.4f} {m.color[2]:.4f}\n")
            f.write(f"Ks 0 0 0\nd {m.color[3]:.4f}\nillum 1\n")
            if i in tex:
                f.write(f"map_Kd {tex[i]}\n")
                if m.has_alpha:
                    f.write(f"map_d {tex[i]}\n")
    with open(path, "w", encoding="utf-8") as f:
        f.write(f"# Exported by N64 Asset Studio\n# {len(mesh.vertices)} vertices, {len(mesh.triangles)} triangles\n")
        f.write(f"mtllib {mtl_name}\no {safe(mesh.name)}\n")
        for v in mesh.vertices:
            if mesh.has_colors:
                f.write(f"v {v.x * scale:.6f} {v.y * scale:.6f} {v.z * scale:.6f} {v.r:.4f} {v.g:.4f} {v.b:.4f}\n")
            else:
                f.write(f"v {v.x * scale:.6f} {v.y * scale:.6f} {v.z * scale:.6f}\n")
        for v in mesh.vertices:
            f.write(f"vt {v.u:.6f} {1.0 - v.v:.6f}\n")
        for v in mesh.vertices:
            f.write(f"vn {v.nx:.5f} {v.ny:.5f} {v.nz:.5f}\n")
        for mi, tris in sorted(mesh.triangles_by_material().items()):
            f.write(f"usemtl {safe(mesh.materials[mi].name)}\n")
            for a, b, c in tris:
                f.write(f"f {a + 1}/{a + 1}/{a + 1} {b + 1}/{b + 1}/{b + 1} {c + 1}/{c + 1}/{c + 1}\n")
    return [path, os.path.join(folder, mtl_name)] + [os.path.join(folder, t) for t in tex.values()]


# --------------------------------------------------------------- glTF ----
_WRAP = {"repeat": 10497, "mirror": 33648, "clamp": 33071}


def build_gltf(mesh: Mesh, scale: float = 1.0, embed_images: bool = True,
               image_uris: dict[int, str] | None = None) -> tuple[dict, bytes]:
    pos, nrm, uv, col = _arrays(mesh, scale)
    blob = bytearray()
    views, accessors = [], []

    def add_view(data: bytes, target: int | None = None) -> int:
        while len(blob) % 4:
            blob.append(0)
        v = {"buffer": 0, "byteOffset": len(blob), "byteLength": len(data)}
        if target:
            v["target"] = target
        blob.extend(data)
        views.append(v)
        return len(views) - 1

    def add_accessor(arr: np.ndarray, typ: str, ctype: int, target: int, minmax: bool = False) -> int:
        view = add_view(arr.tobytes(), target)
        acc = {"bufferView": view, "componentType": ctype, "count": int(len(arr)), "type": typ}
        if minmax and len(arr):
            acc["min"] = [float(x) for x in arr.min(0)]
            acc["max"] = [float(x) for x in arr.max(0)]
        accessors.append(acc)
        return len(accessors) - 1

    attrs = {"POSITION": add_accessor(pos, "VEC3", 5126, 34962, True),
             "NORMAL": add_accessor(nrm, "VEC3", 5126, 34962),
             "TEXCOORD_0": add_accessor(uv, "VEC2", 5126, 34962)}
    if mesh.has_colors:
        attrs["COLOR_0"] = add_accessor(col, "VEC4", 5126, 34962)

    images, textures, samplers, materials = [], [], [], []
    mat_map = {}
    for i, m in enumerate(mesh.materials):
        gm = {"name": safe(m.name), "doubleSided": True,
              "pbrMetallicRoughness": {"baseColorFactor": list(m.color), "metallicFactor": 0.0,
                                       "roughnessFactor": 1.0}}
        if m.texture is not None:
            if embed_images:
                bio = io.BytesIO()
                m.texture.save(bio, "PNG")
                images.append({"bufferView": add_view(bio.getvalue()), "mimeType": "image/png",
                               "name": safe(m.name)})
            else:
                images.append({"uri": (image_uris or {}).get(i, texture_filename(m))})
            samplers.append({"magFilter": 9729, "minFilter": 9729,
                             "wrapS": _WRAP[m.wrap_s], "wrapT": _WRAP[m.wrap_t]})
            textures.append({"source": len(images) - 1, "sampler": len(samplers) - 1})
            gm["pbrMetallicRoughness"]["baseColorTexture"] = {"index": len(textures) - 1}
        if m.has_alpha:
            gm["alphaMode"] = "MASK"
            gm["alphaCutoff"] = 0.5
        materials.append(gm)
        mat_map[i] = len(materials) - 1

    prims = []
    for mi, tris in sorted(mesh.triangles_by_material().items()):
        idx = np.asarray(tris, dtype=np.uint32).reshape(-1)
        ia = add_accessor(idx, "SCALAR", 5125, 34963)
        prims.append({"attributes": attrs, "indices": ia, "material": mat_map[mi], "mode": 4})
    if not prims:
        prims.append({"attributes": attrs, "mode": 0})

    gltf = {
        "asset": {"version": "2.0", "generator": "N64 Asset Studio"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"name": safe(mesh.name), "mesh": 0}],
        "meshes": [{"name": safe(mesh.name), "primitives": prims}],
        "buffers": [{"byteLength": len(blob)}],
        "bufferViews": views,
        "accessors": accessors,
    }
    if materials:
        gltf["materials"] = materials
    if images:
        gltf["images"] = images
        gltf["textures"] = textures
        gltf["samplers"] = samplers
    return gltf, bytes(blob)


def export_glb(mesh: Mesh, path: str, scale: float = 1.0) -> list[str]:
    gltf, blob = build_gltf(mesh, scale, embed_images=True)
    while len(blob) % 4:
        blob += b"\0"
    gltf["buffers"][0]["byteLength"] = len(blob)
    js = json.dumps(gltf, separators=(",", ":")).encode()
    while len(js) % 4:
        js += b" "
    total = 12 + 8 + len(js) + 8 + len(blob)
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
        f.write(struct.pack("<II", len(blob), 0x004E4942) + blob)
    return [path]


def export_gltf(mesh: Mesh, path: str, scale: float = 1.0) -> list[str]:
    folder = os.path.dirname(os.path.abspath(path))
    base = os.path.splitext(os.path.basename(path))[0]
    tex = _write_textures(mesh, folder)
    gltf, blob = build_gltf(mesh, scale, embed_images=False, image_uris=tex)
    bin_name = base + ".bin"
    gltf["buffers"][0]["uri"] = bin_name
    with open(os.path.join(folder, bin_name), "wb") as f:
        f.write(blob)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(gltf, f, indent=1)
    return [path, os.path.join(folder, bin_name)] + [os.path.join(folder, t) for t in tex.values()]


# ------------------------------------------------------------ COLLADA ----
def export_dae(mesh: Mesh, path: str, scale: float = 1.0) -> list[str]:
    folder = os.path.dirname(os.path.abspath(path))
    tex = _write_textures(mesh, folder)
    pos, nrm, uv, col = _arrays(mesh, scale)
    uv_flip = uv.copy()
    if len(uv_flip):
        uv_flip[:, 1] = 1.0 - uv_flip[:, 1]
    name = safe(mesh.name)

    def floats(a):
        return " ".join(f"{x:.6g}" for x in a.reshape(-1))

    x = ['<?xml version="1.0" encoding="utf-8"?>',
         '<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">',
         '<asset><contributor><authoring_tool>N64 Asset Studio</authoring_tool></contributor>'
         '<unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>']
    x.append("<library_images>")
    for i, fn in tex.items():
        x.append(f'<image id="img{i}" name="img{i}"><init_from>{escape(fn)}</init_from></image>')
    x.append("</library_images><library_effects>")
    for i, m in enumerate(mesh.materials):
        x.append(f'<effect id="fx{i}"><profile_COMMON>')
        if i in tex:
            x.append(f'<newparam sid="surf{i}"><surface type="2D"><init_from>img{i}</init_from></surface></newparam>'
                     f'<newparam sid="samp{i}"><sampler2D><source>surf{i}</source></sampler2D></newparam>')
        x.append('<technique sid="common"><lambert><diffuse>')
        if i in tex:
            x.append(f'<texture texture="samp{i}" texcoord="UVSET0"/>')
        else:
            x.append(f'<color>{m.color[0]:.4f} {m.color[1]:.4f} {m.color[2]:.4f} {m.color[3]:.4f}</color>')
        x.append("</diffuse></lambert></technique></profile_COMMON></effect>")
    x.append("</library_effects><library_materials>")
    for i, m in enumerate(mesh.materials):
        x.append(f'<material id="mat{i}" name="{escape(safe(m.name))}"><instance_effect url="#fx{i}"/></material>')
    x.append("</library_materials><library_geometries>")
    n = len(pos)
    x.append(f'<geometry id="geom" name="{name}"><mesh>')
    for sid, arr, params in (("pos", pos, "XYZ"), ("nrm", nrm, "XYZ"), ("uv", uv_flip, "ST"), ("col", col, "RGBA")):
        stride = len(params)
        x.append(f'<source id="{sid}"><float_array id="{sid}-a" count="{n * stride}">{floats(arr)}</float_array>'
                 f'<technique_common><accessor source="#{sid}-a" count="{n}" stride="{stride}">'
                 + "".join(f'<param name="{p}" type="float"/>' for p in params)
                 + "</accessor></technique_common></source>")
    x.append('<vertices id="verts"><input semantic="POSITION" source="#pos"/></vertices>')
    for mi, tris in sorted(mesh.triangles_by_material().items()):
        idx = " ".join(str(i) for t in tris for i in t)
        x.append(f'<triangles material="m{mi}" count="{len(tris)}">'
                 '<input semantic="VERTEX" source="#verts" offset="0"/>'
                 '<input semantic="NORMAL" source="#nrm" offset="0"/>'
                 '<input semantic="TEXCOORD" source="#uv" offset="0" set="0"/>'
                 + ('<input semantic="COLOR" source="#col" offset="0" set="0"/>' if mesh.has_colors else "")
                 + f"<p>{idx}</p></triangles>")
    x.append("</mesh></geometry></library_geometries>")
    x.append(f'<library_visual_scenes><visual_scene id="scene"><node id="{name}" name="{name}">'
             '<instance_geometry url="#geom"><bind_material><technique_common>')
    for mi in sorted(mesh.triangles_by_material()):
        x.append(f'<instance_material symbol="m{mi}" target="#mat{mi}">'
                 '<bind_vertex_input semantic="UVSET0" input_semantic="TEXCOORD" input_set="0"/>'
                 "</instance_material>")
    x.append("</technique_common></bind_material></instance_geometry></node></visual_scene>"
             '</library_visual_scenes><scene><instance_visual_scene url="#scene"/></scene></COLLADA>')
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(x))
    return [path] + [os.path.join(folder, t) for t in tex.values()]


# ---------------------------------------------------------------- PLY ----
def export_ply(mesh: Mesh, path: str, scale: float = 1.0) -> list[str]:
    pos, nrm, uv, col = _arrays(mesh, scale)
    hdr = ["ply", "format binary_little_endian 1.0", "comment N64 Asset Studio",
           f"element vertex {len(pos)}",
           "property float x", "property float y", "property float z",
           "property float nx", "property float ny", "property float nz",
           "property float s", "property float t",
           "property uchar red", "property uchar green", "property uchar blue", "property uchar alpha",
           f"element face {len(mesh.triangles)}", "property list uchar int vertex_indices", "end_header", ""]
    c8 = np.clip(col * 255, 0, 255).astype(np.uint8)
    rec = np.zeros(len(pos), dtype=[("p", "<f4", 3), ("n", "<f4", 3), ("t", "<f4", 2), ("c", "u1", 4)])
    rec["p"], rec["n"], rec["c"] = pos, nrm, c8
    if len(uv):
        rec["t"] = np.stack([uv[:, 0], 1.0 - uv[:, 1]], 1)
    faces = np.zeros(len(mesh.triangles), dtype=[("n", "u1"), ("i", "<i4", 3)])
    faces["n"] = 3
    if mesh.triangles:
        faces["i"] = np.array([t[:3] for t in mesh.triangles], dtype=np.int32)
    with open(path, "wb") as f:
        f.write("\n".join(hdr).encode())
        f.write(rec.tobytes())
        f.write(faces.tobytes())
    return [path]


# ---------------------------------------------------------------- STL ----
def export_stl(mesh: Mesh, path: str, scale: float = 1.0) -> list[str]:
    pos, *_ = _arrays(mesh, scale)
    tris = np.array([t[:3] for t in mesh.triangles], dtype=np.int64).reshape(-1, 3)
    rec = np.zeros(len(tris), dtype=[("n", "<f4", 3), ("v", "<f4", (3, 3)), ("a", "<u2")])
    if len(tris):
        v = pos[tris]
        n = np.cross(v[:, 1] - v[:, 0], v[:, 2] - v[:, 0])
        ln = np.linalg.norm(n, axis=1, keepdims=True)
        ln[ln == 0] = 1
        rec["n"] = n / ln
        rec["v"] = v
    with open(path, "wb") as f:
        f.write(b"N64 Asset Studio STL".ljust(80, b" "))
        f.write(struct.pack("<I", len(tris)))
        f.write(rec.tobytes())
    return [path]


def export_fbx(mesh: Mesh, path: str, scale: float = 1.0) -> list[str]:
    folder = os.path.dirname(os.path.abspath(path))
    tex = _write_textures(mesh, folder)
    fbx_writer.write_fbx(mesh, path, scale, tex)
    return [path] + [os.path.join(folder, t) for t in tex.values()]


EXPORTERS = {
    "obj": export_obj, "gltf": export_gltf, "glb": export_glb, "dae": export_dae,
    "ply": export_ply, "stl": export_stl, "fbx": export_fbx,
}


def export_model(mesh: Mesh, path: str, fmt: str, scale: float = 1.0) -> list[str]:
    fmt = fmt.lower()
    if fmt not in EXPORTERS:
        raise ValueError(f"unknown model format {fmt}")
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    return EXPORTERS[fmt](mesh, path, scale)


def glb_bytes(mesh: Mesh, scale: float = 1.0) -> bytes:
    bio = io.BytesIO()
    gltf, blob = build_gltf(mesh, scale, embed_images=True)
    while len(blob) % 4:
        blob += b"\0"
    gltf["buffers"][0]["byteLength"] = len(blob)
    js = json.dumps(gltf, separators=(",", ":")).encode()
    while len(js) % 4:
        js += b" "
    bio.write(struct.pack("<III", 0x46546C67, 2, 28 + len(js) + len(blob)))
    bio.write(struct.pack("<II", len(js), 0x4E4F534A) + js)
    bio.write(struct.pack("<II", len(blob), 0x004E4942) + blob)
    return bio.getvalue()

