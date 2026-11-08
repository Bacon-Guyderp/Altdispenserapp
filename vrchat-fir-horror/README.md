# Fir Warning: a Quest-friendly VRChat horror world about animatronic talking trees

*"Don't get fooled by imitations. Unauthorized copies will not be tolerated."*

It's Christmas Eve, 1996, and the talking-tree factory has shut down for good. The trees never switched off.
You and your friends are the night clean-up crew. You go room by room through the factory and finish
on the Recall Floor, where you burn the "imitation" trees in the furnace while **the Original** hunts you.

* **About 45 minutes** for a group playing for the first time, across six chapters.
* **1–8 players**, co-op. Progress, doors and items are synced, and late joiners can catch up.
* **33 placed jumpscares**, plus scares tied to events (finding the last item, wrong codes, getting caught) and random whispers, rustles and flickers between them.
* **Quest-friendly:** no realtime lights or shadows, no post-processing, one tiny shader, 512 px textures
  (ASTC on Android) and about 1 MB of mono Vorbis audio. The whole package is under 3 MB.

Everything (models, sounds, posters) is generated from code in this folder, so it carries no asset licences.
The songs use *O Tannenbaum*, which is public domain. The company in the world is the fictional
"Evergreen Novelty Corp.". This is a fan-made world and isn't affiliated with any toy company.

## The chapters

| # | Room | Goal | Rough time | What goes wrong |
|---|---|---|---|---|
| — | Lobby | Story, rules, personal options, **START** | 2 min | Nothing. It's the one safe room. |
| 1 | **Showroom** | Find 3 batteries (under the long table, behind the curtain, in the presents), then use the power panel | 6 min | The 2.6 m Original greets you, the trees sing and follow you with their eyes, something grabs you under the table, a tree stands in the corner until you look at it, and the lights die when the last battery is found |
| 2 | **Stockroom** | Find 4 fuses in the shelf aisles, then use the fuse box | 9 min | **The stalker:** a tree that only moves when nobody is looking at it. Also a crate that screams, a falling box, singing from the dark and a face in the cross aisle |
| 3 | **Voice Lab** | Collect 4 voice tapes (on the conveyor, in the booth, on the desks), then play them on the deck in the order on the whiteboard | 8 min | Tree heads on the conveyor stop and turn to you, the booth singer screams, and the wrong order makes every tree scream |
| 4 | **Offices** | Read 4 memos (each gives one digit of the door code), then use the keypad | 8 min | The stalker is back among the cubicles. A phone rings, the employees turn round in their chairs, and something waits in a cubicle |
| 5 | **Living Room** | The catalogue photo, but wrong. Put the small, medium and large Santa hats on the matching trees | 5 min | Every tree turns to stare when a hat goes on, the family sings out of tune, and something is behind you at the fireplace |
| 6 | **Recall Floor** | Carry 5 small imitation trees (they whimper) to the furnace | 8 min | **The Original hunts you.** It's faster, and it creeps forward slowly even while you're watching it. A tree also drops from the ceiling |
| 7 | Exit | Your time, the credits and the way back to the lobby | — | One last scare |

The door code and tape order are randomised every game. If the group is stuck for six minutes, a hint
appears under everyone's view, and another every four minutes after that.

### The stalker
The owner of the stalker (normally the instance master) checks every player's head direction each frame,
with a line-of-sight test against the walls. **If anyone is looking at it, it freezes.** If nobody is,
it walks toward the nearest player on a baked NavMesh. You hear it rustle and creak when it moves, and
a heartbeat plays when it's within 7 m. If it reaches you, you get a face scare and you're sent back to the
start of that room. There's no game over, so the 45-minute run keeps going. Closed doors carve the
NavMesh, so it can't follow you into rooms you haven't unlocked.

### Darkness without lights
There isn't a single Light in the world. Every surface uses the `FirHorror/Lantern` shader, which lights
things by how close they are to **your** camera and how near the middle of your view they are. Every
player gets their own torch, at no cost and with correct stereo. Udon drives three global shader values,
which control blackouts, flicker and brightness. Tree eyes have a faint glow, so in a blackout all
you can see is pairs of eyes.

### Throwable toilets
There are two toilets in the lobby and a three-stall staff restroom by the Offices entrance. Pick one up and
throw it. Press **Use** while you hold one to flush it. A hit on the hunting tree makes it scream and freezes it
for 4 seconds, so a teammate with a toilet can cover a friend who's being chased. The toilets go back to their
places when a new game starts. Code: `Scripts/FirToilet.cs`.

### Comfort options (lobby, per player)
* **Brightness:** dark, normal or bright.
* **Flashing lights:** on or off. Off removes the strobing flicker, but blackouts still happen.
* **Jumpscares:** full, or sound only (no faces).

## Setup

1. Create a world project in the **VRChat Creator Companion** (Worlds SDK 3.5 or newer, with UdonSharp).
2. Import **`FirWarning.unitypackage`** with **Assets > Import Package > Custom Package**. Build it with
   `python tools/make_unitypackage.py`. If the package won't import, unzip **`FirWarning-folder.zip`**
   into the project folder instead, so you get `Assets/FirHorror`.
3. Unity asks **"Set up Fir Warning now?"**. Click **Set it up**, or use **Fir Warning > One-Click Setup**
   later. It imports TextMeshPro's essentials, creates and saves `Assets/FirHorror/Fir Warning.unity`, builds
   the world, bakes the NavMesh and adds the scene to Build Settings.
4. **VRChat SDK > Show Control Panel > Builder > Build & Test**. For Quest, switch the platform to
   **Android** and upload the same world again.

**Fir Warning > Build World** rebuilds everything in the open scene, replacing the old `FirWarning` object.
Re-run it after changing the builder, and save the scene first so the NavMesh bake has a home.

### Regenerating the assets
```
cd tools
python make_audio.py       # numpy + ffmpeg: writes Assets/FirHorror/Audio/*.ogg
python make_textures.py    # numpy + Pillow: writes Assets/FirHorror/Textures/*.png
python make_unitypackage.py
```

## How it's built

| Piece | File |
|---|---|
| Story state, doors, items, HUD and subtitles, lighting, face and behind-you scares | `Assets/FirHorror/Scripts/FirGameManager.cs` |
| Talking-tree animatronic (sway, stare, talk, sing, scream, eye tracking) | `Scripts/FirAnimatronic.cs` |
| The tree that moves when unwatched (NavMeshAgent, view checks, synced) | `Scripts/FirStalker.cs` |
| Scripted scares (face, reveal, sound, drop, behind you, group scream, peek, flicker) | `Scripts/FirJumpscare.cs` |
| Random between-scare ambience | `Scripts/FirAmbience.cs` |
| Collectibles, carried items, hat and furnace sockets | `Scripts/FirItem.cs`, `FirCarryItem.cs`, `FirSocket.cs` |
| Tape deck and keypad | `Scripts/FirSequenceLock.cs`, `FirKey.cs` |
| Buttons and the conveyor belt | `Scripts/FirButton.cs`, `FirConveyor.cs` |
| Throwable toilets | `Scripts/FirToilet.cs` |
| The whole world, built from code (rooms, procedural tree meshes, props, NavMesh, descriptor) | `Editor/FirWorldBuilder.cs` |
| One-click setup | `Editor/FirSetup.cs` |
| The lantern shader | `Shaders/FirLantern.shader` |
| Sound and texture generators, package builder | `tools/` |

### Tuning
* **Length:** the hint timer is `hintAt` in `FirGameManager.EnterPhase` (6 min, then every 4 min). The
  stalker's speeds and catch distance are on the `Stalker` object.
* **Scare density:** every scripted scare is a `Scare …` object under its chapter. Each one is a trigger
  box with a `FirJumpscare`. Move it, change its `kind` or `delay`, or delete it. `FirAmbience.minGap` and
  `maxGap` set how often the random ones happen.
* **Performance:** room geometry is static-batched. If you want more Quest headroom, bake Occlusion Culling
  (**Window > Rendering > Occlusion Culling > Bake**). The walls are already marked as occluders.

### Not tested in Unity yet
This was written without access to Unity or the VRChat SDK, so it hasn't been compiled or played. Expect
some first-run fixes in UdonSharp, and test with at least two people (one PC, one Quest) before publishing.
The likely trouble spots are the stalker's NavMesh, the reach of the tripwire triggers, and hat-socket sizes
in VR.
