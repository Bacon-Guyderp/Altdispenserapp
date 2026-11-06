# GeoQuest: North Carolina, a Quest-friendly GeoGuessr-style VRChat world

You're dropped inside a real 360° street-level photo from somewhere in **North Carolina**,
from the Blue Ridge to the Outer Banks. Look around, point at
the NC map, drop a pin and lock in your guess. Closer guesses score more (up to
5,000 points a round). It plays multiplayer with a live scoreboard and saves your best score.

**Places:** thousands of 360° panoramas sampled evenly across the whole state, so you get
back roads and small towns as well as Charlotte and Raleigh. No two places are within 2 km
of each other. **Every place is in North Carolina.** (If you ever want trick rounds from just over
the border, harvest with `--outside-fraction 0.25` and rebuild the map with
`python make_nc_map.py --neighbor-towns`.) The main source is **Mapillary**
street-level imagery (CC BY-SA), topped up with Wikimedia Commons.

**The map** is a county map of NC (`Assets/GeoQuest/Textures/NCMap.png`) with about 40 towns marked, and it zooms to 32×. Scoring is scaled to the state: `5000 × e^(−km / 90)`. That's
roughly 4,500 points at 10 km, 2,900 at 50 km and 500 at 200 km. The reveal tells you the
county and the nearest town, for example "14 km from Raleigh, Wake County, NC". Photos are
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
| World map (for a whole-world version) and lobby sky | `tools/make_world_map.py`, `tools/make_lobby_panorama.py` |
| Place harvesters and photo site builder | `tools/harvest_mapillary.py`, `tools/harvest_commons.py`, `tools/build_site.py` |
| NC county map (US Census + Natural Earth, public domain) | `tools/make_nc_map.py` |
| Hosting on GitHub Pages | `.github/workflows/geoquest-photos.yml` |

### Why Quest-friendly
* Everything is unlit, uses a few hundred triangles and has no realtime lights, shadows or post-processing.
* The map is a single 4096×2048 texture, compressed to ASTC 6×6 on Android.
* One 2048×1024 photo is in memory at a time. The previous one is disposed and mipmaps are skipped.
* TextMeshPro uses the mobile SDF shader.

## Setup

1. **Get a Mapillary token** (free). At https://www.mapillary.com/dashboard/developers,
   register an app and copy its *Client Token* (`MLY|...`). In your GitHub repo, add it under
   **Settings > Secrets and variables > Actions** as `MAPILLARY_TOKEN`.
   Without a token the workflow falls back to Wikimedia Commons only, which has far fewer NC 360s.
2. **Host the photos.** Under **Settings > Pages**, set the source to **GitHub Actions**.
   Then go to **Actions > GeoQuest photos > Run workflow**.
   The run harvests about **5,000 places** by default, resizes each photo to 2048×1024 and
   publishes them to `https://<you>.github.io/<repo>/`. GitHub Pages sites are capped at 1 GB,
   so the JPEG quality adapts to fit everything: about 190 KB per photo at 5,000 places.
   Ask for fewer places for sharper photos, or more for more variety. Past roughly 8,000 the
   photos start looking rough. Expect 1–2 hours on the first run. Later runs reuse the published
   list and the cached photos, and only add more.
   Photos are hosted on `github.io` because it's on VRChat's trusted image-host list, so
   players don't need *Allow Untrusted URLs*.
3. **Unity project.** Create a world project in the VRChat Creator Companion
   (Worlds SDK **3.7.0 or newer**, which is needed for Player Objects and persistence). Copy the
   `Assets/GeoQuest` folder in. Import TMP Essentials from
   **Window > TextMeshPro > Import TMP Essential Resources**.
4. **Build the world.** Open **GeoQuest > Build World** and paste `https://<you>.github.io/<repo>/p/`
   as the photo base URL. Click **Download locations.json**, then **Build World**. Save the scene.
5. **Upload.** Build & Test on PC, then switch the build target to **Android** and upload the
   same world again for Quest.

You can re-run **Build World** whenever you harvest more places. It replaces the old `GeoQuest` object.

## How to play
* Press **START**. **Rounds** cycles 3/5/10 and **Timer** cycles off, 30s, 60s, 120s, 180s and 300s.
* VR: point a controller at the map and pull the trigger. Desktop: aim the screen centre at the map and click.
* Use **Zoom +** (it centres on your pin), **Zoom -** and the arrow buttons to place your pin precisely, then press **GUESS!**.
* The round ends when everyone has guessed or the timer runs out. The reveal shows the real spot,
  everyone's pins, distances and the photo credit. **Skip photo** rerolls a bad image.
* Score per round is `5000 × e^(−km / 90)`. Your all-time best total is saved with VRChat persistence.

## Credits and licences
The photos are by Mapillary contributors (CC BY-SA 4.0) and Wikimedia Commons contributors (CC BY / CC BY-SA / CC0 / GFDL). The
author and licence of each photo are shown in-world during the reveal and listed on the
Pages site (`index.html`). The map data is from the US Census Bureau and Natural Earth (public domain).

## Want the whole world instead?
Run `python harvest_commons.py --region world`, then pick `WorldMap.png` in the builder.
The builder reads the region from `locations.json` and switches the map bounds and scoring automatically.
