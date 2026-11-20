# Fir Warning: a Quest-friendly VRChat horror world about animatronic talking trees

The singing Douglas firs were recalled after "the incident". Tonight one of them got up, and it only moves
while nobody is looking at it. Get through six rooms, solve each one's puzzle to unlock the next, and lead
the tree to the furnace. Made for two players, about 45 minutes. Everything is fullbright or lit by your
own "lantern" (no realtime lights), so it runs on Quest.

## The lobby (the start room)

* **North wall:** the title, the story, how to play, a status board, **START** (starts a game, or joins
  one that's running) and **RESET**.
* The lobby is a safe room: the tree never comes in, and it stays lit during the game.
* **West wall:** a small button to the **systems check room**. It has the 11 script tests that prove
  UdonSharp is running in your build. Press them all; each says PASS or FAIL.

## The game

Until someone presses START, the map is an open, lit museum of gags. START pulls everyone into the first
room. The map goes dark, so you only see by your lantern. Every exit is locked until its room's puzzle is
solved, 12 jump-scare trees arm, and the hunting tree starts at the far end.

| Room | Puzzle |
|---|---|
| Gift Wrap Hall | Find 5 batteries, then press the power box by the exit |
| Rainbow Hall | 4 glowing numbers are hidden in the hall. Type them in rainbow order (red, yellow, green, blue) |
| Toy Workshop | 4 numbered notes give an order. Press the toys on the exit panel in that order |
| Chimney Lounge | Find 4 ornaments, then hang them on the bare tree |
| Talent Show | The stage manager's memo: performers on stage, then seats in the house (the babies don't count) |
| The Bone Yard | Open 3 gas valves, then pull the furnace lever. The tree goes into the fire, and you escape |

The codes and the toy order change every game. Each room's task board, by its door, tracks your progress.

**The hunting tree** freezes whenever any player has it near the middle of their view with nothing in the
way. Otherwise it walks toward the nearest player, through the doorways, faster in later rooms. You hear a
heartbeat as it gets close. If it reaches you, a tree face fills your view and you're sent back to the
room's entrance while it backs off. **Throw a toilet at it** (there are 12 around the map) to stun it for
6 seconds. A wrong code or toy also earns you a tree in the face. Random whispers and rustles play behind
you.

## The Root Cellar: the horror expansion

Through the doorway in the lobby's east wall is a potting shed with its own START. It's a second,
harder game, separate from the first (both can even run at once).

| Room | Puzzle |
|---|---|
| The Greenhouse | Find 4 seed packets, then plant them in the big pot |
| The Sawmill | Numbers carved into 4 stumps glow red, yellow, green and blue; type them in that order |
| The Tool Room | 4 notes give an order; press the tools on the lock in that order |
| The Generator | Find 3 fuses, then fix the fuse box |
| The Woodchipper | The foreman's code: how many skeletons work the night shift, then how many saw blades hang in the sawmill. Both trees go into the chipper |

* **Two hunting trees:** the second wakes up when you reach the Tool Room.
* **The lights fail:** for a moment every so often.
* **Also:** 10 more jump scares, 10 toilets, and 12 skeletons on the night shift.

## Boneanza Park: the amusement park

The **Station** board in the lobby (by the west wall) teleports you to the park, and there's a "Back to
the lobby" button by the park gates. Future far-off areas get Station buttons too.

* **5 working rides:** you sit in a seat (a VRChat station) and get carried along; jump to get off.
  * the big wheel (its gondolas stay level)
  * the carousel (horses bob)
  * the spinning teacups
  * a drop tower that climbs, hangs and drops, with a scream
  * a ghost train whose carts loop through a pitch-black shed full of things that jump out
* **In step for everyone:** the rides move by the server clock, like Bonetown's wanderers, so every player
  sees the same thing.
* **30 skeletons, each voiced:**
  * snack vendors, game booths (ring toss, test your strength, dunk the skeleton) and a ticket seller
  * a fortune teller, a juggler, a clown and a balloon seller
  * ride operators, plus riders on the wheel, the carousel and the teacups
  * 8 wanderers: kids racing round the teacups, their parent chasing them, and visitors
* **20 gags** (`Data/park_gags.txt`) along the fences, and **7 always-armed jump scares** (5 on the ghost
  train).

## Precinct 206: deadpan cop comedy

Reached from the Station: an original police-station gag area in the spirit of the deadpan spoof films.

* **35 gags** (`Data/precinct_gags.txt`). Signs, frames and talking trees: the "take a number" sign stuck
  on 7 since 1987, the crime map with one pin, the evidence that's still warm, the officer of the month
  (the coffee machine)...
* **The rooms:** a front desk, the bullpen, a line-up of five identical trees, an interrogation room
  (the suspect is a vending machine), the chief's office, a briefing room, the evidence locker and a
  holding cell.
* **14 skeleton cops,** each voiced, in police caps:
  * Sgt. Marrow at the front desk, Detective Femur grilling the vending machine, and Captain Sternum.
  * Radius tailing Ulna, who knows he's being tailed.
  * Vertebra in endless hot pursuit of Pelvis round a desk.

## Pine Pete's Pizza Palace: the night shift

Reached from the Station: an original take on the haunted-pizzeria genre, with Christmas-tree mascots.

**The night.** Press START in the security office (at the back, through either staff hall).
* **The goal:** survive from 12 AM to 6 AM, about 4.5 minutes.
* **The animatronics:** three of them creep from the stage toward your doors, room by room, and you can
  see them in the building.
  * Pine Pete (chef hat) comes down the west side.
  * Holly (bow) comes down the east.
  * Sprucey (drum) picks a side.
* **Doors:** close a door when one is in it, and it goes back to the stage. Leave it open and it gets you.
* **Lights:** a door light shows who's there.
* **Cameras:** the 6 camera buttons show who's where on the monitor.
* **Power:** doors and lights drain it. Keep both doors shut and it runs out after about 3.5 minutes; then
  the doors open, the lights die, and Pete comes.
* **The jump scare:** only players inside the office see the face when someone's caught.
* **The voicemail:** the office phone plays a 48-second message from the last night guard (he's a
  skeleton now; unrelated, probably), with captions.

**By day:** a dining room, the stage, a party table, an arcade, the counter, the kitchen and Parts &
Service. There are 7 voiced staff and guests (one of them mops the dining room) and 12 gags.

## Pixel Hollow: a retro pixel-RPG zone

Reached from the Station: an original, gentle pixel-RPG adventure. It has blocky trees, a checkerboard
path, an inn, a shop and skeleton villagers.

* **Three encounters block the path:** a Shy Pinecone, a Grumbling Stump and a Nervous Lamp.
  * Each has a battle menu: BONK, CHAT (three things to say each round), SNACK and PARDON.
  * Say the right things and its FRIENDSHIP fills; then PARDON makes a friend, and it steps aside.
  * BONK sends you back to the start; PARDON too early doesn't work.
  * The text types itself out, RPG-style.
* **A tile gate:** tapping a tile flips it and its neighbours. Light them all to open it.
* **Each player has their own adventure:** your monsters step aside, and your gate opens, only for you.
* **Also:** a sparkling save point, a chest (with a smaller chest inside), and 10 gags.
* **An original skeleton duo at Castle Cardboard:** lanky lighthouse-keeper Captain Lumen (raincoat,
  knit hat, lantern) and tiny Sir Rattleton (saucepan helmet, cardboard armour, wooden sword). Each
  has their own line, plus their combo act, *The Quest*.

## 40 new skeletons and 15 new moves

There are 40 more skeletons, each with its own colour and voiced gag:

| Where | How many |
|---|---|
| The original rooms' free wall spots | 18 |
| Doormen by the lobby's south door | 2 |
| Bonetown picnickers | 4 |
| New Bonetown wanderers | 4 |
| The Root Cellar's night shift | 12 |

The 15 new moves are twirl, boo lunge, salute-and-march, head off, moonwalk, jumping jacks, head spin,
robot, deep bow, air guitar, chicken dance, rattle-shiver, peekaboo, can-can and limbo.

## Bonetown: the expansion

Go through the doorway in the lobby's west wall to reach a sunny little town where the skeletons live.
It's an original take on the life-sim genre, and none of it is scary.

* **10 residents at home**, each with a voiced gag:
  * Mint is just moving in, and Peach dances with a broom.
  * Lavender is sleepy, Coral works out, Sky composes songs, and Navy hides from the landlord.
  * Rust runs the Snack Shack, and Olive reads the news (a ticker rolls the headlines).
  * Cream keeps tripping over the rug, and Plum keeps eating.
* **10 wanderers** stroll, jog and play tag round the plaza and the fountain: Lime, Amber, Turquoise,
  Crimson, Cocoa, Neon, Magenta, Ivory, Denim and Bubblegum. Press one to say hi, and it stops, turns to
  you, waves and talks, then carries on. They're harmless and walk right through you. Where they are
  comes from the server clock, so everyone sees them in the same place with no network traffic.
* **The Bonetown Band** is Scarlet, Cobalt, Emerald and Mustard, on the bandstand. Each has its own line,
  and the console has 4 combo acts: The Squabble, Town Band, The Proposal and the Bonetown Finale.
* **22 more gags** (`Data/town_gags.txt`): signs, frames, pop-ups, spinners, waddlers and talking trees.
* That's 24 new skeleton colours. The game's tree never goes to Bonetown.

## Gags (all game long)

* **53 original gags** and **12 skeleton routines** line the Gift Wrap Hall, Toy Workshop, Chimney Lounge
  and Bone Yard.
* **The RED SKELETON closet** is in the Chimney Lounge.
* **The rainbow of skeletons** in the Rainbow Hall, and **the skeleton crew** on the Talent Show stage:
  17 skeletons (your uploaded model), each with its own voiced gag.
* **7 combo acts** run from the consoles.
* **30 throwable babies** fill the audience seats.
* Anything someone presses plays for everyone. Walk-in gags play for whoever walks in.

## Networking

Every shared thing is a few manually synced ints. Whoever presses something takes ownership, changes them
and sends them, and every client applies the same state.

* **FirHunt:** running, round, stage, items found, stuns, catches. The puzzle codes come from the round
  number, so they're never sent.
* **FirShare:** the last gag pressed.
* **FirStalker:** the tree's position, sent by its owner 4 times a second.

Late joiners get the current state quietly: no teleport, no replayed gags. They press START to join.

## Setup

1. Create a world project in the **VRChat Creator Companion** (Worlds SDK 3.5 or newer, with UdonSharp).
2. Import **`FirWarning.unitypackage`** with **Assets > Import Package > Custom Package**. Build it with
   `python tools/make_unitypackage.py`. If the package won't import, unzip **`FirWarning-folder.zip`**
   into the project folder instead, so you get `Assets/FirHorror`.
3. Unity asks **"Set up Fir Warning now?"**. Click **Set it up**, or use **Fir Warning > One-Click Setup**
   later. It imports TextMeshPro's essentials, creates and saves `Assets/FirHorror/Fir Warning.unity`, builds
   the world and adds the scene to Build Settings.
4. **VRChat SDK > Show Control Panel > Builder > Build & Test**. For Quest, switch the platform to
   **Android** and upload the same world again.

**Fir Warning > Build World** rebuilds everything in the open scene, replacing the old world object.
Re-run it after changing the builder. It works in any open scene, saved or not.

### Regenerating the assets
```
cd tools
python make_audio.py       # numpy + ffmpeg: writes Assets/FirHorror/Audio/*.ogg
python make_textures.py    # numpy + Pillow: writes the source PNGs to tools/textures/
python bake_textures.py    # packs them into Assets/FirHorror/Data/textures_baked.txt (run after any texture change)
python make_voices.py      # espeak-ng + mbrola: skeleton voices (add --rainbow / --combo for those lines)
python make_unitypackage.py
```

## How it's built

* `Assets/FirHorror/Editor/FirCheckBuilder.cs` builds the whole world: lobby, systems check room, the six
  rooms, gags, skeletons, the game, materials and baked textures.
* `Assets/FirHorror/Scripts/` holds the UdonSharp scripts:
  * FirHunt, FirHuntButton, FirStalker: the game
  * FirGag, FirSkeletonGag, FirCloset, FirRainbowSkeleton, FirSkeletonCombo: the gags
  * FirShare: shares pressed gags with everyone
  * FirWanderer, FirTicker: Bonetown's walkers and the news ticker
  * FirRide, FirSeat, FirTeleport: rides, ride seats and the Station's teleporters
  * FirNight: the pizzeria's night shift
  * FirEncounter, FirTiles, FirSay: Pixel Hollow's encounters, tile gate and talking props
  * FirThrowable: toilets and babies
  * FirCheck*: the systems check
* `legacy/` holds the previous map's code, for reference only. Nothing uses it.

## Testing without Unity

* `bash tools/verify/check_code.sh` compiles every script and the builder against stubs, lints for
  TextMeshPro members Udon doesn't expose, and checks the shaders.
* `bash tools/sim/run_check.sh` builds the world inside a VRChat/UdonSharp simulator and runs 439
  checks with several players. It covers:
  * every systems-check test, gag, routine, combo and skeleton
  * walking the whole route
  * sharing gags, and late joiners
  * a full two-player playthrough of the game (wrong answers too)
  * the tree freezing when watched, hunting, catching and being stunned by a thrown toilet
  * Bonetown's wanderers staying in step on every client, keeping to their paths, and stopping to chat
  * a full Root Cellar playthrough, its second tree and flickering lights, and every new skeleton and move
  * the Station, every park ride (riding the wheel and the ghost train), and the park's wanderers
  * the precinct, and three pizzeria nights: a sensible guard survives, a careless one doesn't, and the
    power runs out
  * Pixel Hollow's encounters, tile gate and path, per player
