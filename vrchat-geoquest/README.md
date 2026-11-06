# GeoQuest: North Carolina, a Quest-friendly GeoGuessr-style VRChat world

You're dropped inside a real 360° street-level photo from somewhere in **North Carolina**,
from the Blue Ridge to the Outer Banks. Look around, point at
the NC map, drop a pin and lock in your guess. Closer guesses score more (up to
5,000 points a round). It plays multiplayer with a live scoreboard and saves your best score.

**Places:** two kinds, both from Mapillary street-level imagery (CC BY-SA), and **every place is in
North Carolina**. Both lists are already harvested and committed in `Assets/GeoQuest/Data/locations.json`.
* **360° photos** (3,051): you stand inside the photo and can look all around. Mapillary only has
  360° imagery in 44 NC counties, mostly along I-40, I-77, I-95, US-17, the Outer Banks, Asheville and
  Raleigh/Cary. Each county is capped at 200 places so Wake doesn't take over.
* **Flat photos** (4,086 regular road photos, up to 60 per county, at least 1.5 km apart): shown on a big
  screen in front of the map. These cover all 100 counties, including Boone, Johnston and Duplin,
  which have no 360° imagery at all.

The **Photo type** button in the lobby cycles **Mixed**, **360° only** and **Flat only**.
`coverage.png` shows where the 360° places are. (For trick rounds from just over the border, harvest
with `--outside-fraction 0.25` and rebuild the map with `python make_nc_map.py --neighbor-towns`.)

**The map** is bare: North Carolina alone on a blank background, with just its 100 county outlines. There are no neighbouring states, ocean, names, towns or labels, so you have to know your state (`python make_nc_map.py --labels` brings the towns and labels back), and it zooms to 32×. Scoring is scaled to the state: `5000 × e^(−km / 90)`. That's
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

1. **Add your Mapillary token to GitHub.** The place list is already harvested and committed
   (`Assets/GeoQuest/Data/locations.json`). The workflow still needs the token to refresh photo
   download links, which expire after about 30 days, and to harvest more places later. In your
   GitHub repo, go to **Settings > Secrets and variables > Actions > New repository secret**.
   Name it `MAPILLARY_TOKEN` and paste the `MLY|...` client token from
   https://www.mapillary.com/dashboard/developers. Don't commit the token to the repo.
2. **Host the photos.** Under **Settings > Pages**, set the source to **GitHub Actions**.
   Then go to **Actions > GeoQuest photos > Run workflow**.
   A normal run downloads the committed places' photos (about 20–30 minutes), resizes each to
   2048×1024 and publishes them to `https://<you>.github.io/<repo>/`. Tick **harvest_more**
   to also search Mapillary and Wikimedia Commons for new places, which takes about an hour. GitHub Pages sites are capped at 1 GB,
   so the JPEG quality adapts to fit everything: about 110 KB per flat photo and about 170 KB per 360° photo at 7,137 places.
   Ask for fewer places for sharper photos, or more for more variety. Past roughly 8,000 the
   photos start looking rough. Later runs reuse the published
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

## The world
* **Hangout**: where you spawn. It's an enclosed room with couches, a wall map of NC and a **TV**.
  Paste a video link into the box under it and everyone watches together. Play/pause and stop are
  shared, and volume is per person. It uses AVPro, which works on PC and Quest. On Quest, direct
  video links are the most reliable. The green **Play GeoQuest** button takes you to the game.
* **Game area**: you stand inside the photo with the map table in front of you. A **6 m × 3 m NC
  map is on the floor behind you**, with a rack of 12 pins. Grab a pin and drop it on the floor
  map to set your guess. That works the same as pointing at the table map, and everyone sees the
  pins. On the reveal, a red beam marks the real spot on the floor map. **Reset pins** puts them
  back on the rack, and the **Hangout** button takes you back.
* Only people in the game area count as players, so friends chilling in the hangout don't hold up rounds.

## How to play
* Press **START**. **Rounds** cycles 3/5/10, **Timer** cycles off, 30s, 60s, 120s, 180s and 300s, and
  **Photo type** cycles Mixed, 360° only and Flat only.
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
