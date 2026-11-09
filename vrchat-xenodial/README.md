# XenoDial: an alien-transformation watch world for VRChat

A pod has crashed in the woods. Inside is the **XenoDial**, a watch that turns you into one of
six aliens. Take it, spin the dial, slam the core, and you transform. Each alien changes how you
move (speed, jump, gravity and even your size) and gives you a power. Everyone in the instance
sees your alien and your powers. When the time runs out the watch turns red and has to recharge.

Everything is built from Unity primitives and synthesised sounds, so there are no outside assets
to download or license. It works on PC and Quest.

## The aliens

| # | Alien | Power | What changes |
|---|---|---|---|
| 1 | **Emberjack** | Throws fireballs where you point (VR: your hand, desktop: the centre of your screen) | Faster run, higher jump |
| 2 | **Bedrock** | Ground slam: a shockwave that smashes everything within 9 m | You become a **3.4 m giant** with a huge jump |
| 3 | **Zipwire** | Dash: a burst forward that smashes whatever you run through | Runs at 16 m/s |
| 4 | **Skyfin** | Flap: press again in mid-air (or jump in mid-air) to fly higher. You glide when you fall | Gravity drops to 30% |
| 5 | **Pip** | Hop | You shrink to **bug size** (25 cm eye height) and fit through the vault tunnel |
| 6 | **Shardling** | Crystal platforms: on the ground you get a step ahead; in the air you get a platform under your feet. Jump, then use it, to climb anything | Everyone can stand on your crystals for 30 s |

A transformation lasts **45 s**. The watch beeps for the last 5 s, then you turn back and it
recharges for 8 s. If you change back yourself, the recharge only takes 2 s.

## Using the watch

| | VR | Desktop |
|---|---|---|
| Take the watch | Use the crashed pod | Use the crashed pod |
| Open the dial | Tap the core on your left wrist with your right index finger | **Q** |
| Pick an alien | Tap its hologram | **1–6** or **← / →** |
| Transform | Tap the core again | **F** |
| Use your power | Pull either trigger | **Left click** or **F** |
| Change back early | Tap the core | **Q** |

In VR the watch sits on your left wrist and turns to face you. On desktop it sits at the
bottom-left of your view. Other players see the watch on your wrist, and your alien when you transform.
Your own alien is hidden from your own camera so it doesn't block your view. Turn on the **mirror** at the crash
site to see yourself.

## The world

```
                     Sky Tower (ring the bell)
                              |
                         crashed pod
  Speed Track -------- spawn + mirror -------- Training Arena
                              |
                     "too small" tunnel -> Pip Vault
```

* **Crash site** (spawn): the pod with the watch floating above it, a how-to sign, a mirror and a signpost.
* **Training Arena** (east): nine floating drones for fireballs and eight boulders for slams
  and dashes. They burst for everyone and rebuild after 6 s. A board counts your smashes.
* **Sky Tower** (north): a 24 m pillar. The ledges are 1.5 m apart, which is too far for a
  normal jump. Use Bedrock's jump, Skyfin's wings or Shardling's crystals to reach the bell at the top.
  Everyone in the instance hears it ring.
* **Speed Track** (west): an oval with a start/finish gate and a halfway gate. Laps are timed and your best lap
  is saved between visits with VRChat persistence.
* **Pip Vault** (south): a rock mesa whose only way in is a tunnel 45 cm tall. Walk into it as Pip and you're
  pulled through into the hidden room.

## How it's built

| Piece | File |
|---|---|
| The watch: wrist tracking, finger taps, keys, dial, timer, stats, powers | `Assets/XenoDial/Scripts/XenoDialWatch.cs` |
| Your synced alien: form, powers and effects for everyone (one per player, VRChat Player Objects) | `Assets/XenoDial/Scripts/XenoBody.cs` |
| Smashable drones and boulders | `XenoTarget.cs` |
| Lap timer and gates | `XenoRaceTimer.cs`, `XenoRaceGate.cs` |
| Pod, bell, vault tunnel, buttons | `XenoPod.cs`, `XenoBell.cs`, `XenoVaultDoor.cs`, `XenoButton.cs` |
| One-click scene builder (all geometry and the six alien models) | `Assets/XenoDial/Editor/XenoDialWorldBuilder.cs` (menu **XenoDial > Build World**) |
| Shaders: shaded toon colour with fog, and an additive hologram glow | `Assets/XenoDial/Shaders/` |
| Sound effects (synthesised) | `tools/make_sounds.py` → `Assets/XenoDial/Audio/` |
| `.unitypackage` / folder zip | `tools/make_unitypackage.py` |

How the pieces work:
* Size changes use `SetAvatarEyeHeightByMeters`. Your original height is restored when you change back,
  and kept for the alien if you swap avatars mid-transformation.
* Movement uses the player API (`SetWalkSpeed`, `SetRunSpeed`, `SetJumpImpulse`, `SetGravityStrength`, `SetVelocity`).
* The alien bodies are primitives that follow each player's position and rotation, scale to their avatar's height and swing
  their limbs while walking. Only the form and the last power are synced, a few bytes per action.
* Smashes are network events, so targets burst for everyone. Crystals have real colliders on every client.

### Why it's Quest-friendly
There are no realtime lights, shadows, textures or post-processing. Every mesh is a primitive with a tiny shader
that has the sun's shading built in, and the scenery is static-batched. The mirror is off until you turn it on.

## Setup

1. Create a **world** project in the VRChat Creator Companion (Worlds SDK **3.7.0 or newer**, which is needed for
   Player Objects and persistence).
2. Build the package: `python vrchat-xenodial/tools/make_unitypackage.py` writes `XenoDial.unitypackage` and
   `XenoDial-folder.zip`. In Unity, import the package with **Assets > Import Package > Custom Package**. If it won't
   import, close Unity and unzip `XenoDial-folder.zip` into the project folder (next to `Assets`).
   You can also just copy `vrchat-xenodial/Assets/XenoDial` into your project's `Assets` folder.
3. Unity asks **"Set up XenoDial now?"**. Click **Set it up**, or use **XenoDial > One-Click Setup** later.
   It imports TextMeshPro's essentials, creates `Assets/XenoDial/XenoDial.unity` with the whole world
   built in, saves it and adds it to Build Settings.
4. Open **VRChat SDK > Show Control Panel > Builder** and use **Build & Test**. To publish for Quest too, switch the
   platform to Android and upload again.

Re-run **XenoDial > Build World** whenever you change the builder. It replaces the old `XenoDial` object.

### Tuning
Everything is set in the builder's tables at the top of `XenoDialWorldBuilder.cs`: names, colours, speeds, jump,
gravity, size and cooldowns. Transformation time, recharge time, fireball range, slam radius and the desktop keys are
fields on the **XenoDial System** object in the scene. If fireballs don't leave along your hand on your
controllers, adjust **Hand Pointer Euler** there.

## Testing notes
None of this has run inside Unity or VRChat yet. The scripts are syntax-checked but not compiled
against the SDK. On the first Build & Test, check:
* the dial opens and tapping works with your avatar's finger bones (the watch falls back to controller tracking
  if an avatar has no finger bones);
* Bedrock and Pip sizes. VRChat clamps eye height to the world's limits, and the watch sets those to 0.1–5 m;
* that other players see your alien and powers (test with a second client: **Build & Test** with 2 clients).
