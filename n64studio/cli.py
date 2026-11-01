"""Command-line interface for scripted / batch exports.

    N64AssetStudio.exe --cli info  game.z64
    N64AssetStudio.exe --cli export game.z64 out_dir --models fbx --audio wav --unity --blender
"""
from __future__ import annotations

import argparse
import sys

from .export.batch import BatchOptions, export_all
from .project import Project, ScanOptions


def main(argv: list[str]) -> None:
    ap = argparse.ArgumentParser(prog="n64studio --cli")
    sub = ap.add_subparsers(dest="cmd", required=True)
    pi = sub.add_parser("info", help="print ROM info and scan summary")
    pi.add_argument("rom")
    pe = sub.add_parser("export", help="export all assets")
    pe.add_argument("rom")
    pe.add_argument("out")
    pe.add_argument("--models", default="glb", help="glb|gltf|fbx|obj|dae|ply|stl|none")
    pe.add_argument("--images", default="png")
    pe.add_argument("--audio", default="wav", help="wav|aiff|flac|au|raw|ogg|mp3|...|none")
    pe.add_argument("--scale", type=float, default=1.0)
    pe.add_argument("--unity", action="store_true", help="also write a .unitypackage")
    pe.add_argument("--blender", action="store_true", help="also write a .blend (needs Blender)")
    pe.add_argument("--code", action="store_true", help="also write pseudo-C")
    pe.add_argument("--raw", action="store_true", help="also write decompressed files")
    pe.add_argument("--fast", action="store_true", help="skip the slow zlib/Rare scan")
    a = ap.parse_args(argv)

    def progress(f, m):
        sys.stderr.write(f"\r[{f * 100:5.1f}%] {m[:70]:<70}")
        sys.stderr.flush()

    proj = Project.open(a.rom)
    opts = ScanOptions(deep_compression=not getattr(a, "fast", False))
    proj.scan(progress, opts)
    sys.stderr.write("\n")
    for k, v in proj.rom.info_lines():
        print(f"{k:>20}: {v}")
    for line in proj.log:
        print(" -", line)
    if a.cmd == "export":
        opt = BatchOptions(models=a.models != "none", model_format=a.models if a.models != "none" else "glb",
                           model_scale=a.scale, image_format=a.images, audio=a.audio != "none",
                           audio_format=a.audio if a.audio != "none" else "wav", unity_package=a.unity,
                           blender=a.blender, code=a.code, raw_files=a.raw)
        report = export_all(proj, a.out, opt, progress)
        sys.stderr.write("\n")
        for line in report:
            print(line)
