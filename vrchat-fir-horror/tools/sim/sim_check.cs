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

  public static int Main(string[] args) {
    Sim.SceneBuilder = c => { if (!FirCheckBuilder.Build(true)) Sim.Fail("Build Systems Check returned false"); return null; };
    var A = Sim.Join("Alice", new Vector3(0, 0.05f, -4));
    var B = Sim.Join("Bob", new Vector3(1, 0.05f, -4));
    if (args.Length > 0 && args[0] == "mystery") { MysteryTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "later") { LaterTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "town") { TownTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "park") { ParkTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "apart") { ApartTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "batch2") { Batch2Tests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "cell") { CellTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "bigtown") { BigTownTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "fans") { FanTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "scream") { ScreamTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "four") { FourTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "scp") { ScpTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "rm") { PortalGarageTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "water") { WaterParkTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "ct") { ChrisTrippTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "remaster") { RemasterTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "rooms") { RoomsTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "expand") { ExpansionTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "north") { NorthTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "lab") { LabTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "pixel") { PixelTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "night") { NightTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed, " + Sim.Failures.Count + " runtime problems"); foreach (var f in Sim.Failures.Take(10)) Console.WriteLine("  problem: " + f); return fail == 0 ? 0 : 1; }
    if (args.Length > 0 && args[0] == "hunt") { HuntTests(A, B); Console.WriteLine("\n" + pass + " passed, " + fail + " failed"); return fail == 0 ? 0 : 1; }
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
      float maxJaw = 0f, maxDrop = 0f; var skullHome = rs.meshes[2].position; var skullLocal = rs.transform.InverseTransformPoint(rs.meshes[2].position);
      for (int f = 0; f < 60; f++) { Sim.Step(); maxJaw = Mathf.Max(maxJaw, Quaternion.Angle(jawRest, rs.pivots[9].localRotation)); maxDrop = Mathf.Max(maxDrop, skullHome.y - rs.meshes[2].position.y); }
      bool spoke = Sim.Events.Any(e => e.Contains("sound:") && e.Contains(rs.voice.clip.name));
      Check((bool)rf.GetValue(rs) && spoke && maxJaw > 10f, Plain(rs.colourName) + " skeleton: routine running, said its line (" + rs.voice.clip.name + "), jaw moved " + maxJaw.ToString("F0") + " degrees");
      if (rs.act == 6) Check(maxDrop > 1.0f, "  violet went to pieces (its skull fell " + maxDrop.ToString("F2") + " m)");
      for (int f = 0; f < 600 && (bool)rf.GetValue(rs); f++) Sim.Step();
      Sim.Run(0.5f);
      Check(!(bool)rf.GetValue(rs) && Quaternion.Angle(jawRest, rs.pivots[9].localRotation) < 0.5f && Vector3.Distance(rs.transform.InverseTransformPoint(rs.meshes[2].position), skullLocal) < 0.01f, "  ...and back to standing");
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
    var townRoute = new[] { new Vector3(0, 0, -2.6f), new Vector3(-4f, 0, 4f), new Vector3(-13f, 0, 4f), new Vector3(-24.5f, 0, 4f), new Vector3(-28f, 0, 6f), new Vector3(-28f, 0, 9.6f),
                            new Vector3(-38.5f, 0, 9.6f), new Vector3(-38.5f, 0, 0f), new Vector3(-34f, 0, -5.5f), new Vector3(-20f, 0, -5.5f), new Vector3(-14f, 0, 4f) };
    int townBlocked = 0;
    for (int i = 0; i + 1 < townRoute.Length; i++)
      foreach (float h in new[] { 0.4f, 1.2f, 1.9f }) {
        var d = townRoute[i + 1] - townRoute[i]; RaycastHit hit;
        if (Physics.Raycast(townRoute[i] + new Vector3(0, h, 0), d.normalized, out hit, d.magnitude, 1, QueryTriggerInteraction.Ignore)) { townBlocked++; Console.WriteLine("    town leg " + i + " blocked by " + hit.collider.name + " at " + hit.point); break; }
      }
    Check(townBlocked == 0, "walked from the lobby's west doorway into Bonetown and round the plaza");
    var cellarRoute = new[] { new Vector3(0, 0, -2.6f), new Vector3(4f, 0, -2f), new Vector3(22f, 0, -2f), new Vector3(22f, 0, -9f), new Vector3(22f, 0, -27.5f),
                              new Vector3(25f, 0, -33f), new Vector3(25f, 0, -42.5f), new Vector3(26f, 0, -43.5f), new Vector3(26f, 0, -45.5f), new Vector3(21.5f, 0, -55f), new Vector3(16.5f, 0, -56f),
                              new Vector3(17.5f, 0, -60.5f), new Vector3(18f, 0, -61.5f), new Vector3(18f, 0, -63.5f), new Vector3(22f, 0, -72f), new Vector3(22f, 0, -90f) };
    int cellarBlocked = 0;
    for (int i = 0; i + 1 < cellarRoute.Length; i++)
      foreach (float h in new[] { 0.4f, 1.2f, 1.9f }) {
        var d = cellarRoute[i + 1] - cellarRoute[i]; RaycastHit hit;
        if (Physics.Raycast(cellarRoute[i] + new Vector3(0, h, 0), d.normalized, out hit, d.magnitude, 1, QueryTriggerInteraction.Ignore)) { cellarBlocked++; Console.WriteLine("    cellar leg " + i + " blocked by " + hit.collider.name + " at " + hit.point); break; }
      }
    Check(cellarBlocked == 0, "walked from the lobby's east doorway through the potting shed and all five rooms of the Root Cellar");
    Sim.Current = null;
    // the way home
    var home = All<FirCheckButton>(A).First(x => x.title == "Back to the lobby");
    A.player.pos = new Vector3(0, 0.05f, -116.5f);
    Sim.Run(1.2f); Sim.Press(A, home); Sim.Run(1f);
    Check(Vector3.Distance(A.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f, "the Bone Yard's teleporter took Alice back to the lobby");

    Console.WriteLine("\n== The original gags (Bob)");
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x < 555f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 186, gagOk + " of " + gagN + " gags (53 original + 22 Bonetown + 5 apartments + 6 bigger-town + 20 park + 35 precinct + 12 pizzeria + 10 pixel + 8 lab + 5 north + 10 mystery) played, talked, showed their line and reset" + string.Concat(gagBad.Take(6).Select(x => " | " + x)));

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
    var babies = All<FirThrowable>(A).Where(b => b.name.StartsWith("Baby")).ToList();
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

    Console.WriteLine("\n== Gags for everyone: Bob presses, Alice sees it too");
    Sim.Run(20f);
    Func<object, bool> Running = o => (bool)(o.GetType().GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
      ?? o.GetType().GetField("chatting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)).GetValue(o);
    Func<Sim.Client, int, int, UdonSharpBehaviour> Pick = (c, kind, i) =>
      kind == 0 ? (UdonSharpBehaviour)All<FirGag>(c).First(g => g.shareIndex == i) : kind == 1 ? (UdonSharpBehaviour)All<FirSkeletonGag>(c).First(g => g.shareIndex == i)
      : kind == 2 ? (UdonSharpBehaviour)All<FirCloset>(c).First(g => g.shareIndex == i) : kind == 3 ? (UdonSharpBehaviour)All<FirRainbowSkeleton>(c).First(g => g.shareIndex == i)
      : kind == 4 ? (UdonSharpBehaviour)All<FirSkeletonCombo>(c).First(g => g.shareIndex == i) : kind == 5 ? (UdonSharpBehaviour)All<FirWanderer>(c).First(g => g.shareIndex == i) : (UdonSharpBehaviour)All<FirResident>(c).First(g => g.shareIndex == i);
    string[] kindName = { "pressed gags", "skeleton routines", "closet", "gag skeletons", "combo acts", "Bonetown wanderers", "apartment residents" };
    int[] kindCount = { All<FirGag>(B).Count, All<FirSkeletonGag>(B).Count, All<FirCloset>(B).Count, All<FirRainbowSkeleton>(B).Count, All<FirSkeletonCombo>(B).Count, All<FirWanderer>(B).Count, All<FirResident>(B).Count };
    for (int kind = 0; kind < 7; kind++) {
      int ok = 0, n = 0; var bad = new List<string>();
      for (int i = 0; i < kindCount[kind]; i++) {
        var mine = Pick(B, kind, i);
        if (kind == 0 && ((FirGag)mine).walkIn) continue;
        if (kind == 0 && ((FirGag)mine).scare) continue;
        n++;
        B.player.pos = mine.transform.position + mine.transform.rotation * new Vector3(0, 0, 1.5f);
        var theirs = Pick(A, kind, i);
        Sim.Press(B, mine);
        bool seen = false;
        for (int f = 0; f < 50 && !seen; f++) { Sim.Step(); if (Running(theirs)) seen = true; }
        if (seen) ok++; else bad.Add(mine.name);
        Sim.Run(kind == 4 ? 40f : kind == 2 ? 11f : 9f);
      }
      Check(ok == n && n > 0, ok + " of " + n + " " + kindName[kind] + " pressed by Bob also played for Alice" + string.Concat(bad.Take(5).Select(x => " | " + x)));
    }
    B.player.pos = new Vector3(40, 0.05f, 0);
    var C = Sim.Join("Carol", new Vector3(0, 0.05f, -4));
    Sim.Run(3f);
    bool replay = All<FirGag>(C).Any(g => Running(g)) || All<FirSkeletonGag>(C).Any(g => Running(g)) || All<FirCloset>(C).Any(g => Running(g))
                  || All<FirRainbowSkeleton>(C).Any(g => Running(g)) || All<FirSkeletonCombo>(C).Any(g => Running(g));
    Check(!replay, "Carol joins late and doesn't get the last press replayed");
    var shareBob = All<FirShare>(B)[0];
    var lastCombo = All<FirSkeletonCombo>(B)[0]; B.player.pos = lastCombo.transform.position + lastCombo.transform.rotation * new Vector3(0, 0, 1.5f);
    Sim.Press(B, lastCombo); bool carolSees = false;
    for (int f = 0; f < 50 && !carolSees; f++) { Sim.Step(); if (Running(All<FirSkeletonCombo>(C)[0])) carolSees = true; }
    Check(carolSees, "...but she does see the next thing Bob presses");
    Sim.Run(40f);
    Sim.Leave(C);

    HuntTests(A, B);
    TownTests(A, B);
    FacingTests(A);
    CellarTests(A, B);
    ParkTests(A, B);
    PrecinctTests(A, B);
    NightTests(A, B);
    PixelTests(A, B);
    LabTests(A, B);
    NorthTests(A, B);
    MysteryTests(A, B);
    LaterTests(A, B);
    ApartTests(A, B);
    Batch2Tests(A, B);
    CellTests(A, B);
    BigTownTests(A, B);
    FanTests(A, B);
    ScreamTests(A, B);
    FourTests(A, B);
    ScpTests(A, B);
    PortalGarageTests(A, B);
    WaterParkTests(A, B);
    ChrisTrippTests(A, B);
    RemasterTests(A, B);
    ExpansionTests(A, B);
    RoomsTests(A, B);

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

  // ======================================================================== the horror game
  static FirHuntButton Btn(Sim.Client c, int act, int val) { return All<FirHuntButton>(c).First(b => b.action == act && b.value == val); }
  static FirHunt H(Sim.Client c) { return All<FirHunt>(c)[0]; }
  static FirStalker St(Sim.Client c) { return All<FirStalker>(c)[0]; }
  static bool DoorsMatch(Sim.Client c, int stage, bool running) { var h = H(c); for (int i = 1; i < 6; i++) if (h.doors[i].activeSelf != (running && stage < 6 && stage < i)) return false; return true; }
  static void Enter(Sim.Client c, int room, string code) {
    foreach (char ch in code) { Sim.Press(c, Btn(c, 5, room * 100 + (ch - '0'))); Sim.Step(); }
    Sim.Press(c, Btn(c, 5, room * 100 + 11));
  }
  static float Flat(Vector3 a, Vector3 b) { var d = a - b; d.y = 0; return d.magnitude; }

  static void HuntTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The horror game: before START");
    var lobbySafe = new Vector3(0, 0.05f, -1f);
    A.player.pos = lobbySafe; B.player.pos = lobbySafe + new Vector3(1, 0, 0);
    Sim.Run(1f);
    foreach (var c in new[] { A, B }) {
      var h = H(c);
      Check(!h.running && DoorsMatch(c, 0, false) && h.items.All(i => !i.activeSelf) && h.darkMats.All(m => m.GetFloat("_Fullbright") > 0.5f),
            c.player.displayName + ": no game yet: doors open, items hidden, the map lit (" + h.darkMats.Length + " map materials)");
    }
    // scare zones only fire during the game
    var scare = All<FirGag>(B).First(g => g.scare);
    var gfr = typeof(FirGag).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    B.player.pos = scare.transform.TransformPoint(scare.zoneCenter) - new Vector3(0, 0.9f, 0); Sim.Run(1f);
    Check(!(bool)gfr.GetValue(scare), "a jump scare zone does nothing outside the game");
    B.player.pos = lobbySafe + new Vector3(1, 0, 0); Sim.Run(1f);

    Console.WriteLine("\n== The horror game: Alice presses START");
    Sim.Press(A, Btn(A, 1, 0)); Sim.Run(1f);
    foreach (var c in new[] { A, B }) {
      var h = H(c);
      Check(h.running && h.stage == 0 && DoorsMatch(c, 0, true) && Flat(c.player.pos, h.checkpoints[0].position) < 0.5f,
            c.player.displayName + ": the game is on, pulled into the Gift Wrap Hall, doors 1-5 locked");
      var lobbyFloor = All<MeshRenderer>(c).First(r => r.name == "Floor" && r.transform.parent.name == "Lobby").sharedMaterial;
      Check(h.darkMats.All(m => m.GetFloat("_Fullbright") < 0.5f) && lobbyFloor.GetFloat("_Fullbright") > 0.5f && !h.darkMats.Contains(lobbyFloor),
            c.player.displayName + ": the map went dark (lantern only); the lobby stays lit");
      Check(h.items.Where((x, i) => h.itemRoom[i] == 0 || true).All(i => i.activeSelf), c.player.displayName + ": all " + h.items.Length + " items are out to find");
    }
    Check(St(A).body.activeSelf && Flat(St(A).transform.position, St(A).lairs[0].position) < 0.3f && Flat(St(B).transform.position, St(A).lairs[0].position) < 0.5f,
          "the tree is waiting at the far end of the hall, for both of them");
    // the lobby is safe: park them there while they solve, so the tree doesn't interfere (it's tested below)
    A.player.pos = lobbySafe; B.player.pos = lobbySafe + new Vector3(1, 0, 0);

    Console.WriteLine("\n== Room 1: batteries and the power box");
    Sim.Press(A, Btn(A, 4, 0)); Sim.Run(0.5f);
    Check(H(A).stage == 0, "the power box does nothing without the batteries");
    var bat = Enumerable.Range(0, H(B).items.Length).Where(i => H(B).itemRoom[i] == 0).ToList();
    foreach (int i in bat) { Sim.Press(B, H(B).items[i].GetComponent<FirHuntButton>()); Sim.Run(0.3f); }
    Sim.Run(0.5f);
    Check(bat.Count == 5 && bat.All(i => !H(A).items[i].activeSelf && !H(B).items[i].activeSelf), "Bob picked up the 5 batteries; they're gone for Alice too");
    Check(Plain(H(A).roomBoards[0].text).Contains("5 of 5 found"), "Alice's task board: " + Plain(H(A).roomBoards[0].text).Substring(0, 40) + "... 5 of 5 found");
    Sim.Press(A, Btn(A, 4, 0)); Sim.Run(0.5f);
    Check(H(A).stage == 1 && H(B).stage == 1 && DoorsMatch(A, 1, true) && DoorsMatch(B, 1, true), "Alice powered the door: the Rainbow Hall is open for both");

    Console.WriteLine("\n== Room 2: the glowing numbers");
    string code = string.Concat(H(A).rainbowDigits.Select(t => t.text));
    string codeB = string.Concat(H(B).rainbowDigits.Select(t => t.text));
    Check(code.Length == 4 && code == codeB && code == H(A).RainbowCode(), "both see the same 4 glowing numbers: " + code);
    string wrong = string.Concat(code.Select(ch => (char)('0' + (ch - '0' + 1) % 10)));
    Enter(B, 1, wrong); Sim.Run(0.3f);
    Check(H(B).stage == 1 && H(B).scareFace.gameObject.activeSelf, "a wrong code (" + wrong + ") gets Bob a tree in the face, and stays locked");
    Sim.Run(1f);
    Enter(B, 1, code); Sim.Run(0.5f);
    Check(H(A).stage == 2 && H(B).stage == 2 && DoorsMatch(A, 2, true), "the right code opens the Toy Workshop");

    Console.WriteLine("\n== Room 3: the toy lock");
    var names = H(B).toyNames;
    var order = H(B).toyNotes.Select(t => Array.IndexOf(names, Plain(t.text).Split('|').Last().Trim())).ToList();
    Check(order.All(i => i >= 0) && order.Distinct().Count() == 4 && H(A).toyNotes.Select(t => t.text).SequenceEqual(H(B).toyNotes.Select(t => t.text)),
          "the 4 notes give the order: " + string.Join(", ", order.Select(i => names[i])));
    int notFirst = Enumerable.Range(0, 5).First(i => i != order[0]);
    Sim.Press(A, Btn(A, 6, 200 + order[0])); Sim.Step(); Sim.Press(A, Btn(A, 6, 200 + notFirst == 200 + order[1] ? 200 + order[2] : 200 + notFirst)); Sim.Run(0.3f);
    Check(H(A).stage == 2, "a wrong toy starts Alice over");
    Sim.Run(1f);
    foreach (int i in order) { Sim.Press(A, Btn(A, 6, 200 + i)); Sim.Step(); }
    Sim.Run(0.5f);
    Check(H(A).stage == 3 && H(B).stage == 3 && DoorsMatch(B, 3, true), "the toys in order open the Chimney Lounge");

    Console.WriteLine("\n== Room 4: the ornaments and the bare tree");
    Sim.Press(B, Btn(B, 4, 3)); Sim.Run(0.3f);
    Check(H(B).stage == 3, "the bare tree needs the ornaments first");
    foreach (int i in Enumerable.Range(0, H(A).items.Length).Where(i => H(A).itemRoom[i] == 3)) { Sim.Press(A, H(A).items[i].GetComponent<FirHuntButton>()); Sim.Run(0.3f); }
    Sim.Press(B, Btn(B, 4, 3)); Sim.Run(0.5f);
    Check(H(A).stage == 4 && H(B).stage == 4 && H(B).stageShow[0].activeSelf, "the ornaments hung, the tree lights up: the Talent Show is open");

    Console.WriteLine("\n== Room 5: the stage door");
    Check(H(A).talentCode == "1033", "the memo's code works out to " + H(A).talentCode + " (10 performers, 33 seats)");
    Enter(A, 4, "1030"); Sim.Run(0.3f);
    Check(H(A).stage == 4, "counting the babies instead (1030) doesn't work");
    Sim.Run(1f);
    Enter(A, 4, H(A).talentCode); Sim.Run(0.5f);
    Check(H(A).stage == 5 && H(B).stage == 5 && DoorsMatch(A, 5, true), "the stage door opens: the Bone Yard");

    Console.WriteLine("\n== Room 6: the gas and the furnace");
    Sim.Press(B, Btn(B, 4, 5)); Sim.Run(0.3f);
    Check(H(B).stage == 5, "the lever does nothing with the gas off");
    var valves = Enumerable.Range(0, H(B).items.Length).Where(i => H(B).itemRoom[i] == 5).ToList();
    foreach (int i in valves) { Sim.Press(B, H(B).items[i].GetComponent<FirHuntButton>()); Sim.Run(0.3f); }
    Sim.Run(0.5f);
    Check(valves.Count == 3 && valves.All(i => H(A).itemDone[i].activeSelf && !H(A).items[i].activeSelf), "3 valves opened (Alice sees them turned)");
    Sim.Events.Clear();
    Sim.Press(B, Btn(B, 4, 5)); Sim.Run(1f);
    foreach (var c in new[] { A, B }) {
      var h = H(c);
      Check(h.stage == 6 && DoorsMatch(c, 6, true) && h.stageShow[2].activeSelf && h.darkMats.All(m => m.GetFloat("_Fullbright") > 0.5f)
            && Flat(St(c).transform.position, St(A).furnace.position) < 0.5f && Plain(h.lobbyBoard.text).Contains("ESCAPED"),
            c.player.displayName + ": YOU ESCAPED: the tree is in the furnace, the lights are back, every door is open");
    }
    Check(Sim.Events.Any(e => e.Contains("ending")), "the ending music plays");

    Console.WriteLine("\n== The hunting tree");
    Sim.Press(A, Btn(A, 2, 0)); Sim.Run(0.5f);
    Check(!H(B).running && DoorsMatch(B, 0, false) && !St(B).body.activeSelf, "RESET: back to the open, lit museum");
    Sim.Press(A, Btn(A, 1, 0)); Sim.Run(0.5f);
    A.player.pos = lobbySafe;
    var cp = H(B).checkpoints[0].position;
    B.player.pos = cp; B.player.LookAt(St(A).transform.position + Vector3.up * 1.3f);
    var startPos = St(A).transform.position;
    Sim.Run(24f);
    Check(Flat(St(A).transform.position, startPos) < 0.05f, "Bob stares at it: it doesn't move (" + Flat(St(A).transform.position, startPos).ToString("F2") + " m)");
    B.player.LookAt(B.player.pos + new Vector3(0, 1.6f, 10f));   // looks back at the door
    Sim.Run(3f);
    float moved = Flat(St(A).transform.position, startPos);
    Check(moved > 2f, "Bob looks away: it comes for him (" + moved.ToString("F1") + " m in 3 s)");
    Check(Flat(St(B).transform.position, St(A).transform.position) < 1.5f, "Bob's client sees it where Alice's (its owner) has it");
    A.player.pos = cp + new Vector3(-1.5f, 0, -1f); A.player.LookAt(St(A).transform.position + Vector3.up * 1.3f);
    Sim.Run(0.25f);   // it re-checks who's watching every 0.1 s
    var held = St(A).transform.position;
    {
      Sim.Current = A; Physics.SyncTransforms();
      var head = A.player.GetTrackingData(VRC.SDKBase.VRCPlayerApi.TrackingDataType.Head); var eye = St(A).transform.position + Vector3.up * 1.3f; RaycastHit hh;
      var dd = eye - head.position;
      if (Physics.Raycast(head.position, dd.normalized, out hh, dd.magnitude, 1, QueryTriggerInteraction.Ignore)) Console.WriteLine("    (Alice's view of the tree is blocked by " + hh.collider.name + " at " + hh.point + ")");
      Console.WriteLine("    (angle " + Vector3.Angle(head.rotation * Vector3.forward, dd).ToString("F1") + ", tree at " + St(A).transform.position + ", Alice at " + A.player.pos + ")");
      Sim.Current = null;
    }
    Sim.Run(3f);
    Check(Flat(St(A).transform.position, held) < 0.05f, "Alice watches it while Bob looks away: it freezes");
    // a toilet to the face
    var toilet = All<FirThrowable>(B).First(t => t.name == "Toilet");
    Sim.With(B, () => VRC.SDKBase.Networking.SetOwner(B.player, toilet.gameObject));
    A.player.pos = lobbySafe;
    toilet.transform.position = St(B).transform.position + new Vector3(0, 1.2f, 0.6f);
    toilet.body.velocity = new Vector3(0, 0, -6f);
    Sim.Run(0.3f); toilet.body.velocity = Vector3.zero; Sim.Run(0.5f);
    Check(H(A).stunCount == 1 && H(B).stunCount == 1, "Bob throws a toilet at it: stunned (Alice sees it too)");
    var stunned = St(A).transform.position; Sim.Run(4f);
    Check(Flat(St(A).transform.position, stunned) < 0.05f, "...and it stays put while stunned, though nobody is watching");
    // caught
    int caught0 = H(A).caughtCount;
    bool faced = false, tele = false;
    for (int f = 0; f < 600 && !tele; f++) {
      Sim.Step();
      if (H(B).scareFace.gameObject.activeSelf) faced = true;
      if (faced && Flat(B.player.pos, cp) < 0.3f && H(B).caughtCount > caught0) tele = true;
      if (!faced) B.player.pos = cp;   // Bob stands still, looking away
    }
    Sim.Run(1.5f);
    Check(faced && H(A).caughtCount == caught0 + 1 && H(B).caughtCount == caught0 + 1 && Flat(B.player.pos, cp) < 0.3f, "it reaches Bob: tree in the face, he's sent back to the room's entrance");
    Check(Flat(St(A).transform.position, cp) > 8f, "...and it backs off to the far end (" + Flat(St(A).transform.position, cp).ToString("F1") + " m away)");

    Console.WriteLine("\n== Jump scares and a late joiner");
    B.player.pos = scare.transform.TransformPoint(scare.zoneCenter) - new Vector3(0, 0.9f, 0); Sim.Events.Clear(); Sim.Run(0.3f);
    Check((bool)gfr.GetValue(scare) && Sim.Events.Any(e => e.Contains("scream")), "during the game, walking past " + scare.name + " makes a tree burst out screaming");
    B.player.pos = lobbySafe; Sim.Run(2f);
    var C = Sim.Join("Carol", new Vector3(0, 0.05f, -2.6f));
    Sim.Run(3f);
    Check(H(C).running && H(C).stage == H(A).stage && DoorsMatch(C, H(A).stage, true) && Flat(C.player.pos, new Vector3(0, 0, -2.6f)) < 0.3f,
          "Carol joins mid-game: she sees the game state but stays in the lobby");
    Sim.Press(C, Btn(C, 1, 0)); Sim.Run(0.5f);
    Check(Flat(C.player.pos, H(C).checkpoints[H(C).stage].position) < 0.3f && H(A).round == H(C).round, "...START puts her in the current room without restarting it");
    Sim.Leave(C);
    Sim.Press(A, Btn(A, 2, 0)); Sim.Run(1f);
  }

  // ======================================================================== Bonetown, the expansion
  static void TownTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== Bonetown: the wanderers");
    var wa = All<FirWanderer>(A).Where(w => w.transform.position.x < -12f).ToList(); var wb = All<FirWanderer>(B).Where(w => w.transform.position.x < -12f).ToList();
    Check(wa.Count == 20, wa.Count + " wanderers live in Bonetown (14 in the old town, 6 on the new streets)");
    Sim.Run(1f);
    var p0 = wa.Select(w => w.transform.position).ToList();
    var leg0 = wa.Select(w => w.pivots[7].localRotation).ToList();
    float maxLeg = 0f; var walked = new float[wa.Count]; var prev = p0.ToList();
    for (int f = 0; f < 60; f++) {
      Sim.Step();
      for (int i = 0; i < wa.Count; i++) { maxLeg = Mathf.Max(maxLeg, Quaternion.Angle(leg0[i], wa[i].pivots[7].localRotation)); walked[i] += Flat(wa[i].transform.position, prev[i]); prev[i] = wa[i].transform.position; }
    }
    var speeds = wa.Select((w, i) => walked[i] / (60f * Time.deltaTime)).ToList();
    Check(speeds.All(v => v > 0.4f) && maxLeg > 15f, "they all walk (0.4+ m/s), legs swinging (" + maxLeg.ToString("F0") + " degrees): " + string.Join(", ", wa.Select((w, i) => w.name.Split(' ')[0] + " " + speeds[i].ToString("F1"))));
    Check(wa.Zip(wb, (x, y) => Flat(x.transform.position, y.transform.position)).All(d => d < 0.05f), "Alice and Bob see every wanderer in the same place (no syncing: it's the server clock)");
    var cocoa = wa.First(w => w.name.StartsWith("Cocoa")); var lime = wa.First(w => w.name.StartsWith("Lime"));
    Check(speeds[wa.IndexOf(cocoa)] > 1.8f && speeds[wa.IndexOf(lime)] < 1.2f, "Cocoa jogs (" + speeds[wa.IndexOf(cocoa)].ToString("F1") + " m/s), Lime strolls (" + speeds[wa.IndexOf(lime)].ToString("F1") + " m/s)");
    // they stay on their paths: inside the town, off the fountain, the homes and the bandstand, and nothing solid in the way
    int bad = 0; string what = "";
    Sim.Current = A; Physics.SyncTransforms(); Sim.Current = null;
    var last = wa.Select(w => w.transform.position).ToList();
    for (int k = 0; k < 160; k++) {
      Sim.Run(0.5f);
      Sim.Current = A;
      for (int i = 0; i < wa.Count; i++) {
        var p = wa[i].transform.position;
        bool newStreets = wa[i].voice.clip != null && wa[i].voice.clip.name.StartsWith("w_");
        bool ok = newStreets || p.x < -12.6f && p.x > -47.4f && p.z > -9.2f && p.z < 17.2f && Flat(p, new Vector3(-30f, 0, 4f)) > 2.3f && !(p.x < -43f && p.z > -1.7f && p.z < 7.7f);
        RaycastHit hit;
        var d = p - last[i];
        if (ok && d.magnitude > 0.01f && d.magnitude < 3f && Physics.Raycast(last[i] + Vector3.up * 0.6f, d.normalized, out hit, d.magnitude, 1, QueryTriggerInteraction.Ignore)) { ok = false; what = wa[i].name + " walked through " + hit.collider.name + " (" + hit.collider.transform.parent.name + ") from " + last[i] + " to " + p; }
        if (!ok) { bad++; if (what == "") what = wa[i].name + " at " + p; }
        last[i] = p;
      }
      Sim.Current = null;
    }
    Check(bad == 0, "80 seconds of wandering: every one stays on its path, clear of walls, homes, benches, the fountain and the bandstand" + (bad > 0 ? " | " + bad + " problems, e.g. " + what : ""));
    // say hi
    var la = lime; var lb = wb.First(w => w.name.StartsWith("Lime"));
    B.player.pos = lb.transform.position + new Vector3(1.2f, 0, 0);
    Sim.Events.Clear();
    var stopAt = lb.transform.position;
    Sim.Press(B, lb); Sim.Run(1.0f);
    var chat = typeof(FirWanderer).GetField("chatting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    Check((bool)chat.GetValue(lb) && (bool)chat.GetValue(la) && lb.bubbleObject.activeSelf && Sim.Events.Any(e => e.Contains("wan_lime")),
          "Bob says hi to Lime: it stops, waves and talks, for both of them: \"" + Plain(lb.line) + "\"");
    float len = lb.voice.clip.length + 1.4f;
    Sim.Run(len - 1.3f);
    Check(Flat(lb.transform.position, stopAt) < 0.05f, "...and stays put while it chats");
    Sim.Run(3f);
    Check(!(bool)chat.GetValue(lb) && Flat(lb.transform.position, stopAt) > 1f && Flat(la.transform.position, lb.transform.position) < 0.5f,
          "then it carries on its way, from where it stopped, still in step on both clients (" + Flat(la.transform.position, lb.transform.position).ToString("F2") + " m apart)");

    Console.WriteLine("\n== Bonetown: residents, the band and the gags");
    var names = new[] { "Mint", "Peach", "Lavender", "Coral", "Sky", "Navy", "Rust", "Olive", "Cream", "Plum", "Scarlet", "Cobalt", "Emerald", "Mustard" };
    var rsA = All<FirRainbowSkeleton>(A).Where(r => names.Any(n => r.name == n + " Skeleton")).ToList();
    Check(rsA.Count == 14 && rsA.All(r => r.voice.clip != null && r.voice.clip.name.StartsWith(r.name.Split(' ')[0].ToLower() == "scarlet" || r.name.StartsWith("Cobalt") || r.name.StartsWith("Emerald") || r.name.StartsWith("Mustard") ? "band_" : "res_")),
          rsA.Count + " residents and band members, each with its own voice line");
    var combos = All<FirSkeletonCombo>(A).Where(c => c.combo >= 6 && c.transform.position.x < -12f).ToList();
    Check(combos.Count == 4, combos.Count + " Bonetown combo acts: " + string.Join(", ", combos.Select(c => c.title)));
    Check(Sim.BuildLog.Any(l => l.Contains("Bonetown: 10 residents, 4 in the band, 10 wanderers, 4 combo acts, 22 gags")), "the builder placed all of Bonetown");
    var tick = All<FirTicker>(A)[0]; string t0 = tick.text.text; Sim.Run(6.5f);
    Check(tick.text.text != "" && tick.text.text != t0, "the news ticker rolls: " + tick.text.text);
  }

  // ======================================================================== which way everyone faces (the models face +z)
  static void FacingTests(Sim.Client A) {
    Console.WriteLine("\n== Facing the right way");
    var rs = All<FirRainbowSkeleton>(A);
    Func<FirRainbowSkeleton, Vector3> Fwd = r => r.transform.rotation * Vector3.forward;
    var theatre = rs.Where(r => r.transform.parent != null && (r.transform.parent.name == "RAINBOW HALL" || r.transform.parent.name == "TALENT SHOW")).ToList();
    Check(theatre.Count == 17 && theatre.All(r => Fwd(r).z > 0.9f), theatre.Count + " skeletons in the Rainbow Hall and on the Talent Show stage face the audience (north)");
    var townNames = new[] { "Mint", "Peach", "Lavender", "Coral", "Sky", "Navy", "Rust", "Olive", "Cream", "Plum", "Scarlet", "Cobalt", "Emerald", "Mustard" };
    var town = rs.Where(r => townNames.Any(n => r.name == n + " Skeleton")).ToList();
    var plaza = new Vector3(-30f, 0, 4f);
    Check(town.Count == 14 && town.All(r => Vector3.Dot(Fwd(r), (plaza - r.transform.position).normalized) > 0.5f), town.Count + " Bonetown residents and band members face the plaza");
    var babies = All<FirThrowable>(A).Where(b => b.name.StartsWith("Baby")).ToList();
    var backs = All<MeshRenderer>(A).Where(m => m.name == "SeatBack").Select(m => m.transform.position).ToList();
    bool ok = babies.All(b => (b.transform.rotation * Vector3.forward).z < -0.9f
                            && backs.Any(p => Mathf.Abs(p.x - b.transform.position.x) < 0.05f && p.z > b.transform.position.z + 0.2f && p.z < b.transform.position.z + 0.6f));
    Check(babies.Count == 30 && ok, "every baby faces the stage, with its seat's backrest behind it");
  }

  // ======================================================================== the Root Cellar (the second game) and the 40 new skeletons
  static FirHunt H2(Sim.Client c) { return All<FirHunt>(c)[1]; }
  static FirHuntButton Btn2(Sim.Client c, int act, int val) { var h = H2(c); return All<FirHuntButton>(c).First(b => b.action == act && b.value == val && b.hunt != null && b.hunt.program == h); }
  static void Enter2(Sim.Client c, int room, string code) {
    foreach (char ch in code) { Sim.Press(c, Btn2(c, 5, room * 100 + (ch - '0'))); Sim.Step(); }
    Sim.Press(c, Btn2(c, 5, room * 100 + 11));
  }
  static bool DoorsMatch2(Sim.Client c, int stage, bool running) { var h = H2(c); for (int i = 1; i < 5; i++) if (h.doors[i].activeSelf != (running && stage < 5 && stage < i)) return false; return true; }

  static void CellarTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The 40 new skeletons");
    var xs = All<FirRainbowSkeleton>(A).Where(r => r.voice.clip != null && r.voice.clip.name.StartsWith("xs_")).ToList();
    var xw = All<FirWanderer>(A).Where(w => w.voice.clip != null && w.voice.clip.name.StartsWith("xs_")).ToList();
    Check(xs.Count + xw.Count == 40, (xs.Count + xw.Count) + " new skeletons, each with its own voice line (" + xs.Count + " standing, " + xw.Count + " wandering Bonetown)");
    Check(Sim.BuildLog.Any(l => l.Contains("18 of 18 new skeletons placed in the map")), "18 of them found free spots along the original rooms' walls");
    var newActs = All<FirRainbowSkeleton>(A).Select(r => r.act).Where(a => a >= 18).Distinct().Count();
    Check(newActs == 15, newActs + " of the 15 new moves are in use");

    Console.WriteLine("\n== The Root Cellar: Alice presses START in the potting shed");
    var shed = new Vector3(22f, 0.05f, -2f);
    A.player.pos = shed; B.player.pos = shed + new Vector3(1, 0, 0);
    Sim.Run(1f);
    Check(!H2(A).running && DoorsMatch2(A, 0, false) && H2(A).darkMats.Length > 20 && H2(A).darkMats.All(m => m.GetFloat("_Fullbright") > 0.5f), "no game yet: the cellar is open and lit (" + H2(A).darkMats.Length + " materials of its own)");
    Sim.Press(A, Btn2(A, 1, 0)); Sim.Run(1f);
    foreach (var c in new[] { A, B }) {
      var h = H2(c);
      Check(h.running && h.stage == 0 && DoorsMatch2(c, 0, true) && Flat(c.player.pos, h.checkpoints[0].position) < 0.5f && !H(c).running,
            c.player.displayName + ": the cellar game is on (the first game isn't), pulled into the Greenhouse, doors locked");
      Check(h.darkMats.All(m => m.GetFloat("_Fullbright") < 0.5f) && H(c).darkMats.All(m => m.GetFloat("_Fullbright") > 0.5f),
            c.player.displayName + ": the cellar went dark; the first game's rooms stay lit");
    }
    var stA = All<FirStalker>(A).Where(x => x.name.StartsWith("Cellar Stalker")).ToList();
    Check(stA.Count == 2 && stA[0].body.activeSelf && !stA[1].body.activeSelf, "two trees down here: the first is up, the second still asleep");
    // the lights fail now and then
    bool flick = false;
    for (int f = 0; f < 900 && !flick; f++) { Sim.Step(); if (H2(A).darkMats[0].GetFloat("_Dark") > 0.5f) flick = true; }
    Check(flick, "the lights flicker out for a moment (Alice's view)");
    A.player.pos = shed; B.player.pos = shed + new Vector3(1, 0, 0); Sim.Run(1.5f);

    Console.WriteLine("\n== The Root Cellar: five rooms");
    Sim.Press(A, Btn2(A, 4, 0)); Sim.Run(0.3f);
    Check(H2(A).stage == 0, "the big pot needs the seeds first");
    foreach (int i in Enumerable.Range(0, H2(B).items.Length).Where(i => H2(B).itemRoom[i] == 0)) { Sim.Press(B, H2(B).items[i].GetComponent<FirHuntButton>()); Sim.Run(0.3f); }
    Sim.Press(A, Btn2(A, 4, 0)); Sim.Run(0.5f);
    Check(H2(A).stage == 1 && H2(B).stage == 1 && H2(B).stageShow[0].activeSelf && DoorsMatch2(A, 1, true), "4 seeds planted: a sapling sprouts, and the Sawmill opens");
    string code = string.Concat(H2(A).rainbowDigits.Select(t => t.text));
    Check(code.Length == 4 && code == string.Concat(H2(B).rainbowDigits.Select(t => t.text)), "the stumps' carved numbers: " + code);
    Enter2(B, 1, code); Sim.Run(0.5f);
    Check(H2(A).stage == 2 && H2(B).stage == 2, "the carved code opens the Tool Room");
    Sim.Run(0.5f);
    var stB = All<FirStalker>(B).Where(x => x.name.StartsWith("Cellar Stalker")).ToList();
    Check(stA[1].body.activeSelf && stB[1].body.activeSelf && Flat(stA[1].transform.position, stA[1].lairs[2].position) < 0.3f, "...and the second tree wakes up, at the far end of the Tool Room");
    var names = H2(A).toyNames;
    var order = H2(A).toyNotes.Select(t => Array.IndexOf(names, Plain(t.text).Split('|').Last().Trim())).ToList();
    Check(order.All(i => i >= 0) && order.Distinct().Count() == 4, "the notes give the tool order: " + string.Join(", ", order.Select(i => names[i])));
    foreach (int i in order) { Sim.Press(A, Btn2(A, 6, 200 + i)); Sim.Step(); }
    Sim.Run(0.5f);
    Check(H2(A).stage == 3 && H2(B).stage == 3, "the tools in order open the Generator room");
    foreach (int i in Enumerable.Range(0, H2(A).items.Length).Where(i => H2(A).itemRoom[i] == 3)) { Sim.Press(A, H2(A).items[i].GetComponent<FirHuntButton>()); Sim.Run(0.3f); }
    Sim.Press(B, Btn2(B, 4, 3)); Sim.Run(0.5f);
    Check(H2(A).stage == 4 && H2(B).stage == 4 && DoorsMatch2(B, 4, true), "3 fuses in the box: the Woodchipper room opens");
    Check(H2(A).talentCode == "127", "the foreman's code works out to " + H2(A).talentCode + " (12 skeletons on the night shift, 7 saw blades)");
    Enter2(A, 4, "1270"); Sim.Run(0.3f);
    Check(H2(A).stage == 4, "a wrong code doesn't start the chipper");
    Sim.Run(1f);
    Enter2(A, 4, H2(A).talentCode); Sim.Run(1f);
    foreach (var c in new[] { A, B }) {
      var h = H2(c);
      Check(h.stage == 5 && DoorsMatch2(c, 5, true) && h.stageShow[2].activeSelf && h.darkMats.All(m => m.GetFloat("_Fullbright") > 0.5f) && Plain(h.lobbyBoard.text).Contains("ESCAPED")
            && All<FirStalker>(c).Where(x => x.name.StartsWith("Cellar Stalker")).All(x => Flat(x.transform.position, x.furnace.position) < 0.5f),
            c.player.displayName + ": YOU ESCAPED THE CELLAR: both trees in the woodchipper, the lights back on");
    }
    Sim.Press(A, Btn2(A, 2, 0)); Sim.Run(1f);
    Check(!H2(B).running && DoorsMatch2(B, 0, false), "RESET: the cellar is open and lit again");
    A.player.pos = new Vector3(0, 0.05f, -1f); B.player.pos = new Vector3(1, 0.05f, -1f);
  }

  // ======================================================================== Boneanza Park: the Station, the rides, the park's people
  static void ParkTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== Boneanza Park");
    var go = All<FirTeleport>(A).First(t => t.name == "BONEANZA PARK");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, go); Sim.Run(0.5f);
    Check(Flat(A.player.pos, new Vector3(90f, 0, 23.5f)) < 0.3f, "the lobby's Station sends Alice to the park gates");
    Check(Sim.BuildLog.Any(l => l.Contains("Boneanza Park: 5 working rides") && l.Contains("30 skeletons, 20 gags, 7 jump scares")),
          Sim.BuildLog.Where(l => l.Contains("Boneanza Park")).Select(l => l.Replace("Fir Warning systems check: ", "")).FirstOrDefault() ?? "no park in the build log");
    var parkRides = new[] { "Big Wheel", "Carousel", "Teacups", "Drop Tower", "Ghost Train" };
    var ridesA = All<FirRide>(A).Where(r => parkRides.Contains(r.name) && (r.rotor == null || r.rotor.position.x < 1000f)).ToList(); var ridesB = All<FirRide>(B).Where(r => parkRides.Contains(r.name) && (r.rotor == null || r.rotor.position.x < 1000f)).ToList();
    Check(ridesA.Count == 5, ridesA.Count + " rides: " + string.Join(", ", ridesA.Select(r => r.name)));
    var wheel = ridesA.First(r => r.name == "Big Wheel"); var wheelB = ridesB.First(r => r.name == "Big Wheel");
    var w0 = wheel.rotor.rotation; Sim.Run(2f);
    float turned = Quaternion.Angle(w0, wheel.rotor.rotation);
    Check(turned > 10f && turned < 14f && Quaternion.Angle(wheel.rotor.rotation, wheelB.rotor.rotation) < 0.5f, "the big wheel turns (" + turned.ToString("F1") + " degrees in 2 s), in step for Alice and Bob");
    Check(wheel.upright.All(u => Quaternion.Angle(u.rotation, Quaternion.identity) < 1f), "...and its gondolas stay level");
    foreach (var r in ridesA.Where(r => r.kind == 0 && r != wheel)) {
      var q = r.rotor.rotation; Sim.Run(1f);
      Check(Quaternion.Angle(q, r.rotor.rotation) > 5f && (r.bobbers.Length == 0 || r.bobbers.Any(b => Mathf.Abs(b.localPosition.y - 0.6f) > 0.05f)) && (r.spinners.Length == 0 || r.spinners.Length == 4), r.name + " turns" + (r.bobbers.Length > 0 ? ", horses bobbing" : "") + (r.spinners.Length > 0 ? ", cups spinning" : ""));
    }
    // ride the big wheel
    var seats = All<FirSeat>(A).Where(x => x.name.StartsWith("Wheel seat")).ToList();
    var low = seats.OrderBy(x => x.transform.position.y).First();
    Sim.Press(A, low);
    Check(Sim.Seated(A.player), "Alice sits in the lowest gondola (" + low.name + ")");
    float y0 = A.player.pos.y; Sim.Run(15f);
    var lowB = All<FirSeat>(B).First(x => x.name == low.name);
    Check(A.player.pos.y > y0 + 3f && Vector3.Distance(A.player.pos, low.transform.position) < 0.05f && Vector3.Distance(lowB.transform.position, A.player.pos) < 0.3f,
          "15 s later she's " + (A.player.pos.y - y0).ToString("F1") + " m up, carried by the wheel; Bob sees her seat in the same place");
    Sim.With(A, () => low.station.ExitStation(A.player));
    A.player.pos = new Vector3(90f, 0.05f, 30f);
    // the drop tower
    var drop = ridesA.First(r => r.kind == 2);
    float hi = 0f, lo = 99f; Sim.Events.Clear();
    for (int f = 0; f < 22 * 20; f++) { Sim.Step(); float y = drop.rotor.localPosition.y; hi = Mathf.Max(hi, y); lo = Mathf.Min(lo, y); }
    Check(hi > 12f && lo < 0.5f && Sim.Events.Any(e => e.Contains("scream1")), "the drop tower climbs to " + hi.ToString("F1") + " m, drops (with a scream) and rests at the bottom");
    // the ghost train
    var cartSeat = All<FirSeat>(A).First(x => x.name.StartsWith("Cart seat"));
    Sim.Press(A, cartSeat); Sim.Events.Clear();
    float minX = 999f;
    for (int f = 0; f < 45 * 20; f++) { Sim.Step(); minX = Mathf.Min(minX, A.player.pos.x); }
    int screams = Sim.Events.Count(e => e.StartsWith("Alice sound") && e.Contains("scream"));
    Check(minX < 66f && screams >= 3, "Alice rides the ghost train deep into the shed (x " + minX.ToString("F0") + ") and " + screams + " things jump out at her");
    Sim.With(A, () => cartSeat.station.ExitStation(A.player));
    // the park's people stay on their paths
    var pw = All<FirWanderer>(A).Where(w => w.transform.position.x > 59f && w.transform.position.x < 121f).ToList();
    Check(pw.Count == 8, pw.Count + " wanderers in the park");
    int bad = 0; string what = "";
    var last = pw.Select(w => w.transform.position).ToList();
    for (int k = 0; k < 120; k++) {
      Sim.Run(0.5f);
      Sim.Current = A; Physics.SyncTransforms();
      for (int i = 0; i < pw.Count; i++) {
        var p = pw[i].transform.position;
        bool ok = p.x > 60.6f && p.x < 119.4f && p.z > 20.6f && p.z < 79.4f && Flat(p, new Vector3(92f, 0, 50f)) > 4.4f && Flat(p, new Vector3(104f, 0, 64f)) > 4.9f
                  && Flat(p, new Vector3(108f, 0, 36f)) > 2.2f && !(p.x > 61.5f && p.x < 80.5f && p.z > 25.5f && p.z < 42.5f);
        RaycastHit hit; var d = p - last[i];
        if (ok && d.magnitude > 0.01f && d.magnitude < 3f && Physics.Raycast(last[i] + Vector3.up * 0.6f, d.normalized, out hit, d.magnitude, 1, QueryTriggerInteraction.Ignore)) { ok = false; what = pw[i].name + " walked through " + hit.collider.name; }
        if (!ok) { bad++; if (what == "") what = pw[i].name + " at " + p; }
        last[i] = p;
      }
      Sim.Current = null;
    }
    Check(bad == 0, "60 seconds of the park's kids, parents and visitors: all on their paths, clear of rides, stalls and the ghost train" + (bad > 0 ? " | " + bad + " problems, e.g. " + what : ""));
    var back = All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 59f && t.transform.position.x < 121f);
    Sim.Press(A, back); Sim.Run(0.5f);
    Check(Flat(A.player.pos, new Vector3(0, 0, -2.6f)) < 0.3f, "the park's way back takes Alice home to the lobby");
  }

  // ======================================================================== Precinct 206
  static void PrecinctTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== Precinct 206");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "PRECINCT 206")); Sim.Run(0.5f);
    Check(Flat(A.player.pos, new Vector3(158f, 0, 21.5f)) < 0.3f, "the Station sends Alice to the precinct's front door");
    Check(Sim.BuildLog.Any(l => l.Contains("Precinct 206: 14 skeleton cops, 35 gags")), "14 skeleton cops and 35 deadpan gags");
    var route = new[] { new Vector3(158f, 0, 22.5f), new Vector3(151.3f, 0, 23.5f), new Vector3(151.3f, 0, 29f), new Vector3(158f, 0, 30f), new Vector3(158f, 0, 44.5f),
                        new Vector3(147.2f, 0, 44.5f), new Vector3(147.2f, 0, 48f), new Vector3(147.2f, 0, 44.5f), new Vector3(170f, 0, 44.5f), new Vector3(170f, 0, 48.5f) };
    int blocked = 0;
    Sim.Current = A; Physics.SyncTransforms();
    for (int i = 0; i + 1 < route.Length; i++)
      foreach (float h in new[] { 0.4f, 1.2f, 1.9f }) {
        var d = route[i + 1] - route[i]; RaycastHit hit;
        if (Physics.Raycast(route[i] + new Vector3(0, h, 0), d.normalized, out hit, d.magnitude, 1, QueryTriggerInteraction.Ignore)) { blocked++; Console.WriteLine("    precinct leg " + i + " blocked by " + hit.collider.name + " at " + hit.point); break; }
      }
    Sim.Current = null;
    Check(blocked == 0, "walked round the front desk, up the bullpen, into the interrogation room and the chief's office");
    var cw = All<FirWanderer>(A).Where(w => w.transform.position.x > 139f && w.transform.position.x < 177f).ToList();
    Check(cw.Count == 4, cw.Count + " cops on the move: Radius tailing Ulna, and Vertebra in hot pursuit of Pelvis");
    int bad = 0; string what = ""; float gap = 99f, gapMax = 0f;
    var radius = cw.First(w => w.name.StartsWith("Radius")); var ulna = cw.First(w => w.name.StartsWith("Ulna"));
    var last = cw.Select(w => w.transform.position).ToList();
    for (int k = 0; k < 80; k++) {
      Sim.Run(0.5f);
      gap = Mathf.Min(gap, Flat(radius.transform.position, ulna.transform.position)); gapMax = Mathf.Max(gapMax, Flat(radius.transform.position, ulna.transform.position));
      Sim.Current = A; Physics.SyncTransforms();
      for (int i = 0; i < cw.Count; i++) {
        var p = cw[i].transform.position; RaycastHit hit; var d = p - last[i];
        bool ok = p.x > 140.5f && p.x < 175.5f && p.z > 20.5f && p.z < 55.5f;
        if (ok && d.magnitude > 0.01f && d.magnitude < 3f && Physics.Raycast(last[i] + Vector3.up * 0.6f, d.normalized, out hit, d.magnitude, 1, QueryTriggerInteraction.Ignore)) { ok = false; what = cw[i].name + " walked through " + hit.collider.name; }
        if (!ok) { bad++; if (what == "") what = cw[i].name + " at " + p; }
        last[i] = p;
      }
      Sim.Current = null;
    }
    Check(bad == 0, "40 seconds of tailing and pursuing: nobody walks through a desk or a wall" + (bad > 0 ? " | " + bad + " problems, e.g. " + what : ""));
    Check(gap > 1.2f && gapMax < 3.5f, "Radius keeps a suspicious " + gap.ToString("F1") + " to " + gapMax.ToString("F1") + " m behind Ulna");
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 139f && t.transform.position.x < 177f)); Sim.Run(0.5f);
    Check(Flat(A.player.pos, new Vector3(0, 0, -2.6f)) < 0.3f, "the precinct's way back goes to the lobby");
  }

  // ======================================================================== Pine Pete's Pizza Palace: the night shift
  static FirNight N(Sim.Client c) { return All<FirNight>(c)[0]; }
  static FirHuntButton NB(Sim.Client c, int act, int val) { var n = N(c); return All<FirHuntButton>(c).First(b => b.action == act && b.value == val && b.hunt != null && b.hunt.program == n); }

  static void NightTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== Pine Pete's Pizza Palace");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "PINE PETE'S PIZZA")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(220f, 0, 22f)) < 0.3f, "the Station sends Alice to the pizzeria");
    Check(Sim.BuildLog.Any(l => l.Contains("Pine Pete's Pizza Palace: 3 animatronics") && l.Contains("12 gags")), Sim.BuildLog.Where(l => l.Contains("Pine Pete's")).Select(l => l.Replace("Fir Warning systems check: ", "")).FirstOrDefault() ?? "no pizzeria");
    var route = new[] { new Vector3(220f, 0, 22.5f), new Vector3(220f, 0, 34f), new Vector3(216.5f, 0, 35f), new Vector3(216.5f, 0, 41.3f), new Vector3(216.5f, 0, 35f), new Vector3(205.2f, 0, 35f),
                        new Vector3(205.2f, 0, 44f), new Vector3(205.2f, 0, 54.2f), new Vector3(220f, 0, 54.2f), new Vector3(235f, 0, 54.2f), new Vector3(235f, 0, 44f), new Vector3(234.8f, 0, 35f) };
    int blocked = 0;
    Sim.Current = A; Physics.SyncTransforms();
    for (int i = 0; i + 1 < route.Length; i++)
      foreach (float h in new[] { 0.4f, 1.2f, 1.9f }) {
        var d = route[i + 1] - route[i]; RaycastHit hit;
        if (Physics.Raycast(route[i] + new Vector3(0, h, 0), d.normalized, out hit, d.magnitude, 1, QueryTriggerInteraction.Ignore)) { blocked++; Console.WriteLine("    pizzeria leg " + i + " blocked by " + hit.collider.name + " at " + hit.point); break; }
      }
    Sim.Current = null;
    Check(blocked == 0, "walked from the front door past the stage, through the parts room and the west hall, through the office and back down the east side");

    // the guards go to the office; a shorter night for the test
    var office = new Vector3(219.2f, 0.05f, 54.3f);
    A.player.pos = office; B.player.pos = office + new Vector3(1.4f, 0, 0);
    foreach (var c in new[] { A, B }) N(c).hourSeconds = 20f;
    Sim.Run(1f);
    Check(!N(A).running && Plain(N(A).statusText.text).Contains("NIGHT SHIFT"), "the office board waits for START");
    Sim.Press(B, NB(B, 10, 0)); Sim.Events.Clear(); Sim.Run(3f);
    Check(N(B).phone.isPlaying && Plain(N(B).captionText.text).Contains("Hello"), "the voicemail plays, with captions: \"" + Plain(N(B).captionText.text) + "\"");

    Console.WriteLine("\n== Night 1: a sensible guard");
    Sim.Press(A, NB(A, 1, 0)); Sim.Run(1f);
    Check(N(A).running && N(B).running && Plain(N(B).clockText.text).Contains("12 AM") && N(B).power == 1000 - 1 * 1 || N(B).power > 980, "the night starts for both: 12 AM, power " + (N(B).power / 10) + "%");
    int sentBack = 0, maxPos = 0, camSeen = 0; int lastP = 0, lastH = 0, lastS = 0;
    for (int k = 0; k < 125 * 4 && N(A).outcome == 0; k++) {
      Sim.Run(0.25f);
      var n = N(A);
      bool west = n.posPete == 4 || (n.posSprucey == 4 && n.sideSprucey == 0), east = n.posHolly == 4 || (n.posSprucey == 4 && n.sideSprucey == 1);
      if (west != n.doorL) { Sim.Press(A, NB(A, 7, 0)); Sim.Step(); }
      if (east != n.doorR) { Sim.Press(A, NB(A, 7, 1)); Sim.Step(); }
      if ((lastP == 4 && n.posPete == 0) || (lastH == 4 && n.posHolly == 0) || (lastS == 4 && n.posSprucey == 0)) sentBack++;
      lastP = n.posPete; lastH = n.posHolly; lastS = n.posSprucey;
      maxPos = Mathf.Max(maxPos, Mathf.Max(n.posPete, Mathf.Max(n.posHolly, n.posSprucey)));
      var nb = N(B);
      if (camSeen == 0 && nb.posPete >= 1 && nb.posPete <= 3) {   // Bob checks his own copy's idea of where Pete is
        int cam = nb.posPete == 1 ? 2 : nb.posPete == 2 ? 3 : 5;
        Sim.Press(B, NB(B, 9, cam)); Sim.Step();
        if (Plain(N(B).monitorText.text).Contains("PINE PETE")) camSeen = 1;
      }
    }
    Check(camSeen == 1, "Bob's cameras show Pine Pete where he is");
    Sim.Run(1.5f);
    Check(N(A).outcome == 9 && N(B).outcome == 9 && N(A).power > 0, "6 AM: they survived (power left: " + (N(A).power / 10) + "%; outcome " + N(A).outcome + "/" + N(B).outcome + ", hour " + N(A).Hour() + ")");
    Check(maxPos == 4 && sentBack >= 1, "the trees made it to the doors, and were sent back " + sentBack + " times by a shut door");
    Check(Plain(N(B).statusText.text).Contains("SURVIVED") && Sim.Events.Any(e => e.Contains("music_box")), "the 6 AM chime, and the board says so");

    Console.WriteLine("\n== Night 2: nobody touches anything");
    B.player.pos = new Vector3(220f, 0.05f, 30f);   // Bob wanders off to the dining room
    Sim.Press(A, NB(A, 1, 0)); Sim.Run(0.5f);
    bool aFace = false, bFace = false;
    for (int k = 0; k < 125 * 20 && N(A).outcome == 0; k++) Sim.Step();
    for (int k = 0; k < 40; k++) { Sim.Step(); aFace |= N(A).scareFaces.Any(f => f.gameObject.activeSelf); bFace |= N(B).scareFaces.Any(f => f.gameObject.activeSelf); }
    int got = N(A).outcome;
    Check(got >= 1 && got <= 3 && N(B).outcome == got, (got == 1 ? "Pine Pete" : got == 2 ? "Holly" : got == 3 ? "Sprucey" : "nobody (" + got + ")") + " gets in through an open door before 6 AM");
    Check(aFace && !bFace, "Alice, in the office, gets the face; Bob, out in the dining room, only hears it");
    Sim.Run(2f);

    Console.WriteLine("\n== Night 3: both doors shut all night");
    foreach (var c in new[] { A, B }) N(c).hourSeconds = 45f;
    B.player.pos = office + new Vector3(1.4f, 0, 0);
    Sim.Press(A, NB(A, 1, 0)); Sim.Run(0.5f);
    Sim.Press(A, NB(A, 7, 0)); Sim.Step(); Sim.Press(A, NB(A, 7, 1)); Sim.Run(1f);
    Check(N(B).doorL && N(B).doorR, "both doors shut (Bob sees them shut)");
    float outAt = -1f; float t0 = Time.time;
    for (int k = 0; k < 260 * 20 && N(A).outcome == 0; k++) { Sim.Step(); if (outAt < 0 && N(A).power == 0) outAt = Time.time - t0; }
    Check(outAt > 0 && outAt < 230f && !N(B).doorL && !N(B).doorR, "the power runs out after " + outAt.ToString("F0") + " s, and the doors fly open");
    Check(N(A).outcome == 1, "...and in the dark, Pine Pete comes for them (" + N(A).outcome + ")");
    Sim.Press(A, NB(A, 2, 0)); Sim.Run(1f);
    Check(!N(B).running, "RESET: the pizzeria's quiet again");
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 199f)); Sim.Run(0.5f);
    B.player.pos = new Vector3(1, 0.05f, -1f);
  }

  // ======================================================================== Pixel Hollow
  static FirHuntButton EB(Sim.Client c, UdonSharpBehaviour owner, int act, int val) { return All<FirHuntButton>(c).First(b => b.action == act && b.value == val && b.hunt != null && b.hunt.program == owner); }
  static bool Blocked(Sim.Client c, Vector3[] route, out string what) {
    what = ""; Sim.Current = c; Physics.SyncTransforms();
    try {
      for (int i = 0; i + 1 < route.Length; i++)
        foreach (float h in new[] { 0.4f, 1.2f, 1.9f }) {
          var d = route[i + 1] - route[i]; RaycastHit hit;
          var o = route[i] + new Vector3(0, h, 0); float left = d.magnitude;
          // people walking about (residents, wanderers) move out of the way: look past them
          while (left > 0f && Physics.Raycast(o, d.normalized, out hit, left, 1, QueryTriggerInteraction.Ignore)) {
            if (hit.collider.GetComponentInParent<FirResident>() != null || hit.collider.GetComponentInParent<FirWanderer>() != null) { float step = hit.distance + 0.05f; o += d.normalized * step; left -= step; continue; }
            what = hit.collider.name + " at " + hit.point; return true;
          }
        }
      return false;
    } finally { Sim.Current = null; }
  }

  static void PixelTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== Pixel Hollow");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "PIXEL HOLLOW")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(280f, 0, 22f)) < 0.3f, "the Station sends Alice to Pixel Hollow");
    Check(Sim.BuildLog.Any(l => l.Contains("Pixel Hollow: 3 encounters, a tile gate, 6 skeletons") && l.Contains("10 gags")), "3 encounters, a tile gate, 6 skeletons, 10 gags, a save point and a chest");
    var path = new[] { new Vector3(280f, 0, 22.5f), new Vector3(280f, 0, 82f) };
    string what;
    Check(Blocked(A, path, out what) && what.StartsWith("Blocker"), "the first monster blocks the path (" + what + ")");
    var encA = All<FirEncounter>(A).OrderBy(e => e.blocker.transform.position.z).ToList();
    var encB = All<FirEncounter>(B).OrderBy(e => e.blocker.transform.position.z).ToList();
    var e1 = encA[0];
    Sim.Run(2f);
    Check(Plain(e1.dialog.text).Contains("SHY PINECONE blocks the way"), "its dialog box types out: \"" + Plain(e1.dialog.text) + "\"");
    Sim.Press(A, EB(A, e1, 24, 0)); Sim.Run(0.3f);
    Check(!e1.IsFriend(), "PARDON too soon: \"" + Plain(e1.dialog.text).Substring(0, Math.Min(40, Plain(e1.dialog.text).Length)) + "...\"");
    int wrong = (e1.correct[0] + 1) % 3;
    Sim.Press(A, EB(A, e1, 22, wrong)); Sim.Run(2f);
    Check(Plain(e1.meter.text).Contains("FRIENDSHIP") && Plain(e1.optionLabels[0].text) == e1.options[0], "a wrong thing to say doesn't move it on: \"" + Plain(e1.dialog.text) + "\"");
    Sim.Press(A, EB(A, e1, 22, e1.correct[0])); Sim.Run(0.3f);
    Sim.Press(A, EB(A, e1, 21, 0)); Sim.Run(0.3f);
    Check(Plain(e1.optionLabels[0].text) == e1.options[0], "BONK undoes it (back to round one)");
    foreach (var enc in encA) {
      for (int r = 0; r < enc.correct.Length; r++) { Sim.Press(A, EB(A, enc, 22, enc.correct[r])); Sim.Run(0.3f); }
      Sim.Press(A, EB(A, enc, 23, 0)); Sim.Run(0.2f);
      Sim.Press(A, EB(A, enc, 24, 0)); Sim.Run(0.5f);
    }
    Check(encA.All(e => e.IsFriend() && !e.blocker.activeSelf), "Alice says the right things to all three, and PARDONs them: friends, and out of the way");
    Sim.Run(2f);
    Check(encA.All(e => Vector3.Distance(e.monster.localPosition, e.blocker.transform.localPosition + e.stepAside - new Vector3(0, 1.5f, 0)) < 0.2f), "...and each one steps aside");
    Check(encB.All(e => !e.IsFriend() && e.blocker.activeSelf), "Bob's monsters still block his way (each player has their own adventure)");
    var tiles = All<FirTiles>(A)[0];
    Check(!tiles.IsSolved() && tiles.gate.activeSelf, "the tile gate starts shut");
    foreach (int i in new[] { 0, 4, 8 }) { Sim.Press(A, EB(A, tiles, 31, i)); Sim.Step(); }
    Check(tiles.IsSolved() && !tiles.gate.activeSelf && All<FirTiles>(B)[0].gate.activeSelf, "tapping the right tiles (0, 4, 8) lights them all: Alice's gate opens (Bob's stays shut)");
    Check(!Blocked(A, path, out what), "with everyone befriended and the gate open, Alice walks the whole path to the castle" + (what != "" ? " | " + what : ""));
    var says = All<FirSay>(A).Where(x => x.transform.position.x > 259f && x.transform.position.x < 301f).ToList();
    foreach (var sy in says) Sim.Press(A, sy);
    Sim.Run(1f);
    Check(says.Count == 2 && says.All(sy => Plain(sy.caption.text).Length > 10), "the save point and the chest speak, with captions: \"" + Plain(says[0].caption.text) + "\"");
    var quest = All<FirSkeletonCombo>(A).FirstOrDefault(c => c.title == "The Quest");
    Check(quest != null && quest.lines.Length == 5, "Sir Rattleton and Captain Lumen have their act: The Quest");
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 259f && t.transform.position.x < 301f)); Sim.Run(0.5f);
  }

  // ======================================================================== the Test Annex and Blocklands
  static void LabTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The Test Annex");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Events.Clear();
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "TEST ANNEX")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(338f, 0, 22f)) < 0.3f, "the Station sends Alice to the Test Annex");
    Check(Sim.BuildLog.Any(l => l.Contains("Test Annex: 3 tests, 2 portal pairs")) && Sim.BuildLog.Any(l => l.Contains("8 gags in the Test Annex and Blocklands")), "3 tests, 2 portal pairs, 2 crates, 7 announcements, 8 gags");
    A.player.pos = new Vector3(338f, 0.05f, 24f); Sim.Run(1f);
    Check(Sim.Events.Any(e => e.Contains("lb_proctor0")), "walking in, PROCTOR welcomes Alice (voiced, with a caption)");
    string what;
    Check(Blocked(A, new[] { new Vector3(338f, 0, 27f), new Vector3(338f, 0, 36f) }, out what), "test 1: the way on is a solid wall (" + what + ")");
    A.player.pos = new Vector3(330f, 0.05f, 31.2f); Sim.Run(0.3f);
    Check(Flat(A.player.pos, new Vector3(346f, 0, 35.8f)) < 0.2f, "stepping into the purple portal puts her out of the green one, over the wall");
    Check(!Blocked(A, new[] { new Vector3(346f, 0, 35.8f), new Vector3(338f, 0, 38.5f), new Vector3(338f, 0, 42f) }, out what), "on through to test 2" + (what != "" ? " | " + what : ""));
    Check(Blocked(A, new[] { new Vector3(338f, 0, 47f), new Vector3(338f, 0, 52f) }, out what) && what.StartsWith("Door 2"), "test 2's door is shut");
    var crate1 = All<VRC.SDK3.Components.VRCPickup>(A).First(p => p.name == "Test crate 1").transform;
    Sim.With(A, () => VRC.SDKBase.Networking.SetOwner(A.player, crate1.gameObject));
    crate1.position = new Vector3(346f, 0.35f, 45f); Sim.Run(0.5f);
    var padsA = All<FirPad>(A); var padsB = All<FirPad>(B);
    Check(padsA[0].IsDown() && padsB[0].IsDown() && !padsA[0].door.activeSelf && !padsB[0].door.activeSelf, "Alice sets the crate on the button: the door opens, for Bob too");
    crate1.position = new Vector3(340f, 0.35f, 45f); Sim.Run(0.5f);
    Check(!padsB[0].IsDown() && padsB[0].door.activeSelf, "take it off, and the door shuts again");
    Sim.Press(A, All<FirHuntButton>(A).First(b => b.action == 51 && b.transform.position.z < 45f)); Sim.Run(0.5f);
    Check(Vector3.Distance(crate1.position, new Vector3(330f, 0.35f, 44f)) < 0.05f, "RESPAWN CRATE sends it home");
    crate1.position = new Vector3(346f, 0.35f, 45f); Sim.Run(0.5f);
    A.player.pos = new Vector3(338f, 0.05f, 52f);
    A.player.pos = new Vector3(340.5f, 0.05f, 55.5f); Sim.Run(0.3f);
    Check(Flat(A.player.pos, new Vector3(351.2f, 0, 57.5f)) < 0.2f, "test 3: the portal by the glass takes her to the crate's side");
    var crate2 = All<VRC.SDK3.Components.VRCPickup>(A).First(p => p.name == "Test crate 2").transform;
    Sim.With(A, () => VRC.SDKBase.Networking.SetOwner(A.player, crate2.gameObject));
    Sim.Run(1.5f);   // (a portal waits a moment before it'll take you again)
    A.player.pos = new Vector3(350f, 0.05f, 57.5f); Sim.Run(0.3f);
    Check(Flat(A.player.pos, new Vector3(339.3f, 0, 55.5f)) < 0.2f, "...and back again (carrying the crate)");
    crate2.position = new Vector3(330f, 0.35f, 57f); Sim.Run(0.5f);
    Check(padsB[1].IsDown() && !padsB[1].door.activeSelf && padsB[1].showWhenDown[0].activeSelf, "the crate on the last button: the final door opens and TESTING COMPLETE lights up (for Bob too)");

    Console.WriteLine("\n== Blocklands");
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 319f && t.transform.position.x < 357f && t.transform.position.z < 21f)); Sim.Run(0.3f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "BLOCKLANDS")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(400f, 0, 22f)) < 0.3f, "the Station sends Alice to Blocklands");
    var va = All<FirVoxel>(A)[0]; var vb = All<FirVoxel>(B)[0];
    Check(va.blocks.Length == 48 && va.slotSolid.Length == 24, "48 blocks in the quarry, 24 places to build");
    Func<Sim.Client, int, int, FirHuntButton> VB = (c, act, val) => { var v = All<FirVoxel>(c)[0]; return All<FirHuntButton>(c).First(b => b.action == act && b.value == val && b.hunt != null && b.hunt.program == v); };
    foreach (int i in new[] { 0, 9, 20 }) { Sim.Press(A, VB(A, 41, i)); Sim.Run(0.2f); }
    Sim.Run(0.5f);
    Check(va.Pocket() == 3 && vb.Pocket() == 0 && !va.blocks[9].activeSelf && !vb.blocks[9].activeSelf, "Alice mines 3 blocks: gone for both of them, 3 in her pocket");
    Check(!va.slotGhost[12].activeSelf && va.slotGhost[0].activeSelf, "no ghost to press in mid-air: the top layer only appears over a built block");
    Sim.Press(A, VB(A, 42, 0)); Sim.Run(0.3f); Sim.Press(A, VB(A, 42, 12)); Sim.Run(0.5f);
    Check(va.Pocket() == 1 && vb.slotSolid[0].activeSelf && vb.slotSolid[12].activeSelf && !vb.slotGhost[0].activeSelf, "she builds a block, then one on top of it: Bob sees both");
    Sim.Press(B, VB(B, 42, 1)); Sim.Run(0.5f);
    Check(!va.slotSolid[1].activeSelf, "Bob's pocket is empty, so he can't build until he mines");
    Sim.Press(B, VB(B, 43, 0)); Sim.Run(0.5f);
    Check(va.blocks.All(b => b.activeSelf) && va.slotSolid.All(b => !b.activeSelf), "RESET WORLD puts every block back");
    Check(All<FirRainbowSkeleton>(A).Count(r => r.voice.clip != null && r.voice.clip.name.StartsWith("lb_")) == 4, "4 block-headed villagers");
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 379f && t.transform.position.x < 421f)); Sim.Run(0.5f);
  }

  // ======================================================================== the Cold North
  static void NorthTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The Cold North");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "THE COLD NORTH")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(447f, 0, 21.8f)) < 0.3f, "the Station sends Alice to the Cold North");
    Func<string, FirGag> G = n => All<FirGag>(A).First(x => x.name == n);
    var tests = new[] {
      new { n = "The Shout", what = "the tree flies off", ok = (Func<FirGag, Vector3, bool>)((x, home) => Vector3.Distance(x.target.position, home) > 8f) },
      new { n = "The Giant", what = "the skeleton is launched into the sky", ok = (Func<FirGag, Vector3, bool>)((x, home) => x.target.position.y - home.y > 15f) },
      new { n = "The Cheese", what = "the cheese wheels roll down the mountain", ok = (Func<FirGag, Vector3, bool>)((x, home) => home.z - x.target.position.z > 5f) },
      new { n = "The Horse", what = "the horse walks up the cliff", ok = (Func<FirGag, Vector3, bool>)((x, home) => x.target.position.y - home.y > 5f) } };
    foreach (var tc in tests) {
      var gag = G(tc.n); var home = gag.target.position; Sim.Events.Clear();
      Sim.Press(A, gag);
      bool did = false, cap = false;
      for (int f = 0; f < 80; f++) { Sim.Step(); if (tc.ok(gag, home)) did = true; if (gag.bubbleObject.activeSelf && gag.bubble.text == gag.line) cap = true; }
      bool voiced = Sim.Events.Any(e => e.Contains(gag.voice.clip.name));
      for (int f = 0; f < 200 && Vector3.Distance(gag.target.position, home) > 0.01f; f++) Sim.Step();
      Check(did && cap && voiced && Vector3.Distance(gag.target.position, home) < 0.01f, tc.n + ": " + tc.what + " (voiced, captioned), then it's all put back");
    }
    var seat = All<FirSeat>(A).First(x => x.name.StartsWith("Cart seat") && x.transform.position.x > 439f);
    Sim.Press(A, seat); Sim.Step(); var p0 = A.player.pos; Sim.Run(10f);
    Check(Sim.Seated(A.player) && Flat(A.player.pos, p0) > 8f, "Alice rides the cart round the road (" + Flat(A.player.pos, p0).ToString("F0") + " m in 10 s), with the passenger who has some news");
    Sim.With(A, () => seat.station.ExitStation(A.player));
    Check(All<FirRainbowSkeleton>(A).Count(r => r.voice.clip != null && r.voice.clip.name.StartsWith("nt_")) == 5, "5 voiced skeletons: the passenger, three guards and the shopkeeper in the bucket");
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 439f && t.transform.position.x < 477f)); Sim.Run(0.5f);
  }

  // ======================================================================== the Mystery Hut
  static string gagSet = "";   // the expansion's wing gags (mo_ clips) and the new rooms' gags (rm_ clips) are counted only by their own tests
  static int RunGags(Sim.Client B, Func<FirGag, bool> keep, out int gagN, List<string> gagBad) {
  var gf = typeof(FirGag).GetField("running", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
  int gagOk = 0; gagN = 0;
  foreach (var gag in All<FirGag>(B)) {
    string cn = gag.voice != null && gag.voice.clip != null ? gag.voice.clip.name : "";
    if ((cn.StartsWith("mo_") ? "mo" : cn.StartsWith("rm_") ? "rm" : "") != gagSet) continue;
    if (!keep(gag) || gag.scare || gag.bubble == null || gag.name.StartsWith("Proctor")) continue;   // jump scares (no line), and PROCTOR's announcements (a voice, nothing moves)
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
    return gagOk;
  }

  static void SetDayHour(float h) {
    int day = 720000;
    int now = VRC.SDKBase.Networking.GetServerTimeInMilliseconds();
    int want = (int)(h / 24f * day);
    int cur = ((now % day) + day) % day;
    VRC.SDKBase.Networking.ServerOffsetMs += ((want - cur) % day + day) % day;
  }

  // ======================================================================== the expansion: a new wing for every Station area
  static void ExpansionTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The expansion");
    var line = Sim.BuildLog.FirstOrDefault(l => l.Contains("The expansion:"));
    Check(line != null, line != null ? Plain(line) : "no expansion line in the build log");
    Check(!Sim.BuildLog.Any(x => x.Contains("problem")), "the builder reported no problems" + string.Concat(Sim.BuildLog.Where(x => x.Contains("problem")).Distinct().Take(5).Select(x => " | " + x)));
    var wings = FirCheckBuilder.wings;
    // no wing overlaps another area or another wing
    var floors = new List<KeyValuePair<string, float[]>>();
    var root = All<FirTeleport>(A).First().transform; while (root.parent != null) root = root.parent;
    for (int ci = 0; ci < root.childCount; ci++) { var area = root.GetChild(ci);
      var fl = area.Find("Floor"); if (fl == null) continue;
      var bc = fl.GetComponent<BoxCollider>(); if (bc == null) continue;
      var c = fl.position; floors.Add(new KeyValuePair<string, float[]>(area.name, new[] { c.x - bc.size.x / 2f, c.z - bc.size.z / 2f, c.x + bc.size.x / 2f, c.z + bc.size.z / 2f }));
    }
    var clash = new List<string>();
    Func<float[], float[], bool> overlap = (a, b) => a[0] < b[2] - 0.05f && b[0] < a[2] - 0.05f && a[1] < b[3] - 0.05f && b[1] < a[3] - 0.05f;
    for (int i = 0; i < wings.Count; i++) {
      foreach (var f in floors) if (overlap(wings[i].Value, f.Value)) clash.Add(wings[i].Key + "'s wing / " + f.Key);
      for (int j = i + 1; j < wings.Count; j++) if (overlap(wings[i].Value, wings[j].Value)) clash.Add(wings[i].Key + " / " + wings[j].Key);
    }
    Check(clash.Count == 0, wings.Count + " wings, none overlapping another area or wing" + (clash.Count > 0 ? " | " + string.Join(", ", clash.Take(5)) : ""));
    // every wing: the shortcut from the area's entrance, the walk back out through the doorway, the way home
    int ok = 0; var bad = new List<string>();
    foreach (var wn in FirCheckBuilder.wingNames.Select((n, i) => new { n, w = wings[i] })) {
      var b = wn.w.Value; float cx = (b[0] + b[2]) / 2f, zw = b[1], dx = b[4];
      var sc = All<FirTeleport>(A).FirstOrDefault(t => t.name == "To " + wn.n);
      if (sc == null) { bad.Add(wn.n + " (no shortcut)"); continue; }
      A.player.pos = new Vector3(cx, 0.05f, 21f);
      Sim.Press(A, sc); Sim.Run(0.5f);
      bool there = Flat(A.player.pos, new Vector3(dx, 0, zw + 2.2f)) < 0.4f;
      string what, what2;
      bool door = !Blocked(A, new[] { new Vector3(dx, 0, zw + 2.2f), new Vector3(dx, 0, zw - 2.5f) }, out what);
      bool inside = !Blocked(A, new[] { new Vector3(dx, 0, zw + 2.2f), new Vector3(dx, 0, zw + 5f) }, out what2);
      var home = All<FirTeleport>(A).FirstOrDefault(t => t.name == "Back to the entrance" && Mathf.Abs(t.transform.position.z - (zw + 0.35f)) < 0.1f && Mathf.Abs(t.transform.position.x - (cx + 3.4f)) < 0.1f);
      bool back = false;
      if (home != null) { Sim.Press(A, home); Sim.Run(0.5f); back = Flat(A.player.pos, new Vector3(b[5], 0, b[6])) < 0.4f; }
      if (there && door && inside && back) ok++; else bad.Add(wn.n + (there ? "" : " (shortcut missed)") + (door ? "" : " (doorway blocked: " + what + ")") + (inside ? "" : " (blocked inside: " + what2 + ")") + (back ? "" : " (no way back)"));
    }
    Check(ok == wings.Count, ok + " of " + wings.Count + " wings: the shortcut takes you in, the doorway walks back out, and a button takes you back to the entrance" + (bad.Count > 0 ? " | " + string.Join(", ", bad.Take(6)) : ""));
    // the new people: every one voiced and captioned
    var folk = All<FirRainbowSkeleton>(A).Where(k => k.voice != null && k.voice.clip != null && k.voice.clip.name.StartsWith("mo_")).ToList();
    Check(folk.Count == wings.Count * 3 && folk.All(k => !string.IsNullOrEmpty(k.caption)), folk.Count + " new people, all voiced and captioned (3 a wing)");
    // the new gags: each one moves, speaks and shows its caption
    int gagN; var gagBad = new List<string>();
    gagSet = "mo";
    int gagOk = RunGags(B, g => true, out gagN, gagBad);
    gagSet = "";
    Check(gagN == wings.Count * 3 && gagOk == gagN, gagOk + " of " + gagN + " new gags (3 a wing) move, speak and show their caption" + (gagBad.Count > 0 ? " | " + string.Join(", ", gagBad.Take(6)) : ""));
    int boards = All<FirBoost>(A).Count(f => wings.Any(w => f.transform.position.x > w.Value[0] && f.transform.position.x < w.Value[2] && f.transform.position.z > w.Value[1] && f.transform.position.z < w.Value[3]));
    Check(boards >= 3, boards + " wings have something to collect");
  }

  // ======================================================================== the new rooms, and their rides
  static void RoomsTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The new rooms");
    var line = Sim.BuildLog.FirstOrDefault(l => l.Contains("The new rooms:"));
    Check(line != null && line.Contains("15 more Station stops"), line != null ? Plain(line) : "no rooms line in the build log");
    Check(!Sim.BuildLog.Any(x => x.Contains("problem")), "the builder reported no problems (coaster tracks clear of themselves)" + string.Concat(Sim.BuildLog.Where(x => x.Contains("problem")).Distinct().Take(5).Select(x => " | " + x)));
    foreach (var l in Sim.BuildLog.Where(l => l.Contains("roller coaster")).Distinct()) Console.WriteLine("        " + Plain(l).Replace("Fir Warning systems check: ", ""));
    var rooms = FirCheckBuilder.rooms;
    // no room overlaps another area, wing or room
    var floors = new List<KeyValuePair<string, float[]>>();
    var root = All<FirTeleport>(A).First().transform; while (root.parent != null) root = root.parent;
    for (int ci = 0; ci < root.childCount; ci++) { var area = root.GetChild(ci);
      if (rooms.Any(r => r.Key == area.name)) continue;
      var fl = area.Find("Floor"); if (fl == null) continue;
      var bc = fl.GetComponent<BoxCollider>(); if (bc == null) continue;
      var c = fl.position; floors.Add(new KeyValuePair<string, float[]>(area.name, new[] { c.x - bc.size.x / 2f, c.z - bc.size.z / 2f, c.x + bc.size.x / 2f, c.z + bc.size.z / 2f }));
    }
    floors.AddRange(FirCheckBuilder.wings);
    var clash = new List<string>();
    Func<float[], float[], bool> overlap = (a, b) => a[0] < b[2] - 0.05f && b[0] < a[2] - 0.05f && a[1] < b[3] - 0.05f && b[1] < a[3] - 0.05f;
    for (int i = 0; i < rooms.Count; i++) {
      foreach (var f in floors) if (overlap(rooms[i].Value, f.Value)) clash.Add(rooms[i].Key + " / " + f.Key);
      for (int j = i + 1; j < rooms.Count; j++) if (overlap(rooms[i].Value, rooms[j].Value)) clash.Add(rooms[i].Key + " / " + rooms[j].Key);
    }
    Check(rooms.Count == 15 && clash.Count == 0, rooms.Count + " rooms, none overlapping anything else (" + floors.Count + " areas and wings checked)" + (clash.Count > 0 ? " | " + string.Join(", ", clash.Take(5)) : ""));
    // the Station takes you to each one; you can walk in from the arrival spot; a button takes you home
    int ok = 0; var bad = new List<string>();
    for (int i = 0; i < rooms.Count; i++) {
      var r = rooms[i].Value; float cx = (r[0] + r[2]) / 2f; string stop = FirCheckBuilder.roomStops[i];
      var go = All<FirTeleport>(A).FirstOrDefault(t => t.name == stop && t.transform.position.x < 100f);
      if (go == null) { bad.Add(stop + " (not on the Station board)"); continue; }
      A.player.pos = new Vector3(-4f, 0.05f, -1f);
      Sim.Press(A, go); Sim.Run(0.5f);
      bool there = Flat(A.player.pos, new Vector3(cx, 0, 22f)) < 0.4f;
      string what;
      bool walk = !Blocked(A, new[] { new Vector3(cx, 0, 22f), new Vector3(cx, 0, 27f) }, out what);
      var home = All<FirTeleport>(A).FirstOrDefault(t => t.name == "Back to the lobby" && t.transform.position.x > r[0] && t.transform.position.x < r[2] && t.transform.position.z < 21f);
      bool back = false;
      if (home != null) { Sim.Press(A, home); Sim.Run(0.5f); back = Flat(A.player.pos, new Vector3(0, 0, -2.6f)) < 0.4f; }
      if (there && walk && back) ok++; else bad.Add(stop + (there ? "" : " (missed: " + A.player.pos + ")") + (walk ? "" : " (blocked: " + what + ")") + (back ? "" : " (no way home)"));
    }
    var board = All<FirTeleport>(A).Where(t => t.transform.parent != null && t.transform.parent.name == "Station").ToList();
    bool tidy = board.All(t => board.All(u => u == t || Vector3.Distance(t.transform.position, u.transform.position) > 0.15f));
    Check(ok == rooms.Count && board.Count >= 57 && tidy, ok + " of " + rooms.Count + " rooms: the Station sends you there, the way in is clear, and a button takes you home; the board has " + board.Count + " stops, none overlapping" + (bad.Count > 0 ? " | " + string.Join(", ", bad.Take(6)) : ""));
    // the people: voiced and captioned
    var folk = All<FirRainbowSkeleton>(A).Where(k => k.voice != null && k.voice.clip != null && k.voice.clip.name.StartsWith("rm_")).ToList();
    Check(folk.Count >= 55 && folk.All(k => !string.IsNullOrEmpty(k.caption)), folk.Count + " new people, all voiced and captioned");
    int gagN; var gagBad = new List<string>();
    gagSet = "rm";
    int gagOk = RunGags(B, g => true, out gagN, gagBad);
    gagSet = "";
    Check(gagN >= 55 && gagOk == gagN, gagOk + " of " + gagN + " new gags move, speak and show their caption" + (gagBad.Count > 0 ? " | " + string.Join(", ", gagBad.Take(6)) : ""));
    // the coasters: ride a whole lap of each
    foreach (var cname in new[] { "Gator Gulch", "The Storm Chaser" }) {
      var ride = All<FirRide>(A).First(x => x.name == cname);
      var rideB = All<FirRide>(B).First(x => x.name == cname);
      var seat = All<FirSeat>(A).First(x => x.name == cname + " seat 0.0a");
      var station = ride.route[0];
      // wait for train 0 to come into the station, then get on
      int wait = 0;
      while (Vector3.Distance(seat.transform.position, station) > 8f || ride.carts[0].position.y > 1.2f) { Sim.Step(); if (++wait > 20 * 200) break; }
      Sim.Press(A, seat);
      bool seated = Sim.Seated(A.player);
      float maxY = 0f, maxV = 0f, maxPitch = 0f, maxRoll = 0f, off = 0f; var last = A.player.pos; bool stayed = true, bobOk = true; float far = 0f;
      for (int f = 0; f < 20 * 150; f++) {
        Sim.Step();
        var p = A.player.pos;
        maxY = Mathf.Max(maxY, p.y); maxV = Mathf.Max(maxV, Vector3.Distance(p, last) / Time.deltaTime); last = p;
        var fw = seat.transform.forward; maxPitch = Mathf.Max(maxPitch, Mathf.Abs(90f - Vector3.Angle(fw, Vector3.up)));
        var rt = seat.transform.rotation * Vector3.right; maxRoll = Mathf.Max(maxRoll, Mathf.Abs(90f - Vector3.Angle(rt, Vector3.up)));
        float dmin = 99f; foreach (var q in ride.route) dmin = Mathf.Min(dmin, Vector3.Distance(q, ride.carts[0].position)); off = Mathf.Max(off, dmin);
        far = Mathf.Max(far, Flat(p, station));
        if (!Sim.Seated(A.player)) stayed = false;
        if (f % 100 == 0 && Vector3.Distance(ride.carts[0].position, rideB.carts[0].position) > 0.5f) bobOk = false;
        if (f > 400 && Vector3.Distance(p, station) < 3f && ride.carts[0].position.y < 1f) break;
      }
      Check(seated && stayed && bobOk && maxY > (cname == "Gator Gulch" ? 13f : 29f) && maxV > (cname == "Gator Gulch" ? 11f : 17f) && maxPitch > 20f && maxRoll > 10f && off < 2.5f,
            "Alice rides " + cname + ": up to " + maxY.ToString("F1") + " m, " + (maxV * 3.6f).ToString("F0") + " km/h at the bottom of the drop, pitched up to " + maxPitch.ToString("F0") + " degrees and banked " + maxRoll.ToString("F0") + " into the turns, " + far.ToString("F0") + " m from the station, on the track all the way; Bob sees the same train" + (stayed ? "" : " | she fell off") + (bobOk ? "" : " | Bob's train is elsewhere"));
      Check(Vector3.Distance(A.player.pos, station) < 3f, "...and she comes back into the station (" + Flat(A.player.pos, station).ToString("F1") + " m away)");
      Sim.With(A, () => seat.station.ExitStation(A.player));
    }
    // the dark rides: things jump out as you ride past
    foreach (var dr in new[] { new { cart = "Grove seat 0a", deep = 61f, room = "Haunted Orange Grove" }, new { cart = "Capsule seat 0a", deep = 93f, room = "Mission: Space Coast" }, new { cart = "Mystery ride seat 0a", deep = 64f, room = "the Mystery Ride" } }) {
      var seat = All<FirSeat>(A).First(x => x.name == dr.cart);
      Sim.Press(A, seat); Sim.Events.Clear();
      float maxZ = 0f;
      for (int f = 0; f < 20 * 75; f++) { Sim.Step(); maxZ = Mathf.Max(maxZ, A.player.pos.z); }
      int screams = Sim.Events.Count(e => e.StartsWith("Alice sound") && (e.Contains("scream") || e.Contains("rm_")));
      Check(Sim.Seated(A.player) && maxZ > dr.deep && screams >= 3, "Alice rides " + dr.room + " to the back of the building (z " + maxZ.ToString("F0") + ") and " + screams + " things happen as she passes");
      Sim.With(A, () => seat.station.ExitStation(A.player));
    }
    // the gentle rides: sit down and get carried
    foreach (var name in new[] { "Swan seat 0a", "Wheel gondola seat 0", "Train seat 0a", "Toon car seat 0a" }) {
      var seat = All<FirSeat>(A).First(x => x.name == name);
      Sim.Press(A, seat); var p0 = A.player.pos; float moved = 0f;
      for (int f = 0; f < 20 * 25; f++) { Sim.Step(); moved = Mathf.Max(moved, Vector3.Distance(A.player.pos, p0)); }
      Check(Sim.Seated(A.player) && moved > 4f, name.Replace(" seat 0a", "").Replace(" seat 0", "") + ": Alice sits down and is carried " + moved.ToString("F0") + " m in 25 s");
      Sim.With(A, () => seat.station.ExitStation(A.player));
    }
    var wheel = All<FirRide>(A).First(r => r.rotor != null && r.rotor.parent != null && r.rotor.parent.name == "Observation Wheel");
    Check(wheel.upright.All(u => Quaternion.Angle(u.rotation, Quaternion.identity) < 1f), "the observation wheel's gondolas stay level");
    int seats = All<VRC.SDK3.Components.VRCStation>(A).Count(st => st.transform.position.x > 2895f);
    Check(seats >= 60, seats + " ride seats in the new rooms");
  }

  // ======================================================================== the remaster: shading, bevels, trees untouched
  static void RemasterTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The remaster");
    var line = Sim.BuildLog.FirstOrDefault(l => l.Contains("Remaster:"));
    Check(line != null, line != null ? line.Replace("Fir Warning systems check: ", "") : "no remaster line in the build log");
    var mats = All<MeshRenderer>(A).Select(r => r.sharedMaterial).Where(m => m != null && m.name != null && m.name.StartsWith("Check_")).Distinct().ToList();
    var treeNames = new[] { "Needles", "Pot", "Stand", "Trunk", "Eye", "Pupil", "Mouth", "Teeth", "HatRed", "HatWhite", "ScareGreen" };
    Func<Material, string> baseName = m => m.name.Split('_').Last();
    var tree = mats.Where(m => treeNames.Contains(baseName(m))).ToList();
    var rest = mats.Where(m => m.floats.ContainsKey("_Fullbright") && !treeNames.Contains(baseName(m))).ToList();
    Check(tree.Count >= 8 && tree.All(m => m.floats.ContainsKey("_Remaster") && m.floats["_Remaster"] == 0f),
          "the talking trees keep their original look (" + tree.Count + " tree materials with the remaster off: " + string.Join(", ", tree.Select(m => baseName(m)).Distinct()) + ")");
    var off = rest.Where(m => !(m.floats.ContainsKey("_Remaster") && m.floats["_Remaster"] == 1f)).ToList();
    Check(rest.Count > 100 && off.Count == 0, rest.Count + " other materials get the remastered shading" + (off.Count > 0 ? " | not: " + string.Join(", ", off.Take(8).Select(m => m.name)) : ""));
    // the bevelled boxes: closed, facing outwards, and exactly the size of their colliders
    var boxes = All<MeshFilter>(A).Where(f => f.sharedMesh != null && f.sharedMesh.name != null && f.sharedMesh.name.StartsWith("box") && f.sharedMesh.verts.Count == 96).Select(f => f.sharedMesh).Distinct().ToList();
    int badWind = 0, badSize = 0;
    foreach (var m in boxes) {
      for (int t = 0; t < m.tris.Count; t += 3) {
        Vector3 a = m.verts[m.tris[t]], b = m.verts[m.tris[t + 1]], c = m.verts[m.tris[t + 2]];
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f) <= 0f) { badWind++; if (badWind == 1) Console.WriteLine("    inward: " + m.name + " " + a + b + c); }
      }
      var parts = m.name.Substring(3).Split('_').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
      float mx = m.verts.Max(q => q.x) * 2f, my = m.verts.Max(q => q.y) * 2f, mz = m.verts.Max(q => q.z) * 2f;
      if (Mathf.Abs(mx - parts[0]) > 0.002f || Mathf.Abs(my - parts[1]) > 0.002f || Mathf.Abs(mz - parts[2]) > 0.002f) { badSize++; if (badSize == 1) Console.WriteLine("    size: " + m.name + " " + mx + " " + my + " " + mz); }
    }
    Check(boxes.Count > 300 && badWind == 0 && badSize == 0, boxes.Count + " bevelled box shapes, every face pointing outwards and every one the size of its collider" + (badWind + badSize > 0 ? " | " + badWind + " inward faces, " + badSize + " wrong sizes" : ""));
    var walls = All<MeshFilter>(A).Count(f => f.sharedMesh != null && f.sharedMesh.name != null && f.sharedMesh.name.StartsWith("box") && f.sharedMesh.verts.Count == 24);
    Check(walls > 100, walls + " big walls and floors stay plain boxes (cheap on Quest)");
  }

  static void ChrisTrippTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The Chris and Tripp Show");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "CHRIS AND TRIPP")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(2840f, 0, 22f)) < 0.3f, "the Station sends Alice to the Chris and Tripp Show");
    var acts = All<FirSkeletonCombo>(A).Where(c => c.title.StartsWith("SCENE ")).OrderBy(c => c.title).ToList();
    Check(acts.Count == 5 && acts.Sum(c => c.lines.Length) == 21, acts.Count + " scenes, " + acts.Sum(c => c.lines.Length) + " lines: " + string.Join(", ", acts.Select(c => c.title + " (" + c.lines.Length + ")")));
    var expected = new[] { "I want a cat.", "The house is on fire.", "Oh my gosh.", "Smoke so dee dee em dee.", "Foss gains.", "F.", "F R E T G.", "What happened here?", "Someone got murdered.",
      "You do it for him and you would do it again you do it for her.", "That is to say you'll do it for him.", "Keep your stance wide.", "Keep your body lowered.",
      "As you're moving forward balance is the key right foot left foot.", "But, now go even faster and as you're moving backwards keep your eyes on me.", "You are weird.",
      "Diamonds.", "Diamonds.", "Diamonds.", "That was a weird show.", "I agree." };
    var got = acts.SelectMany(c => c.lineText).ToArray();
    Check(got.SequenceEqual(expected), "the captions are the episode's exact dialogue, in order" + (got.SequenceEqual(expected) ? "" : " | got: " + string.Join(" / ", got)));
    Check(acts.SelectMany(c => c.lines).All(l => l.clip != null && l.clip.name.StartsWith("ct_") && l.clip.length > 0.3f), "every line plays a clip cut from the episode");
    var kitchen = acts[0];
    Sim.Events.Clear();
    Sim.Press(A, kitchen); Sim.Run(kitchen.length + 0.5f);
    int heard = kitchen.lines.Count(l => Sim.Events.Any(e => e.Contains("sound:") && e.Contains(l.clip.name)));
    Check(heard == 7, "PLAY SCENE 1 plays the kitchen's " + heard + " lines, captioned on the button");
    Check(All<TMPro.TextMeshPro>(A).Any(t => t.name == "Script" && Plain(t.text).Contains("F R E T G.") && Plain(t.text).Contains("I agree.")), "the script is on the wall");
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 2805f)); Sim.Run(1f);
    Check(Vector3.Distance(A.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f, "...and back to the lobby");
  }

  static void WaterParkTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== Splashbone Water Park");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "WATER PARK")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(2750f, 0, 22f)) < 0.3f, "the Station sends Alice to the water park");
    Check(Sim.BuildLog.Any(l => l.Contains("Splashbone Water Park: the Bone Bowl")), Plain(Sim.BuildLog.FirstOrDefault(l => l.Contains("Splashbone")) ?? "missing"));
    string what;
    Check(!Blocked(A, new[] { new Vector3(2784f, 0, 26f), new Vector3(2784f, 0.35f, 28.2f), new Vector3(2784f, 11.2f, 43.7f), new Vector3(2783f, 11.2f, 45f) }, out what), "up the tower stairs to the platform" + (what != "" ? " | " + what : ""));
    var ride = All<FirRide>(A).First(r => r.name == "The Bone Bowl");
    var C = new Vector3(2762f, 0, 62f);
    // follow one raft through a whole run: it must board, enter the bowl at the rim, spiral inward and down, drop through the middle and reach the pool
    float minR = 99f, maxRin = 0f; bool rim = false, center = false, pool = false, boarded = false, inside = false; int runs = 0; float lastA = 0f, total = 0f;
    var raft = ride.carts[0];
    for (int f = 0; f < 2400; f++) {
      Sim.Step();
      var p = raft.position; var d = p - C; d.y = 0f;
      if (Mathf.Abs(p.y - 11.3f) < 0.2f && Flat(p, new Vector3(2782.8f, 0, 45f)) < 1.5f) boarded = true;
      bool nowIn = d.magnitude < 7f && p.y > 2.9f && p.y < 6.6f;
      if (nowIn && !inside) { runs++; maxRin = 0f; }
      inside = nowIn;
      if (nowIn) {
        if (d.magnitude > 6f) rim = true;
        if (d.magnitude < 1.5f) center = true;
        float a = Mathf.Atan2(d.z, d.x);
        if (maxRin > 0f && runs == 2) { float da = ((a - lastA) * Mathf.Rad2Deg + 540f) % 360f - 180f; total += da; }
        lastA = a; maxRin = Mathf.Max(maxRin, d.magnitude); minR = Mathf.Min(minR, d.magnitude);
      }
      if (Flat(p, C + new Vector3(0, 0, 13f)) < 1.5f && p.y < 0.6f && p.y > 0f) pool = true;
    }
    float perRun = Mathf.Abs(total) / 360f;
    Check(boarded && rim && center && pool && perRun > 2.2f && perRun < 2.8f, "a raft boards at the top, enters the bowl at the rim, spirals " + perRun.ToString("0.0") + " turns down to the middle, drops through and splashes into the pool");
    int seats = All<VRC.SDK3.Components.VRCStation>(A).Count(st => st.name.StartsWith("Bowl raft seat"));
    int tubes = All<VRC.SDK3.Components.VRCStation>(A).Count(st => st.name.StartsWith("River tube seat"));
    Check(seats == 3 && tubes == 6, seats + " bowl rafts and " + tubes + " lazy river tubes to ride");
    var voiced = All<FirRainbowSkeleton>(A).Count(r => r.voice.clip != null && r.voice.clip.name.StartsWith("wp_") && r.caption.Length > 3);
    Check(voiced == 4, voiced + " voiced, captioned skeletons (a kid, the lifeguard, the snack seller, a sunbather)");
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x > 2705f && g.transform.position.x < 2795f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 3, gagOk + " of " + gagN + " gags (the Bone Bowl sign, the wave pool sign, the big bucket)" + string.Concat(gagBad.Select(x => " | " + x)));
  }

  static void PortalGarageTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The interdimensional garage");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "PORTAL GARAGE")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(2588f, 0, 22f)) < 0.3f, "the Station sends Alice to the garage");
    Check(Sim.BuildLog.Any(l => l.Contains("The Interdimensional Garage:")), Plain(Sim.BuildLog.FirstOrDefault(l => l.Contains("Interdimensional")) ?? "missing"));
    var tv = All<FirChannels>(A).First(c => c.name == "Dimension remote");
    var seen = new List<string>();
    for (int k = 0; k < 4; k++) { Sim.Press(A, tv); Sim.Run(0.6f); seen.Add(tv.channels[tv.Channel()].activeSelf && Plain(tv.caption.text).StartsWith("Channel") ? tv.channels[tv.Channel()].name : "BAD"); }
    Check(seen.Distinct().Count() == 4 && !seen.Contains("BAD"), "the dimension TV: " + string.Join(", ", seen));
    var portal = All<FirPortal>(A).First(p => p.a.position.x > 2600f && p.a.position.x < 2610f);
    A.player.pos = portal.a.position + new Vector3(0, 0.05f, 0); Sim.Run(0.3f);
    Check(A.player.pos.x > 2620f, "through the green portal into the dimensions " + A.player.pos);
    var saucer = All<FirRide>(A).First(r => r.rotor != null && r.rotor.parent != null && r.rotor.parent.name == "Saucer ride");
    var q0 = saucer.rotor.rotation; Sim.Run(2f);
    Check(Quaternion.Angle(q0, saucer.rotor.rotation) > 10f && All<VRC.SDK3.Components.VRCStation>(A).Any(st => st.name == "Saucer seat"), "the saucer flies round in a circle, with a seat");
    A.player.pos = new Vector3(2635f, 0.05f, 40f); Sim.Run(1.5f);
    A.player.pos = portal.b.position + new Vector3(0, 0.05f, 0); Sim.Run(0.3f);
    Check(A.player.pos.x < 2610f, "...and back out through the portal to the garage");
    var voiced = All<FirRainbowSkeleton>(A).Count(r => r.voice.clip != null && r.voice.clip.name.StartsWith("rm_") && r.caption.Length > 3);
    Check(voiced == 5, voiced + " voiced, captioned skeletons (the genius, the assistant, their pink selves, an alien)");
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x > 2565f && g.transform.position.x < 2695f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 4, gagOk + " of " + gagN + " gags (the thingamaflux, the sideways sign, the sandwich, the saucer button)" + string.Concat(gagBad.Select(x => " | " + x)));
  }

  static void ScpTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The containment facility");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "SCP ROOM")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(2540f, 0, 22f)) < 0.3f, "the Station sends Alice to the containment facility");
    Check(Sim.BuildLog.Any(l => l.Contains("The Containment Facility: five new anomalies")), Plain(Sim.BuildLog.FirstOrDefault(l => l.Contains("Containment")) ?? "no facility in the build log"));
    var term = All<FirSay>(A).First(x => x.name == "Records terminal");
    Sim.Press(A, term); Sim.Run(1f);
    Check(Plain(term.caption.text).Contains("Socks never pair"), "the records terminal: \"" + Plain(term.caption.text) + "\"");
    var voiced = All<FirRainbowSkeleton>(A).Count(r => r.voice.clip != null && r.voice.clip.name.StartsWith("sp_") && r.transform.position.x > 2515f && r.caption.Length > 3);
    Check(voiced == 3, voiced + " voiced, captioned staff (a researcher, a volunteer, a guard)");
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x > 2515f && g.transform.position.x < 2565f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 6, gagOk + " of " + gagN + " anomalies and alarms behave (anomalously)" + string.Concat(gagBad.Select(x => " | " + x)));
    A.player.pos = new Vector3(2558.5f, 0.05f, 33f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "BONE-999 (open the door)")); Sim.Run(1f);
    Check(Vector3.Distance(A.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f, "BONE-999 leads, as documented, to the lobby");
  }

  static void FourTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The backyard, the notebook school, the seabed town, the battle island");
    var rooms = new[] { new { t = "INVENTORS' BACKYARD", x = 2330f }, new { t = "NOTEBOOK SCHOOL", x = 2378f }, new { t = "SEABED TOWN", x = 2430f }, new { t = "BATTLE ISLAND", x = 2485f } };
    foreach (var rm in rooms) {
      A.player.pos = new Vector3(-4f, 0.05f, -1f);
      Sim.Press(A, All<FirTeleport>(A).First(t => t.name == rm.t)); Sim.Run(1f);
      bool there = Flat(A.player.pos, new Vector3(rm.x, 0, 22f)) < 0.3f;
      var plaque = All<FirSay>(A).First(x => x.name == "The plaque" && Mathf.Abs(x.transform.position.x - rm.x) < 20f);
      Sim.Press(A, plaque); Sim.Run(1f);
      bool said = Plain(plaque.caption.text).Contains("inspired by");
      Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && Mathf.Abs(t.transform.position.x - rm.x) < 20f)); Sim.Run(1f);
      Check(there && said && Vector3.Distance(A.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f, rm.t + ": there, the plaque, and back");
    }
    Check(new[] { "The Inventors' Backyard:", "The Notebook School:", "The Seabed Town:", "Battle Island:" }.All(r => Sim.BuildLog.Any(l => l.Contains(r))), "all four built");
    var bus = All<FirRide>(A).First(r => r.rotor != null && r.rotor.name == "The bus route");
    var jelly = All<FirRide>(A).First(r => r.rotor != null && r.rotor.name == "Jellyfish");
    var q0 = bus.rotor.rotation; var j0 = jelly.bobbers[0].localPosition.y; Sim.Run(2f);
    Check(Quaternion.Angle(q0, bus.rotor.rotation) > 10f && Mathf.Abs(jelly.bobbers[0].localPosition.y - j0) > 0.01f, "the bus flies round the island, and the jellyfish bob");
    string[] pre = { "iv_", "nb_", "sb_", "br_" };
    var voiced = All<FirRainbowSkeleton>(A).Count(r => r.voice.clip != null && pre.Any(p => r.voice.clip.name.StartsWith(p)) && r.caption.Length > 3);
    Check(voiced == 19, voiced + " voiced, captioned skeletons across the four rooms");
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x > 2305f && g.transform.position.x < 2515f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 9, gagOk + " of " + gagN + " gags (Mom's car, the shrink ray, the cheese, the jellyfish sign, the burger, the bus driver, the storm, the chest, the supply drop)" + string.Concat(gagBad.Select(x => " | " + x)));
  }

  static void ScreamTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The Scream Room");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "SCREAM ROOM")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(2278f, 0, 22f)) < 0.3f, "the Station sends Alice to the Scream Room");
    Check(Sim.BuildLog.Any(l => l.Contains("The Scream Room: a scream booth")), Plain(Sim.BuildLog.FirstOrDefault(l => l.Contains("Scream Room")) ?? "no Scream Room in the build log"));
    var vd = All<FirSay>(A).First(x => x.name == "The Void" && x.transform.position.x > 2255f);
    Sim.Press(A, vd); Sim.Run(1f);
    Check(Plain(vd.caption.text).Contains("void screams back"), "the void: \"" + Plain(vd.caption.text) + "\"");
    var choir = All<FirSkeletonCombo>(A).FirstOrDefault(c => c.title == "The Screaming Choir");
    Check(choir != null && choir.lines.Length == 5 && choir.pivots.Length == 40, "the screaming choir: 4 skeletons, 5 screams, on the console");
    var voiced = All<FirRainbowSkeleton>(A).Count(r => r.voice.clip != null && (r.voice.clip.name.StartsWith("sc_") || r.voice.clip.name.StartsWith("scream_")) && r.caption.Length > 3);
    Check(voiced == 6, voiced + " voiced, captioned skeletons (the choir, Ms Hush and Hollerbone)");
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x > 2255f && g.transform.position.x < 2300f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 5, gagOk + " of " + gagN + " gags scream (the booth, the meter, the goat, the kettle, the rubber chicken)" + string.Concat(gagBad.Select(x => " | " + x)));
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 2255f)); Sim.Run(1f);
    Check(Vector3.Distance(A.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f, "...and back to the (quiet) lobby");
  }

  static void FanTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The fan rooms");
    var rooms = new[] { new { t = "MARKIPLIER ROOM", x = 2128f }, new { t = "FLAMINGO ROOM", x = 2178f }, new { t = "RYAN TRAHAN ROOM", x = 2228f } };
    foreach (var rm in rooms) {
      A.player.pos = new Vector3(-4f, 0.05f, -1f);
      Sim.Press(A, All<FirTeleport>(A).First(t => t.name == rm.t)); Sim.Run(1f);
      bool there = Flat(A.player.pos, new Vector3(rm.x, 0, 22f)) < 0.3f;
      var plaque = All<FirSay>(A).First(x => x.name == "The plaque" && Mathf.Abs(x.transform.position.x - rm.x) < 18f);
      Sim.Press(A, plaque); Sim.Run(1f);
      bool honest = Plain(plaque.caption.text).Contains("unofficial tribute") && Plain(plaque.caption.text).Contains("Not affiliated");
      Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && Mathf.Abs(t.transform.position.x - rm.x) < 18f)); Sim.Run(1f);
      Check(there && honest && Vector3.Distance(A.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f, "the " + rm.t + ": there, its plaque (\"" + Plain(plaque.caption.text).Substring(0, 40) + "...\") and back");
    }
    // free admin
    A.player.pos = new Vector3(2178f, 0.05f, 28.4f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "FREE ADMIN")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(2164f, 0, 23f)) < 0.3f, "FREE ADMIN sends you back to the start of the room");
    // trading up
    var tr = All<FirChannels>(A).First(c => c.name == "Trade bell"); var trB = All<FirChannels>(B).First(c => c.name == "Trade bell");
    var got = new List<string>();
    for (int k = 0; k < 5; k++) {
      Sim.Events.Clear(); Sim.Press(A, tr); Sim.Run(0.6f);
      int c = tr.Channel();
      bool ok = tr.channels[c].activeSelf && tr.channels.Count(x => x.activeSelf) == 1 && Sim.Events.Any(e => e.Contains("sound:") && e.Contains(tr.lines[c].clip.name)) && Plain(tr.caption.text).Length > 15;
      got.Add((ok ? "" : "BAD ") + tr.channels[c].name);
    }
    Check(string.Join(" > ", got) == "Penny > Paperclip > Pencil > Bicycle > Car" && trB.Channel() == -1, "trading up: " + string.Join(" > ", got) + " (Bob's pedestal is his own)");
    var voiced = All<FirRainbowSkeleton>(A).Count(r => r.voice.clip != null && (r.voice.clip.name.StartsWith("mk_") || r.voice.clip.name.StartsWith("fl_") || r.voice.clip.name.StartsWith("rt_")) && r.caption.Length > 5);
    Check(voiced == 6, voiced + " voiced, captioned skeletons (none of them is the creator: they're a streamer, a superfan, a skeleton in a flamingo costume, the free admin victim, a vlogger and a hitchhiker)");
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x > 2105f && g.transform.position.x < 2250f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 7, gagOk + " of " + gagN + " gags (the monitor, door A, door B, the vault, the troll door, the dancing flamingo, the jar)" + string.Concat(gagBad.Select(x => " | " + x)));
  }

  static void BigTownTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== Bonetown, bigger");
    Check(Sim.BuildLog.Any(l => l.Contains("Bonetown districts: the town is now 98 x 130 m")), Plain(Sim.BuildLog.FirstOrDefault(l => l.Contains("Bonetown districts")) ?? "no districts in the build log"));
    string what;
    Check(!Blocked(A, new[] { new Vector3(-42f, 0, -6.5f), new Vector3(-46f, 0, -12f), new Vector3(-46f, 0, -30f), new Vector3(-46f, 0, -35f) }, out what),
          "from old Bonetown's west end, down the south lane to the town hall steps" + (what != "" ? " | " + what : ""));
    Check(!Blocked(A, new[] { new Vector3(-14f, 0, -30f), new Vector3(-108f, 0, -30f) }, out what), "the whole length of Main Street" + (what != "" ? " | " + what : ""));
    Check(!Blocked(A, new[] { new Vector3(-44.5f, 0, 12f), new Vector3(-46f, 0, 18f), new Vector3(-46f, 0, 50f), new Vector3(-108f, 0, 50f) }, out what), "up the north lane and along Clavicle Avenue" + (what != "" ? " | " + what : ""));
    Check(!Blocked(A, new[] { new Vector3(-51.5f, 0, -25f), new Vector3(-51.5f, 0, -12f) }, out what) && !Blocked(A, new[] { new Vector3(-51.5f, 0, 45f), new Vector3(-51.5f, 0, 33f) }, out what),
          "the Marrow Arms' hedge has gates from Main Street and Clavicle Avenue" + (what != "" ? " | " + what : ""));
    Check(Blocked(A, new[] { new Vector3(-70f, 0, -20f), new Vector3(-70f, 0, -14f) }, out what), "...and is a hedge everywhere else (" + what + ")");
    Check(Blocked(A, new[] { new Vector3(-30f, 0, 16f), new Vector3(-30f, 0, 26f) }, out what), "old Bonetown's houses have real back walls now (" + what + ")");
    var folk = All<FirRainbowSkeleton>(A).Where(r => r.voice.clip != null && r.voice.clip.name.StartsWith("t2_") && r.caption.Length > 5).ToList();
    Check(folk.Count == 19, folk.Count + " new voiced, captioned townsfolk");
    var wn = All<FirWanderer>(A).Where(w => w.voice.clip != null && w.voice.clip.name.StartsWith("w_")).ToList();
    Check(wn.Count == 6, wn.Count + " new wanderers: " + string.Join(", ", wn.Select(w => w.voice.clip.name)));
    var mgr = All<FirRide>(A).First(r => r.rotor != null && r.rotor.parent != null && r.rotor.parent.name == "Merry-go-round");
    var q0 = mgr.rotor.rotation; Sim.Run(2f);
    int seats = All<VRC.SDK3.Components.VRCStation>(A).Count(st => st.name.StartsWith("Merry-go-round seat"));
    Check(Quaternion.Angle(q0, mgr.rotor.rotation) > 20f && seats == 4, "the merry-go-round turns, with " + seats + " horses to ride");
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.voice != null && g.voice.clip != null && g.voice.clip.name.StartsWith("g_"), out gagN, gagBad);
    Check(gagOk == gagN && gagN == 6, gagOk + " of " + gagN + " new town gags (the ducks, the clock, the scarecrow, the cow, the school bell, the fire truck's ladder)" + string.Concat(gagBad.Select(x => " | " + x)));
  }

  static void CellTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== Cellbound (1955)");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "CELLBOUND (1955)")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(2070f, 0, 22f)) < 0.3f, "the Station sends Alice to the Cellbound room");
    Check(Sim.BuildLog.Any(l => l.Contains("Cellbound (1955): a tribute room")), Plain(Sim.BuildLog.FirstOrDefault(l => l.Contains("Cellbound")) ?? "no Cellbound in the build log"));
    var plaque = All<FirSay>(A).First(x => x.name == "The plaque");
    Sim.Press(A, plaque); Sim.Run(1f);
    Check(Plain(plaque.caption.text).Contains("Tex Avery and Michael Lah"), "the plaque: \"" + Plain(plaque.caption.text) + "\"");
    string what;
    Check(!Blocked(A, new[] { new Vector3(2064f, 0, 30f), new Vector3(2060.5f, 0, 34f), new Vector3(2060.5f, 0, 40f) }, out what), "the cell door is open: walk in" + (what != "" ? " | " + what : ""));
    A.player.pos = new Vector3(2057f, 0.05f, 41.5f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Crawl into the tunnel")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(2080.5f, 0, 46.5f)) < 0.3f, "the tunnel comes out behind the warden's TV");
    var tvA = All<FirChannels>(A).First(c => c.name == "The remote"); var tvB = All<FirChannels>(B).First(c => c.name == "The remote");
    Check(tvA.staticScreen.activeSelf && tvA.channels.All(c => !c.activeSelf), "the TV starts on static");
    var seen = new List<string>();
    for (int k = 0; k < 4; k++) {
      Sim.Events.Clear();
      Sim.Press(A, tvA); Sim.Run(0.6f);
      int c = tvA.Channel();
      bool ok = tvA.channels[c].activeSelf && !tvA.staticScreen.activeSelf && tvA.channels.Count(x => x.activeSelf) == 1 && Sim.Events.Any(e => e.Contains("sound:") && e.Contains(tvA.lines[c].clip.name)) && Plain(tvA.caption.text).Length > 20;
      seen.Add((ok ? "" : "BAD ") + tvA.channels[c].name);
    }
    Check(seen.All(x => !x.StartsWith("BAD")) && seen.Distinct().Count() == 4, "the remote flicks through 4 channels, the convict acting out each: " + string.Join(", ", seen));
    Sim.Press(A, tvA); Sim.Run(0.6f);
    Check(tvA.Channel() == 0 && tvB.staticScreen.activeSelf, "...then round again (and Bob's TV is his own: still static)");
    var voiced = All<FirRainbowSkeleton>(A).Count(r => r.voice.clip != null && r.voice.clip.name.StartsWith("cb_") && r.caption.Length > 5);
    Check(voiced == 3, voiced + " voiced, captioned skeletons: the convict, the warden and his wife");
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x > 2045f && g.transform.position.x < 2095f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 1, gagOk + " of " + gagN + " gags (the spoon)" + string.Concat(gagBad.Select(x => " | " + x)));
    A.player.pos = new Vector3(2070f, 0.05f, 22f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 2045f)); Sim.Run(1f);
    Check(Vector3.Distance(A.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f, "...and back to the lobby");
  }

  static void Batch2Tests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The second batch of Station stops");
    var stops = new[] { new { t = "CRYSTAL COVE", x = 1055f }, new { t = "CRITTER MEADOW", x = 1125f }, new { t = "THE SHADOW BELOW", x = 1180f }, new { t = "INK PLAZA", x = 1310f },
                        new { t = "THE DREAD DEPTHS", x = 1375f }, new { t = "HERO'S GLADE", x = 1445f }, new { t = "HONEYCOMB HOLLOW", x = 1515f }, new { t = "DRAGON SHORE", x = 1585f },
                        new { t = "HERO TOWER", x = 1650f }, new { t = "GLOOM CITY", x = 1715f }, new { t = "MYRTLE BEACH", x = 1790f }, new { t = "BOONE, NC", x = 1870f },
                        new { t = "DACHSHUND ROOM", x = 1935f }, new { t = "EXTREMELY CHRISTMAS", x = 2000f } };
    int ok = 0; var bad = new List<string>();
    foreach (var st in stops) {
      A.player.pos = new Vector3(-4f, 0.05f, -1f);
      var go = All<FirTeleport>(A).FirstOrDefault(t => t.name == st.t);
      if (go == null) { bad.Add(st.t + " (no button)"); continue; }
      Sim.Press(A, go); Sim.Run(1f);
      bool there = Flat(A.player.pos, new Vector3(st.x, 0, 22f)) < 0.3f;
      var back = All<FirTeleport>(A).FirstOrDefault(t => t.name == "Back to the lobby" && Mathf.Abs(t.transform.position.x - st.x) < 25f);
      if (back != null) { Sim.Press(A, back); Sim.Run(1f); }
      if (there && back != null && Vector3.Distance(A.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f) ok++; else bad.Add(st.t);
    }
    Check(ok == 14, ok + " of 14 new Station stops go there and back" + string.Concat(bad.Select(x => " | " + x)));
    foreach (var rep in new[] { "Crystal Cove:", "Critter Meadow:", "The Shadow Below:", "Ink Plaza:", "The Dread Depths:", "Hero's Glade:", "Honeycomb Hollow:", "Dragon Shore:", "Hero Tower:", "Gloom City:", "Myrtle Beach:", "Boone, NC:", "The Dachshund Room:", "Extremely Christmas:" })
      Check(Sim.BuildLog.Any(l => l.Contains(rep)), "built: " + Plain(Sim.BuildLog.FirstOrDefault(l => l.Contains(rep)) ?? rep + " MISSING"));
    var probs = Sim.BuildLog.Where(l => l.Contains("problem")).ToList();
    Check(probs.Count == 0, "the build reports no problems" + string.Concat(probs.Take(5).Select(x => " | " + x)));
    string what;
    // the shadow world: in through the supply closet, a tile gate, out by waking up
    A.player.pos = new Vector3(1188.5f, 0.05f, 30f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Open the supply closet")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(1235f, 0, 23f)) < 0.3f, "the supply closet opens on the dark world");
    var toCastle = new[] { new Vector3(1235f, 0, 55f), new Vector3(1235f, 0, 64f) };
    Check(Blocked(A, toCastle, out what), "the tile gate blocks the way to the castle (" + what + ")");
    var ftA = All<FirTiles>(A).First(t => t.gate.transform.position.x > 1190f);
    foreach (int v in ftA.startPressed) { Sim.Press(A, All<FirHuntButton>(A).First(b => b.action == 31 && b.value == v && b.hunt != null && b.hunt.program == ftA)); Sim.Step(); }
    Check(ftA.IsSolved() && !Blocked(A, toCastle, out what), "flipping the right tiles opens the gate" + (what != "" ? " | " + what : ""));
    var spring = All<FirSay>(A).First(x => x.name == "The Shade Spring");
    Sim.Press(A, spring); Sim.Run(1f);
    Check(Plain(spring.caption.text).Contains("shade spring"), "the shade spring: \"" + Plain(spring.caption.text) + "\"");
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Wake up (back to class)")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(1180f, 0, 22f)) < 0.3f, "waking up puts you back in class");
    // the warp pad and the morph tube
    var warp = All<FirPortal>(A).First(p => p.a.position.x > 1030f && p.a.position.x < 1080f);
    A.player.pos = warp.a.position + new Vector3(0, 0.05f, 0); Sim.Run(0.3f);
    Check(A.player.pos.y > 9.5f && Flat(A.player.pos, warp.b.position) < 1.5f, "the warp pad puts you on top of the cliff " + A.player.pos);
    A.player.pos = new Vector3(1055f, 0.05f, 22f); Sim.Run(1.5f);
    var tube = All<FirPortal>(A).First(p => p.a.position.x > 1350f && p.a.position.x < 1400f);
    A.player.pos = tube.a.position + new Vector3(0, 0.05f, 0); Sim.Run(0.3f);
    Check(Flat(A.player.pos, tube.b.position) < 1.5f, "the morph tube rolls you across the cave");
    // collectables and launch pads
    foreach (var lab in new[] { "ENERGY", "NOTES", "GEMS" }) {
      var fb = All<FirBoost>(A).First(x => x.counterLabel == lab);
      A.player.pos = new Vector3(1000f, 0.05f, 22f); Sim.Run(0.5f);
      int before = fb.Count();
      A.player.pos = fb.rings[fb.rings.Length - 1].position - new Vector3(0, 0.9f, 0); Sim.Run(0.3f);
      Check(fb.Count() == before + 1 && Plain(fb.counter.text).Contains(lab + ": "), fb.rings.Length + " " + lab.ToLower() + " to collect; picked one up: \"" + Plain(fb.counter.text) + "\"");
      if (fb.pads.Length > 0) {
        A.player.pos = new Vector3(1000f, 0.05f, 22f); Sim.Run(0.8f);
        A.player.velocity = Vector3.zero; A.player.pos = fb.pads[0].position + new Vector3(0, 0.03f, 0); Sim.Run(0.2f);
        Check(A.player.velocity.y > 6f, "...and its launch pad throws you up (" + A.player.velocity + ")");
      }
    }
    // the ink turf
    var turf = All<FirInkTurf>(A)[0];
    Func<int, FirHuntButton> Ink = act => All<FirHuntButton>(A).First(b => b.action == (act < 0 ? 72 : 71) && (act < 0 || b.value == act) && b.hunt != null && b.hunt.program == turf);
    for (int k = 0; k < 5; k++) { A.player.pos = turf.origin + new Vector3(k * 2f, 0.05f, 0); Sim.Step(); }
    Check(turf.Painted() == 5 && turf.tiles[0].sharedMaterial == turf.inkMats[0] && Plain(turf.board.text).Contains("TURF: 6%"), "walking 5 tiles inks them orange: " + Plain(turf.board.text));
    Sim.Press(A, Ink(1)); Sim.Step();
    A.player.pos = turf.origin + new Vector3(0, 0.05f, 2f); Sim.Step();
    var turfB = All<FirInkTurf>(B)[0];
    Check(turf.tiles[10].sharedMaterial == turf.inkMats[1] && turfB.Painted() == 0, "switch to blue and it inks blue (just for you: Bob's turf is clean)");
    A.player.pos = new Vector3(1310f, 0.05f, 27.5f); Sim.Step();
    Sim.Press(A, Ink(-1)); Sim.Step();
    Check(turf.Painted() == 0 && turf.tiles[0].sharedMaterial == turf.blankMat, "CLEAN wipes the turf");
    // rides
    var rides = All<FirRide>(A);
    var wheel = rides.First(r => r.rotor != null && r.rotor.position.x > 1760f && r.rotor.position.x < 1820f && r.upright != null && r.upright.Length == 6);
    var train = rides.First(r => r.kind == 1 && r.carts[0].position.x > 1840f && r.carts[0].position.x < 1900f);
    var xtrain = rides.First(r => r.kind == 1 && r.carts.Length == 3 && r.carts[0].position.x > 1970f);
    var dog = rides.First(r => r.kind == 1 && r.carts[0].position.x > 1920f && r.carts[0].position.x < 1950f);
    var q0 = wheel.rotor.rotation; var p1 = train.carts[0].position; var p2 = xtrain.carts[0].position; var p3 = dog.carts[0].position;
    Sim.Run(3f);
    Check(Quaternion.Angle(q0, wheel.rotor.rotation) > 5f && Vector3.Distance(p1, train.carts[0].position) > 2f && Vector3.Distance(p2, xtrain.carts[0].position) > 2f && Vector3.Distance(p3, dog.carts[0].position) > 1f,
          "the beach wheel turns, the steam train and the Christmas train run, and a dachshund walks the room");
    int seats = All<VRC.SDK3.Components.VRCStation>(A).Count(st => st.transform.position.x > 1020f && st.transform.position.x < 2040f);
    Check(seats == 10, seats + " seats to ride in (6 on the beach wheel, the steam train, 3 on the Christmas train)");
    // the people
    string[] pre = { "gm_", "cr_", "sh_", "ik_", "dr_", "gl_", "hn_", "dg_", "hr_", "gc_", "mb_", "bn_", "dx_", "xm_" };
    var voiced = All<FirRainbowSkeleton>(A).Where(r => r.voice.clip != null && pre.Any(p => r.voice.clip.name.StartsWith(p))).ToList();
    Check(voiced.Count == 60 && voiced.All(r => r.caption.Length > 5), voiced.Count + " new voiced, captioned skeletons");
    Check(All<FirWanderer>(A).Any(w => w.voice.clip != null && w.voice.clip.name == "cr_rival"), "the rival runs laps of the meadow");
    var carol = All<FirSkeletonCombo>(A).FirstOrDefault(c => c.title == "The Twelve Bones of Christmas");
    Check(carol != null && carol.lines.Length == 5 && carol.lineText.All(t => t.Length > 5), "the carol is on the console, captioned");
    // every new gag
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x > 1020f && g.transform.position.x < 2040f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 31, gagOk + " of " + gagN + " new gags played, talked, showed their line and reset" + string.Concat(gagBad.Take(6).Select(x => " | " + x)));
  }

  static void ApartTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The Marrow Arms (Bonetown's apartments)");
    Check(Sim.BuildLog.Any(l => l.Contains("The Marrow Arms: 11 apartments on two floors")), Plain(Sim.BuildLog.FirstOrDefault(l => l.Contains("Marrow Arms")) ?? "no Marrow Arms in the build log"));
    var probs = Sim.BuildLog.Where(l => l.Contains("can't get to") || l.Contains("are both at")).ToList();
    Check(probs.Count == 0, "every routine fits its day, and nobody stands in anybody's spot" + string.Concat(probs.Take(4).Select(x => " | " + x)));
    string what;
    float[] xc = { -69.75f, -77.25f, -84.75f, -92.25f };
    Check(!Blocked(A, new[] { new Vector3(-40f, 0, 11f), new Vector3(-56f, 0, 11f), new Vector3(-63f, 0, 11f), new Vector3(-94.5f, 0, 11f) }, out what), "from Bonetown's plaza, through the west doorway, across the front yard, the lobby and down the hallway" + (what != "" ? " | " + what : ""));
    Check(!Blocked(A, new[] { new Vector3(-49f, 0, 11f), new Vector3(-49f, 0, -16f), new Vector3(-107f, 0, -16f), new Vector3(-107f, 0, 37f), new Vector3(-49f, 0, 37f), new Vector3(-49f, 0, 11f) }, out what), "all the way round the outside of the building" + (what != "" ? " | " + what : ""));
    Check(!Blocked(A, new[] { new Vector3(-60.3f, 0.2f, -8.15f), new Vector3(-66.2f, 3.6f, -8.15f), new Vector3(-67.2f, 3.6f, -6.4f), new Vector3(-92.25f, 3.6f, -6.2f), new Vector3(-92.25f, 3.6f, -3.5f) }, out what), "up the ramp, along the balcony and in at flat 11's door" + (what != "" ? " | " + what : ""));
    Check(!Blocked(A, new[] { new Vector3(-63f, 3.6f, -6.2f), new Vector3(-63f, 3.6f, -2f), new Vector3(-63f, 3.6f, 15f), new Vector3(-78f, 3.6f, 18f) }, out what), "from the balcony onto the roof terrace" + (what != "" ? " | " + what : ""));
    Check(!Blocked(A, new[] { new Vector3(-92.25f, 3.6f, -6.2f), new Vector3(-97.1f, 3.6f, -6.2f), new Vector3(-97.1f, 3.6f, 10.8f), new Vector3(-94f, 3.6f, 10.8f), new Vector3(-92f, 3.6f, 18f) }, out what), "...and round the side walkway onto the terrace" + (what != "" ? " | " + what : ""));
    int open = 0;
    foreach (float x in xc) {
      if (!Blocked(A, new[] { new Vector3(x, 0, 11f), new Vector3(x, 0, 16f) }, out what)) open++;
      if (!Blocked(A, new[] { new Vector3(x, 0, 11f), new Vector3(x, 0, 6f) }, out what)) open++;
    }
    Check(open == 8, open + " of 8 doorways off the hallway walk in (7 flats and the laundry)");
    Check(!Blocked(A, new[] { new Vector3(-63f, 0, 11f), new Vector3(-63f, 0, 19f) }, out what) && !Blocked(A, new[] { new Vector3(-63f, 0, 11f), new Vector3(-63f, 0, 2f) }, out what), "the gym and the common room open off the lobby");
    var res = All<FirResident>(A);
    Check(res.Count == 12, res.Count + " residents: " + string.Join(", ", res.Select(r => r.who)));
    // a day on the Bonetown clock, checked every hour: everyone is where their routine says, doing it, captioned
    int checks = 0, right = 0; var wrong = new List<string>();
    for (float h = 0.75f; h < 24f; h += 1f) {
      SetDayHour(h); Sim.Run(2.5f);
      foreach (var r in res) {
        float hh = r.DayHour();
        int s = r.StopAt(hh);
        float since = hh - r.hour[s]; if (since < 0f) since += 24f;
        bool shouldBeThere = since * (r.dayMs / 1000f / 24f) * r.walkSpeed > r.PathLength(s) + 2.5f * 4f;
        if (!shouldBeThere) continue;
        checks++;
        bool ok = Flat(r.transform.position, r.spot[s]) < 0.15f && r.Walking() == 0 && Plain(r.caption.text).Contains(r.doing[s]);
        if (ok) right++; else wrong.Add(r.who + " at " + hh.ToString("0.0") + "h: " + Plain(r.caption.text) + " @" + r.transform.position);
      }
    }
    Check(checks > 120 && right == checks, right + " of " + checks + " hourly checks: each resident in the right place doing the right thing" + string.Concat(wrong.Take(4).Select(x => " | " + x)));
    var mort = res.First(r => r.who == "Mortimer"); var tib = res.First(r => r.who == "Granny Tibia");
    SetDayHour(23.1f); Sim.Run(2.5f);
    Check(Plain(tib.caption.text).Contains("TOO LOUD") && Plain(mort.caption.text).Contains("loudly"), "at 11 pm Granny Tibia knocks on Mortimer's door (\"" + Plain(tib.caption.text) + "\") while he practises (\"" + Plain(mort.caption.text) + "\")");
    SetDayHour(18.9f); Sim.Run(0.5f);
    Check(mort.Walking() == 1 && Plain(mort.caption.text).Contains("on the way") && Flat(mort.transform.position, mort.spot[mort.Stop()]) > 2f, "on the way out to busk: \"" + Plain(mort.caption.text) + "\" " + mort.transform.position);
    var clock = All<TMPro.TextMeshPro>(A).First(t => t.name == "MarrowClock");
    Check(Plain(clock.text).Contains("BONETOWN TIME") && Plain(clock.text).Contains("PM"), "the lobby clock: " + Plain(clock.text));
    // press someone: they stop, say their line, and it's captioned
    SetDayHour(10.6f); Sim.Run(2.5f);
    var cl = res.First(r => r.who == "Coccyx");
    Sim.Events.Clear();
    Sim.Press(A, cl); Sim.Run(1f);
    var capB = All<FirResident>(B).First(r => r.who == "Coccyx");
    Check(cl.IsChatting() && Plain(cl.caption.text).Contains(cl.line) && cl.line.Length > 20 && Sim.Events.Any(e => e.Contains("sound:") && e.Contains("ap_coccyx")) && capB.IsChatting(),
          "press Coccyx: he stops and says (captioned, for everyone) \"" + cl.line + "\"");
    Sim.Run(8f);
    Check(!cl.IsChatting() && Plain(cl.caption.text).Contains(cl.doing[cl.Stop()]), "...then gets back to it: \"" + Plain(cl.caption.text) + "\"");
    // captions for every voiced skeleton and every combo line
    var rbs = All<FirRainbowSkeleton>(A).Where(r => r.voice != null && r.voice.clip != null).ToList();
    var noCap = rbs.Where(r => string.IsNullOrEmpty(r.caption)).Select(r => r.name + " (" + r.voice.clip.name + ")").ToList();
    Check(noCap.Count == 0, (rbs.Count - noCap.Count) + " of " + rbs.Count + " voiced skeletons have captions" + string.Concat(noCap.Take(5).Select(x => " | " + x)));
    var rb = rbs.First(r => r.voice.clip.name == "in_hop");
    Sim.Press(A, rb); Sim.Run(1f);
    Check(Plain(rb.label.text).Contains(rb.caption), "pressed, a skeleton's label shows what it says: \"" + Plain(rb.label.text) + "\"");
    var cbs = All<FirSkeletonCombo>(A);
    int lines = cbs.Sum(c => c.lines.Length), capped = cbs.Sum(c => c.lineText.Count(t => !string.IsNullOrEmpty(t)));
    Check(lines == capped, capped + " of " + lines + " combo lines have captions");
    var gw = All<FirWanderer>(A);
    Check(gw.All(w => w.line.Length > 5), "all " + gw.Count + " wanderers caption their lines");
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x < -48f && g.transform.position.x > -110f && g.transform.position.z > -17f && g.transform.position.z < 39f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 5, gagOk + " of " + gagN + " apartment gags (the lift, the mailboxes, the dryer, the for-rent sign, the dumpster)" + string.Concat(gagBad.Select(x => " | " + x)));
  }

  static void LaterTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The later Station stops");
    var stops = new[] { new { t = "THE REDEMPTION INN", at = new Vector3(580f, 0, 22f) }, new { t = "THE SPEED ZONE", at = new Vector3(650f, 0, 22f) },
                        new { t = "THE PLATFORM WORLD", at = new Vector3(730f, 0, 22f) }, new { t = "NORTH CAROLINA", at = new Vector3(810f, 0, 22f) },
                        new { t = "THE DIGITAL CIRCUS", at = new Vector3(880f, 0, 22f) }, new { t = "THE SKITS", at = new Vector3(938f, 0, 22f) },
                        new { t = "CORN FLAKES ROOM", at = new Vector3(992f, 0, 22f) } };
    foreach (var st in stops) {
      A.player.pos = new Vector3(-4f, 0.05f, -1f);
      Sim.Press(A, All<FirTeleport>(A).First(t => t.name == st.t)); Sim.Run(1f);
      bool there = Flat(A.player.pos, st.at) < 0.3f;
      Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && Mathf.Abs(t.transform.position.x - st.at.x) < 40f && t.transform.position.x > 550f)); Sim.Run(1f);
      Check(there && Vector3.Distance(A.player.pos, new Vector3(0, 0.05f, -2.6f)) < 1f, "the Station goes to " + st.t + " and back");
    }
    foreach (var rep in new[] { "The Redemption Inn: 6 voiced", "The Speed Zone: 5 boost pads and springs, 24 rings", "The Platform World: 12 platforms", "North Carolina: the Blue Ridge", "The Digital Circus: a TV-headed", "The Skits: 10 stick-figure", "The Corn Flakes Room: 9 corn flake jokes" })
      Check(Sim.BuildLog.Any(l => l.Contains(rep)), "built: " + Plain(Sim.BuildLog.First(l => l.Contains(rep.Split(':')[0]))));
    // the inn's elevator only goes down
    A.player.pos = new Vector3(598.5f, 0.05f, 48f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Going down")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(22f, 0, -2f)) < 0.3f, "the Inn's elevator goes down... to the cellar shed");
    // boost pads, springs and rings
    var fb = All<FirBoost>(A)[0];
    A.player.velocity = Vector3.zero; A.player.pos = fb.pads[0].position + new Vector3(0, 0.03f, 0); Sim.Run(0.2f);
    Check(A.player.velocity.z > 12f, "a boost pad launches Alice down the track (" + A.player.velocity + ")");
    A.player.pos = new Vector3(650f, 0.05f, 22f); Sim.Run(0.8f);
    A.player.velocity = Vector3.zero; A.player.pos = fb.pads[3].position + new Vector3(0, 0.03f, 0); Sim.Run(0.2f);
    Check(A.player.velocity.y > 8f, "a spring throws her up (" + A.player.velocity + ")");
    int before = fb.Count();
    A.player.pos = fb.rings[0].position - new Vector3(0, 0.9f, 0); Sim.Run(0.3f);
    Check(fb.Count() == before + 1 && !fb.rings[0].gameObject.activeSelf && Plain(fb.counter.text).Contains("1"), "running through a ring collects it: \"" + Plain(fb.counter.text) + "\"");
    var fbB = All<FirBoost>(B)[0];
    Check(fbB.rings[0].gameObject.activeSelf, "...rings are per player (Bob's is still there)");
    A.player.pos = new Vector3(650f, 0.05f, 22f); Sim.Run(21f);
    Check(fb.rings[0].gameObject.activeSelf, "...and it comes back after 20 seconds");
    // warp tubes
    A.player.pos = new Vector3(650f, 0.05f, 22f); Sim.Run(1.5f);
    var tubes = All<FirPortal>(A).Where(p => p.a.position.x > 700f && p.a.position.x < 760f).ToList();
    A.player.pos = tubes[0].a.position + new Vector3(0, 0.05f, 0); Sim.Run(0.3f);
    Check(tubes.Count == 2 && Flat(A.player.pos, tubes[0].b.position) < 1.5f, "a warp tube pops Alice out of the other tube across the level");
    var cloud = All<FirRide>(A).FirstOrDefault(r => r.name == "Cloud Ride");
    var c0 = cloud.carts[0].position; Sim.Run(2f);
    Check(cloud != null && Vector3.Distance(c0, cloud.carts[0].position) > 1f, "the cloud ride drifts round the course");
    // the circus: the EXIT isn't, the void talks
    A.player.pos = new Vector3(880f, 0.05f, 57f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "EXIT")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(880f, 0, 22f)) < 0.3f, "the circus EXIT puts you back at the circus entrance");
    var vd = All<FirSay>(A).First(x => x.transform.position.x > 859f && x.transform.position.x < 901f);
    Sim.Press(A, vd); Sim.Run(1f);
    Check(Plain(vd.caption.text).Contains("void"), "the void says: \"" + Plain(vd.caption.text) + "\"");
    // the people
    var voiced = All<FirRainbowSkeleton>(A).Where(r => r.voice.clip != null && new[] { "in_", "sp_", "pl_", "nc_", "ci_", "cf_" }.Any(pre => r.voice.clip.name.StartsWith(pre)) && r.transform.position.x < 1020f).ToList();
    Check(voiced.Count == 21, voiced.Count + " voiced skeletons (6 inn, 2 speed, 2 platform, 5 NC, 4 circus, 2 corn)");
    Check(All<FirWanderer>(A).Any(w => w.voice.clip != null && w.voice.clip.name == "sp_dash"), "Dash laps the speed zone");
    // every gag in the new areas
    int gagN; var gagBad = new List<string>();
    int gagOk = RunGags(B, g => g.transform.position.x >= 555f && g.transform.position.x < 1010f, out gagN, gagBad);
    Check(gagOk == gagN && gagN == 46, gagOk + " of " + gagN + " new gags (16 placed + 4 bonk blocks + flag + 5 NC/circus + 10 skits + 10 corn) played, talked, showed their line and reset" + string.Concat(gagBad.Take(6).Select(x => " | " + x)));
  }

  static void MysteryTests(Sim.Client A, Sim.Client B) {
    Console.WriteLine("\n== The Mystery Hut");
    A.player.pos = new Vector3(-4f, 0.05f, -1f);
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "THE MYSTERY HUT")); Sim.Run(1f);
    Check(Flat(A.player.pos, new Vector3(520f, 0, 22f)) < 0.3f, "the Station sends Alice to the Mystery Hut");
    Check(Sim.BuildLog.Any(l => l.Contains("The Mystery Hut: 8 exhibits, 4 cipher signs")), "8 exhibits, 4 cipher signs, a secret room, 5 skeletons, a gnome and a lake monster");
    // decode the signs the way a player would: every letter back three
    var signs = All<TMPro.TextMeshPro>(A).Where(t => t.name == "Cipher").Select(t => Plain(t.text)).OrderBy(x => x).ToList();
    Func<string, string> Back3 = x => new string(x.Select(ch => ch >= 'A' && ch <= 'Z' ? (char)('A' + (ch - 'A' + 23) % 26) : ch).ToArray());
    var words = new Dictionary<string, char> { { "ZERO", '0' }, { "ONE", '1' }, { "TWO", '2' }, { "THREE", '3' }, { "FOUR", '4' }, { "FIVE", '5' }, { "SIX", '6' }, { "SEVEN", '7' }, { "EIGHT", '8' }, { "NINE", '9' } };
    string decoded = "", plain = "";
    foreach (var sg in signs) { var msg = Back3(sg.Split('|').Last().Trim()); plain += msg + " / "; decoded += words[msg.Split(' ').Last()]; }
    var lockA = All<FirCodeLock>(A)[0]; var lockB = All<FirCodeLock>(B)[0];
    Check(decoded == lockA.code, "the 4 signs decode to \"" + plain.TrimEnd(' ', '/') + "\": code " + decoded);
    string what;
    var toRoom = new[] { new Vector3(520f, 0, 22.5f), new Vector3(520f, 0, 33f), new Vector3(520f, 0, 38.3f), new Vector3(518.4f, 0, 42.5f), new Vector3(520f, 0, 44.6f), new Vector3(520f, 0, 49f) };
    Check(Blocked(A, toRoom, out what) && what.StartsWith("Case"), "inside, past the pit: the bookcase blocks the way (" + what + ")");
    Func<Sim.Client, int, FirHuntButton> K = (c, v) => { var l = All<FirCodeLock>(c)[0]; return All<FirHuntButton>(c).First(b => b.action == 61 && b.value == v && b.hunt != null && b.hunt.program == l); };
    foreach (int d in new[] { 1, 2, 3, 4, 11 }) { Sim.Press(A, K(A, d)); Sim.Step(); }
    Check(!lockA.IsOpen(), "a wrong code (1234) does nothing");
    foreach (char ch in lockA.code) { Sim.Press(A, K(A, ch - '0')); Sim.Step(); }
    Sim.Press(A, K(A, 11)); Sim.Run(0.3f);
    Check(lockA.IsOpen() && !lockA.door.activeSelf && lockB.door.activeSelf, "the decoded code swings the bookcase open (for Alice; Bob still has to crack it)");
    Check(!Blocked(A, toRoom, out what), "...into the secret room" + (what != "" ? " | " + what : ""));
    var says = All<FirSay>(A).Where(x => x.transform.position.x > 499f && x.transform.position.x < 541f).ToList();
    foreach (var sy in says) Sim.Press(A, sy);
    Sim.Run(1f);
    Check(says.Count == 2 && says.All(sy => Plain(sy.caption.text).Length > 10), "the journal and the weirdness detector speak: \"" + Plain(says.First(x => x.name == "Weirdness Detector").caption.text) + "\"");
    Check(All<FirRainbowSkeleton>(A).Count(r => r.voice.clip != null && r.voice.clip.name.StartsWith("my_")) == 5, "5 voiced skeletons: the owner, the cashier, a tourist and his kid, and a lumberjack");
    Sim.Press(A, All<FirTeleport>(A).First(t => t.name == "Back to the lobby" && t.transform.position.x > 499f && t.transform.position.x < 541f)); Sim.Run(0.5f);
  }
}
