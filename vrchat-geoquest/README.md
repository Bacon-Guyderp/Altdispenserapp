# GeoQuest: a Quest-friendly GeoGuessr-style VRChat world

You're dropped inside a real 360° photo from somewhere on Earth. Look around, point at
the world map, drop a pin and lock in your guess. Closer guesses score more (up to
5,000 points a round). It plays multiplayer with a live scoreboard and saves your best score.

**Places:** thousands of geotagged 360° panoramas from Wikimedia Commons, spread
across the globe tile by tile (at most 2 per 1°×1° cell and 120 per country). Photos are
**streamed one at a time** with VRChat's Image Loading. The upload stays a few MB and
Quest memory stays flat however many places you add.

## How it's built

| Piece | File |
|---|---|
| Game loop, synced state, photo streaming, scoring, scoreboard | `Assets/GeoQuest/Scripts/GeoGameManager.cs` |
| Map: laser pointing (VR) or screen-centre aiming (desktop), zoom and pan, pins, reveal lines | `Assets/GeoQuest/Scripts/MapBoard.cs` |
| Per-player guess and score (VRChat Player Objects) | `Assets/GeoQuest/Scripts/PlayerGuess.cs` |
| Buttons | `Assets/GeoQuest/Scripts/GeoButton.cs` |
| One-click scene builder | `Assets/GeoQuest/Editor/GeoQuestWorldBuilder.cs` (menu **GeoQuest > Build World**) |
| 360° photo shader (sampled by view direction, no seam, unlit, works with single-pass stereo) | `Assets/GeoQuest/Shaders/` |
| World map (public-domain Natural Earth) and lobby sky | `tools/make_world_map.py`, `tools/make_lobby_panorama.py` |
| Place harvester and photo site builder | `tools/harvest_commons.py`, `tools/build_site.py` |
| Hosting on GitHub Pages | `.github/workflows/geoquest-photos.yml` |

### Why Quest-friendly
* Everything is unlit, uses a few hundred triangles and has no realtime lights, shadows or post-processing.
* The map is a single 4096×2048 texture, compressed to ASTC 6×6 on Android.
* One 2048×1024 photo is in memory at a time. The previous one is disposed and mipmaps are skipped.
* TextMeshPro uses the mobile SDF shader.

## Setup

1. **Host the photos** (one time). Push this repo to GitHub. Under **Settings > Pages**, set
   the source to **GitHub Actions**. Then go to **Actions > GeoQuest photos > Run workflow**.
   The run harvests about 2,500 places (you can raise the target), resizes each photo to
   2048×1024 and publishes them to `https://<you>.github.io/<repo>/`. Expect 1–2 hours on
   the first run because the script throttles itself to stay polite to Wikimedia. GitHub Pages
   sites should stay under 1 GB, which is roughly 2,500–3,000 photos at the default quality.
   Photos are hosted on `github.io` because it's on VRChat's trusted image-host list, so
   players don't need *Allow Untrusted URLs*.
2. **Unity project.** Create a world project in the VRChat Creator Companion
   (Worlds SDK **3.7.0 or newer**, which is needed for Player Objects and persistence). Copy the
   `Assets/GeoQuest` folder in. Import TMP Essentials from
   **Window > TextMeshPro > Import TMP Essential Resources**.
3. **Build the world.** Open **GeoQuest > Build World** and paste `https://<you>.github.io/<repo>/p/`
   as the photo base URL. Click **Download locations.json**, then **Build World**. Save the scene.
4. **Upload.** Build & Test on PC, then switch the build target to **Android** and upload the
   same world again for Quest.

You can re-run **Build World** whenever you harvest more places. It replaces the old `GeoQuest` object.

## How to play
* Press **START**. **Rounds** cycles 3/5/10 and **Timer** cycles off, 30s, 60s, 120s, 180s and 300s.
* VR: point a controller at the map and pull the trigger. Desktop: aim the screen centre at the map and click.
* Use **Zoom +** (it centres on your pin), **Zoom -** and the arrow buttons to place your pin precisely, then press **GUESS!**.
* The round ends when everyone has guessed or the timer runs out. The reveal shows the real spot,
  everyone's pins, distances and the photo credit. **Skip photo** rerolls a bad image.
* Score per round is `5000 × e^(−km / 1500)`. Your all-time best total is saved with VRChat persistence.

## Credits and licences
The photos are by Wikimedia Commons contributors under CC BY / CC BY-SA / CC0 / GFDL. The
author and licence of each photo are shown in-world during the reveal and listed on the
Pages site (`index.html`). The map data is from Natural Earth (public domain).
