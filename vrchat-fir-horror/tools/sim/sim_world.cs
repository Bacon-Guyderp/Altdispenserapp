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
