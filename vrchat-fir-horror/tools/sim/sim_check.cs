// Builds the systems-check world with FirCheckBuilder (inside the simulator), presses every test with two
// players and reads every label back. Run: bash tools/sim/run_check.sh
using System;
using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UnityEngine;

public static class SimCheck {
  static int pass, fail;
  static void Check(bool ok, string what) { if (ok) pass++; else fail++; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what); }
  static List<T> All<T>(Sim.Client c) where T : Component { var r = new List<T>(); foreach (var g in c.objects) if (!g.destroyed) foreach (var k in g.comps) if (k is T) r.Add((T)k); return r; }
  static string Plain(string s) { return System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "").Replace("\n", " | "); }

  public static int Main() {
    Sim.SceneBuilder = c => { if (!FirCheckBuilder.Build(true)) Sim.Fail("Build Systems Check returned false"); return null; };
    var A = Sim.Join("Alice", new Vector3(0, 0.05f, -4));
    var B = Sim.Join("Bob", new Vector3(1, 0.05f, -4));
    Console.WriteLine("Built " + A.objects.Count + " objects. Builder log:");
    foreach (var l in Sim.BuildLog) Console.WriteLine("  " + l);
    Sim.Run(2f);
    var hubBoard = All<TMPro.TextMeshPro>(A).First(t => t.name == "HubBoard");
    Check(Plain(hubBoard.text).Contains("HUB: running"), "hub board: " + Plain(hubBoard.text));
    Check(All<TMPro.TextMeshPro>(A).First(t => t.name == "BuildInfo").text.Contains("BUILD INFO"), "build info: " + Plain(All<TMPro.TextMeshPro>(A).First(t => t.name == "BuildInfo").text));
    foreach (var who in new[] { A, B }) {
      Console.WriteLine("\n== " + who.player.displayName + " presses every test");
      var start = who.player.pos;
      foreach (var b in All<FirCheckButton>(who).OrderBy(x => x.test)) {
        Sim.Press(who, b); Sim.Run(1f);
        Check(Plain(b.label.text).Contains("PASS"), Plain(b.label.text));
        if (b.test == 1) { var back = All<FirCheckButton>(who).First(x => x.test == 11); Sim.Press(who, back); Sim.Run(1f); Check(Plain(back.label.text).Contains("PASS") && Vector3.Distance(who.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f, Plain(back.label.text)); }
      }
      var typed = All<FirCheckTyped>(who)[0]; Sim.Press(who, typed); Sim.Run(0.5f);
      Check(Plain(typed.label.text).Contains("PASS"), Plain(typed.label.text));
      var sk = All<FirCheckSkeleton>(who)[0]; Sim.Press(who, sk); Sim.Run(0.5f);
      Check(Plain(sk.label.text).Contains("PASS"), Plain(sk.label.text));
      var tr = All<FirCheckTree>(who)[0]; Sim.Press(who, tr); Sim.Run(0.5f);
      Check(Plain(tr.label.text).Contains("PASS"), Plain(tr.label.text));
    }
    Console.WriteLine("\n== The rainbow of skeletons (Alice presses each one)");
    var rf = typeof(FirRainbowSkeleton).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    foreach (var rs in All<FirRainbowSkeleton>(A).OrderBy(x => x.act)) {
      Sim.Events.Clear();
      var jawRest = rs.pivots[9].localRotation;
      Sim.Press(A, rs);
      float maxJaw = 0f, maxDrop = 0f; var skullHome = rs.meshes[2].position;
      for (int f = 0; f < 60; f++) { Sim.Step(); maxJaw = Mathf.Max(maxJaw, Quaternion.Angle(jawRest, rs.pivots[9].localRotation)); maxDrop = Mathf.Max(maxDrop, skullHome.y - rs.meshes[2].position.y); }
      bool spoke = Sim.Events.Any(e => e.Contains("sound:") && e.Contains(rs.voice.clip.name));
      Check((bool)rf.GetValue(rs) && spoke && maxJaw > 10f, Plain(rs.colourName) + " skeleton: routine running, said its line (" + rs.voice.clip.name + "), jaw moved " + maxJaw.ToString("F0") + " degrees");
      if (rs.act == 6) Check(maxDrop > 1.0f, "  violet went to pieces (its skull fell " + maxDrop.ToString("F2") + " m)");
      Sim.Run(8f);
      Check(!(bool)rf.GetValue(rs) && Quaternion.Angle(jawRest, rs.pivots[9].localRotation) < 0.5f && Vector3.Distance(rs.meshes[2].position, skullHome) < 0.01f, "  ...and back to standing");
    }
    Console.WriteLine("\n== Combination acts (Bob presses each one)");
    var cf = typeof(FirSkeletonCombo).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    foreach (var co in All<FirSkeletonCombo>(B).OrderBy(x => x.combo)) {
      Sim.Events.Clear();
      int riders = co.pivots.Length / 10;
      var rest = co.pivots.Select(p => p.localRotation).ToArray();
      var restPos = co.pivots.Select(p => p.localPosition).ToArray();
      var moved = new float[riders]; var jaw = new float[riders];
      Sim.Press(B, co);
      int steps = (int)(co.length / Time.deltaTime) - 5;
      for (int f = 0; f < steps; f++) {
        Sim.Step();
        for (int r = 0; r < riders; r++) {
          for (int j = 0; j < 9; j++) moved[r] = Mathf.Max(moved[r], Quaternion.Angle(rest[r * 10 + j], co.pivots[r * 10 + j].localRotation) + 100f * Vector3.Distance(restPos[r * 10 + j], co.pivots[r * 10 + j].localPosition));
          jaw[r] = Mathf.Max(jaw[r], Quaternion.Angle(rest[r * 10 + 9], co.pivots[r * 10 + 9].localRotation));
        }
      }
      int linesHeard = co.lines.Count(l => Sim.Events.Any(e => e.Contains("sound:") && e.Contains(l.clip.name)));
      Check(linesHeard == co.lines.Length && moved.All(m => m > 8f) && jaw.All(m => m > 10f),
            co.title + ": " + riders + " skeletons all moved and talked, " + linesHeard + "/" + co.lines.Length + " lines, " + co.length.ToString("F1") + " s");
      Sim.Run(1.5f);
      bool back = !(bool)cf.GetValue(co) && Enumerable.Range(0, co.pivots.Length).All(i => Quaternion.Angle(rest[i], co.pivots[i].localRotation) < 0.5f && Vector3.Distance(restPos[i], co.pivots[i].localPosition) < 0.001f);
      Check(back, "  ...everyone back in place; button says: " + Plain(co.label.text));
    }
    var tinyRoot = All<FirSkeletonCombo>(A).First(x => x.combo == 4).pivots[0];
    var upsy = All<FirSkeletonCombo>(A).First(x => x.combo == 4);
    var tinyHome = tinyRoot.position; float tinyMax = 0f;
    Sim.Press(A, upsy);
    for (int f = 0; f < (int)(upsy.length / Time.deltaTime); f++) { Sim.Step(); tinyMax = Mathf.Max(tinyMax, tinyRoot.position.y - tinyHome.y); }
    Check(tinyMax > 2f, "Upsy-daisy: the giant lifted tiny " + tinyMax.ToString("F2") + " m up");
    Sim.Run(1f);
    foreach (var c in new[] { A, B }) {
      var hub = All<FirCheckHub>(c)[0];
      Check(hub.sharedCount == 2, c.player.displayName + " sees the shared counter at " + hub.sharedCount + " (each player pressed once)");
    }
    var mat = All<FirCheckButton>(A).First(x => x.test == 9).testMaterial;
    Check(mat.GetFloat("_Dark") > 0.5f, "Alice's test panel is dark after test 9 (" + mat.GetFloat("_Dark") + ")");
    Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems");
    foreach (var f in Sim.Failures.Take(20)) Console.WriteLine("  problem: " + f);
    return fail == 0 && Sim.Failures.Count == 0 ? 0 : 1;
  }
}
