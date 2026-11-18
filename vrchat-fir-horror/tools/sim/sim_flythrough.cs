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

    var buttons = All<FirCheckButton>(A).ToDictionary(b => b.test);
    var typed = All<FirCheckTyped>(A)[0];
    var skel = All<FirCheckSkeleton>(A)[0];
    var tree = All<FirCheckTree>(A)[0];
    // button shots: back, up and off to the side, so the player pressing it doesn't block the view
    Func<Component, Vector3> CamAt = b => { var p = b.transform.position; float side = p.x > 0.5f ? -1.7f : 1.7f; if (p.x > 30f) side = 1.7f; return Front(b, 3.3f) + new Vector3(side, 0.85f, 0); };
    Func<Component, Vector3> LookAt = b => b.transform.position + new Vector3(0, 0.2f, 0);

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
    Press(A, skel, "Press your skeleton directly", 3.5f);
    Cam(new Vector3(1.2f, 1.7f, -0.3f), tree.transform.position + new Vector3(0, 1.1f, 0));
    Press(A, tree, "Press the tree directly", 3f);

    Walk(A, new Vector3(0, 0.05f, -3.5f));
    Cam(new Vector3(0, 2.1f, -1.0f), new Vector3(0, 1.75f, 6));
    Caption("Every test says PASS in the simulator. Now the same check needs to run in your VRChat");
    Sim.Run(4.5f);
    Cam(new Vector3(-5.6f, 3.2f, -5.6f), new Vector3(1, 1.3f, 3), 3f);

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
            meshJson.Append((id > 0 ? "," : "") + "{\"v\":[" + string.Join(",", m.verts.Select(v => F(v.x) + "," + F(v.y) + "," + F(v.z))) + "],\"t\":[" + string.Join(",", m.tris) + "]}");
          }
          kind = "mesh"; extra = ",\"mesh\":" + id;
        } else kind = "none";
        var mat = mr.sharedMaterial; Color col = Color.white; float full = 1;
        if (mat != null) { mat.colors.TryGetValue("_Color", out col); full = mat.GetFloat("_Fullbright"); }
        extra += ",\"col\":[" + F(col.r) + "," + F(col.g) + "," + F(col.b) + "],\"full\":" + F(full) + ",\"mat\":" + J(mat != null ? mat.name : "");
      }
      Console.Write((i > 0 ? "," : "") + "{\"kind\":" + J(kind) + ",\"name\":" + J(c.name) + extra + "}");
    }
    Console.Write("],\"meshes\":[" + meshJson + "],\"cams\":[" + cams + "],\"captions\":[" + string.Join(",", captions) + "],\"sounds\":[" + string.Join(",", sounds) + "],\"frames\":[" + frames + "]}");
    Console.Error.WriteLine("frames " + frameCount + ", objects " + objs.Count + ", meshes " + meshes.Count + ", problems " + Sim.Failures.Count);
    foreach (var f in Sim.Failures) Console.Error.WriteLine("problem: " + f);
    return 0;
  }
}
