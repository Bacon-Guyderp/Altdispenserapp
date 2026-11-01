""".blend -> .unitypackage conversion.

1. Blender is run in the background to export the scene as FBX, with every
   texture it uses copied into a Textures folder (see blender_export.py).
2. The FBX and textures (and optionally the original .blend) are packed into
   a .unitypackage (see unitypackage.py).

Can also be used from the command line:
    python converter.py model.blend [more.blend ...] -o OUTPUT_DIR [--blender PATH]
"""

from __future__ import annotations

import argparse
import glob
import os
import platform
import queue
import re
import shutil
import subprocess
import sys
import tempfile
import threading
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, List, Optional

from unitypackage import build_unitypackage

TAG = "@@B2U"

ProgressFn = Callable[[float, str], None]
LogFn = Callable[[str], None]

# Share of a single file's progress bar spent in Blender; the rest is packaging.
BLENDER_SHARE = 0.85


class ConversionError(Exception):
    pass


class ConversionCancelled(ConversionError):
    pass


@dataclass
class ConvertOptions:
    unity_folder: str = "Assets/{name}"   # {name} = .blend file name without extension
    apply_transform: bool = True          # bake axis conversion (avoids -90° X rotation)
    apply_modifiers: bool = True
    include_animation: bool = True
    include_textures: bool = True
    include_blend: bool = False           # also ship the original .blend


def resource_path(name: str) -> Path:
    """Locate a bundled file, also when frozen with PyInstaller."""
    base = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parent))
    return base / name


def _version_key(path: str):
    return [int(n) for n in re.findall(r"\d+", path)]


def find_blender() -> Optional[str]:
    """Best-effort search for a Blender executable."""
    candidates: List[str] = []
    if os.environ.get("BLENDER_PATH"):
        candidates.append(os.environ["BLENDER_PATH"])
    which = shutil.which("blender")
    if which:
        candidates.append(which)

    system = platform.system()
    if system == "Windows":
        bases = [os.environ.get(v) for v in ("ProgramFiles", "ProgramFiles(x86)", "LOCALAPPDATA")]
        for base in filter(None, bases):
            found = glob.glob(os.path.join(base, "Blender Foundation", "Blender*", "blender.exe"))
            candidates += sorted(found, key=_version_key, reverse=True)
            candidates.append(os.path.join(base, "Steam", "steamapps", "common", "Blender", "blender.exe"))
    elif system == "Darwin":
        for apps in ("/Applications", os.path.expanduser("~/Applications")):
            found = glob.glob(os.path.join(apps, "Blender*.app", "Contents", "MacOS", "Blender"))
            candidates += sorted(found, key=_version_key, reverse=True)
    else:
        candidates += ["/snap/bin/blender", "/usr/bin/blender", "/usr/local/bin/blender"]
        for pattern in ("~/blender*/blender", "~/Applications/blender*/blender", "/opt/blender*/blender"):
            found = glob.glob(os.path.expanduser(pattern))
            candidates += sorted(found, key=_version_key, reverse=True)

    for c in candidates:
        if c and os.path.isfile(c):
            return c
    return None


def blender_version(blender: str) -> Optional[str]:
    try:
        out = subprocess.run([blender, "--version"], capture_output=True, text=True,
                             timeout=60, **_popen_kwargs()).stdout
    except (OSError, subprocess.SubprocessError):
        return None
    for line in out.splitlines():
        if line.startswith("Blender "):
            return line.strip()
    return None


def _popen_kwargs() -> dict:
    if platform.system() == "Windows":
        return {"creationflags": getattr(subprocess, "CREATE_NO_WINDOW", 0)}
    return {}


def _safe_unity_name(name: str) -> str:
    return re.sub(r'[<>:"\\|?*\x00-\x1f]', "_", name).strip() or "Model"


def _run_blender(blend: Path, export_dir: Path, name: str, blender: str,
                 options: ConvertOptions, progress: ProgressFn, log: LogFn,
                 cancel: Optional[threading.Event]) -> None:
    cmd = [blender, "-b", "--factory-startup", str(blend),
           "--python-exit-code", "1",
           "--python", str(resource_path("blender_export.py")),
           "--", "--out", str(export_dir), "--name", name]
    if options.apply_transform:
        cmd.append("--apply-transform")
    if options.apply_modifiers:
        cmd.append("--modifiers")
    if options.include_animation:
        cmd.append("--animation")
    if options.include_textures:
        cmd.append("--textures")

    try:
        proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                stdin=subprocess.DEVNULL, text=True, encoding="utf-8",
                                errors="replace", bufsize=1, **_popen_kwargs())
    except OSError as exc:
        raise ConversionError(f"Could not start Blender ({blender}): {exc}") from exc

    lines: "queue.Queue[Optional[str]]" = queue.Queue()

    def reader():
        for line in proc.stdout:
            lines.put(line.rstrip("\r\n"))
        lines.put(None)

    threading.Thread(target=reader, daemon=True).start()

    errors: List[str] = []
    tail: List[str] = []
    progress(0.02, "Starting Blender")
    try:
        _pump_output(proc, lines, errors, tail, progress, log, cancel)
    finally:
        if proc.poll() is None:
            proc.kill()
        code = proc.wait()
        proc.stdout.close()
    if code != 0:
        detail = errors[-1] if errors else (tail[-1] if tail else "no output")
        raise ConversionError(f"Blender failed (exit code {code}): {detail}")


def _pump_output(proc, lines, errors, tail, progress, log, cancel) -> None:
    while True:
        if cancel is not None and cancel.is_set():
            raise ConversionCancelled("Cancelled")
        try:
            line = lines.get(timeout=0.1)
        except queue.Empty:
            continue
        if line is None:
            break
        if line.startswith(TAG + " "):
            _, kind, *rest = line.split(" ", 2)
            text = rest[0] if rest else ""
            if kind == "PROGRESS":
                frac, _, msg = text.partition(" ")
                try:
                    progress(float(frac) * BLENDER_SHARE, msg)
                except ValueError:
                    pass
            elif kind == "ERROR":
                errors.append(text)
                log("ERROR: " + text)
            elif kind == "WARN":
                log("Warning: " + text)
            elif kind in ("INFO", "DONE"):
                log(text if kind == "INFO" else f"Exported {text}")
        elif line.strip():
            tail.append(line)
            del tail[:-15]
            log("  " + line)


def convert_file(blend: Path, output_dir: Optional[Path], blender: str,
                 options: Optional[ConvertOptions] = None,
                 progress: Optional[ProgressFn] = None,
                 log: Optional[LogFn] = None,
                 cancel: Optional[threading.Event] = None) -> Path:
    """Convert one .blend file. Returns the path of the written .unitypackage.

    ``output_dir`` of None writes next to the .blend file.
    """
    options = options or ConvertOptions()
    progress = progress or (lambda f, m: None)
    log = log or (lambda m: None)
    blend = Path(blend).resolve()
    if not blend.is_file():
        raise ConversionError(f"File not found: {blend}")
    if not blender or not os.path.isfile(blender):
        raise ConversionError("Blender executable not found. Set its location first.")

    name = blend.stem
    output_dir = Path(output_dir) if output_dir else blend.parent
    out_path = output_dir / f"{name}.unitypackage"
    unity_folder = options.unity_folder.replace("{name}", _safe_unity_name(name)).strip("/\\") or "Assets"

    with tempfile.TemporaryDirectory(prefix="blend2unity_") as tmp:
        export_dir = Path(tmp) / "export"
        export_dir.mkdir()
        _run_blender(blend, export_dir, name, blender, options, progress, log, cancel)

        if cancel is not None and cancel.is_set():
            raise ConversionCancelled("Cancelled")
        progress(BLENDER_SHARE, "Building .unitypackage")

        assets = []
        for f in sorted(export_dir.rglob("*")):
            if f.is_file():
                rel = f.relative_to(export_dir).as_posix()
                assets.append((f, f"{unity_folder}/{rel}"))
        if options.include_blend:
            assets.append((blend, f"{unity_folder}/{blend.name}"))
        if not any(dst.endswith(".fbx") for _, dst in assets):
            raise ConversionError("Blender did not produce an FBX file")

        build_unitypackage(
            out_path, assets,
            progress=lambda f: progress(BLENDER_SHARE + (1 - BLENDER_SHARE) * f, "Building .unitypackage"),
        )

    progress(1.0, "Done")
    log(f"Wrote {out_path} ({len(assets)} asset(s))")
    return out_path


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description="Convert .blend files to .unitypackage files.")
    p.add_argument("files", nargs="+", type=Path)
    p.add_argument("-o", "--output", type=Path, help="Output folder (default: next to each .blend)")
    p.add_argument("--blender", help="Path to the Blender executable")
    p.add_argument("--unity-folder", default="Assets/{name}")
    p.add_argument("--no-apply-transform", action="store_true")
    p.add_argument("--no-modifiers", action="store_true")
    p.add_argument("--no-animation", action="store_true")
    p.add_argument("--no-textures", action="store_true")
    p.add_argument("--include-blend", action="store_true")
    p.add_argument("-v", "--verbose", action="store_true")
    args = p.parse_args(argv)

    blender = args.blender or find_blender()
    if not blender:
        p.error("Blender not found; pass --blender or set BLENDER_PATH")
    opts = ConvertOptions(
        unity_folder=args.unity_folder,
        apply_transform=not args.no_apply_transform,
        apply_modifiers=not args.no_modifiers,
        include_animation=not args.no_animation,
        include_textures=not args.no_textures,
        include_blend=args.include_blend,
    )

    failures = 0
    for i, f in enumerate(args.files, 1):
        def show(frac, msg, _i=i):
            bar = "#" * int(frac * 30)
            sys.stdout.write(f"\r[{_i}/{len(args.files)}] [{bar:<30}] {frac:4.0%} {msg[:40]:<40}")
            sys.stdout.flush()

        def log(msg):
            if args.verbose or not msg.startswith("  "):
                sys.stdout.write("\r" + " " * 100 + "\r" + msg + "\n")

        try:
            convert_file(f, args.output, blender, opts, show, log)
            print()
        except ConversionError as exc:
            failures += 1
            print(f"\nFailed: {f}: {exc}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
