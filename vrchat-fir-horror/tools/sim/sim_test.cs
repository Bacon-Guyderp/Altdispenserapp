// Plays Fir Warning with simulated players, running the real game scripts, and checks what each player
// sees. Run: bash tools/sim/run_sim.sh
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

public class Scene {
  public FirGameManager game;
  public FirTeleporter start, join, exitBack;
  public FirTeleporter[] roomSelect = new FirTeleporter[7], back = new FirTeleporter[6];
  public FirButton reset, power, lights;
  public FirItem[] items = new FirItem[30];
  public FirCarryItem[] imitations = new FirCarryItem[5], hats = new FirCarryItem[3];
  public FirSocket[] hatSockets = new FirSocket[3]; public FirSocket furnace;
  public FirSequenceLock carol, fuseBox, tapeDeck, officeDoor;
  public Dictionary<FirSequenceLock, FirKey[]> keys = new Dictionary<FirSequenceLock, FirKey[]>();
  public FirGag gag; public FirSkeletonGag skel; public FirCloset closet;
  public FirJumpscare sharedScare, localScare;
  public FirStalker stalker; public FirToilet toilet;
  public Material[] mats;
}

public static class SimTest {
  static readonly float[] Z0 = { 0, 20, 56, 80, 104, 120, 150 }, Z1 = { 20, 56, 80, 104, 120, 150, 160 }, W = { 16, 24, 16, 20, 14, 22, 10 };
  static readonly Vector3 LobbySpawn = new Vector3(-60, 0.05f, -4);
  static int passed, failedChecks;

  static GameObject Go(string n, Vector3 p, Transform parent = null) { var g = new GameObject(n); if (parent != null) g.transform.SetParent(parent, false); g.transform.localPosition = p; return g; }
  static T B<T>(string n, Vector3 p) where T : Component, new() { return Go(n, p).AddComponent<T>(); }
  static TextMeshPro Txt(string n) { return B<TextMeshPro>(n, Vector3.zero); }
  static AudioClip Clip(string n) { return new AudioClip { name = n }; }
  static AudioSource Src(string n, AudioClip c = null) { var s = B<AudioSource>(n, Vector3.zero); s.clip = c; return s; }
  static FirAnimatronic Tree(string n, Vector3 p) {
    var g = Go(n, p); var a = g.AddComponent<FirAnimatronic>();
    a.head = Go("Head", new Vector3(0, 1, 0), g.transform).transform; a.mouth = Go("Mouth", new Vector3(0, 1, 0.2f), g.transform).transform;
    a.voice = Src(n + " voice"); a.noteClip = Clip("tree_note"); a.talkClip = Clip("tree_talk"); a.screamClip = Clip("scream");
    return a;
  }
  static FirTeleporter Tp(string n, Vector3 p, Transform dest, FirGameManager game, string evt) {
    var t = B<FirTeleporter>("Teleporter " + n, p); t.destination = dest; t.notify = game; t.notifyEvent = evt; t.sound = Src("whoosh"); return t;
  }
  static FirSequenceLock Lock(Scene s, string n, int ch, int len, bool needs, int values) {
    var l = B<FirSequenceLock>(n, Vector3.zero); l.game = s.game; l.chapter = ch; l.length = len; l.needsItems = needs; l.display = Txt(n + " display");
    var k = new FirKey[values]; for (int i = 0; i < values; i++) { k[i] = B<FirKey>(n + " key " + i, Vector3.zero); k[i].lockTarget = l; k[i].value = i; }
    s.keys[l] = k; return l;
  }
  static Transform[] Rig(string n, Transform parent) { var r = new Transform[10]; r[0] = Go(n, Vector3.zero, parent).transform; for (int i = 1; i < 10; i++) r[i] = Go(n + i, new Vector3(0, 0.1f * i, 0), r[0]).transform; return r; }

  /// The same scene on every client (same creation order = same network ids), wired like FirWorldBuilder.
  static object Build(Sim.Client c) {
    var s = new Scene();
    var game = s.game = B<FirGameManager>("GameManager", Vector3.zero);
    game.lobbySpawn = Go("Spawn", LobbySpawn).transform;
    game.checkpoints = new Transform[7];
    for (int i = 0; i < 7; i++) game.checkpoints[i] = Go("Checkpoint " + i, new Vector3(0, 0.05f, Z0[i] + 2.2f)).transform;
    game.doors = new GameObject[6]; for (int i = 0; i < 6; i++) game.doors[i] = Go("Door " + i, new Vector3(0, 1.3f, Z1[i]));
    game.hud = Go("HUD", Vector3.zero).transform; game.hudSubtitle = Txt("Subtitle"); game.hudObjective = Txt("Objective");
    game.lobbyStatus = Txt("Status"); game.lobbyStatus.text = "The game's scripts aren't running.";
    game.endingBoard = Txt("Ending"); game.optionsText = Txt("Options");
    game.memoTexts = new[] { Txt("Memo0"), Txt("Memo1"), Txt("Memo2"), Txt("Memo3") };
    game.countNames = new[] { "employees", "hats", "phones", "calendars" }; game.countValues = new[] { 6, 2, 8, 4 };
    game.tapeBoard = Txt("TapeBoard"); game.tapeCard = Txt("TapeCard"); game.hatClue = Txt("HatClue");
    game.glowDigits = new[] { Txt("G0"), Txt("G1"), Txt("G2") }; game.glowOnly = new[] { Go("GlowPaint", Vector3.zero) };
    game.furnaceFire = Go("FurnaceFire", Vector3.zero);
    game.sfx = Src("sfx"); game.music = Src("music");
    game.chapterMusic = new AudioClip[8]; for (int i = 0; i < 8; i++) game.chapterMusic[i] = Clip("music" + i);
    game.screams = new[] { Clip("scream1"), Clip("scream2"), Clip("scream3") };
    game.clipPickup = Clip("pickup"); game.clipUnlock = Clip("unlock"); game.clipWrong = Clip("wrong"); game.clipTalk = Clip("talk"); game.clipLaugh = Clip("laugh");
    game.clipStatic = Clip("static"); game.clipHit = Clip("hit"); game.clipWhoosh = Clip("whoosh"); game.clipHeartbeat = Clip("heartbeat"); game.clipWhisper = Clip("whisper"); game.clipSwitch = Clip("switch");
    var rig = Go("ScareRig", Vector3.zero); game.scareRig = rig.transform; game.scareAnim = Tree("ScareTree", Vector3.zero); game.scareAnim.transform.SetParent(rig.transform, false);
    s.mats = new[] { new Material { name = "Wood" }, new Material { name = "Brick" }, new Material { name = "Bulb" } };
    game.worldMaterials = s.mats;

    // teleporters
    s.start = Tp("START", new Vector3(-62.2f, 1.1f, 5.85f), game.checkpoints[0], game, "StartGame");
    s.join = Tp("Join", new Vector3(-60, 1.1f, 5.85f), game.checkpoints[0], game, "JoinGame");
    for (int i = 0; i < 7; i++) s.roomSelect[i] = Tp("Room " + i, new Vector3(-65, 2 - i / 2 * 0.42f, 5.85f), game.checkpoints[i], game, "EnteredRoom");
    for (int i = 0; i < 6; i++) s.back[i] = Tp("Back " + i, new Vector3(2.1f, 1.3f, Z0[i] + 0.16f), game.lobbySpawn, game, "LeftGame");
    s.exitBack = Tp("ExitBack", new Vector3(-3, 1, 159.8f), game.lobbySpawn, game, "LeftGame");
    s.reset = B<FirButton>("Reset", Vector3.zero); s.reset.target = game; s.reset.eventName = "ResetGame";
    s.power = B<FirButton>("Power", new Vector3(5, 1, 19)); s.power.target = game; s.power.eventName = "StationPower";
    s.lights = B<FirButton>("Lights", new Vector3(-10, 1, 21)); s.lights.target = game; s.lights.eventName = "StationLights";

    // items, in the builder's order: batteries, fuses, tapes, valves, imitations
    var itemGos = new List<GameObject>(); var chap = new List<int>();
    int[] chs = { 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 9, 9, 9 };
    foreach (int ch in chs) { int idx = itemGos.Count; var it = B<FirItem>("Item " + idx + " ch" + ch, new Vector3(0, 0.5f, idx)); it.game = game; it.index = idx; s.items[idx] = it; itemGos.Add(it.gameObject); chap.Add(ch); }
    var syncs = new List<VRCObjectSync>(); var carry = new List<FirCarryItem>();
    Func<string, Vector3, int, FirCarryItem> Carry = (n, p, kind) => {
      var g = Go(n, p); g.AddComponent<VRCPickup>(); var os = g.AddComponent<VRCObjectSync>(); os.home = p; syncs.Add(os);
      var ci = g.AddComponent<FirCarryItem>(); ci.kind = kind; ci.game = game; ci.pickup = g.GetComponent<VRCPickup>(); carry.Add(ci); return ci;
    };
    for (int i = 0; i < 5; i++) { var ci = Carry("Imitation " + i, new Vector3(-4 + 2 * i, 0, 130), 1); ci.index = itemGos.Count; s.imitations[i] = ci; itemGos.Add(ci.gameObject); chap.Add(6); }
    game.items = itemGos.ToArray(); game.itemChapter = chap.ToArray();
    for (int k = 0; k < 3; k++) { s.hats[k] = Carry("Hat " + k, new Vector3(-3 + 3 * k, 0.5f, 112), 0); s.hats[k].sizeId = k; }
    game.hatPickups = new[] { s.hats[0].gameObject, s.hats[1].gameObject, s.hats[2].gameObject };
    game.hatsOnTrees = new GameObject[9]; for (int i = 0; i < 9; i++) game.hatsOnTrees[i] = Go("HatOn " + i, Vector3.zero);
    for (int i = 0; i < 3; i++) { var so = B<FirSocket>("HatSocket " + i, new Vector3(-4 + 4 * i, 1.8f, 116)); so.game = game; so.acceptKind = 0; so.socketId = i; so.radius = 0.4f + 0.1f * i; s.hatSockets[i] = so; }
    s.furnace = B<FirSocket>("Furnace", new Vector3(-7.5f, 1.1f, 145.4f)); s.furnace.game = game; s.furnace.acceptKind = 1; s.furnace.radius = 1.3f;
    foreach (var so in new[] { s.hatSockets[0], s.hatSockets[1], s.hatSockets[2], s.furnace }) so.candidates = carry.FindAll(x => x.kind == so.acceptKind).ToArray();

    // trees, locks
    game.carolTrees = new FirAnimatronic[4]; for (int i = 0; i < 4; i++) game.carolTrees[i] = Tree("CarolTree " + i, new Vector3(-5.6f, 0.8f, 6 + 2 * i));
    game.animatronics = game.carolTrees;
    s.carol = Lock(s, "Carol", 1, 5, true, 4); s.fuseBox = Lock(s, "FuseBox", 2, 3, true, 10);
    s.tapeDeck = Lock(s, "TapeDeck", 3, 4, true, 4); s.officeDoor = Lock(s, "OfficeDoor", 4, 4, false, 10);

    // gags (shared through the manager)
    var gag = s.gag = B<FirGag>("Gag", new Vector3(10, 0, 5)); gag.game = game; gag.target = Go("GagProp", new Vector3(10, 0, 5)).transform; gag.moveBy = new Vector3(0, 1, 0); gag.line = "Wheee!"; gag.voice = Src("gagvoice", Clip("gag_line"));
    var sk = s.skel = B<FirSkeletonGag>("Skeleton", new Vector3(-60, 0, -5)); sk.game = game; sk.act = 0; var rg = Rig("SkelRig", sk.transform); sk.pivots = rg; sk.meshes = Rig("SkelMesh", sk.transform); sk.line = "A skeleton."; sk.voice = Src("skelvoice", Clip("skel_line"));
    var cl = s.closet = B<FirCloset>("Closet", new Vector3(6, 0, 110)); cl.game = game; cl.hinge = Go("Hinge", Vector3.zero, cl.transform).transform; cl.white = Rig("White", cl.transform); cl.red = Rig("Red", cl.transform);
    cl.whiteVoice = Src("wv", Clip("a_skeleton")); cl.redVoice = Src("rv", Clip("red_skeleton")); cl.creak = Src("creak");
    game.gags = new[] { gag }; game.skeletonGags = new[] { sk }; game.closet = cl;

    // scares: one shared (everyone in the game gets it), one local
    s.sharedScare = B<FirJumpscare>("SharedScare", new Vector3(0, 1.5f, 30)); s.sharedScare.game = game; s.sharedScare.kind = 0; s.sharedScare.global = true; s.sharedScare.zoneSize = new Vector3(6, 3, 1); s.sharedScare.minPhase = 2; s.sharedScare.maxPhase = 2; s.sharedScare.duration = 1f;
    s.localScare = B<FirJumpscare>("LocalScare", new Vector3(0, 1.5f, 10)); s.localScare.game = game; s.localScare.kind = 0; s.localScare.zoneSize = new Vector3(6, 3, 1); s.localScare.minPhase = 1; s.localScare.maxPhase = 1;
    game.jumpscares = new[] { s.sharedScare, s.localScare }; s.sharedScare.scareIndex = 0; s.localScare.scareIndex = 1;

    // the hunting tree, with waypoints round one long shelf in the stockroom
    var st = s.stalker = B<FirStalker>("Stalker", new Vector3(0, -40, -30)); st.game = game; st.visual = Go("StalkerVisual", Vector3.zero, st.transform); st.visual.SetActive(false);
    st.eyes = Go("Eyes", new Vector3(0, 1.8f, 0.2f), st.transform).transform; st.anim = Tree("StalkerTree", Vector3.zero); st.anim.transform.SetParent(st.visual.transform, false);
    st.parking = Go("Parking", new Vector3(0, -40, -30)).transform;
    st.spawnByPhase = new Transform[8]; st.spawnByPhase[2] = Go("Sp2", new Vector3(8.5f, 0, 52)).transform; st.spawnByPhase[4] = Go("Sp4", new Vector3(-8.5f, 0, 102.5f)).transform; st.spawnByPhase[6] = Go("Sp6", new Vector3(9, 0, 148)).transform;
    var wps = new List<Transform>(); var wph = new List<int>();
    foreach (int p in new[] { 2, 4, 6 }) { int r = p - 1; for (float z = Z0[r] + 2; z <= Z1[r] - 1.5f; z += 3) for (float x = -W[r] / 2 + 1; x <= W[r] / 2 - 1; x += 3) {
      var at = new Vector3(x, 0.05f, z); if (p == 2 && Math.Abs(x) < 6.6f && z > 34 && z < 46) continue; wps.Add(Go("WP", at).transform); wph.Add(p); } }
    st.waypoints = wps.ToArray(); st.waypointPhase = wph.ToArray();
    st.roomMinX = new float[8]; st.roomMaxX = new float[8]; st.roomMinZ = new float[8]; st.roomMaxZ = new float[8];
    for (int p = 1; p <= 7; p++) { st.roomMinX[p] = -W[p - 1] / 2 + 0.6f; st.roomMaxX[p] = W[p - 1] / 2 - 0.6f; st.roomMinZ[p] = Z0[p - 1] + 0.6f; st.roomMaxZ[p] = Z1[p - 1] - 0.6f; }
    st.moveAudio = Src("stalkermove"); st.moveClips = new[] { Clip("rustle") };
    game.stalker = st;

    var tg = Go("Toilet", new Vector3(-55, 0, -5)); var tp = tg.AddComponent<VRCPickup>(); var tsync = tg.AddComponent<VRCObjectSync>(); tsync.home = tg.transform.position; syncs.Add(tsync);
    var rb = tg.AddComponent<Rigidbody>(); s.toilet = tg.AddComponent<FirToilet>(); s.toilet.pickup = tp; s.toilet.body = rb; s.toilet.stalker = st; s.toilet.game = game; s.toilet.audioSrc = Src("toiletaudio"); s.toilet.clunkClip = Clip("clunk");
    game.carrySyncs = syncs.ToArray();

    var amb = B<FirAmbience>("Ambience", Vector3.zero); amb.game = game; amb.emitter = Src("amb"); amb.clips = new[] { Clip("whisper") };
    return s;
  }

  // ------------------------------------------------------------------ helpers
  static Scene S(Sim.Client c) { return (Scene)c.scene; }
  static T Get<T>(object o, string f) { return (T)o.GetType().GetField(f, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(o); }
  static void Check(bool ok, string what) {
    if (ok) { passed++; Console.WriteLine("  PASS  " + what); }
    else { failedChecks++; Console.WriteLine("  FAIL  " + what); }
  }
  static bool Near(Vector3 a, Vector3 b, float d = 0.3f) { return Vector3.Distance(a, b) < d; }
  static Vector3 Cp(int i) { return new Vector3(0, 0.05f, Z0[i] + 2.2f); }
  /// Waits long enough for the network: the given time plus a round trip at the simulated latency.
  static void Settle(float t) { Sim.Run(t + 2.5f * Sim.Latency); }
  static string Why(Sim.Client c) { var g = S(c).game; return " [" + c.player.displayName + ": scareState " + Get<int>(g, "scareState") + ", behind " + Get<bool>(g, "behindActive") + ", inGame " + g.IsInGame() + ", pos " + c.player.pos + "]"; }
  static void Section(string t) { Console.WriteLine("\n== " + t); }
  static void EnterCode(Sim.Client c, FirSequenceLock l, int ch, int len) { var g = S(c).game; for (int i = 0; i < len; i++) { int v = 0; Sim.With(c, () => v = g.CodeAt(ch, i)); Sim.Press(c, S(c).keys[l][v]); Sim.Run(0.1f); } }
  static void AllLookAway(Sim.Client[] cs, Vector3 at) { foreach (var c in cs) { var p = c.player; p.LookAt(p.pos + (p.pos - at)); } }

  public static int Main(string[] args) {
    if (args.Length > 0) UnityEngine.Random.rng = new System.Random(int.Parse(args[0]));
    if (args.Length > 1) Sim.Latency = float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
    Sim.SceneBuilder = Build;
    var A = Sim.Join("Alice", LobbySpawn);
    var B = Sim.Join("Bob", LobbySpawn + new Vector3(1, 0, 0));
    Sim.Run(1f);

    Section("1. The scripts start on every client");
    foreach (var c in Sim.Clients) Check(!S(c).game.lobbyStatus.text.Contains("aren't running") && S(c).game.lobbyStatus.text.Contains("Nobody is playing"), c.player.displayName + "'s lobby board is live: \"" + S(c).game.lobbyStatus.text.Split('\n')[0] + "\"");
    var seedA = S(A).game.seed; Settle(0.5f);
    Check(seedA != 0 && S(B).game.seed == seedA, "both clients share the same puzzle seed (" + seedA + ")");

    Section("2. Every Room Select and Back-to-lobby teleporter (Bob, not the instance master)");
    for (int i = 0; i < 7; i++) {
      Sim.Press(B, S(B).roomSelect[i]); Settle(0.2f);
      Check(Near(B.player.pos, Cp(i)), "Room Select " + (i + 1) + " -> " + B.player.pos);
      var back = i < 6 ? S(B).back[i] : S(B).exitBack;
      Sim.Run(1.1f); Sim.Press(B, back); Settle(0.2f);
      Check(Near(B.player.pos, LobbySpawn), "  Back to the lobby from room " + (i + 1) + " -> " + B.player.pos);
    }
    Check(Near(A.player.pos, LobbySpawn + new Vector3(1, 0, 0), 2f), "Alice was never moved by Bob's teleports");

    Section("3. Bob (not the owner of the game) presses START");
    Sim.Press(B, S(B).start);
    Check(Near(B.player.pos, Cp(0)), "Bob is in the Showroom the same frame he pressed it");
    Settle(0.6f);
    foreach (var c in Sim.Clients) Check(S(c).game.phase == 1, c.player.displayName + " sees chapter 1");
    Check(Near(A.player.pos, Cp(0)), "Alice, waiting in the lobby, was taken in too -> " + A.player.pos);
    Check(S(A).game.IsInGame() && S(B).game.IsInGame(), "both are in the game");
    Check(S(A).game.lobbyStatus.text.Contains("chapter 1"), "lobby board: \"" + S(A).game.lobbyStatus.text.Split('\n')[0] + "\"");

    Section("4. Carla joins late and presses Join game");
    var C = Sim.Join("Carla", LobbySpawn); Settle(1f);
    Check(S(C).game.phase == 1 && S(C).game.seed == S(A).game.seed, "Carla received the game state (chapter " + S(C).game.phase + ")");
    Check(Near(C.player.pos, LobbySpawn), "Carla wasn't yanked out of the lobby on joining");
    Sim.Press(C, S(C).join); Settle(0.3f);
    Check(Near(C.player.pos, Cp(0)), "Join game took Carla to the current room -> " + C.player.pos);
    var all = new[] { A, B, C };

    Section("5. Chapter 1: batteries, power, the carol");
    Sim.Press(A, S(A).items[0]); Sim.Press(B, S(B).items[1]);   // same frame: a sync race
    Settle(1f);
    foreach (var c in all) Check(S(c).game.ChapterItemsHave(1) == 2, c.player.displayName + " sees both batteries taken in the same frame (race resolved): " + S(c).game.ChapterItemsHave(1) + "/3");
    Sim.Press(C, S(C).items[2]); Settle(0.5f);
    foreach (var c in all) Check(!S(c).items[2].gameObject.activeSelf, c.player.displayName + ": Carla's battery vanished");
    Sim.Events.Clear(); Sim.Press(B, S(B).power); Sim.Run(7f);
    foreach (var c in all) Check(Sim.Events.FindAll(e => e.StartsWith(c.player.displayName + " sound: CarolTree")).Count >= 5, c.player.displayName + " heard the carol (" + Sim.Events.FindAll(e => e.StartsWith(c.player.displayName + " sound: CarolTree")).Count + " notes)");
    EnterCode(A, S(A).carol, 1, 5); Settle(0.6f);
    foreach (var c in all) Check(S(c).game.phase == 2 && !S(c).game.doors[0].activeSelf, c.player.displayName + ": carol solved, door 1 open, chapter " + S(c).game.phase);

    Section("6. Gags, skeletons and the closet are seen by everyone");
    Sim.Press(A, S(A).gag); Settle(0.4f);
    foreach (var c in all) Check(Get<bool>(S(c).gag, "running"), c.player.displayName + " sees the gag play");
    Sim.Run(4f); Sim.Press(C, S(C).skel); Settle(0.4f);
    foreach (var c in all) Check(Get<bool>(S(c).skel, "running"), c.player.displayName + " sees the skeleton routine");
    Sim.Press(B, S(B).closet); Settle(0.4f);
    foreach (var c in all) Check(Get<bool>(S(c).closet, "running"), c.player.displayName + " sees the RED SKELETON closet scene");
    Sim.Run(10f);
    foreach (var c in all) Check(!Get<bool>(S(c).closet, "running") && !S(c).closet.white[0].gameObject.activeSelf, c.player.displayName + ": closet finished and reset");

    Section("7. Chapter 2: the hunting tree, a toilet, a shared scare, lights out");
    foreach (var c in all) Check(S(c).stalker.IsActive() && S(c).stalker.visual.activeSelf, c.player.displayName + " sees the hunting tree");
    foreach (var c in all) { c.player.pos = new Vector3(-8, 0, 24); }
    Sim.Run(7f);   // its grace period
    var sp0 = S(A).stalker.transform.position;
    foreach (var c in all) c.player.LookAt(S(A).stalker.transform.position + Vector3.up);
    Sim.Run(3f);
    Check(Vector3.Distance(S(A).stalker.transform.position, sp0) < 0.05f, "while watched it doesn't move");
    AllLookAway(all, S(A).stalker.transform.position);
    Sim.Run(3f);
    var moved = Vector3.Distance(S(A).stalker.transform.position, sp0);
    Check(moved > 2f, "unwatched it closes in (" + moved.ToString("F1") + " m in 3 s)");
    Check(Vector3.Distance(S(B).stalker.transform.position, S(A).stalker.transform.position) < 1f + 3f * Sim.Latency, "Bob's and Carla's copies follow the owner's (" + Vector3.Distance(S(B).stalker.transform.position, S(A).stalker.transform.position).ToString("F2") + " m apart)");
    // toilet: Bob grabs it and throws it at the tree
    Sim.SetOwner(B.player, S(B).toilet.gameObject);
    S(B).toilet.transform.position = S(B).stalker.transform.position + new Vector3(0, 1.1f, 0); S(B).toilet.body.velocity = new Vector3(0, 0, 6);
    Settle(0.5f); S(B).toilet.body.velocity = Vector3.zero;
    foreach (var c in all) Check(Get<float>(S(c).stalker, "stunUntil") > Time.time, c.player.displayName + ": the toilet stunned it");
    var stunPos = S(A).stalker.transform.position; Sim.Run(2f);
    Check(Vector3.Distance(stunPos, S(A).stalker.transform.position) < 0.05f, "stunned, it stays put");
    // it catches Carla
    Sim.Run(3f);
    C.player.pos = S(A).stalker.transform.position + new Vector3(0, 0, 0.5f);
    Sim.Run(2.5f);
    Check(Near(C.player.pos, Cp(1)), "it caught Carla: face scare, then back to the Stockroom's start -> " + C.player.pos);
    // shared scare: Alice walks into it, Bob gets it too
    foreach (var c in all) c.player.pos = new Vector3(0, 0, 25);
    A.player.pos = new Vector3(0, 0, 30);
    Sim.Run(0.1f);
    Check(Get<int>(S(A).game, "scareState") != 0, "Alice walked into the shared scare");
    Sim.Run(0.2f + 1.2f * Sim.Latency);
    Check(Get<int>(S(B).game, "scareState") != 0 && Get<int>(S(C).game, "scareState") != 0, "Bob and Carla got it at the same moment" + Why(B) + Why(C));
    Sim.Run(3f);
    for (int i = 3; i < 7; i++) { Sim.Press(B, S(B).items[i]); Settle(0.3f); }
    Sim.Press(C, S(C).lights); Settle(0.5f);
    foreach (var c in all) Check(S(c).mats[0].GetFloat("_Dark") > 0.5f && S(c).game.glowOnly[0].activeSelf, c.player.displayName + ": lights out (material _Dark = " + S(c).mats[0].GetFloat("_Dark") + "), glow paint showing" + Why(c));
    foreach (var c in all) { float end = Get<float>(S(c).game, "lightsOutUntil"); Check(end > Time.time && end < Time.time + 8.2f, c.player.displayName + ": the blackout lasts " + (end - Time.time + 0.5f).ToString("F1") + " s"); }
    Sim.Run(9f);
    var seenLight = new HashSet<Sim.Client>();
    for (int f = 0; f < 280; f++) { Sim.Step(); foreach (var c in all) if (S(c).mats[0].GetFloat("_Dark") < 0.5f) seenLight.Add(c); }
    foreach (var c in all) Check(seenLight.Contains(c), c.player.displayName + ": lights came back on afterwards");
    EnterCode(C, S(C).fuseBox, 2, 3); Settle(0.6f);
    foreach (var c in all) Check(S(c).game.phase == 3, c.player.displayName + ": fuse box opened, chapter " + S(c).game.phase);

    Section("8. Chapters 3-5");
    for (int i = 7; i < 11; i++) { Sim.Press(A, S(A).items[i]); Settle(0.3f); }
    EnterCode(A, S(A).tapeDeck, 3, 4); Settle(0.6f);
    foreach (var c in all) Check(S(c).game.phase == 4, c.player.displayName + ": tapes played in order, chapter " + S(c).game.phase);
    Sim.Press(B, S(B).officeDoor.gameObject.GetComponent<FirSequenceLock>() == null ? null : S(B).keys[S(B).officeDoor][(S(B).game.CodeAt(4, 0) + 1) % 10]);
    for (int i = 1; i < 4; i++) { Sim.Press(B, S(B).keys[S(B).officeDoor][0]); }
    Settle(0.5f); Check(S(A).game.phase == 4, "a wrong office code is refused");
    EnterCode(B, S(B).officeDoor, 4, 4); Settle(0.6f);
    foreach (var c in all) Check(S(c).game.phase == 5, c.player.displayName + ": office door opened, chapter " + S(c).game.phase);
    // hats: Carla carries the wrong hat to socket 0 first
    int right0 = 0; Sim.With(C, () => right0 = S(C).game.CodeAt(5, 0));
    var wrong = S(C).hats[(right0 + 1) % 3];
    Sim.SetOwner(C.player, wrong.gameObject); wrong.transform.position = S(C).hatSockets[0].transform.position; Settle(0.6f);
    Check(S(A).game.hatMask == 0, "the wrong hat is refused (\"THAT'S NOT MY HAT.\")");
    wrong.transform.position = new Vector3(0, 0.5f, 112); Settle(0.3f);
    for (int s = 0; s < 3; s++) {
      int col = 0; Sim.With(C, () => col = S(C).game.CodeAt(5, s));
      var who = all[s]; var hat = S(who).hats[col];
      Sim.SetOwner(who.player, hat.gameObject); hat.transform.position = S(who).hatSockets[s].transform.position; Settle(0.8f);
    }
    foreach (var c in all) Check(S(c).game.phase == 6, c.player.displayName + ": every tree has its hat, chapter " + S(c).game.phase);

    Section("9. Chapter 6: valves and the furnace");
    var early = S(A).imitations[0]; Sim.SetOwner(A.player, early.gameObject); early.transform.position = S(A).furnace.transform.position; Settle(0.6f);
    Check(S(A).game.ChapterItemsHave(6) == 0, "the cold furnace refuses an imitation before the valves are open");
    early.transform.position = new Vector3(-4, 0, 130); Settle(0.3f);
    for (int i = 11; i < 14; i++) { Sim.Press(all[i - 11], S(all[i - 11]).items[i]); Settle(0.4f); }
    foreach (var c in all) Check(S(c).game.ValvesOpen() && S(c).game.furnaceFire.activeSelf, c.player.displayName + ": furnace lit");
    for (int i = 0; i < 5; i++) { var who = all[i % 3]; var im = S(who).imitations[i]; Sim.SetOwner(who.player, im.gameObject); im.transform.position = S(who).furnace.transform.position; Settle(0.7f); }
    foreach (var c in all) Check(S(c).game.phase == 7 && S(c).game.endingBoard.text.Contains("YOU ESCAPED"), c.player.displayName + ": escaped! board says \"" + S(c).game.endingBoard.text.Split('\n')[0] + "\"");

    Section("10. Exit, reset, play again");
    Sim.Press(A, S(A).roomSelect[6]); Sim.Run(1.2f); Sim.Press(A, S(A).exitBack); Settle(0.2f);
    Check(Near(A.player.pos, LobbySpawn), "Alice used the exit room's Back to the lobby");
    Sim.Press(C, S(C).reset); Settle(0.2f);
    Check(S(A).game.phase == 7, "one press of Reset does nothing (needs two)");
    Sim.Press(C, S(C).reset); Settle(0.6f);
    foreach (var c in all) Check(S(c).game.phase == 0, c.player.displayName + " sees the game reset");
    Check(Near(B.player.pos, LobbySpawn) && Near(C.player.pos, LobbySpawn), "Bob and Carla were sent back to the lobby");
    Sim.Press(A, S(A).start); Settle(0.6f);
    foreach (var c in all) Check(S(c).game.phase == 1 && Near(c.player.pos, Cp(0)), c.player.displayName + " is in the new game's Showroom");
    foreach (var c in all) Check(S(c).items[0].gameObject.activeSelf && S(c).imitations[0].gameObject.activeSelf, c.player.displayName + ": items are back for the new game");
    Check(S(A).game.seed != seedA, "new game, new puzzle answers");

    Section("11. The master (Alice) leaves mid-game");
    Sim.Leave(A); Settle(0.5f);
    var rest = new[] { B, C };
    Sim.Press(B, S(B).items[0]); Settle(0.5f);
    foreach (var c in rest) Check(S(c).game.ChapterItemsHave(1) == 1, c.player.displayName + " still sees progress after the master left");
    Sim.Press(C, S(C).roomSelect[3]); Settle(0.2f);
    Check(Near(C.player.pos, Cp(3)), "teleporters still work after the master left");

    Section("Summary");
    Console.WriteLine("  " + passed + " checks passed, " + failedChecks + " failed, " + Sim.Failures.Count + " runtime problems (crashes, ignored sync, bad events)");
    foreach (var f in Sim.Failures) Console.WriteLine("  problem: " + f.Split('\n')[0]);
    return failedChecks == 0 && Sim.Failures.Count == 0 ? 0 : 1;
  }
}
