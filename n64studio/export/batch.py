"""Export every asset of a project in one go."""
from __future__ import annotations

import os
import shutil
import tempfile
from dataclasses import dataclass

from .. import mips
from ..project import Project
from . import blender as blender_export
from .images import save_image
from .models import export_model, safe
from .soundfont import bank_file_to_sf2
from .sounds import save_audio
from .unity import UnityPackage


@dataclass
class BatchOptions:
    models: bool = True
    model_format: str = "glb"
    model_scale: float = 1.0
    roots_only: bool = True
    textures: bool = True
    image_format: str = "png"
    audio: bool = True
    audio_format: str = "wav"
    soundfonts: bool = True
    music: bool = True
    raw_files: bool = False
    code: bool = False
    unity_package: bool = False
    unity_model_format: str = "fbx"
    blender: bool = False


def export_all(project: Project, out_dir: str, opt: BatchOptions, progress=None, cancel=None) -> list[str]:
    prog = progress or (lambda f, m: None)
    stop = cancel or (lambda: False)
    game = safe(project.rom.display_title)
    root = os.path.join(out_dir, game)
    os.makedirs(root, exist_ok=True)
    report: list[str] = []
    errors = 0

    models = [m for m in project.models if not (opt.roots_only and m.dl.is_child)]
    meshes = []
    if opt.models or opt.unity_package or opt.blender:
        for i, m in enumerate(models):
            if stop():
                return report + ["Cancelled"]
            if i % 8 == 0:
                prog(0.4 * i / max(1, len(models)), f"Building models ({i}/{len(models)})")
            try:
                mesh = project.get_mesh(m)
            except Exception:  # noqa: BLE001 - keep going on bad data
                errors += 1
                continue
            if mesh.triangles:
                meshes.append(mesh)
    if opt.models:
        d = os.path.join(root, "Models")
        for i, mesh in enumerate(meshes):
            if stop():
                return report + ["Cancelled"]
            if i % 8 == 0:
                prog(0.4 + 0.15 * i / max(1, len(meshes)), f"Writing models ({i}/{len(meshes)})")
            sub = os.path.join(d, safe(mesh.name)) if opt.model_format in ("obj", "gltf", "dae", "fbx") else d
            try:
                export_model(mesh, os.path.join(sub, safe(mesh.name) + "." + opt.model_format),
                             opt.model_format, opt.model_scale)
            except Exception:  # noqa: BLE001
                errors += 1
        report.append(f"{len(meshes)} models -> {d}")

    if opt.textures:
        d = os.path.join(root, "Textures")
        n = 0
        for i, t in enumerate(project.textures):
            if stop():
                return report + ["Cancelled"]
            if i % 32 == 0:
                prog(0.55 + 0.1 * i / max(1, len(project.textures)), f"Textures ({i}/{len(project.textures)})")
            try:
                save_image(project.texture_image(t),
                           os.path.join(d, f"b{t.blob_index}_{t.offset:06X}_{t.fmt}_{t.width}x{t.height}"),
                           opt.image_format)
                n += 1
            except Exception:  # noqa: BLE001
                errors += 1
        report.append(f"{n} textures -> {d}")

    sounds = project.all_sounds()
    if opt.audio:
        d = os.path.join(root, "Audio")
        n = 0
        for i, ref in enumerate(sounds):
            if stop():
                return report + ["Cancelled"]
            if i % 16 == 0:
                prog(0.65 + 0.15 * i / max(1, len(sounds)), f"Sounds ({i}/{len(sounds)})")
            try:
                pcm, rate = project.decode_sound(ref)
                save_audio(pcm, rate, os.path.join(d, f"bankfile{ref.bank_file}", ref.name), opt.audio_format)
                n += 1
            except Exception:  # noqa: BLE001
                errors += 1
        report.append(f"{n} sounds -> {d}")
    if opt.soundfonts:
        d = os.path.join(root, "Audio")
        os.makedirs(d, exist_ok=True)
        for fi in range(len(project.bank_files)):
            try:
                bank_file_to_sf2(project, fi, os.path.join(d, f"bankfile{fi}.sf2"))
                report.append(f"SoundFont bankfile{fi}.sf2")
            except Exception:  # noqa: BLE001
                errors += 1

    if opt.music:
        d = os.path.join(root, "Music")
        os.makedirs(d, exist_ok=True)
        n = 0
        for i, s in enumerate(project.sequences):
            name = f"seq{i:03d}_b{s.blob_index}_{s.offset:06X}"
            try:
                midi = project.sequence_midi(s)
                if midi:
                    with open(os.path.join(d, name + ".mid"), "wb") as f:
                        f.write(midi)
                    n += 1
                if s.kind != "MIDI":
                    with open(os.path.join(d, name + ".seq"), "wb") as f:
                        f.write(project.sequence_raw(s))
            except Exception:  # noqa: BLE001
                errors += 1
        report.append(f"{n} MIDI sequences -> {d}")

    if opt.raw_files:
        d = os.path.join(root, "Files")
        os.makedirs(d, exist_ok=True)
        for b in project.blobs[1:]:
            with open(os.path.join(d, safe(b.name) + ".bin"), "wb") as f:
                f.write(b.data)
        report.append(f"{len(project.blobs) - 1} decompressed files -> {d}")

    if opt.code and project.functions:
        prog(0.82, "Writing pseudo-C")
        path = os.path.join(root, "code_pseudo.c")
        rom = project.rom
        with open(path, "w", encoding="utf-8") as f:
            f.write(f"// {rom.display_title} - pseudo-C lifted from MIPS R4300i machine code\n\n")
            for fn in project.functions[:5000]:
                off = rom.entry_to_rom(fn)
                if off is not None:
                    f.write(mips.function_to_pseudo_c(rom.data, off, fn, 2000) + "\n\n")
        report.append(f"pseudo-C for {min(5000, len(project.functions))} functions -> {path}")

    if opt.unity_package:
        prog(0.85, "Building Unity package")
        pkg = UnityPackage(f"Assets/N64/{game}")
        tmp = tempfile.mkdtemp(prefix="n64studio_")
        try:
            for mesh in meshes:
                sub = os.path.join(tmp, "Models", safe(mesh.name))
                export_model(mesh, os.path.join(sub, safe(mesh.name) + "." + opt.unity_model_format),
                             opt.unity_model_format, opt.model_scale)
            if opt.textures:
                for t in project.textures:
                    try:
                        save_image(project.texture_image(t), os.path.join(
                            tmp, "Textures", f"b{t.blob_index}_{t.offset:06X}_{t.fmt}_{t.width}x{t.height}.png"),
                            "png")
                    except Exception:  # noqa: BLE001
                        pass
            for ref in sounds:
                try:
                    pcm, rate = project.decode_sound(ref)
                    save_audio(pcm, rate, os.path.join(tmp, "Audio", f"bankfile{ref.bank_file}",
                                                       ref.name + ".wav"), "wav")
                except Exception:  # noqa: BLE001
                    pass
            for i, s in enumerate(project.sequences):
                try:
                    midi = project.sequence_midi(s)
                except Exception:  # noqa: BLE001
                    midi = None
                if midi:
                    os.makedirs(os.path.join(tmp, "Music"), exist_ok=True)
                    with open(os.path.join(tmp, "Music", f"seq{i:03d}.mid.bytes"), "wb") as f:
                        f.write(midi)
            pkg.add_folder_tree("", tmp)
            # add_folder_tree with an empty prefix yields "Assets/N64/<game>//x"; normalise
            pkg.files = {k.replace("//", "/"): v for k, v in pkg.files.items()}
            out = pkg.save(os.path.join(out_dir, game + ".unitypackage"))
            report.append(f"Unity package -> {out}")
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    if opt.blender and meshes:
        prog(0.92, "Building Blender scene")
        res = blender_export.export_blender(meshes, os.path.join(root, "Blender"), game, opt.model_scale)
        report.append(res["message"] or "Blender export written")

    if errors:
        report.append(f"{errors} items could not be decoded and were skipped")
    prog(1.0, "Export finished")
    return report
