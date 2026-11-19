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
    var tinyHome = tinyRoot.position; float tinyMax = 0f; Vector3 tinyTop = tinyHome;
    var giantRoot = upsy.pivots[10];
    Sim.Press(A, upsy);
    for (int f = 0; f < (int)(upsy.length / Time.deltaTime); f++) { Sim.Step(); if (tinyRoot.position.y - tinyHome.y > tinyMax) { tinyMax = tinyRoot.position.y - tinyHome.y; tinyTop = tinyRoot.position; } }
    var flat = new Vector3(tinyTop.x - giantRoot.position.x, 0, tinyTop.z - giantRoot.position.z);
    Check(tinyMax > 2f && flat.magnitude < 0.4f, "Upsy-daisy: the giant lifted tiny " + tinyMax.ToString("F2") + " m up, right over its head (" + flat.magnitude.ToString("F2") + " m off centre)");
    Console.WriteLine("\n== The map: walking through it");
    foreach (var l in Sim.BuildLog.Where(x => x.Contains("placed in the map")).Distinct()) Check(!l.Contains("problem") && System.Text.RegularExpressions.Regex.IsMatch(l, @"(\d+) of \1 "), l.Replace("Fir Warning systems check: ", ""));
    Check(!Sim.BuildLog.Any(x => x.Contains("problem")), "the builder reported no problems" + string.Concat(Sim.BuildLog.Where(x => x.Contains("problem")).Distinct().Take(5).Select(x => " | " + x)));
    // a route a player can walk, round the consoles and the stages, through every doorway; nothing may block it
    var route = new[] { new Vector3(0, 0, -2.6f), new Vector3(0, 0, -7f), new Vector3(0, 0, -27f), new Vector3(6.8f, 0, -33f), new Vector3(6.8f, 0, -40.8f),
                        new Vector3(0, 0, -40.8f), new Vector3(0, 0, -43f), new Vector3(0, 0, -81f), new Vector3(7.3f, 0, -82.6f), new Vector3(8.0f, 0, -86f), new Vector3(8.0f, 0, -97.2f),
                        new Vector3(7.5f, 0, -99f), new Vector3(0, 0, -112f), new Vector3(0, 0, -116.6f) };
    Sim.Current = A; Physics.SyncTransforms();
    for (int i = 0; i + 1 < route.Length; i++) {
      foreach (float h in new[] { 0.4f, 1.2f, 1.9f }) {
        var a0 = route[i] + new Vector3(0, h, 0); var d = route[i + 1] - route[i];
        RaycastHit hit;
        bool blocked = Physics.Raycast(a0, d.normalized, out hit, d.magnitude, 1, QueryTriggerInteraction.Ignore);
        if (blocked) { Check(false, "route leg " + i + " " + route[i] + " -> " + route[i + 1] + " blocked at " + hit.point + " by " + hit.collider.name); goto nextLeg; }
      }
      nextLeg:;
    }
    Check(true, "walked the route: lobby -> 6 rooms -> the end (" + route.Length + " points)");
    Sim.Current = null;
    // the way home
    var home = All<FirCheckButton>(A).First(x => x.title == "Back to the lobby");
    A.player.pos = new Vector3(0, 0.05f, -116.5f);
    Sim.Run(1.2f); Sim.Press(A, home); Sim.Run(1f);
    Check(Vector3.Distance(A.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f, "the Bone Yard's teleporter took Alice back to the lobby");

    Console.WriteLine("\n== The original gags (Bob)");
    var gf = typeof(FirGag).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    int gagOk = 0, gagN = 0; var gagBad = new List<string>();
    foreach (var gag in All<FirGag>(B)) {
      gagN++;
      Sim.Events.Clear();
      var home0 = gag.target.localPosition; var rot0 = gag.target.localRotation; var sc0 = gag.target.localScale;
      float mouthRest = gag.mouth != null ? gag.mouth.localScale.y : 0f;
      if (gag.walkIn) { B.player.pos = gag.transform.TransformPoint(gag.zoneCenter) - new Vector3(0, 0.9f, 0); }
      else Sim.Press(B, gag);
      float moved = 0f; bool bubbled = false;
      for (int f = 0; f < 40; f++) {
        Sim.Step();
        moved = Mathf.Max(moved, Vector3.Distance(home0, gag.target.localPosition) * 100f + Quaternion.Angle(rot0, gag.target.localRotation) + Mathf.Abs(gag.target.localScale.x - sc0.x) * 100f);
        if (gag.bubble.gameObject.activeSelf && gag.bubble.text == gag.line) bubbled = true;
        if (gag.mouth != null) moved = Mathf.Max(moved, Mathf.Abs(gag.mouth.localScale.y - mouthRest) * 100f);
      }
      bool spoke = Sim.Events.Any(e => e.Contains("sound:") && gag.voice.clip != null && e.Contains(gag.voice.clip.name));
      bool wasRunning = (bool)gf.GetValue(gag) || moved > 1f;
      B.player.pos = new Vector3(40, 0.05f, 0);   // out of every walk-in zone
      Sim.Run(gag.inTime + gag.holdTime + gag.outTime + 0.5f);
      bool reset = !(bool)gf.GetValue(gag) && Vector3.Distance(home0, gag.target.localPosition) < 0.001f && !gag.bubble.gameObject.activeSelf;
      if (wasRunning && spoke && bubbled && moved > 1f && reset) gagOk++; else gagBad.Add(gag.name + (spoke ? "" : " (silent)") + (bubbled ? "" : " (no bubble)") + (moved > 1f ? "" : " (didn't move)") + (reset ? "" : " (didn't reset)"));
    }
    Check(gagOk == gagN && gagN == 53, gagOk + " of " + gagN + " original gags played, talked, showed their line and reset" + string.Concat(gagBad.Take(6).Select(x => " | " + x)));

    Console.WriteLine("\n== The original skeleton routines and the RED SKELETON closet (Bob)");
    var sf = typeof(FirSkeletonGag).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    int skOk = 0, skN = 0;
    foreach (var sk in All<FirSkeletonGag>(B)) {
      skN++; Sim.Events.Clear();
      B.player.pos = sk.transform.position + sk.transform.rotation * new Vector3(0, 0, 2f);
      Sim.Press(B, sk); bool bub = false, ran = false;
      for (int f = 0; f < 90; f++) { Sim.Step(); if (sk.bubble.gameObject.activeSelf) bub = true; if ((bool)sf.GetValue(sk)) ran = true; }
      bool spoke = Sim.Events.Any(e => e.Contains("sound:") && e.Contains(sk.voice.clip.name));
      if (ran && bub && spoke) skOk++; else Console.WriteLine("    " + sk.name + ": running " + sf.GetValue(sk) + " bubble " + bub + " spoke " + spoke);
      Sim.Run(7f);
    }
    Check(skOk == skN && skN == 12, skOk + " of " + skN + " skeleton routines played, talked and showed their line");
    var closet = All<FirCloset>(B)[0];
    B.player.pos = closet.transform.position + new Vector3(3f, 0, -1f);
    var clf = typeof(FirCloset).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    Sim.Press(B, closet); bool cb = false; bool whiteOut = false;
    for (int f = 0; f < 80; f++) { Sim.Step(); if (closet.bubble.gameObject.activeSelf) cb = true; if (closet.white[0].gameObject.activeSelf) whiteOut = true; }
    Check((bool)clf.GetValue(closet) && cb && whiteOut, "the RED SKELETON closet opens: a skeleton steps out and its line shows");
    Sim.Run(8f);

    Console.WriteLine("\n== The audience: throwable babies");
    var babies = All<FirThrowable>(A);
    bool kitted = babies.All(b => b.GetComponent<VRC.SDK3.Components.VRCPickup>() != null && b.GetComponent<Rigidbody>() != null && b.GetComponent<BoxCollider>() != null
                                  && b.GetComponent<VRC.SDK3.Components.VRCObjectSync>() != null && b.gameObject.layer == 13);
    Check(babies.Count == 30 && kitted, babies.Count + " babies, each a pickup with a rigidbody, a collider and object sync, on the Pickup layer");
    Sim.Current = A; Physics.SyncTransforms();
    int seated = babies.Count(b => { RaycastHit h; return Physics.Raycast(b.transform.position + Vector3.up * 0.05f, Vector3.down, out h, 0.2f, 1, QueryTriggerInteraction.Ignore) && h.collider.name == "Seat"; });
    Sim.Current = null;
    Check(seated == babies.Count, seated + " of them sitting on a seat, facing the stage");
    var baby = babies[0]; Sim.Events.Clear();
    Sim.With(A, () => baby.OnPickup());
    baby.body.velocity = new Vector3(0, 2f, -6f); Sim.Run(0.2f); baby.body.velocity = Vector3.zero; Sim.Run(0.2f);
    Check(Sim.Events.Any(e => e.Contains("whoosh")) && Sim.Events.Any(e => e.Contains("toilet_clunk")), "a thrown baby whooshes when grabbed and thuds when it lands");

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
