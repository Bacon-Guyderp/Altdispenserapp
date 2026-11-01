"""Unity .unitypackage writer.

A .unitypackage is a gzipped tar where every asset lives in a folder named
after its GUID containing ``asset``, ``asset.meta`` and ``pathname``.
"""
from __future__ import annotations

import hashlib
import io
import os
import tarfile
import time


def _guid(path: str) -> str:
    return hashlib.md5(("n64studio:" + path).encode()).hexdigest()


def _meta(guid: str, folder: bool, ext: str) -> bytes:
    lines = ["fileFormatVersion: 2", f"guid: {guid}"]
    if folder:
        lines += ["folderAsset: yes", "DefaultImporter:", "  externalObjects: {}", "  userData: ",
                  "  assetBundleName: ", "  assetBundleVariant: "]
    elif ext in (".png", ".tga", ".bmp", ".tif", ".jpg"):
        lines += ["TextureImporter:", "  mipmaps:", "    enableMipMap: 0", "  textureSettings:",
                  "    filterMode: 0", "  alphaIsTransparency: 1", "  textureType: 0",
                  "  userData: ", "  assetBundleName: ", "  assetBundleVariant: "]
    elif ext in (".fbx", ".obj", ".dae"):
        lines += ["ModelImporter:", "  materials:", "    materialImportMode: 1",
                  "    materialLocation: 1", "  userData: ", "  assetBundleName: ", "  assetBundleVariant: "]
    elif ext in (".wav", ".ogg", ".aiff", ".mp3"):
        lines += ["AudioImporter:", "  userData: ", "  assetBundleName: ", "  assetBundleVariant: "]
    else:
        lines += ["DefaultImporter:", "  externalObjects: {}", "  userData: ", "  assetBundleName: ",
                  "  assetBundleVariant: "]
    return ("\n".join(lines) + "\n").encode()


class UnityPackage:
    def __init__(self, root: str = "Assets/N64"):
        self.root = root.rstrip("/")
        self.files: dict[str, bytes] = {}

    def add_bytes(self, rel_path: str, data: bytes) -> None:
        self.files[f"{self.root}/{rel_path}".replace("\\", "/")] = data

    def add_file(self, rel_path: str, src: str) -> None:
        with open(src, "rb") as f:
            self.add_bytes(rel_path, f.read())

    def add_folder_tree(self, rel_dir: str, src_dir: str) -> None:
        for dirpath, _dirs, files in os.walk(src_dir):
            for fn in files:
                full = os.path.join(dirpath, fn)
                rel = os.path.relpath(full, src_dir)
                self.add_file(f"{rel_dir}/{rel}", full)

    def save(self, path: str) -> str:
        folders = set()
        for p in self.files:
            parts = p.split("/")
            for i in range(1, len(parts)):
                folders.add("/".join(parts[:i]))
        folders.discard("Assets")
        now = time.time()
        with tarfile.open(path, "w:gz") as tar:
            def add(name: str, data: bytes) -> None:
                ti = tarfile.TarInfo(name)
                ti.size = len(data)
                ti.mtime = now
                tar.addfile(ti, io.BytesIO(data))

            for folder in sorted(folders):
                g = _guid(folder)
                add(f"{g}/asset.meta", _meta(g, True, ""))
                add(f"{g}/pathname", folder.encode())
            for p, data in sorted(self.files.items()):
                g = _guid(p)
                add(f"{g}/asset", data)
                add(f"{g}/asset.meta", _meta(g, False, os.path.splitext(p)[1].lower()))
                add(f"{g}/pathname", p.encode())
        return path
