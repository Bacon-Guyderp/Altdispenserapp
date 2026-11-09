"""Pack Assets/XenoDial for Unity, two ways (no Unity needed):

  XenoDial.unitypackage   Assets > Import Package > Custom Package
  XenoDial-folder.zip     fallback: unzip into the Unity project folder (next to Assets/)

A .unitypackage is a gzipped tar with one folder per asset, named by the asset's
GUID, holding `asset` (the file), `asset.meta` and `pathname`. This writer mirrors
Unity's own exporter: a directory entry per GUID, plain ustar headers, and a
gzip stream without an embedded file name. GUIDs come from each path, so
re-importing a newer package updates files in place instead of duplicating them.
The zip carries the same .meta files, so both routes give identical GUIDs.

  python make_unitypackage.py
"""
import argparse, gzip, hashlib, io, os, tarfile, time, zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
SKIP = {".keep", ".DS_Store"}


def guid(path):
    return hashlib.md5(("xenodial:" + path).encode("utf-8")).hexdigest()


def meta(path, g, is_dir):
    head = f"fileFormatVersion: 2\nguid: {g}\n"
    tail = "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    if is_dir:
        return head + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n" + tail
    ext = os.path.splitext(path)[1].lower()
    if ext == ".cs":
        return head + ("MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
                       "  executionOrder: 0\n  icon: {instanceID: 0}\n") + tail
    if ext == ".shader":
        return head + "ShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n" + tail
    if ext in (".json", ".txt", ".md"):
        return head + "TextScriptImporter:\n  externalObjects: {}\n" + tail
    if ext in (".png", ".jpg", ".wav"):
        # Unity fills in importer defaults for images and sounds.
        return head
    return head + "DefaultImporter:\n  externalObjects: {}\n" + tail


def collect():
    root = os.path.join(PROJECT, "Assets", "XenoDial")
    entries = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if not d.startswith("."))
        rel_dir = os.path.relpath(dirpath, PROJECT).replace(os.sep, "/")
        entries.append((rel_dir, None))
        for f in sorted(filenames):
            if f in SKIP or f.endswith(".meta"):
                continue
            entries.append((rel_dir + "/" + f, os.path.join(dirpath, f)))
    return entries


def write_unitypackage(out, entries):
    mtime = int(time.time())

    def add(tar, name, data=None):
        info = tarfile.TarInfo(name)
        info.mtime = mtime
        info.uname = info.gname = ""
        if data is None:
            info.type = tarfile.DIRTYPE
            info.mode = 0o755
            tar.addfile(info)
        else:
            info.size = len(data)
            info.mode = 0o644
            tar.addfile(info, io.BytesIO(data))

    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode="w", format=tarfile.USTAR_FORMAT) as tar:
        for path, src in entries:
            g = guid(path)
            add(tar, g)                                   # the GUID folder itself, like Unity's exporter
            if src:
                with open(src, "rb") as fh:
                    add(tar, f"{g}/asset", fh.read())
            add(tar, f"{g}/asset.meta", meta(path, g, src is None).encode("utf-8"))
            add(tar, f"{g}/pathname", path.encode("utf-8"))
    with open(out, "wb") as f, gzip.GzipFile(filename="", mode="wb", fileobj=f, mtime=mtime) as gz:
        gz.write(buf.getvalue())


def write_zip(out, entries):
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for path, src in entries:
            g = guid(path)
            z.writestr(path + ".meta", meta(path, g, src is None))
            if src:
                z.write(src, path)
            else:
                z.writestr(path + "/", "")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(PROJECT, "XenoDial.unitypackage"))
    ap.add_argument("--zip", default=None, help="also write the folder zip here (default: next to --out)")
    a = ap.parse_args()
    entries = collect()
    write_unitypackage(a.out, entries)
    zip_out = a.zip or os.path.join(os.path.dirname(os.path.abspath(a.out)), "XenoDial-folder.zip")
    write_zip(zip_out, entries)
    files = sum(1 for _, s in entries if s)
    print(f"wrote {a.out} ({os.path.getsize(a.out) / 1e6:.1f} MB) and {zip_out} "
          f"({os.path.getsize(zip_out) / 1e6:.1f} MB): {files} files")


if __name__ == "__main__":
    main()
