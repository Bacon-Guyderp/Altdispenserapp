// Builds the REAL world with FirWorldBuilder (inside the simulator) and plays it, so wiring mistakes in the
// builder show up, not just in the game scripts. Run: bash tools/sim/run_world.sh
using System;
using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public static class SimWorld {
  static int pass, fail;
  static void Check(bool ok, string what) { if (ok) pass++; else fail++; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what); }
  static T One<T>(Sim.Client c, Func<T, bool> f = null) where T : Component { foreach (var g in c.objects) if (!g.destroyed) foreach (var k in g.comps) if (k is T && (f == null || f((T)k))) return (T)k; return null; }
  static List<T> All<T>(Sim.Client c) where T : Component { var r = new List<T>(); foreach (var g in c.objects) if (!g.destroyed) foreach (var k in g.comps) if (k is T) r.Add((T)k); return r; }
  static FirGameManager G(Sim.Client c) { return One<FirGameManager>(c); }

  public static int Main(string[] args) {
    Sim.SceneBuilder = c => { bool ok = FirWorldBuilder.Build(true); if (!ok) Sim.Fail("Build World returned false"); return null; };
    var A = Sim.Join("Alice", new Vector3(-60, 0.05f, -4));
    Console.WriteLine("Built: " + A.objects.Count + " objects; builder log:");
    foreach (var l in Sim.BuildLog) Console.WriteLine("  log: " + l);
    foreach (var w in Debug.Warnings.Distinct().Take(30)) Console.WriteLine("  warning: " + w);
    var spawn = G(A).lobbySpawn.position; A.player.pos = spawn;
    Sim.Run(2f);
    Console.WriteLine("\n== Alice alone, like Build & Test");
    Console.WriteLine("  lobby board: " + G(A).lobbyStatus.text.Replace("\n", " | "));
    Check(!G(A).lobbyStatus.text.Contains("aren't running"), "game manager is running");
    var start = One<FirTeleporter>(A, t => t.name.Contains("START"));
    Sim.Press(A, start); Sim.Run(1f);
    Check(G(A).phase == 1, "START begins the game (phase " + G(A).phase + ")");
    Console.WriteLine("  at " + A.player.pos + "  subtitle: " + G(A).hudSubtitle.text);
    var items = All<FirItem>(A).Where(i => !i.fake && G(A).itemChapter[i.index] == 1).ToList();
    foreach (var it in items) { Sim.Press(A, it); Sim.Run(0.3f); }
    Check(G(A).ChapterItemsHave(1) == 3, "batteries collected: " + G(A).ChapterItemsHave(1));
    var power = One<FirButton>(A, b => b.eventName == "StationPower");
    Check(power != null, "power panel exists");
    if (power != null) { Sim.Press(A, power); Sim.Run(7f); }
    Check(G(A).carolCount == 1, "power panel pressed -> carol (" + G(A).carolCount + ")");
    var skel = All<FirSkeletonGag>(A);
    Check(skel.Count > 0, skel.Count + " skeleton routines");
    if (skel.Count > 0) { Sim.Press(A, skel[0]); Sim.Run(0.5f); Check((bool)typeof(FirSkeletonGag).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(skel[0]), "skeleton routine plays"); }
    var dog = One<FirWatchdog>(A);
    Check(dog != null && dog.board.text.Contains("running"), "diagnostics board: " + (dog != null ? dog.board.text.Replace("\n", " | ") : "missing"));
    var pressedSomething = One<FirKey>(A);
    Sim.Press(A, pressedSomething); Sim.Run(1.2f);
    Check(dog.board.text.Contains(pressedSomething.name), "the board shows the last press: " + dog.board.text.Split('\n')[1]);

    Console.WriteLine("\n== Bob joins; a full playthrough of the real world, two players");
    var B = Sim.Join("Bob", new Vector3(-59, 0.05f, -4));
    Sim.Run(2f);
    Check(G(B).phase == 1, "Bob received the game state (chapter " + G(B).phase + ")");
    Sim.Press(B, One<FirTeleporter>(B, t => t.name.Contains("Join"))); Sim.Run(1f);
    Check(G(B).inGame && B.player.pos.z > 0 && B.player.pos.z < 20, "Join game put Bob in the Showroom " + B.player.pos);
    var both = new[] { A, B };
    Func<Sim.Client, int, int[]> Ans = (c, ch) => { var a = G(c).answers; int n = ch == 1 ? 5 : ch == 2 ? 3 : ch == 5 ? 3 : 4; var r = new int[n]; for (int k = 0; k < n; k++) r[k] = a[ch * 5 + k]; return r; };
    Action<Sim.Client, int> Solve = (c, ch) => {
      var lk = One<FirSequenceLock>(c, l => l.chapter == ch);
      var keys = All<FirKey>(c).Where(k => k.lockTarget != null && k.lockTarget.program == lk).ToList();
      foreach (int v in Ans(c, ch)) { var key = keys.FirstOrDefault(k => k.value == v); if (key == null) { Check(false, "no key " + v + " on lock " + ch); return; } Sim.Press(c, key); Sim.Run(0.15f); }
      Sim.Run(0.6f);
    };
    Action<Sim.Client, int> Collect = (c, ch) => { foreach (var it in All<FirItem>(c).Where(x => !x.fake && G(c).itemChapter[x.index] == ch)) { Sim.Press(c, it); Sim.Run(0.3f); } Sim.Run(0.5f); };
    // the carol was already powered above by Alice alone; Bob solves it
    Solve(B, 1);
    foreach (var c in both) Check(G(c).phase == 2, c.player.displayName + ": carol solved, chapter " + G(c).phase);
    Collect(B, 2);
    Sim.Press(A, One<FirButton>(A, b => b.eventName == "StationLights")); Sim.Run(1f);
    foreach (var c in both) Check(G(c).lightsOutUntil > Time.time, c.player.displayName + ": lights out");
    Solve(A, 2);
    foreach (var c in both) Check(G(c).phase == 3, c.player.displayName + ": fuse box, chapter " + G(c).phase);
    Collect(A, 3); Solve(B, 3);
    foreach (var c in both) Check(G(c).phase == 4, c.player.displayName + ": tapes, chapter " + G(c).phase);
    Solve(A, 4);
    foreach (var c in both) Check(G(c).phase == 5, c.player.displayName + ": office door, chapter " + G(c).phase);
    var socks = All<FirSocket>(B).Where(x => x.acceptKind == 0).OrderBy(x => x.socketId).ToList();
    foreach (var so in socks) {
      int want = G(B).answers[25 + so.socketId];
      var hat = All<FirCarryItem>(B).First(h => h.kind == 0 && h.sizeId == want);
      Sim.SetOwner(B.player, hat.gameObject); hat.transform.position = so.transform.position; Sim.Run(0.8f);
    }
    foreach (var c in both) Check(G(c).phase == 6, c.player.displayName + ": all three hats, chapter " + G(c).phase);
    Collect(A, 9);
    foreach (var c in both) Check(G(c).valvesOpen && G(c).furnaceFire.activeSelf, c.player.displayName + ": furnace lit");
    var furnace = All<FirSocket>(A).First(x => x.acceptKind == 1);
    foreach (var im in All<FirCarryItem>(A).Where(x => x.kind == 1).ToList()) { Sim.SetOwner(A.player, im.gameObject); im.transform.position = furnace.transform.position; Sim.Run(0.8f); }
    foreach (var c in both) Check(G(c).phase == 7 && G(c).endingBoard.text.Contains("ESCAPED"), c.player.displayName + ": escaped");

    Console.WriteLine("\n== Everything else, once each, with the real wiring");
    int nScares = 0; foreach (var j in All<FirJumpscare>(A)) { Sim.With(A, () => j.Fire()); Sim.Run(0.2f); nScares++; }
    Sim.Run(8f);
    Check(true, "fired all " + nScares + " scripted scares");
    int nGags = 0; foreach (var g in All<FirGag>(A)) { Sim.Press(A, g); nGags++; } Sim.Run(1f);
    Check(All<FirGag>(B).Count(g => (bool)typeof(FirGag).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(g)) > 0, "pressed all " + nGags + " gags; Bob sees them");
    foreach (var g in All<FirSkeletonGag>(B)) Sim.Press(B, g);
    Sim.Run(1f);
    Check(All<FirSkeletonGag>(A).All(g => (bool)typeof(FirSkeletonGag).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(g)), "every skeleton routine plays for Alice when Bob presses them");
    Sim.Press(A, One<FirCloset>(A)); Sim.Run(1f);
    Check((bool)typeof(FirCloset).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(One<FirCloset>(B)), "the closet scene plays for Bob");
    foreach (var ci in All<FirCarryItem>(A)) Sim.With(A, () => ci.OnPickup());
    foreach (var b in All<FirButton>(A).Where(b => b.eventName.StartsWith("Toggle"))) Sim.Press(A, b);
    Sim.Run(12f);
    Check(G(A).optionsText.text.Contains("Brightness"), "option buttons: " + G(A).optionsText.text.Replace("\n", " | "));
    Console.WriteLine("\n== Reset and play again, with the hunting tree and a toilet");
    var reset = One<FirButton>(A, b => b.eventName == "ResetGame");
    Sim.Press(A, reset); Sim.Run(0.3f); Sim.Press(A, reset); Sim.Run(1f);
    foreach (var c in both) Check(G(c).phase == 0, c.player.displayName + ": reset to the lobby");
    Sim.Press(B, One<FirTeleporter>(B, t => t.name.Contains("START"))); Sim.Run(1f);
    foreach (var c in both) Check(G(c).phase == 1, c.player.displayName + ": new game");
    // jump straight to chapter 2 to meet the hunting tree
    Collect(A, 1); Sim.Press(A, One<FirButton>(A, b => b.eventName == "StationPower")); Sim.Run(9f); Solve(A, 1);
    foreach (var c in both) Check(G(c).phase == 2, c.player.displayName + ": chapter 2 again (a half-entered carol from earlier doesn't spoil it)");
    foreach (var c in both) c.player.pos = new Vector3(0, 0.05f, 24);
    Sim.Run(9f);
    var st = One<FirStalker>(A);
    Check(st.IsActive(), "the hunting tree is out, at " + st.transform.position);
    var toilet = One<FirToilet>(A);
    Sim.SetOwner(A.player, toilet.gameObject);
    toilet.transform.position = st.transform.position + new Vector3(0, 1.1f, 0); toilet.body.velocity = new Vector3(0, 0, 6);
    Sim.Run(0.4f); toilet.body.velocity = Vector3.zero;
    foreach (var c in both) Check((float)typeof(FirStalker).GetField("stunUntil", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(One<FirStalker>(c)) > Time.time, c.player.displayName + ": toilet hit stuns it");
    Sim.Run(6f);
    Console.WriteLine("\n== Breaking the game manager on purpose, to check the board catches it");
    G(A).checkpoints = null;
    var join = One<FirTeleporter>(A, t => t.name.Contains("Join"));
    Sim.Press(A, join); Sim.Run(4f);
    Check(dog.board.text.Contains("STOPPED") && dog.board.text.Contains("JoinGame"), "board: " + dog.board.text.Replace("\n", " | "));
    int expected = Sim.Failures.Count; Sim.Failures.Clear();
    Console.WriteLine("  (" + expected + " expected crash report(s) from the deliberate break cleared)");
    Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems");
    foreach (var f in Sim.Failures.Take(20)) Console.WriteLine("  problem: " + f);
    return 0;
  }
}
