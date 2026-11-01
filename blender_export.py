"""Runs *inside* Blender (``blender -b file.blend --python blender_export.py -- ...``).

Exports the open .blend to an FBX suited to Unity and copies every image the
file uses into a ``Textures`` folder next to it. Progress is reported to the
parent process on stdout as lines starting with ``@@B2U``.
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
    return p.parse_args(argv)


def safe_name(name):
    name = re.sub(r'[<>:"/\\|?*\x00-\x1f]', "_", name).strip(" .")
    return name or "texture"


EXT_BY_FORMAT = {
    "PNG": ".png", "JPEG": ".jpg", "TARGA": ".tga", "TARGA_RAW": ".tga",
    "BMP": ".bmp", "OPEN_EXR": ".exr", "OPEN_EXR_MULTILAYER": ".exr",
    "HDR": ".hdr", "TIFF": ".tif", "WEBP": ".webp",
}


def export_textures(tex_dir):
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
        progress(0.10 + 0.25 * i / len(images), f"Copying texture {img.name}")
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

    if args.textures:
        n = export_textures(os.path.join(args.out, "Textures"))
        emit("INFO", f"Exported {n} texture(s)")

    progress(0.40, "Exporting FBX")
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
