#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The expansion: every Station area gets a new wing through a doorway in its far (north) wall, with a
/// shortcut button beside its arrival point. Each wing has its own people (voiced and captioned), its own
/// gags and something to do. Voices: tools/make_voices.py (AREAS3, Audio/More).
/// </summary>
public static partial class FirCheckBuilder
{
    /// <summary>Every wing built: area name, wing name, its floor rectangle (x0, z0, x1, z1), arrival point.</summary>
    public static readonly List<KeyValuePair<string, float[]>> wings = new List<KeyValuePair<string, float[]>>();
    public static readonly List<string> wingNames = new List<string>();

    /// <summary>
    /// Opens a 4 m doorway in an area's north wall and builds a wing beyond it, the same width, `depth` deep.
    /// Returns the wing's group; P(dx, dz) positions are relative to the doorway (x = area centre, z = wall).
    /// </summary>
    static Transform Wing(Transform root, string area, float x0, float x1, float zWall, float depth, float h, string floor, string wall, string ceil,
                          string title, Color col, string sub, float doorX = float.NaN)
    {
        var ag = root.Find(area);
        if (ag == null) { report.Add("problem: no area called " + area + " for its new wing"); ag = Group(root, area, Vector3.zero); }
        float cx = (x0 + x1) / 2f, w = x1 - x0, z2 = zWall + depth, cz = (zWall + z2) / 2f;
        float dx = float.IsNaN(doorX) ? cx : doorX;   // the doorway sits off-centre where the area has something against that wall
        // the old north wall comes down; the new one has a doorway in the middle
        var old = ag.Find("WallN");
        if (old == null)   // some older areas just call all four walls "Wall": take the one along this edge
            for (int i = 0; i < ag.childCount; i++)
            {
                var c = ag.GetChild(i);
                if (c.name == "Wall" && Mathf.Abs(c.localPosition.z - (zWall + 0.1f)) < 0.05f) { old = c; break; }
            }
        Material areaWall = M(wall);
        float areaH = h;
        if (old != null)
        {
            var r = old.GetComponent<MeshRenderer>();
            if (r != null && r.sharedMaterial != null) areaWall = r.sharedMaterial;
            var bc = old.GetComponent<BoxCollider>();
            if (bc != null) areaH = bc.size.y;
            Object.DestroyImmediate(old.gameObject);
        }
        float door = 4f, doorH = Mathf.Min(4f, areaH);
        float wl = (dx - door / 2f) - (x0 - 0.2f), wr = (x1 + 0.2f) - (dx + door / 2f);
        Box(ag, "WallN west", new Vector3(x0 - 0.2f + wl / 2f, areaH / 2f, zWall + 0.1f), new Vector3(wl, areaH, 0.2f), areaWall);
        Box(ag, "WallN east", new Vector3(x1 + 0.2f - wr / 2f, areaH / 2f, zWall + 0.1f), new Vector3(wr, areaH, 0.2f), areaWall);
        if (areaH > doorH) Box(ag, "WallN lintel", new Vector3(dx, (doorH + areaH) / 2f, zWall + 0.1f), new Vector3(door, areaH - doorH, 0.2f), areaWall);
        Text("Wing sign", ag, new Vector3(dx, doorH + 0.45f, zWall - 0.02f), Face(Vector3.back), new Vector2(6f, 0.6f), 0.4f, "<b>NEW: " + title + "  ></b>").color = col;

        // the wing
        var g = Group(ag, title, Vector3.zero);
        Box(g, "Floor", new Vector3(cx, -0.1f, cz), new Vector3(w, 0.2f, depth), M(floor));
        Box(g, "Ceiling", new Vector3(cx, h + 0.1f, cz), new Vector3(w, 0.2f, depth), M(ceil));
        Box(g, "WallW", new Vector3(x0 - 0.1f, h / 2f, cz), new Vector3(0.2f, h, depth + 0.4f), M(wall));
        Box(g, "WallE", new Vector3(x1 + 0.1f, h / 2f, cz), new Vector3(0.2f, h, depth + 0.4f), M(wall));
        Box(g, "WallN", new Vector3(cx, h / 2f, z2 + 0.1f), new Vector3(w + 0.4f, h, 0.2f), M(wall));
        if (h > areaH) Box(g, "WallS upper", new Vector3(cx, (areaH + h) / 2f, zWall + 0.1f), new Vector3(w + 0.4f, h - areaH, 0.2f), M(wall));
        float ty = Mathf.Min(h - 1.2f, 5.2f);
        Text("Title", g, new Vector3(cx, Mathf.Max(ty, doorH + 1.1f), zWall + 0.25f), Face(Vector3.forward), new Vector2(Mathf.Min(w * 0.8f, 16f), 1.0f), 0.7f, "<b>" + title + "</b>").color = col;
        Text("Sub", g, new Vector3(cx, Mathf.Max(ty, doorH + 1.1f) - 0.75f, zWall + 0.25f), Face(Vector3.forward), new Vector2(Mathf.Min(w * 0.8f, 16f), 0.4f), 0.2f, sub).color = Color.white;
        var arrive = Marker(g, "Wing arrive", new Vector3(dx, 0.05f, zWall + 2.2f), 0f);
        // shortcuts: from the area's arrival point into the wing, and back out again
        var areaArrive = ag.Find("Arrive");
        TeleportButton(ag, "To " + title, new Vector3(cx + 2.7f, 1.3f, 20.15f), Vector3.forward, arrive, new Vector3(1.6f, 0.3f, 0.1f));
        TeleportButton(g, "Back to the lobby", new Vector3(cx - 3.4f, 1.3f, zWall + 0.35f), Vector3.forward, lobbyArrive, new Vector3(1.0f, 0.3f, 0.1f));
        if (areaArrive != null) TeleportButton(g, "Back to the entrance", new Vector3(cx + 3.4f, 1.3f, zWall + 0.35f), Vector3.forward, areaArrive, new Vector3(1.2f, 0.3f, 0.1f));
        wings.Add(new KeyValuePair<string, float[]>(area, new[] { x0, zWall, x1, z2, dx, areaArrive != null ? areaArrive.position.x : cx, areaArrive != null ? areaArrive.position.z : 22f }));
        wingNames.Add(title);
        return g;
    }

    /// <summary>Sets a gag's moving prop and its motion in one go.</summary>
    static FirGag Move(FirGag gag, Transform target, Vector3 moveBy, Vector3 rotateBy, float bob = 0f, float wobble = 0f, float spin = 0f, float scaleBy = 1f)
    {
        gag.target = target; gag.moveBy = moveBy; gag.rotateBy = rotateBy; gag.bob = bob; gag.wobble = wobble; gag.spin = spin; gag.scaleBy = scaleBy;
        return gag;
    }

    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    static void BuildExpansions(Transform root)
    {
        wings.Clear(); wingNames.Clear();
        int before = report.Count;
        WingInn(root); WingSpeed(root); WingPlatforms(root); WingNC(root); WingCircus(root); WingSkits(root);
        WingCorn(root); WingCove(root); WingCritter(root); WingShadow(root); WingInk(root); WingDread(root);
        BuildExpansions2(root);
        BuildExpansions3(root);
        report.Add("The expansion: " + wings.Count + " areas have a new wing: " + string.Join(", ", wingNames.ToArray()));
    }

    // ------------------------------------------------------------------------------------------------ the Inn

    static void WingInn(Transform root)
    {
        float cx = 580f, z = 60f;
        var g = Wing(root, "The Redemption Inn", 560f, 600f, z, 26f, 7f, "Floor", "InnRed", "InnBlack",
                     "THE GRAND BALLROOM", new Color(1f, 0.8f, 0.3f), "dancing nightly. the band is dead. so is everyone.", 592f);
        // dance floor, stage, bar, tables
        for (int i = 0; i < 6; i++) for (int j = 0; j < 6; j++)
            Box(g, "Dance tile", V(cx - 5f + i * 2f, 0.01f, z + 8f + j * 2f), V(1.95f, 0.02f, 1.95f), M((i + j) % 2 == 0 ? "InnGold" : "InnBlack"), false);
        Box(g, "Bandstand", V(cx, 0.4f, z + 23.5f), V(14f, 0.8f, 4f), M("Stage"));
        Box(g, "Bar", V(cx - 16f, 0.55f, z + 14f), V(1.2f, 1.1f, 10f), M("DeskWood"));
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Cylinder, "Bottle", g, V(cx - 16f, 1.3f, z + 10f + k * 2f), V(0.12f, 0.2f, 0.12f), M(k % 2 == 0 ? "GiftGreen" : "Glass"));
        foreach (float tx in new[] { -12f, 12f }) for (int k = 0; k < 3; k++)
        {
            var at = V(cx + tx, 0, z + 6f + k * 6f);
            Box(g, "Table", at + V(0, 0.75f, 0), V(1.6f, 0.08f, 1.6f), M("InnBlack"));
            DPrim(PrimitiveType.Cylinder, "Table leg", g, at + V(0, 0.37f, 0), V(0.15f, 0.37f, 0.15f), M("InnGold"));
            DPrim(PrimitiveType.Cylinder, "Candle", g, at + V(0, 0.9f, 0), V(0.06f, 0.08f, 0.06f), M("GlowWarm"));
        }
        // the band (scriptless skeletons in a row) and the ballroom's people
        for (int k = 0; k < 4; k++) Mini(g, "Band skeleton " + (k + 1), V(cx - 4.5f + k * 3f, 0.8f, z + 23.5f), 180f, 1f, k % 2 == 0 ? "#ffd54f" : "#e0e0e0");
        Folk(g, "Madame Mandible", "#ce93d8", 9, V(cx - 15f, 0, z + 14f), 90f, "More/mo_inn_host");
        Folk(g, "Two-Step Tibia", "#ffab91", 16, V(cx + 2f, 0, z + 12f), 200f, "More/mo_inn_dancer");
        Folk(g, "The Spa Attendant", "#80deea", 5, V(cx + 15f, 0, z + 22f), 220f, "More/mo_inn_spa");
        // gags: the chandelier, the mirror ball, the hot tub
        var ch = Gg(g, "The chandelier", V(cx, 0, z + 13f), 0f, "More/mo_inn_chandelier", 2f, 2f);
        var lamp = Group(ch.transform, "Chandelier", V(0, 5.6f, 0));
        DPrim(PrimitiveType.Cylinder, "Ring", lamp, Vector3.zero, V(1.8f, 0.05f, 1.8f), M("InnGold"));
        for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; Prim(PrimitiveType.Sphere, "Crystal", lamp, V(Mathf.Cos(a) * 0.85f, -0.2f, Mathf.Sin(a) * 0.85f), Vector3.one * 0.18f, M("GlowWarm")); }
        Move(ch, lamp, V(0, -2.6f, 0), Vector3.zero, 0f, 12f).inTime = 0.25f;
        var mb = Gg(g, "The mirror ball", V(cx + 6f, 0, z + 20f), 0f, "More/mo_inn_ball", 1.4f, 2f);
        var ball = Prim(PrimitiveType.Sphere, "Mirror ball", mb.transform, V(0, 5.2f, 0), Vector3.one * 0.9f, M("Glint")).transform;
        Move(mb, ball, V(0, -1f, 0), Vector3.zero, 0f, 0f, 240f);
        var tub = Gg(g, "The hot tub", V(cx + 15f, 0, z + 18f), 180f, "More/mo_inn_tub", 2.6f, 1.4f);
        Box(g, "Tub", V(cx + 15f, 0.4f, z + 18f), V(3f, 0.8f, 3f), M("Porcelain"));
        Box(g, "Tub water", V(cx + 15f, 0.78f, z + 18f), V(2.6f, 0.05f, 2.6f), M("PoolBlue"), false);
        var bather = Group(tub.transform, "Bather", V(0, -1.6f, 0));
        Prim(PrimitiveType.Sphere, "Skull", bather, V(0, 1.6f, 0), Vector3.one * 0.35f, M("BoneWhite"));
        Prim(PrimitiveType.Cube, "Towel", bather, V(0, 1.85f, 0), V(0.4f, 0.08f, 0.3f), M("CaneWhite"));
        Move(tub, bather, V(0, 1.3f, 0), Vector3.zero, 0.04f);
        report.Add("The Grand Ballroom (the Inn's new wing): a dance floor, a skeleton band, a bar, a hot tub, a chandelier and a mirror ball; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Speed Zone

    static void WingSpeed(Transform root)
    {
        float cx = 650f, z = 90f;
        var g = Wing(root, "The Speed Zone", 620f, 680f, z, 40f, 20f, "PixelGreen", "PixelSky", "Sky",
                     "LOOP CANYON", new Color(0.2f, 0.6f, 1f), "a canyon full of loops. go fast. go faster. go home.");
        // canyon walls (checkered cliffs) and a giant loop
        for (int k = 0; k < 8; k++)
        {
            Box(g, "Cliff W", V(cx - 26f, 3f + (k % 2), z + 3f + k * 5f), V(6f, 6f + (k % 3) * 2f, 5f), M(k % 2 == 0 ? "Dirt" : "PixelSand"));
            Box(g, "Cliff E", V(cx + 26f, 3f + (k % 2), z + 3f + k * 5f), V(6f, 6f + ((k + 1) % 3) * 2f, 5f), M(k % 2 == 1 ? "Dirt" : "PixelSand"));
        }
        var loop = Group(g, "The big loop", V(cx, 0, z + 26f));
        for (int k = 0; k < 24; k++)
        {
            float a = k * Mathf.PI * 2f / 24f;
            DPrim(PrimitiveType.Cube, "Loop segment", loop, V(0, 6f - Mathf.Cos(a) * 6f, Mathf.Sin(a) * 6f), V(4f, 0.3f, 1.7f), M(k % 2 == 0 ? "HeroBlue" : "GlowGold"), Quaternion.Euler(-k * 15f, 0, 0));
        }
        // rings to collect along the canyon floor, with launch pads up onto the ledges
        var at = new List<Vector3>();
        for (int k = 0; k < 14; k++) at.Add(V(cx - 12f + (k % 7) * 4f, 1.2f + (k / 7) * 2.5f, z + 8f + (k % 7) * 1.5f + (k / 7) * 22f));
        Collectables(g, "Canyon rings", at.ToArray(), (t, i) => { DPrim(PrimitiveType.Cylinder, "Ring", t, Vector3.zero, V(0.8f, 0.04f, 0.8f), M("GlowGold"), Quaternion.Euler(90, 0, 0)); return true; },
                     V(cx - 6f, 1.6f, z + 0.35f), Vector3.forward, new[] { V(cx - 18f, 0, z + 12f), V(cx + 18f, 0, z + 12f) }, new[] { V(4f, 14f, 6f), V(-4f, 14f, 6f) });
        Folk(g, "Pit Chief Pelvis", "#ff7043", 7, V(cx - 12f, 0, z + 4f), 160f, "More/mo_spd_mech");
        Folk(g, "Stopwatch Stella", "#4fc3f7", 2, V(cx + 12f, 0, z + 4f), 200f, "More/mo_spd_time");
        Folk(g, "Cool Hand Clavicle", "#76ff03", 15, V(cx + 8f, 0, z + 34f), 180f, "More/mo_spd_racer");
        // gags: the blur, the spring, the boulder
        var blur = Gg(g, "The blur", V(cx - 20f, 0, z + 18f), 90f, "More/mo_spd_blur", 2f, 2f);
        var streak = Group(blur.transform, "Streak", Vector3.zero);
        Prim(PrimitiveType.Capsule, "Blue streak", streak, V(0, 0.8f, 0), V(0.8f, 1.6f, 0.8f), M("HeroBlue")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        Move(blur, streak, V(0, 0, 40f), Vector3.zero).inTime = 0.4f;
        blur.hideWhenIdle = true;
        var spring = Gg(g, "The big spring", V(cx + 16f, 0, z + 26f), 180f, "More/mo_spd_spring", 1.6f, 1.6f);
        var sp = Group(spring.transform, "Spring", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "Coil", sp, V(0, 0.4f, 0), V(1.2f, 0.4f, 1.2f), M("GlowYellow"));
        Prim(PrimitiveType.Cylinder, "Cap", sp, V(0, 0.85f, 0), V(1.4f, 0.06f, 1.4f), M("GlowRed"));
        Move(spring, sp, V(0, 3f, 0), V(0, 0, 0), 0f, 0f, 360f).inTime = 0.15f;
        var boulder = Gg(g, "The rolling boulder", V(cx, 0, z + 38f), 180f, "More/mo_spd_boulder", 3f, 3f);
        var rock = Prim(PrimitiveType.Sphere, "Boulder", boulder.transform, V(0, 1.6f, 0), Vector3.one * 3.2f, M("Stone")).transform;
        Move(boulder, rock, V(0, 0, 12f), V(720f, 0, 0)).inTime = 2.5f;
        report.Add("Loop Canyon (the Speed Zone's new wing): a giant loop, checkered cliffs, 14 rings and 2 launch pads; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Platform World

    static void WingPlatforms(Transform root)
    {
        float cx = 730f, z = 80f;
        var g = Wing(root, "The Platform World", 700f, 760f, z, 36f, 20f, "GemWhite", "PixelSky", "Sky",
                     "CLOUD KINGDOM", new Color(1f, 1f, 1f), "above the clouds. watch your step. the clouds are mostly decorative.");
        // a staircase of floating platforms up to a cloud castle
        for (int k = 0; k < 9; k++)
            Box(g, "Floating platform", V(cx - 20f + k * 4.2f, 0.6f + k * 0.9f, z + 8f + (k % 2) * 3f), V(3f, 0.4f, 3f), M(k % 3 == 0 ? "PixelGreen" : "GemWhite"));
        var castle = V(cx + 18f, 8.6f, z + 26f);
        Box(g, "Castle deck", castle, V(12f, 0.4f, 10f), M("GemWhite"));
        for (int k = 0; k < 4; k++)
        {
            var tp = castle + V(-5f + (k % 2) * 10f, 2.5f, -4f + (k / 2) * 8f);
            DPrim(PrimitiveType.Cylinder, "Tower", g, tp, V(1.6f, 2.5f, 1.6f), M("GemPink"));
            DPrim(PrimitiveType.Cylinder, "Tower roof", g, tp + V(0, 2.9f, 0), V(1.9f, 0.4f, 1.9f), M("PixelRoof"));
        }
        Box(g, "Ramp to the castle", V(cx + 14f, 4.4f, z + 18f), V(3f, 0.3f, 8f), M("GemWhite")).transform.localRotation = Quaternion.Euler(-28f, 0, 0);
        // coins above the platforms
        var at = new List<Vector3>();
        for (int k = 0; k < 9; k++) at.Add(V(cx - 20f + k * 4.2f, 2.3f + k * 0.9f, z + 8f + (k % 2) * 3f));
        Collectables(g, "Cloud coins", at.ToArray(), (t, i) => { DPrim(PrimitiveType.Cylinder, "Coin", t, Vector3.zero, V(0.6f, 0.05f, 0.6f), M("GlowGold"), Quaternion.Euler(90, 0, 0)); return true; },
                     V(cx - 6f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Nimbus Nell", "#ffffff", 4, V(cx - 22f, 0, z + 4f), 150f, "More/mo_plt_cloud");
        Folk(g, "Morel the Mushroom Merchant", "#ef5350", 9, V(cx + 4f, 0, z + 4f), 180f, "More/mo_plt_merchant");
        Folk(g, "Sir Ulna of the Ramparts", "#b0bec5", 11, castle + V(0, 0.2f, -3f), 180f, "More/mo_plt_guard");
        // gags: the bonk block, the grumpy slab, the pipe surprise
        var block = Gg(g, "The bonk block", V(cx - 8f, 0, z + 20f), 180f, "More/mo_plt_block", 1.4f, 3f);
        Box(g, "Bonk block", V(cx - 8f, 3f, z + 20f), V(1f, 1f, 1f), M("GlowGold"), false);
        var coin = Prim(PrimitiveType.Cylinder, "Coin", block.transform, V(0, 3f, 0), V(0.6f, 0.05f, 0.6f), M("GlowYellow")).transform;
        coin.localRotation = Quaternion.Euler(90, 0, 0);
        Move(block, coin, V(0, 2f, 0), Vector3.zero, 0f, 0f, 720f).hideWhenIdle = true;
        var slab = Gg(g, "The grumpy slab", V(cx + 2f, 0, z + 30f), 180f, "More/mo_plt_slab", 2f, 2f);
        var sl = Group(slab.transform, "Slab", V(0, 6f, 0));
        Prim(PrimitiveType.Cube, "Stone face", sl, Vector3.zero, V(2f, 2f, 0.8f), M("Stone"));
        foreach (float ex in new[] { -0.45f, 0.45f }) Prim(PrimitiveType.Cube, "Angry eye", sl, V(ex, 0.3f, 0.41f), V(0.4f, 0.15f, 0.02f), M("GlowRed")).transform.localRotation = Quaternion.Euler(0, 0, ex > 0 ? 20 : -20);
        Move(slab, sl, V(0, -4.9f, 0), Vector3.zero, 0f, 4f).inTime = 0.12f;
        var pipe = Gg(g, "The pipe surprise", V(cx - 16f, 0, z + 30f), 180f, "More/mo_plt_pipe", 1.6f, 2f);
        DPrim(PrimitiveType.Cylinder, "Pipe", g, V(cx - 16f, 0.9f, z + 30f), V(1.6f, 0.9f, 1.6f), M("PixelGreenDark"));
        var snap = Group(pipe.transform, "Snapper", V(0, 0.4f, 0));
        Prim(PrimitiveType.Sphere, "Head", snap, V(0, 0.6f, 0), V(0.9f, 0.8f, 0.9f), M("GlowRed"));
        Prim(PrimitiveType.Cube, "Grin", snap, V(0, 0.55f, 0.42f), V(0.6f, 0.06f, 0.04f), M("CaneWhite"));
        Prim(PrimitiveType.Cylinder, "Stalk", snap, V(0, 0f, 0), V(0.15f, 0.4f, 0.15f), M("GiftGreen"));
        Move(pipe, snap, V(0, 1.4f, 0), Vector3.zero, 0f, 10f).hideWhenIdle = true;
        report.Add("Cloud Kingdom (the Platform World's new wing): 9 floating platforms up to a cloud castle, 9 coins; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ North Carolina

    static void WingNC(Transform root)
    {
        float cx = 810f, z = 80f;
        var g = Wing(root, "North Carolina", 780f, 840f, z, 40f, 25f, "PixelSand", "Sky", "Sky",
                     "THE OUTER BANKS", new Color(1f, 0.95f, 0.7f), "dunes, wild horses, a lighthouse and the first flight (twelve seconds of it)");
        Box(g, "Ocean", V(cx, 0.02f, z + 34f), V(60f, 0.04f, 12f), M("Water"), false);
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Sphere, "Dune", g, V(cx - 24f + k * 9f, 0f, z + 22f + (k % 2) * 3f), V(8f, 2.4f, 5f), M("PixelSand"));
        // the striped lighthouse
        var lh = V(cx + 20f, 0, z + 10f);
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Cylinder, "Lighthouse band", g, lh + V(0, 1f + k * 2f, 0), V(3.2f - k * 0.15f, 1f, 3.2f - k * 0.15f), M(k % 2 == 0 ? "CaneWhite" : "Black"));
        Box(g, "Lighthouse base", lh + V(0, 0.5f, 0), V(3.6f, 1f, 3.6f), M("Brick"));
        DPrim(PrimitiveType.Cylinder, "Gallery", g, lh + V(0, 16.3f, 0), V(2.6f, 0.15f, 2.6f), M("Black"));
        DPrim(PrimitiveType.Sphere, "Lantern room", g, lh + V(0, 17.2f, 0), V(1.6f, 1.6f, 1.6f), M("GlowWarm"));
        // a sea-fishing pier
        Box(g, "Pier", V(cx - 14f, 0.4f, z + 32f), V(3f, 0.3f, 16f), M("DeskWood"));
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cylinder, "Piling", g, V(cx - 15.4f + (k % 2) * 2.8f, 0.3f, z + 25f + (k / 2) * 6f), V(0.3f, 0.6f, 0.3f), M("TrimDark"));
        Folk(g, "Keeper Kneecap", "#90a4ae", 9, lh + V(-3f, 0, -3f), 200f, "More/mo_nc_keeper");
        Folk(g, "Kitty Hawkbone", "#ffcc80", 16, V(cx - 4f, 0, z + 10f), 160f, "More/mo_nc_kite");
        Folk(g, "Captain Barnacle-Beard", "#4e342e", 7, V(cx - 14f, 0.55f, z + 38f), 180f, "More/mo_nc_pirate");
        // gags: the first flight, the wild horses, the lighthouse beam
        var flyer = Gg(g, "The first flight", V(cx - 20f, 0, z + 6f), 90f, "More/mo_nc_flight", 3f, 2f);
        var plane = Group(flyer.transform, "Flyer", V(0, 0.6f, 0));
        foreach (float wy in new[] { 0.3f, 1.3f }) Prim(PrimitiveType.Cube, "Wing", plane, V(0, wy, 0), V(6f, 0.06f, 1.2f), M("Paper"));
        for (int k = 0; k < 4; k++) Prim(PrimitiveType.Cylinder, "Strut", plane, V(-2.4f + k * 1.6f, 0.8f, 0), V(0.05f, 0.5f, 0.05f), M("TrimDark"));
        Prim(PrimitiveType.Cube, "Pilot", plane, V(0, 0.45f, 0), V(0.3f, 0.2f, 0.8f), M("BoneWhite"));
        Move(flyer, plane, V(0, 3f, 36f), V(-6f, 0, 0), 0.15f).inTime = 6f;
        var horses = Gg(g, "The wild horses", V(cx + 4f, 0, z + 28f), 270f, "More/mo_nc_horses", 3f, 2f);
        var herd = Group(horses.transform, "Herd", Vector3.zero);
        for (int k = 0; k < 3; k++)
        {
            var hz = Group(herd, "Horse " + k, V(k * 1.6f - 1.6f, 0, (k % 2) * 1.2f));
            Prim(PrimitiveType.Capsule, "Body", hz, V(0, 1.1f, 0), V(0.6f, 0.8f, 0.6f), M(k == 1 ? "DoxieBlack" : "DoxieBrown")).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Capsule, "Neck", hz, V(0, 1.6f, 0.6f), V(0.3f, 0.5f, 0.3f), M(k == 1 ? "DoxieBlack" : "DoxieBrown")).transform.localRotation = Quaternion.Euler(40, 0, 0);
            foreach (float lx in new[] { -0.2f, 0.2f }) foreach (float lz in new[] { -0.5f, 0.5f }) Prim(PrimitiveType.Cylinder, "Leg", hz, V(lx, 0.45f, lz), V(0.12f, 0.45f, 0.12f), M("TrimDark"));
        }
        Move(horses, herd, V(0, 0, 20f), Vector3.zero, 0.25f).inTime = 3f;
        var beam = Gg(g, "The lighthouse beam", lh + V(-3f, 0, 3f), 0f, "More/mo_nc_beam", 1.6f, 2f);
        var lamp = Group(beam.transform, "Beam", V(3f, 17.2f, -3f));
        Prim(PrimitiveType.Cube, "Light", lamp, V(0, 0, 6f), V(1.2f, 1.2f, 12f), M("BeamWhite"));
        Move(beam, lamp, Vector3.zero, Vector3.zero, 0f, 0f, 90f);
        beam.hideWhenIdle = true;
        report.Add("The Outer Banks (North Carolina's new wing): dunes, the ocean, a striped lighthouse, a pier, wild horses and the first flight; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Digital Circus

    static void WingCircus(Transform root)
    {
        float cx = 880f, z = 60f;
        var g = Wing(root, "The Digital Circus", 860f, 900f, z, 26f, 14f, "Black", "WallDark", "Black",
                     "BACKSTAGE", new Color(1f, 0.3f, 0.3f), "staff only. the staff is also only. there is no outside.", 888f);
        // floating glitch cubes, a costume rack, a prop cannon, the exit door
        for (int k = 0; k < 14; k++)
            DPrim(PrimitiveType.Cube, "Glitch cube", g, V(cx - 16f + (k * 7.3f) % 32f, 3f + (k * 1.7f) % 8f, z + 4f + (k * 5.1f) % 20f), Vector3.one * (0.3f + (k % 4) * 0.25f), M(k % 3 == 0 ? "DarkGlowCyan" : k % 3 == 1 ? "DarkGlowPink" : "GlowYellow"), Quaternion.Euler(k * 23f, k * 41f, k * 17f));
        Box(g, "Costume rail", V(cx - 15f, 2f, z + 10f), V(0.1f, 0.1f, 8f), M("Metal"), false);
        for (int k = 0; k < 7; k++) Box(g, "Costume", V(cx - 15f, 1.3f, z + 6.6f + k * 1.1f), V(0.6f, 1.3f, 0.12f), M(k % 2 == 0 ? "CaneRed" : "CaneWhite"), false);
        for (int k = 0; k < 6; k++) Box(g, "Prop crate", V(cx + 14f, 0.5f + (k / 3) * 1f, z + 6f + (k % 3) * 1.2f), V(1f, 1f, 1f), M("Cardboard"));
        Folk(g, "Stagehand Stapes", "#9e9e9e", 7, V(cx + 12f, 0, z + 12f), 220f, "More/mo_cir_hand");
        Folk(g, "Juggling Jawbone", "#ffeb3b", 16, V(cx - 4f, 0, z + 14f), 180f, "More/mo_cir_juggle");
        Folk(g, "The Understudy", "#ef9a9a", 5, V(cx - 12f, 0, z + 20f), 150f, "More/mo_cir_understudy");
        // gags: the exit door, the glitch, the human cannonball
        var door = Gg(g, "The EXIT door", V(cx, 0, z + 25.5f), 180f, "More/mo_cir_exit", 2f, 2.6f);
        var dr = Group(door.transform, "Door", V(-0.6f, 0, 0));
        Prim(PrimitiveType.Cube, "Door panel", dr, V(0.6f, 1.2f, 0), V(1.2f, 2.4f, 0.08f), M("DoorWood"));
        Text("EXIT", g, V(cx, 2.8f, z + 25.85f), Face(Vector3.back), V2(1.4f, 0.4f), 0.3f, "<b>EXIT</b>").color = new Color(0.2f, 1f, 0.3f);
        Box(g, "Bricked-up doorway", V(cx, 1.2f, z + 25.95f), V(1.3f, 2.5f, 0.05f), M("BrickWall"), false);
        Move(door, dr, Vector3.zero, V(0, 100f, 0));
        var glitch = Gg(g, "The glitch", V(cx + 6f, 0, z + 6f), 200f, "More/mo_cir_glitch", 1.6f, 2.2f);
        var gl = Group(glitch.transform, "Glitched", Vector3.zero);
        for (int k = 0; k < 5; k++) Prim(PrimitiveType.Cube, "Pixel", gl, V(-0.4f + k * 0.2f, 0.6f + k * 0.3f, 0), V(0.4f, 0.25f, 0.4f), M(k % 2 == 0 ? "DarkGlowPink" : "DarkGlowCyan"));
        Move(glitch, gl, V(0.3f, 0, 0), V(0, 0, 30f), 0.3f, 45f, 0f, 1.4f);
        var cannon = Gg(g, "The human cannonball", V(cx - 8f, 0, z + 4f), 0f, "More/mo_cir_cannon", 2f, 2f);
        var barrel = Prim(PrimitiveType.Cylinder, "Cannon", g, V(cx - 8f, 1f, z + 4f), V(1f, 1.4f, 1f), M("CaneRed"));
        barrel.transform.localRotation = Quaternion.Euler(50f, 0, 0);
        var ballist = Group(cannon.transform, "Cannonball skeleton", V(0, 1.5f, 0.6f));
        Prim(PrimitiveType.Sphere, "Skull", ballist, Vector3.zero, Vector3.one * 0.5f, M("BoneWhite"));
        Prim(PrimitiveType.Cube, "Helmet", ballist, V(0, 0.25f, 0), V(0.5f, 0.12f, 0.5f), M("GlowRed"));
        Move(cannon, ballist, V(0, 9f, 14f), V(720f, 0, 0)).hideWhenIdle = true;
        cannon.inTime = 1.2f;
        report.Add("Backstage (the Digital Circus's new wing): glitch cubes, a costume rail, prop crates, the EXIT door; 3 voiced people, 3 gags");
    }

    static Vector2 V2(float x, float y) { return new Vector2(x, y); }

    // ------------------------------------------------------------------------------------------------ the Skits

    static void WingSkits(Transform root)
    {
        float cx = 938f, z = 56f;
        var g = Wing(root, "The Skits", 920f, 956f, z, 20f, 6f, "Floor2", "LabWhite", "Ceiling",
                     "THE WRITERS' ROOM", new Color(0.2f, 0.2f, 0.2f), "where the skits are written. and rewritten. and thrown away.");
        Box(g, "Big table", V(cx, 0.75f, z + 9f), V(10f, 0.1f, 3f), M("DeskWood"));
        for (int k = 0; k < 10; k++) Box(g, "Chair", V(cx - 4.5f + (k % 5) * 2.2f, 0.45f, z + 9f + (k < 5 ? -2.2f : 2.2f)), V(0.6f, 0.9f, 0.6f), M("TrimDark"));
        for (int k = 0; k < 14; k++) DPrim(PrimitiveType.Sphere, "Crumpled draft", g, V(cx - 15f + (k * 2.3f) % 30f, 0.15f, z + 2f + (k * 3.7f) % 16f), Vector3.one * 0.3f, M("Paper"));
        Box(g, "Whiteboard", V(cx, 2f, z + 19.85f), V(8f, 2.4f, 0.1f), M("LabWhite"), false);
        Text("Board", g, V(cx, 2f, z + 19.78f), Face(Vector3.back), V2(7.6f, 2.2f), 0.22f,
             "<b>SKIT IDEAS</b>\n1. a man buys a sandwich (too long)\n2. a sandwich buys a man (too short)\n3. the door that is a window\n4. ??? (ask Gary)").color = new Color(0.1f, 0.1f, 0.4f);
        Folk(g, "Head Writer Humerus", "#8d6e63", 9, V(cx - 6f, 0, z + 12f), 160f, "More/mo_skt_head");
        Folk(g, "Gary", "#c5e1a5", 20, V(cx + 6f, 0, z + 12f), 200f, "More/mo_skt_gary");
        Folk(g, "The Intern", "#ffe082", 2, V(cx + 13f, 0, z + 4f), 220f, "More/mo_skt_intern");
        var coffee = Gg(g, "The coffee machine", V(cx - 14f, 0, z + 18f), 180f, "More/mo_skt_coffee", 1.2f, 1.8f);
        Box(g, "Coffee machine", V(cx - 14f, 1.2f, z + 18.5f), V(0.8f, 0.9f, 0.6f), M("Metal"));
        var pot = Prim(PrimitiveType.Cylinder, "Pot", coffee.transform, V(0, 1f, -0.1f), V(0.3f, 0.2f, 0.3f), M("Glass")).transform;
        Move(coffee, pot, V(0, 0, 0), V(0, 0, 0), 0f, 25f);
        var laugh = Gg(g, "The laugh track machine", V(cx + 14f, 0, z + 18f), 180f, "More/mo_skt_laugh", 1.4f, 1.6f);
        var lt = Group(laugh.transform, "Machine", V(0, 1f, 0));
        Prim(PrimitiveType.Cube, "Box", lt, Vector3.zero, V(0.9f, 0.7f, 0.6f), M("GiftRed"));
        Prim(PrimitiveType.Cylinder, "Big button", lt, V(0, 0.4f, 0), V(0.3f, 0.05f, 0.3f), M("GlowYellow"));
        Move(laugh, lt, V(0, 0.1f, 0), Vector3.zero, 0.08f, 6f);
        var bin = Gg(g, "The idea bin", V(cx + 9f, 0, z + 3f), 0f, "More/mo_skt_bin", 1f, 1.2f);
        var bn = Group(bin.transform, "Bin", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "Bin", bn, V(0, 0.35f, 0), V(0.6f, 0.35f, 0.6f), M("Metal"));
        for (int k = 0; k < 4; k++) Prim(PrimitiveType.Sphere, "Idea", bn, V(-0.15f + k * 0.1f, 0.75f, 0), Vector3.one * 0.22f, M("Paper"));
        Move(bin, bn, V(0, 0.2f, 0), V(0, 0, 0), 0.05f, 15f);
        report.Add("The Writers' Room (the Skits' new wing): a big table, crumpled drafts, a whiteboard of ideas; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Corn Flakes Room

    static void WingCorn(Transform root)
    {
        float cx = 992f, z = 44f;
        var g = Wing(root, "The Corn Flakes Room", 980f, 1004f, z, 22f, 6f, "LabFloor", "CornYellow", "CaneWhite",
                     "THE CEREAL AISLE", new Color(0.9f, 0.5f, 0.1f), "every cereal. all of them corn flakes.", 982.4f);
        string[] box = { "GiftRed", "CornYellow", "GiftBlue", "GiftGreen", "GlowOrange" };
        foreach (float sx in new[] { -8f, -3.5f, 3.5f, 8f })
        {
            Box(g, "Shelf", V(cx + sx, 1.2f, z + 11f), V(1.2f, 2.4f, 14f), M("Metal"));
            foreach (float face in new[] { -0.73f, 0.73f })
                for (int lv = 0; lv < 3; lv++) for (int k = 0; k < 12; k++)
                    Box(g, "Cereal box", V(cx + sx + face, 0.45f + lv * 0.75f, z + 4.6f + k * 1.15f), V(0.25f, 0.6f, 0.45f), M(box[(k + lv + (face > 0 ? 2 : 0)) % 5]), false);
        }
        DPrim(PrimitiveType.Capsule, "Giant spoon handle", g, V(cx - 1.4f, 3.5f, z + 20.5f), V(0.4f, 3f, 0.2f), M("Metal"), Quaternion.Euler(0, 0, 30f));
        DPrim(PrimitiveType.Sphere, "Giant spoon bowl", g, V(cx, 0.9f, z + 20.5f), V(1.6f, 0.5f, 2.2f), M("Metal"));
        Folk(g, "Milkman Malleus", "#f5f5f5", 9, V(cx - 4f, 0, z + 3f), 170f, "More/mo_crn_milk");
        Folk(g, "Sir Flakes-a-Lot", "#ffb300", 16, V(cx + 6f, 0, z + 20f), 180f, "More/mo_crn_knight");
        Folk(g, "The Indecisive Shopper", "#90caf9", 2, V(cx + 5.75f, 0, z + 10f), 270f, "More/mo_crn_shopper");
        var aval = Gg(g, "The cereal avalanche", V(cx - 5.75f, 0, z + 14f), 90f, "More/mo_crn_avalanche", 1.4f, 2.4f);
        var pile = Group(aval.transform, "Falling boxes", V(0, 2.4f, 0));
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Cube, "Box", pile, V((k % 3) * 0.3f - 0.3f, k * 0.3f, (k / 3) * 0.4f), V(0.25f, 0.6f, 0.45f), M(box[k % 5])).transform.localRotation = Quaternion.Euler(k * 30f, k * 15f, 0);
        Move(aval, pile, V(0.5f, -2.1f, 0), V(0, 0, 80f), 0f, 5f).hideWhenIdle = true;
        var fountain = Gg(g, "The milk fountain", V(cx + 9.5f, 0, z + 20.5f), 270f, "More/mo_crn_fountain", 1.6f, 2f);
        Box(g, "Fountain basin", V(cx + 9.5f, 0.3f, z + 20.5f), V(2f, 0.6f, 2f), M("Porcelain"));
        var jet = Prim(PrimitiveType.Cylinder, "Milk jet", fountain.transform, V(0, 0.6f, 0), V(0.25f, 0.05f, 0.25f), M("Milk")).transform;
        Move(fountain, jet, V(0, 1.3f, 0), Vector3.zero, 0.1f, 0f, 0f, 1f).hideWhenIdle = true;
        var prize = Gg(g, "The free toy inside", V(cx - 9.5f, 0, z + 20.5f), 90f, "More/mo_crn_prize", 1.2f, 1.6f);
        var toy = Group(prize.transform, "Toy", V(0, 1f, 0));
        Prim(PrimitiveType.Cube, "Big box", g, V(cx - 9.5f, 0.6f, z + 20.5f), V(1f, 1.2f, 0.5f), M("GiftRed"));
        Prim(PrimitiveType.Sphere, "Toy skull", toy, Vector3.zero, Vector3.one * 0.35f, M("BoneGold"));
        Prim(PrimitiveType.Cylinder, "Spring", toy, V(0, -0.3f, 0), V(0.1f, 0.3f, 0.1f), M("Metal"));
        Move(prize, toy, V(0, 1f, 0), Vector3.zero, 0f, 20f).inTime = 0.15f;
        report.Add("The Cereal Aisle (the Corn Flakes Room's new wing): three aisles of cereal boxes and a giant spoon; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Crystal Cove

    static void WingCove(Transform root)
    {
        float cx = 1055f, z = 70f;
        var g = Wing(root, "Crystal Cove", 1030f, 1080f, z, 34f, 20f, "GemWhite", "NightSky", "NightSky",
                     "THE MOON TEMPLE", new Color(0.8f, 0.7f, 1f), "an old temple on the moon. well. a room painted like the moon.");
        for (int k = 0; k < 10; k++)
        {
            var at = V(cx - 16f + (k % 5) * 8f, 0, z + 8f + (k / 5) * 16f);
            DPrim(PrimitiveType.Cylinder, "Pillar", g, at + V(0, 4f, 0), V(1.4f, 4f, 1.4f), M(k % 2 == 0 ? "GemPink" : "GemWhite"));
            DPrim(PrimitiveType.Cube, "Capital", g, at + V(0, 8.2f, 0), V(2f, 0.4f, 2f), M("GemPurple"));
        }
        for (int k = 0; k < 40; k++) DPrim(PrimitiveType.Sphere, "Star", g, V(cx - 24f + (k * 13.7f) % 48f, 10f + (k * 3.3f) % 9f, z + 33.7f), Vector3.one * 0.18f, M("GlowYellow"));
        Box(g, "Altar", V(cx, 0.6f, z + 28f), V(6f, 1.2f, 3f), M("GemPurple"));
        Folk(g, "Temple Keeper Opal", "#e1bee7", 9, V(cx - 4f, 0, z + 26f), 180f, "More/mo_gem_keeper");
        Folk(g, "Chip the Gem Sculptor", "#80cbc4", 7, V(cx + 14f, 0, z + 12f), 220f, "More/mo_gem_sculptor");
        Folk(g, "A Very Lost Tourist", "#ffcc80", 2, V(cx - 14f, 0, z + 4f), 160f, "More/mo_gem_tourist");
        // the statue's gem, the bubbled gems, the waving hand
        var st = Gg(g, "The statue's gem", V(cx, 0, z + 28f), 180f, "More/mo_gem_statue", 2f, 3f);
        var gem = Prim(PrimitiveType.Sphere, "Big gem", st.transform, V(0, 2.4f, 0), V(1.1f, 1.4f, 1.1f), M("GemPink")).transform;
        Move(st, gem, V(0, 1.5f, 0), Vector3.zero, 0f, 0f, 120f, 1.4f);
        var bub = Gg(g, "The bubbled gems", V(cx + 10f, 0, z + 22f), 200f, "More/mo_gem_bubbles", 2f, 2f);
        var bg = Group(bub.transform, "Bubbles", V(0, 1.5f, 0));
        for (int k = 0; k < 4; k++)
        {
            Prim(PrimitiveType.Sphere, "Bubble", bg, V(-0.9f + k * 0.6f, k * 0.4f, 0), Vector3.one * 0.6f, M("Glass"));
            Prim(PrimitiveType.Sphere, "Gem inside", bg, V(-0.9f + k * 0.6f, k * 0.4f, 0), Vector3.one * 0.25f, M(k % 2 == 0 ? "GemPurple" : "GemPink"));
        }
        Move(bub, bg, V(0, 2.5f, 0), Vector3.zero, 0.15f, 0f, 45f);
        var hand = Gg(g, "The giant hand", V(cx - 18f, 0, z + 28f), 150f, "More/mo_gem_hand", 2.4f, 4f);
        var hd = Group(hand.transform, "Hand", V(0, 0.5f, 0));
        Prim(PrimitiveType.Cube, "Palm", hd, V(0, 2f, 0), V(2f, 2.4f, 0.6f), M("GemWhite"));
        for (int k = 0; k < 4; k++) Prim(PrimitiveType.Capsule, "Finger", hd, V(-0.75f + k * 0.5f, 3.9f, 0), V(0.35f, 0.8f, 0.35f), M("GemWhite"));
        Move(hand, hd, Vector3.zero, V(0, 0, 25f), 0f, 0f);
        hand.holdTime = 3f;
        report.Add("The Moon Temple (Crystal Cove's new wing): 10 crystal pillars, a star field, an altar; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Critter Meadow

    static void WingCritter(Transform root)
    {
        float cx = 1125f, z = 70f;
        var g = Wing(root, "Critter Meadow", 1100f, 1150f, z, 34f, 20f, "CaveRock", "CaveRock", "Black",
                     "CRITTER CAVE", new Color(0.6f, 0.9f, 0.6f), "rare critters live here. so do bats. mostly bats.");
        for (int k = 0; k < 16; k++)
            DPrim(PrimitiveType.Cylinder, "Stalactite", g, V(cx - 22f + (k * 5.9f) % 44f, 17f, z + 3f + (k * 7.1f) % 30f), V(0.8f, 3f, 0.8f), M("Stone"));
        for (int k = 0; k < 10; k++)
            DPrim(PrimitiveType.Sphere, "Crystal cluster", g, V(cx - 20f + (k * 9.7f) % 40f, 0.3f, z + 4f + (k * 6.3f) % 28f), V(0.8f, 1.2f, 0.8f), M(k % 2 == 0 ? "OreBlue" : "GemPurple"));
        Box(g, "Underground lake", V(cx + 10f, 0.02f, z + 24f), V(18f, 0.04f, 14f), M("Water"), false);
        Box(g, "Rock bridge", V(cx + 10f, 0.25f, z + 24f), V(2f, 0.5f, 14f), M("Stone"));
        // critters to spot (scriptless) and catch cubes to collect
        string[] kinds = { "Mossbun", "Puffbat", "Zapcorn", "Pebblet" };
        for (int k = 0; k < 6; k++) Critter(g, kinds[k % 4], V(cx - 18f + k * 6f, 0, z + 14f + (k % 2) * 6f)).localRotation = Quaternion.Euler(0, 180f + k * 30f, 0);
        var at = new List<Vector3>();
        for (int k = 0; k < 8; k++) at.Add(V(cx - 20f + k * 5.5f, 1f, z + 30f - (k % 3) * 2f));
        Collectables(g, "Catch cubes", at.ToArray(), (t, i) => { DPrim(PrimitiveType.Cube, "Cube", t, Vector3.zero, Vector3.one * 0.4f, M(i % 2 == 0 ? "GlowRed" : "CaneWhite")); return true; },
                     V(cx - 6f, 1.6f, z + 0.35f), Vector3.forward);
        Folk(g, "Hiker Hyoid", "#a1887f", 2, V(cx - 10f, 0, z + 6f), 180f, "More/mo_crt_hiker");
        Folk(g, "Spelunker Sacrum", "#ffd54f", 7, V(cx - 2f, 0, z + 20f), 150f, "More/mo_crt_caver");
        Folk(g, "Patient Pat the Angler", "#4fc3f7", 9, V(cx + 10f, 0.5f, z + 18f), 0f, "More/mo_crt_angler");
        var roll = Gg(g, "The rolling Pebblet", V(cx - 16f, 0, z + 26f), 90f, "More/mo_crt_pebble", 1.6f, 1.6f);
        var pb = Critter(roll.transform, "Pebblet", Vector3.zero);
        Move(roll, pb, V(0, 0, 8f), V(1080f, 0, 0)).inTime = 1.5f;
        var bat = Gg(g, "The swooping Puffbat", V(cx + 2f, 0, z + 10f), 0f, "More/mo_crt_bat", 1.6f, 3f);
        var pbat = Critter(bat.transform, "Puffbat", V(0, 6f, 0));
        Move(bat, pbat, V(0, -4f, 3f), V(0, 0, 0), 0.3f, 20f, 360f).hideWhenIdle = true;
        var rod = Gg(g, "The fishing rod", V(cx + 10f, 0.5f, z + 19.5f), 0f, "More/mo_crt_boot", 1.4f, 2f);
        var boot = Group(rod.transform, "Catch", V(0, -0.6f, 2.5f));
        Prim(PrimitiveType.Cube, "Old boot", boot, Vector3.zero, V(0.3f, 0.4f, 0.6f), M("TrimDark"));
        Prim(PrimitiveType.Cylinder, "Line", boot, V(0, 1f, 0), V(0.01f, 1f, 0.01f), M("CaneWhite"));
        Move(rod, boot, V(0, 2f, -1.5f), V(0, 0, 0), 0f, 20f).hideWhenIdle = true;
        report.Add("Critter Cave (Critter Meadow's new wing): stalactites, crystal clusters, an underground lake, 6 critters and 8 catch cubes; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Shadow Below

    static void WingShadow(Transform root)
    {
        float cx = 1180f, z = 36f;
        var g = Wing(root, "The Shadow Below", 1170f, 1190f, z, 18f, 4f, "Floor2", "Wall", "Ceiling",
                     "THE SCHOOL LIBRARY", new Color(0.3f, 0.3f, 0.6f), "quiet please. the books are sleeping.");
        for (int k = 0; k < 4; k++)
        {
            Box(g, "Bookcase", V(cx - 9.3f, 1.2f, z + 3f + k * 3.8f), V(0.6f, 2.4f, 3f), M("TrimDark"));
            Box(g, "Bookcase", V(cx + 9.3f, 1.2f, z + 3f + k * 3.8f), V(0.6f, 2.4f, 3f), M("TrimDark"));
            for (int lv = 0; lv < 4; lv++)
            {
                Box(g, "Books", V(cx - 8.95f, 0.35f + lv * 0.55f, z + 3f + k * 3.8f), V(0.2f, 0.4f, 2.8f), M(new[] { "Book1", "Book2", "Book3" }[(k + lv) % 3]), false);
                Box(g, "Books", V(cx + 8.95f, 0.35f + lv * 0.55f, z + 3f + k * 3.8f), V(0.2f, 0.4f, 2.8f), M(new[] { "Book1", "Book2", "Book3" }[(k + lv + 1) % 3]), false);
            }
        }
        for (int k = 0; k < 2; k++) Box(g, "Reading table", V(cx, 0.75f, z + 6f + k * 6f), V(4f, 0.08f, 1.6f), M("DeskWood"));
        Folk(g, "Librarian Lacrimal", "#b39ddb", 9, V(cx - 4f, 0, z + 16f), 180f, "More/mo_shd_librarian");
        Folk(g, "Hall Monitor Malar", "#ffab40", 15, V(cx + 4f, 0, z + 2.5f), 180f, "More/mo_shd_monitor");
        Folk(g, "The Sleepy Student", "#aed581", 20, V(cx + 2.4f, 0, z + 12f), 270f, "More/mo_shd_sleepy");
        var shelf = Gg(g, "The secret bookcase", V(cx - 8.6f, 0, z + 14.4f), 90f, "More/mo_shd_secret", 1.6f, 2.4f);
        var sc = Group(shelf.transform, "Swinging shelf", Vector3.zero);
        Prim(PrimitiveType.Cube, "Bookcase door", sc, V(0, 1.2f, 0), V(3f, 2.4f, 0.5f), M("Book2"));
        Move(shelf, sc, V(0, 0, 0.6f), V(0, 70f, 0));
        var vend = Gg(g, "The vending machine", V(cx + 7.5f, 0, z + 17f), 180f, "More/mo_shd_vend", 1.2f, 2.2f);
        Box(g, "Vending machine", V(cx + 7.5f, 1.1f, z + 17.4f), V(1.2f, 2.2f, 0.8f), M("GiftBlue"));
        var can = Prim(PrimitiveType.Cylinder, "Can", vend.transform, V(0, 0.35f, -0.1f), V(0.12f, 0.15f, 0.12f), M("GlowRed")).transform;
        Move(vend, can, V(0, 0, -0.6f), V(90f, 0, 0)).hideWhenIdle = true;
        var book = Gg(g, "The flying book", V(cx, 0, z + 6f), 0f, "More/mo_shd_book", 1.6f, 1.6f);
        var bk = Prim(PrimitiveType.Cube, "Book", book.transform, V(0, 0.85f, 0), V(0.5f, 0.08f, 0.35f), M("Book1")).transform;
        Move(book, bk, V(0, 1.6f, 0), V(0, 0, 0), 0.2f, 15f, 200f);
        report.Add("The School Library (the Shadow Below's new wing): 8 bookcases, reading tables; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ Ink Plaza

    static void WingInk(Transform root)
    {
        float cx = 1310f, z = 60f;
        var g = Wing(root, "Ink Plaza", 1290f, 1330f, z, 28f, 12f, "Concrete", "WallDark", "Sky",
                     "INK TOWER STAGE", new Color(1f, 0.5f, 0.1f), "tonight: the loudest band in the deep end");
        Box(g, "Stage", V(cx, 0.6f, z + 23f), V(20f, 1.2f, 8f), M("Black"));
        for (int k = 0; k < 4; k++) Box(g, "Speaker stack", V(cx - 11.5f + (k % 2) * 23f, 1.8f + (k / 2) * 2.4f, z + 25f), V(2f, 2.4f, 2f), M("Black"));
        for (int k = 0; k < 20; k++)
            Box(g, "Ink splat", V(cx - 16f + (k * 7.7f) % 32f, 0.015f, z + 3f + (k * 4.9f) % 16f), V(1.4f + (k % 3) * 0.5f, 0.02f, 1.2f + (k % 2) * 0.6f), M(k % 2 == 0 ? "InkOrange" : "InkBlue"), false);
        for (int k = 0; k < 3; k++) Mini(g, "Band member " + (k + 1), V(cx - 5f + k * 5f, 1.2f, z + 22f), 180f, 1f, k == 1 ? "#ff7043" : "#29b6f6");
        Folk(g, "DJ Dentin", "#ce93d8", 16, V(cx + 7f, 1.2f, z + 25f), 180f, "More/mo_ink_dj");
        Folk(g, "Merch Stand Mandy", "#ffb74d", 9, V(cx - 15f, 0, z + 8f), 90f, "More/mo_ink_merch");
        Folk(g, "Referee Ridley Crab", "#ef5350", 11, V(cx + 14f, 0, z + 6f), 230f, "More/mo_ink_ref");
        Box(g, "Merch table", V(cx - 13.6f, 0.45f, z + 8f), V(1f, 0.9f, 3f), M("InkOrange"));
        var band = Gg(g, "The band plays", V(cx, 1.2f, z + 19f), 180f, "More/mo_ink_band", 4f, 2f);
        var drum = Group(band.transform, "Drum kit", V(0, 0, -1f));
        Prim(PrimitiveType.Cylinder, "Bass drum", drum, V(0, 0.5f, 0), V(1f, 0.3f, 1f), M("InkBlue")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Cylinder, "Cymbal", drum, V(0.8f, 1.2f, 0), V(0.6f, 0.02f, 0.6f), M("GlowGold"));
        Move(band, drum, Vector3.zero, Vector3.zero, 0.12f, 10f);
        var rain = Gg(g, "The ink rain", V(cx - 6f, 0, z + 10f), 0f, "More/mo_ink_rain", 2.4f, 2f);
        var drops = Group(rain.transform, "Drops", V(0, 9f, 0));
        for (int k = 0; k < 8; k++) Prim(PrimitiveType.Sphere, "Drop", drops, V((k % 4) * 0.7f - 1f, (k / 4) * 1.2f, (k % 3) * 0.5f), V(0.3f, 0.5f, 0.3f), M(k % 2 == 0 ? "InkOrange" : "InkBlue"));
        Move(rain, drops, V(0, -8.6f, 0), Vector3.zero).hideWhenIdle = true;
        rain.inTime = 0.8f;
        var blast = Gg(g, "The speaker blast", V(cx + 11.5f, 0, z + 22f), 180f, "More/mo_ink_blast", 2.4f, 4f);
        var cone = Prim(PrimitiveType.Cylinder, "Speaker cone", blast.transform, V(0, 1.8f, -1.05f), V(1.2f, 0.05f, 1.2f), M("TurfGrey")).transform;
        cone.localRotation = Quaternion.Euler(90, 0, 0);
        Move(blast, cone, V(0, 0, -0.2f), Vector3.zero, 0f, 8f, 0f, 1.3f);
        report.Add("Ink Tower Stage (Ink Plaza's new wing): a concert stage with a band, speaker stacks, ink splats, a merch stand; 3 voiced people, 3 gags");
    }

    // ------------------------------------------------------------------------------------------------ the Dread Depths

    static void WingDread(Transform root)
    {
        float cx = 1375f, z = 70f;
        var g = Wing(root, "The Dread Depths", 1350f, 1400f, z, 32f, 14f, "LabFloor", "CaveRock", "Black",
                     "THE REACTOR CORE", new Color(0.4f, 1f, 0.6f), "the deepest level. the core is stable. the core is lying.");
        var core = V(cx, 0, z + 18f);
        DPrim(PrimitiveType.Cylinder, "Core base", g, core + V(0, 0.5f, 0), V(8f, 0.5f, 8f), M("Metal"));
        Box(g, "Core pillar", core + V(0, 6f, 0), V(3f, 11f, 3f), M("AlienGlow"));
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.PI / 3f;
            DPrim(PrimitiveType.Cylinder, "Coolant pipe", g, core + V(Mathf.Cos(a) * 9f, 6.5f, Mathf.Sin(a) * 9f), V(0.6f, 6.5f, 0.6f), M("Metal"));
        }
        for (int k = 0; k < 8; k++) Box(g, "Catwalk", V(cx - 21f + k * 6f, 0.15f, z + 4f), V(5.5f, 0.3f, 2f), M("Pegboard"));
        Box(g, "Control desk", V(cx - 16f, 0.55f, z + 10f), V(5f, 1.1f, 1.6f), M("LabGrey"));
        Box(g, "Desk screen", V(cx - 16f, 1.6f, z + 10.6f), V(4f, 1f, 0.1f), M("DarkGlowCyan"), false);
        Folk(g, "Doctor Distal", "#e0f7fa", 9, V(cx - 16f, 0, z + 8.5f), 0f, "More/mo_drd_doctor");
        Folk(g, "Maintenance Unit M-0", "#bdbdbd", 7, V(cx + 14f, 0, z + 8f), 220f, "More/mo_drd_bot");
        Folk(g, "Space Pirate Patella", "#7e57c2", 11, V(cx + 18f, 0, z + 26f), 200f, "More/mo_drd_pirate");
        var alarm = Gg(g, "The core alarm", V(cx - 8f, 0, z + 10f), 180f, "More/mo_drd_alarm", 1.6f, 3f);
        var siren = Group(alarm.transform, "Siren", V(0, 2.6f, 0));
        Prim(PrimitiveType.Cylinder, "Siren light", siren, Vector3.zero, V(0.5f, 0.3f, 0.5f), M("GlowRed"));
        Move(alarm, siren, Vector3.zero, Vector3.zero, 0f, 0f, 720f, 1.5f);
        var jelly = Gg(g, "The floating jelly", V(cx + 8f, 0, z + 12f), 200f, "More/mo_drd_jelly", 2f, 3f);
        var jl = Group(jelly.transform, "Jelly", V(0, 2.2f, 0));
        Prim(PrimitiveType.Sphere, "Membrane", jl, Vector3.zero, V(1.2f, 0.9f, 1.2f), M("AlienGlow"));
        for (int k = 0; k < 3; k++) Prim(PrimitiveType.Sphere, "Nucleus", jl, V(-0.25f + k * 0.25f, -0.05f, 0), Vector3.one * 0.25f, M("GlowRed"));
        Move(jelly, jl, V(0, 0.8f, -2.5f), Vector3.zero, 0.3f, 10f).holdTime = 2.5f;
        var lift = Gg(g, "The cargo lift", V(cx + 16f, 0, z + 16f), 270f, "More/mo_drd_lift", 3f, 3f);
        var pl = Group(lift.transform, "Lift", Vector3.zero);
        Prim(PrimitiveType.Cube, "Platform", pl, V(0, 0.1f, 0), V(3f, 0.2f, 3f), M("Pegboard"));
        Prim(PrimitiveType.Cube, "Crate", pl, V(0, 0.7f, 0), V(1f, 1f, 1f), M("SuitOrange"));
        Move(lift, pl, V(0, 9f, 0), Vector3.zero).inTime = 3f;
        report.Add("The Reactor Core (the Dread Depths' new wing): a glowing core, coolant pipes, catwalks, a control desk; 3 voiced people, 3 gags");
    }
}
#endif
