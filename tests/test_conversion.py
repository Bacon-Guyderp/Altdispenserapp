"""Tests. Blender-dependent tests run only when BLENDER_PATH points to Blender.

    python -m unittest discover tests
"""
import os
import subprocess
import sys
import tarfile
import tempfile
import threading
import time
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from converter import ConversionCancelled, ConversionError, convert_file  # noqa: E402
from unitypackage import build_unitypackage, guid_for, read_unitypackage  # noqa: E402

BLENDER = os.environ.get("BLENDER_PATH")

MAKE_SCENE = r"""
import bpy, sys
out = sys.argv[sys.argv.index("--") + 1]
bpy.ops.mesh.primitive_monkey_add()
ob = bpy.context.active_object
mat = bpy.data.materials.new("Mat"); mat.use_nodes = True
img = bpy.data.images.new("Painted", 16, 16)
node = mat.node_tree.nodes.new("ShaderNodeTexImage"); node.image = img
ob.data.materials.append(mat)
bpy.ops.wm.save_as_mainfile(filepath=out)
"""


class UnityPackageTests(unittest.TestCase):
    def test_roundtrip_and_folders(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            a = tmp / "a.fbx"; a.write_bytes(b"fbx")
            b = tmp / "b.png"; b.write_bytes(b"png")
            out = build_unitypackage(tmp / "x.unitypackage",
                                     [(a, "Assets/M/a.fbx"), (b, "M/Textures/b.png")])
            contents = read_unitypackage(out)
            self.assertEqual(contents, {
                "Assets/M": None,
                "Assets/M/Textures": None,
                "Assets/M/a.fbx": b"fbx",
                "Assets/M/Textures/b.png": b"png",
            })
            with tarfile.open(out, "r:gz") as tar:
                guid = guid_for("Assets/M/a.fbx")
                meta = tar.extractfile(f"{guid}/asset.meta").read().decode()
                self.assertIn(f"guid: {guid}", meta)
                folder_meta = tar.extractfile(f"{guid_for('Assets/M')}/asset.meta").read().decode()
                self.assertIn("folderAsset: yes", folder_meta)

    def test_guid_is_stable_hex(self):
        g = guid_for("Assets/x.fbx")
        self.assertEqual(g, guid_for("Assets/x.fbx"))
        self.assertRegex(g, r"^[0-9a-f]{32}$")

    def test_rejects_parent_paths(self):
        with tempfile.TemporaryDirectory() as tmp:
            f = Path(tmp) / "a"; f.write_bytes(b"")
            with self.assertRaises(ValueError):
                build_unitypackage(Path(tmp) / "x.unitypackage", [(f, "Assets/../a")])


@unittest.skipUnless(BLENDER and os.path.isfile(BLENDER), "set BLENDER_PATH to run Blender tests")
class BlenderTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.dir = Path(self.tmp.name)
        self.blend = self.dir / "Test Model.blend"
        script = self.dir / "make.py"
        script.write_text(MAKE_SCENE)
        subprocess.run([BLENDER, "-b", "--factory-startup", "--python", str(script),
                        "--", str(self.blend)], check=True, capture_output=True)

    def tearDown(self):
        self.tmp.cleanup()

    def test_convert(self):
        seen = []
        out = convert_file(self.blend, self.dir / "out", BLENDER,
                           progress=lambda f, m: seen.append(f))
        contents = read_unitypackage(out)
        self.assertIn("Assets/Test Model/Test Model.fbx", contents)
        self.assertIn("Assets/Test Model/Textures/Painted.png", contents)
        self.assertEqual(seen, sorted(seen))
        self.assertEqual(seen[-1], 1.0)

    def test_bad_file(self):
        bad = self.dir / "bad.blend"
        bad.write_bytes(b"not a blend file")
        with self.assertRaises(ConversionError):
            convert_file(bad, self.dir / "out", BLENDER)

    def test_cancel(self):
        cancel = threading.Event()
        threading.Timer(0.2, cancel.set).start()
        start = time.time()
        with self.assertRaises(ConversionCancelled):
            convert_file(self.blend, self.dir / "out", BLENDER, cancel=cancel)
        self.assertLess(time.time() - start, 5)
        self.assertFalse((self.dir / "out" / "Test Model.unitypackage").exists())


if __name__ == "__main__":
    unittest.main()
