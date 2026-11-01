"""Writer for Unity's ``.unitypackage`` format.

A ``.unitypackage`` is a gzip-compressed tar archive. Each asset (file or
folder) lives in a directory named after its GUID::

    <guid>/pathname     text file holding the project path, e.g. "Assets/Foo/bar.fbx"
    <guid>/asset        the file's raw bytes (missing for folders)
    <guid>/asset.meta   the Unity .meta file (YAML) holding the same GUID

GUIDs are derived from the project path, so importing a new version of the
same package updates the existing assets instead of duplicating them.
"""

from __future__ import annotations

import hashlib
import io
import tarfile
import time
from pathlib import Path, PurePosixPath
from typing import Callable, Iterable, Optional, Tuple

FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

# Unity fills in default importer settings for anything not given here.
FILE_META = """fileFormatVersion: 2
guid: {guid}
"""


def guid_for(unity_path: str) -> str:
    """Return a stable 32-character hex GUID for a Unity project path."""
    return hashlib.md5(("blend2unity:" + unity_path).encode("utf-8")).hexdigest()


def _normalize(unity_path: str) -> str:
    path = PurePosixPath(unity_path.replace("\\", "/"))
    parts = [p for p in path.parts if p not in ("", ".", "/")]
    if ".." in parts:
        raise ValueError(f"Unity path may not contain '..': {unity_path}")
    if not parts or parts[0] != "Assets":
        parts.insert(0, "Assets")
    return "/".join(parts)


def _add_bytes(tar: tarfile.TarFile, name: str, data: bytes, mtime: float) -> None:
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mtime = mtime
    info.mode = 0o644
    tar.addfile(info, io.BytesIO(data))


def _add_dir(tar: tarfile.TarFile, name: str, mtime: float) -> None:
    info = tarfile.TarInfo(name)
    info.type = tarfile.DIRTYPE
    info.mtime = mtime
    info.mode = 0o755
    tar.addfile(info)


def build_unitypackage(
    output: Path,
    assets: Iterable[Tuple[Path, str]],
    progress: Optional[Callable[[float], None]] = None,
) -> Path:
    """Write a .unitypackage.

    ``assets`` is a list of (source file on disk, Unity project path) pairs.
    Every parent folder of each project path is added to the package too.
    ``progress`` (if given) is called with a 0..1 fraction as files are written.
    """
    files = [(Path(src), _normalize(dst)) for src, dst in assets]
    if not files:
        raise ValueError("No assets to package")

    folders = set()
    for _, dst in files:
        parent = PurePosixPath(dst).parent
        while str(parent) not in ("Assets", "."):
            folders.add(str(parent))
            parent = parent.parent

    total_bytes = sum(src.stat().st_size for src, _ in files) or 1
    written = 0
    mtime = time.time()
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    tmp = output.with_name(output.name + ".part")

    with tarfile.open(tmp, "w:gz", format=tarfile.GNU_FORMAT) as tar:
        for folder in sorted(folders):
            guid = guid_for(folder)
            _add_dir(tar, guid, mtime)
            _add_bytes(tar, f"{guid}/pathname", folder.encode("utf-8"), mtime)
            _add_bytes(tar, f"{guid}/asset.meta", FOLDER_META.format(guid=guid).encode(), mtime)

        for src, dst in files:
            guid = guid_for(dst)
            _add_dir(tar, guid, mtime)
            _add_bytes(tar, f"{guid}/pathname", dst.encode("utf-8"), mtime)
            _add_bytes(tar, f"{guid}/asset.meta", FILE_META.format(guid=guid).encode(), mtime)
            info = tarfile.TarInfo(f"{guid}/asset")
            info.size = src.stat().st_size
            info.mtime = mtime
            info.mode = 0o644
            with open(src, "rb") as fh:
                tar.addfile(info, fh)
            written += info.size
            if progress:
                progress(written / total_bytes)

    tmp.replace(output)
    return output


def read_unitypackage(path: Path) -> dict:
    """Return {unity_path: bytes or None (folder)} for a .unitypackage. Used by tests."""
    result = {}
    with tarfile.open(path, "r:gz") as tar:
        names = {m.name for m in tar.getmembers()}
        guids = {n.split("/")[0] for n in names}
        for guid in guids:
            if f"{guid}/pathname" not in names:
                continue
            pathname = tar.extractfile(f"{guid}/pathname").read().decode("utf-8").splitlines()[0]
            asset = None
            if f"{guid}/asset" in names:
                asset = tar.extractfile(f"{guid}/asset").read()
            result[pathname] = asset
    return result
