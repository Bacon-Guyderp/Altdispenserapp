"""Records the two skeleton voice lines for the Living Room closet easter egg.

  python make_voices.py     (needs espeak-ng, the MBROLA us2/us3 voices, and ffmpeg)
                            Debian/Ubuntu: apt install espeak-ng mbrola mbrola-us2 mbrola-us3

Writes Assets/FirHorror/Audio/skeleton_white.ogg and skeleton_red.ogg, plus one line per comedy
gag in Assets/FirHorror/Data/gags.txt, the rainbow skeletons' lines to Audio/Rainbow/rb_<colour>.ogg (--rainbow: only those), and the combination acts' lines to Audio/Combo/<name>.ogg (--combo: only those), to Assets/FirHorror/Audio/Gags/gag_<id>.ogg and each skeleton routine
in Data/skeleton_gags.txt to Audio/Gags/skel_<id>.ogg (mono Vorbis).
Gag voices need mbrola-us1 too.
"""
import os, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(os.path.dirname(HERE), "Assets", "FirHorror", "Audio")

# every skeleton line is played 43% louder than the tree and narrator gag voices
SKELETON_BOOST = 1.43

LINES = [
    # name, MBROLA voice, words/min, pitch (0-99), text, ffmpeg filter (pitch shift + closet echo)
    ("skeleton_white", "mb-us2", 118, 32, "A skeleton.",
     f"asetrate=16000*0.93,aresample=22050,aecho=0.8:0.6:45:0.22,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled"),
    ("skeleton_red", "mb-us3", 150, 62, "Red skeleton. Ha!",
     f"asetrate=16000*1.06,aresample=22050,aecho=0.8:0.6:40:0.2,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled"),
]


# gag voice presets: MBROLA voice, words/min, pitch (0-99), playback-rate shift
PRESETS = {
    "tree": ("mb-us3", 150, 55, 1.0),
    "tiny": ("mb-us3", 165, 75, 1.3),
    "big": ("mb-us2", 125, 25, 0.85),
    "narrator": ("mb-us1", 145, 45, 1.0),
    "lady": ("mb-us1", 150, 65, 1.05),
    "old": ("mb-us2", 110, 40, 0.95),
    "skel": ("mb-us2", 125, 32, 0.93),
    "skelred": ("mb-us3", 150, 62, 1.06),
    "skelgreen": ("mb-us3", 140, 72, 1.12),
}


# the rainbow of skeletons (red reuses skeleton_red): name, MBROLA voice, words/min, pitch, rate shift, text
RAINBOW = [
    ("orange", "mb-us3", 150, 58, 1.08, "Knock knock. Orange you glad I'm just bones?"),
    ("yellow", "mb-us2", 135, 60, 1.12, "I'm not yellow. I just haven't got the guts."),
    ("green", "mb-us3", 120, 40, 0.92, "Feeling a little green. Too many pine needles."),
    ("blue", "mb-us2", 105, 20, 0.86, "I've got the blues. Nobody to dance with."),
    ("indigo", "mb-us1", 145, 50, 1.0, "Indigo? Nobody even knows what colour that is."),
    ("violet", "mb-us1", 160, 78, 1.18, "Sorry. I'm a shrinking violet. I go to pieces."),
    # the second line-up (south wall)
    ("pink", "mb-us1", 165, 80, 1.15, "Stop it, I'm ticklish! Tickled pink, and I haven't even got skin."),
    ("teal", "mb-us2", 100, 30, 0.9, "Ooooh. I'm the teal skeleton. Teal death do us part."),
    ("gold", "mb-us2", 130, 22, 0.88, "Gold skeleton. Worth my weight in gold. I weigh nothing!"),
    ("silver", "mb-us3", 110, 35, 0.95, "Silver skeleton. I came second in the skeleton contest."),
    ("black", "mb-us2", 115, 15, 0.84, "Black skeleton. I'm the one hiding in your closet. Boo."),
    ("brown", "mb-us1", 140, 45, 0.98, "Brown skeleton. I'm not hungry. I've got no stomach for it."),
    ("glow", "mb-us3", 135, 70, 1.1, "I glow in the dark. Someone turn off the lights. Please?"),
    ("rainbow", "mb-us1", 175, 70, 1.12, "I'm every colour! I just couldn't decide."),
    ("tiny", "mb-us3", 160, 95, 1.42, "Hi! I'm just a little skele-tiny!"),
    ("giant", "mb-us2", 100, 5, 0.74, "I'm not fat. I'm big-boned."),
]


# combination acts: clip name, voices (several = a chorus, mixed), text. Voice presets match the skeletons.
SKV = {"red": ("mb-us3", 150, 62, 1.06), "orange": ("mb-us3", 150, 58, 1.08), "yellow": ("mb-us2", 135, 60, 1.12),
       "green": ("mb-us3", 120, 40, 0.92), "blue": ("mb-us2", 105, 20, 0.86), "indigo": ("mb-us1", 145, 50, 1.0),
       "violet": ("mb-us1", 160, 78, 1.18), "gold": ("mb-us2", 130, 22, 0.88), "silver": ("mb-us3", 110, 35, 0.95),
       "brown": ("mb-us1", 140, 45, 0.98), "tiny": ("mb-us3", 160, 95, 1.42), "giant": ("mb-us2", 100, 5, 0.74),
       "pink": ("mb-us1", 165, 80, 1.15), "teal": ("mb-us2", 100, 30, 0.9), "black": ("mb-us2", 115, 15, 0.84)}
COMBO = [
    ("traffic_green", ["green"], "Green means go!"),
    ("traffic_yellow", ["yellow"], "Yellow means slow down."),
    ("traffic_red", ["red"], "And red means... red skeleton. Ha!"),
    ("knock_1", ["orange"], "Knock knock."),
    ("knock_2", ["yellow"], "Who's there?"),
    ("knock_3", ["orange"], "Orange."),
    ("knock_4", ["yellow"], "Orange who?"),
    ("knock_5", ["orange"], "Orange you glad I didn't say banana?"),
    ("knock_6", ["yellow"], "That's not even a skeleton joke."),
    ("bonetones_1", ["blue", "indigo", "violet"], "Doo wop, doo wop, doo wop."),
    ("bonetones_2", ["indigo"], "We're the Bone Tones!"),
    ("bonetones_3", ["blue", "indigo", "violet"], "Shoo be doo wah. Oooooh!"),
    ("medal_gold", ["gold"], "First place!"),
    ("medal_silver", ["silver"], "Second. Again."),
    ("medal_brown", ["brown"], "Third! That's bronze, right? Close enough."),
    ("upsy_1", ["tiny"], "Pick me up! Pick me up!"),
    ("upsy_2", ["giant"], "Upsy daisy!"),
    ("upsy_3", ["tiny"], "Wheeeeee!"),
    ("upsy_4", ["giant"], "Oof. You're heavier than you look."),
    ("upsy_5", ["tiny"], "I'm big boned too!"),
    ("finale_1", ["red", "gold", "blue", "violet", "giant", "tiny", "pink", "black"], "We are the skeleton crew!"),
    ("finale_2", ["red", "gold", "blue", "violet", "giant", "tiny", "pink", "black"], "Thank you, good night!"),
]


# Bonetown (the expansion): every resident, band member and wanderer has its own voice and line.
# name, MBROLA voice, words/min, pitch, rate shift, text  ->  Audio/Town/<name>.ogg
TOWN = [
    # residents, at home
    ("res_mint", "mb-us3", 165, 78, 1.15, "I just moved in! I haven't even unpacked my spare ribs."),
    ("res_peach", "mb-us1", 140, 60, 1.05, "I'm practising for the town dance. My partner is a broom. He's very quiet."),
    ("res_lavender", "mb-us1", 95, 40, 0.92, "Five more minutes. I've only been asleep for two hundred years."),
    ("res_coral", "mb-us2", 140, 30, 0.9, "I work out every single day. Look at these bones. All bone. No muscle. Still proud."),
    ("res_plum", "mb-us2", 120, 45, 0.95, "I keep eating, but nothing ever stays down. Or in."),
    ("res_sky", "mb-us3", 170, 70, 1.12, "I'm writing a song! It's called Ribcage Rock. It's mostly xylophone."),
    ("res_navy", "mb-us2", 120, 20, 0.86, "Shh! I'm hiding from my landlord. I owe him a skull."),
    ("res_cream", "mb-us1", 150, 75, 1.1, "I tripped over the rug. Again. Could somebody... pick me up? All of me?"),
    ("res_rust", "mb-us3", 150, 50, 1.0, "Welcome to the snack shack! Today's special is soup. It goes right through you."),
    ("res_olive", "mb-us1", 135, 50, 0.98, "Breaking news! Local skeleton loses his temper. Also his arm. More at eleven."),
    # the band, each pressed on its own
    ("band_scarlet", "mb-us3", 150, 62, 1.06, "I play the trumpet. Well. I blow through my ribs."),
    ("band_mustard", "mb-us2", 135, 40, 0.95, "I'm on drums. And these are my own drumsticks. Literally."),
    ("band_emerald", "mb-us3", 125, 45, 0.94, "Guitar! I only know one chord. A spinal chord."),
    ("band_cobalt", "mb-us1", 145, 55, 1.0, "I'm the singer. Great voice, for a guy with no throat."),
    # the wanderers: they stop to chat
    ("wan_lime", "mb-us2", 110, 35, 0.93, "Out for my evening stroll. Want to come? I'm very slow. And very dead."),
    ("wan_magenta", "mb-us1", 185, 80, 1.18, "Can't stop! I'm late for a very important... nothing! Bye!"),
    ("wan_turquoise", "mb-us3", 170, 75, 1.15, "Tag! You're it! ...Oh. You're not playing. That's fine."),
    ("wan_crimson", "mb-us3", 175, 65, 1.1, "You can't catch me! I'm not even running from anything!"),
    ("wan_amber", "mb-us1", 130, 55, 1.0, "Lovely day for a walk. I left my legs on, just in case."),
    ("wan_ivory", "mb-us2", 125, 50, 0.97, "Hello, neighbour! Have you met everyone? Everyone's bones."),
    ("wan_cocoa", "mb-us2", 150, 30, 0.9, "I jog every morning. Lose weight? I haven't got any to lose!"),
    ("wan_denim", "mb-us1", 140, 45, 0.96, "Ooh, a visitor! We don't get many living ones."),
    ("wan_neon", "mb-us3", 180, 85, 1.2, "Wheee! Running is the best! I never get out of breath. No lungs!"),
    ("wan_bubblegum", "mb-us1", 160, 85, 1.16, "Shopping day! I'm buying a new skull. This one's a bit hollow."),
]
SKV.update({"scarlet": ("mb-us3", 150, 62, 1.06), "mustard": ("mb-us2", 135, 40, 0.95),
            "emerald": ("mb-us3", 125, 45, 0.94), "cobalt": ("mb-us1", 145, 55, 1.0)})
COMBO += [
    ("squabble_1", ["scarlet"], "You took my femur!"),
    ("squabble_2", ["cobalt"], "It's MY femur!"),
    ("squabble_3", ["scarlet"], "Then whose femur am I standing on?"),
    ("squabble_4", ["cobalt"], "Mine. Sorry."),
    ("squabble_5", ["scarlet", "cobalt"], "Bone buddies! Hug it out!"),
    ("band_1", ["scarlet", "mustard", "emerald", "cobalt"], "A one, a two, a one two three four!"),
    ("band_2", ["scarlet"], "Doot doot da doot!"),
    ("band_3", ["cobalt"], "We are the bones! The bones of Bonetown!"),
    ("band_4", ["scarlet", "mustard", "emerald", "cobalt"], "Thank you, Bonetown! Good night!"),
    ("propose_1", ["emerald"], "Mustard. Will you be my neighbour?"),
    ("propose_2", ["mustard"], "I'm already your neighbour."),
    ("propose_3", ["emerald"], "Then will you be my BEST neighbour?"),
    ("propose_4", ["mustard"], "Yes! Yes! A thousand times, yes!"),
    ("town_finale_1", ["scarlet", "mustard", "emerald", "cobalt"], "Welcome to Bonetown!"),
    ("town_finale_2", ["scarlet", "mustard", "emerald", "cobalt"], "Population: us!"),
]


def town_lines():
    for name, v, speed, pitch, rate, text in TOWN:
        yield ("Town/" + name, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled")


# The 40 new skeletons spread through the map (Audio/Extra/xs_<name>.ogg): name, voice, words/min, pitch, rate, text
EXTRA = [
    # the Gift Wrap Hall
    ("ash", "mb-us2", 120, 35, 0.94, "I'm wrapping myself as a present. Nobody ever wants to unwrap a skeleton."),
    ("slate", "mb-us2", 110, 25, 0.9, "Somebody wrapped me in a box with air holes. Very thoughtful. I don't breathe."),
    ("rose", "mb-us1", 165, 80, 1.15, "The ribbon tickles! It goes right between my ribs!"),
    ("saffron", "mb-us3", 150, 55, 1.04, "Gift wrap inspector, reporting for duty! This one's a sock. Again."),
    ("tangerine", "mb-us3", 140, 60, 1.08, "Knock knock. It's me, inside the present. Please let me out."),
    # the Toy Workshop
    ("lemon", "mb-us3", 170, 85, 1.2, "I'm a jack in the box! Well, a skeleton in the box. Same spring."),
    ("sage", "mb-us2", 100, 35, 0.9, "I've been building the same toy for three hundred years. Still missing one screw."),
    ("seafoam", "mb-us1", 140, 55, 1.0, "Need a hand? How about a head? Here. Hold this."),
    ("aqua", "mb-us3", 175, 70, 1.12, "Wind me up and watch me go! Clickety clack!"),
    ("sapphire", "mb-us2", 130, 25, 0.9, "Quality control! I tested every single toy. I broke every single toy."),
    # the Chimney Lounge
    ("periwinkle", "mb-us1", 105, 45, 0.95, "Santa never comes down this chimney. I think it's because of me."),
    ("orchid", "mb-us1", 135, 65, 1.05, "Chestnuts roasting by the fire. Not me. I'm not roasting. I'm chilling."),
    ("fuchsia", "mb-us3", 120, 50, 0.98, "I'm the ghost of Christmas past. Mostly past. Very, very past."),
    # the Bone Yard
    ("cherry", "mb-us3", 160, 60, 1.06, "Boo! Sorry. Force of habit. It's the Bone Yard."),
    ("maroon", "mb-us2", 110, 20, 0.86, "I live here. The rent's cheap, and the neighbours are dead quiet."),
    ("copper", "mb-us2", 130, 40, 0.95, "I came here for the bone broth. Turns out I was the bone broth."),
    ("bronze", "mb-us3", 120, 40, 0.94, "Third place in the skeleton contest. Bronze. Story of my life. Well. Afterlife."),
    ("caramel", "mb-us1", 145, 55, 1.02, "Don't mind me. I'm just falling apart. It happens."),
    # the lobby
    ("frost", "mb-us1", 150, 60, 1.03, "Welcome to Fir Warning! Doors that way. Trees everywhere. Good luck!"),
    ("holly", "mb-us3", 160, 70, 1.1, "Hi! I'm the welcome committee. And the goodbye committee, if the trees get you."),
    # Bonetown: picnics
    ("honey", "mb-us1", 145, 60, 1.04, "Picnic time! I brought sandwiches. They go right through me, but I love the taste."),
    ("butter", "mb-us2", 115, 45, 0.95, "I'm lying in the sun, working on my tan. It's not working."),
    ("jade", "mb-us2", 90, 30, 0.9, "I'm meditating. I've been very still for a very long time. I'm good at it."),
    ("blush", "mb-us1", 170, 85, 1.16, "Is it true the trees in the cellar are awake? Ooh, spooky! Count me out!"),
    # Bonetown: new wanderers
    ("pistachio", "mb-us3", 140, 55, 1.02, "Just walking my invisible dog. He's a very good boy. Mostly bones."),
    ("salmon", "mb-us1", 135, 60, 1.0, "I swim upstream every single day. Well. I walk. Upstream is that way."),
    ("apricot", "mb-us3", 185, 75, 1.15, "Can't talk! Training for the skeleton marathon! It's all downhill from here!"),
    ("lilac", "mb-us1", 125, 55, 0.98, "Lovely evening. Have you been down to the root cellar? Don't."),
    # the Root Cellar night shift
    ("moss", "mb-us2", 125, 30, 0.92, "Welcome to the night shift. The trees down here bite. Clock in!"),
    ("bark", "mb-us3", 140, 45, 0.96, "Shh! Did you hear that? That was the tree. Or my knees."),
    ("fern", "mb-us1", 120, 50, 0.98, "I water the plants. The plants water me back. With their eyes."),
    ("midnight", "mb-us2", 105, 15, 0.84, "It's always midnight down here. That's why they named me Midnight. Also my mum."),
    ("steel", "mb-us2", 130, 25, 0.9, "I run the sawmill. Keep your arms inside the ride. I don't have that problem."),
    ("pewter", "mb-us3", 125, 40, 0.95, "I lost my head in the sawmill once. Found it. Still a bit loose."),
    ("charcoal", "mb-us3", 165, 65, 1.08, "Don't look away from them! I looked away once. Now I'm a skeleton."),
    ("mauve", "mb-us1", 140, 55, 1.0, "This is the tool library. Borrow anything except the axe. The tree has the axe."),
    ("burgundy", "mb-us2", 150, 35, 0.95, "The generator's broken. I've been pedalling for a hundred years. Hello!"),
    ("mocha", "mb-us1", 115, 45, 0.96, "Coffee break. I drink it and it goes straight through. Mostly onto the floor."),
    ("chartreuse", "mb-us3", 150, 75, 1.12, "I glow. It's not a gift. It's the fuses. Don't touch the fuses."),
    ("wine", "mb-us2", 135, 40, 0.96, "Count us! The foreman says the number of us is the code. I always lose count. Of me."),
]


def extra_lines():
    for name, v, speed, pitch, rate, text in EXTRA:
        yield ("Extra/xs_" + name, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled")


# Boneanza Park (the amusement park): 30 skeletons -> Audio/Park/pk_<name>.ogg
PARK = [
    ("taffy", "mb-us1", 150, 65, 1.06, "Salt water taffy! It sticks to your teeth. Good thing I've got plenty."),
    ("popcorn", "mb-us3", 165, 60, 1.08, "Fresh popcorn! It pops right out between my ribs. Don't ask."),
    ("cotton", "mb-us1", 160, 85, 1.16, "Cotton candy! It's mostly air. Like me. Mostly gaps."),
    ("churro", "mb-us2", 135, 40, 0.95, "Churros! Long, crunchy and covered in sugar. That's also my dating profile."),
    ("pretzel", "mb-us3", 120, 45, 0.96, "I tied myself in a pretzel once. Took three days to untie."),
    ("sundae", "mb-us1", 140, 70, 1.08, "Ice cream! I get brain freeze. Well. Skull freeze. There's no brain."),
    ("slushy", "mb-us3", 170, 70, 1.1, "Blue raspberry slushy! Fair warning, you'll see it again. Through my ribs."),
    ("ticket", "mb-us2", 125, 35, 0.92, "Tickets! One ticket per ride. Two if you're a skeleton. We come apart."),
    ("jester", "mb-us3", 175, 75, 1.14, "Watch me juggle! Three skulls! Wait. Whose skulls are these?"),
    ("confetti", "mb-us1", 180, 85, 1.2, "Honk honk! I'm a clown! My nose is the only part of my face left."),
    ("balloon", "mb-us1", 145, 75, 1.1, "Balloons! Hold on tight. I let go of one once and floated for a week."),
    ("fortune", "mb-us2", 95, 30, 0.88, "I see your future. Hmm. It has trees in it. Lots and lots of trees."),
    ("brawn", "mb-us2", 130, 15, 0.82, "Ring the bell, win a prize! I hit it once. My arm went higher than the puck."),
    ("hoop", "mb-us3", 155, 55, 1.04, "Toss a ring over a bottle! Nobody's won since nineteen sixty. That's the fun."),
    ("dunk", "mb-us2", 140, 45, 0.98, "Dunk the skeleton! Go on! I can't drown. I just get soggy."),
    ("gears", "mb-us2", 120, 30, 0.9, "Hop on the big wheel! Best view in the park. Of the other skeletons."),
    ("whirly", "mb-us3", 170, 70, 1.12, "Teacups! Spin it as fast as you like. Just aim away from me."),
    ("dizzy", "mb-us1", 150, 80, 1.15, "Whee! I've been on this ride for six hours. I can't feel my bones."),
    ("loop", "mb-us3", 130, 50, 1.0, "I've been on this wheel since opening day. It goes round. So do I."),
    ("gallop", "mb-us1", 165, 70, 1.1, "Giddy up! My horse doesn't go anywhere, but it does it with style."),
    ("ghoul", "mb-us2", 100, 10, 0.82, "Ride the ghost train, if you dare. The ghosts are my cousins. Say hi."),
    ("shriek", "mb-us3", 160, 65, 1.08, "The drop tower! Up. Up. Up. Then, for a moment, everything comes apart."),
    ("sprinkle", "mb-us1", 185, 95, 1.3, "Mum! Mum! Can we go on the ghost train? Can we? Can we?"),
    ("gumdrop", "mb-us3", 160, 85, 1.25, "I've had nine candy apples. I'm not hungry. I just like the noise."),
    ("lollipop", "mb-us1", 185, 90, 1.28, "Race you to the carousel! Last one there is a pile of bones!"),
    ("fudge", "mb-us2", 140, 40, 0.96, "Has anyone seen my kid? About this tall. Skeletal. Sticky."),
    ("toffee", "mb-us3", 130, 50, 1.0, "I lost my ticket. And my left foot. The ticket's more important."),
    ("licorice", "mb-us3", 165, 80, 1.2, "The ghost train isn't scary. I definitely didn't scream. That was my knees."),
    ("nougat", "mb-us1", 150, 75, 1.12, "I won a goldfish! It's a skeleton fish. Very low maintenance."),
    ("marshmallow", "mb-us1", 140, 70, 1.08, "I'm toasting marshmallows! Ah. That one was me. Ow."),
]


def park_lines():
    for name, v, speed, pitch, rate, text in PARK:
        yield ("Park/pk_" + name, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled")


# Precinct 206 (deadpan cop comedy): 14 skeleton cops -> Audio/Precinct/pc_<name>.ogg
COPS = [
    ("marrow", "mb-us2", 115, 25, 0.88, "Welcome to Precinct Two Oh Six. Take a number. We only have the one."),
    ("femur", "mb-us2", 120, 30, 0.9, "I'm a detective. I detect. Right now, I detect that you're standing there."),
    ("tibia", "mb-us3", 125, 45, 0.96, "I've never fired my weapon. Mostly because it's a banana."),
    ("fibula", "mb-us1", 125, 50, 0.98, "We found the body. It was mine. I'd left it in the break room."),
    ("patella", "mb-us2", 115, 30, 0.9, "Lieutenant Patella. I'm the kneecap of this department. I keep things bending."),
    ("sternum", "mb-us2", 105, 15, 0.84, "I want this case solved by morning. Which morning, I don't know. Any morning."),
    ("clavicle", "mb-us1", 120, 45, 0.96, "I'm on desk duty. I sit at this desk. Sometimes the desk sits on me."),
    ("coccyx", "mb-us3", 165, 75, 1.14, "First day! They gave me a badge, a hat, and a tail. Wait. That's mine."),
    ("scapula", "mb-us3", 115, 40, 0.94, "I inspected everything. Twice. Everything is still there. Case closed."),
    ("humerus", "mb-us1", 135, 55, 1.0, "They call me Humerus. Because I'm funny. Also, I'm an arm bone."),
    ("radius", "mb-us2", 125, 35, 0.92, "I'm tailing a suspect. Don't look. Don't look! You looked."),
    ("ulna", "mb-us3", 135, 50, 1.0, "I think somebody is following me. It's probably nothing. It's probably Radius."),
    ("vertebra", "mb-us3", 175, 65, 1.1, "Hot pursuit! I've been chasing him round this desk since Tuesday!"),
    ("pelvis", "mb-us1", 180, 70, 1.12, "Can't stop! I don't even know what I did!"),
]


def cop_lines():
    for name, v, speed, pitch, rate, text in COPS:
        yield ("Precinct/pc_" + name, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled")


def render_only(tmp, lines, folder):
    """Records just these lines (a new area's voices) into Audio/<folder>."""
    os.makedirs(os.path.join(OUT, folder), exist_ok=True)
    for name, voice, speed, pitch, text, filt in lines:
        wav = os.path.join(tmp, name.replace("/", "_") + ".wav")
        subprocess.run(["espeak-ng", "-v", voice, "-s", str(speed), "-p", str(pitch), "-w", wav, text], check=True)
        out = os.path.join(OUT, name + ".ogg")
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-af", filt + ",apad=pad_dur=0.25",
                        "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", out], check=True)
        print(name, os.path.getsize(out) // 1024, "KB")


# Pine Pete's Pizza Palace: staff and guests -> Audio/Pizza/pz_<name>.ogg, and the last night guard's voicemail
PIZZA = [
    ("basil", "mb-us2", 140, 40, 0.95, "Pizza's up! Extra cheese, extra pine needles. House special."),
    ("pepper", "mb-us1", 160, 70, 1.08, "Welcome to Pine Pete's! Would you like to try our new tree fold pizza? It's a pizza. In a tree."),
    ("mop", "mb-us3", 115, 40, 0.94, "I mop every night. Every morning there are footprints. Big, rooty footprints."),
    ("candle", "mb-us3", 175, 90, 1.3, "It's my birthday! I'm three hundred! I don't look a day over two hundred and ninety!"),
    ("frosting", "mb-us1", 180, 95, 1.32, "Can we see the animatronics? Mum says they only bite at night."),
    ("gary", "mb-us2", 120, 35, 0.92, "Night shift? I did that. Five nights. On the sixth night I got very, very relaxed."),
    ("tokens", "mb-us3", 140, 50, 1.0, "I've been trying to win the big bear for sixty years. I'm so close."),
    ("voicemail", "mb-us2", 135, 35, 0.92,
     "Hello? Hello, hello! Welcome to your first night at Pine Pete's Pizza Palace. I used to work this shift. "
     "Now I'm a skeleton. Unrelated! Probably. Anyway. The trees get a bit restless at night. They like to wander. "
     "Pete comes down the west side. Holly comes down the east. Sprucey, who knows. "
     "If one of them shows up at your door, shut it. Don't leave it shut all night, though. The power is on a budget. "
     "Check the cameras. Check the lights. And whatever you do, don't let Pete in. He'll want to give you a hug. "
     "A very, very pointy hug. Okay! Good luck! Have a pizza!"),
]


def pizza_lines():
    for name, v, speed, pitch, rate, text in PIZZA:
        yield ("Pizza/pz_" + name, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled")


# Pixel Hollow: the duo, the villagers, the save point and the chest -> Audio/Pixel/px_<name>.ogg
PIXEL = [
    ("lumen", "mb-us2", 110, 20, 0.86, "Evening, traveller. I keep the lighthouse. There's no sea. But you can never be too careful."),
    ("rattleton", "mb-us3", 165, 80, 1.2, "Halt! I am Sir Rattleton, knight of the cardboard order! My armour is water resistant. Mostly."),
    ("pebble", "mb-us1", 150, 60, 1.04, "Welcome to the shop! Everything costs one gold. We don't take gold."),
    ("fizz", "mb-us3", 140, 55, 1.0, "The inn has one bed. It's a very popular bed."),
    ("nib", "mb-us1", 180, 95, 1.3, "If you stand still long enough, the music changes. It doesn't. I've checked."),
    ("brindle", "mb-us2", 100, 30, 0.88, "In my day, monsters just wanted a chat. Nowadays monsters also just want a chat."),
    ("save", "mb-us1", 130, 50, 1.0, "The sparkle twinkles. Your bones feel very sturdy. Progress saved. Spiritually."),
    ("chest", "mb-us1", 130, 50, 1.0, "You open the chest. Inside, there's a smaller chest. Inside that, a note. It says: nice try."),
]
SKV.update({"rattleton": ("mb-us3", 165, 80, 1.2), "lumen": ("mb-us2", 110, 20, 0.86)})
COMBO += [
    ("quest_1", ["rattleton"], "Halt! None shall pass!"),
    ("quest_2", ["lumen"], "Everyone shall pass. It's a path."),
    ("quest_3", ["rattleton"], "Then. Some shall pass?"),
    ("quest_4", ["lumen"], "Most shall pass. Let's say most."),
    ("quest_5", ["rattleton", "lumen"], "Agreed! Onward!"),
]


def pixel_lines():
    for name, v, speed, pitch, rate, text in PIXEL:
        boost = SKELETON_BOOST if name not in ("save", "chest") else 1.0
        yield ("Pixel/px_" + name, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * boost:.3f},alimiter=limit=0.95:level=disabled")


# The Test Annex's announcer and Blocklands' villagers -> Audio/Lab/lb_<name>.ogg
LAB = [
    ("proctor0", "mb-us1", 125, 30, 0.9, "Welcome to the Bonewell Testing Annex. You have volunteered. Please do not check whether you volunteered."),
    ("proctor1", "mb-us1", 125, 30, 0.9, "Test one. The door is locked. The portal is not. Draw your own conclusions."),
    ("proctor2", "mb-us1", 125, 30, 0.9, "Excellent. You walked through a hole. Science thanks you."),
    ("proctor3", "mb-us1", 125, 30, 0.9, "Test two. Place the crate on the button. The crate does not mind. We asked."),
    ("proctor4", "mb-us1", 125, 30, 0.9, "The door is open. Please do not grow attached to the crate. It has already forgotten you."),
    ("proctor5", "mb-us1", 125, 30, 0.9, "Test three. The crate is on the other side of the glass. So is a portal. Coincidence? Yes."),
    ("proctor6", "mb-us1", 125, 30, 0.9, "Testing complete. Your reward is the satisfaction of a test completed. And this sign."),
    ("digby", "mb-us2", 120, 30, 0.9, "I dig. Then I dig some more. The hole is my friend now."),
    ("brick", "mb-us3", 140, 50, 1.0, "I built a house out of dirt. It's mostly dirt. And a door. A dirt door."),
    ("sprout", "mb-us1", 150, 70, 1.08, "Square wheat. Square carrots. I grew a round potato once. Nobody believed me."),
    ("woolly", "mb-us3", 160, 75, 1.12, "My sheep are cubes. When they jump, they go boing. A square boing."),
]


def lab_lines():
    for name, v, speed, pitch, rate, text in LAB:
        boost = 1.0 if name.startswith("proctor") else SKELETON_BOOST
        filt = (f"asetrate=16000*{rate},aresample=22050,aecho=0.6:0.4:20:0.3,volume={1.8 * boost:.3f},alimiter=limit=0.95:level=disabled" if name.startswith("proctor")
                else f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * boost:.3f},alimiter=limit=0.95:level=disabled")
        yield ("Lab/lb_" + name, v, speed, pitch, text, filt)


# The Cold North (meme parodies) -> Audio/North/nt_<name>.ogg
NORTH = [
    ("passenger", "mb-us2", 130, 35, 0.92, "Hey. You. You're finally awake. Oh. You're a skeleton too. Never mind. Go back to sleep."),
    ("guard1", "mb-us2", 115, 25, 0.88, "I used to wander like you. Then I took a pine cone to the knee."),
    ("guard2", "mb-us3", 120, 35, 0.92, "Let me guess. Someone stole your pine roll."),
    ("guard3", "mb-us1", 135, 55, 1.0, "Do you get to the Bone District very often? Oh, what am I saying. Of course you don't."),
    ("bucket", "mb-us3", 125, 45, 0.96, "Hm? Who's there? I can't see a thing. Take whatever you like. I mean. Don't."),
    ("shout", "mb-us2", 95, 10, 0.8, "FIR! ROH! DAH!"),
    ("launch", "mb-us3", 190, 95, 1.35, "Wheeeeeeee! I can see my house from here!"),
    ("cheese", "mb-us1", 130, 50, 1.0, "And the cheese wheels roll down the mountain. As is tradition."),
    ("horse", "mb-us1", 130, 50, 1.0, "The horse has decided that the mountain is a road. Nobody can stop the horse."),
    ("sign", "mb-us1", 130, 50, 1.0, "Dragons not included. The trees, however, are."),
]


def north_lines():
    for name, v, speed, pitch, rate, text in NORTH:
        narr = name in ("cheese", "horse", "sign")
        yield ("North/nt_" + name, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * (1.0 if narr else SKELETON_BOOST):.3f},alimiter=limit=0.95:level=disabled")


# The Mystery Hut -> Audio/Mystery/my_<name>.ogg (Marrowby's recorded tour reads the exhibits)
MYSTERY = [
    ("marrowby", "mb-us2", 140, 35, 0.94, "Welcome to the Mystery Hut! Every exhibit is one hundred percent genuine. Some percent. A percent."),
    ("pip", "mb-us1", 110, 45, 0.96, "Gift shop's open. Everything is a mug. Even the T-shirts are mugs."),
    ("dad", "mb-us2", 130, 40, 0.95, "I came for the mysteries. I've paid forty dollars to see a rock. It's a good rock."),
    ("kid", "mb-us1", 180, 95, 1.3, "Dad, is the unicorn a goat? Dad. Dad. It's a goat, Dad."),
    ("lumberjack", "mb-us3", 120, 30, 0.9, "I chop trees for a living. Around here, the trees chop back. I've stopped."),
    ("squirrel", "mb-us2", 140, 35, 0.94, "Behold! The six legged squirrel! Please ignore the tape."),
    ("toenail", "mb-us2", 140, 35, 0.94, "Bigfoot's toenail! He wants it back. He can't have it."),
    ("rock", "mb-us2", 140, 35, 0.94, "A rock that looks exactly like a rock. Uncanny."),
    ("unicorn", "mb-us2", 140, 35, 0.94, "The genuine unicorn! Do not look at the tape. Do not ask about the goat."),
    ("pit", "mb-us2", 140, 35, 0.94, "The bottomless pit. Drop something in. Still falling. Still falling. That'll be one dollar."),
    ("mirror", "mb-us2", 140, 35, 0.94, "The mirror of truth! It shows you as you truly are. A person who paid to look at a mirror."),
    ("shrink", "mb-us2", 140, 35, 0.94, "The incredible shrinking tree! Please don't ask it about its feelings."),
    ("jar", "mb-us3", 150, 55, 1.02, "Get me out of this jar. Also, the gift shop is that way."),
    ("gnome", "mb-us3", 175, 85, 1.25, "You didn't see me. I'm a garden ornament. Garden ornaments don't talk. Bye."),
    ("lake", "mb-us1", 130, 50, 1.0, "The lake monster! It's a cardboard cutout. Or is it? It is."),
    ("journal", "mb-us1", 120, 45, 0.98, "Day one. The trees are watching me. Day two. The trees are still watching me. Day three. I have started watching the trees back. It is a stalemate."),
    ("detector", "mb-us3", 160, 60, 1.1, "Beep. Beep. Weirdness level: yes."),
]


def mystery_lines():
    for name, v, speed, pitch, rate, text in MYSTERY:
        boost = SKELETON_BOOST if name in ("marrowby", "pip", "dad", "kid", "lumberjack", "jar") else 1.0
        yield ("Mystery/my_" + name, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * boost:.3f},alimiter=limit=0.95:level=disabled")


# The rest of the Station's areas: (folder/prefix_name, voice, words/min, pitch, rate, kind, text)
# kind: "skel" (a skeleton: boosted), "narr" (narrator), "radio" (an old radio host), "glitch" (a glitchy ringmaster)
AREAS = [
    # the Redemption Inn
    ("Inn/in_dial", "mb-us2", 150, 30, 0.95, "radio", "Good evening, listeners, and welcome to the Redemption Inn! Please stay tuned. You can't change the station anyway."),
    ("Inn/in_cinder", "mb-us1", 175, 80, 1.12, "skel", "Hi! I'm Cinder! This hotel redeems sinners! So far we've redeemed a sock. It was a very bad sock."),
    ("Inn/in_brimstone", "mb-us2", 105, 10, 0.82, "skel", "I'm the bartender. Drinks are on the house. The house is on fire. Same thing."),
    ("Inn/in_hop", "mb-us3", 185, 95, 1.35, "skel", "Bellhop! I carry the luggage! Sometimes the luggage carries me! It's a partnership!"),
    ("Inn/in_dusty", "mb-us1", 120, 45, 0.95, "skel", "I dust every inch of this hotel. The dust comes back. The dust always comes back."),
    ("Inn/in_lustre", "mb-us1", 120, 60, 1.0, "skel", "Darling, I've been checked in for three hundred years. The minibar is simply divine."),
    # the speed zone
    ("Speed/sp_dash", "mb-us3", 200, 70, 1.15, "skel", "Gotta go! Gotta go! I don't know where. I've never known where. Gotta go!"),
    ("Speed/sp_coach", "mb-us2", 140, 35, 0.95, "skel", "Hit the boost pads. Bounce on the springs. Grab the rings. That's it. That's the whole sport."),
    ("Speed/sp_tired", "mb-us2", 100, 25, 0.88, "skel", "I've run this course nine thousand times. I'm not tired. Skeletons don't get tired. I'm lying down for fun."),
    # the platform world
    ("Plat/pl_bonny", "mb-us1", 150, 60, 1.04, "skel", "I built all these floating platforms. Don't ask how they float. I don't know either."),
    ("Plat/pl_coins", "mb-us3", 140, 50, 1.0, "skel", "Every block has a coin in it. Who keeps putting coins in blocks? That's what I want to know."),
    ("Plat/pl_flag", "mb-us1", 135, 50, 1.0, "narr", "Course clear! You win absolutely nothing. Congratulations!"),
    ("Plat/pl_bonk", "mb-us3", 175, 80, 1.2, "narr", "Bonk! A coin! Bonk! Another coin! Your head will feel this tomorrow."),
    # North Carolina
    ("NC/nc_vern", "mb-us2", 125, 30, 0.9, "skel", "Eastern style! Whole hog, vinegar and pepper. Anything else is just a sandwich with a ketchup problem."),
    ("NC/nc_kay", "mb-us1", 140, 60, 1.04, "skel", "Lexington dip! A little tomato never hurt anybody. Well. It hurt Vern's feelings."),
    ("NC/nc_keeper", "mb-us2", 115, 30, 0.9, "skel", "They moved this lighthouse half a mile inland once. Lighthouses don't usually go for walks. This one did."),
    ("NC/nc_surfer", "mb-us3", 160, 65, 1.08, "skel", "Surf's up on the Outer Banks! The sharks are up too. We have an understanding."),
    ("NC/nc_hiker", "mb-us1", 135, 50, 1.0, "skel", "I hiked the Blue Ridge. Turns out it's only blue because it's very, very far away."),
    ("NC/nc_flight", "mb-us1", 130, 50, 1.0, "narr", "First in flight! Twelve seconds, a hundred and twenty feet. The skeletons would like it noted that they were not invited."),
    ("NC/nc_flytrap", "mb-us2", 120, 20, 0.86, "narr", "Venus flytraps only grow wild around here. I'm very picky about my neighbours. And my lunch."),
    ("NC/nc_tea", "mb-us1", 130, 50, 1.0, "narr", "Sweet tea. So sweet it's legally a dessert."),
    ("NC/nc_pony", "mb-us1", 130, 50, 1.0, "narr", "The wild ponies of the Outer Banks. Do not feed them. Do not pet them. Do not challenge them to a race."),
    ("NC/nc_mitchell", "mb-us1", 130, 50, 1.0, "narr", "Mount Mitchell. The highest peak east of the Mississippi. Our version is a bit smaller."),
    # the digital circus
    ("Circus/ci_ringmaster", "mb-us2", 165, 45, 1.0, "glitch", "Welcome, welcome, welcome, to the show! The show never ends! That's a feature!"),
    ("Circus/ci_acrobat", "mb-us1", 165, 75, 1.12, "skel", "I've been doing this backflip for six hours. I'm not sure which way is up any more. Is it you?"),
    ("Circus/ci_strongman", "mb-us2", 110, 10, 0.82, "skel", "I lift weights. The weights are drawn on. I am lifting a drawing. Very heavy drawing."),
    ("Circus/ci_juggler", "mb-us3", 150, 60, 1.04, "skel", "I juggle cubes, spheres, and existential dread. The cubes are the hardest."),
    ("Circus/ci_mime", "mb-us1", 130, 50, 1.0, "narr", "The mime would like you to know he is trapped in a box. The box is also trapped. In a circus."),
    ("Circus/ci_exit", "mb-us2", 165, 45, 1.0, "glitch", "Welcome back to the show!"),
    ("Circus/ci_void", "mb-us1", 90, 20, 0.85, "narr", "The void says hi. The void would like to know how your day is going."),
    # the asdf-style skits
    ("Asdf/as_magic", "mb-us3", 160, 70, 1.1, "skel", "Want to see a magic trick? I'm still here. It wasn't a good trick."),
    ("Asdf/as_potato", "mb-us1", 150, 75, 1.1, "skel", "I'm a potato. No, really. I'm a potato now."),
    ("Asdf/as_spoon", "mb-us3", 170, 80, 1.15, "skel", "Look, a spoon! That's a fork. This is the worst day of my life."),
    ("Asdf/as_hat", "mb-us2", 140, 40, 0.95, "skel", "I bought a new hat. And thirty more, apparently."),
    ("Asdf/as_cake", "mb-us1", 150, 60, 1.05, "skel", "Who ordered the cake? Nobody? It's mine now."),
    ("Asdf/as_duck", "mb-us3", 140, 60, 1.05, "skel", "Quack. I'm not a duck. I'm a skeleton saying quack. Quack."),
    ("Asdf/as_knock", "mb-us2", 140, 40, 0.95, "skel", "Knock knock. Who's there? Nobody. I'm a skeleton. There is literally no body."),
    ("Asdf/as_stairs", "mb-us1", 150, 55, 1.02, "skel", "I fell down the stairs. What stairs? Exactly."),
    ("Asdf/as_balloon", "mb-us3", 175, 85, 1.2, "skel", "I'm going to pop this balloon! Waaaaah!"),
    ("Asdf/as_sandwich", "mb-us2", 135, 45, 0.96, "skel", "I made a sandwich. Wait. I'm the filling."),
    # the corn flakes room
    ("Corn/cf_box", "mb-us2", 130, 40, 0.95, "narr", "I'm the cereal box. Every joke in this room is about me. I've made my peace with it."),
    ("Corn/cf_1", "mb-us1", 135, 55, 1.0, "narr", "Corn flakes. The only breakfast that's scared of milk."),
    ("Corn/cf_2", "mb-us3", 140, 50, 1.0, "narr", "I'd tell you a corn flake joke, but it's a bit corny. And flaky. And soggy by the end."),
    ("Corn/cf_3", "mb-us2", 130, 35, 0.94, "narr", "What do you call a corn flake that won't stay in the bowl? A cereal escapist."),
    ("Corn/cf_4", "mb-us1", 135, 55, 1.0, "narr", "Corn flakes never crack under pressure. They just go soggy."),
    ("Corn/cf_5", "mb-us3", 140, 50, 1.0, "narr", "Corn flakes are just popcorn that gave up."),
    ("Corn/cf_6", "mb-us2", 130, 35, 0.94, "narr", "Me and my corn flakes have a lot in common. We both fall apart in the morning."),
    ("Corn/cf_7", "mb-us1", 135, 55, 1.0, "narr", "How do corn flakes stay in shape? Daily bowling."),
    ("Corn/cf_8", "mb-us3", 140, 50, 1.0, "narr", "Doctor, I think I'm a corn flake. Well, that's a bit flaky of you."),
    ("Corn/cf_9", "mb-us2", 130, 35, 0.94, "narr", "The corn flake went to therapy. It had layers to work through. Well. One layer."),
    ("Corn/cf_chef", "mb-us2", 130, 40, 0.95, "skel", "I'm the breakfast chef. I make corn flakes. I pour them. That's the whole recipe."),
    ("Corn/cf_swim", "mb-us3", 160, 70, 1.1, "skel", "The milk's lovely today! A bit flaky."),
]
SKV.update({"cinder": ("mb-us1", 175, 80, 1.12), "dial": ("mb-us2", 150, 30, 0.95), "brimstone": ("mb-us2", 105, 10, 0.82), "hop": ("mb-us3", 185, 95, 1.35),
            "vern": ("mb-us2", 125, 30, 0.9), "kay": ("mb-us1", 140, 60, 1.04)})
COMBO += [
    ("inn_1", ["cinder"], "Welcome to the Redemption Inn!"),
    ("inn_2", ["dial"], "Where every guest gets a second chance!"),
    ("inn_3", ["brimstone"], "And a third. And a fourth. I'm not singing."),
    ("inn_4", ["hop"], "He's singing a little bit!"),
    ("inn_5", ["cinder", "dial", "brimstone", "hop"], "Check in, cheer up, and stay forever!"),
    ("bbq_1", ["vern"], "Vinegar!"),
    ("bbq_2", ["kay"], "Tomato!"),
    ("bbq_3", ["vern"], "Vinegar is tradition!"),
    ("bbq_4", ["kay"], "Tomato is also tradition!"),
    ("bbq_5", ["vern", "kay"], "Hush puppies? Hush puppies."),
]
AREA_FILTERS = {
    "skel": "aecho=0.8:0.5:35:0.15,volume={v:.3f},alimiter=limit=0.95:level=disabled",
    "narr": "aecho=0.8:0.5:30:0.12,volume=1.8,alimiter=limit=0.95:level=disabled",
    "radio": "highpass=f=450,lowpass=f=2800,aecho=0.6:0.4:12:0.3,volume=2.6,alimiter=limit=0.95:level=disabled",
    "glitch": "flanger=delay=3:depth=4:speed=2,tremolo=f=9:d=0.35,aecho=0.8:0.5:25:0.2,volume=2.0,alimiter=limit=0.95:level=disabled",
}


# The Marrow Arms apartments in Bonetown: the residents and the building's gags -> Audio/Apart/ap_<name>.ogg
APART = [
    ("Apart/ap_clavicle", "mb-us3", 170, 70, 1.1, "skel", "Morning! I've done four hundred push-ups. My bones are very toned. That's all I have. Bones."),
    ("Apart/ap_mortimer", "mb-us2", 110, 25, 0.88, "skel", "I write sad songs on the guitar. Granny Tibia says they're too loud. I say they're too sad. We're both right."),
    ("Apart/ap_tibia", "mb-us1", 115, 75, 1.15, "skel", "Back in my day we didn't have apartments. We had crypts. And we were grateful!"),
    ("Apart/ap_coccyx", "mb-us2", 150, 45, 0.98, "skel", "I work from home. My commute is eleven steps. I'm still late."),
    ("Apart/ap_ribsy", "mb-us1", 180, 70, 1.12, "skel", "Have you heard? Apartment two got a new guitar string! And apartment four ordered soup! Again!"),
    ("Apart/ap_phalange", "mb-us3", 160, 55, 1.05, "skel", "I've swept this hallway nine times today. There's still dust. I think the dust is me."),
    ("Apart/ap_scapula", "mb-us1", 170, 65, 1.1, "skel", "I teach dance in the common room! Nobody comes. The dancing is still mandatory."),
    ("Apart/ap_femur", "mb-us2", 120, 20, 0.85, "skel", "I'm the superintendent. The lift's been broken since I got here. I'm starting to think it's decorative."),
    ("Apart/ap_lift", "mb-us2", 140, 40, 1.0, "narr", "Going up! ...Just kidding. It has never gone up."),
    ("Apart/ap_mail", "mb-us1", 150, 55, 1.0, "narr", "You've got mail! It's a bill. For the mailbox."),
    ("Apart/ap_dryer", "mb-us3", 150, 50, 1.0, "narr", "The dryer ate a sock. And a femur. Femur would like it back."),
    ("Apart/ap_humerus", "mb-us3", 165, 60, 1.08, "skel", "I'm a comedian! Why did the skeleton skip the party? He had no body to go with. I'll be here all week. I live here."),
    ("Apart/ap_mandible", "mb-us2", 120, 30, 0.9, "skel", "I grow tomatoes on the roof. Well. I grow one tomato. His name is Gerald."),
    ("Apart/ap_stapes", "mb-us1", 190, 95, 1.4, "skel", "I'm the smallest bone in the body! I watch the stars from the roof. From here, they're even smaller than me."),
    ("Apart/ap_hyoid", "mb-us3", 160, 55, 1.05, "skel", "Just moved in! Box thirty seven of two hundred. Who designed this ramp? Me. I did. I regret it."),
    ("Apart/ap_raccoon", "mb-us3", 175, 70, 1.25, "skel", "Excuse me. This is a private dumpster. Residents only. I'm a resident."),
    ("Apart/ap_forrent", "mb-us2", 140, 45, 1.0, "narr", "Now renting: second floor flats! Stairs not included. There's a ramp. It's a very good ramp."),
]


def apart_lines():
    for name, v, speed, pitch, rate, kind, text in APART:
        filt = AREA_FILTERS[kind].replace("{v:.3f}", f"{1.8 * SKELETON_BOOST:.3f}")
        yield (name, v, speed, pitch, text, f"asetrate=16000*{rate},aresample=22050," + filt)


# The second batch of Station stops -> Audio/<Area>/<id>.ogg
AREAS2 = [
    ("Gem/gm_pearl", "mb-us1", 125, 70, 1.05, "skel", "I'm a crystal skeleton. My gem is in my ribcage. Very convenient. Very hard to lose."),
    ("Gem/gm_amethyst", "mb-us1", 150, 45, 0.95, "skel", 'Fusion dance? We tried. We just became one very confused skeleton with four elbows.'),
    ("Gem/gm_donut", "mb-us3", 160, 60, 1.05, "skel", 'Welcome to the Big Ring! Every donut has a hole. So do I. Several.'),
    ("Gem/gm_kid", "mb-us3", 175, 85, 1.25, "skel", "I've got a magic shield, a cat-shaped backpack, and absolutely no idea what I'm doing!"),
    ("Gem/gm_warp", "mb-us2", 140, 40, 1.0, "narr", 'Warp pad activated. Please keep your arms inside the beam. And your bones.'),
    ("Gem/gm_fuse", "mb-us2", 140, 40, 1.0, "narr", 'Two skeletons dance, and become one taller skeleton! Much too tall. Abort.'),
    ("Critter/cr_trainer", "mb-us3", 175, 75, 1.2, "skel", "I challenge you to a critter battle! My critter is a rock. It's undefeated. It's never moved."),
    ("Critter/cr_prof", "mb-us2", 130, 35, 0.92, "skel", 'Ah, a new trainer! Pick a critter! Not that one. That one bites. They all bite.'),
    ("Critter/cr_nurse", "mb-us1", 150, 75, 1.1, "skel", "Your critters are fully healed! Please don't ask how. It's mostly hugs."),
    ("Critter/cr_grass", "mb-us2", 150, 45, 1.0, "narr", "A wild critter appeared! It's a Mossbun! It's eating your shoelaces."),
    ("Critter/cr_catch", "mb-us2", 150, 45, 1.0, "narr", "Catch cube thrown! One. Two. Three. It got out. It's judging you."),
    ("Critter/cr_gym", "mb-us2", 120, 25, 0.88, "skel", 'To earn this badge you must defeat me! Or ask nicely. Badges are mostly decorative.'),
    ("Critter/cr_rival", "mb-us3", 180, 65, 1.15, "skel", "Smell you later! ...I don't have a nose. Smell you never!"),
    ("Shade/sh_teacher", "mb-us1", 135, 55, 1.0, "skel", "Class! Please collect your chalk. Someone's gone missing. Probably in the supply closet. It's always the closet."),
    ("Shade/sh_jester", "mb-us3", 185, 90, 1.3, "glitch", 'Ho ho! Welcome to the shadow side! Here everything is purple and nothing makes sense! Chaos! Chaos!'),
    ("Shade/sh_king", "mb-us2", 110, 15, 0.82, "skel", "I am the Chess King! I can only move one square at a time. It's very frustrating. Bow. Slowly."),
    ("Shade/sh_shop", "mb-us1", 140, 60, 1.05, "skel", 'Welcome, welcome! I sell darkness candy. It tastes like the colour purple.'),
    ("Shade/sh_hero", "mb-us3", 150, 55, 1.05, "skel", "In this world I'm a knight! In the other world I'm a student. The homework follows me here."),
    ("Shade/sh_spring", "mb-us2", 130, 30, 0.95, "narr", "The shade spring roars. It's very loud. It's very dramatic. Please don't touch it."),
    ("Shade/sh_rude", "mb-us2", 160, 40, 0.95, "skel", "I'm the rude buddy. I'm not your friend. I'm also your friend. Don't tell anyone."),
    ("Ink/ik_kid", "mb-us3", 180, 80, 1.25, "skel", "Stay fresh! I'm half kid, half squid, all skeleton. The squid half is very confused."),
    ("Ink/ik_dj", "mb-us2", 165, 50, 1.05, "skel", 'Turf war at noon! Cover the floor with ink! The janitors hate us.'),
    ("Ink/ik_shop", "mb-us1", 155, 65, 1.1, "skel", 'Fresh fits! This hat makes you look like a tentacle. Everyone looks like a tentacle.'),
    ("Ink/ik_cannon", "mb-us2", 150, 45, 1.0, "narr", 'Splat! Direct hit. The wall is now a lovely shade of orange.'),
    ("Ink/ik_news", "mb-us1", 170, 70, 1.15, "skel", "Today's stages: a parking lot, and a different parking lot. Stay fresh!"),
    ("Dread/dr_hunter", "mb-us1", 125, 35, 0.95, "skel", 'Bounty hunter. Power suit. Very shiny. I can roll into a ball. My knees hate it.'),
    ("Dread/dr_jelly", "mb-us2", 130, 35, 0.95, "narr", 'Specimen: floating jelly. Do not remove from the jar. It drains energy. And small talk.'),
    ("Dread/dr_save", "mb-us2", 130, 35, 0.95, "radio", 'Saving. Saving. Game saved. You are still in a creepy space cave.'),
    ("Dread/dr_science", "mb-us3", 150, 60, 1.05, "skel", 'We found a creature that eats energy. We fed it a battery. It wants another battery.'),
    ("Dread/dr_computer", "mb-us2", 120, 30, 0.95, "radio", "Warning. Self destruct in ten minutes. Just kidding. Or am I. I'm the computer."),
    ("Glade/gl_hero", "mb-us3", 160, 60, 1.1, "skel", "Hyaa! Sorry. I only speak in grunts. It's a whole thing. Hyaa!"),
    ("Glade/gl_sword", "mb-us2", 140, 40, 1.0, "narr", "You try to pull the sword. It doesn't budge. A sign says: come back in a hundred years."),
    ("Glade/gl_pot", "mb-us2", 140, 40, 1.0, "narr", 'You smashed a pot! You found one coin. The pot was worth more.'),
    ("Glade/gl_chest", "mb-us2", 140, 40, 1.0, "narr", 'You opened the chest! You found: a slightly smaller chest!'),
    ("Glade/gl_fairy", "mb-us1", 190, 95, 1.4, "skel", "Hello! I'm a fairy! Hey! Listen! Hey! ...Sorry. It's a habit."),
    ("Glade/gl_clucks", "mb-us2", 140, 40, 1.0, "narr", 'You hit the chicken. The chicken called its friends.'),
    ("Glade/gl_old", "mb-us2", 105, 20, 0.85, "skel", "It's dangerous to go alone. Take this! It's a spoon. Good luck."),
    ("Honey/hn_bear", "mb-us2", 120, 25, 0.85, "skel", "Hi! I'm a bear skeleton, and there's a bird skeleton in my backpack. We don't talk about how."),
    ("Honey/hn_bird", "mb-us3", 190, 90, 1.35, "skel", "Squawk! Hurry up, bonehead! Those notes won't collect themselves!"),
    ("Honey/hn_witch", "mb-us1", 150, 70, 1.1, "glitch", "I'm the witch, I'm here to say, I'll steal your looks and run away! ...You don't have looks. You're bones."),
    ("Honey/hn_mole", "mb-us2", 110, 30, 0.9, "skel", "Ah, you found me! I'll teach you a move! It's called walking. You've mastered it!"),
    ("Honey/hn_jigsaw", "mb-us2", 140, 45, 1.0, "narr", "Jigsaw piece! You've got one. You need ninety nine more. Good luck!"),
    ("Honey/hn_gibber", "mb-us3", 200, 70, 1.3, "skel", "Gruntle wobble bluh bluh! Sorry. That's how we talk around here."),
    ("Dragon/dg_dragon", "mb-us3", 165, 70, 1.15, "skel", "I'm a purple dragon skeleton! I breathe fire! I also breathe out of my ribs. Lots of drafts."),
    ("Dragon/dg_frozen", "mb-us2", 130, 30, 0.9, "skel", "Thank you for freeing me from the crystal! I'd shake your hand, but I'm a dragon. Have a claw."),
    ("Dragon/dg_elder", "mb-us2", 100, 10, 0.8, "skel", "Long ago, all the dragons were turned to crystal. We've been very still. Very, very still."),
    ("Dragon/dg_fly", "mb-us1", 200, 95, 1.4, "skel", "I'm the dragonfly! I follow you around and change colour when you get hurt. Mostly, I buzz."),
    ("Dragon/dg_sheep", "mb-us2", 140, 45, 1.0, "narr", 'You charged the sheep. The sheep is fine. The sheep is now a butterfly.'),
    ("Dragon/dg_thief", "mb-us3", 175, 55, 1.1, "skel", "Egg thief! That's me! These are my eggs now. They're rocks. I've been robbing rocks."),
    ("Hero/hr_captain", "mb-us2", 135, 35, 0.95, "skel", "I'm Captain Calcium! My shield is made of milk. I can do this all day. Mostly because I'm bones. I don't get tired."),
    ("Hero/hr_thunder", "mb-us2", 115, 15, 0.82, "skel", "I am Thunderbone, god of loud noises! My hammer is very heavy. Nobody else can lift it. I've also never lifted it."),
    ("Hero/hr_armor", "mb-us3", 170, 55, 1.05, "skel", "Genius. Inventor. Skeleton. My armour has heated seats. I just think that's important."),
    ("Hero/hr_snap", "mb-us2", 130, 35, 0.95, "narr", "The gauntlet snapped. Half the skeletons turned to dust. They're fine. They were already dust."),
    ("Hero/hr_credits", "mb-us2", 130, 35, 0.95, "narr", "You stayed after the credits! That's it. That was the scene. A skeleton eating a sandwich."),
    ("Hero/hr_assemble", "mb-us1", 150, 60, 1.05, "skel", "Heroes, assemble! Okay, one at a time. Someone's elbow is in my eye."),
    ("Gloom/gc_owl", "mb-us2", 95, 5, 0.78, "skel", 'I am the night. I am vengeance. I am also very cold. Gloom City has terrible heating.'),
    ("Gloom/gc_flyer", "mb-us3", 160, 55, 1.05, "skel", "Up, up and away! I'm faster than a speeding bus! Which isn't saying much in this city."),
    ("Gloom/gc_giggles", "mb-us3", 175, 85, 1.25, "glitch", "Why so bony? Ha ha! I'm Mister Giggles! My jokes are criminal! Literally. They're illegal."),
    ("Gloom/gc_butler", "mb-us2", 115, 30, 0.9, "skel", 'Master Owl, your soup is getting cold. Again. Every night. Brooding is not a meal plan.'),
    ("Gloom/gc_signal", "mb-us2", 130, 35, 0.95, "narr", "The bone signal lights up the sky! It's a call for help. Or a very big night light."),
    ("Gloom/gc_reporter", "mb-us1", 170, 65, 1.1, "skel", 'This is the Daily Gloom! Still no idea who the masked owl is. All skeletons look the same!'),
    ("Myrtle/mb_shag", "mb-us1", 155, 65, 1.05, "skel", "We're doing the shag! The state dance of South Carolina! Slide, slide, rock step. Don't make it weird."),
    ("Myrtle/mb_pancake", "mb-us3", 165, 60, 1.1, "skel", 'Welcome to pancake house number forty seven! Number forty eight is across the street. Number forty nine is next door.'),
    ("Myrtle/mb_golf", "mb-us2", 150, 45, 1.0, "skel", 'Mini golf! The volcano erupts every ten minutes. Last time, it erupted on me. Hole in one, though.'),
    ("Myrtle/mb_wheel", "mb-us1", 150, 70, 1.1, "skel", 'The big wheel! You can see the whole beach! And a lot of seagulls. The seagulls can see you too.'),
    ("Myrtle/mb_shop", "mb-us3", 180, 65, 1.15, "skel", "Beach towels! Hermit crabs! T-shirts that say I survived! We're the fourteenth beach shop on this street!"),
    ("Myrtle/mb_gull", "mb-us2", 140, 40, 1.0, "narr", 'A seagull stole your french fry. The seagull feels no remorse.'),
    ("Myrtle/mb_taffy", "mb-us2", 140, 50, 1.0, "skel", "Saltwater taffy! Pulled fresh! It pulls your teeth out too. Good thing I'm all teeth."),
    ("Boone/bn_banjo", "mb-us2", 125, 30, 0.9, "skel", 'Welcome to the porch! I play the banjo. Five strings, fifty songs, and every one is about a mountain.'),
    ("Boone/bn_hiker", "mb-us3", 165, 60, 1.1, "skel", "Mile high swinging bridge up ahead! Don't look down. I looked down. I'm fine."),
    ("Boone/bn_coffee", "mb-us1", 150, 60, 1.05, "skel", 'Fresh coffee on King Street! Flannel optional. Flannel is not optional.'),
    ("Boone/bn_rock", "mb-us2", 140, 40, 1.0, "narr", 'This is the rock where the snow falls upward. The wind blows everything back up. Including your hat.'),
    ("Boone/bn_ski", "mb-us3", 170, 70, 1.15, "skel", "First snow! Hit the slopes! I can't ski. But I can fall down a mountain beautifully."),
    ("Boone/bn_student", "mb-us1", 160, 70, 1.1, "skel", 'College in the mountains! The walk to class is uphill both ways. I checked.'),
    ("Boone/bn_train", "mb-us2", 130, 30, 0.92, "skel", "All aboard the old steam train! Watch out for train robbers! They're actors. Mostly."),
    ("Boone/bn_fog", "mb-us2", 140, 40, 1.0, "narr", "The fog rolled in. You can't see your hand. You're a skeleton. You couldn't see it anyway."),
    ("Doxie/dx_long", "mb-us2", 140, 40, 1.0, "narr", "This is the longest dachshund in the world. He started walking in on Tuesday. He's still arriving."),
    ("Doxie/dx_owner", "mb-us1", 160, 70, 1.1, "skel", "I love dachshunds! Long dogs, short legs! They're basically a hallway with a tail."),
    ("Doxie/dx_bun", "mb-us2", 140, 40, 1.0, "narr", 'A dachshund in a hot dog bun costume. He has never been so proud.'),
    ("Doxie/dx_stairs", "mb-us2", 140, 40, 1.0, "narr", 'Dachshund versus stairs. The stairs are winning. The stairs always win.'),
    ("Doxie/dx_skeleton", "mb-us3", 150, 50, 1.05, "skel", "I'm a dachshund skeleton. I'm mostly ribs. Long, long ribs."),
    ("Doxie/dx_race", "mb-us2", 170, 50, 1.05, "narr", "And they're off! The wiener dog race! It's very close. They're all very low to the ground."),
    ("Doxie/dx_bark", "mb-us3", 160, 80, 1.3, "skel", "Woof! Woof! That's the whole joke. Woof."),
    ("Xmas/xm_santa", "mb-us2", 125, 20, 0.85, "skel", "Ho ho ho! I'm Santa Bones! I checked my list. Everybody's on the nice list. I'm very lenient!"),
    ("Xmas/xm_elf", "mb-us1", 190, 90, 1.35, "skel", 'I make toys all year! Eleven months of toys! One month of panic!'),
    ("Xmas/xm_reindeer", "mb-us3", 150, 55, 1.05, "skel", "I'm a reindeer! My nose doesn't glow. I'm a skeleton. I don't have a nose."),
    ("Xmas/xm_snowman", "mb-us2", 130, 40, 0.95, "skel", "I'm a snowman made of snow bones! Brrr. That's not the cold. That's my personality."),
    ("Xmas/xm_cocoa", "mb-us1", 160, 70, 1.1, "skel", 'Hot cocoa! Extra marshmallows! It goes straight through my ribs. A very festive puddle.'),
    ("Xmas/xm_train", "mb-us2", 140, 40, 1.0, "narr", "All aboard the Christmas train! It goes round the tree. Forever. There's no stop. Merry Christmas."),
    ("Xmas/xm_lights", "mb-us2", 140, 40, 1.0, "narr", 'You turned on the lights! All forty thousand of them! Somewhere, the power company is crying.'),
    ("Xmas/xm_present", "mb-us2", 140, 40, 1.0, "narr", "You opened a present! It's another present! Inside that, another present! It's presents all the way down."),
    ("Xmas/xm_tree", "mb-us2", 140, 40, 1.0, "narr", "The tree is so big, it has its own weather. It's snowing on the angel."),
    ("Cell/cb_convict", "mb-us2", 125, 30, 0.9, "skel", "Twenty years of digging with a spoon. Freedom at last! ...Wait. Why is it so cozy in here?"),
    ("Cell/cb_warden", "mb-us2", 110, 15, 0.84, "skel", "Escaped? Nonsense. Nobody gets out of my prison. Now, where's the remote?"),
    ("Cell/cb_wife", "mb-us1", 150, 70, 1.08, "skel", "Ooh, the picture's so clear tonight! It's almost like he's really in there."),
    ("Cell/cb_plaque", "mb-us2", 135, 40, 1.0, "narr", "Cellbound. Nineteen fifty five. Directed by Tex Avery and Michael Lah, for M G M. A convict tunnels out of prison, and comes up inside the warden's television set."),
    ("Cell/cb_spoon", "mb-us2", 140, 40, 1.0, "narr", "The spoon. Worn down from years of digging. Now it's a very, very small spoon."),
    ("Cell/cb_news", "mb-us3", 150, 45, 1.0, "radio", "Channel two, the news. A prisoner has escaped. Nobody knows where. Especially not this newsreader."),
    ("Cell/cb_cowboy", "mb-us3", 160, 50, 1.05, "radio", "Channel four. Cowboys! Yee haw! Bang, bang! ...I'm getting very tired."),
    ("Cell/cb_cooking", "mb-us3", 150, 55, 1.05, "radio", "Channel seven, the cooking show. Today we're making a getaway."),
    ("Cell/cb_dance", "mb-us3", 175, 60, 1.1, "radio", "Channel nine, the late night dance party! Faster! Faster! Please, change the channel."),
    ("Town2/t2_baker", "mb-us1", 150, 65, 1.05, "skel", "Fresh bread! Still warm! I can't feel warm. I just trust the oven."),
    ("Town2/t2_barber", "mb-us2", 130, 35, 0.92, "skel", 'Skull Cuts! We do trims, fades, and polishes. Mostly polishes. Nobody here has any hair.'),
    ("Town2/t2_toys", "mb-us3", 170, 75, 1.2, "skel", "Toys for all ages! Wind-up skeletons, skeleton dolls, and a skeleton in a box. That last one's my cousin."),
    ("Town2/t2_postie", "mb-us2", 145, 45, 1.0, "skel", 'Post office! Letters, parcels, and one very confused pigeon. Please stop sending us bones. We know.'),
    ("Town2/t2_usher", "mb-us3", 155, 55, 1.05, "skel", "Welcome to the Boneplex! Tonight's film is a horror movie. It's about a man with skin. Terrifying."),
    ("Town2/t2_cafe", "mb-us1", 165, 70, 1.1, "skel", 'Coffee? Tea? A milkshake? Everything here goes straight through. We call it the full experience.'),
    ("Town2/t2_florist", "mb-us1", 140, 80, 1.15, "skel", "Flowers for your sweetheart! Lilies, roses, and these. They're called skeleton flowers. They're real. Look them up."),
    ("Town2/t2_mayor", "mb-us2", 115, 20, 0.85, "skel", "As mayor of Bonetown, I'm proud to announce the town is bigger! We didn't build anything new. We just moved the hedges."),
    ("Town2/t2_veg", "mb-us3", 160, 60, 1.05, "skel", 'Fresh vegetables! Carrots, onions, and calcium. Calcium is not a vegetable. I sell it anyway.'),
    ("Town2/t2_fish", "mb-us2", 140, 40, 0.95, "skel", 'Fish! Fresh fish! Well. Fish skeletons. We have a lot in common, me and these fish.'),
    ("Town2/t2_bus", "mb-us2", 105, 25, 0.85, "skel", "I've been waiting for the number nine bus since nineteen sixty two. It'll come. Any minute now."),
    ("Town2/t2_pigeon", "mb-us2", 100, 15, 0.8, "skel", 'I feed the pigeons every day. They used to be scared of me. Now they just steal my fingers.'),
    ("Town2/t2_house1", "mb-us1", 150, 75, 1.1, "skel", "Welcome to the neighbourhood! I've baked you a pie. It's mostly crust. I ate the middle. I have no middle."),
    ("Town2/t2_house2", "mb-us3", 160, 50, 1.0, "skel", "I've been mowing this lawn since nineteen eighty. The grass is plastic. It's very well kept."),
    ("Town2/t2_house3", "mb-us2", 125, 35, 0.92, "skel", "Keep off my lawn! ...Sorry. Habit. You can stand on it. Just don't rattle."),
    ("Town2/t2_teacher", "mb-us1", 135, 60, 1.0, "skel", "Class is in session! Today's lesson: the two hundred and six bones of the body. Don't worry. You can count mine."),
    ("Town2/t2_librarian", "mb-us1", 110, 50, 0.95, "skel", "Shhh. This is a library. Even my bones have to be quiet. They're on silent."),
    ("Town2/t2_fire", "mb-us2", 140, 35, 0.95, "skel", "Bonetown Fire Station! We've never had a fire. Skeletons don't burn very well. We mostly rescue cats. The cats are also skeletons."),
    ("Town2/t2_farmer", "mb-us3", 130, 40, 0.9, "skel", "Welcome to the farm! Corn, pumpkins, and a scarecrow who isn't very scary. He's family."),
    ("Town2/g_ducks", "mb-us2", 140, 40, 1.0, "narr", 'The ducks are skeletons too. They still quack. Nobody knows how.'),
    ("Town2/g_clock", "mb-us2", 140, 40, 1.0, "narr", "Bong! Bong! The town hall clock strikes the hour. Every hour is thirteen o'clock here."),
    ("Town2/g_scarecrow", "mb-us3", 150, 55, 1.05, "skel", "I'm a scarecrow! The crows aren't scared. They think I'm a perch. I've given up."),
    ("Town2/g_cow", "mb-us2", 120, 20, 0.85, "skel", "Moo. I'm a cow skeleton. Yes, I still make milk. No, you don't want it."),
    ("Town2/g_schoolbell", "mb-us2", 140, 40, 1.0, "narr", "Ring ring! School's out! Everyone rattles home."),
    ("Town2/g_ladder", "mb-us2", 140, 40, 1.0, "narr", "The ladder goes up! It's rescuing a cat. The cat is fine. The cat is always fine."),
    ("Town2/w_mail", "mb-us3", 165, 55, 1.05, "skel", "Mail's here! Letters for everyone! Well, bills for everyone. And one postcard from the Root Cellar. It's blank."),
    ("Town2/w_jog", "mb-us1", 180, 70, 1.15, "skel", "Morning jog! Five laps! My knees rattle but I don't feel a thing!"),
    ("Town2/w_scooter", "mb-us3", 195, 90, 1.35, "skel", "Wheee! Out of the way! I can't stop! These scooters have no brakes!"),
    ("Town2/w_dog", "mb-us2", 140, 40, 0.95, "skel", "This is Biscuit. He's a skeleton dog. He fetches his own bones. Mostly his ribs."),
    ("Town2/w_tourist", "mb-us1", 150, 65, 1.05, "skel", "We're tourists! We came for the fountain. It's out of order. Five stars!"),
    ("Town2/w_tourist2", "mb-us3", 140, 50, 1.0, "skel", 'Is this the way to the apartments? Every direction is the way to the apartments.'),
    ("Fan/mk_streamer", "mb-us3", 170, 60, 1.1, "skel", "Okay. Okay. Nothing is going to jump out. Nothing is going to... AAAAH! I'm fine. I'm fine. My bones left my body for a second."),
    ("Fan/mk_fan", "mb-us1", 150, 65, 1.05, "skel", "I've watched every single video. Twice. I don't sleep. I'm a skeleton. It's very efficient."),
    ("Fan/mk_scare", "mb-us2", 140, 40, 1.0, "narr", "Jump scare! The streamer screamed so hard his jaw came off. It's fine. It clicks back on."),
    ("Fan/mk_doora", "mb-us2", 140, 40, 1.0, "narr", 'You chose door A. You chose wisely. You win: a sandwich.'),
    ("Fan/mk_doorb", "mb-us2", 140, 40, 1.0, "narr", "You chose door B. You chose poorly. Go back and choose again. You'll choose poorly again."),
    ("Fan/mk_vault", "mb-us2", 140, 40, 1.0, "narr", 'The heist was a success! We stole: nothing. The vault was empty. Great teamwork, everyone.'),
    ("Fan/mk_plaque", "mb-us2", 135, 40, 1.0, "narr", "A fan room, inspired by Markiplier's videos: let's plays, horror game screams, heists, and choose your own adventures. An unofficial tribute. Not affiliated."),
    ("Fan/fl_costume", "mb-us3", 175, 70, 1.15, "skel", "I'm not a flamingo. I'm a skeleton in a flamingo costume. Don't tell the flamingos."),
    ("Fan/fl_victim", "mb-us2", 130, 35, 0.92, "skel", "I've pressed the free admin button eleven times. It's never been free admin. I'll press it again. It might be this time."),
    ("Fan/fl_flamingo", "mb-us1", 185, 85, 1.3, "skel", "Squawk! I'm a flamingo! I stand on one leg because the other leg is a skeleton."),
    ("Fan/fl_door", "mb-us2", 140, 40, 1.0, "narr", "It's a door! It opens onto a brick wall. You've been trolled."),
    ("Fan/fl_admin", "mb-us3", 160, 55, 1.05, "narr", 'Free admin! You are now the admin of: the start of the room. Have fun!'),
    ("Fan/fl_plaque", "mb-us2", 135, 40, 1.0, "narr", "A fan room, inspired by Flamingo's videos: chaotic blocky games, a lot of trolling, and a lot of pink. An unofficial tribute. Not affiliated."),
    ("Fan/rt_vlog", "mb-us3", 165, 65, 1.1, "skel", "Day one! I've got one penny and a dream. Mostly the penny. Let's see how far it gets us!"),
    ("Fan/rt_hitch", "mb-us2", 140, 40, 0.95, "skel", 'Anyone heading west? I can pay you. I have a penny. Or exposure. Mostly the penny.'),
    ("Fan/rt_jar", "mb-us2", 140, 40, 1.0, "narr", 'The donation jar! Every penny helps. This one is mostly buttons. The buttons help too.'),
    ("Fan/rt_t1", "mb-us2", 140, 45, 1.0, "narr", "One penny. That's all we've got. Let's trade it for something better."),
    ("Fan/rt_t2", "mb-us2", 140, 45, 1.0, "narr", 'Traded the penny for a paperclip. Progress!'),
    ("Fan/rt_t3", "mb-us2", 140, 45, 1.0, "narr", 'Paperclip, for a pencil. We are moving up in the world.'),
    ("Fan/rt_t4", "mb-us2", 140, 45, 1.0, "narr", "Pencil, for a bicycle! Don't ask how. Nobody knows how."),
    ("Fan/rt_t5", "mb-us2", 140, 45, 1.0, "narr", "Bicycle, for a car! It doesn't have an engine. But it's a car!"),
    ("Fan/rt_plaque", "mb-us2", 135, 40, 1.0, "narr", "A fan room, inspired by Ryan Trahan's videos: penny challenges, trading up, and very long road trips. An unofficial tribute. Not affiliated."),
    ("Scream/sc_plaque", "mb-us2", 135, 40, 1.0, "narr", 'The Scream Room. A room dedicated entirely to screaming. Please scream responsibly.'),
    ("Scream/sc_booth", "mb-us3", 120, 99, 1.3, "skel", 'AAAAAAAAAAAAAAAAAAAAAAAAAH!'),
    ("Scream/sc_goat", "mb-us2", 150, 70, 1.2, "skel", 'Meeeeeeh! AAAAAAAAAAAAH! ...meh.'),
    ("Scream/sc_kettle", "mb-us1", 90, 99, 1.5, "glitch", 'Eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee!'),
    ("Scream/sc_chicken", "mb-us3", 110, 95, 1.45, "skel", 'Aaaaaaaaaaaaaaaaaaahhhhhh... eeeeee.'),
    ("Scream/sc_void", "mb-us2", 130, 30, 0.9, "narr", "You scream into the void. The void screams back. Politely. Then it asks if you're okay."),
    ("Scream/sc_hush", "mb-us1", 120, 60, 1.0, "skel", "Shhhhh! This is a quiet room! ...AAAAAAAAAAAH! Sorry. It's catching."),
    ("Scream/sc_hobby", "mb-us3", 160, 60, 1.05, "skel", "I've been screaming since nineteen eighty seven. I'm not scared. It's a hobby. I'm very good at it."),
    ("Four/iv_bro1", "mb-us3", 175, 70, 1.15, "skel", "Today's project: a rollercoaster! In the backyard! Out of spare ribs! It's going to be the best day of summer!"),
    ("Four/iv_bro2", "mb-us2", 90, 20, 0.85, "skel", '...Yep.'),
    ("Four/iv_sis", "mb-us1", 175, 80, 1.15, "skel", "Mom! They're building a rollercoaster again! ...Mom? MOM!"),
    ("Four/iv_mom", "mb-us2", 140, 40, 1.0, "narr", "Mom's car pulls into the driveway. The rollercoaster vanishes. Nobody knows where it goes."),
    ("Four/iv_gnome", "mb-us2", 120, 30, 0.9, "skel", "I'm a garden gnome. I'm also a secret agent. Please act natural. Stop looking at me."),
    ("Four/iv_doc", "mb-us2", 140, 35, 0.95, "glitch", 'Behold! The Shrink Ray Three Thousand! It shrinks: nothing. It has never worked. But behold it anyway!'),
    ("Four/iv_plaque", "mb-us2", 135, 40, 1.0, "narr", 'A summer backyard, inspired by cartoons about inventing something new every single day of the holidays.'),
    ("Four/nb_kid", "mb-us3", 150, 55, 1.0, "skel", "Day one of middle school. I've already lost my locker combination. And my dignity. Mostly the locker combination."),
    ("Four/nb_friend", "mb-us1", 175, 85, 1.2, "skel", 'Hi! Want to be best friends? We already are! I decided!'),
    ("Four/nb_brother", "mb-us2", 135, 25, 0.9, "skel", "My band is called Loud Bones. We know one song. It's very loud. It's the only thing it is."),
    ("Four/nb_cheese", "mb-us2", 140, 40, 1.0, "narr", "Don't touch the cursed cheese. Nobody has touched the cursed cheese since nineteen ninety eight. It's still there. It's still cursed."),
    ("Four/nb_gym", "mb-us2", 130, 35, 0.95, "skel", 'Gym class! Today we climb the rope. Nobody has ever reached the top. Nobody ever will.'),
    ("Four/nb_plaque", "mb-us2", 135, 40, 1.0, "narr", 'A notebook middle school, inspired by books drawn in a diary, about surviving the worst years of your life. In pencil.'),
    ("Four/sb_cook", "mb-us3", 185, 80, 1.25, "skel", "Order up! One clam burger, extra kelp! I love this job! I've worked here every day since I was a skeleton!"),
    ("Four/sb_cashier", "mb-us2", 100, 20, 0.85, "skel", "Welcome to the Crusty Clam. Can I take your order. Please don't order."),
    ("Four/sb_boss", "mb-us2", 130, 30, 0.92, "skel", 'Money! Lovely money! Every customer is a coin with legs. Well. Bones.'),
    ("Four/sb_star", "mb-us3", 110, 45, 0.95, "skel", "I'm a starfish. I've been lying here for three weeks. It's my best work."),
    ("Four/sb_jelly", "mb-us2", 140, 40, 1.0, "narr", "Jellyfish! They sting. Not you, though. You're bones. There's nothing to sting."),
    ("Four/sb_burger", "mb-us2", 140, 40, 1.0, "narr", 'Flip! The burger flies up, and lands on the ceiling. Order up?'),
    ("Four/sb_plaque", "mb-us2", 135, 40, 1.0, "narr", 'An undersea town, inspired by cartoons about life at the bottom of the sea.'),
    ("Four/br_bus", "mb-us2", 140, 40, 1.0, "narr", "Thank the bus driver! The bus flies over the island. Nobody knows who's driving. It's a skeleton."),
    ("Four/br_builder", "mb-us3", 185, 70, 1.2, "skel", "Build! Build! Build! I've built forty ramps. I don't know why. I can't stop."),
    ("Four/br_dancer", "mb-us1", 160, 70, 1.1, "skel", "I won a match once. I've been dancing about it ever since. Four years."),
    ("Four/br_storm", "mb-us2", 140, 40, 1.0, "narr", "The storm is closing in! Get to the circle! The circle is very small. It's mostly a hula hoop."),
    ("Four/br_chest", "mb-us2", 140, 40, 1.0, "narr", 'You opened a chest! You found: a slightly legendary spoon.'),
    ("Four/br_drop", "mb-us2", 140, 40, 1.0, "narr", "Supply drop! It's full of bandages. Skeletons don't bleed. Thanks, though."),
    ("Four/br_noob", "mb-us3", 150, 60, 1.05, "skel", "Where do I land? Where's everyone going? Why am I alone? ...Oh no. Oh no."),
    ("Four/br_plaque", "mb-us2", 135, 40, 1.0, "narr", 'A battle royale island, inspired by the games where a hundred players jump out of a flying bus. Here, everyone is a skeleton.'),
    ("Vault/sp_plaque", "mb-us2", 135, 40, 1.0, "narr", 'A containment facility, in the style of collaborative online horror fiction. Every anomaly in here is brand new. Please do not feed them.'),
    ("Vault/sp_alarm", "mb-us2", 150, 40, 1.0, "radio", "Containment breach. Containment breach. Please remain calm. Calmer than that. That's it."),
    ("Vault/sp_researcher", "mb-us1", 150, 60, 1.05, "skel", "We've been studying the sock dryer for eleven years. It has made four million socks. None of them match. We're very close to a breakthrough."),
    ("Vault/sp_volunteer", "mb-us3", 150, 50, 1.0, "skel", "I volunteered for testing! Today I'm testing the chair. It's very comfortable. Please come and get me in a week."),
    ("Vault/sp_guard", "mb-us2", 110, 20, 0.85, "skel", 'Nothing gets out of this facility. Except the duck. The duck gets out every Tuesday.'),
    ("Vault/sp_terminal", "mb-us2", 130, 30, 0.95, "radio", 'Item: Bone fourteen. Class: hmm. Containment: keep the dryer door shut. Description: produces socks. Socks never pair. Do not ask it about the left ones.'),
    ("Vault/sp_sock", "mb-us2", 140, 40, 1.0, "narr", 'The dryer door opens. Socks. So many socks. None of them match.'),
    ("Vault/sp_box", "mb-us1", 110, 55, 0.95, "skel", "Oh! Hello. I'm the void. Sorry about the darkness. And the endlessness. Would you like a biscuit? I don't have biscuits. Sorry."),
    ("Vault/sp_duck", "mb-us1", 90, 40, 0.9, "glitch", "I know your name. ...It's on your name tag."),
    ("Vault/sp_vend", "mb-us2", 140, 40, 1.0, "narr", "The machine only sells spoons. You pressed for a soda. You got a spoon. It's the best spoon you've ever seen."),
    ("Vault/sp_chair", "mb-us2", 140, 40, 1.0, "narr", "You sat in the chair. It's too comfortable. You've been sitting here for three days. You don't mind."),
    ("Vault/sp_door", "mb-us2", 140, 40, 1.0, "narr", 'Anomaly Bone nine nine nine: a door that always leads to the lobby. Researchers are furious.'),
    ("Portal/rm_plaque", "mb-us2", 135, 40, 1.0, "narr", 'An interdimensional garage, inspired by cartoons about a cranky genius dragging his family through portals. Everyone here is a skeleton. In every dimension.'),
    ("Portal/rm_genius", "mb-us2", 150, 25, 0.88, "skel", 'Listen. I built a portal to every dimension there is. Most of them are worse. One of them is just this garage. But pink.'),
    ("Portal/rm_anxious", "mb-us3", 170, 75, 1.15, "skel", 'Oh boy. Oh no. Are we going through the portal again? I left some of my bones in the last dimension. The important ones.'),
    ("Portal/rm_flux", "mb-us2", 140, 40, 1.0, "narr", "The thingamaflux. Everyone has one. Nobody can explain it. You turn the handle, and it goes thwup. That's all it does. It's essential."),
    ("Portal/rm_tv1", "mb-us3", 160, 55, 1.05, "radio", "Channel ninety: celebrity housewives of the sun. They're all on fire. Nobody minds."),
    ("Portal/rm_tv2", "mb-us3", 160, 55, 1.05, "radio", 'Channel twelve: a cooking show where the food cooks the chef.'),
    ("Portal/rm_tv3", "mb-us3", 160, 55, 1.05, "radio", 'Channel three: a man shouting the weather at a goat. The goat disagrees.'),
    ("Portal/rm_tv4", "mb-us3", 160, 55, 1.05, "radio", 'Channel infinity: an advert for a car that is also a dog. It runs on treats.'),
    ("Portal/rm_sandwich", "mb-us1", 130, 50, 0.95, "skel", "I'm a sandwich. Everyone here is a sandwich. Please don't look at me like that."),
    ("Portal/rm_alien", "mb-us3", 150, 80, 1.2, "glitch", 'Welcome to the galactic market! Three eyes, two prices, and one refund policy: no.'),
    ("Portal/rm_saucer", "mb-us2", 140, 40, 1.0, "narr", 'The saucer takes off! It flies in a circle. It has never flown in anything but a circle.'),
    ("Portal/rm_sideways", "mb-us2", 140, 40, 1.0, "narr", 'Welcome to the sideways dimension. The floor is the wall. Your coffee is everywhere.'),
    ("Portal/rm_pink", "mb-us2", 140, 40, 1.0, "narr", "It's the garage again. But pink. The skeletons here are pink too. They seem happier."),
    ("Portal/rm_portal", "mb-us2", 140, 40, 1.0, "narr", 'Portal opened! Please mind the green.'),
    ("Water/wp_plaque", "mb-us2", 135, 40, 1.0, "narr", 'Splashbone Water Park! A bowl slide, a lazy river, a wave pool, and a very big bucket. Skeletons float surprisingly well.'),
    ("Water/wp_bowl", "mb-us3", 165, 55, 1.05, "radio", 'Welcome to the Bone Bowl! Climb the tower, sit in a raft, keep your arms inside the tube. Round and round you go, and down the hole!'),
    ("Water/wp_lifeguard", "mb-us2", 140, 30, 0.92, "skel", "No running! No diving! No... is that a skeleton at the bottom of the deep end? Oh. It's all of you. Carry on."),
    ("Water/wp_sunscreen", "mb-us1", 140, 65, 1.05, "skel", "Sunscreen? Don't need it. No skin. I'm just here to get a tan on my bones."),
    ("Water/wp_bucket", "mb-us2", 140, 40, 1.0, "narr", 'The giant bucket tips! Splash! Everyone underneath is now very wet. And still bones.'),
    ("Water/wp_snack", "mb-us3", 160, 60, 1.05, "skel", 'Hot dogs! Nachos! Frozen lemonade! It all goes straight through me. Like everything.'),
    ("Water/wp_wave", "mb-us2", 140, 40, 1.0, "narr", 'Wave pool! Every ten minutes, a wave! The wave is about this big.'),
    ("Water/wp_kid", "mb-us3", 190, 95, 1.4, "skel", 'Again! Again! Can we go on the bowl again? Can we? Can we?'),
]
SKV.update({"santa": ("mb-us2", 125, 20, 0.85), "elf": ("mb-us1", 190, 90, 1.35), "reindeer": ("mb-us3", 150, 55, 1.05), "snowman": ("mb-us2", 130, 40, 0.95)})
COMBO += [
    ("xmas_1", ["santa"], "On the first day of Christmas, my true love gave to me..."),
    ("xmas_2", ["elf"], "A skull in a fir tree!"),
    ("xmas_3", ["reindeer"], "On the second day of Christmas, my true love gave to me..."),
    ("xmas_4", ["snowman"], "Two femurs, and..."),
    ("xmas_5", ["santa", "elf", "reindeer", "snowman"], "A skull in a fir tree! Merry Christmas!"),
]


SKV.update({"shrieka": ("mb-us1", 120, 99, 1.4), "howler": ("mb-us3", 120, 70, 1.15), "yelpington": ("mb-us2", 120, 40, 0.95), "bellow": ("mb-us2", 110, 5, 0.78)})
COMBO += [
    ("scream_1", ["bellow"], "Aaaaaaaaaah!"),
    ("scream_2", ["yelpington"], "Aaaaaaaaaah!"),
    ("scream_3", ["howler"], "Aaaaaaaaaah!"),
    ("scream_4", ["shrieka"], "Eeeeeeeeeeeeeh!"),
    ("scream_5", ["bellow", "yelpington", "howler", "shrieka"], "AAAAAAAAAAAAAAAAAAAH!"),
]


# The expansion: each Station area's new wing (Audio/More/mo_<area>_<who>.ogg)
F1, M2, M3 = "mb-us1", "mb-us2", "mb-us3"
AREAS3 = [
    # the Inn's Grand Ballroom
    ("More/mo_inn_host", F1, 140, 55, 1.0, "skel", "Welcome to the Grand Ballroom, darling. Dress code is formal. Flesh is optional. Most of us opted out."),
    ("More/mo_inn_dancer", M3, 165, 70, 1.1, "skel", "Five, six, seven, eight! Step, turn, dip! Careful on the dip. Last week I dipped and my spine went on without me."),
    ("More/mo_inn_spa", F1, 125, 45, 0.95, "skel", "Our signature treatment is the bone polish. Thirty minutes of buffing and you'll shine like a new tooth."),
    ("More/mo_inn_chandelier", M2, 140, 40, 1.0, "narr", "The chandelier drops. It stops one inch above the dance floor. It does that every night. Nobody knows who taught it."),
    ("More/mo_inn_ball", M2, 140, 40, 1.0, "narr", "The mirror ball comes down and spins. A hundred tiny skeletons dance on the walls."),
    ("More/mo_inn_tub", M3, 130, 50, 0.95, "skel", "Ahh. Do you mind? I'm soaking. Well. I'm soaking through. That's the trouble with being all gaps."),
    # the Speed Zone's Loop Canyon
    ("More/mo_spd_mech", M2, 150, 35, 0.95, "skel", "New tyres, new spokes, new shoelaces. You're good to go. Try not to go faster than your own skeleton this time."),
    ("More/mo_spd_time", F1, 175, 70, 1.1, "skel", "Your lap time was four point two seconds. The record is four point one. The record holder is a very determined squirrel."),
    ("More/mo_spd_racer", M3, 160, 60, 1.05, "skel", "Loops? I love loops. Upside down is just right side up for people who are fast enough."),
    ("More/mo_spd_blur", M2, 140, 40, 1.0, "narr", "Something blue goes past. You hear the word cool. It is already gone."),
    ("More/mo_spd_spring", M2, 140, 40, 1.0, "narr", "Boing! The spring launches nothing at all, very, very high."),
    ("More/mo_spd_boulder", M3, 150, 55, 1.0, "skel", "Rock coming through! Don't worry. It's a friendly rock. It just doesn't know how to stop."),
    # the Platform World's Cloud Kingdom
    ("More/mo_plt_cloud", F1, 150, 70, 1.1, "skel", "Welcome to the Cloud Kingdom! The platforms are solid. The clouds are not. Please do not stand on the clouds. We lose a tourist a week."),
    ("More/mo_plt_merchant", M3, 155, 65, 1.05, "skel", "Mushrooms! Get your mushrooms! Red ones make you taller. Green ones make you happier. Purple ones, I'm not allowed to talk about."),
    ("More/mo_plt_guard", M2, 120, 30, 0.9, "skel", "Halt! Who goes there? Oh. A visitor. The princess is in another castle. Actually, there's no princess. Just me. I'm lonely."),
    ("More/mo_plt_block", M2, 140, 40, 1.0, "narr", "Bonk! A coin pops out of the block. The block looks pleased with itself."),
    ("More/mo_plt_slab", M3, 110, 20, 0.85, "skel", "Grr! I'm the grumpy slab! I fall on things! It's not personal. It's just gravity and a bad attitude."),
    ("More/mo_plt_pipe", M3, 180, 85, 1.25, "skel", "Surprise! I live in a pipe! I'm a plant! I bite! Please water me."),
    # North Carolina's Outer Banks
    ("More/mo_nc_keeper", M2, 125, 30, 0.9, "skel", "I've kept this lighthouse lit for two hundred years. Ships stay off the shoals. Seagulls stay off my head. Mostly."),
    ("More/mo_nc_kite", F1, 165, 75, 1.15, "skel", "Best wind on the whole coast! People came here to learn to fly. I came here to fly a kite. I'm winning."),
    ("More/mo_nc_pirate", M3, 125, 25, 0.9, "skel", "Arr! These waters were full of pirates once. Now it's mostly me and a very confused crab. The crab is my first mate."),
    ("More/mo_nc_flight", M2, 140, 40, 1.0, "narr", "The flying machine lifts off the sand. Twelve whole seconds in the air! History is made. And then it lands in a dune."),
    ("More/mo_nc_horses", M2, 140, 40, 1.0, "narr", "Wild horses gallop along the beach. They've lived on these islands for hundreds of years. They don't care about you at all."),
    ("More/mo_nc_beam", M2, 140, 40, 1.0, "narr", "The lighthouse beam sweeps over the sea. Somewhere out there, a ship says thank you."),
    # the Digital Circus's Backstage
    ("More/mo_cir_hand", M2, 135, 35, 0.95, "skel", "Staff only, pal. I move the scenery. Sometimes the scenery moves me. We take turns."),
    ("More/mo_cir_juggle", M3, 175, 80, 1.2, "skel", "Three balls! Four balls! Five! Six! I don't know where they come from! I just keep juggling! Please send help!"),
    ("More/mo_cir_understudy", F1, 145, 60, 1.0, "skel", "I'm the understudy for every act. I've been waiting four hundred years for someone to call in sick. Nobody here can get sick."),
    ("More/mo_cir_exit", M2, 140, 40, 1.0, "glitch", "The exit door swings open. Behind it: bricks. Just bricks. The door closes, as if nothing happened."),
    ("More/mo_cir_glitch", M2, 140, 40, 1.0, "glitch", "Error. Error. This prop has stopped responding. Would you like to wait? Waiting. Waiting. It's fine now."),
    ("More/mo_cir_cannon", M3, 180, 85, 1.25, "skel", "Ladies and gentlebones! The human cannonball! Fire! Wheee! I'll be back in about a minute!"),
    # the Skits' Writers' Room
    ("More/mo_skt_head", M2, 135, 35, 0.95, "skel", "Okay team, we need a skit by Friday. It's Friday. We need a skit by now. Anyone? Anyone at all?"),
    ("More/mo_skt_gary", M3, 145, 55, 1.0, "skel", "What if, and hear me out, the door is a window. No? What if the window is a door. I've got about forty of these."),
    ("More/mo_skt_intern", F1, 170, 80, 1.15, "skel", "I'm just the intern! I fetch coffee, I fetch paper, and once they sent me to fetch the punchline. I'm still looking."),
    ("More/mo_skt_coffee", M2, 140, 40, 1.0, "narr", "The coffee machine gurgles, shakes, and produces one single drop. The writers cheer."),
    ("More/mo_skt_laugh", M2, 140, 40, 1.0, "narr", "Ha ha ha ha ha! The laugh track plays. Nobody said anything funny. The machine doesn't care."),
    ("More/mo_skt_bin", M2, 140, 40, 1.0, "narr", "The idea bin is full of ideas. All of them are crumpled. One of them was genius. We'll never know which."),
    # the Corn Flakes Room's Cereal Aisle
    ("More/mo_crn_milk", M2, 130, 35, 0.95, "skel", "Milk delivery! Great for your bones. Trust me. Look at my bones. All milk."),
    ("More/mo_crn_knight", M3, 150, 50, 1.05, "skel", "I am Sir Flakes a Lot, defender of breakfast! I have sworn to protect the crunch. The soggy shall not pass!"),
    ("More/mo_crn_shopper", F1, 145, 60, 1.0, "skel", "Corn flakes, or corn flakes? Or maybe corn flakes. Oh, I can never decide. I've been in this aisle since nineteen fifty."),
    ("More/mo_crn_avalanche", M2, 140, 40, 1.0, "narr", "Cleanup on aisle two! A tower of cereal boxes comes crashing down."),
    ("More/mo_crn_fountain", M2, 140, 40, 1.0, "narr", "The milk fountain sprays a fountain of milk. It's for the cereal. And for your bones."),
    ("More/mo_crn_prize", M3, 180, 85, 1.25, "skel", "Free toy inside! It's me! I'm the toy! Collect all one of us!"),
    # Crystal Cove's Moon Temple
    ("More/mo_gem_keeper", F1, 125, 50, 0.95, "skel", "This temple has stood for five thousand years. It was built by gems. It was dusted by me. Mostly by me."),
    ("More/mo_gem_sculptor", M3, 150, 60, 1.05, "skel", "I carve statues out of crystal. Big ones, small ones, one of my cousin. He says it doesn't look like him. It's better."),
    ("More/mo_gem_tourist", M2, 155, 45, 1.0, "skel", "Excuse me, is this the gift shop? I took a wrong warp pad. Then another. Then about nine. Where am I?"),
    ("More/mo_gem_statue", M2, 140, 40, 1.0, "narr", "The statue's gem begins to glow and turn. Something ancient wakes up. It yawns. It goes back to sleep."),
    ("More/mo_gem_bubbles", M2, 140, 40, 1.0, "narr", "The bubbled gems float up into the dark, safe in their little bubbles."),
    ("More/mo_gem_hand", M2, 140, 40, 1.0, "narr", "The giant stone hand waves at you. Slowly. Politely. Wave back. It's only polite."),
    # Critter Meadow's Critter Cave
    ("More/mo_crt_hiker", M3, 150, 55, 1.0, "skel", "I've been hiking this cave for weeks! I've seen bats, I've seen rocks, I've seen rocks that were bats. Bring a torch."),
    ("More/mo_crt_caver", F1, 150, 65, 1.05, "skel", "Deep down, past the crystals, there's a critter nobody's ever caught. I haven't caught it either. I did catch a cold."),
    ("More/mo_crt_angler", M2, 115, 25, 0.88, "skel", "Fishing in an underground lake takes patience. I've been here thirty years. One boot. One more boot. Then two more boots."),
    ("More/mo_crt_pebble", M2, 140, 40, 1.0, "narr", "A wild Pebblet rolls past! It's not attacking. It just can't stop rolling."),
    ("More/mo_crt_bat", M2, 140, 40, 1.0, "narr", "A Puffbat swoops down out of the dark! It's very fluffy. It's very confused. It flies back up."),
    ("More/mo_crt_boot", M2, 115, 25, 0.88, "skel", "I've got a bite! It's big! It's... a boot. Another boot. I'll start a shop."),
    # the Shadow Below's School Library
    ("More/mo_shd_librarian", F1, 115, 40, 0.9, "skel", "Shh. This is a library. The books are sleeping. If you wake the encyclopedias, they won't stop talking."),
    ("More/mo_shd_monitor", M3, 160, 60, 1.05, "skel", "Hall pass? No hall pass, no hall. Rules are rules. I made the rules. I'm very proud of the rules."),
    ("More/mo_shd_sleepy", M3, 95, 40, 0.9, "skel", "Five more minutes. Five more centuries. Is the bell? Did the bell? Wake me up for lunch."),
    ("More/mo_shd_secret", M2, 140, 40, 1.0, "narr", "Pull the right book, and the whole bookcase swings open. Behind it: more books. It's a very thorough library."),
    ("More/mo_shd_vend", M2, 140, 40, 1.0, "narr", "Clunk. The vending machine drops a can of soda. Nobody put any money in. It just likes you."),
    ("More/mo_shd_book", M2, 140, 40, 1.0, "narr", "A book flaps up off the table and circles the room. Overdue books are like that."),
    # Ink Plaza's Ink Tower Stage
    ("More/mo_ink_dj", M3, 160, 60, 1.05, "skel", "Turn it up! Louder! Louder! The bass is so deep you can hear it in your bones. Especially here. We're all bones."),
    ("More/mo_ink_merch", F1, 160, 70, 1.1, "skel", "Band tees! Ink-stained, pre-splatted, one size fits all. That's a lie. One size fits skeletons."),
    ("More/mo_ink_ref", M2, 140, 35, 0.95, "skel", "I'm the referee. I count who inked the most floor. Tonight the winner is everybody. Also the floor."),
    ("More/mo_ink_band", M2, 140, 40, 1.0, "narr", "The band kicks in! Drums, guitar, and a lot of very enthusiastic splatting."),
    ("More/mo_ink_rain", M2, 140, 40, 1.0, "narr", "It's raining ink! Orange and blue drops splash all over the plaza. Bring an umbrella. Bring two."),
    ("More/mo_ink_blast", M2, 140, 40, 1.0, "narr", "The speaker stack goes boom. Everybody's bones rattle in time."),
    # the Dread Depths' Reactor Core
    ("More/mo_drd_doctor", F1, 130, 45, 0.95, "skel", "The core is perfectly stable. Ninety nine percent stable. Ninety eight. Please don't touch anything."),
    ("More/mo_drd_bot", M2, 120, 20, 0.85, "radio", "Maintenance unit M zero. Status: sweeping. Status: still sweeping. This floor has been sweeping me back for years."),
    ("More/mo_drd_pirate", M3, 135, 30, 0.9, "skel", "We came to steal the reactor. Then we saw how heavy it was. Now we just work here. The dental plan is great."),
    ("More/mo_drd_alarm", M2, 140, 40, 1.0, "radio", "Warning. Core temperature rising. Warning. Please evacuate. Or don't. The core has been doing this all week."),
    ("More/mo_drd_jelly", M2, 140, 40, 1.0, "narr", "A glowing jelly floats over to say hello. It wants to give you a hug. Do not accept the hug."),
    ("More/mo_drd_lift", M2, 140, 40, 1.0, "narr", "The cargo lift rattles all the way up to the ceiling with one orange crate. The crate looks thrilled."),
]

AREAS3 += [
    # Hero's Glade: the Sunken Shrine
    ("More/mo_gld_sage", M2, 115, 25, 0.88, "skel", "It's dangerous to go alone. That's why I never go anywhere. I've been sitting on this rock for three hundred years. Very safe."),
    ("More/mo_gld_fairy", F1, 170, 90, 1.25, "skel", "Welcome to the fountain! Throw in a coin and make a wish. I'll keep the coin. The wish is between you and the fountain."),
    ("More/mo_gld_shop", M3, 150, 40, 0.95, "skel", "Bombs, arrows, a slightly used shield. Everything a young hero needs. No refunds. No returns. No questions."),
    ("More/mo_gld_chest", M2, 140, 40, 1.0, "narr", "Da da da daaa! You got a key! It opens this chest. Which is already open. Congratulations."),
    ("More/mo_gld_sword", M2, 140, 40, 1.0, "narr", "The sword slides out of the stone! Then slides right back in. Not today, it says. Come back with more courage. And bigger arms."),
    ("More/mo_gld_pot", M3, 155, 55, 1.0, "skel", "Hey! Stop smashing pots! Every hero comes in here smashing pots. Do you know how long these take to make?"),
    # Honeycomb Hollow: the Great Hive
    ("More/mo_hny_queen", F1, 125, 50, 0.95, "skel", "I am the Queen of the Great Hive. My subjects make the honey. I make the decisions. Mostly the decision to have more honey."),
    ("More/mo_hny_keeper", M2, 135, 35, 0.95, "skel", "Beekeeping's easy when you're a skeleton. They can't sting what isn't there. They do try. Bless them."),
    ("More/mo_hny_jars", M3, 145, 50, 1.0, "skel", "I collect jars. Empty jars, full jars, jars of jars. This one's my favourite. It's a jar."),
    ("More/mo_hny_drip", M2, 140, 40, 1.0, "narr", "A big drop of honey falls from the ceiling. Splat. That's going to be sticky for a week."),
    ("More/mo_hny_swarm", M2, 140, 40, 1.0, "narr", "The bees swirl around in a buzzing cloud. They're not angry. They're dancing. It's how they give directions."),
    ("More/mo_hny_jar", M3, 175, 85, 1.25, "skel", "Hey! Hey! I'm a talking honey jar! Please don't eat me! Eat the other jars! They don't talk!"),
    # Dragon Shore: the Dragon Hatchery
    ("More/mo_drg_sitter", F1, 140, 60, 1.0, "skel", "Every egg in this hatchery is a little dragon waiting to happen. I keep them warm. I knit them tiny scarves."),
    ("More/mo_drg_thief", M3, 155, 40, 0.95, "skel", "Eggs? What eggs? I don't know anything about eggs. These are very large pockets. Nothing to see."),
    ("More/mo_drg_prof", M2, 130, 35, 0.92, "skel", "Fascinating! A dragon egg takes a hundred years to hatch. I've been waiting ninety nine. Any minute now."),
    ("More/mo_drg_hatch", M3, 185, 95, 1.35, "skel", "Squeak! I hatched! Hello world! Is this the world? It's very sandy! Am I a dragon? I feel like a dragon!"),
    ("More/mo_drg_sneeze", M2, 140, 40, 1.0, "narr", "Somewhere a dragon sneezes. A fireball whooshes across the hatchery. Bless you, says everyone."),
    ("More/mo_drg_gems", M2, 140, 40, 1.0, "narr", "The gem chest spins open. Shiny! Every dragon loves shiny."),
    # Hero Tower: the Training Room
    ("More/mo_her_drill", M2, 150, 25, 0.9, "skel", "Drop and give me twenty! Twenty bones! I don't care which ones! Move it, recruits!"),
    ("More/mo_her_gadget", F1, 165, 70, 1.1, "skel", "This gadget shoots grappling hooks. This one shoots foam. This one shoots me, somehow. Still working on that one."),
    ("More/mo_her_sidekick", M3, 140, 55, 1.0, "skel", "I was a sidekick for forty years. Fetched the coffee, held the cape. Now I'm retired. I still hold the cape. Habit."),
    ("More/mo_her_robot", M2, 120, 20, 0.85, "radio", "Training robot online. Punching sequence engaged. Please stand very still. Or don't. I'm not very accurate."),
    ("More/mo_her_shield", M2, 140, 40, 1.0, "narr", "The shield bounces off the wall, off the ceiling, off a dummy, and right back to where it started. Show-off."),
    ("More/mo_her_lab", M2, 140, 40, 1.0, "narr", "The experiment bubbles, glows, and swells up to twice its size. That's either very good or very bad."),
    # Gloom City: the Rooftops
    ("More/mo_glm_watch", M2, 125, 30, 0.9, "skel", "Quiet night up here. Well. Quiet for Gloom City. Only three mysterious figures so far. That's a slow night."),
    ("More/mo_glm_inform", M3, 110, 30, 0.88, "radio", "You didn't hear it from me. Psst. Something big is going down at the docks. Or the bakery. I always mix them up."),
    ("More/mo_glm_reporter", F1, 175, 70, 1.12, "skel", "Scoop of the century! A masked stranger saves the city again! And I missed it again! Do you know what he looks like? Anyone?"),
    ("More/mo_glm_signal", M2, 140, 40, 1.0, "narr", "The skull signal lights up the clouds. Somewhere, someone with a cape sighs and puts their shoes back on."),
    ("More/mo_glm_swing", M2, 140, 40, 1.0, "narr", "A shadowy stranger swings past on a rope. Nobody knows who it is. It's definitely not the night watchman."),
    ("More/mo_glm_gargoyle", M3, 100, 15, 0.82, "skel", "I'm a gargoyle. I've guarded this roof for five hundred years. You're the first person to notice I can turn my head."),
    # Myrtle Beach: the Boardwalk Arcade
    ("More/mo_myr_tokens", F1, 160, 70, 1.1, "skel", "Tokens! Get your tokens! Twenty tokens a dollar! The skee-ball takes three. The claw machine takes your soul."),
    ("More/mo_myr_skee", M3, 160, 55, 1.05, "skel", "Skee-ball champion, nineteen eighty-two through now. The secret is in the wrist. I don't have a wrist anymore. Still champion."),
    ("More/mo_myr_prize", F1, 150, 65, 1.05, "skel", "You've got twelve thousand tickets! You can have a pencil. Or two pencils. Or a slightly larger pencil."),
    ("More/mo_myr_claw", M2, 140, 40, 1.0, "narr", "The claw comes down, grabs a prize, lifts it up, and drops it. Of course it does. That's what claws do."),
    ("More/mo_myr_whack", M3, 175, 80, 1.2, "skel", "Hi! Ow! Hi! Ow! Whack a skull is the worst game! Who invented this? Ow!"),
    ("More/mo_myr_ball", M2, 140, 40, 1.0, "narr", "The skee-ball rolls up the lane, hops over the hundred, and lands in the ten. Every time."),
    # Boone: the Mountain Trail
    ("More/mo_bne_hiker", F1, 150, 55, 1.0, "skel", "I'm walking the whole trail from Georgia to Maine. Two thousand miles. I'm on mile three. I stopped for snacks."),
    ("More/mo_bne_banjo", M3, 140, 45, 0.95, "skel", "Pull up a log, friend. I'll play you a tune. I only know the one tune. It's a good tune though."),
    ("More/mo_bne_ranger", M2, 130, 30, 0.92, "skel", "Leave no trace, take only pictures, and please don't feed the bears. Especially not your sandwiches. They'll never leave."),
    ("More/mo_bne_fire", M2, 140, 40, 1.0, "narr", "The campfire flares up. Somebody's marshmallow is now a small torch. That's how they like it up here."),
    ("More/mo_bne_bear", M3, 110, 20, 0.85, "skel", "Roar! I'm a bear! Well. A bear skeleton. Same thing, really. Do you have any sandwiches?"),
    ("More/mo_bne_leaf", M2, 140, 40, 1.0, "narr", "One leaf falls all the way down from the top of the ridge. It's the first leaf of autumn. Everybody stops to watch."),
    # the Dachshund Room: the Dog Park
    ("More/mo_dox_walker", F1, 155, 60, 1.05, "skel", "I walk twelve dachshunds a day. Their legs are very short. It's less of a walk and more of a slow parade."),
    ("More/mo_dox_groomer", M3, 155, 60, 1.05, "skel", "Bath time! They hate bath time. I've been splashed so many times I'm completely rinsed. Look how clean my ribs are."),
    ("More/mo_dox_judge", F1, 135, 45, 0.95, "skel", "Agility course! Over the hurdle, through the tunnel, and round the pole. Points off for stopping to sniff. So, all the points."),
    ("More/mo_dox_launcher", M2, 140, 40, 1.0, "narr", "Thunk! The ball launcher fires a tennis ball across the park. Twelve dachshunds give chase. One of them finds it. Three days later."),
    ("More/mo_dox_zoomies", M2, 140, 40, 1.0, "narr", "Zoomies! A dachshund runs as fast as its little legs will go, round and round and round. Nobody knows why. Least of all the dachshund."),
    ("More/mo_dox_tunnel", M2, 140, 40, 1.0, "narr", "A dachshund goes into the tunnel. A dachshund comes out of the tunnel. A long time later, the rest of the dachshund comes out."),
    # Extremely Christmas: the Toy Workshop
    ("More/mo_xms_headelf", F1, 170, 85, 1.3, "skel", "Faster! Faster! Only four hundred days until Christmas! That's not a lot of days! Every day is Christmas here, actually. Never mind."),
    ("More/mo_xms_tester", M3, 175, 75, 1.25, "skel", "I'm the toy tester. I test every toy! This one squeaks. This one bounces. This one bit me. That's a fail."),
    ("More/mo_xms_wrap", F1, 165, 80, 1.2, "skel", "Wrapping champion, three years running! I can wrap anything! A bicycle! A piano! Myself, once. That took a while to undo."),
    ("More/mo_xms_confetti", M2, 140, 40, 1.0, "narr", "The present bursts open! Confetti everywhere! Inside: another present. Inside that: more confetti."),
    ("More/mo_xms_robot", M2, 120, 20, 0.85, "radio", "Wind up robot. Walking. Walking. Walking. Please wind me up again. Thank you."),
    ("More/mo_xms_sleigh", M2, 140, 40, 1.0, "narr", "The test sleigh shoots up through the roof! It'll be back by Christmas. Probably this Christmas."),
    # Cellbound: the Warden's Office
    ("More/mo_cel_guard", M2, 125, 25, 0.9, "skel", "Nobody gets out of this prison. Nobody. Well, there was that one fellow with the spoon. But nobody else."),
    ("More/mo_cel_cook", M3, 140, 45, 0.95, "skel", "Soup again. It's always soup. I used to have a spoon for it. Somebody borrowed my spoon. Years ago."),
    ("More/mo_cel_inmate", M3, 135, 35, 0.92, "skel", "I'm doing ninety nine years. I've done ninety eight. Last year's the hardest. The food doesn't help."),
    ("More/mo_cel_hole", M3, 170, 80, 1.2, "skel", "Hello! Is this the way out? No? Is this the warden's office? Oh no. Excuse me. Wrong hole."),
    ("More/mo_cel_tv", M2, 140, 40, 1.0, "narr", "The old TV flickers on all by itself. It's playing a show about a prison. Very on the nose."),
    ("More/mo_cel_spoon", M2, 140, 40, 1.0, "narr", "The spoon. The legendary spoon. It has dug more tunnels than any spoon in history. It's very tired."),
    # the Markiplier room's Fan Art Gallery (fans, not the creator)
    ("More/mo_mrk_plaque", M2, 140, 40, 1.0, "narr", "The Fan Art Gallery. An unofficial fan tribute. Not affiliated with or endorsed by anyone it celebrates."),
    ("More/mo_mrk_curator", M2, 125, 35, 0.92, "skel", "Every painting in this gallery was made by a fan. A skeleton fan. Mostly with their fingers. Very bony fingers."),
    ("More/mo_mrk_fan", F1, 175, 80, 1.2, "skel", "I made a sign! It says hello! I've been holding it up for three days. My arms are fine. They're just bones."),
    ("More/mo_mrk_editor", M3, 120, 35, 0.9, "skel", "I edit videos all night. Cut, cut, jump scare, cut. I haven't slept since nineteen ninety nine. Don't need to. Skeleton."),
    ("More/mo_mrk_scare", M3, 190, 90, 1.35, "skel", "Boo! Ha! Got you! That's what the painting is about. It's a jump scare painting. It's my best work."),
    ("More/mo_mrk_confetti", M2, 140, 40, 1.0, "narr", "Pop! Confetti everywhere! Somebody somewhere just reached a big milestone. Everyone cheers."),
    ("More/mo_mrk_desk", M2, 140, 40, 1.0, "narr", "The editing desk rattles. Rendering. Rendering. Still rendering. It'll be done by Tuesday."),
    # the Flamingo room's Pool Party (guests, not the creator)
    ("More/mo_flm_plaque", M2, 140, 40, 1.0, "narr", "The Pool Party. An unofficial fan tribute. Not affiliated with or endorsed by anyone it celebrates."),
    ("More/mo_flm_guard", M3, 150, 40, 1.0, "skel", "No running! No diving! No pushing! No... honestly I've forgotten the rest. Just be nice."),
    ("More/mo_flm_dj", M3, 165, 70, 1.1, "skel", "Pink lights, pink pool, pink everything! This party is so pink, my bones are blushing."),
    ("More/mo_flm_guest", F1, 160, 70, 1.1, "skel", "Best party ever! I came for the pool, stayed for the flamingos. I can't swim. I just float. Skeletons are very floaty."),
    ("More/mo_flm_float", M2, 140, 40, 1.0, "narr", "The giant inflatable flamingo spins slowly round the pool. It's the real guest of honour."),
    ("More/mo_flm_splash", M3, 185, 90, 1.3, "skel", "Cannonbaaall! Splash! Did you see that? Rate it out of ten! Be honest! Be nice!"),
    ("More/mo_flm_dance", M2, 140, 40, 1.0, "narr", "A plastic lawn flamingo starts dancing on one leg. Nobody plugged it in. It just felt the music."),
    # the Ryan Trahan room's Road Trip (people met along the way, not the creator)
    ("More/mo_trh_plaque", M2, 140, 40, 1.0, "narr", "The Road Trip. An unofficial fan tribute. Not affiliated with or endorsed by anyone it celebrates."),
    ("More/mo_trh_waitress", F1, 155, 60, 1.05, "skel", "Coffee's a penny, pie's two pennies, and the conversation is free. Long way from home, hon?"),
    ("More/mo_trh_hitch", M3, 140, 45, 0.98, "skel", "Going anywhere? I'm going everywhere. I've hitched across the whole country twice. The trick is to look friendly. I just look bony."),
    ("More/mo_trh_gas", M2, 130, 35, 0.92, "skel", "Fill it up? That'll be a lot more than a penny, friend. A lot more. Maybe pay me in jokes."),
    ("More/mo_trh_car", M2, 140, 40, 1.0, "narr", "Beep beep! The little car honks. It's ready for the next fifty states. It's had a long week."),
    ("More/mo_trh_jar", M2, 140, 40, 1.0, "narr", "The penny jar rattles. A hundred pennies. A thousand. Enough to cross the country, one penny at a time."),
    ("More/mo_trh_map", M2, 140, 40, 1.0, "narr", "The road map unfolds, and unfolds, and unfolds. Nobody will ever fold it back up again."),
    # the Scream Room's Whisper Room (quiet voices)
    ("More/mo_wsp_wilma", F1, 110, 40, 0.92, "radio", "Psst. Welcome to the whisper room. Next door they scream. In here, we whisper. It's a lot easier on the jaw."),
    ("More/mo_wsp_mort", M2, 95, 25, 0.85, "skel", "Breathe in. Breathe out. I don't have lungs. I just pretend. It's still very relaxing."),
    ("More/mo_wsp_cousin", F1, 110, 45, 0.92, "skel", "Shh. My cousin runs the library. I run the whisper room. Our family has been shushing people for six generations."),
    ("More/mo_wsp_pin", M2, 100, 40, 1.0, "narr", "You could hear a pin drop in here. Listen. There. You just did."),
    ("More/mo_wsp_sneeze", M3, 160, 60, 1.05, "skel", "Ah. Ah. Ahh. Choo! Sorry! Sorry. Sorry. That was so loud. Everyone's looking at me."),
    ("More/mo_wsp_shush", M2, 100, 40, 1.0, "radio", "Shhhhhh."),
    # the Inventors' Backyard: the Rocket Garage
    ("More/mo_inv_neighbour", F1, 150, 55, 1.0, "skel", "Those kids next door built a rocket again. Last week it was a submarine. In the paddling pool. It worked."),
    ("More/mo_inv_mail", M3, 150, 50, 1.0, "skel", "Parcel for the garage! Fourteen boxes of rocket parts and one box of sandwiches. Somebody's planning a big day."),
    ("More/mo_inv_scout", M2, 140, 35, 0.95, "skel", "Our scout troop is here for the space badge. We'll need to go to space. I've brought snacks. And helmets. And more snacks."),
    ("More/mo_inv_rocket", M2, 140, 40, 1.0, "narr", "Three, two, one, liftoff! The test rocket shoots up and away! It'll come down somewhere. Probably not here."),
    ("More/mo_inv_arm", M2, 120, 20, 0.85, "radio", "Robot arm, ready. Picking up wrench. Picking up wrench. Dropping wrench. Picking up wrench again."),
    ("More/mo_inv_plan", M2, 140, 40, 1.0, "narr", "The blueprint unrolls across the table. It's a plan for a rocket. And a rollercoaster. And a rocket rollercoaster."),
    # the Notebook School: the Cafeteria
    ("More/mo_nbk_lunch", F1, 125, 40, 0.92, "skel", "Today's special is mystery meat. Yesterday's special was mystery meat. Tomorrow's is a surprise. It's mystery meat."),
    ("More/mo_nbk_clown", M3, 180, 80, 1.25, "skel", "Want to see a trick? I can fit eleven grapes in my mouth! Well. Through my mouth. They fall out the bottom."),
    ("More/mo_nbk_council", F1, 160, 65, 1.1, "skel", "Vote for me for student council! I promise longer lunches, shorter lessons, and a better seat in the cafeteria. Anywhere but by the bins."),
    ("More/mo_nbk_fight", M2, 140, 40, 1.0, "narr", "Food fight! A pea flies across the cafeteria. Then a meatball. Then, somehow, a whole trifle."),
    ("More/mo_nbk_meat", M3, 110, 25, 0.88, "skel", "Hello. I'm the mystery meat. Nobody knows what I am. Not even me. It's a lot of pressure."),
    ("More/mo_nbk_milk", M2, 140, 40, 1.0, "narr", "The milk carton spins on the table. Chocolate milk. The good kind. Everybody wants it."),
    # the Seabed Town: the Kelp Forest
    ("More/mo_sea_snail", F1, 140, 60, 1.0, "skel", "I race snails! My snail is the fastest on the seabed. Top speed: one foot an hour. We're in no hurry."),
    ("More/mo_sea_diver", M2, 125, 30, 0.9, "radio", "Diver to surface. I've found the kelp forest. Also, I've lost the surface. Please send directions."),
    ("More/mo_sea_guide", F1, 160, 70, 1.1, "skel", "Welcome to the kelp forest! Stay with the group, mind the jellyfish, and don't let the clam talk you into anything."),
    ("More/mo_sea_jelly", M2, 140, 40, 1.0, "narr", "A jellyfish pulses up through the kelp, glowing pink. It doesn't have a brain. It's doing great anyway."),
    ("More/mo_sea_clam", M3, 105, 20, 0.85, "skel", "I'm the giant clam. I have opinions. Close the door. Use a coaster. And never trust a crab."),
    ("More/mo_sea_treasure", M2, 140, 40, 1.0, "narr", "The old treasure chest burps a stream of bubbles. Inside: gold, pearls, and one very soggy sandwich."),
    # Battle Island: the Storm's Edge
    ("More/mo_bat_builder", F1, 175, 70, 1.15, "skel", "Wall, ramp, floor, roof! I can build a castle in four seconds! I can't build a sandwich, though. Too many layers."),
    ("More/mo_bat_emote", M3, 165, 70, 1.15, "skel", "I don't fight. I just dance. Every match, I dance. I've never won. I've never been happier."),
    ("More/mo_bat_goblin", M3, 175, 50, 1.1, "skel", "Loot! Loot! Shiny loot! Mine! All mine! Okay, you can have one. The small one. The not shiny one."),
    ("More/mo_bat_drop", M2, 140, 40, 1.0, "narr", "A supply drop floats down on a big red balloon. Inside: bandages, a boom box, and one rubber chicken."),
    ("More/mo_bat_wall", M2, 140, 40, 1.0, "narr", "A wooden wall pops up out of nowhere. Somebody's building. Somebody's always building."),
    ("More/mo_bat_storm", M2, 140, 40, 1.0, "narr", "The storm creeps forward a few more steps. Then it sees you looking, and stops. It's shy."),
    # Site Bone: Containment Wing B (all-new anomalies)
    ("More/mo_scb_researcher", M2, 130, 35, 0.92, "skel", "Containment Wing B. Three anomalies, all perfectly safe. That's what the paperwork says. I wrote the paperwork."),
    ("More/mo_scb_security", M3, 125, 25, 0.9, "skel", "My job is to make sure nothing gets out. In twenty years, nothing has. Well. One stair. We don't talk about the stair."),
    ("More/mo_scb_volunteer", M3, 150, 60, 1.05, "skel", "I'm a test volunteer! I've tested all three anomalies! The door was nice. The cube was confusing. The stair, I'm still on the stair."),
    ("More/mo_scb_door", M2, 120, 25, 0.88, "radio", "S C B nine fifty. A door that only opens if you say please. If you say thank you, it closes again, very proudly."),
    ("More/mo_scb_cube", M2, 120, 25, 0.88, "radio", "S C B nine fifty seven. A cube that counts. It has been counting since nineteen sixty. It's on four. Nobody knows what happens at five."),
    ("More/mo_scb_stair", M2, 120, 25, 0.88, "radio", "S C B nine sixty four. One extra stair. Wherever you walk in the dark, there is always one more stair than you expected."),
    # the Interdimensional Garage: the Invention Shed
    ("More/mo_inv2_neighbour", M3, 145, 50, 1.0, "skel", "Every night there's a green flash in that garage. Then somebody comes out wearing a different shirt. Then a different face."),
    ("More/mo_inv2_alien", M3, 190, 95, 1.4, "glitch", "Greetings, Earth skeleton! I am Zorp! I came through the portal for the gift shop. Where is the gift shop? Is this the gift shop?"),
    ("More/mo_inv2_robot", M2, 120, 20, 0.85, "radio", "Helpful robot H three. What is my purpose? To hold this screwdriver. Oh. Okay. I am happy to hold the screwdriver."),
    ("More/mo_inv2_ray", M2, 140, 40, 1.0, "narr", "The shrink ray fires! Everything gets bigger. It's a grow ray. Somebody wired it backwards."),
    ("More/mo_inv2_gravity", M2, 140, 40, 1.0, "narr", "The gravity flipper flips. The gadget on top flips. Everything else stays exactly where it was. Version two will be better."),
    ("More/mo_inv2_snack", M2, 140, 40, 1.0, "narr", "Bing! The snack teleporter beams in one cheese puff from a dimension made entirely of cheese puffs."),
    # the water park: the Wave Pool and Lazy River
    ("More/mo_wat_guard", F1, 150, 50, 1.0, "skel", "Walk, don't run! Whistle! Walk, don't run! Whistle! I've been blowing this whistle so long it's playing a tune."),
    ("More/mo_wat_snack", M3, 150, 55, 1.0, "skel", "Hot dogs, nachos, and slushies in every colour! The blue one turns your teeth blue. And you've got a lot of teeth."),
    ("More/mo_wat_float", M2, 130, 35, 0.92, "skel", "Rent a tube! Float round the lazy river! It takes forty minutes. It takes four hours if you fall asleep. Most people fall asleep."),
    ("More/mo_wat_wave", M2, 140, 40, 1.0, "narr", "Wave incoming! The wave machine sends a big one rolling across the pool. Everybody bobs. Nobody minds."),
    ("More/mo_wat_tube", M2, 140, 40, 1.0, "narr", "A skeleton drifts past on a tube, fast asleep. They've been going round since the park opened."),
    ("More/mo_wat_bucket", M2, 140, 40, 1.0, "narr", "The giant bucket fills, and fills, and tips! Splooosh! Nobody was standing under it. Somebody always is next time."),
    # the Chris and Tripp Show: Behind the Scenes (crew and a fan; the show's stars aren't voiced)
    ("More/mo_ctb_camera", F1, 150, 55, 1.0, "skel", "Camera one, rolling. Camera two, rolling. Camera three is pointing at the snack table. That's fine. Leave it."),
    ("More/mo_ctb_director", M2, 135, 35, 0.95, "skel", "And action! And cut! And action again! That was perfect, let's do it nineteen more times."),
    ("More/mo_ctb_fan", F1, 175, 85, 1.25, "skel", "I've watched episode one forty times! The diamonds bit is my favourite! I say diamonds to everybody now. Diamonds!"),
    ("More/mo_ctb_clap", M2, 140, 40, 1.0, "narr", "Clap! Scene two, take forty one. Everybody's very hopeful about take forty one."),
    ("More/mo_ctb_boom", M2, 140, 40, 1.0, "narr", "The boom mic dips into the shot. Again. Somebody yells cut. Again."),
    ("More/mo_ctb_blooper", M2, 140, 40, 1.0, "narr", "The blooper reel: forty minutes of people laughing, falling over, and saying the wrong line. The best episode yet."),
]

AREAS3 += [
    # Boneanza Park: the Midway
    ("More/mo_prk_barker", M3, 165, 55, 1.05, "skel", "Step right up! Step right up! Win a prize for your sweetheart! Or your skeleton-heart! Three throws a token!"),
    ("More/mo_prk_strong", M2, 115, 15, 0.82, "skel", "I am the strongman! I can lift anything! Except my own arm. That came off in nineteen thirty. Try the bell!"),
    ("More/mo_prk_candy", F1, 165, 80, 1.2, "skel", "Cotton candy! Pink, blue, and bone white! It melts in your mouth. It falls straight through mine."),
    ("More/mo_prk_bell", M2, 140, 40, 1.0, "narr", "Ding! The puck flies all the way up and rings the bell! Nobody's ever done that. The strongman is furious."),
    ("More/mo_prk_rings", M2, 140, 40, 1.0, "narr", "The ring flies, spins, bounces off three bottles and lands on the floor. So close. So very close."),
    ("More/mo_prk_darts", M2, 140, 40, 1.0, "narr", "Pop! One balloon down! Only four hundred more for the big prize."),
    # Precinct 206: Evidence and Interrogation
    ("More/mo_pct_clerk", F1, 120, 35, 0.9, "skel", "Evidence locker. Sign here, initial here, and here, and here. Don't sign there. That's evidence."),
    ("More/mo_pct_detective", M2, 115, 20, 0.85, "skel", "Where were you on the night of the twelfth? I'll tell you where I was. Here. Asking you that. I'm always here."),
    ("More/mo_pct_suspect", M3, 125, 35, 0.9, "skel", "I want my lawyer. He's a skeleton too. We went to school together. He wasn't very good then either."),
    ("More/mo_pct_lamp", M2, 140, 40, 1.0, "narr", "The interrogation lamp swings round and shines straight in the detective's eyes. He confesses to everything."),
    ("More/mo_pct_exhibit", M2, 115, 20, 0.85, "skel", "Exhibit A. A rubber chicken. Found at the scene. We don't know which scene. We have a lot of scenes."),
    ("More/mo_pct_mirror", M3, 150, 50, 1.0, "skel", "This is a one way mirror. You can't see me. I can see you. Hello. Wave if you can't see me."),
    # Pine Pete's: the Kitchen
    ("More/mo_pzk_chef", M2, 135, 40, 0.95, "skel", "Every pizza is made with love, flour, and a little pine needle. That last one isn't on purpose. It just happens."),
    ("More/mo_pzk_driver", M3, 170, 60, 1.1, "skel", "Thirty minutes or it's free! I've never made it in thirty minutes. Everybody's pizza is free. I'm very popular."),
    ("More/mo_pzk_dishes", F1, 140, 55, 1.0, "skel", "I wash the dishes. Every night, the dishes come back dirty. Every night. I don't think anyone's eating off them."),
    ("More/mo_pzk_oven", M2, 140, 40, 1.0, "narr", "Whoomph! The oven flares up. The pizza is done. And so is the oven glove."),
    ("More/mo_pzk_dough", M2, 140, 40, 1.0, "narr", "The dough spins up into the air, round and round. It lands perfectly. The chef bows."),
    ("More/mo_pzk_box", M3, 95, 15, 0.8, "skel", "Hello. Is this my pizza? No. I'm in the pizza box. Close the lid. Close the lid. Please."),
    # Pixel Hollow: the Old Ruins
    ("More/mo_pxr_riddle", M3, 150, 70, 1.1, "skel", "Riddle me this! What has bones but no body? Me! Also you! Also everybody here. It's an easy riddle."),
    ("More/mo_pxr_gate", M2, 110, 25, 0.88, "skel", "None shall pass. Unless they ask nicely. Or answer a riddle. Or wait about a minute, I get bored."),
    ("More/mo_pxr_post", M3, 175, 85, 1.25, "skel", "Letter for you! It's from you! You sent it to yourself! You must have wanted to know something."),
    ("More/mo_pxr_spikes", M2, 140, 40, 1.0, "narr", "The spike tiles pop up! Then back down. They're more of a suggestion."),
    ("More/mo_pxr_star", M2, 140, 40, 1.0, "narr", "A little star glows in the old ruins. You feel like everything is going to be okay."),
    ("More/mo_pxr_rock", M3, 100, 20, 0.85, "skel", "I'm a rock. I don't move. Push all you like. I've been here since the ruins were new. Not moving now."),
    # the Test Annex: the Observation Deck
    ("More/mo_tst_super", F1, 120, 40, 0.92, "radio", "Test subject, you are doing very well. Statistically. Compared to the others. The others are mostly bones now."),
    ("More/mo_tst_janitor", M3, 130, 40, 0.92, "skel", "Somebody's got to clean the test chambers. Goo, glass, little burnt patches. I've seen things. Mostly goo."),
    ("More/mo_tst_subject", M3, 160, 65, 1.05, "skel", "I'm test subject thirty seven! I've passed every test! Well, I've finished every test. That's nearly the same."),
    ("More/mo_tst_cube", M2, 120, 25, 0.88, "radio", "Dispensing one test cube. Please do not become attached to the cube. The cube will not become attached to you."),
    ("More/mo_tst_scan", M2, 120, 25, 0.88, "radio", "Scanning. Bones detected. Many bones. That's normal. Please continue."),
    ("More/mo_tst_drone", M2, 120, 25, 0.88, "radio", "Safety drone online. Hovering safely. Watching safely. Please do not wave at the safety drone."),
    # Blocklands: the Mines
    ("More/mo_blk_miner", M2, 135, 35, 0.95, "skel", "Dig, dig, dig. Found coal. Dig, dig, dig. Found gold. Dig, dig, dig. Found another miner. We said hello."),
    ("More/mo_blk_trader", M3, 120, 50, 0.95, "skel", "Hmm. Hrrm. I'll trade you one shiny ore for twelve emeralds. Or one emerald for twelve ores. I'm bad at maths."),
    ("More/mo_blk_smith", M2, 120, 20, 0.86, "skel", "Bring me ore and I'll make you a pickaxe. Bring me more ore and I'll make you a better pickaxe. It's pickaxes all the way down."),
    ("More/mo_blk_cart", M2, 140, 40, 1.0, "narr", "The minecart rattles off down the track, empty, on its way to somewhere very deep."),
    ("More/mo_blk_boom", M2, 140, 40, 1.0, "narr", "The red block hisses, swells up, and... nothing. It's a dud. Everybody breathes out."),
    ("More/mo_blk_ore", M2, 140, 40, 1.0, "narr", "A chunk of shiny blue ore glints in the torchlight. Diamonds? Better. It's very shiny rock."),
    # the Cold North: the Mead Hall
    ("More/mo_nth_bard", M3, 145, 55, 1.0, "skel", "Sit, friend, and hear the song of the hero who took a cheese wheel to the knee. It's a short song. It's a sad song. It's mostly about cheese."),
    ("More/mo_nth_chief", M2, 110, 15, 0.82, "skel", "Welcome to my hall, traveller! Eat! Drink! Sit anywhere but my chair. That's my chair. Everyone knows that's my chair."),
    ("More/mo_nth_guard", M2, 125, 25, 0.9, "skel", "I used to be a traveller like you. Then I got a really comfy job in a warm hall. Best career move I ever made."),
    ("More/mo_nth_fire", M2, 140, 40, 1.0, "narr", "The long fire roars up the whole length of the hall. Everybody cheers. Somebody's beard is a little bit singed."),
    ("More/mo_nth_mug", M2, 140, 40, 1.0, "narr", "Skol! A mug flies down the table, spinning, and lands right in somebody's hand. Not the right somebody."),
    ("More/mo_nth_shields", M2, 140, 40, 1.0, "narr", "Shield wall! The shields pop up in a line. Nothing's attacking. They just like doing it."),
    # the Mystery Hut: the Deep Woods
    ("More/mo_mys_lumber", M2, 120, 20, 0.86, "skel", "I'm a lumberjack and I'm okay. I chop all day. Well, I used to. The trees around here started chopping back."),
    ("More/mo_mys_hunter", F1, 160, 60, 1.05, "skel", "I've photographed a sea monster, a gnome army and a man made entirely of bees. All of the photos are blurry. All of them."),
    ("More/mo_mys_conspiracy", M3, 150, 50, 1.0, "skel", "Birds aren't real. Squirrels aren't real. I'm not sure I'm real. Write that down. Actually, don't write anything down."),
    ("More/mo_mys_hatch", M2, 140, 40, 1.0, "narr", "The bunker hatch creaks open. A ladder goes down into the dark. A sign at the bottom says, go back up."),
    ("More/mo_mys_eye", M3, 100, 30, 0.85, "glitch", "I see you. I always see you. I have one eye and it's always looking. It's very tiring. Please blink for me."),
    ("More/mo_mys_journal", M2, 140, 40, 1.0, "narr", "The old journal rises off its pedestal and turns its own pages. Most of them say: trust no one. One says: buy milk."),
]


def more_lines():
    for name, v, speed, pitch, rate, kind, text in AREAS3:
        filt = AREA_FILTERS[kind].replace("{v:.3f}", f"{1.8 * SKELETON_BOOST:.3f}")
        yield (name, v, speed, pitch, text, f"asetrate=16000*{rate},aresample=22050," + filt)


def area2_lines():
    for name, v, speed, pitch, rate, kind, text in AREAS2:
        filt = AREA_FILTERS[kind].replace("{v:.3f}", f"{1.8 * SKELETON_BOOST:.3f}")
        yield (name, v, speed, pitch, text, f"asetrate=16000*{rate},aresample=22050," + filt)


def chris_tripp_lines():
    """The Chris and Tripp Show's lines (clips cut from the episode by make_chris_tripp.py): captions only, never re-voiced."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("ct", os.path.join(os.path.dirname(os.path.abspath(__file__)), "make_chris_tripp.py"))
    ct = importlib.util.module_from_spec(spec); spec.loader.exec_module(ct)
    return [(name, text) for name, a, b, who, text in ct.LINES]


def write_captions():
    """Data/captions.txt: clip name|what is said, for every voiced line (the builder shows them as captions)."""
    rows = {}
    for gen in (list(LINES), gag_lines(), skeleton_lines(), rainbow_lines(), town_lines(), extra_lines(), park_lines(), cop_lines(),
                pizza_lines(), pixel_lines(), lab_lines(), north_lines(), mystery_lines(), area_lines(), apart_lines(), area2_lines(), more_lines()):
        for row in gen:
            rows[row[0].split("/")[-1]] = row[4]
    for name, _, text in COMBO:
        rows[name] = text
    for name, text in chris_tripp_lines():
        rows[name] = text
    path = os.path.join(os.path.dirname(OUT), "Data", "captions.txt")
    with open(path, "w", encoding="utf-8") as f:
        f.write("# clip|caption (written by tools/make_voices.py --captions)\n")
        for k in sorted(rows):
            f.write(k + "|" + " ".join(rows[k].split()).replace("|", "/") + "\n")
    print(len(rows), "captions ->", path)


def area_lines():
    for name, v, speed, pitch, rate, kind, text in AREAS:
        filt = AREA_FILTERS[kind].replace("{v:.3f}", f"{1.8 * SKELETON_BOOST:.3f}")
        yield (name, v, speed, pitch, text, f"asetrate=16000*{rate},aresample=22050," + filt)


def make_combo(tmp):
    """Each combo line; a line with several voices is rendered once per voice and mixed, slightly staggered."""
    os.makedirs(os.path.join(OUT, "Combo"), exist_ok=True)
    for name, voices, text in COMBO:
        parts = []
        for k, who in enumerate(voices):
            v, speed, pitch, rate = SKV[who]
            wav = os.path.join(tmp, f"{name}_{k}.wav")
            subprocess.run(["espeak-ng", "-v", v, "-s", str(speed), "-p", str(pitch), "-w", wav, text], check=True)
            parts.append((wav, rate, k * 35))
        out = os.path.join(OUT, "Combo", name + ".ogg")
        args = ["ffmpeg", "-y", "-loglevel", "error"]
        for wav, _, _ in parts: args += ["-i", wav]
        filt = [f"[{i}:a]asetrate=16000*{rate},aresample=22050,adelay={d}|{d}[p{i}]" for i, (_, rate, d) in enumerate(parts)]
        mix = "".join(f"[p{i}]" for i in range(len(parts))) + f"amix=inputs={len(parts)}:normalize=0,volume={(1.8 if len(parts) == 1 else 0.9) * SKELETON_BOOST:.3f},aecho=0.8:0.5:35:0.15,alimiter=limit=0.95:level=disabled,apad=pad_dur=0.25[a]"
        args += ["-filter_complex", ";".join(filt + [mix]), "-map", "[a]", "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", out]
        subprocess.run(args, check=True)
        print("Combo/" + name, os.path.getsize(out) // 1024, "KB")


def rainbow_lines():
    for colour, v, speed, pitch, rate, text in RAINBOW:
        yield ("Rainbow/rb_" + colour, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled")


def gag_lines():
    for fname in ("gags.txt", "town_gags.txt", "park_gags.txt", "precinct_gags.txt", "pizza_gags.txt", "pixel_gags.txt", "lab_gags.txt", "more_gags.txt"):
      path = os.path.join(os.path.dirname(OUT), "Data", fname)
      for line in open(path, encoding="utf-8"):
        if not line.strip() or line.startswith("#"):
            continue
        f = line.rstrip("\n").split("|")
        gid, voice, text = f[0], f[6], f[7]
        v, speed, pitch, rate = PRESETS[voice]
        yield ("Gags/gag_" + gid, v, speed, pitch, text,
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:30:0.12,volume=1.8")


def skeleton_lines():
    path = os.path.join(os.path.dirname(OUT), "Data", "skeleton_gags.txt")
    for line in open(path, encoding="utf-8"):
        if not line.strip() or line.startswith("#"):
            continue
        f = line.rstrip("\n").split("|")
        v, speed, pitch, rate = PRESETS[f[7]]
        yield ("Gags/skel_" + f[0], v, speed, pitch, f[9],
               f"asetrate=16000*{rate},aresample=22050,aecho=0.8:0.5:35:0.15,volume={1.8 * SKELETON_BOOST:.3f},alimiter=limit=0.95:level=disabled")


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(os.path.join(OUT, "Gags"), exist_ok=True)
    os.makedirs(os.path.join(OUT, "Rainbow"), exist_ok=True)
    only_rainbow = "--rainbow" in sys.argv
    os.makedirs(os.path.join(OUT, "Town"), exist_ok=True)
    os.makedirs(os.path.join(OUT, "Extra"), exist_ok=True)
    os.makedirs(os.path.join(OUT, "Park"), exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        if "--combo" in sys.argv:
            make_combo(tmp)
            return
        if "--moregags" in sys.argv:   # only the later areas' gags (ids starting I and J)
            render_only(tmp, [g for g in gag_lines() if g[0].startswith(("Gags/gag_I", "Gags/gag_J"))], "Gags")
            return
        if "--more" in sys.argv:   # only the expansion's new wings
            render_only(tmp, list(more_lines()), "More")
            write_captions()
            return
        if "--captions" in sys.argv:
            write_captions()
            return
        if "--areas2" in sys.argv:   # only the second batch of Station stops, and the carol
            for folder in sorted({n.split("/")[0] for n, *_ in AREAS2}): os.makedirs(os.path.join(OUT, folder), exist_ok=True)
            render_only(tmp, list(area2_lines()), "Xmas")
            only = [c for c in COMBO if c[0].startswith("xmas_")]
            saved = COMBO[:]; COMBO[:] = only
            try: make_combo(tmp)
            finally: COMBO[:] = saved
            write_captions()
            return
        if "--apart" in sys.argv:   # only the Marrow Arms apartments
            render_only(tmp, list(apart_lines()), "Apart")
            write_captions()
            return
        if "--areas" in sys.argv:   # only the later Station areas, and their two combo acts
            for folder in sorted({n.split("/")[0] for n, *_ in AREAS}): os.makedirs(os.path.join(OUT, folder), exist_ok=True)
            render_only(tmp, list(area_lines()), "Inn")
            only = [c for c in COMBO if c[0].startswith(("inn_", "bbq_"))]
            saved = COMBO[:]; COMBO[:] = only
            try: make_combo(tmp)
            finally: COMBO[:] = saved
            return
        if "--mystery" in sys.argv:   # only the Mystery Hut
            render_only(tmp, list(mystery_lines()), "Mystery")
            return
        if "--north" in sys.argv:   # only the Cold North
            render_only(tmp, list(north_lines()), "North")
            return
        if "--lab" in sys.argv:   # only the Test Annex and Blocklands: the announcer, the villagers, their gags (ids starting X)
            render_only(tmp, list(lab_lines()) + [g for g in gag_lines() if g[0].startswith("Gags/gag_X")], "Lab")
            return
        if "--pixel" in sys.argv:   # only Pixel Hollow: its people, its gags (ids starting U), and the duo's combo lines
            render_only(tmp, list(pixel_lines()) + [g for g in gag_lines() if g[0].startswith("Gags/gag_U")], "Pixel")
            only = [c for c in COMBO if c[0].startswith("quest_")]
            saved = COMBO[:]; COMBO[:] = only
            try: make_combo(tmp)
            finally: COMBO[:] = saved
            return
        if "--pizza" in sys.argv:   # only the pizzeria: its people, the voicemail, and its gags (ids starting Z)
            render_only(tmp, list(pizza_lines()) + [g for g in gag_lines() if g[0].startswith("Gags/gag_Z")], "Pizza")
            return
        if "--precinct" in sys.argv:   # only Precinct 206: the cops and its gags (ids starting Q)
            render_only(tmp, list(cop_lines()) + [g for g in gag_lines() if g[0].startswith("Gags/gag_Q")], "Precinct")
            return
        if "--park" in sys.argv:   # only the amusement park: its skeletons and its gags (ids starting P)
            os.makedirs(os.path.join(OUT, "Park"), exist_ok=True)
            todo = list(park_lines()) + [g for g in gag_lines() if g[0].startswith("Gags/gag_P")]
            for name, voice, speed, pitch, text, filt in todo:
                wav = os.path.join(tmp, name.replace("/", "_") + ".wav")
                subprocess.run(["espeak-ng", "-v", voice, "-s", str(speed), "-p", str(pitch), "-w", wav, text], check=True)
                out = os.path.join(OUT, name + ".ogg")
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-af", filt + ",apad=pad_dur=0.25",
                                "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", out], check=True)
                print(name, os.path.getsize(out) // 1024, "KB")
            return
        if "--extra" in sys.argv:   # only the 40 new skeletons
            os.makedirs(os.path.join(OUT, "Extra"), exist_ok=True)
            for name, voice, speed, pitch, text, filt in extra_lines():
                wav = os.path.join(tmp, name.replace("/", "_") + ".wav")
                subprocess.run(["espeak-ng", "-v", voice, "-s", str(speed), "-p", str(pitch), "-w", wav, text], check=True)
                out = os.path.join(OUT, name + ".ogg")
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-af", filt + ",apad=pad_dur=0.25",
                                "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", out], check=True)
                print(name, os.path.getsize(out) // 1024, "KB")
            return
        if "--town" in sys.argv:   # only the expansion: its voices, its gags (ids starting B), and the combo lines
            todo = list(town_lines()) + [g for g in gag_lines() if g[0].startswith("Gags/gag_B")]
            for name, voice, speed, pitch, text, filt in todo:
                wav = os.path.join(tmp, name.replace("/", "_") + ".wav")
                subprocess.run(["espeak-ng", "-v", voice, "-s", str(speed), "-p", str(pitch), "-w", wav, text], check=True)
                out = os.path.join(OUT, name + ".ogg")
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-af", filt + ",apad=pad_dur=0.25",
                                "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", out], check=True)
                print(name, os.path.getsize(out) // 1024, "KB")
            make_combo(tmp)
            return
        todo = list(rainbow_lines()) if only_rainbow else list(LINES) + list(gag_lines()) + list(skeleton_lines()) + list(rainbow_lines()) + list(town_lines()) + list(extra_lines()) + list(park_lines()) + list(cop_lines()) + list(pizza_lines()) + list(pixel_lines()) + list(lab_lines()) + list(north_lines()) + list(mystery_lines()) + list(area_lines()) + list(apart_lines()) + list(area2_lines())
        for name, voice, speed, pitch, text, filt in todo:
            wav = os.path.join(tmp, name.replace("/", "_") + ".wav")
            subprocess.run(["espeak-ng", "-v", voice, "-s", str(speed), "-p", str(pitch), "-w", wav, text], check=True)
            out = os.path.join(OUT, name + ".ogg")
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-af", filt + ",apad=pad_dur=0.25",
                            "-ac", "1", "-c:a", "libvorbis", "-q:a", "4", out], check=True)
            print(name, os.path.getsize(out) // 1024, "KB")
        if not only_rainbow:
            make_combo(tmp)


if __name__ == "__main__":
    main()
