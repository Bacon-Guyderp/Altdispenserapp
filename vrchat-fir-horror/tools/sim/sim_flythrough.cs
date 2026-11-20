// Records a fly-through of the systems-check world: builds it with FirCheckBuilder, has two simulated
// players press every test, and writes every frame (object transforms, label text, the lighting panel,
// players, and camera keyframes) to JSON for flythrough.html to render. Run: bash tools/sim/run_flythrough.sh
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TMPro;
using UdonSharp;
using UnityEngine;

public static class SimFly {
  static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
  static string F(float f) { return f.ToString("0.###", Inv); }
  static string J(string s) { return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\""; }
  static List<T> All<T>(Sim.Client c) where T : Component { var r = new List<T>(); foreach (var g in c.objects) if (!g.destroyed) foreach (var k in g.comps) if (k is T) r.Add((T)k); return r; }

  static Sim.Client A, B;
  static readonly List<Component> objs = new List<Component>();
  static readonly Dictionary<Component, string> lastTr = new Dictionary<Component, string>();
  static readonly Dictionary<Component, string> lastTx = new Dictionary<Component, string>();
  static readonly StringBuilder frames = new StringBuilder();
  static readonly StringBuilder cams = new StringBuilder();
  static readonly List<string> captions = new List<string>();
  static Material panel;
  static int frameCount;
  static int seenEvents;
  static readonly List<string> sounds = new List<string>();

  static void Cam(Vector3 pos, Vector3 look, float hold = 0f) {
    cams.Append((cams.Length > 0 ? "," : "") + "[" + F(Time.time) + "," + F(pos.x) + "," + F(pos.y) + "," + F(pos.z) + "," + F(look.x) + "," + F(look.y) + "," + F(look.z) + "]");
    if (hold > 0f) Sim.Run(hold);
  }
  static void Caption(string text) { captions.Add("[" + F(Time.time) + "," + J(text) + "]"); }

  static void Record() {
    for (; seenEvents < Sim.Events.Count; seenEvents++) {
      var e = Sim.Events[seenEvents];
      if (!e.StartsWith("Alice sound: ")) continue;
      var parts = e.Substring(13).Split(' ');
      sounds.Add("[" + F(Time.time) + "," + J(parts[parts.Length - 1]) + "]");
    }
    var sb = new StringBuilder();
    sb.Append("{\"t\":" + F(Time.time) + ",\"tr\":{");
    bool first = true;
    for (int i = 0; i < objs.Count; i++) {
      var c = objs[i]; var t = c.transform;
      var p = t.position; var q = t.rotation; var s = t.lossyScale;
      string v = "[" + F(p.x) + "," + F(p.y) + "," + F(p.z) + "," + F(q.x) + "," + F(q.y) + "," + F(q.z) + "," + F(q.w) + "," + F(s.x) + "," + F(s.y) + "," + F(s.z) + "," + (c.gameObject.activeInHierarchy ? 1 : 0) + "]";
      string old; if (lastTr.TryGetValue(c, out old) && old == v) continue;
      lastTr[c] = v; sb.Append((first ? "" : ",") + "\"" + i + "\":" + v); first = false;
    }
    sb.Append("},\"tx\":{"); first = true;
    for (int i = 0; i < objs.Count; i++) {
      var tm = objs[i] as TextMeshPro; if (tm == null) continue;
      string old; if (lastTx.TryGetValue(tm, out old) && old == tm.text) continue;
      lastTx[tm] = tm.text; sb.Append((first ? "" : ",") + "\"" + i + "\":" + J(tm.text)); first = false;
    }
    sb.Append("},\"dark\":" + F(panel != null ? panel.GetFloat("_Dark") : 0f) + ",\"pl\":[");
    first = true;
    foreach (var c in Sim.Clients) { var p = c.player; sb.Append((first ? "" : ",") + "[" + J(p.displayName) + "," + F(p.pos.x) + "," + F(p.pos.y) + "," + F(p.pos.z) + "," + F(p.rot.eulerAngles.y) + "]"); first = false; }
    sb.Append("]}");
    frames.Append((frameCount++ > 0 ? ",\n" : "") + sb);
  }

  static void Walk(Sim.Client c, Vector3 to) {
    for (int g = 0; g < 2000; g++) {
      var d = new Vector3(to.x - c.player.pos.x, 0, to.z - c.player.pos.z);
      if (d.magnitude < 0.05f) break;
      c.player.pos = c.player.pos + d.normalized * Mathf.Min(d.magnitude, 2.2f * Time.deltaTime);
      c.player.rot = Quaternion.LookRotation(d);
      Sim.Step();
    }
  }
  static void Face(Sim.Client c, Vector3 at) { var d = at - c.player.pos; d.y = 0; if (d.magnitude > 0.01f) c.player.rot = Quaternion.LookRotation(d); }
  static Vector3 Front(Component b, float dist) { var t = b.transform; var n = t.rotation * Vector3.back; n.y = 0; return t.position + n.normalized * dist; }

  static void Press(Sim.Client c, UdonSharpBehaviour b, string caption, float wait) {
    // stand a little to the side the camera isn't on, so the view of the button stays clear
    var p = b.transform.position; float side = p.x > 0.5f ? 0.65f : -0.65f; if (p.x > 30f) side = -0.65f;
    var stand = Front(b, 1.0f) + (b is FirCheckButton || b is FirCheckTyped ? new Vector3(side, 0, 0) : Vector3.zero); stand.y = 0.05f;
    Walk(c, stand); Face(c, b.transform.position);
    Caption(caption);
    Sim.Press(c, b);
    Sim.Run(wait);
  }

  static readonly Dictionary<string, string> lineText = new Dictionary<string, string> {
    {"traffic_green", "Green: \"Green means go!\""}, {"traffic_yellow", "Yellow: \"Yellow means slow down.\""}, {"traffic_red", "Red: \"And red means... red skeleton. Ha!\""},
    {"knock_1", "Orange: \"Knock knock.\""}, {"knock_2", "Yellow: \"Who's there?\""}, {"knock_3", "Orange: \"Orange.\""}, {"knock_4", "Yellow: \"Orange who?\""},
    {"knock_5", "Orange: \"Orange you glad I didn't say banana?\""}, {"knock_6", "Yellow: \"That's not even a skeleton joke.\""},
    {"bonetones_1", "All three: \"Doo wop, doo wop, doo wop.\""}, {"bonetones_2", "Indigo: \"We're the Bone-Tones!\""}, {"bonetones_3", "All three: \"Shoo be doo wah. Oooooh!\""},
    {"medal_gold", "Gold: \"First place!\""}, {"medal_silver", "Silver: \"Second. Again.\""}, {"medal_brown", "Brown: \"Third! That's bronze, right? Close enough.\""},
    {"upsy_1", "Tiny: \"Pick me up! Pick me up!\""}, {"upsy_2", "Giant: \"Upsy-daisy!\""}, {"upsy_3", "Tiny: \"Wheeeeee!\""}, {"upsy_4", "Giant: \"Oof. You're heavier than you look.\""},
    {"upsy_5", "Tiny: \"I'm big-boned too!\""}, {"finale_1", "Everyone: \"We are the skeleton crew!\""}, {"finale_2", "Everyone: \"Thank you, good night!\""} };

  /// Just the Combo Acts: Alice plays each one from the console while the camera watches the skeletons.
  static void CombosOnly() {
    var combos = All<FirSkeletonCombo>(A).OrderBy(x => x.combo).ToList();
    var console = combos[0].transform.parent;
    var front = console.position + console.rotation * new Vector3(0, 0, -1.0f); front.y = 0.05f;
    Cam(new Vector3(-0.8f, 2.5f, 1.6f), new Vector3(4.0f, 1.0f, -3.5f));
    Caption("Combo Acts: the skeletons perform together");
    Walk(A, front); Face(A, console.position);
    Sim.Run(3f);
    Cam(front + new Vector3(-0.9f, 1.55f, 0.9f), console.position, 2.5f);
    foreach (var co in combos) {
      int riders = co.pivots.Length / 10;
      var c = Vector3.zero; for (int r = 0; r < riders; r++) c = c + co.pivots[r * 10].position; c = c / riders;
      Vector3 camPos, look = c + new Vector3(0, 1.0f, 0);
      if (co.combo == 5) { camPos = new Vector3(-1.4f, 2.6f, -0.4f); look = new Vector3(3.0f, 0.9f, -3.6f); }
      else if (c.x > 5f) camPos = c + new Vector3(-3.4f, 1.45f, 0);
      else camPos = c + new Vector3(0, 1.45f, 3.3f);
      if (co.combo == 4) { camPos = new Vector3(c.x + 0.2f, 2.3f, -2.45f); look = c + new Vector3(0.3f, 2.05f, 0); }
      Cam(camPos, look);
      Caption("Combo: " + co.title);
      Sim.Press(A, co);
      float t0 = Time.time;
      for (int i = 0; i < co.lines.Length; i++) {
        float at = t0 + co.lineStart[i];
        while (Time.time < at) Sim.Step();
        string txt; if (lineText.TryGetValue(co.lines[i].clip.name, out txt)) Caption(txt);
      }
      while (Time.time < t0 + co.length + 0.6f) Sim.Step();
    }
    Cam(new Vector3(-0.8f, 2.5f, 1.6f), new Vector3(4.0f, 1.0f, -3.5f), 2.5f);
  }

  /// A walk through the map: into each room, pressing a few things on the way.
  static void Stills() {
    Cam(new Vector3(-3.5f, 1.7f, -1.0f), new Vector3(-6.2f, 1.6f, -1.0f), 1.2f);   // 0: the lobby's Station
    Cam(new Vector3(90f, 2.2f, 24f), new Vector3(90f, 4f, 60f), 1.2f);             // 1: the park from the gate
    Cam(new Vector3(75f, 2.0f, 50f), new Vector3(75f, 8f, 64f), 1.2f);             // 2: the big wheel
    Cam(new Vector3(98f, 2.5f, 54f), new Vector3(104f, 1.5f, 64f), 1.2f);          // 3: the carousel
    Cam(new Vector3(90f, 2.0f, 33f), new Vector3(80f, 2.0f, 33f), 1.2f);           // 4: the ghost train
    Cam(new Vector3(104f, 2.0f, 42f), new Vector3(116f, 1.5f, 40f), 1.2f);         // 5: stalls and the drop tower
  }

  static void MapTour() {
    var gags = All<FirGag>(A).Where(g => !g.walkIn).ToList();
    var skels = All<FirSkeletonGag>(A).ToList();
    var combos = All<FirSkeletonCombo>(A).ToList();
    var closet = All<FirCloset>(A)[0];
    Action<Vector3, Vector3, string> Shot = (cam, look, cap) => { Cam(cam, look); if (cap != null) Caption(cap); };
    Action<Component, float> PressNear = (b, wait) => {
      var t = b.transform; var fwd = t.rotation * Vector3.forward; fwd.y = 0;
      var stand = t.position + fwd.normalized * 1.6f; stand.y = 0.05f;
      Shot(t.position + fwd.normalized * 3.4f + new Vector3(0, 1.7f, 0) + Vector3.Cross(Vector3.up, fwd.normalized) * 1.4f, t.position + new Vector3(0, 1.1f, 0), null);
      Walk(A, stand); Face(A, t.position);
      Sim.Press(A, (UdonSharpBehaviour)b); Sim.Run(wait);
    };
    Func<float, float, IEnumerable<FirGag>> GagsIn = (z0, z1) => gags.Where(g => g.transform.position.z < z0 && g.transform.position.z > z1);
    Shot(new Vector3(0, 2.2f, -1.0f), new Vector3(0, 1.4f, -8f), "The map: through the lobby's south doorway");
    Walk(A, new Vector3(0, 0.05f, -5.0f)); Sim.Run(0.5f);
    // Gift Wrap Hall
    Shot(new Vector3(1.5f, 2.6f, -7.5f), new Vector3(-1f, 1.0f, -18f), "GIFT WRAP HALL");
    Walk(A, new Vector3(0, 0.05f, -10f)); Sim.Run(1.5f);
    foreach (var g in GagsIn(-6, -26).Take(3)) PressNear(g, 3.2f);
    PressNear(skels.First(k => k.transform.position.z < -6 && k.transform.position.z > -26), 4.5f);
    // Rainbow Hall
    Walk(A, new Vector3(0, 0.05f, -24f));
    Shot(new Vector3(0, 2.4f, -27.5f), new Vector3(0, 1.2f, -38f), "RAINBOW HALL");
    Walk(A, new Vector3(0, 0.05f, -30.2f)); Sim.Run(1.0f);
    var traffic = combos.First(c => c.combo == 0);
    Shot(new Vector3(0, 2.0f, -32.5f), new Vector3(0, 1.4f, -38.3f), "Combo: Traffic Lights");
    Sim.Press(A, traffic); Sim.Run(traffic.length + 0.5f);
    // Toy Workshop
    Walk(A, new Vector3(6.8f, 0.05f, -33f)); Walk(A, new Vector3(6.8f, 0.05f, -40.8f)); Walk(A, new Vector3(0, 0.05f, -41f));
    Walk(A, new Vector3(0, 0.05f, -44f));
    Shot(new Vector3(0, 2.4f, -43.5f), new Vector3(0, 1.0f, -56f), "TOY WORKSHOP");
    Sim.Run(1.5f);
    foreach (var g in GagsIn(-42, -62).Take(3)) PressNear(g, 3.2f);
    // Chimney Lounge
    Walk(A, new Vector3(0, 0.05f, -64f));
    Shot(new Vector3(-2.0f, 2.3f, -64f), new Vector3(3f, 1.0f, -73f), "CHIMNEY LOUNGE");
    Sim.Run(1.5f);
    Shot(new Vector3(-3.6f, 1.8f, -66.2f), closet.transform.position + new Vector3(0, 1.0f, -0.5f), "The RED SKELETON closet");
    Walk(A, new Vector3(-5.2f, 0.05f, -68.5f)); Face(A, closet.transform.position);
    Sim.Press(A, closet); Sim.Run(9.5f);
    // Talent Show
    Walk(A, new Vector3(0, 0.05f, -79f)); Walk(A, new Vector3(0, 0.05f, -85.5f));
    Shot(new Vector3(-3f, 2.6f, -82.5f), new Vector3(0, 1.6f, -95.5f), "TALENT SHOW");
    Sim.Run(2f);
    var aud = All<FirThrowable>(A).ToList();
    Shot(new Vector3(-8.2f, 2.3f, -81.4f), new Vector3(-2f, 0.7f, -85.5f), "THE AUDIENCE: pick one up and throw it");
    Walk(A, new Vector3(-6.3f, 0.05f, -81.6f)); Face(A, aud[0].transform.position);
    Sim.Run(1.2f);
    var thrown = aud.OrderBy(b => Vector3.Distance(b.transform.position, A.player.pos)).First();
    Sim.With(A, () => thrown.OnPickup());
    for (int f = 0; f < 18; f++) { thrown.transform.position = A.player.pos + new Vector3(0.3f, 1.1f, -0.3f); Sim.Step(); }
    // thrown towards the stage: a simple ballistic arc (the simulator has no physics engine)
    var p0 = thrown.transform.position; var vel = new Vector3(1.5f, 4f, -6.5f);
    for (int f = 0; f < 60; f++) {
      float tt = f * Time.deltaTime;
      var pos = p0 + vel * tt + new Vector3(0, -4.9f * tt * tt, 0);
      if (pos.y < 0.6f) { thrown.body.velocity = Vector3.zero; break; }
      thrown.transform.position = pos; thrown.transform.rotation = Quaternion.Euler(f * 21f, 180f, f * 9f);
      thrown.body.velocity = vel + new Vector3(0, -9.8f * tt, 0);
      Sim.Step();
    }
    Sim.Run(2f);
    var upsy = combos.First(c => c.combo == 4);
    var upsyAt = (upsy.pivots[0].position + upsy.pivots[10].position) / 2f;
    Shot(new Vector3(upsyAt.x + 0.3f, 2.4f, -89.5f), upsyAt + new Vector3(0, 2.2f, 0), "Combo: Upsy-Daisy");
    Sim.Press(A, upsy); Sim.Run(upsy.length + 0.5f);
    // Bone Yard
    Walk(A, new Vector3(8.0f, 0.05f, -86f)); Walk(A, new Vector3(8.0f, 0.05f, -97.2f)); Walk(A, new Vector3(7.5f, 0.05f, -100f));
    Shot(new Vector3(7.0f, 2.4f, -100.5f), new Vector3(-1f, 1.0f, -112f), "THE BONE YARD");
    Sim.Run(1.5f);
    foreach (var k in skels.Where(k => k.transform.position.z < -98).Take(2)) PressNear(k, 5f);
    Shot(new Vector3(0, 2.0f, -112f), new Vector3(0, 1.6f, -118f), "...and the way back to the lobby");
    Walk(A, new Vector3(0, 0.05f, -116.6f)); Sim.Run(2f);
  }

  static void Finish() {
    Console.Write("{\"objs\":[");
    var meshes = new Dictionary<Mesh, int>();
    var meshJson = new StringBuilder();
    for (int i = 0; i < objs.Count; i++) {
      var c = objs[i];
      string kind; string extra = "";
      var tm = c as TextMeshPro;
      if (tm != null) {
        var sz = tm.rectTransform.sizeDelta;
        kind = "text"; extra = ",\"w\":" + F(sz.x) + ",\"h\":" + F(sz.y) + ",\"fs\":" + F(tm.fontSizeMax) + ",\"col\":[" + F(tm.color.r) + "," + F(tm.color.g) + "," + F(tm.color.b) + "]";
      } else {
        var mr = (MeshRenderer)c; var mf = c.GetComponent<MeshFilter>();
        if (c.gameObject.primitive != null) kind = c.gameObject.primitive;
        else if (mf != null && mf.sharedMesh != null) {
          int id; if (!meshes.TryGetValue(mf.sharedMesh, out id)) {
            id = meshes.Count; meshes[mf.sharedMesh] = id;
            var m = mf.sharedMesh;
            meshJson.Append((id > 0 ? "," : "") + "{\"v\":[" + string.Join(",", m.verts.Select(v => F(v.x) + "," + F(v.y) + "," + F(v.z))) + "],\"t\":[" + string.Join(",", m.tris) + "]"
              + ",\"uv\":[" + string.Join(",", m.uvs.Select(u => F(u.x) + "," + F(u.y))) + "],\"c\":[" + string.Join(",", m.cols.Select(cc => F(cc.r))) + "]}");
          }
          kind = "mesh"; extra = ",\"mesh\":" + id;
        } else kind = "none";
        var mat = mr.sharedMaterial; Color col = Color.white; float full = 1;
        if (mat != null) { mat.colors.TryGetValue("_Color", out col); full = mat.GetFloat("_Fullbright"); }
        bool bulb = mat != null && mat.shader != null && mat.shader.name.Contains("Bulb");
        if (bulb) full = 1;
        string texName = mat != null && mat.mainTexture != null ? mat.mainTexture.name : "";
        float vc = mat != null ? mat.GetFloat("_VertexColor") : 0;
        Color glow = Color.black; if (mat != null) mat.colors.TryGetValue("_Glow", out glow);
        if (bulb) glow = new Color(col.r * 0.7f, col.g * 0.7f, col.b * 0.7f);
        extra += ",\"col\":[" + F(col.r) + "," + F(col.g) + "," + F(col.b) + "],\"glow\":[" + F(glow.r) + "," + F(glow.g) + "," + F(glow.b) + "],\"full\":" + F(full) + ",\"tex\":" + J(texName) + ",\"vc\":" + F(vc) + ",\"mat\":" + J(mat != null ? mat.name : "");
      }
      Console.Write((i > 0 ? "," : "") + "{\"kind\":" + J(kind) + ",\"name\":" + J(c.name) + extra + "}");
    }
    Console.Write("],\"meshes\":[" + meshJson + "],\"cams\":[" + cams + "],\"captions\":[" + string.Join(",", captions) + "],\"sounds\":[" + string.Join(",", sounds) + "],\"frames\":[" + frames + "]}");
    Console.Error.WriteLine("frames " + frameCount + ", objects " + objs.Count + ", meshes " + meshes.Count + ", problems " + Sim.Failures.Count);
    foreach (var f in Sim.Failures) Console.Error.WriteLine("problem: " + f);
  }

  public static int Main(string[] args) {
    Time.deltaTime = 1f / 30f;
    Sim.SceneBuilder = c => { if (!FirCheckBuilder.Build(true)) Sim.Fail("build failed"); return null; };
    A = Sim.Join("Alice", new Vector3(0, 0.05f, -4));
    B = Sim.Join("Bob", new Vector3(1.2f, 0.05f, -4.4f));
    // what to draw: Alice's copy of the world (mesh renderers and texts)
    foreach (var g in A.objects) {
      if (g.destroyed) continue;
      var mr = g.GetComponent<MeshRenderer>(); if (mr != null) objs.Add(mr);
      var tm = g.GetComponent<TextMeshPro>(); if (tm != null) objs.Add(tm);
    }
    panel = All<FirCheckButton>(A).First(b => b.test == 9).testMaterial;
    Sim.OnStep = Record;

    var buttons = All<FirCheckButton>(A).GroupBy(b => b.test).ToDictionary(g => g.Key, g => g.First());
    var typed = All<FirCheckTyped>(A)[0];
    var skel = All<FirCheckSkeleton>(A)[0];
    var tree = All<FirCheckTree>(A)[0];
    // button shots: back, up and off to the side, so the player pressing it doesn't block the view
    Func<Component, Vector3> CamAt = b => { var p = b.transform.position; float side = p.x > 0.5f ? -1.7f : 1.7f; if (p.x > 30f) side = 1.7f; return Front(b, 3.3f) + new Vector3(side, 0.85f, 0); };
    Func<Component, Vector3> LookAt = b => b.transform.position + new Vector3(0, 0.2f, 0);

    if (args.Length > 0 && args[0] == "combos") { CombosOnly(); Sim.Run(1f); Finish(); return 0; }
    if (args.Length > 0 && args[0] == "stills") { Stills(); Sim.Run(1f); Finish(); return 0; }
    if (args.Length > 0 && args[0] == "map") { MapTour(); Sim.Run(1f); Finish(); return 0; }

    // opening: high in the south-east corner, looking over the lobby at the test wall
    Cam(new Vector3(5.5f, 3.2f, -5.6f), new Vector3(-1, 1.4f, 3));
    Caption("Fir Warning: systems check (simulated: the real builder and scripts, two players)");
    Sim.Run(3f);
    Cam(new Vector3(0, 2.2f, -1.5f), new Vector3(0, 1.7f, 6), 2.5f);
    Caption("Each button tests one thing the new map needs, and writes PASS or FAIL on itself");
    Sim.Run(1.5f);

    foreach (int test in new[] { 2, 3, 4 }) {
      var b = buttons[test];
      Cam(CamAt(b), LookAt(b));
      Press(A, b, b.title, 1.6f);
    }
    Cam(CamAt(typed), LookAt(typed));
    Press(A, typed, "5. Typed script call", 1.6f);
    Cam(CamAt(buttons[6]), LookAt(buttons[6]));
    Press(A, buttons[6], "6. Network sync: Alice adds one to a shared counter", 1.2f);
    var hubBoard = All<TextMeshPro>(A).First(t => t.name == "HubBoard");
    Cam(new Vector3(-2.4f, 2.0f, 3.9f), hubBoard.transform.position + new Vector3(0, -0.2f, -0.4f), 0f);
    Caption("The hub board on the west wall: Bob presses test 6 too, and the counter reaches 2 for both");
    Walk(B, Front(All<FirCheckButton>(B).First(x => x.test == 6), 1.0f) + new Vector3(0.6f, 0, 0));
    Sim.Press(B, All<FirCheckButton>(B).First(x => x.test == 6));
    Sim.Run(2.5f);

    Cam(new Vector3(-0.9f, 1.75f, -0.7f), skel.transform.position + new Vector3(0, 1.0f, 0));
    Press(A, buttons[7], "7. Another script tells your skeleton to wave", 3.5f);
    Cam(new Vector3(0.9f, 1.8f, -0.8f), tree.transform.position + new Vector3(0, 1.1f, 0));
    Press(A, buttons[8], "8. Another script tells the tree to talk", 3f);
    var pan = All<MeshRenderer>(A).First(r => r.name == "TestPanel");
    Cam(new Vector3(3.2f, 1.9f, 3.2f), pan.transform.position);
    Press(A, buttons[9], "9. Lighting: the test panel on the east wall goes dark", 2.5f);
    Cam(CamAt(buttons[10]), LookAt(buttons[10]));
    Press(A, buttons[10], "10. Sound", 1.2f);

    Cam(CamAt(buttons[1]), LookAt(buttons[1]));
    Walk(A, Front(buttons[1], 1.0f) + new Vector3(-0.65f, 0, 0)); Face(A, buttons[1].transform.position);
    Caption("1. Teleport to the test room");
    Sim.Press(A, buttons[1]);
    Cam(new Vector3(40, 2.2f, -3.8f), new Vector3(40, 1.4f, 3), 2.5f);
    Cam(CamAt(buttons[11]), LookAt(buttons[11]));
    Press(A, buttons[11], "11. And back to the lobby", 0.3f);
    Cam(new Vector3(0.5f, 2.4f, -5.4f), new Vector3(0, 1.4f, 2), 1.5f);

    Cam(new Vector3(-1.2f, 1.55f, -0.2f), skel.transform.position + new Vector3(0, 1.0f, 0));
    Press(A, skel, "The white skeleton (your model): press it directly", 3.5f);
    Cam(new Vector3(1.2f, 1.7f, -0.3f), tree.transform.position + new Vector3(0, 1.1f, 0));
    Press(A, tree, "Press the tree directly", 3f);

    // the rainbow of skeletons along the east wall
    var rainbow = All<FirRainbowSkeleton>(A).Where(x => x.act < 7).OrderBy(x => x.act).ToList();
    string[] says = { "\"Red skeleton. Ha!\"", "\"Knock knock. Orange you glad I'm just bones?\"", "\"I'm not yellow. I just haven't got the guts.\"",
                      "\"Feeling a little green. Too many pine needles.\"", "\"I've got the blues. Nobody to dance with.\"",
                      "\"Indigo? Nobody even knows what colour that is.\"", "\"Sorry. I'm a shrinking violet. I go to pieces.\"" };
    Cam(new Vector3(1.0f, 2.3f, -5.4f), new Vector3(6.2f, 1.2f, -0.8f));
    Caption("A rainbow of skeletons, each with its own voiced gag");
    Walk(A, new Vector3(4.6f, 0.05f, -5.0f));
    Sim.Run(1.5f);
    for (int i = 0; i < rainbow.Count; i++) {
      var rs = rainbow[i];
      var at = rs.transform.position;
      Cam(new Vector3(3.4f, 1.6f, Mathf.Max(-5.5f, at.z - 0.3f)), at + new Vector3(0, 0.95f, 0));
      var stand = new Vector3(5.1f, 0.05f, at.z + 0.75f);
      Walk(A, stand); Face(A, at);
      Caption(says[i]);
      Sim.Press(A, rs);
      float clip = rs.voice != null && rs.voice.clip != null ? rs.voice.clip.length : 3f;
      Sim.Run(Mathf.Max(4.2f, clip + 1.6f));
    }
    Cam(new Vector3(0.5f, 2.2f, -1.8f), new Vector3(6.2f, 1.1f, -1.7f), 2.5f);

    // the second line-up along the south wall
    var row2 = All<FirRainbowSkeleton>(A).Where(x => x.act >= 8).OrderBy(x => x.act).ToList();
    string[] says2 = { "\"Stop it, I'm ticklish! Tickled pink, and I haven't even got skin.\"", "\"Ooooh. I'm the teal skeleton. Teal death do us part.\"",
                       "\"Gold skeleton. Worth my weight in gold. I weigh nothing!\"", "\"Silver skeleton. I came second in the skeleton contest.\"",
                       "\"Black skeleton. I'm the one hiding in your closet. Boo.\"", "\"Brown skeleton. I'm not hungry. I've got no stomach for it.\"",
                       "\"I glow in the dark. Someone turn off the lights. Please?\"", "\"I'm every colour! I just couldn't decide.\"",
                       "\"Hi! I'm just a little skele-tiny!\"", "\"I'm not fat. I'm big-boned.\"" };
    Cam(new Vector3(-1.0f, 2.4f, -0.2f), new Vector3(-0.7f, 1.0f, -5.3f));
    Caption("And ten more along the south wall");
    Walk(A, new Vector3(-5.9f, 0.05f, -3.6f));
    Sim.Run(1.5f);
    for (int i = 0; i < row2.Count; i++) {
      var rs = row2[i];
      var at = rs.transform.position;
      Cam(new Vector3(Mathf.Clamp(at.x + 0.45f, -5.6f, 5.6f), 1.6f, -2.3f), at + new Vector3(0, 0.95f * rs.transform.localScale.y, 0));
      Walk(A, new Vector3(at.x - 0.85f, 0.05f, -4.3f)); Face(A, at);
      Caption(says2[i]);
      Sim.Press(A, rs);
      float clip = rs.voice != null && rs.voice.clip != null ? rs.voice.clip.length : 3f;
      Sim.Run(Mathf.Max(4.2f, clip + 1.4f));
    }

    Walk(A, new Vector3(0, 0.05f, -3.5f));
    Cam(new Vector3(0, 2.1f, -1.0f), new Vector3(0, 1.75f, 6));
    Caption("Every test says PASS in the simulator. Now the same check needs to run in your VRChat");
    Sim.Run(4.5f);
    Cam(new Vector3(-5.6f, 3.2f, -5.6f), new Vector3(1, 1.3f, 3), 3f);

    Finish();
    return 0;
  }
}
