"""Runs *inside* Blender (``blender -b file.blend --python blender_export.py -- ...``).

Exports the open .blend to an FBX suited to Unity and copies every image the
file uses into a ``Textures`` folder next to it. With ``--bake``, materials
whose color or normals come from node setups (procedural textures, mixes,
bump) are first rendered down to image textures with Cycles, so they look the
same in Unity. Progress is reported to the parent process on stdout as lines
starting with ``@@B2U``.
"""

import argparse
import os
import re
import shutil
import sys
import traceback

import bpy

TAG = "@@B2U"


def emit(kind, *parts):
    print(TAG, kind, *parts, flush=True)


def progress(fraction, message):
    emit("PROGRESS", f"{fraction:.3f}", message)


def warn(message):
    emit("WARN", message)


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser(prog="blender_export.py")
    p.add_argument("--out", required=True, help="Directory to write the export into")
    p.add_argument("--name", required=True, help="Base name of the FBX file")
    p.add_argument("--apply-transform", action="store_true")
    p.add_argument("--animation", action="store_true")
    p.add_argument("--textures", action="store_true")
    p.add_argument("--modifiers", action="store_true")
    p.add_argument("--bake", action="store_true", help="Bake node-based materials to textures")
    p.add_argument("--bake-size", type=int, default=1024, help="Baked texture size in pixels")
    return p.parse_args(argv)


def safe_name(name):
    name = re.sub(r'[<>:"/\\|?*\x00-\x1f]', "_", name).strip(" .")
    return name or "texture"


EXT_BY_FORMAT = {
    "PNG": ".png", "JPEG": ".jpg", "TARGA": ".tga", "TARGA_RAW": ".tga",
    "BMP": ".bmp", "OPEN_EXR": ".exr", "OPEN_EXR_MULTILAYER": ".exr",
    "HDR": ".hdr", "TIFF": ".tif", "WEBP": ".webp",
}


def export_textures(tex_dir, start=0.10, span=0.25):
    images = [
        img for img in bpy.data.images
        if img.users > 0 and img.type in {"IMAGE", "UV_TEST"} and img.source in {"FILE", "GENERATED"}
    ]
    if not images:
        return 0
    os.makedirs(tex_dir, exist_ok=True)
    used_names = set()
    count = 0
    for i, img in enumerate(images):
        progress(start + span * i / len(images), f"Copying texture {img.name}")
        src = bpy.path.abspath(img.filepath, library=img.library) if img.filepath else ""
        base = os.path.basename(src) if src else ""
        stem, ext = os.path.splitext(base)
        if not stem:
            stem = img.name
        if not ext or img.source == "GENERATED":
            ext = EXT_BY_FORMAT.get(img.file_format, ".png")
        stem = safe_name(stem)
        filename = stem + ext
        n = 1
        while filename.lower() in used_names:
            filename = f"{stem}_{n}{ext}"
            n += 1
        dst = os.path.join(tex_dir, filename)

        try:
            if img.packed_file is not None:
                with open(dst, "wb") as fh:
                    fh.write(img.packed_file.data)
            elif img.source == "FILE" and src and os.path.isfile(src):
                shutil.copyfile(src, dst)
            elif img.has_data:
                # Generated or painted image that was never saved to disk.
                if img.source == "GENERATED":
                    img.file_format = "PNG"
                img.filepath_raw = dst
                img.save()
            else:
                warn(f"Texture '{img.name}' not found ({src or 'no file path'}); skipped")
                continue
        except Exception as exc:  # keep going; one bad texture shouldn't fail the export
            warn(f"Could not export texture '{img.name}': {exc}")
            continue

        used_names.add(filename.lower())
        # The FBX stores just the file name (path_mode='STRIP'); Unity then
        # finds the texture in the Textures folder next to the model.
        img.filepath_raw = dst
        count += 1
    return count


# --------------------------------------------------------------------- baking
#
# Unity only understands image textures, so a material whose Base Color or
# Normal is driven by other nodes (Noise, Voronoi, ramps, mixes, bump...) is
# rendered to images with Cycles, and the material is rewired to use them.
#
#  * Principled BSDF: the node feeding Base Color is temporarily routed into an
#    Emission shader and baked with the EMIT pass, which captures the exact
#    color (a DIFFUSE bake would come out black for metallic surfaces).
#  * Any other shader (Mix Shader, Diffuse, ...): the DIFFUSE color pass, and
#    the material is replaced by a Principled BSDF using the baked image.
#  * Normal input driven by Bump / procedural nodes: a tangent-space NORMAL bake.
#
# Materials that already use plain image textures are left alone.

def _plain_image(socket):
    """True if socket is fed straight from an Image Texture with default UVs."""
    if not socket.is_linked:
        return False
    node = socket.links[0].from_node
    return (node.type == "TEX_IMAGE" and node.image is not None
            and not node.inputs["Vector"].is_linked)


def _plain_normal_map(socket):
    node = socket.links[0].from_node
    return node.type == "NORMAL_MAP" and _plain_image(node.inputs["Color"])


def _surface(mat):
    """(output node, shader node) for a node material, or (None, None)."""
    if mat is None or not mat.use_nodes or mat.node_tree is None:
        return None, None
    out = mat.node_tree.get_output_node("CYCLES")
    if out is None or not out.inputs["Surface"].is_linked:
        return None, None
    return out, out.inputs["Surface"].links[0].from_node


def bake_plan(mat):
    """Which maps a material needs baked: {'color': 'EMIT'|'DIFFUSE', 'normal': True}."""
    out, shader = _surface(mat)
    if shader is None:
        return {}
    plan = {}
    if shader.type == "BSDF_PRINCIPLED":
        color, normal = shader.inputs["Base Color"], shader.inputs["Normal"]
        if color.is_linked and not _plain_image(color):
            plan["color"] = "EMIT"
        if normal.is_linked and not _plain_normal_map(normal):
            plan["normal"] = True
    else:
        plan["color"] = "DIFFUSE"
        if any(n.type in {"BUMP", "NORMAL_MAP", "DISPLACEMENT"} for n in mat.node_tree.nodes):
            plan["normal"] = True
    return plan


def _select_only(obj):
    vl = bpy.context.view_layer
    for o in vl.objects:
        o.select_set(False)
    obj.select_set(True)
    vl.objects.active = obj


def ensure_uvs(obj):
    if obj.data.uv_layers:
        return False
    _select_only(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    return True


def _target_node(mat, image):
    """Add an Image Texture node holding image and make it the bake target."""
    nodes = mat.node_tree.nodes
    node = nodes.new("ShaderNodeTexImage")
    node.image = image
    node.location = (-600, 400)
    for n in nodes:
        n.select = False
    node.select = True
    nodes.active = node
    return node


def _bake_pass(obj, bake_type, targets, slots):
    """Bake one pass for obj. targets: {slot index: image}; other slots get a
    throwaway target, because Cycles needs one in every material it bakes."""
    added, dummies, rewired = [], [], []
    for i in slots:
        mat = obj.material_slots[i].material
        img = targets.get(i)
        if img is None:
            img = bpy.data.images.new("b2u_dummy", 4, 4)
            dummies.append(img)
        added.append((mat, _target_node(mat, img)))
        if bake_type == "EMIT" and i in targets:
            # Route the Base Color source into an Emission shader for the bake.
            out, shader = _surface(mat)
            links = mat.node_tree.links
            emission = mat.node_tree.nodes.new("ShaderNodeEmission")
            links.new(shader.inputs["Base Color"].links[0].from_socket, emission.inputs["Color"])
            emission.inputs["Strength"].default_value = 1.0
            links.new(emission.outputs["Emission"], out.inputs["Surface"])
            rewired.append((mat, out, shader, emission))
        elif bake_type == "EMIT":
            # Keep other materials black so they don't bleed into shared texels.
            out, _ = _surface(mat)
            if out is not None:
                black = mat.node_tree.nodes.new("ShaderNodeEmission")
                black.inputs["Color"].default_value = (0, 0, 0, 1)
                orig = out.inputs["Surface"].links[0].from_socket if out.inputs["Surface"].is_linked else None
                mat.node_tree.links.new(black.outputs["Emission"], out.inputs["Surface"])
                rewired.append((mat, out, orig, black))
    try:
        _select_only(obj)
        kwargs = {"type": bake_type, "margin": 16, "use_clear": True, "target": "IMAGE_TEXTURES"}
        if bake_type == "DIFFUSE":
            kwargs["pass_filter"] = {"COLOR"}
        bpy.ops.object.bake(**kwargs)
    finally:
        for mat, out, shader, temp in rewired:
            src = shader.outputs[0] if hasattr(shader, "outputs") else shader
            if src is not None:
                mat.node_tree.links.new(src, out.inputs["Surface"])
            mat.node_tree.nodes.remove(temp)
        for mat, node in added:
            mat.node_tree.nodes.remove(node)
        for img in dummies:
            bpy.data.images.remove(img)


def _use_baked(mat, plan, color_img, normal_img):
    """Rewire mat so its Base Color / Normal come from the baked images."""
    tree = mat.node_tree
    out, shader = _surface(mat)
    if shader.type != "BSDF_PRINCIPLED":
        # Replace the whole shader with a Principled BSDF using the baked maps.
        old = shader
        shader = tree.nodes.new("ShaderNodeBsdfPrincipled")
        shader.location = (old.location.x, old.location.y - 300)
        tree.links.new(shader.outputs["BSDF"], out.inputs["Surface"])
    if color_img is not None:
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.image = color_img
        tex.location = (shader.location.x - 400, shader.location.y)
        tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])
    if normal_img is not None:
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.image = normal_img
        tex.location = (shader.location.x - 700, shader.location.y - 300)
        nmap = tree.nodes.new("ShaderNodeNormalMap")
        nmap.location = (shader.location.x - 400, shader.location.y - 300)
        tree.links.new(tex.outputs["Color"], nmap.inputs["Color"])
        tree.links.new(nmap.outputs["Normal"], shader.inputs["Normal"])


def bake_materials(size, start, span):
    scene = bpy.context.scene
    objects = [o for o in bpy.context.view_layer.objects if o.type == "MESH"]
    todo = []
    for o in objects:
        if any(bake_plan(s.material) for s in o.material_slots):
            todo.append(o)
    if not todo:
        emit("INFO", "No node-based materials need baking")
        return 0

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 1
    if hasattr(scene.cycles, "use_denoising"):
        scene.cycles.use_denoising = False

    baked = 0
    originals = set()
    for n, obj in enumerate(todo):
        progress(start + span * n / len(todo), f"Baking materials on {obj.name}")
        if not obj.visible_get() or not obj.data.polygons:
            warn(f"Skipped baking '{obj.name}' (hidden or has no faces)")
            continue
        try:
            # Give this object its own mesh and material copies, so baking it
            # doesn't change other objects that share them.
            if obj.data.users > 1:
                obj.data = obj.data.copy()
            slots, plans = [], {}
            for i, slot in enumerate(obj.material_slots):
                if slot.material is None:
                    continue
                plan = bake_plan(slot.material)
                if plan:
                    orig = slot.material
                    originals.add(orig)
                    copy = orig.copy()
                    copy.name = f"{orig.name}_{obj.name}" if orig.users > 1 else orig.name
                    slot.material = copy
                    plans[i] = (plan, orig.name)
                slots.append(i)
            if ensure_uvs(obj):
                emit("INFO", f"Unwrapped UVs for {obj.name}")

            def new_image(i, suffix, non_color=False):
                img = bpy.data.images.new(f"{obj.name}_{plans[i][1]}_{suffix}", size, size, alpha=False)
                if non_color:
                    img.colorspace_settings.name = "Non-Color"
                return img

            color_emit = {i: new_image(i, "BaseColor") for i, (p, _) in plans.items() if p.get("color") == "EMIT"}
            color_diffuse = {i: new_image(i, "BaseColor") for i, (p, _) in plans.items() if p.get("color") == "DIFFUSE"}
            normals = {i: new_image(i, "Normal", True) for i, (p, _) in plans.items() if p.get("normal")}

            if color_emit:
                _bake_pass(obj, "EMIT", color_emit, slots)
            if color_diffuse:
                _bake_pass(obj, "DIFFUSE", color_diffuse, slots)
            if normals:
                _bake_pass(obj, "NORMAL", normals, slots)

            for i, (plan, _) in plans.items():
                mat = obj.material_slots[i].material
                _use_baked(mat, plan, color_emit.get(i) or color_diffuse.get(i), normals.get(i))
                baked += 1
        except Exception as exc:
            warn(f"Could not bake materials on '{obj.name}': {exc}")

    # Drop the original node materials nothing uses any more, so their
    # procedural-only images don't end up in the package.
    for mat in originals:
        if mat.users == 0:
            bpy.data.materials.remove(mat)
    return baked


def ensure_fbx_exporter():
    if hasattr(bpy.ops.export_scene, "fbx"):
        return
    import addon_utils
    addon_utils.enable("io_scene_fbx", default_set=True)


def main():
    args = parse_args()
    os.makedirs(args.out, exist_ok=True)

    progress(0.05, "Preparing scene")
    obj = bpy.context.view_layer.objects.active
    if obj is not None and obj.mode != "OBJECT":
        try:
            bpy.ops.object.mode_set(mode="OBJECT")
        except Exception:
            pass

    exportable = [o for o in bpy.context.scene.objects
                  if o.type in {"MESH", "ARMATURE", "EMPTY", "CURVE", "SURFACE",
                                "FONT", "META", "CAMERA", "LIGHT"}]
    if not exportable:
        warn("The scene contains no exportable objects")

    if args.bake:
        n = bake_materials(args.bake_size, 0.08, 0.52)
        if n:
            emit("INFO", f"Baked {n} material(s) to textures")

    if args.textures or args.bake:
        n = export_textures(os.path.join(args.out, "Textures"), 0.60, 0.10)
        emit("INFO", f"Exported {n} texture(s)")

    progress(0.72, "Exporting FBX")
    ensure_fbx_exporter()
    fbx_path = os.path.join(args.out, args.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=False,
        object_types={"ARMATURE", "CAMERA", "EMPTY", "LIGHT", "MESH", "OTHER"},
        use_mesh_modifiers=args.modifiers,
        mesh_smooth_type="FACE",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=args.apply_transform,
        add_leaf_bones=False,
        bake_anim=args.animation,
        path_mode="STRIP",
        embed_textures=False,
    )
    if not os.path.isfile(fbx_path):
        raise RuntimeError("FBX exporter did not produce a file")
    progress(1.0, "Blender export finished")
    emit("DONE", fbx_path)


try:
    main()
except SystemExit:
    raise
except BaseException:
    for line in traceback.format_exc().splitlines():
        emit("ERROR", line)
    sys.exit(1)
