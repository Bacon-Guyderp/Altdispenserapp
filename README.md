# Blend2Unity

A small desktop app that converts Blender `.blend` files into Unity
`.unitypackage` files, with a progress bar for each file and for the whole batch.

![](docs/screenshot.png)

## How it works

1. Blender is run in the background (`blender -b`) on each `.blend` file. It
   exports the scene as an FBX set up for Unity (Y-up, -Z forward, unit scale
   applied, no leaf bones). Every texture the file uses, including packed
   and generated or painted images, is copied into a `Textures/` folder.
2. The FBX and textures are packed into a `.unitypackage` under
   `Assets/<file name>/`. Import it in Unity with **Assets → Import Package →
   Custom Package…**.

Asset GUIDs come from each asset's path, so importing a newer package
for the same model updates it in place instead of making duplicates.

## Requirements

- Python 3.8+ with Tkinter. It ships with the python.org installers for Windows and macOS.
  On Debian/Ubuntu, run `sudo apt install python3-tk`.
- [Blender](https://www.blender.org/download/) 2.93 or newer (tested with 4.2 LTS).
  The app looks for it in the usual install locations, or you can point it at the executable.

No other Python packages are needed.

## Usage

```sh
python blend2unity.py                 # open the GUI
python blend2unity.py a.blend b.blend # open the GUI with files already added
```

1. Check that the **Blender** path is correct. Use **Detect** or **Browse…** if it isn't.
2. **Add files…** or **Add folder…** (every `.blend` file under the folder is added).
3. Pick an output folder, or tick **Save next to each .blend file**.
4. Click **Convert**. You can cancel at any time. The log shows Blender's output and any warnings,
   such as missing textures.

Options:

| Option | Effect |
| --- | --- |
| Unity folder | Where assets go inside the project. `{name}` is replaced by the file name. |
| Include textures | Copy the images used by materials into `Textures/`. |
| Include animation | Bake actions and animations into the FBX. |
| Apply modifiers | Export meshes with their modifiers applied. |
| Apply transform | Bake the axis conversion so objects don't import with a -90° X rotation. |
| Also include original .blend | Ship the `.blend` too. Unity can only import it if Blender is installed. |

Settings are saved in `~/.blend2unity.json`.

### Command line

The same conversion works without the GUI:

```sh
python converter.py model.blend -o out/ [--blender /path/to/blender] [--include-blend] [--no-animation] ...
```

You can also set the `BLENDER_PATH` environment variable instead of passing `--blender`.

## Building a standalone executable (optional)

```sh
pip install pyinstaller
pyinstaller --onefile --windowed --name Blend2Unity --add-data "blender_export.py:." blend2unity.py
```

(On Windows, use `;` instead of `:` in `--add-data`.)

## Tests

```sh
python -m unittest discover tests                        # package-format tests
BLENDER_PATH=/path/to/blender python -m unittest discover tests   # plus end-to-end tests
```

## Files

- `blend2unity.py`: Tkinter GUI
- `converter.py`: finds and runs Blender, builds the package, and provides the CLI
- `blender_export.py`: script that runs inside Blender to export the FBX and textures
- `unitypackage.py`: writes the `.unitypackage` (tar.gz) format
