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
]
SKV.update({"santa": ("mb-us2", 125, 20, 0.85), "elf": ("mb-us1", 190, 90, 1.35), "reindeer": ("mb-us3", 150, 55, 1.05), "snowman": ("mb-us2", 130, 40, 0.95)})
COMBO += [
    ("xmas_1", ["santa"], "On the first day of Christmas, my true love gave to me..."),
    ("xmas_2", ["elf"], "A skull in a fir tree!"),
    ("xmas_3", ["reindeer"], "On the second day of Christmas, my true love gave to me..."),
    ("xmas_4", ["snowman"], "Two femurs, and..."),
    ("xmas_5", ["santa", "elf", "reindeer", "snowman"], "A skull in a fir tree! Merry Christmas!"),
]


def area2_lines():
    for name, v, speed, pitch, rate, kind, text in AREAS2:
        filt = AREA_FILTERS[kind].replace("{v:.3f}", f"{1.8 * SKELETON_BOOST:.3f}")
        yield (name, v, speed, pitch, text, f"asetrate=16000*{rate},aresample=22050," + filt)


def write_captions():
    """Data/captions.txt: clip name|what is said, for every voiced line (the builder shows them as captions)."""
    rows = {}
    for gen in (list(LINES), gag_lines(), skeleton_lines(), rainbow_lines(), town_lines(), extra_lines(), park_lines(), cop_lines(),
                pizza_lines(), pixel_lines(), lab_lines(), north_lines(), mystery_lines(), area_lines(), apart_lines(), area2_lines()):
        for row in gen:
            rows[row[0].split("/")[-1]] = row[4]
    for name, _, text in COMBO:
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
