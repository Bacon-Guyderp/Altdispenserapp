"""Pack Assets/GeoQuest into GeoQuestNC.unitypackage (no Unity needed).

A .unitypackage is a gzipped tar with one folder per asset, named by the asset's
GUID, holding `asset` (the file), `asset.meta` and `pathname`. GUIDs are derived
from each path, so re-importing a newer package updates files in place instead of
duplicating them.

  python make_unitypackage.py            -> ../GeoQuestNC.unitypackage
"""
import argparse, hashlib, io, os, tarfile, time

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
SKIP = {".keep", ".DS_Store"}


def guid(path):
    return hashlib.md5(("geoquest-nc:" + path).encode("utf-8")).hexdigest()


def meta(path, g, is_dir):
    head = f"fileFormatVersion: 2\nguid: {g}\n"
    if is_dir:
        return head + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    ext = os.path.splitext(path)[1].lower()
    if ext == ".cs":
        return head + ("MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
                       "  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    if ext == ".shader":
        return head + ("ShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n"
                       "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    if ext in (".json", ".txt", ".md"):
        return head + "TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    # Textures: Unity fills in importer defaults; GeoQuest > Build World then sets size,
    # wrap mode and Quest (ASTC) compression itself.
    return head


def add_bytes(tar, name, data, mtime):
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mtime = mtime
    info.mode = 0o644
    tar.addfile(info, io.BytesIO(data))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(PROJECT, "GeoQuestNC.unitypackage"))
    a = ap.parse_args()
    root = os.path.join(PROJECT, "Assets", "GeoQuest")
    entries = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if not d.startswith("."))
        rel_dir = os.path.relpath(dirpath, PROJECT).replace(os.sep, "/")
        entries.append((rel_dir, None))
        for f in sorted(filenames):
            if f in SKIP or f.endswith(".meta"):
                continue
            entries.append((rel_dir + "/" + f, os.path.join(dirpath, f)))
    mtime = int(time.time())
    with tarfile.open(a.out, "w:gz") as tar:
        for path, src in entries:
            g = guid(path)
            add_bytes(tar, f"{g}/pathname", path.encode("utf-8"), mtime)
            add_bytes(tar, f"{g}/asset.meta", meta(path, g, src is None).encode("utf-8"), mtime)
            if src:
                with open(src, "rb") as fh:
                    add_bytes(tar, f"{g}/asset", fh.read(), mtime)
    files = sum(1 for _, s in entries if s)
    print(f"wrote {a.out}: {files} files, {os.path.getsize(a.out) / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
