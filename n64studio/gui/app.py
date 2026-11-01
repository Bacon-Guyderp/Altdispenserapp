"""N64 Asset Studio - Tkinter GUI (Windows-first, also runs on Linux/macOS)."""
from __future__ import annotations

import json
import os
import queue
import subprocess
import sys
import tempfile
import threading
import tkinter as tk
import traceback
from tkinter import filedialog, messagebox, ttk

import numpy as np
from PIL import Image, ImageTk

from .. import APP_NAME, __version__, mips
from ..audio import decode_raw_pcm
from ..export import blender as blender_export
from ..export import sounds as sound_export
from ..export.batch import BatchOptions, export_all
from ..export.images import IMAGE_FORMATS, save_image
from ..export.models import MODEL_FORMATS, export_model, safe
from ..export.soundfont import bank_file_to_sf2
from ..export.unity import UnityPackage
from ..project import ModelAsset, Project, ScanOptions, SoundRef, TextureAsset
from ..rom import ROM_EXTENSIONS, save_rom
from ..sequence import Sequence
from ..texture import TEXTURE_FORMATS, decode_palette, decode_texture, texture_byte_size
from .render import MeshView, checkerboard, waveform

IS_WINDOWS = os.name == "nt"


# ---------------------------------------------------------------- settings
def _settings_path() -> str:
    base = os.environ.get("APPDATA") or os.path.expanduser("~")
    folder = os.path.join(base, "N64AssetStudio")
    os.makedirs(folder, exist_ok=True)
    return os.path.join(folder, "settings.json")


DEFAULT_SETTINGS = {
    "blender_path": "", "ffmpeg_path": "", "model_scale": 1.0, "deep_compression": True,
    "scan_rom_display_lists": True, "texture_budget": 90, "show_child_dls": False,
    "last_dir": "", "export_dir": "", "recent": [],
}


def load_settings() -> dict:
    s = dict(DEFAULT_SETTINGS)
    try:
        with open(_settings_path(), encoding="utf-8") as f:
            s.update(json.load(f))
    except (OSError, ValueError):
        pass
    return s


def save_settings(s: dict) -> None:
    try:
        with open(_settings_path(), "w", encoding="utf-8") as f:
            json.dump(s, f, indent=1)
    except OSError:
        pass


# ------------------------------------------------------------- audio play
class Player:
    def __init__(self):
        self.proc = None
        self.tmp = os.path.join(tempfile.gettempdir(), "n64studio_preview.wav")

    def play(self, pcm: np.ndarray, rate: int) -> None:
        self.stop()
        with open(self.tmp, "wb") as f:
            f.write(sound_export.wav_bytes(pcm, rate))
        if IS_WINDOWS:
            import winsound
            winsound.PlaySound(self.tmp, winsound.SND_FILENAME | winsound.SND_ASYNC)
            return
        for cmd in (["afplay", self.tmp], ["aplay", "-q", self.tmp], ["paplay", self.tmp],
                    ["ffplay", "-nodisp", "-autoexit", "-loglevel", "quiet", self.tmp]):
            try:
                self.proc = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                return
            except OSError:
                continue

    def stop(self) -> None:
        if IS_WINDOWS:
            import winsound
            winsound.PlaySound(None, 0)
        if self.proc and self.proc.poll() is None:
            self.proc.terminate()
        self.proc = None


def open_with_default_app(path: str) -> None:
    if IS_WINDOWS:
        os.startfile(path)  # type: ignore[attr-defined]
    elif sys.platform == "darwin":
        subprocess.Popen(["open", path])
    else:
        subprocess.Popen(["xdg-open", path])


def parse_int(text: str) -> int:
    text = text.strip().lower()
    if text.startswith("0x"):
        return int(text, 16)
    if any(c in "abcdef" for c in text):
        return int(text, 16)
    return int(text)


# ================================================================== App
class App(tk.Tk):
    def __init__(self, path: str | None = None):
        super().__init__()
        self.settings = load_settings()
        blender_export.set_blender_path(self.settings.get("blender_path"))
        sound_export.set_ffmpeg_path(self.settings.get("ffmpeg_path"))
        self.title(f"{APP_NAME} {__version__}")
        self.geometry("1280x800")
        self.minsize(900, 560)
        self._set_theme()
        self._set_icon()
        self.project: Project | None = None
        self.items: dict[str, tuple[str, object]] = {}
        self.lazy: dict[str, callable] = {}
        self.player = Player()
        self.queue: queue.Queue = queue.Queue()
        self.busy = False
        self.current = None
        self.mesh_view: MeshView | None = None
        self._photo = None
        self._drag = None

        self._build_menu()
        self._build_toolbar()
        self._build_body()
        self._build_status()
        self.bind_all("<Control-o>", lambda e: self.open_dialog())
        self.bind_all("<Control-e>", lambda e: self.export_selected())
        self.bind_all("<Control-E>", lambda e: self.export_all_dialog())
        self.protocol("WM_DELETE_WINDOW", self.on_close)
        self.after(50, self._poll_queue)
        self.show_welcome()
        if path:
            self.after(200, lambda: self.open_rom(path))

    # ------------------------------------------------------------ layout
    def _set_theme(self) -> None:
        style = ttk.Style(self)
        for theme in ("vista", "winnative", "clam"):
            if theme in style.theme_names():
                style.theme_use(theme)
                break
        style.configure("Treeview", rowheight=22)
        style.configure("Header.TLabel", font=("Segoe UI", 12, "bold"))
        style.configure("Mono.TLabel", font=("Consolas", 10))

    def _set_icon(self) -> None:
        base = getattr(sys, "_MEIPASS", os.path.join(os.path.dirname(__file__), "..", ".."))
        try:
            if IS_WINDOWS and os.path.isfile(os.path.join(base, "assets", "icon.ico")):
                self.iconbitmap(default=os.path.join(base, "assets", "icon.ico"))
            elif os.path.isfile(os.path.join(base, "assets", "icon.png")):
                self._icon = tk.PhotoImage(file=os.path.join(base, "assets", "icon.png"))
                self.iconphoto(True, self._icon)
        except tk.TclError:
            pass

    def _build_menu(self) -> None:
        mb = tk.Menu(self)
        fm = tk.Menu(mb, tearoff=0)
        fm.add_command(label="Open ROM...", accelerator="Ctrl+O", command=self.open_dialog)
        self.recent_menu = tk.Menu(fm, tearoff=0)
        fm.add_cascade(label="Recent ROMs", menu=self.recent_menu)
        fm.add_separator()
        fm.add_command(label="Save ROM as .z64 (big endian)...", command=lambda: self.save_rom_as("z64"))
        fm.add_command(label="Save ROM as .v64 (byte-swapped)...", command=lambda: self.save_rom_as("v64"))
        fm.add_command(label="Save ROM as .n64 (little endian)...", command=lambda: self.save_rom_as("n64"))
        fm.add_separator()
        fm.add_command(label="Exit", command=self.on_close)
        mb.add_cascade(label="File", menu=fm)

        em = tk.Menu(mb, tearoff=0)
        em.add_command(label="Export Selected...", accelerator="Ctrl+E", command=self.export_selected)
        em.add_command(label="Export All Assets...", accelerator="Ctrl+Shift+E", command=self.export_all_dialog)
        em.add_separator()
        em.add_command(label="Export Everything as Unity Package...", command=lambda: self.export_all_dialog("unity"))
        em.add_command(label="Export All Models to Blender (.blend)...", command=lambda: self.export_all_dialog("blender"))
        mb.add_cascade(label="Export", menu=em)

        tm = tk.Menu(mb, tearoff=0)
        tm.add_command(label="Texture Ripper", command=lambda: self.nb.select(self.tab_ripper))
        tm.add_command(label="Raw Audio Explorer", command=lambda: self.nb.select(self.tab_rawaudio))
        tm.add_command(label="Verify ROM checksum", command=self.verify_crc)
        tm.add_separator()
        tm.add_command(label="Settings...", command=self.settings_dialog)
        mb.add_cascade(label="Tools", menu=tm)

        hm = tk.Menu(mb, tearoff=0)
        hm.add_command(label="How it works", command=self.show_help)
        hm.add_command(label="About", command=self.show_about)
        mb.add_cascade(label="Help", menu=hm)
        self.config(menu=mb)
        self._refresh_recent()

    def _build_toolbar(self) -> None:
        bar = ttk.Frame(self, padding=(6, 4))
        bar.pack(side=tk.TOP, fill=tk.X)
        ttk.Button(bar, text="Open ROM", command=self.open_dialog).pack(side=tk.LEFT)
        ttk.Button(bar, text="Export Selected", command=self.export_selected).pack(side=tk.LEFT, padx=4)
        ttk.Button(bar, text="Export All...", command=self.export_all_dialog).pack(side=tk.LEFT)
        ttk.Separator(bar, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=8)
        ttk.Label(bar, text="Filter:").pack(side=tk.LEFT)
        self.filter_var = tk.StringVar()
        ent = ttk.Entry(bar, textvariable=self.filter_var, width=28)
        ent.pack(side=tk.LEFT, padx=4)
        ent.bind("<Return>", lambda e: self.populate_tree())
        ttk.Button(bar, text="Apply", command=self.populate_tree).pack(side=tk.LEFT)
        self.child_var = tk.BooleanVar(value=bool(self.settings.get("show_child_dls")))
        ttk.Checkbutton(bar, text="Show sub-meshes", variable=self.child_var,
                        command=self._toggle_children).pack(side=tk.LEFT, padx=8)

    def _build_body(self) -> None:
        pw = ttk.PanedWindow(self, orient=tk.HORIZONTAL)
        pw.pack(fill=tk.BOTH, expand=True)
        left = ttk.Frame(pw, width=360)
        self.tree = ttk.Treeview(left, show="tree", selectmode="browse")
        self.tree.column("#0", width=340, minwidth=200, stretch=True)
        ys = ttk.Scrollbar(left, orient=tk.VERTICAL, command=self.tree.yview)
        self.tree.configure(yscrollcommand=ys.set)
        self.tree.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        ys.pack(side=tk.RIGHT, fill=tk.Y)
        self.tree.bind("<<TreeviewSelect>>", self.on_select)
        self.tree.bind("<<TreeviewOpen>>", self.on_open_node)
        self.tree.bind("<Double-1>", lambda e: self.export_selected())
        pw.add(left, weight=1)

        right = ttk.Frame(pw)
        self.nb = ttk.Notebook(right)
        self.nb.pack(fill=tk.BOTH, expand=True)
        self.tab_preview = ttk.Frame(self.nb)
        self.tab_hex = ttk.Frame(self.nb)
        self.tab_ripper = ttk.Frame(self.nb)
        self.tab_rawaudio = ttk.Frame(self.nb)
        self.nb.add(self.tab_preview, text="Preview")
        self.nb.add(self.tab_hex, text="Hex")
        self.nb.add(self.tab_ripper, text="Texture Ripper")
        self.nb.add(self.tab_rawaudio, text="Raw Audio")
        pw.add(right, weight=3)
        self.after(100, lambda: pw.sashpos(0, 360))
        self._build_hex_tab()
        self._build_ripper_tab()
        self._build_rawaudio_tab()

    def _build_status(self) -> None:
        bar = ttk.Frame(self, padding=(6, 2))
        bar.pack(side=tk.BOTTOM, fill=tk.X)
        self.status = tk.StringVar(value="Open a Nintendo 64 ROM to begin.")
        ttk.Label(bar, textvariable=self.status).pack(side=tk.LEFT, fill=tk.X, expand=True)
        self.progress = ttk.Progressbar(bar, length=260, maximum=1.0)
        self.progress.pack(side=tk.RIGHT)

    def clear_preview(self) -> ttk.Frame:
        self.player.stop()
        for w in self.tab_preview.winfo_children():
            w.destroy()
        self.mesh_view = None
        f = ttk.Frame(self.tab_preview, padding=8)
        f.pack(fill=tk.BOTH, expand=True)
        return f

    # ------------------------------------------------------------- misc
    def show_welcome(self) -> None:
        f = self.clear_preview()
        ttk.Label(f, text=APP_NAME, style="Header.TLabel").pack(anchor=tk.W)
        ttk.Label(f, justify=tk.LEFT, wraplength=760, text=(
            "Open any Nintendo 64 ROM (.z64 .v64 .n64 .bin .rom .u64 .d64). The ROM is normalised to "
            "big endian, its compressed files (Yaz0, Yay0, MIO0, zlib/gzip, Rare deflate) are unpacked, "
            "and every file is scanned for:\n\n"
            "  - 3D models: F3D / F3DEX / F3DEX2 display lists, with textures, UVs, vertex colours\n"
            "  - Textures: RGBA16/32, CI4/8 (with palettes), IA4/8/16, I4/8 - plus a manual Texture Ripper\n"
            "  - Sound: libultra sound banks (VADPCM / raw PCM samples), SoundFont export\n"
            "  - Music: MIDI and compressed-MIDI sequences converted to standard .mid\n"
            "  - Code: MIPS R4300i disassembly and pseudo-C of every function\n\n"
            "Export to glTF/GLB, FBX, OBJ, DAE, PLY, STL, Blender (.blend), Unity (.unitypackage), "
            "PNG/BMP/TGA/TIFF/DDS/..., WAV/AIFF/FLAC (+ OGG/MP3/... with ffmpeg), SF2 and MIDI.\n\n"
            "Tip: you can drag a ROM onto the program's .exe to open it.")).pack(anchor=tk.W, pady=8)

    def set_status(self, text: str, frac: float | None = None) -> None:
        self.status.set(text)
        if frac is not None:
            self.progress["value"] = frac

    def _poll_queue(self) -> None:
        try:
            while True:
                kind, payload = self.queue.get_nowait()
                if kind == "progress":
                    frac, msg = payload
                    self.set_status(msg, frac)
                elif kind == "call":
                    payload()
        except queue.Empty:
            pass
        self.after(50, self._poll_queue)

    def run_bg(self, work, done=None, title="Working") -> None:
        """Run ``work(progress)`` on a worker thread; ``done(result)`` back on the UI thread."""
        if self.busy:
            messagebox.showinfo(APP_NAME, "Please wait for the current task to finish.")
            return
        self.busy = True
        self.config(cursor="watch")

        def progress(frac, msg):
            self.queue.put(("progress", (frac, msg)))

        def runner():
            try:
                res = work(progress)
                err = None
            except Exception as e:  # noqa: BLE001
                res, err = None, e
                traceback.print_exc()

            def finish():
                self.busy = False
                self.config(cursor="")
                if err is not None:
                    self.set_status(f"{title} failed: {err}", 0)
                    messagebox.showerror(APP_NAME, f"{title} failed:\n{err}")
                elif done:
                    done(res)
            self.queue.put(("call", finish))

        threading.Thread(target=runner, daemon=True).start()

    # ------------------------------------------------------------ files
    def open_dialog(self) -> None:
        pats = " ".join("*" + e for e in ROM_EXTENSIONS)
        path = filedialog.askopenfilename(
            title="Open Nintendo 64 ROM", initialdir=self.settings.get("last_dir") or None,
            filetypes=[("N64 ROMs", pats), ("All files", "*.*")])
        if path:
            self.open_rom(path)

    def _refresh_recent(self) -> None:
        self.recent_menu.delete(0, tk.END)
        for p in self.settings.get("recent", [])[:10]:
            self.recent_menu.add_command(label=p, command=lambda p=p: self.open_rom(p))
        if not self.settings.get("recent"):
            self.recent_menu.add_command(label="(none)", state=tk.DISABLED)

    def open_rom(self, path: str) -> None:
        if not os.path.isfile(path):
            messagebox.showerror(APP_NAME, f"File not found:\n{path}")
            return
        self.settings["last_dir"] = os.path.dirname(path)
        rec = [p for p in self.settings.get("recent", []) if p != path]
        self.settings["recent"] = [path] + rec[:9]
        save_settings(self.settings)
        self._refresh_recent()
        opts = ScanOptions(deep_compression=bool(self.settings["deep_compression"]),
                           scan_rom_display_lists=bool(self.settings["scan_rom_display_lists"]),
                           texture_time_budget=float(self.settings["texture_budget"]))

        def work(progress):
            proj = Project.open(path)
            proj.scan(progress, opts)
            return proj

        def done(proj):
            self.project = proj
            self.title(f"{APP_NAME} - {proj.rom.display_title} [{proj.rom.name}]")
            self.populate_tree()
            self.set_status(f"Loaded {proj.rom.name}: " + (proj.log[-1] if proj.log else ""), 1.0)
            self.ripper_refresh_blobs()
            first = self.tree.get_children()
            if first:
                self.tree.selection_set(first[0])
        self.tree.delete(*self.tree.get_children())
        self.show_welcome()
        self.run_bg(work, done, "Loading ROM")

    def save_rom_as(self, order: str) -> None:
        if not self.project:
            return
        base = os.path.splitext(self.project.rom.name)[0]
        path = filedialog.asksaveasfilename(defaultextension="." + order, initialfile=base + "." + order,
                                            filetypes=[(f".{order} ROM", "*." + order)])
        if path:
            save_rom(self.project.rom, path, order)
            self.set_status(f"Saved {path}")

    def verify_crc(self) -> None:
        if not self.project or self.project.rom.is_disk:
            return

        def work(progress):
            progress(0.5, "Calculating checksum...")
            return self.project.rom.verify_crc()

        def done(res):
            ok, calc = res
            if ok is None:
                messagebox.showinfo(APP_NAME, "Checksum can't be verified for this CIC / ROM size.")
            else:
                h = self.project.rom.header
                messagebox.showinfo(APP_NAME, ("Checksum OK" if ok else "Checksum MISMATCH") +
                                    f"\nHeader:     {h.crc1:08X} {h.crc2:08X}\nCalculated: "
                                    f"{calc[0]:08X} {calc[1]:08X}")
            self.set_status("Checksum verified", 1.0)
        self.run_bg(work, done, "Checksum")

    def on_close(self) -> None:
        self.player.stop()
        save_settings(self.settings)
        self.destroy()

    # ------------------------------------------------------------- tree
    def _add(self, parent: str, text: str, kind: str, obj=None, lazy=None) -> str:
        iid = self.tree.insert(parent, tk.END, text=text)
        self.items[iid] = (kind, obj)
        if lazy:
            self.lazy[iid] = lazy
            self.tree.insert(iid, tk.END, text="...")
        return iid

    def on_open_node(self, _e=None) -> None:
        iid = self.tree.focus()
        fn = self.lazy.pop(iid, None)
        if fn:
            self.tree.delete(*self.tree.get_children(iid))
            fn(iid)

    def _toggle_children(self) -> None:
        self.settings["show_child_dls"] = self.child_var.get()
        self.populate_tree()

    def populate_tree(self) -> None:
        p = self.project
        self.tree.delete(*self.tree.get_children())
        self.items.clear()
        self.lazy.clear()
        if not p:
            return
        flt = self.filter_var.get().strip().lower()

        def match(text: str) -> bool:
            return not flt or flt in text.lower()

        self._add("", f"ROM: {p.rom.display_title}", "rom")

        # files
        def fill_files(iid):
            for b in p.blobs:
                txt = f"{b.name}  [{len(b.data):,} bytes]"
                if match(txt):
                    self._add(iid, txt, "blob", b)
        self._add("", f"Files ({len(p.blobs)})", "group", lazy=fill_files)

        # models grouped by blob
        show_children = self.child_var.get()
        by_blob: dict[int, list[ModelAsset]] = {}
        for m in p.models:
            if m.dl.is_child and not show_children:
                continue
            by_blob.setdefault(m.blob_index, []).append(m)
        total = sum(len(v) for v in by_blob.values())

        def fill_models(iid):
            for bi, lst in by_blob.items():
                def fill_group(gid, lst=lst, bi=bi):
                    if len(lst) > 1:
                        self._add(gid, "[whole file as one model]", "blobmodel", bi)
                    for m in lst:
                        txt = f"{m.name}  ({m.dl.tri_count} tris)" + ("  [sub]" if m.dl.is_child else "")
                        if match(txt):
                            self._add(gid, txt, "model", m)
                self._add(iid, f"{p.blobs[bi].name}  ({len(lst)})", "group", lazy=fill_group)
        self._add("", f"Models ({total})", "group", lazy=fill_models)

        # textures
        tex_by_blob: dict[int, list[TextureAsset]] = {}
        for t in p.textures:
            tex_by_blob.setdefault(t.blob_index, []).append(t)

        def fill_tex(iid):
            for bi, lst in tex_by_blob.items():
                def fill_group(gid, lst=lst):
                    for t in lst:
                        if match(t.name):
                            self._add(gid, t.name, "texture", t)
                self._add(iid, f"{p.blobs[bi].name}  ({len(lst)})", "group", lazy=fill_group)
        self._add("", f"Textures ({len(p.textures)})", "group", lazy=fill_tex)

        # sound
        sounds = p.all_sounds()

        def fill_sound(iid):
            for fi, bf in enumerate(p.bank_files):
                def fill_bf(gid, fi=fi):
                    for ref in sounds:
                        if ref.bank_file == fi and match(ref.name):
                            w = ref.sound.wave
                            typ = "VADPCM" if w.type == 0 else "PCM16"
                            self._add(gid, f"{ref.name}  ({typ}, {w.length:,} bytes)", "sound", ref)
                n = sum(1 for r in sounds if r.bank_file == fi)
                self._add(iid, f"Bank file {fi} @ 0x{bf.offset:X} in {p.blobs[bf.blob_index].name} ({n} sounds)",
                          "bankfile", fi, lazy=fill_bf)
        self._add("", f"Sound banks ({len(p.bank_files)} files, {len(sounds)} sounds)", "group", lazy=fill_sound)

        def fill_music(iid):
            for i, s in enumerate(p.sequences):
                txt = f"{i:03d} {s.name} [{s.kind}, {s.length:,} bytes]"
                if match(txt):
                    self._add(iid, txt, "sequence", s)
        self._add("", f"Music ({len(p.sequences)})", "group", lazy=fill_music)

        def fill_code(iid):
            for i, fn in enumerate(p.functions[:50000]):
                txt = f"func_{fn:08X}"
                if match(txt):
                    self._add(iid, txt, "function", fn)
        self._add("", f"Code ({len(p.functions)} functions)", "group", lazy=fill_code)

    def selected(self):
        sel = self.tree.selection()
        if not sel:
            return None, None
        return self.items.get(sel[0], (None, None))

    def on_select(self, _e=None) -> None:
        kind, obj = self.selected()
        if kind is None or not self.project:
            return
        self.current = (kind, obj)
        try:
            {"rom": self.show_rom, "blob": self.show_blob, "model": self.show_model,
             "blobmodel": self.show_blob_model, "texture": self.show_texture, "sound": self.show_sound,
             "bankfile": self.show_bankfile, "sequence": self.show_sequence,
             "function": self.show_function}.get(kind, lambda o: None)(obj)
        except Exception as e:  # noqa: BLE001
            traceback.print_exc()
            f = self.clear_preview()
            ttk.Label(f, text=f"Could not decode this asset:\n{e}", foreground="red").pack(anchor=tk.W)

    # ---------------------------------------------------------- previews
    def _info_table(self, parent, rows) -> None:
        t = ttk.Frame(parent)
        t.pack(anchor=tk.W, fill=tk.X, pady=4)
        for r, (k, v) in enumerate(rows):
            ttk.Label(t, text=k + ":", font=("Segoe UI", 9, "bold")).grid(row=r, column=0, sticky=tk.W, padx=(0, 10))
            ttk.Label(t, text=str(v)).grid(row=r, column=1, sticky=tk.W)

    def show_rom(self, _obj) -> None:
        p = self.project
        f = self.clear_preview()
        ttk.Label(f, text=p.rom.display_title, style="Header.TLabel").pack(anchor=tk.W)
        self._info_table(f, p.rom.info_lines())
        ttk.Label(f, text="Scan results", style="Header.TLabel").pack(anchor=tk.W, pady=(12, 0))
        self._info_table(f, [
            ("Files / compressed blocks", len(p.blobs) - 1),
            ("Display lists (models)", len(p.models)),
            ("Textures", len(p.textures)),
            ("Sound bank files", len(p.bank_files)),
            ("Sounds", len(p.all_sounds())),
            ("Music sequences", len(p.sequences)),
            ("Code functions", len(p.functions)),
        ])
        for line in p.log:
            ttk.Label(f, text="- " + line).pack(anchor=tk.W)
        bf = ttk.Frame(f)
        bf.pack(anchor=tk.W, pady=10)
        ttk.Button(bf, text="Export All Assets...", command=self.export_all_dialog).pack(side=tk.LEFT)
        ttk.Button(bf, text="Verify checksum", command=self.verify_crc).pack(side=tk.LEFT, padx=6)
        self.hex_show(0, 0)

    def show_blob(self, b) -> None:
        p = self.project
        f = self.clear_preview()
        ttk.Label(f, text=b.name, style="Header.TLabel").pack(anchor=tk.W)
        n_models = sum(1 for m in p.models if m.blob_index == b.index)
        n_tex = sum(1 for t in p.textures if t.blob_index == b.index)
        self._info_table(f, [("Type", b.kind), ("ROM offset", f"0x{b.rom_offset:08X}"),
                             ("Stored size", f"{b.compressed_size:,} bytes"),
                             ("Decompressed size", f"{len(b.data):,} bytes"),
                             ("Display lists", n_models), ("Textures", n_tex)])
        bf = ttk.Frame(f)
        bf.pack(anchor=tk.W, pady=8)
        ttk.Button(bf, text="Export raw file (.bin)...", command=lambda: self.export_blob_raw(b)).pack(side=tk.LEFT)
        ttk.Button(bf, text="Open in Texture Ripper", command=lambda: self.ripper_open(b.index, 0)).pack(side=tk.LEFT, padx=6)
        ttk.Button(bf, text="Open in Raw Audio", command=lambda: self.rawaudio_open(b.index, 0)).pack(side=tk.LEFT)
        if n_models:
            ttk.Button(bf, text="Preview all models in file", command=lambda: self.show_blob_model(b.index)).pack(side=tk.LEFT, padx=6)
        self.hex_show(b.index, 0)

    # models ------------------------------------------------------------
    def show_model(self, m: ModelAsset) -> None:
        mesh = self.project.get_mesh(m)
        self._model_view(mesh, m)
        self.hex_show(m.blob_index, m.dl.offset)

    def show_blob_model(self, bi: int) -> None:
        mesh = self.project.get_blob_mesh(bi, roots_only=not self.child_var.get())
        if mesh is None:
            return
        mesh.name = f"file_{bi}"
        self._model_view(mesh, None)

    def _model_view(self, mesh, asset: ModelAsset | None) -> None:
        f = self.clear_preview()
        top = ttk.Frame(f)
        top.pack(fill=tk.X)
        ttk.Label(top, text=mesh.name, style="Header.TLabel").pack(side=tk.LEFT)
        self.render_mode = tk.StringVar(value="textured")
        for mode in ("textured", "solid", "wire"):
            ttk.Radiobutton(top, text=mode.title(), value=mode, variable=self.render_mode,
                            command=self._redraw_model).pack(side=tk.LEFT, padx=4)
        self.wire_var = tk.BooleanVar(value=False)
        ttk.Checkbutton(top, text="Edges", variable=self.wire_var, command=self._redraw_model).pack(side=tk.LEFT)
        ttk.Button(top, text="Reset view", command=self._reset_view).pack(side=tk.LEFT, padx=6)
        ttk.Button(top, text="Export...", command=self.export_selected).pack(side=tk.RIGHT)

        info = ttk.Frame(f)
        info.pack(fill=tk.X, pady=2)
        tex_count = sum(1 for m in mesh.materials if m.texture is not None)
        ttk.Label(info, text=f"{len(mesh.vertices)} vertices, {len(mesh.triangles)} triangles, "
                             f"{len(mesh.materials)} materials ({tex_count} textured).   "
                             "Drag = rotate, right-drag = pan, wheel = zoom").pack(side=tk.LEFT)
        if asset is not None:
            seg = ttk.Frame(f)
            seg.pack(fill=tk.X)
            ttk.Label(seg, text="Segment base offset (if the model looks scrambled, adjust):").pack(side=tk.LEFT)
            self.base_var = tk.StringVar(value=f"0x{(asset.base_override if asset.base_override is not None else asset.dl.base or 0):X}")
            ttk.Entry(seg, textvariable=self.base_var, width=12).pack(side=tk.LEFT, padx=4)

            def apply_base(delta=None):
                try:
                    v = parse_int(self.base_var.get())
                except ValueError:
                    return
                if delta:
                    v = max(0, v + delta)
                asset.base_override = v
                self.show_model(asset)

            ttk.Button(seg, text="Apply", command=apply_base).pack(side=tk.LEFT)
            ttk.Button(seg, text="-16", width=4, command=lambda: apply_base(-16)).pack(side=tk.LEFT, padx=2)
            ttk.Button(seg, text="+16", width=4, command=lambda: apply_base(16)).pack(side=tk.LEFT)
            ttk.Button(seg, text="Auto", command=lambda: (setattr(asset, "base_override", None),
                                                          self.show_model(asset))).pack(side=tk.LEFT, padx=4)
        if mesh.notes:
            ttk.Label(f, text="; ".join(mesh.notes), foreground="#666").pack(anchor=tk.W)

        self.canvas = tk.Canvas(f, background="#282a30", highlightthickness=0)
        self.canvas.pack(fill=tk.BOTH, expand=True, pady=4)
        self.mesh_view = MeshView(mesh)
        self.canvas.bind("<Configure>", lambda e: self._redraw_model())
        self.canvas.bind("<ButtonPress-1>", lambda e: self._drag_start(e, "rot"))
        self.canvas.bind("<ButtonPress-3>", lambda e: self._drag_start(e, "pan"))
        self.canvas.bind("<B1-Motion>", self._drag_move)
        self.canvas.bind("<B3-Motion>", self._drag_move)
        self.canvas.bind("<ButtonRelease-1>", lambda e: self._drag_end())
        self.canvas.bind("<ButtonRelease-3>", lambda e: self._drag_end())
        self.canvas.bind("<MouseWheel>", lambda e: self._zoom(1.1 if e.delta > 0 else 1 / 1.1))
        self.canvas.bind("<Button-4>", lambda e: self._zoom(1.1))
        self.canvas.bind("<Button-5>", lambda e: self._zoom(1 / 1.1))

        if mesh.materials:
            strip = ttk.Frame(f)
            strip.pack(fill=tk.X)
            self._thumbs = []
            for m in mesh.materials[:24]:
                if m.texture is None:
                    continue
                im = m.texture.copy()
                im.thumbnail((48, 48), Image.NEAREST)
                if im.width < 32:
                    im = im.resize((im.width * 2, im.height * 2), Image.NEAREST)
                ph = ImageTk.PhotoImage(im)
                self._thumbs.append(ph)
                lbl = ttk.Label(strip, image=ph)
                lbl.pack(side=tk.LEFT, padx=2)

    def _reset_view(self) -> None:
        if self.mesh_view:
            mv = self.mesh_view
            mv.yaw, mv.pitch, mv.zoom, mv.pan = 0.61, -0.35, 1.0, np.zeros(2)
            self._redraw_model()

    def _drag_start(self, e, mode) -> None:
        self._drag = (mode, e.x, e.y)

    def _drag_move(self, e) -> None:
        if not self._drag or not self.mesh_view:
            return
        mode, x, y = self._drag
        mv = self.mesh_view
        if mode == "rot":
            mv.yaw += (e.x - x) * 0.01
            mv.pitch = max(-1.57, min(1.57, mv.pitch + (e.y - y) * 0.01))
        else:
            mv.pan = mv.pan + np.array([e.x - x, e.y - y])
        self._drag = (mode, e.x, e.y)
        self._redraw_model(fast=True)

    def _drag_end(self) -> None:
        self._drag = None
        self._redraw_model()

    def _zoom(self, factor) -> None:
        if self.mesh_view:
            self.mesh_view.zoom *= factor
            self._redraw_model()

    def _redraw_model(self, fast: bool = False) -> None:
        if not self.mesh_view or not self.canvas.winfo_exists():
            return
        w, h = self.canvas.winfo_width(), self.canvas.winfo_height()
        if w < 20 or h < 20:
            return
        mode = self.render_mode.get()
        if fast and mode == "textured":
            mode = "solid" if len(self.mesh_view.tris) < 6000 else "wire"
        if fast and mode == "solid" and len(self.mesh_view.tris) > 20000:
            mode = "wire"
        img = self.mesh_view.render(w, h, mode, self.wire_var.get())
        self._photo = ImageTk.PhotoImage(img)
        self.canvas.delete("all")
        self.canvas.create_image(0, 0, image=self._photo, anchor=tk.NW)

    # textures ----------------------------------------------------------
    def _image_view(self, parent, img: Image.Image, zoom: int | None = None) -> None:
        cv = tk.Canvas(parent, background="#555", highlightthickness=0)
        cv.pack(fill=tk.BOTH, expand=True, pady=4)

        def draw(_e=None):
            w, h = cv.winfo_width(), cv.winfo_height()
            if w < 10:
                return
            z = zoom or max(1, min(16, min(w // max(img.width, 1), h // max(img.height, 1))))
            big = img.resize((img.width * z, img.height * z), Image.NEAREST)
            bg = checkerboard(big.width, big.height)
            bg.paste(big, (0, 0), big)
            ph = ImageTk.PhotoImage(bg)
            cv._ph = ph  # keep reference
            cv.delete("all")
            cv.create_image(w // 2, h // 2, image=ph)
            cv.create_text(6, 6, anchor=tk.NW, fill="white", text=f"{img.width}x{img.height}  zoom {z}x")
        cv.bind("<Configure>", draw)

    def show_texture(self, t: TextureAsset) -> None:
        img = self.project.texture_image(t)
        f = self.clear_preview()
        top = ttk.Frame(f)
        top.pack(fill=tk.X)
        ttk.Label(top, text=t.name, style="Header.TLabel").pack(side=tk.LEFT)
        ttk.Button(top, text="Export...", command=self.export_selected).pack(side=tk.RIGHT)
        ttk.Button(top, text="Open in Texture Ripper",
                   command=lambda: self.ripper_open(t.blob_index, t.offset, t.width, t.height, t.fmt,
                                                    t.tlut_offset)).pack(side=tk.RIGHT, padx=6)
        self._info_table(f, [("Format", t.fmt), ("Size", f"{t.width} x {t.height}"),
                             ("File", self.project.blobs[t.blob_index].name),
                             ("Offset", f"0x{t.offset:X}"),
                             ("Palette", f"0x{t.tlut_offset:X}" if t.tlut_offset is not None else "-")])
        self._image_view(f, img)
        self.hex_show(t.blob_index, t.offset)

    # sound -------------------------------------------------------------
    def show_sound(self, ref: SoundRef) -> None:
        p = self.project
        f = self.clear_preview()
        top = ttk.Frame(f)
        top.pack(fill=tk.X)
        ttk.Label(top, text=ref.name, style="Header.TLabel").pack(side=tk.LEFT)
        ttk.Button(top, text="Export...", command=self.export_selected).pack(side=tk.RIGHT)
        w = ref.sound.wave
        try:
            pcm, rate = p.decode_sound(ref)
        except ValueError as e:
            ttk.Label(f, text=str(e), foreground="red").pack(anchor=tk.W)
            self._tbl_controls(f, ref.bank_file)
            return
        self._info_table(f, [("Codec", "VADPCM" if w.type == 0 else "PCM 16-bit"),
                             ("Sample rate", f"{rate} Hz"), ("Samples", f"{len(pcm):,}"),
                             ("Duration", f"{len(pcm) / rate:.2f} s"), ("Root key", ref.sound.key_base),
                             ("Loop", f"{w.loop_start}-{w.loop_end} x{w.loop_count}" if w.loop_count else "none"),
                             ("Wave offset in .tbl", f"0x{w.base:X}")])
        ctl = ttk.Frame(f)
        ctl.pack(anchor=tk.W, pady=4)
        rate_var = tk.StringVar(value=str(rate))
        ttk.Button(ctl, text="Play", command=lambda: self.player.play(pcm, int(rate_var.get() or rate))).pack(side=tk.LEFT)
        ttk.Button(ctl, text="Stop", command=self.player.stop).pack(side=tk.LEFT, padx=4)
        ttk.Label(ctl, text="Playback rate:").pack(side=tk.LEFT, padx=(12, 2))
        ttk.Combobox(ctl, textvariable=rate_var, width=8,
                     values=["8000", "11025", "16000", "22050", "32000", "44100"]).pack(side=tk.LEFT)
        self._tbl_controls(f, ref.bank_file)
        self._waveform(f, pcm)
        tb, to = p.locate_tbl(ref.bank_file)
        self.hex_show(tb, to + w.base)

    def _waveform(self, parent, pcm) -> None:
        cv = tk.Canvas(parent, background="#181a1e", highlightthickness=0, height=200)
        cv.pack(fill=tk.BOTH, expand=True, pady=4)

        def draw(_e=None):
            img = waveform(pcm, cv.winfo_width(), cv.winfo_height())
            cv._ph = ImageTk.PhotoImage(img)
            cv.delete("all")
            cv.create_image(0, 0, image=cv._ph, anchor=tk.NW)
        cv.bind("<Configure>", draw)

    def _tbl_controls(self, parent, fi: int) -> None:
        p = self.project
        bf = p.bank_files[fi]
        row = ttk.Frame(parent)
        row.pack(anchor=tk.W, pady=4)
        ttk.Label(row, text="Wave table (.tbl) location - file #, offset:").pack(side=tk.LEFT)
        bv = tk.StringVar(value=str(bf.tbl_blob if bf.tbl_blob is not None else 0))
        ov = tk.StringVar(value=f"0x{bf.tbl_offset:X}" if bf.tbl_offset is not None else "")
        ttk.Entry(row, textvariable=bv, width=5).pack(side=tk.LEFT, padx=2)
        ttk.Entry(row, textvariable=ov, width=12).pack(side=tk.LEFT, padx=2)

        def apply():
            try:
                bf.tbl_blob, bf.tbl_offset = int(bv.get()), parse_int(ov.get())
            except ValueError:
                return
            p._pcm_cache.clear()
            self.on_select()
        ttk.Button(row, text="Apply", command=apply).pack(side=tk.LEFT, padx=4)

    def show_bankfile(self, fi: int) -> None:
        p = self.project
        bf = p.bank_files[fi]
        f = self.clear_preview()
        ttk.Label(f, text=f"Sound bank file {fi}", style="Header.TLabel").pack(anchor=tk.W)
        refs = [r for r in p.all_sounds() if r.bank_file == fi]
        loc = p.locate_tbl(fi)
        self._info_table(f, [("Location", f"{p.blobs[bf.blob_index].name} @ 0x{bf.offset:X}"),
                             ("Banks", len(bf.banks)),
                             ("Instruments", sum(len(b.instruments) for b in bf.banks)),
                             ("Sounds", len(refs)),
                             ("Sample rate", ", ".join(str(b.sample_rate) for b in bf.banks)),
                             ("Wave table", f"file {loc[0]} @ 0x{loc[1]:X}" if loc else "not found")])
        self._tbl_controls(f, fi)
        row = ttk.Frame(f)
        row.pack(anchor=tk.W, pady=8)
        ttk.Button(row, text="Export SoundFont (.sf2)...", command=lambda: self.export_sf2(fi)).pack(side=tk.LEFT)
        ttk.Button(row, text="Export all sounds...", command=lambda: self.export_bank_sounds(fi)).pack(side=tk.LEFT, padx=6)
        self.hex_show(bf.blob_index, bf.offset)

    # music -------------------------------------------------------------
    def show_sequence(self, s: Sequence) -> None:
        f = self.clear_preview()
        top = ttk.Frame(f)
        top.pack(fill=tk.X)
        ttk.Label(top, text=s.name, style="Header.TLabel").pack(side=tk.LEFT)
        ttk.Button(top, text="Export...", command=self.export_selected).pack(side=tk.RIGHT)
        fmt = {"MIDI": "Standard MIDI file", "CSeq": "libultra compressed MIDI (converted to .mid)",
               "raw": "Unknown sequence format (raw export only)"}[s.kind]
        self._info_table(f, [("Format", fmt), ("File", self.project.blobs[s.blob_index].name),
                             ("Offset", f"0x{s.offset:X}"), ("Size", f"{s.length:,} bytes")])
        if s.kind != "raw":
            ttk.Button(f, text="Play in default MIDI player", command=lambda: self.play_midi(s)).pack(anchor=tk.W, pady=6)
        self.hex_show(s.blob_index, s.offset)

    def play_midi(self, s: Sequence) -> None:
        midi = self.project.sequence_midi(s)
        if not midi:
            return
        path = os.path.join(tempfile.gettempdir(), "n64studio_preview.mid")
        with open(path, "wb") as fh:
            fh.write(midi)
        open_with_default_app(path)

    # code --------------------------------------------------------------
    def show_function(self, fn: int) -> None:
        p = self.project
        f = self.clear_preview()
        top = ttk.Frame(f)
        top.pack(fill=tk.X)
        ttk.Label(top, text=f"func_{fn:08X}", style="Header.TLabel").pack(side=tk.LEFT)
        ttk.Button(top, text="Export...", command=self.export_selected).pack(side=tk.RIGHT)
        off = p.rom.entry_to_rom(fn)
        if off is None:
            ttk.Label(f, text="Function is outside the ROM").pack()
            return
        ttk.Label(f, text=f"ROM offset 0x{off:X}. Pseudo-C is a literal per-instruction translation "
                          "(MIPS delay slots execute before the branch).", foreground="#666").pack(anchor=tk.W)
        txt = tk.Text(f, font=("Consolas", 10), wrap=tk.NONE, background="#1e1f24", foreground="#d7dae0",
                      insertbackground="white")
        ys = ttk.Scrollbar(f, orient=tk.VERTICAL, command=txt.yview)
        xs = ttk.Scrollbar(f, orient=tk.HORIZONTAL, command=txt.xview)
        txt.configure(yscrollcommand=ys.set, xscrollcommand=xs.set)
        xs.pack(side=tk.BOTTOM, fill=tk.X)
        ys.pack(side=tk.RIGHT, fill=tk.Y)
        txt.pack(fill=tk.BOTH, expand=True)
        txt.insert("1.0", mips.function_to_pseudo_c(p.rom.data, off, fn))
        txt.configure(state=tk.DISABLED)
        self.hex_show(0, off)

    # --------------------------------------------------------------- hex
    def _build_hex_tab(self) -> None:
        top = ttk.Frame(self.tab_hex, padding=4)
        top.pack(fill=tk.X)
        ttk.Label(top, text="File #").pack(side=tk.LEFT)
        self.hex_blob = tk.StringVar(value="0")
        ttk.Entry(top, textvariable=self.hex_blob, width=6).pack(side=tk.LEFT, padx=2)
        ttk.Label(top, text="Offset").pack(side=tk.LEFT, padx=(8, 0))
        self.hex_off = tk.StringVar(value="0x0")
        e = ttk.Entry(top, textvariable=self.hex_off, width=12)
        e.pack(side=tk.LEFT, padx=2)
        e.bind("<Return>", lambda ev: self._hex_go())
        ttk.Button(top, text="Go", command=self._hex_go).pack(side=tk.LEFT)
        ttk.Button(top, text="<< Page", command=lambda: self._hex_page(-1)).pack(side=tk.LEFT, padx=4)
        ttk.Button(top, text="Page >>", command=lambda: self._hex_page(1)).pack(side=tk.LEFT)
        self.hex_text = tk.Text(self.tab_hex, font=("Consolas", 10), wrap=tk.NONE)
        ys = ttk.Scrollbar(self.tab_hex, orient=tk.VERTICAL, command=self.hex_text.yview)
        self.hex_text.configure(yscrollcommand=ys.set)
        ys.pack(side=tk.RIGHT, fill=tk.Y)
        self.hex_text.pack(fill=tk.BOTH, expand=True)

    HEX_PAGE = 0x4000

    def hex_show(self, blob_index: int, offset: int) -> None:
        self.hex_blob.set(str(blob_index))
        self.hex_off.set(f"0x{offset:X}")
        self._hex_go()

    def _hex_go(self) -> None:
        if not self.project:
            return
        try:
            bi = int(self.hex_blob.get())
            off = parse_int(self.hex_off.get())
            data = self.project.blobs[bi].data
        except (ValueError, IndexError):
            return
        start = off & ~0xF
        chunk = data[start:start + self.HEX_PAGE]
        lines = []
        for i in range(0, len(chunk), 16):
            row = chunk[i:i + 16]
            hx = " ".join(f"{b:02X}" for b in row)
            asc = "".join(chr(b) if 32 <= b < 127 else "." for b in row)
            lines.append(f"{start + i:08X}  {hx:<47}  {asc}")
        self.hex_text.configure(state=tk.NORMAL)
        self.hex_text.delete("1.0", tk.END)
        self.hex_text.insert("1.0", "\n".join(lines))
        self.hex_text.configure(state=tk.DISABLED)

    def _hex_page(self, d: int) -> None:
        try:
            off = parse_int(self.hex_off.get())
        except ValueError:
            off = 0
        self.hex_off.set(f"0x{max(0, off + d * self.HEX_PAGE):X}")
        self._hex_go()

    # ----------------------------------------------------- texture ripper
    def _build_ripper_tab(self) -> None:
        f = ttk.Frame(self.tab_ripper, padding=6)
        f.pack(fill=tk.BOTH, expand=True)
        ttk.Label(f, wraplength=900, justify=tk.LEFT, text=(
            "Browse any file as raw texture data - use this for games whose textures are not referenced "
            "by a recognised display list. Step through the data until the image looks right.")).pack(anchor=tk.W)
        row = ttk.Frame(f)
        row.pack(fill=tk.X, pady=4)
        self.rp_blob = tk.StringVar()
        self.rp_off = tk.StringVar(value="0x0")
        self.rp_w = tk.StringVar(value="32")
        self.rp_h = tk.StringVar(value="32")
        self.rp_fmt = tk.StringVar(value="RGBA16")
        self.rp_pal = tk.StringVar(value="")
        self.rp_zoom = tk.StringVar(value="4")
        ttk.Label(row, text="File").pack(side=tk.LEFT)
        self.rp_blob_cb = ttk.Combobox(row, textvariable=self.rp_blob, width=34, state="readonly")
        self.rp_blob_cb.pack(side=tk.LEFT, padx=2)
        for lbl, var, wd in (("Offset", self.rp_off, 10), ("W", self.rp_w, 5), ("H", self.rp_h, 5)):
            ttk.Label(row, text=lbl).pack(side=tk.LEFT, padx=(6, 0))
            e = ttk.Entry(row, textvariable=var, width=wd)
            e.pack(side=tk.LEFT, padx=2)
            e.bind("<Return>", lambda ev: self.ripper_draw())
        ttk.Label(row, text="Format").pack(side=tk.LEFT, padx=(6, 0))
        cb = ttk.Combobox(row, textvariable=self.rp_fmt, values=TEXTURE_FORMATS, width=8, state="readonly")
        cb.pack(side=tk.LEFT, padx=2)
        cb.bind("<<ComboboxSelected>>", lambda ev: self.ripper_draw())
        ttk.Label(row, text="Palette @").pack(side=tk.LEFT, padx=(6, 0))
        e = ttk.Entry(row, textvariable=self.rp_pal, width=10)
        e.pack(side=tk.LEFT, padx=2)
        e.bind("<Return>", lambda ev: self.ripper_draw())
        ttk.Label(row, text="Zoom").pack(side=tk.LEFT, padx=(6, 0))
        zc = ttk.Combobox(row, textvariable=self.rp_zoom, values=["1", "2", "3", "4", "6", "8", "12", "16"],
                          width=4, state="readonly")
        zc.pack(side=tk.LEFT)
        zc.bind("<<ComboboxSelected>>", lambda ev: self.ripper_draw())
        self.rp_blob_cb.bind("<<ComboboxSelected>>", lambda ev: self.ripper_draw())

        nav = ttk.Frame(f)
        nav.pack(fill=tk.X)
        for txt, d in (("<< image", "-img"), ("< row", "-row"), ("< 8", -8), ("< 2", -2),
                       ("2 >", 2), ("8 >", 8), ("row >", "+row"), ("image >>", "+img")):
            ttk.Button(nav, text=txt, width=len(txt) + 2,
                       command=lambda d=d: self.ripper_step(d)).pack(side=tk.LEFT, padx=1)
        ttk.Button(nav, text="Width /2", command=lambda: self._rp_scale_w(0.5)).pack(side=tk.LEFT, padx=(10, 1))
        ttk.Button(nav, text="Width x2", command=lambda: self._rp_scale_w(2)).pack(side=tk.LEFT, padx=1)
        ttk.Button(nav, text="Export image...", command=self.ripper_export).pack(side=tk.RIGHT)
        self.rp_canvas = tk.Canvas(f, background="#555", highlightthickness=0)
        self.rp_canvas.pack(fill=tk.BOTH, expand=True, pady=4)
        self.rp_canvas.bind("<Configure>", lambda e: self.ripper_draw())
        self.rp_canvas.bind("<MouseWheel>", lambda e: self.ripper_step("-row" if e.delta > 0 else "+row"))
        self.rp_img = None

    def ripper_refresh_blobs(self) -> None:
        if not self.project:
            return
        vals = [f"{b.index}: {b.name}" for b in self.project.blobs]
        self.rp_blob_cb["values"] = vals
        self.ra_blob_cb["values"] = vals
        if vals:
            self.rp_blob.set(vals[0])
            self.ra_blob.set(vals[0])

    def ripper_open(self, bi, off, w=None, h=None, fmt=None, pal=None) -> None:
        self.rp_blob.set(f"{bi}: {self.project.blobs[bi].name}")
        self.rp_off.set(f"0x{off:X}")
        if w:
            self.rp_w.set(str(w))
            self.rp_h.set(str(h))
        if fmt:
            self.rp_fmt.set(fmt)
        self.rp_pal.set(f"0x{pal:X}" if pal is not None else "")
        self.nb.select(self.tab_ripper)
        self.ripper_draw()

    def _rp_params(self):
        bi = int(self.rp_blob.get().split(":")[0])
        return (bi, parse_int(self.rp_off.get()), max(1, min(4096, int(self.rp_w.get()))),
                max(1, min(4096, int(self.rp_h.get()))), self.rp_fmt.get())

    def _rp_scale_w(self, k) -> None:
        try:
            w = int(self.rp_w.get())
            h = int(self.rp_h.get())
        except ValueError:
            return
        self.rp_w.set(str(max(1, int(w * k))))
        self.rp_h.set(str(max(1, int(h / k))))
        self.ripper_draw()

    def ripper_step(self, d) -> None:
        try:
            _bi, off, w, h, fmt = self._rp_params()
        except ValueError:
            return
        size = texture_byte_size(w, h, fmt)
        row = max(1, texture_byte_size(w, 1, fmt))
        step = {"-img": -size, "+img": size, "-row": -row, "+row": row}.get(d, d)
        self.rp_off.set(f"0x{max(0, off + step):X}")
        self.ripper_draw()

    def ripper_draw(self) -> None:
        if not self.project or not self.rp_blob.get():
            return
        try:
            bi, off, w, h, fmt = self._rp_params()
        except ValueError:
            return
        data = self.project.blobs[bi].data
        pal = None
        if fmt in ("CI4", "CI8") and self.rp_pal.get().strip():
            try:
                pal = decode_palette(data, parse_int(self.rp_pal.get()), 16 if fmt == "CI4" else 256)
            except ValueError:
                pal = None
        img = decode_texture(data, off, w, h, fmt, pal)
        self.rp_img = img
        z = int(self.rp_zoom.get())
        big = img.resize((w * z, h * z), Image.NEAREST)
        bg = checkerboard(big.width, big.height)
        bg.paste(big, (0, 0), big)
        self._rp_photo = ImageTk.PhotoImage(bg)
        self.rp_canvas.delete("all")
        self.rp_canvas.create_image(4, 4, image=self._rp_photo, anchor=tk.NW)
        self.rp_canvas.create_text(8 + big.width, 6, anchor=tk.NW, fill="white",
                                   text=f"{fmt} {w}x{h}\n@ 0x{off:X}\n{texture_byte_size(w, h, fmt)} bytes")

    def ripper_export(self) -> None:
        if self.rp_img is None:
            return
        path = filedialog.asksaveasfilename(defaultextension=".png", initialfile=f"texture_{self.rp_off.get()}",
                                            filetypes=self._image_filetypes())
        if path:
            save_image(self.rp_img, path)
            self.set_status(f"Saved {path}")

    # -------------------------------------------------------- raw audio
    def _build_rawaudio_tab(self) -> None:
        f = ttk.Frame(self.tab_rawaudio, padding=6)
        f.pack(fill=tk.BOTH, expand=True)
        ttk.Label(f, wraplength=900, justify=tk.LEFT, text=(
            "Interpret any region as uncompressed PCM audio - for games with custom sound engines. "
            "Try different formats / rates until it sounds right.")).pack(anchor=tk.W)
        row = ttk.Frame(f)
        row.pack(fill=tk.X, pady=4)
        self.ra_blob = tk.StringVar()
        self.ra_off = tk.StringVar(value="0x0")
        self.ra_len = tk.StringVar(value="0x10000")
        self.ra_fmt = tk.StringVar(value="s16be")
        self.ra_rate = tk.StringVar(value="22050")
        ttk.Label(row, text="File").pack(side=tk.LEFT)
        self.ra_blob_cb = ttk.Combobox(row, textvariable=self.ra_blob, width=34, state="readonly")
        self.ra_blob_cb.pack(side=tk.LEFT, padx=2)
        for lbl, var, wd in (("Offset", self.ra_off, 10), ("Length", self.ra_len, 10)):
            ttk.Label(row, text=lbl).pack(side=tk.LEFT, padx=(6, 0))
            ttk.Entry(row, textvariable=var, width=wd).pack(side=tk.LEFT, padx=2)
        ttk.Label(row, text="Format").pack(side=tk.LEFT, padx=(6, 0))
        ttk.Combobox(row, textvariable=self.ra_fmt, values=["s16be", "s16le", "s8", "u8"], width=6,
                     state="readonly").pack(side=tk.LEFT)
        ttk.Label(row, text="Rate").pack(side=tk.LEFT, padx=(6, 0))
        ttk.Combobox(row, textvariable=self.ra_rate, values=["8000", "11025", "16000", "22050", "32000", "44100"],
                     width=7).pack(side=tk.LEFT)
        btns = ttk.Frame(f)
        btns.pack(fill=tk.X)
        ttk.Button(btns, text="Decode", command=self.rawaudio_decode).pack(side=tk.LEFT)
        ttk.Button(btns, text="Play", command=lambda: self.rawaudio_decode(play=True)).pack(side=tk.LEFT, padx=4)
        ttk.Button(btns, text="Stop", command=self.player.stop).pack(side=tk.LEFT)
        ttk.Button(btns, text="Export...", command=self.rawaudio_export).pack(side=tk.RIGHT)
        self.ra_canvas = tk.Canvas(f, background="#181a1e", highlightthickness=0)
        self.ra_canvas.pack(fill=tk.BOTH, expand=True, pady=4)
        self.ra_pcm = None

    def rawaudio_open(self, bi: int, off: int) -> None:
        self.ra_blob.set(f"{bi}: {self.project.blobs[bi].name}")
        self.ra_off.set(f"0x{off:X}")
        self.nb.select(self.tab_rawaudio)

    def rawaudio_decode(self, play: bool = False) -> None:
        if not self.project or not self.ra_blob.get():
            return
        try:
            bi = int(self.ra_blob.get().split(":")[0])
            pcm = decode_raw_pcm(self.project.blobs[bi].data, parse_int(self.ra_off.get()),
                                 parse_int(self.ra_len.get()), self.ra_fmt.get())
            rate = int(self.ra_rate.get())
        except ValueError:
            return
        self.ra_pcm = (pcm, rate)
        img = waveform(pcm, self.ra_canvas.winfo_width(), self.ra_canvas.winfo_height())
        self._ra_photo = ImageTk.PhotoImage(img)
        self.ra_canvas.delete("all")
        self.ra_canvas.create_image(0, 0, image=self._ra_photo, anchor=tk.NW)
        if play:
            self.player.play(pcm, rate)

    def rawaudio_export(self) -> None:
        self.rawaudio_decode()
        if not self.ra_pcm:
            return
        path = filedialog.asksaveasfilename(defaultextension=".wav", filetypes=self._audio_filetypes())
        if path:
            sound_export.save_audio(self.ra_pcm[0], self.ra_pcm[1], path)
            self.set_status(f"Saved {path}")

    # ------------------------------------------------------------ export
    def _image_filetypes(self):
        return [(f"{fmt} image", f"*.{ext}") for ext, fmt in IMAGE_FORMATS.items()]

    def _audio_filetypes(self):
        return [(desc, f"*.{ext}") for ext, desc in sound_export.audio_formats().items()]

    def _model_filetypes(self):
        ft = [(desc, f"*.{ext}") for ext, desc in MODEL_FORMATS.items()]
        ft.insert(1, ("Blender scene (.blend - uses your Blender install)", "*.blend"))
        ft.insert(2, ("Unity package (.unitypackage)", "*.unitypackage"))
        return ft

    def _ask_save(self, name: str, filetypes, default_ext: str) -> str | None:
        path = filedialog.asksaveasfilename(
            title="Export", initialdir=self.settings.get("export_dir") or None, initialfile=name,
            defaultextension=default_ext, filetypes=filetypes)
        if path:
            self.settings["export_dir"] = os.path.dirname(path)
            # Tk on Windows may leave the extension off when "all types" is used
            if not os.path.splitext(path)[1]:
                path += default_ext
        return path or None

    def export_selected(self) -> None:
        kind, obj = self.selected()
        p = self.project
        if not p or kind is None:
            return
        try:
            if kind in ("model", "blobmodel"):
                mesh = p.get_mesh(obj) if kind == "model" else p.get_blob_mesh(obj, not self.child_var.get())
                if mesh is None:
                    return
                name = safe(mesh.name) if kind == "model" else f"file_{obj}"
                path = self._ask_save(name, self._model_filetypes(), ".glb")
                if path:
                    self.export_mesh(mesh, path)
            elif kind == "texture":
                path = self._ask_save(safe(obj.name), self._image_filetypes(), ".png")
                if path:
                    save_image(p.texture_image(obj), path)
                    self.set_status(f"Saved {path}")
            elif kind == "sound":
                path = self._ask_save(obj.name, self._audio_filetypes(), ".wav")
                if path:
                    pcm, rate = p.decode_sound(obj)
                    self.set_status(f"Saved {sound_export.save_audio(pcm, rate, path)}")
            elif kind == "bankfile":
                self.export_sf2(obj)
            elif kind == "sequence":
                ft = [("MIDI file", "*.mid"), ("Raw sequence data", "*.seq")] if obj.kind != "raw" else \
                     [("Raw sequence data", "*.seq")]
                path = self._ask_save(f"seq_{obj.offset:06X}", ft, ft[0][1][1:])
                if path:
                    data = p.sequence_raw(obj) if path.lower().endswith(".seq") else p.sequence_midi(obj)
                    with open(path, "wb") as fh:
                        fh.write(data)
                    self.set_status(f"Saved {path}")
            elif kind == "function":
                path = self._ask_save(f"func_{obj:08X}", [("Pseudo-C", "*.c"), ("Assembly", "*.s")], ".c")
                if path:
                    off = p.rom.entry_to_rom(obj)
                    if path.lower().endswith(".s"):
                        txt = "\n".join(f"/* {i.addr:08X} {i.word:08X} */  {i.text()}"
                                        for i in mips.disassemble(p.rom.data, off, obj, 4000))
                    else:
                        txt = mips.function_to_pseudo_c(p.rom.data, off, obj)
                    with open(path, "w", encoding="utf-8") as fh:
                        fh.write(txt)
                    self.set_status(f"Saved {path}")
            elif kind == "blob":
                self.export_blob_raw(obj)
            elif kind in ("rom", "group"):
                self.export_all_dialog()
        except Exception as e:  # noqa: BLE001
            traceback.print_exc()
            messagebox.showerror(APP_NAME, f"Export failed:\n{e}")

    def export_mesh(self, mesh, path: str) -> None:
        ext = os.path.splitext(path)[1].lower().lstrip(".")
        scale = float(self.settings.get("model_scale") or 1.0)
        if ext == "blend":
            folder = os.path.splitext(path)[0] + "_blender"

            def work(progress):
                progress(0.3, "Building .blend with Blender...")
                return blender_export.export_blender([mesh], folder, os.path.splitext(os.path.basename(path))[0], scale)

            self.run_bg(work, lambda r: messagebox.showinfo(APP_NAME, r["message"] + f"\n\nFolder: {folder}"),
                        "Blender export")
        elif ext == "unitypackage":
            tmp = tempfile.mkdtemp()
            export_model(mesh, os.path.join(tmp, safe(mesh.name) + ".fbx"), "fbx", scale)
            pkg = UnityPackage(f"Assets/N64/{safe(self.project.rom.display_title)}/{safe(mesh.name)}")
            pkg.add_folder_tree("", tmp)
            pkg.files = {k.replace("//", "/"): v for k, v in pkg.files.items()}
            pkg.save(path)
            self.set_status(f"Saved {path}")
        else:
            files = export_model(mesh, path, ext, scale)
            self.set_status(f"Saved {len(files)} file(s): {path}")

    def export_blob_raw(self, b) -> None:
        path = self._ask_save(safe(b.name) + ".bin", [("Binary", "*.bin")], ".bin")
        if path:
            with open(path, "wb") as fh:
                fh.write(b.data)
            self.set_status(f"Saved {path}")

    def export_sf2(self, fi: int) -> None:
        path = self._ask_save(f"bankfile{fi}.sf2", [("SoundFont 2", "*.sf2")], ".sf2")
        if not path:
            return
        self.run_bg(lambda prog: bank_file_to_sf2(self.project, fi, path),
                    lambda r: self.set_status(f"Saved {r}", 1.0), "SoundFont export")

    def export_bank_sounds(self, fi: int) -> None:
        folder = filedialog.askdirectory(title="Export all sounds to folder")
        if not folder:
            return
        fmt_var = self._choose("Audio format", list(sound_export.audio_formats()), "wav")
        if not fmt_var:
            return

        def work(progress):
            refs = [r for r in self.project.all_sounds() if r.bank_file == fi]
            for i, ref in enumerate(refs):
                progress(i / max(1, len(refs)), f"Exporting {ref.name}")
                pcm, rate = self.project.decode_sound(ref)
                sound_export.save_audio(pcm, rate, os.path.join(folder, ref.name), fmt_var)
            return len(refs)
        self.run_bg(work, lambda n: self.set_status(f"Exported {n} sounds to {folder}", 1.0), "Sound export")

    def _choose(self, title: str, values: list[str], default: str) -> str | None:
        dlg = tk.Toplevel(self)
        dlg.title(title)
        dlg.transient(self)
        var = tk.StringVar(value=default)
        ttk.Label(dlg, text=title, padding=8).pack()
        ttk.Combobox(dlg, textvariable=var, values=values, state="readonly").pack(padx=12)
        res = {"v": None}

        def ok():
            res["v"] = var.get()
            dlg.destroy()
        ttk.Button(dlg, text="OK", command=ok).pack(pady=8)
        dlg.grab_set()
        self.wait_window(dlg)
        return res["v"]

    def export_all_dialog(self, preset: str | None = None) -> None:
        if not self.project:
            messagebox.showinfo(APP_NAME, "Open a ROM first.")
            return
        dlg = tk.Toplevel(self)
        dlg.title("Export All Assets")
        dlg.transient(self)
        dlg.resizable(False, False)
        f = ttk.Frame(dlg, padding=12)
        f.pack(fill=tk.BOTH, expand=True)
        opt = BatchOptions(model_scale=float(self.settings.get("model_scale") or 1.0))
        if preset == "unity":
            opt.unity_package = True
        if preset == "blender":
            opt.blender = True
        v = {k: tk.BooleanVar(value=getattr(opt, k)) for k in
             ("models", "roots_only", "textures", "audio", "soundfonts", "music", "raw_files", "code",
              "unity_package", "blender")}
        mf = tk.StringVar(value=opt.model_format)
        imf = tk.StringVar(value=opt.image_format)
        af = tk.StringVar(value=opt.audio_format)
        uf = tk.StringVar(value=opt.unity_model_format)
        out = tk.StringVar(value=self.settings.get("export_dir") or os.path.expanduser("~"))
        r = 0

        def row(widget_l, widget_r=None):
            nonlocal r
            widget_l.grid(row=r, column=0, sticky=tk.W, pady=2)
            if widget_r is not None:
                widget_r.grid(row=r, column=1, sticky=tk.W, pady=2, padx=6)
            r += 1

        row(ttk.Checkbutton(f, text="3D models as", variable=v["models"]),
            ttk.Combobox(f, textvariable=mf, values=list(MODEL_FORMATS), state="readonly", width=10))
        row(ttk.Checkbutton(f, text="   skip sub-meshes (avoid duplicates)", variable=v["roots_only"]))
        row(ttk.Checkbutton(f, text="Textures as", variable=v["textures"]),
            ttk.Combobox(f, textvariable=imf, values=list(IMAGE_FORMATS), state="readonly", width=10))
        row(ttk.Checkbutton(f, text="Sounds as", variable=v["audio"]),
            ttk.Combobox(f, textvariable=af, values=list(sound_export.audio_formats()), state="readonly", width=10))
        row(ttk.Checkbutton(f, text="Instrument banks as SoundFont (.sf2)", variable=v["soundfonts"]))
        row(ttk.Checkbutton(f, text="Music as MIDI (.mid)", variable=v["music"]))
        row(ttk.Checkbutton(f, text="Decompressed raw files (.bin)", variable=v["raw_files"]))
        row(ttk.Checkbutton(f, text="Code as pseudo-C (.c)", variable=v["code"]))
        row(ttk.Checkbutton(f, text="Unity package (.unitypackage), models as", variable=v["unity_package"]),
            ttk.Combobox(f, textvariable=uf, values=["fbx", "obj", "glb"], state="readonly", width=10))
        row(ttk.Checkbutton(f, text="Blender scene (.blend)", variable=v["blender"]))
        ttk.Separator(f).grid(row=r, column=0, columnspan=3, sticky=tk.EW, pady=8)
        r += 1
        of = ttk.Frame(f)
        ttk.Label(of, text="Output folder:").pack(side=tk.LEFT)
        ttk.Entry(of, textvariable=out, width=46).pack(side=tk.LEFT, padx=4)
        ttk.Button(of, text="Browse...", command=lambda: out.set(
            filedialog.askdirectory(initialdir=out.get()) or out.get())).pack(side=tk.LEFT)
        of.grid(row=r, column=0, columnspan=3, sticky=tk.W)
        r += 1
        cancel_flag = {"stop": False}

        def start():
            for k, var in v.items():
                setattr(opt, k, var.get())
            opt.model_format, opt.image_format, opt.audio_format = mf.get(), imf.get(), af.get()
            opt.unity_model_format = uf.get()
            folder = out.get()
            self.settings["export_dir"] = folder
            dlg.destroy()

            def work(progress):
                return export_all(self.project, folder, opt, progress, lambda: cancel_flag["stop"])

            def done(report):
                self.set_status("Export finished", 1.0)
                if messagebox.askyesno(APP_NAME, "Export finished:\n\n" + "\n".join(report) +
                                       "\n\nOpen the output folder?"):
                    open_with_default_app(folder)
            self.run_bg(work, done, "Export")

        bf = ttk.Frame(f)
        bf.grid(row=r, column=0, columnspan=3, sticky=tk.E, pady=(10, 0))
        ttk.Button(bf, text="Cancel", command=dlg.destroy).pack(side=tk.RIGHT)
        ttk.Button(bf, text="Export", command=start).pack(side=tk.RIGHT, padx=6)
        dlg.grab_set()

    # ----------------------------------------------------------- dialogs
    def settings_dialog(self) -> None:
        dlg = tk.Toplevel(self)
        dlg.title("Settings")
        dlg.transient(self)
        f = ttk.Frame(dlg, padding=12)
        f.pack(fill=tk.BOTH, expand=True)
        s = self.settings
        bl = tk.StringVar(value=s.get("blender_path") or (blender_export.find_blender() or ""))
        ff = tk.StringVar(value=s.get("ffmpeg_path") or (sound_export.find_ffmpeg() or ""))
        sc = tk.StringVar(value=str(s.get("model_scale", 1.0)))
        tb = tk.StringVar(value=str(s.get("texture_budget", 90)))
        deep = tk.BooleanVar(value=bool(s.get("deep_compression")))
        romdl = tk.BooleanVar(value=bool(s.get("scan_rom_display_lists")))
        rows = [("Blender executable", bl, True), ("ffmpeg executable (OGG/MP3/...)", ff, True),
                ("Model export scale", sc, False), ("Texture discovery time limit (s)", tb, False)]
        for i, (lbl, var, browse) in enumerate(rows):
            ttk.Label(f, text=lbl).grid(row=i, column=0, sticky=tk.W, pady=3)
            ttk.Entry(f, textvariable=var, width=50).grid(row=i, column=1, padx=4)
            if browse:
                ttk.Button(f, text="...", width=3, command=lambda var=var: var.set(
                    filedialog.askopenfilename() or var.get())).grid(row=i, column=2)
        ttk.Checkbutton(f, text="Deep compression scan (zlib / Rare deflate - slower)",
                        variable=deep).grid(row=4, column=0, columnspan=2, sticky=tk.W)
        ttk.Checkbutton(f, text="Search uncompressed ROM data for models",
                        variable=romdl).grid(row=5, column=0, columnspan=2, sticky=tk.W)

        def ok():
            s["blender_path"], s["ffmpeg_path"] = bl.get().strip(), ff.get().strip()
            try:
                s["model_scale"] = float(sc.get())
                s["texture_budget"] = float(tb.get())
            except ValueError:
                pass
            s["deep_compression"], s["scan_rom_display_lists"] = deep.get(), romdl.get()
            blender_export.set_blender_path(s["blender_path"])
            sound_export.set_ffmpeg_path(s["ffmpeg_path"])
            save_settings(s)
            dlg.destroy()
        bf = ttk.Frame(f)
        bf.grid(row=6, column=0, columnspan=3, sticky=tk.E, pady=(10, 0))
        ttk.Button(bf, text="Cancel", command=dlg.destroy).pack(side=tk.RIGHT)
        ttk.Button(bf, text="OK", command=ok).pack(side=tk.RIGHT, padx=6)
        dlg.grab_set()

    def show_about(self) -> None:
        messagebox.showinfo(APP_NAME, f"{APP_NAME} {__version__}\n\nNintendo 64 ROM asset browser and "
                                      "exporter.\nUse only with ROMs you have dumped from cartridges you own.")

    def show_help(self) -> None:
        win = tk.Toplevel(self)
        win.title("How it works")
        txt = tk.Text(win, wrap=tk.WORD, width=90, height=34, padx=10, pady=10)
        txt.pack(fill=tk.BOTH, expand=True)
        txt.insert("1.0", HELP_TEXT)
        txt.configure(state=tk.DISABLED)


HELP_TEXT = """N64 games have no common file system or model format - every developer stored data
differently. N64 Asset Studio therefore works the way N64 hardware does:

FILES
  Compressed blocks (Yaz0, Yay0, MIO0, gzip, zlib, Rare 0x1172 deflate) are located by
  signature and unpacked. Zelda 64 engine games (Ocarina of Time, Majora's Mask) are split
  using their real 'dmadata' file table.

MODELS
  Everything the N64 draws goes through RSP display lists. The scanner finds lists for the
  F3D (Fast3D), F3DEX and F3DEX2 microcodes - which cover almost all commercial games - and
  replays them to rebuild triangles, UVs, vertex colours/normals and bound textures.
  Display lists use segment addresses; the program works out which file offset each
  segment points at. If a model looks scrambled, adjust the "segment base" in the model
  view. Games using custom microcodes (e.g. some Factor 5 / Boss titles) need the
  Texture Ripper and Raw views instead.

TEXTURES
  Textures referenced by display lists are listed automatically. The Texture Ripper lets
  you view any data as any N64 format (RGBA16/32, CI4/8, IA4/8/16, I4/8, YUV16).

AUDIO
  libultra sound banks ('B1' .ctl files) are parsed; samples (VADPCM or PCM16) are decoded
  from the matching wave table (.tbl). If the .tbl isn't found automatically, enter its
  location. Banks can be exported as SoundFonts. Nintendo's later in-house audio engine
  (SM64/OoT era 'Audiobank' format) and custom engines can be explored in Raw Audio.

MUSIC
  Standard MIDI files and libultra compressed-MIDI ('S1' sequence banks) are converted to
  standard .mid. Other sequence formats are exported raw (.seq).

CODE
  The main code segment is disassembled (MIPS R4300i) and every function can be viewed as
  assembly plus a literal pseudo-C translation. This is a reading aid, not a compilable
  decompilation.

EXPORT FORMATS
  Models: glTF/GLB, FBX, OBJ, DAE, PLY, STL, Blender .blend (needs Blender installed; a
  ready-to-run import script is written otherwise), Unity .unitypackage.
  Images: PNG, BMP, TGA, TIFF, GIF, WEBP, JPG, DDS, ICO, ...
  Audio: WAV, AIFF, FLAC, AU, raw (+ OGG, MP3, OPUS, M4A, WMA when ffmpeg is installed),
  SoundFont 2 for instrument banks, MIDI for music.
"""


def main(argv: list[str] | None = None) -> None:
    argv = sys.argv[1:] if argv is None else argv
    if IS_WINDOWS:
        try:
            import ctypes
            ctypes.windll.shcore.SetProcessDpiAwareness(1)
        except (AttributeError, OSError):
            pass
    path = argv[0] if argv else None
    app = App(path)
    app.mainloop()

