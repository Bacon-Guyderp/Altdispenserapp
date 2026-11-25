#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Four wings a room: every Station area's first wing leads, through a doorway in its far wall, to a second, a third
/// and a fourth, chained north. Their contents come from Data/wings_more.txt (also read by tools/make_voices.py):
///   W|area|title|sub|floor|wall|ceil|height|style|accent        a new wing, chained onto that area's last one
///   F|name|hex|act|voice|line                                    one of its three people
///   G|name|kind|shape|sx,sy,sz|mat|amount|lift|voice|line        one of its three gags
/// The n-th W line's clips are More/mo_x{n:000}f0..f2 (people) and More/mo_x{n:000}g0..g2 (gags).
/// </summary>
public static partial class FirCheckBuilder
{
    static readonly Dictionary<string, int> chainCount = new Dictionary<string, int>();

    /// <summary>Opens a doorway in the far wall of `prevTitle` (the area's last wing) and builds the next wing beyond it.</summary>
    static Transform ChainWing(Transform root, string area, string prevTitle, int link, float x0, float x1, float zWall, float depth, float h,
                               string floor, string wall, string ceil, string title, Color col, string sub)
    {
        var ag = root.Find(area);
        var prev = ag != null ? ag.Find(prevTitle) : null;
        if (prev == null) { report.Add("problem: no wing " + prevTitle + " in " + area + " to build on"); return null; }
        float cx = (x0 + x1) / 2f, w = x1 - x0, z2 = zWall + depth, cz = (zWall + z2) / 2f, dx = x1 - 3.5f;
        var old = prev.Find("WallN");
        Material prevWall = M(wall); float prevH = h;
        if (old != null)
        {
            var r = old.GetComponent<MeshRenderer>(); if (r != null && r.sharedMaterial != null) prevWall = r.sharedMaterial;
            var bc = old.GetComponent<BoxCollider>(); if (bc != null) prevH = bc.size.y;
            Object.DestroyImmediate(old.gameObject);
        }
        float door = 4f, doorH = Mathf.Min(4f, Mathf.Min(prevH, h));
        float wl = (dx - door / 2f) - (x0 - 0.2f), wr = (x1 + 0.2f) - (dx + door / 2f);
        Box(prev, "WallN west", V(x0 - 0.2f + wl / 2f, prevH / 2f, zWall + 0.1f), V(wl, prevH, 0.2f), prevWall);
        Box(prev, "WallN east", V(x1 + 0.2f - wr / 2f, prevH / 2f, zWall + 0.1f), V(wr, prevH, 0.2f), prevWall);
        if (prevH > doorH) Box(prev, "WallN lintel", V(dx, (doorH + prevH) / 2f, zWall + 0.1f), V(door, prevH - doorH, 0.2f), prevWall);
        Text("Wing sign", prev, V(dx, Mathf.Min(doorH + 0.45f, prevH - 0.3f), zWall - 0.02f), Face(Vector3.back), V2(5f, 0.5f), 0.32f, "<b>NEXT: " + title + "  ></b>").color = col;

        var g = Group(ag, title, Vector3.zero);
        Box(g, "Floor", V(cx, -0.1f, cz), V(w, 0.2f, depth), M(floor));
        Box(g, "Ceiling", V(cx, h + 0.1f, cz), V(w, 0.2f, depth), M(ceil));
        Box(g, "WallW", V(x0 - 0.1f, h / 2f, cz), V(0.2f, h, depth + 0.4f), M(wall));
        Box(g, "WallE", V(x1 + 0.1f, h / 2f, cz), V(0.2f, h, depth + 0.4f), M(wall));
        Box(g, "WallN", V(cx, h / 2f, z2 + 0.1f), V(w + 0.4f, h, 0.2f), M(wall));
        if (h > prevH) Box(g, "WallS upper", V(cx, (prevH + h) / 2f, zWall + 0.1f), V(w + 0.4f, h - prevH, 0.2f), M(wall));
        float ty = Mathf.Max(Mathf.Min(h - 1.2f, 5.2f), doorH + 0.9f);
        if (ty > h - 0.5f) ty = h - 0.5f;
        Text("Title", g, V(cx - 4f, ty, zWall + 0.25f), Face(Vector3.forward), V2(Mathf.Min(w * 0.6f, 14f), 0.9f), 0.6f, "<b>" + title + "</b>").color = col;
        Text("Sub", g, V(cx - 4f, ty - 0.65f, zWall + 0.25f), Face(Vector3.forward), V2(Mathf.Min(w * 0.6f, 14f), 0.4f), 0.18f, sub).color = Color.white;
        var arrive = Marker(g, "Wing arrive", V(dx, 0.05f, zWall + 2.2f), 0f);
        var areaArrive = ag.Find("Arrive");
        TeleportButton(ag, "To " + title, V(cx + 2.7f, 1.3f + link * 0.38f, 20.15f), Vector3.forward, arrive, V(1.6f, 0.3f, 0.1f));
        TeleportButton(g, "Back to the lobby", V(cx - 3.4f, 1.3f, zWall + 0.35f), Vector3.forward, lobbyArrive, V(1.0f, 0.3f, 0.1f));
        if (areaArrive != null) TeleportButton(g, "Back to the entrance", V(cx + 3.4f, 1.3f, zWall + 0.35f), Vector3.forward, areaArrive, V(1.2f, 0.3f, 0.1f));
        wings.Add(new KeyValuePair<string, float[]>(area, new[] { x0, zWall, x1, z2, dx, areaArrive != null ? areaArrive.position.x : cx, areaArrive != null ? areaArrive.position.z : 22f }));
        wingNames.Add(title);
        return g;
    }

    /// <summary>
    /// Fills a wing by style: a row of features along the back (kept clear of the next doorway in the east), a few along
    /// the west wall, and lights. Never in the people's and gags' places.
    /// </summary>
    static void WingDecor(Transform g, string style, float x0, float x1, float z, float depth, float h, string accent)
    {
        float bz = z + depth - 3f, xa = x0 + 2.2f, xb = x1 - 7.5f;
        int n = Mathf.Max(1, Mathf.FloorToInt((xb - xa) / 4.5f));
        for (int i = 0; i <= n; i++)
        {
            var p = V(Mathf.Lerp(xa, xb, n == 0 ? 0f : i / (float)n), 0, bz + (i % 2) * 0.8f);
            DecorUnit(g, style, p, accent, i, true, h);
        }
        for (int i = 0; i < 3; i++) DecorUnit(g, style, V(x0 + 1.4f, 0, z + 5f + i * 5f), accent, i + 7, false, h);
        float ly = h - 0.3f;
        for (int i = 0; i < 3; i++) Prim(PrimitiveType.Sphere, "Wing light", g, V(Mathf.Lerp(x0 + 4f, x1 - 4f, i / 2f), ly, z + depth / 2f), Vector3.one * 0.35f, M(h > 12f ? "GlowYellow" : "GlowWarm"));
    }

    static void DecorUnit(Transform g, string style, Vector3 p, string accent, int i, bool back, float h)
    {
        string a = accent;
        switch (style)
        {
            case "shelves":
                Box(g, "Shelf unit", p + V(0, 1.2f, 0), V(2.6f, 2.4f, 0.5f), M("DeskWood"), false);
                for (int k = 0; k < 6; k++) Prim(PrimitiveType.Cube, "Shelf item", g, p + V(-1f + (k % 3) * 1f, 0.6f + (k / 3) * 0.9f, -0.3f), V(0.4f, 0.4f, 0.3f), M(k % 2 == 0 ? a : "Cardboard"));
                break;
            case "garden":
                DPrim(PrimitiveType.Cylinder, "Planter", g, p + V(0, 0.3f, 0), V(1.4f, 0.3f, 1.4f), M("Brick"));
                DPrim(PrimitiveType.Sphere, "Bush", g, p + V(0, 1f, 0), V(1.6f, 1.2f, 1.6f), M("GiftGreen"));
                Prim(PrimitiveType.Sphere, "Bloom", g, p + V(0.3f, 1.6f, -0.3f), Vector3.one * 0.4f, M(a));
                break;
            case "machines":
                Box(g, "Machine", p + V(0, 1f, 0), V(2f, 2f, 1.2f), M("Metal"), false);
                Prim(PrimitiveType.Cube, "Panel", g, p + V(0, 1.2f, -0.62f), V(1.2f, 0.6f, 0.02f), M(a));
                DPrim(PrimitiveType.Cylinder, "Pipe", g, p + V(0.8f, 2.6f, 0), V(0.2f, 0.6f, 0.2f), M("Metal"));
                break;
            case "water":
                Box(g, "Pool", p + V(0, 0.02f, 0), V(3.4f, 0.04f, 2f), M("PoolBlue"), false);
                Prim(PrimitiveType.Sphere, "Lily", g, p + V(0.5f, 0.06f, 0.2f), V(0.5f, 0.02f, 0.5f), M(a));
                break;
            case "stage":
                Box(g, "Riser", p + V(0, 0.25f, 0), V(3.6f, 0.5f, 1.6f), M("Stage"), false);
                DPrim(PrimitiveType.Cylinder, "Spotlight", g, p + V(0, Mathf.Min(h - 0.6f, 3.4f), 0.4f), V(0.4f, 0.3f, 0.4f), M(a));
                break;
            case "dining":
                DPrim(PrimitiveType.Cylinder, "Table", g, p + V(0, 0.75f, 0), V(1.4f, 0.03f, 1.4f), M("DeskWood"));
                DPrim(PrimitiveType.Cylinder, "Table leg", g, p + V(0, 0.37f, 0), V(0.12f, 0.37f, 0.12f), M("Black"));
                foreach (float cx in new[] { -0.9f, 0.9f }) Box(g, "Stool", p + V(cx, 0.25f, 0), V(0.45f, 0.5f, 0.45f), M(a), false);
                break;
            case "crates":
                for (int k = 0; k < 3; k++) Box(g, "Crate", p + V(-0.7f + (k % 2) * 1.2f, 0.45f + (k / 2) * 0.9f, 0), V(1f, 0.9f, 1f), M(k == 2 ? a : "Cardboard"), false);
                break;
            case "pillars":
                DPrim(PrimitiveType.Cylinder, "Pillar", g, p + V(0, Mathf.Min(h, 6f) / 2f, 0), V(0.8f, Mathf.Min(h, 6f) / 2f, 0.8f), M(a));
                break;
            case "forest":
                DPrim(PrimitiveType.Cylinder, "Trunk", g, p + V(0, 1.6f, 0), V(0.5f, 1.6f, 0.5f), M("Trunk"));
                DPrim(PrimitiveType.Sphere, "Leaves", g, p + V(0, 3.6f, 0), V(2.6f, 2.2f, 2.6f), M(a));
                break;
            case "lab":
                Box(g, "Bench", p + V(0, 0.45f, 0), V(2.6f, 0.9f, 1f), M("LabWhite"), false);
                for (int k = 0; k < 3; k++) DPrim(PrimitiveType.Cylinder, "Flask", g, p + V(-0.8f + k * 0.8f, 1.1f, 0), V(0.2f, 0.2f, 0.2f), M(k == 1 ? a : "Glass"));
                break;
            case "beach":
                Box(g, "Towel", p + V(0, 0.02f, 0), V(1f, 0.02f, 2f), M(a), false);
                DPrim(PrimitiveType.Cylinder, "Parasol pole", g, p + V(0.8f, 1.2f, 0), V(0.06f, 1.2f, 0.06f), M("CaneWhite"));
                DPrim(PrimitiveType.Cylinder, "Parasol", g, p + V(0.8f, 2.4f, 0), V(2.2f, 0.08f, 2.2f), M(a));
                break;
            case "snow":
                DPrim(PrimitiveType.Sphere, "Snow drift", g, p, V(2.4f, 1f, 1.8f), M("Snow"));
                DPrim(PrimitiveType.Cube, "Ice", g, p + V(0.5f, 0.6f, 0), V(0.8f, 1.2f, 0.8f), M("Glass"), Quaternion.Euler(0, i * 30f, 0));
                break;
            case "cave":
                DPrim(PrimitiveType.Sphere, "Boulder", g, p + V(0, 0.6f, 0), V(2f, 1.4f, 1.6f), M("CaveRock"));
                Prim(PrimitiveType.Sphere, "Crystal", g, p + V(0.6f, 1.3f, -0.4f), V(0.4f, 0.7f, 0.4f), M(a));
                break;
            case "party":
                for (int k = 0; k < 4; k++) Prim(PrimitiveType.Sphere, "Balloon", g, p + V(-0.6f + k * 0.4f, 2f + (k % 2) * 0.3f, 0), V(0.4f, 0.5f, 0.4f), M(k % 2 == 0 ? a : "GiftBlue"));
                Box(g, "Gift", p + V(0, 0.3f, 0), V(0.6f, 0.6f, 0.6f), M(a), false);
                break;
            case "office":
                Box(g, "Desk", p + V(0, 0.4f, 0), V(1.8f, 0.8f, 0.9f), M("DeskWood"), false);
                Box(g, "Screen", p + V(0, 1.05f, 0.2f), V(0.8f, 0.5f, 0.05f), M("Black"), false);
                Box(g, "Chair", p + V(0, 0.25f, -0.7f), V(0.5f, 0.5f, 0.5f), M(a), false);
                break;
            case "library":
                Box(g, "Bookcase", p + V(0, 1.2f, 0), V(2.4f, 2.4f, 0.5f), M("DoorWood"), false);
                for (int k = 0; k < 10; k++) Prim(PrimitiveType.Cube, "Book", g, p + V(-1f + (k % 5) * 0.5f, 0.6f + (k / 5) * 0.9f, -0.28f), V(0.12f, 0.4f, 0.3f), M(new[] { "Book1", "Book2", "Book3", a }[k % 4]));
                break;
            case "arcade":
                Box(g, "Arcade cabinet", p + V(0, 0.9f, 0), V(0.9f, 1.8f, 0.8f), M("Black"), false);
                Prim(PrimitiveType.Cube, "Arcade screen", g, p + V(0, 1.35f, -0.41f), V(0.6f, 0.45f, 0.02f), M(a));
                break;
            case "space":
                Box(g, "Console", p + V(0, 0.5f, 0), V(2f, 1f, 0.8f), M("LabGrey"), false);
                for (int k = 0; k < 4; k++) Prim(PrimitiveType.Cube, "Console light", g, p + V(-0.6f + k * 0.4f, 1.02f, -0.2f), V(0.15f, 0.04f, 0.15f), M(k % 2 == 0 ? a : "GlowGreen"));
                break;
            case "ruins":
                DPrim(PrimitiveType.Cube, "Broken column", g, p + V(0, 0.8f, 0), V(0.9f, 1.6f + (i % 3) * 0.6f, 0.9f), M("Stone"), Quaternion.Euler(0, i * 20f, i % 2 == 0 ? 4f : -6f));
                Prim(PrimitiveType.Sphere, "Moss", g, p + V(0.3f, 0.1f, 0.4f), V(0.8f, 0.2f, 0.6f), M(a));
                break;
            case "farm":
                Box(g, "Hay bale", p + V(0, 0.4f, 0), V(1.4f, 0.8f, 1f), M("CornYellow"), false);
                DPrim(PrimitiveType.Cylinder, "Fence post", g, p + V(1.1f, 0.5f, 0), V(0.1f, 0.5f, 0.1f), M("DeskWood"));
                break;
            case "neon":
                Box(g, "Neon sign", p + V(0, 2.4f, 0.2f), V(2.4f, 0.5f, 0.06f), M(a), false);
                Box(g, "Speaker", p + V(0, 0.6f, 0), V(0.8f, 1.2f, 0.7f), M("Black"), false);
                break;
            default:   // "bones"
                Prim(PrimitiveType.Sphere, "Skull", g, p + V(0, 1.1f, 0), V(0.5f, 0.55f, 0.5f), M("BoneWhite"));
                DPrim(PrimitiveType.Cylinder, "Plinth", g, p + V(0, 0.4f, 0), V(0.8f, 0.4f, 0.8f), M(a));
                break;
        }
    }

    static void BuildMoreWings(Transform root)
    {
        chainCount.Clear();
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/wings_more.txt");
        if (ta == null) { report.Add("problem: Data/wings_more.txt is missing"); return; }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int n = -1, fi = 0, gi = 0, built = 0;
        Transform g = null; float x0 = 0, x1 = 0, z = 0, sx = 1f;
        var fp = new[] { V(-9f, 0, 9f), V(0, 0, 14f), V(9f, 0, 9f) };
        var fy = new[] { 160f, 180f, 200f };
        var gp = new[] { V(-12f, 0, 18f), V(5f, 0, 20f), V(13f, 0, 15f) };
        foreach (var raw in ta.text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Trim() == "" || line.StartsWith("#")) continue;
            var p = line.Split('|');
            if (p[0] == "W")
            {
                n++; fi = 0; gi = 0; g = null;
                string area = p[1];
                int last = -1;
                for (int i = wings.Count - 1; i >= 0; i--) if (wings[i].Key == area) { last = i; break; }
                if (last < 0) { report.Add("problem: " + area + " has no wing to chain onto"); continue; }
                var b = wings[last].Value;
                x0 = b[0]; x1 = b[2]; z = b[3];
                int link = chainCount.ContainsKey(area) ? chainCount[area] + 1 : 1;
                chainCount[area] = link;
                float h = float.Parse(p[7], inv);
                var pal = new[] { new Color(1f, 0.8f, 0.4f), new Color(0.6f, 0.85f, 1f), new Color(0.7f, 1f, 0.6f), new Color(1f, 0.6f, 0.8f), new Color(0.85f, 0.7f, 1f), new Color(1f, 1f, 0.6f) };
                var col = pal[n % pal.Length];
                g = ChainWing(root, area, wingNames[last], link, x0, x1, z, 26f, h, p[4], p[5], p[6], p[2], col, p[3]);
                if (g == null) continue;
                sx = Mathf.Min(1f, ((x1 - x0) / 2f - 2.5f) / 13f);
                WingDecor(g, p[8], x0, x1, z, 26f, h, p[9]);
                built++;
            }
            else if (p[0] == "F" && g != null && fi < 3)
            {
                float cx = (x0 + x1) / 2f;
                var at = V(cx + fp[fi].x * sx, 0, z + fp[fi].z);
                Folk(g, p[1], p[2], int.Parse(p[3]), at, fy[fi], "More/mo_x" + n.ToString("000") + "f" + fi);
                fi++;
            }
            else if (p[0] == "G" && g != null && gi < 3)
            {
                float cx = (x0 + x1) / 2f;
                var sz = p[4].Split(',').Select(x => float.Parse(x, inv)).ToArray();
                var shape = p[3] == "cube" ? PrimitiveType.Cube : p[3] == "cyl" ? PrimitiveType.Cylinder : p[3] == "cap" ? PrimitiveType.Capsule : PrimitiveType.Sphere;
                var at = V(cx + gp[gi].x * sx, 0, z + gp[gi].z);
                var gag = Gk(g, p[1], at, 180f, "More/mo_x" + n.ToString("000") + "g" + gi, p[2], shape, V(sz[0], sz[1], sz[2]), p[5], float.Parse(p[6], inv));
                float lift = float.Parse(p[7], inv);
                if (lift > 0.001f) gag.target.localPosition = V(0, lift, 0);
                gi++;
            }
        }
        report.Add("Four wings a room: " + built + " more wings, chained north from each area's first (every Station area and new room now has 4)");
    }
}
#endif
