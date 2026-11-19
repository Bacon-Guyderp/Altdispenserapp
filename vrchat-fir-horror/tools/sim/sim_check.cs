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
    var townRoute = new[] { new Vector3(0, 0, -2.6f), new Vector3(-4f, 0, 4f), new Vector3(-13f, 0, 4f), new Vector3(-24.5f, 0, 4f), new Vector3(-26f, 0, 9f),
                            new Vector3(-38f, 0, 9f), new Vector3(-38f, 0, 0f), new Vector3(-34f, 0, -5.5f), new Vector3(-20f, 0, -5.5f), new Vector3(-14f, 0, 4f) };
    int townBlocked = 0;
    for (int i = 0; i + 1 < townRoute.Length; i++)
      foreach (float h in new[] { 0.4f, 1.2f, 1.9f }) {
        var d = townRoute[i + 1] - townRoute[i]; RaycastHit hit;
        if (Physics.Raycast(townRoute[i] + new Vector3(0, h, 0), d.normalized, out hit, d.magnitude, 1, QueryTriggerInteraction.Ignore)) { townBlocked++; Console.WriteLine("    town leg " + i + " blocked by " + hit.collider.name + " at " + hit.point); break; }
      }
    Check(townBlocked == 0, "walked from the lobby's west doorway into Bonetown and round the plaza");
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
      if (gag.scare) continue;
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
    Check(gagOk == gagN && gagN == 75, gagOk + " of " + gagN + " gags (53 original + 22 in Bonetown) played, talked, showed their line and reset" + string.Concat(gagBad.Take(6).Select(x => " | " + x)));

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
      : kind == 4 ? (UdonSharpBehaviour)All<FirSkeletonCombo>(c).First(g => g.shareIndex == i) : (UdonSharpBehaviour)All<FirWanderer>(c).First(g => g.shareIndex == i);
    string[] kindName = { "pressed gags", "skeleton routines", "closet", "gag skeletons", "combo acts", "Bonetown wanderers" };
    int[] kindCount = { All<FirGag>(B).Count, All<FirSkeletonGag>(B).Count, All<FirCloset>(B).Count, All<FirRainbowSkeleton>(B).Count, All<FirSkeletonCombo>(B).Count, All<FirWanderer>(B).Count };
    for (int kind = 0; kind < 6; kind++) {
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
    var held = St(A).transform.position; Sim.Run(3f);
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
    var wa = All<FirWanderer>(A); var wb = All<FirWanderer>(B);
    Check(wa.Count == 10, wa.Count + " wanderers live in Bonetown");
    Sim.Run(1f);
    var p0 = wa.Select(w => w.transform.position).ToList();
    var leg0 = wa.Select(w => w.pivots[7].localRotation).ToList();
    float maxLeg = 0f;
    for (int f = 0; f < 60; f++) { Sim.Step(); for (int i = 0; i < wa.Count; i++) maxLeg = Mathf.Max(maxLeg, Quaternion.Angle(leg0[i], wa[i].pivots[7].localRotation)); }
    var speeds = wa.Select((w, i) => Flat(w.transform.position, p0[i]) / (60f * Time.deltaTime)).ToList();
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
        bool ok = p.x < -12.6f && p.x > -47.4f && p.z > -9.2f && p.z < 17.2f && Flat(p, new Vector3(-30f, 0, 4f)) > 2.3f && !(p.x < -43f && p.z > -1.7f && p.z < 7.7f);
        RaycastHit hit;
        var d = p - last[i];
        if (ok && d.magnitude > 0.01f && d.magnitude < 3f && Physics.Raycast(last[i] + Vector3.up * 0.6f, d.normalized, out hit, d.magnitude, 1, QueryTriggerInteraction.Ignore)) { ok = false; what = wa[i].name + " walked through " + hit.collider.name; }
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
    var combos = All<FirSkeletonCombo>(A).Where(c => c.combo >= 6).ToList();
    Check(combos.Count == 4, combos.Count + " Bonetown combo acts: " + string.Join(", ", combos.Select(c => c.title)));
    Check(Sim.BuildLog.Any(l => l.Contains("Bonetown: 10 residents, 4 in the band, 10 wanderers, 4 combo acts, 22 gags")), "the builder placed all of Bonetown");
    var tick = All<FirTicker>(A)[0]; string t0 = tick.text.text; Sim.Run(6.5f);
    Check(tick.text.text != "" && tick.text.text != t0, "the news ticker rolls: " + tick.text.text);
  }
}
