"""Blend2Unity - GUI for converting .blend files to .unitypackage files.

Run:  python blend2unity.py [files.blend ...]
"""

from __future__ import annotations

import json
import os
import platform
import queue
import subprocess
import sys
import threading
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk
from tkinter.scrolledtext import ScrolledText

from converter import (ConversionCancelled, ConversionError, ConvertOptions,
                       blender_version, convert_file, find_blender)

APP_NAME = "Blend2Unity"
SETTINGS_FILE = Path.home() / ".blend2unity.json"


def load_settings() -> dict:
    try:
        return json.loads(SETTINGS_FILE.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def save_settings(data: dict) -> None:
    try:
        SETTINGS_FILE.write_text(json.dumps(data, indent=2), encoding="utf-8")
    except OSError:
        pass


def open_folder(path: Path) -> None:
    try:
        if platform.system() == "Windows":
            os.startfile(path)  # type: ignore[attr-defined]
        elif platform.system() == "Darwin":
            subprocess.Popen(["open", str(path)])
        else:
            subprocess.Popen(["xdg-open", str(path)])
    except OSError:
        pass


class App(ttk.Frame):
    def __init__(self, root: tk.Tk, initial_files=()):
        super().__init__(root, padding=10)
        self.root = root
        self.events: "queue.Queue[tuple]" = queue.Queue()
        self.worker: threading.Thread | None = None
        self.cancel_event = threading.Event()
        self.last_output: Path | None = None

        s = load_settings()
        self.blender_var = tk.StringVar(value=s.get("blender") or find_blender() or "")
        self.output_var = tk.StringVar(value=s.get("output", ""))
        self.next_to_source_var = tk.BooleanVar(value=s.get("next_to_source", False))
        self.unity_folder_var = tk.StringVar(value=s.get("unity_folder", "Assets/{name}"))
        self.apply_transform_var = tk.BooleanVar(value=s.get("apply_transform", True))
        self.modifiers_var = tk.BooleanVar(value=s.get("apply_modifiers", True))
        self.animation_var = tk.BooleanVar(value=s.get("include_animation", True))
        self.textures_var = tk.BooleanVar(value=s.get("include_textures", True))
        self.include_blend_var = tk.BooleanVar(value=s.get("include_blend", False))

        self.file_status_var = tk.StringVar(value="Idle")
        self.overall_status_var = tk.StringVar(value="Add .blend files to begin.")
        self.file_progress = tk.DoubleVar(value=0)
        self.overall_progress = tk.DoubleVar(value=0)

        self._build()
        self.add_files(initial_files)
        self._update_buttons()
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)
        self.root.after(50, self._poll_events)

    # ---------------------------------------------------------------- layout
    def _build(self):
        self.grid(sticky="nsew")
        self.root.columnconfigure(0, weight=1)
        self.root.rowconfigure(0, weight=1)
        self.columnconfigure(0, weight=1)

        # Blender location
        bl = ttk.LabelFrame(self, text="Blender", padding=8)
        bl.grid(row=0, column=0, sticky="ew")
        bl.columnconfigure(0, weight=1)
        ttk.Entry(bl, textvariable=self.blender_var).grid(row=0, column=0, sticky="ew")
        ttk.Button(bl, text="Browse…", command=self.browse_blender).grid(row=0, column=1, padx=(6, 0))
        ttk.Button(bl, text="Detect", command=self.detect_blender).grid(row=0, column=2, padx=(6, 0))

        # File list
        fl = ttk.LabelFrame(self, text=".blend files", padding=8)
        fl.grid(row=1, column=0, sticky="nsew", pady=(8, 0))
        fl.columnconfigure(0, weight=1)
        fl.rowconfigure(0, weight=1)
        self.rowconfigure(1, weight=1)
        self.listbox = tk.Listbox(fl, selectmode=tk.EXTENDED, height=5, activestyle="none")
        self.listbox.grid(row=0, column=0, sticky="nsew")
        sb = ttk.Scrollbar(fl, orient=tk.VERTICAL, command=self.listbox.yview)
        sb.grid(row=0, column=1, sticky="ns")
        self.listbox.configure(yscrollcommand=sb.set)
        self.listbox.bind("<Delete>", lambda e: self.remove_selected())
        btns = ttk.Frame(fl)
        btns.grid(row=0, column=2, sticky="n", padx=(8, 0))
        self.add_btn = ttk.Button(btns, text="Add files…", command=self.browse_files)
        self.add_dir_btn = ttk.Button(btns, text="Add folder…", command=self.browse_folder)
        self.remove_btn = ttk.Button(btns, text="Remove", command=self.remove_selected)
        self.clear_btn = ttk.Button(btns, text="Clear", command=self.clear_files)
        for i, b in enumerate((self.add_btn, self.add_dir_btn, self.remove_btn, self.clear_btn)):
            b.grid(row=i, column=0, sticky="ew", pady=(0, 4))

        # Output
        out = ttk.LabelFrame(self, text="Output", padding=8)
        out.grid(row=2, column=0, sticky="ew", pady=(8, 0))
        out.columnconfigure(1, weight=1)
        ttk.Label(out, text="Save to:").grid(row=0, column=0, sticky="w")
        self.output_entry = ttk.Entry(out, textvariable=self.output_var)
        self.output_entry.grid(row=0, column=1, sticky="ew", padx=(6, 0))
        self.output_btn = ttk.Button(out, text="Browse…", command=self.browse_output)
        self.output_btn.grid(row=0, column=2, padx=(6, 0))
        ttk.Checkbutton(out, text="Save next to each .blend file", variable=self.next_to_source_var,
                        command=self._update_buttons).grid(row=1, column=1, sticky="w", pady=(4, 0))
        ttk.Label(out, text="Unity folder:").grid(row=2, column=0, sticky="w", pady=(4, 0))
        ttk.Entry(out, textvariable=self.unity_folder_var).grid(row=2, column=1, sticky="ew", padx=(6, 0), pady=(4, 0))
        ttk.Label(out, text="{name} = file name", foreground="gray").grid(row=2, column=2, padx=(6, 0), pady=(4, 0))

        # Options
        opt = ttk.LabelFrame(self, text="Options", padding=8)
        opt.grid(row=3, column=0, sticky="ew", pady=(8, 0))
        checks = [
            ("Include textures", self.textures_var),
            ("Include animation", self.animation_var),
            ("Apply modifiers", self.modifiers_var),
            ("Apply transform (fixes -90° rotation)", self.apply_transform_var),
            ("Also include original .blend", self.include_blend_var),
        ]
        for i, (label, var) in enumerate(checks):
            ttk.Checkbutton(opt, text=label, variable=var).grid(row=i // 3, column=i % 3, sticky="w", padx=(0, 16))

        # Progress
        pr = ttk.LabelFrame(self, text="Progress", padding=8)
        pr.grid(row=4, column=0, sticky="ew", pady=(8, 0))
        pr.columnconfigure(0, weight=1)
        ttk.Label(pr, textvariable=self.file_status_var).grid(row=0, column=0, sticky="w")
        ttk.Progressbar(pr, variable=self.file_progress, maximum=100, style="B2U.Horizontal.TProgressbar").grid(row=1, column=0, sticky="ew", pady=(2, 6))
        ttk.Label(pr, textvariable=self.overall_status_var).grid(row=2, column=0, sticky="w")
        ttk.Progressbar(pr, variable=self.overall_progress, maximum=100, style="B2U.Horizontal.TProgressbar").grid(row=3, column=0, sticky="ew", pady=(2, 0))

        # Log
        self.log_text = ScrolledText(self, height=6, state=tk.DISABLED, wrap=tk.WORD)
        self.log_text.grid(row=5, column=0, sticky="nsew", pady=(8, 0))
        self.rowconfigure(5, weight=1)

        # Actions
        act = ttk.Frame(self)
        act.grid(row=6, column=0, sticky="ew", pady=(8, 0))
        act.columnconfigure(0, weight=1)
        self.open_btn = ttk.Button(act, text="Open output folder", command=self.open_output)
        self.open_btn.grid(row=0, column=0, sticky="w")
        self.cancel_btn = ttk.Button(act, text="Cancel", command=self.cancel)
        self.cancel_btn.grid(row=0, column=1, padx=(6, 0))
        self.convert_btn = ttk.Button(act, text="Convert", command=self.start)
        self.convert_btn.grid(row=0, column=2, padx=(6, 0))

    # --------------------------------------------------------------- helpers
    def files(self):
        return list(self.listbox.get(0, tk.END))

    def add_files(self, paths):
        existing = set(self.files())
        for p in paths:
            p = str(Path(p).resolve())
            if p.lower().endswith(".blend") and p not in existing:
                self.listbox.insert(tk.END, p)
                existing.add(p)
        self._update_buttons()

    def log(self, msg: str):
        self.log_text.configure(state=tk.NORMAL)
        self.log_text.insert(tk.END, msg + "\n")
        self.log_text.see(tk.END)
        self.log_text.configure(state=tk.DISABLED)

    def running(self) -> bool:
        return self.worker is not None and self.worker.is_alive()

    def _update_buttons(self):
        busy = self.running()
        idle = tk.DISABLED if busy else tk.NORMAL
        for b in (self.add_btn, self.add_dir_btn, self.remove_btn, self.clear_btn):
            b.configure(state=idle)
        self.convert_btn.configure(state=tk.DISABLED if busy or not self.files() else tk.NORMAL)
        self.cancel_btn.configure(state=tk.NORMAL if busy else tk.DISABLED)
        out_state = tk.DISABLED if self.next_to_source_var.get() else tk.NORMAL
        self.output_entry.configure(state=out_state)
        self.output_btn.configure(state=out_state)
        self.open_btn.configure(state=tk.NORMAL if self.last_output else tk.DISABLED)

    # --------------------------------------------------------------- actions
    def browse_blender(self):
        types = [("Blender", "blender.exe"), ("All files", "*")] if platform.system() == "Windows" else [("All files", "*")]
        path = filedialog.askopenfilename(title="Select the Blender executable", filetypes=types)
        if path:
            if platform.system() == "Darwin" and path.endswith(".app"):
                path = os.path.join(path, "Contents", "MacOS", "Blender")
            self.blender_var.set(path)

    def detect_blender(self):
        path = find_blender()
        if path:
            self.blender_var.set(path)
            self.log(f"Found Blender: {path}")
        else:
            messagebox.showwarning(APP_NAME, "Blender could not be found automatically.\n"
                                             "Install Blender or use Browse… to locate it.")

    def browse_files(self):
        paths = filedialog.askopenfilenames(title="Select .blend files",
                                            filetypes=[("Blender files", "*.blend"), ("All files", "*")])
        self.add_files(paths)

    def browse_folder(self):
        folder = filedialog.askdirectory(title="Add all .blend files in a folder")
        if folder:
            self.add_files(sorted(str(p) for p in Path(folder).rglob("*.blend")))

    def browse_output(self):
        folder = filedialog.askdirectory(title="Choose where to save .unitypackage files")
        if folder:
            self.output_var.set(folder)

    def remove_selected(self):
        if self.running():
            return
        for i in reversed(self.listbox.curselection()):
            self.listbox.delete(i)
        self._update_buttons()

    def clear_files(self):
        self.listbox.delete(0, tk.END)
        self._update_buttons()

    def open_output(self):
        if self.last_output:
            open_folder(self.last_output)

    def options(self) -> ConvertOptions:
        return ConvertOptions(
            unity_folder=self.unity_folder_var.get().strip() or "Assets/{name}",
            apply_transform=self.apply_transform_var.get(),
            apply_modifiers=self.modifiers_var.get(),
            include_animation=self.animation_var.get(),
            include_textures=self.textures_var.get(),
            include_blend=self.include_blend_var.get(),
        )

    def persist(self):
        o = self.options()
        save_settings({
            "blender": self.blender_var.get(),
            "output": self.output_var.get(),
            "next_to_source": self.next_to_source_var.get(),
            "unity_folder": o.unity_folder,
            "apply_transform": o.apply_transform,
            "apply_modifiers": o.apply_modifiers,
            "include_animation": o.include_animation,
            "include_textures": o.include_textures,
            "include_blend": o.include_blend,
        })

    def start(self):
        if self.running():
            return
        files = self.files()
        blender = self.blender_var.get().strip()
        if not blender or not os.path.isfile(blender):
            messagebox.showerror(APP_NAME, "Please set the location of the Blender executable.")
            return
        output = None
        if not self.next_to_source_var.get():
            out = self.output_var.get().strip()
            if not out:
                messagebox.showerror(APP_NAME, "Please choose an output folder.")
                return
            output = Path(out)
            try:
                output.mkdir(parents=True, exist_ok=True)
            except OSError as exc:
                messagebox.showerror(APP_NAME, f"Cannot create output folder:\n{exc}")
                return
        self.persist()
        self.cancel_event.clear()
        self.file_progress.set(0)
        self.overall_progress.set(0)
        options = self.options()
        self.worker = threading.Thread(target=self._work, args=(files, output, blender, options), daemon=True)
        self.worker.start()
        self._update_buttons()

    def cancel(self):
        if self.running():
            self.cancel_event.set()
            self.overall_status_var.set("Cancelling…")

    def on_close(self):
        if self.running():
            if not messagebox.askyesno(APP_NAME, "A conversion is running. Cancel it and quit?"):
                return
            self.cancel_event.set()
            self.worker.join(timeout=5)
        self.persist()
        self.root.destroy()

    # ------------------------------------------------------------ background
    def _work(self, files, output, blender, options):
        post = self.events.put
        total = len(files)
        ok = failed = 0
        version = blender_version(blender)
        post(("log", f"Using {version or 'Blender'} ({blender})"))
        for i, f in enumerate(files):
            if self.cancel_event.is_set():
                break
            name = Path(f).name
            post(("log", f"[{i + 1}/{total}] Converting {name}"))

            def progress(frac, msg, _i=i, _name=name):
                post(("progress", _i, total, frac, f"{_name}: {msg}"))

            try:
                out = convert_file(Path(f), output, blender, options, progress,
                                   lambda m: post(("log", m)), self.cancel_event)
                ok += 1
                post(("done_file", out))
            except ConversionCancelled:
                post(("log", "Cancelled."))
                break
            except ConversionError as exc:
                failed += 1
                post(("log", f"FAILED {name}: {exc}"))
            except Exception as exc:  # unexpected; report and continue with the next file
                failed += 1
                post(("log", f"FAILED {name}: {type(exc).__name__}: {exc}"))
        post(("finished", ok, failed, total, self.cancel_event.is_set()))

    def _poll_events(self):
        try:
            while True:
                ev = self.events.get_nowait()
                kind = ev[0]
                if kind == "log":
                    self.log(ev[1])
                elif kind == "progress":
                    _, i, total, frac, msg = ev
                    self.file_progress.set(frac * 100)
                    self.overall_progress.set((i + frac) / total * 100)
                    self.file_status_var.set(msg)
                    self.overall_status_var.set(f"File {i + 1} of {total}")
                elif kind == "done_file":
                    self.last_output = Path(ev[1]).parent
                elif kind == "finished":
                    _, ok, failed, total, cancelled = ev
                    self.worker = None
                    summary = f"{ok} of {total} converted" + (f", {failed} failed" if failed else "")
                    if cancelled:
                        summary += " (cancelled)"
                    else:
                        self.overall_progress.set(100)
                    self.overall_status_var.set(summary)
                    self.file_status_var.set("Cancelled" if cancelled else "Finished")
                    self.log(summary)
                    self._update_buttons()
                    if failed and not cancelled:
                        messagebox.showwarning(APP_NAME, summary + ".\nSee the log for details.")
        except queue.Empty:
            pass
        self.root.after(50, self._poll_events)


def main():
    root = tk.Tk()
    root.title(APP_NAME + " — .blend → .unitypackage")
    root.minsize(640, 560)
    style = ttk.Style()
    try:
        style.theme_use("vista" if platform.system() == "Windows" else "clam")
    except tk.TclError:
        pass
    style.configure("B2U.Horizontal.TProgressbar", background="#3a9d3a", thickness=18)
    App(root, sys.argv[1:])
    root.mainloop()


if __name__ == "__main__":
    main()
