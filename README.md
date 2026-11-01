# Blend2Unity

A Windows app, a single `Blend2Unity.exe`, that converts Blender `.blend`
files into Unity `.unitypackage` files. It shows progress for each file and for the whole batch.

![](docs/screenshot.png)

## Requirements

- **Windows 10 or 11.** The exe uses .NET Framework 4.8, which is built into Windows, so
  there's nothing to install for the app itself.
- **[Blender](https://www.blender.org/download/)** 2.93 or newer. Only Blender can read
  `.blend` files, so the app runs it in the background. It looks for Blender in the usual install
  locations (including Steam), or you can point it at `blender.exe`.

You don't need Python. The small script that Blender runs is built into the exe.

## Usage

1. Run `Blend2Unity.exe`. Check that the **Blender** path is correct; use **Detect** or **Browse…** if it isn't.
2. Add `.blend` files with **Add files…** or **Add folder…**, or drag files or folders onto the window.
   You can also drop `.blend` files onto `Blend2Unity.exe` itself.
3. Pick an output folder, or tick **Save next to each .blend file**.
4. Click **Convert**. The top bar shows the current file's progress and the bottom bar shows the whole batch.
   **Cancel** stops right away. The log shows Blender's output and any warnings, such as missing textures.
5. In Unity, use **Assets → Import Package → Custom Package…** and pick the `.unitypackage`.

| Option | Effect |
| --- | --- |
| Unity folder | Where assets go inside the Unity project. `{name}` is replaced by the file name. |
| Include textures | Copy every image the materials use, including packed and painted images, into `Textures/`. |
| Include animation | Bake actions and animations into the FBX. |
| Apply modifiers | Export meshes with their modifiers applied. |
| Apply transform | Bake the axis conversion so objects don't import with a -90° X rotation. |
| Also include original .blend | Ship the `.blend` too. Unity can only import it if Blender is installed. |

Settings are saved in `%APPDATA%\Blend2Unity\settings.ini`.

## How it works

1. For each file, Blender runs in the background with the built-in export script
   (`src/BlenderExport.py`). It writes an FBX set up for Unity (Y-up, -Z forward, unit scale applied,
   no leaf bones) and copies the textures into `Textures/`.
2. The app packs them into a `.unitypackage` under `Assets/<file name>/`. A `.unitypackage` is a
   tar.gz with one folder per asset (`src/UnityPackage.cs`). Asset GUIDs come from each asset's path,
   so importing an updated package replaces the old assets instead of making duplicates.

## Building the exe

Install the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer) on Windows, Linux or macOS, then run:

```sh
dotnet build Blend2Unity.csproj -c Release
```

The output is `bin/Release/Blend2Unity.exe`, a single file you can copy anywhere.
The GitHub Actions workflow builds it on every push and attaches it to each GitHub release.

## Tests

The conversion code is tested cross-platform:

```sh
dotnet run --project tests                                    # package-format tests
BLENDER_PATH=/path/to/blender dotnet run --project tests      # plus end-to-end conversions
```

## Source

- `src/MainForm.cs`: the window (WinForms)
- `src/Converter.cs`: finds and runs Blender and parses its progress
- `src/UnityPackage.cs`: `.unitypackage` writer
- `src/BlenderExport.py`: export script built into the exe and run inside Blender
