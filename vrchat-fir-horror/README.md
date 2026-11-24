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

## The Test Annex and Blocklands

Two more Station destinations, both original takes on their genres.

**The Bonewell Testing Annex** is a white-panelled test lab. PROCTOR, a deadpan announcer, speaks
(voiced, captioned) as you enter each test.
* **Test 1:** the door is a locked fake; step into the purple portal and out of the green one, over the wall.
* **Test 2:** carry the test crate onto the floor button to open the door.
* **Test 3:** the crate is behind glass. Take a portal to it, carry it back through, and put it on the
  button. TESTING COMPLETE lights up (the sign is the reward).
* **Shared crates:** they use object sync, so everyone sees the buttons and doors the same. A RESPAWN
  CRATE button sends them home.

**Blocklands** is a blocky voxel meadow.
* **Mining:** press blocks in the quarry to mine them; ore glows.
* **Building:** press the floating ghost markers on the plot to build with what's in your pocket. The
  second layer needs a block underneath.
* **Shared world:** the world is synced for everyone; your pocket is your own. RESET WORLD puts it all back.
* **Also:** cube-headed skeleton villagers, blocky sheep, square wheat, and 8 gags across the two areas.

## The Cold North: meme parodies

A small snowy Station destination of affectionate parodies of famous open-world RPG memes, all in new
wording.

* **The cart:** ride it round the road. The passenger says: "Hey. You. You're finally awake. Oh. You're
  a skeleton too. Never mind."
* **The guards** wear horned helmets:
  * "I used to wander like you. Then I took a pine cone to the knee."
  * "Let me guess. Someone stole your pine roll."
  * A Bone District snob.
* **The shopkeeper** has a bucket on his head and can't see you.
* **The gags:**
  * a skeleton shouts *FIR! ROH! DAH!* and a tree flies off
  * a giant swats a skeleton into orbit
  * a wheel-of-cheese avalanche
  * a horse that decides the cliff is a road

## The Mystery Hut: a roadside tourist trap in the woods

A Station destination: an original take on the small-town-mystery genre, set at night among trees
that are (literally) watching.

* **The exhibits,** each voiced and captioned by the owner's recorded tour:
  * a six-legged squirrel (two legs taped on), Bigfoot's toenail, and a rock that looks like a rock
  * a "genuine unicorn" (a goat with a cone), and the mirror of truth
  * a shrinking tree, a head in a jar, and a bottomless pit you can drop a pebble into
* **The secret:**
  * 4 signs in the woods are written in a letter-shift code, and a decoder board in the hut explains
    how to read them.
  * Decoded, they give the keypad code by the bookcase, which swings open (per player).
  * Behind it is a secret room: a journal, a weirdness detector, floating books, and furniture on the
    ceiling.
* **The people:** owner Marrowby (bowler hat, cane), Pip at the gift shop (everything is a mug), a
  tourist and his kid ("Dad, it's a goat"), and a lumberjack.
* **Outside:** a garden gnome that pops out of a bush, and a lake monster that is very obviously a
  cardboard cutout.

## The later Station stops

All original takes on their genres, each with voiced skeletons and gags (`Data/more_gags.txt`):

* **The Redemption Inn:** a cheerful hotel somewhere very warm. A radio-voiced host, a bartender, a
  bellhop, a musical number ("The Redemption Song") and an elevator that only goes down.
* **The Speed Zone:** a checkered track with boost pads, springs, rings to collect (with a counter),
  a runner who laps it forever, a coach and an exhausted skeleton at the finish.
* **The Platform World:** floating platforms, bonk blocks that pop coins, two pairs of warp tubes,
  a cloud you can ride and a flagpole at the end.
* **North Carolina:** the Blue Ridge, the Outer Banks with a banded lighthouse, wild ponies,
  longleaf pines with cardinals, sweet tea, a Venus flytrap, the first flight over the dunes, and a
  barbecue shack where Vern and Kay argue about vinegar against tomato.
* **The Digital Circus:** a big top inside a computer, with a TV-headed ringmaster, performers, a mime
  in his box, floating shapes, an EXIT that drops you back at the entrance, and the void.
* **The Skits:** 10 rapid-fire stick-figure skits in a white room.
* **The Corn Flakes Room:** a giant bowl, a talking cereal box, a chef, a swimmer, and 9 corn flake jokes.

## The second batch of Station stops

More original takes on favourite genres, plus two real places. Each has voiced, captioned skeletons:

* **Crystal Cove:** a beach town with a temple in the cliff (a four-armed statue), warp pads up the
  cliff, a donut shop, crystal skeletons with gems in their ribs, and a fusion dance that goes wrong.
* **Critter Meadow:** tall grass where a critter jumps out, a lab with three starter critters, a critter
  center, a gym, a battle arena (the trainer's critter is a rock) and a rival who runs laps.
* **The Shadow Below:** a classroom whose supply closet opens on a dark world to explore: cliffs on a
  glowing path, the Glimmerwood, a candy field, floating cards, a tile-puzzle gate, a castle town with the
  Chess King, a shop, a jester, and a roaring pillar of light. Wake up to get back.
* **Ink Plaza:** walk on the turf to ink it in your colour (per player, with a score), ink pads, a
  dripping tower, squid-kid skeletons and an ink cannon.
* **The Dread Depths:** a space cave with energy orbs, a high-jump pad up to a ledge, a morph tube, a
  bounty hunter in her suit, a jelly in a jar, a save station and a talking computer.
* **Hero's Glade:** a sword that won't come out, pots, a chest (with a smaller chest inside), chickens
  you should not hit, a fairy, an old man's cave and a heart container.
* **Honeycomb Hollow:** notes to collect up a climbable hill, a jigsaw piece at the top, a bear with a
  bird in his backpack, a rhyming witch's lair and a mole.
* **Dragon Shore:** gems to collect, a glide pad on a tower, crystal dragons to free, a dragonfly, an
  egg thief, and a sheep that becomes a butterfly.
* **Hero Tower:** a superhero team's headquarters, a hammer nobody has tried to lift, a gauntlet that
  snaps, and a scene after the credits.
* **Gloom City:** night-time rooftops, a vigilante on a gargoyle, a flying hero circling the skyline, a
  clown villain, a butler, a reporter, and the bone signal.
* **Myrtle Beach:** the boardwalk, a big wheel you can ride, pancake houses #47, #48 and #49, mini golf
  with a volcano, a beach shop, saltwater taffy, shag dancers and a fry-stealing seagull.
* **Boone, NC:** King Street, a bluegrass porch, a swinging bridge between two peaks, the rock where the
  snow falls up, a ski slope, a steam train you can ride, fog, and fall colours.
* **The Dachshund Room:** sleepy doxies, portraits, a sausage couch, the longest dachshund in the world
  (still arriving), a hot dog costume, stairs (the stairs win), a race, and a dachshund skeleton.
* **Extremely Christmas:** an enormous tree, a train round it you can ride, Santa Bones on his throne,
  the workshop, a sleigh and reindeer, a snowman, cocoa, a gingerbread house, giant candy canes, far too
  many lights (and a switch for more), presents all the way down, snow, and a carol on the console.

## Cellbound (1955): a tribute room

A room dedicated to the MGM cartoon by Tex Avery and Michael Lah, built from original skeleton scenes
(no frames or artwork from the film). A plaque reads out what the film is. The cell has its door left open,
tally marks, a hole and the spoon that dug it. Crawl into the tunnel and you come out behind the warden's
TV, in his living room, where the escaped convict is hiding inside the set. Press the remote and he has to
act out whatever is on: the news, a cowboy show, a cooking show and a dance party (per player).

## Even more rooms

* **Three fan rooms** (unofficial tributes, each with a plaque saying so). Original skeleton gags about the
  kinds of videos each creator makes: no likenesses, logos or catchphrases, and nobody plays the creator.
  * **The Markiplier Room:** a let's-play corner with a screaming streamer, a superfan, a jump-scare
    monitor, door A and door B, and a heist vault.
  * **The Flamingo Room:** lawn flamingos everywhere, a FREE ADMIN button (it sends you back to the start), a
    troll door, a dancing flamingo, "the floor is lava" and a blocky obby.
  * **The Ryan Trahan Room:** a penny you can trade up (paperclip, pencil, bicycle, car), a road-trip map, a
    vlogger, a hitchhiker and a donation jar.
* **The Scream Room:** a scream booth and meter, a screaming choir on the console, a screaming goat, a
  kettle, a rubber chicken, a void to scream into, and a famous painting with more bones.
* **The Inventors' Backyard:** a rollercoaster in the garden that vanishes the moment Mom pulls in, a big
  sister, a secret-agent gnome, and a shrink ray next door.
* **The Notebook School:** a middle school drawn on lined paper, with lockers, the cursed cheese, a gym rope
  and a basement band.
* **The Seabed Town:** kelp, bubbles, a jellyfish field, the Crusty Clam burger shack (with a flying burger),
  a rock house, a glass dome and a starfish.
* **Battle Island:** a bus flying round on a balloon, a storm that closes in, a chest, a supply drop, ramps,
  and dancing skeletons.
* **Site Bone (the SCP room):** a containment facility in the style of collaborative online horror fiction,
  with brand-new anomalies:
  * the sock dryer, the polite void, the too-comfortable chair, the duck that knows your name, and a vending
    machine that only sells spoons
  * BONE-999, a door that always leads to the lobby
  * a breach alarm and a records terminal

* **The Interdimensional Garage:** a cranky genius, an anxious assistant, a thingamaflux, a TV that picks up
  other dimensions, and a green portal to a world of dimensions: the sideways one, the sandwich one, the
  pink garage, a galactic market and a flying saucer to ride.
* **Splashbone Water Park:**
  * **The Bone Bowl,** a bowl slide: climb the tower and sit in a raft. It goes down an enclosed tube into
    the rim of a big striped bowl, spirals two and a half times down to the middle, drops through the hole
    and splashes out into a pool.
  * A wave pool, a lazy river with inner tubes to ride, a giant tipping bucket, a snack shack and a
    lifeguard.

* **The Chris and Tripp Show:** a room recreating Episode 1's five scenes (the kitchen on fire, the crime
  scene, the office, the dark, the couch), with the episode's own dialogue. Every line is cut from the
  episode's audio (`tools/make_chris_tripp.py`) and captioned. Each set has a PLAY SCENE button, and the
  whole script is on the wall.

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
* **Bonetown is bigger** (98 x 130 m, under one sky). It wraps round the Marrow Arms, whose grounds sit
  behind a hedge with three gates. Lanes lead out of the old town's west end:
  * **Main Street (south):** a bakery, Skull Cuts (the barber), a toy shop, the post office, the Boneplex
    cinema, Cafe Calcium, a florist, and an empty shop to lease. Also the town hall with a clock tower
    (and the mayor), a market, a bus stop where Patience has waited since 1962, and a park behind the
    shops with a duck pond, a pigeon man and a merry-go-round you can ride.
  * **Clavicle Avenue (north):** houses with fences and mailboxes, Bonetown Elementary (with a bell), the
    library (shh), the fire station (the ladder goes up), a farm with a scarecrow and a cow skeleton, and
    a lookout hill.
  * 19 new voiced, captioned townsfolk, 6 gags, and 6 more wanderers: a postie, a jogger, a kid on a
    scooter, a dog walker and two tourists.
* **The Marrow Arms** (through the doorway in Bonetown's west wall) is an apartment building with
  grounds of its own:
  * **Outside:** brick walls with lit windows, an awning and the building's name over the door, and a
    front yard with a for-rent sign. Round the sides are parking with two cars, a back yard (a dumpster
    with a raccoon in it, a washing line and a shed), and a north yard with a pool, a vegetable patch and
    a basketball hoop.
  * **Ground floor:** a lobby (clock, mailboxes, a lift that has never gone up), a hallway, 7 flats, a gym,
    a common room and a laundry.
  * **Second floor:** a ramp from the yard to an open balcony with flats 8-11, and a side walkway round
    to the roof.
  * **The roof terrace:** a water tower, a garden (with Gerald the tomato), a telescope, a barbecue, an
    open-mic corner and string lights.
  * **12 residents** live their own days on the Bonetown clock (a day is 12 minutes). Their walks are
    worked out on a graph of doorways, the ramp, the balcony and the roof:
    * Clavicle works out, Mortimer plays sad guitar all night, Granny Tibia waters plastic plants and
      knocks on Mortimer's door at 11 pm, and Coccyx works from home.
    * Ribsy gossips, Phalange cleans the clean, Scapula teaches a dance class nobody comes to, and Femur
      the superintendent fixes the lift (and the water tower).
    * Upstairs: Humerus does open-mic comedy on the roof, Mandible grows one tomato, tiny Stapes
      stargazes, and Hyoid is still moving in (box 37 of 200).
  * A caption over each head says what they're doing ("on the way: ..." while walking). Press one and
    they stop and talk, captioned, for everyone. It all comes from the server clock, so everyone sees
    the same day with no network traffic.
* **Captions everywhere:** every voiced skeleton shows what it says under its name while it talks, and
  combo acts show each line on their button (`Data/captions.txt`, written by `make_voices.py --captions`).

## The expansion

Every Station area has a new wing through a doorway in its far wall (and a shortcut button beside its
entrance; inside each wing, buttons go back to the entrance and the lobby). Each wing has 3 new voiced,
captioned people and 3 new voiced gags, and many have something to collect. 42 wings in all
(`Editor/FirExpansion*.cs`, voices in `tools/make_voices.py` AREAS3, `Audio/More`):

The Grand Ballroom (Inn) · Loop Canyon (Speed Zone) · Cloud Kingdom (Platform World) · The Outer Banks (North
Carolina) · Backstage (Digital Circus) · The Writers' Room (Skits) · The Cereal Aisle (Corn Flakes) · The Moon
Temple (Crystal Cove) · Critter Cave · The School Library (Shadow Below) · Ink Tower Stage · The Reactor Core
(Dread Depths) · The Sunken Shrine (Hero's Glade) · The Great Hive (Honeycomb Hollow) · The Dragon Hatchery ·
The Training Room (Hero Tower) · The Rooftops (Gloom City) · The Boardwalk Arcade (Myrtle Beach) · The Mountain
Trail (Boone) · The Dog Park (Dachshunds) · The Toy Workshop (Extremely Christmas) · The Warden's Office
(Cellbound) · The Fan Art Gallery, The Pool Party, The Road Trip (the fan rooms; unofficial tributes, fans and
crew only) · The Whisper Room (Scream Room) · The Rocket Garage (Inventors' Backyard) · The Cafeteria (Notebook
School) · The Kelp Forest (Seabed Town) · The Storm's Edge (Battle Island) · Containment Wing B (Site Bone, three
new anomalies) · The Invention Shed (Garage) · The Wave Pool and Lazy River (Water Park) · Behind the Scenes
(Chris and Tripp; crew and a fan) · The Midway (Boneanza Park) · Evidence and Interrogation (Precinct 206) ·
The Kitchen (Pine Pete's) · The Old Ruins (Pixel Hollow) · The Observation Deck (Test Annex) · The Mines
(Blocklands) · The Mead Hall (the Cold North, through a tunnel in the mountain) · The Deep Woods (Mystery Hut).

## The new rooms (and rideable rides)

Fifteen more Station stops (`Editor/FirRooms.cs`, `Editor/FirOrlando.cs`; voices in `tools/make_voices.py`
ROOMS, `Audio/Rooms`; `make_voices.py --rooms` re-records just these). The Station board is six across now. The
cartoon rooms are original takes on a kind of show: new names, new characters and new jokes, with no logos,
theme songs or catchphrases. Every person is voiced and captioned, and every gag speaks.

* **The Cutaway House** (a cartoon family sitcom): the living room, a scheming baby, a dog writing a novel, a
  giant rooster and a cutaway.
* **Yellowville** (a yellow cartoon town): the family house, a power plant, the tavern, a donut shop, a couch gag.
* **The Spooky Mansion Case** (teen mystery-solvers and a van): a hallway of doors, and **the Mystery Ride**, a
  dark ride with two cars, four lanes, three scares and an unmasking.
* **Paper Mountain Town** (a cut-out paper mountain town): paper houses, a bus stop, an angry mob, a cardboard cow.
* **Four Oaks, NC**: Main Street, the water tower, four oaks, a cotton field, the Bentonville Battlefield marker,
  the I-95 sign, sweet tea, ten acorns to collect, and a **little train** to ride round the field.
* **The Dino Sing-Along Studio** (a kids' TV dinosaur show): Dudley the Dinosaur, a treehouse, a toy chest.
* **The Back Rooms**: 60 by 60 metres of yellow wallpaper, damp carpet and humming lights in a maze.
* **The Quiet Town, 1962** (a black-and-white small-town sheriff show): the sheriff's office, the barber, a pond.
* **Castaway Island** (shipwrecked castaways): the wreck of the S.S. Sardine, huts, a coconut radio, a bamboo car.
* **The Wishing Hills** (friendly storybook dragons): a wishing stone, a rainbow, a castle on a cloud.
* **The Kitchen Counter Theatre** (singing vegetables): a radish, a squash, a pea choir and an onion on a giant counter.
* **The Clubhouse on the Hill** (a cartoon clubhouse): a gizmo picker, a garden, and **toon cars** to ride round it.
* **The Suspicious Starship** (a crew with one impostor): the emergency button, vents, wiring, a medbay scan.
* **The Meme Museum**: five skeleton memes behind velvet ropes and a statue of the first meme.
* **Orlando, Florida**: two **roller coasters** (Gator Gulch, the family one, and the Storm Chaser: a 30 m
  lift, about 70 km/h at the bottom of the drop), two **dark rides** (the Haunted Orange Grove, with trees that
  jump out, and Mission: Space Coast), **swan boats** on the lake, an **observation wheel**, an orange grove, a
  gator pond and the I-4.

The coasters use a new kind of `FirRide` (kind 3): the cars follow a 3D track, pitch up and down with it, bank
into
the turns, crawl through the station and up the lift chain, and speed up the further below the top of the lift
they are. As with every ride, the motion comes from the server clock, so everyone sees the same train in the same
place without syncing. Sit in any seat to ride; jump to get off, and you land back on the platform. The builder
checks that each coaster's track never runs through itself.

## The remaster

Everything except the talking trees' models and the voices has been remastered:

* **Shading.** The world's one shader (`FirLantern`) now gives every surface form: a soft key light and a
  sky/ground fill shade each face by which way it points, a fine world-space grain breaks up flat colours, and
  edges catch a faint rim. The lantern-lit horror rooms get the grain and a little sky light on top of the
  lantern. The trees' materials (`Needles`, `Pot`, `Stand`, `Trunk`, `Eye`, `Pupil`, `Mouth`, `Teeth`, the hat
  and the jump-scare green) set `_Remaster` to 0 and look exactly as before.
* **Geometry.** Props and furniture up to 4 m (every builder box, and plain-coloured decoration cubes) are
  bevelled boxes now, with chamfered edges that catch the light; big walls and floors stay plain boxes to keep
  the vertex count down on Quest. Colliders are unchanged.
* **Textures.** Brick, wood floor, concrete, metal, ceiling tiles, both carpets, the wallpaper, paper and
  cardboard are repainted at 1024 px with real-world sizes (25 cm bricks and planks, 67 cm ceiling tiles,
  50 cm carpet tiles), mortar, plank gaps, knots, rivets, cracks, stains and wear
  (`tools/remaster_textures.py`). The needles texture and the window picture are untouched.
* **Sound effects and music.** 44.1 kHz, with a convolution reverb sized for each sound, a sub layer under the
  hits and the music box's tines clicking. The trees' talking, singing, laughing, notes, whisper and screams,
  and every voice line, are untouched.

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
python make_voices.py      # espeak-ng + mbrola: skeleton voices (add --rainbow / --combo / --more / --rooms for those lines)
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
  * FirPortal, FirPad, FirVoxel: the Test Annex's portals and buttons, and Blocklands
  * FirCodeLock: the Mystery Hut's secret keypad
  * FirBoost: the Speed Zone's boost pads, springs and rings
  * FirResident: the Marrow Arms residents' daily routines
  * FirInkTurf: the Ink Plaza's turf
  * FirChannels: the Cellbound room's TV (and the trade-up pedestal)
  * FirThrowable: toilets and babies
  * FirCheck*: the systems check
* `legacy/` holds the previous map's code, for reference only. Nothing uses it.

## Testing without Unity

* `bash tools/verify/check_code.sh` compiles every script and the builder against stubs, lints for
  TextMeshPro members Udon doesn't expose, and checks the shaders.
* `bash tools/sim/run_check.sh` builds the world inside a VRChat/UdonSharp simulator and runs 595
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
  * the Test Annex's portals, crates, buttons and doors, and mining and building in Blocklands
