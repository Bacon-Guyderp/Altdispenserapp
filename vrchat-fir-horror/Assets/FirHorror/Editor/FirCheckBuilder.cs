#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using Object = UnityEngine.Object;

/// <summary>
/// Fir Warning > Build Systems Check. Stage 1 of the rebuild: a small world that proves, inside your
/// own VRChat setup, that every basic piece the new map will use works. Each test is a button that
/// writes PASS or FAIL on itself. Boards in the world also show the UdonSharp and SDK versions and
/// what the builder found, so one photo of the lobby says everything.
/// </summary>
public static class FirCheckBuilder
{
    const string Root = "Assets/FirHorror";
    const string Gen = Root + "/Generated";
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Texture2D> baked = new Dictionary<string, Texture2D>();
    static Material fontMat;
    static readonly List<string> report = new List<string>();

    [MenuItem("Fir Warning/Build Systems Check", priority = 1)]
    public static void BuildMenu() { Build(false); }

    public static bool Build(bool quiet)
    {
        if (TMP_Settings.defaultFontAsset == null)
        {
            EditorUtility.DisplayDialog("Fir Warning", "Import TextMeshPro first:\nWindow > TextMeshPro > Import TMP Essential Resources", "OK");
            return false;
        }
        if (!EnsureProgramAssets()) return false;
        if (UdonSharpHasErrors())
        {
            EditorUtility.DisplayDialog("Fir Warning: UdonSharp has errors",
                "UdonSharp can't compile the scripts, so nothing in the world would work. Open the Console, look for the first " +
                "red UdonSharp error, and send it (or the Editor log). Nothing was built.", "OK");
            return false;
        }
        Directory.CreateDirectory(Gen);
        mats.Clear(); report.Clear(); meshCache.Clear(); rigs.Clear(); skelParts = null; stewieParts = null;
        BakeTextures();   // the wallpaper, carpet, ceiling, needles and skeleton textures (Data/textures_baked.txt)
        foreach (var name in new[] { "FirWarning", "FirWarningCheck" })
        {
            var old = GameObject.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old);
        }
        var root = new GameObject("FirWarningCheck").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Fir Warning systems check");
        fontMat = FontMaterial();
        MakeMaterials();

        // ---- the lobby: 14 x 12 m, everything fullbright so nothing depends on lighting
        var lobby = Group(root, "Lobby", Vector3.zero);
        Box(lobby, "Floor", new Vector3(0, -0.1f, 0), new Vector3(14, 0.2f, 12), M("Floor"));
        Box(lobby, "Ceiling", new Vector3(0, 3.7f, 0), new Vector3(14, 0.2f, 12), M("Ceiling"));
        Box(lobby, "WallN", new Vector3(0, 1.8f, 6.1f), new Vector3(14.4f, 3.6f, 0.2f), M("Wall"));
        DoorWall(lobby, "WallS", -6.1f, 14.4f, 3.6f, M("Wall"));

        // details: trim, twinkling lights, the doorway's frame, wreaths and candy canes, a rug, posters and lamps
        Trim(lobby, "Lobby", 6.0f, -6.0f, 14f, 3.6f, float.NaN, 0f, false);
        StringLights(lobby, "Lobby", -6f, 6f, 14f, 3.6f);
        DoorFrame(lobby, 0f, -6.1f);
        Wreath(lobby, new Vector3(-2.2f, 2.2f, -5.97f), Vector3.forward);
        Wreath(lobby, new Vector3(2.2f, 2.2f, -5.97f), Vector3.forward);
        CandyCane(lobby, new Vector3(-1.75f, 0, -5.65f), true);
        CandyCane(lobby, new Vector3(1.75f, 0, -5.65f), false);
        Rug(lobby, new Vector3(0, 0, -2.6f), new Vector2(4.2f, 3.0f), "Rug", "TrimDark");
        Poster(lobby, "poster_warning", new Vector3(6.97f, 1.85f, -3.6f), Vector3.left, 0.9f, 1.2f);
        Poster(lobby, "poster_original", new Vector3(6.97f, 1.85f, -0.6f), Vector3.left, 0.9f, 1.2f);
        PendantLamp(lobby, new Vector3(-3.5f, 3.6f, -0.5f), 0.45f);
        PendantLamp(lobby, new Vector3(3.5f, 3.6f, -0.5f), 0.45f);
        Box(lobby, "WallW", new Vector3(-7.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 12.4f), M("Wall"));
        Box(lobby, "WallE", new Vector3(7.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 12.4f), M("Wall"));
        var spawn = Marker(lobby, "Spawn", new Vector3(0, 0.05f, -2.6f), 0);
        // dark notice boards behind the text, so it reads clearly against the wallpaper
        Box(lobby, "TestBoard", new Vector3(0, 1.85f, 5.995f), new Vector3(13.2f, 3.3f, 0.01f), M("Board"));
        Box(lobby, "InfoBoard", new Vector3(-6.995f, 1.65f, 0.45f), new Vector3(0.01f, 3.0f, 9.4f), M("Board"));
        Box(lobby, "PanelBoard", new Vector3(6.995f, 1.75f, 3.5f), new Vector3(0.01f, 1.8f, 2.2f), M("Board"));

        var n = Vector3.back;   // the north wall faces the players
        Text("Title", lobby, new Vector3(0, 3.15f, 5.98f), Face(n), new Vector2(10, 0.6f), 0.5f, "<b>FIR WARNING</b>  systems check").color = new Color(0.9f, 0.15f, 0.15f);
        Text("Help", lobby, new Vector3(0, 2.72f, 5.98f), Face(n), new Vector2(12, 0.35f), 0.17f,
             "Press every button. Each one says <color=#70ff90>PASS</color> or <color=#ff6060>FAIL</color> on itself. Stuck on <color=#ffd040>testing...</color> = that test stopped. Then send a photo of the walls.").color = Color.white;

        // ---- the hub (everything that talks to another script talks to this)
        var hubGo = new GameObject("Hub");
        hubGo.transform.SetParent(root, false);
        var hub = Add<FirCheckHub>(hubGo);
        var w = Vector3.right;   // west wall faces east
        hub.board = Text("HubBoard", lobby, new Vector3(-6.98f, 2.2f, 2.5f), Face(w), new Vector2(5f, 1.6f), 0.2f,
                         "<color=#ff6060><b>HUB: not running</b></color>\nIf this never changes, UdonSharp didn't run in this world.");
        hub.board.color = Color.white;

        // ---- the test room (teleport target), far from the lobby
        var room = Group(root, "TestRoom", new Vector3(40, 0, 0));
        Box(room, "Floor", new Vector3(0, -0.1f, 0), new Vector3(8, 0.2f, 8), M("Floor2"));
        Box(room, "WallN", new Vector3(0, 1.5f, 4.1f), new Vector3(8.4f, 3f, 0.2f), M("Wall2"));
        Box(room, "WallS", new Vector3(0, 1.5f, -4.1f), new Vector3(8.4f, 3f, 0.2f), M("Wall2"));
        Box(room, "WallW", new Vector3(-4.1f, 1.5f, 0), new Vector3(0.2f, 3f, 8.4f), M("Wall2"));
        Box(room, "WallE", new Vector3(4.1f, 1.5f, 0), new Vector3(0.2f, 3f, 8.4f), M("Wall2"));
        var roomArrive = Marker(room, "Arrive", new Vector3(0, 0.05f, -2), 0);
        Trim(room, "TestRoom", 4.0f, -4.0f, 8f, 3f, float.NaN, float.NaN, true);
        StringLights(room, "TestRoom", -4f, 4f, 8f, 3f);
        Text("RoomTitle", room, new Vector3(0, 2.4f, 3.98f), Face(n), new Vector2(7, 0.5f), 0.35f, "<b>TEST ROOM</b>: teleport worked if you're reading this").color = Color.white;

        // ---- the test buttons, 4 + 4 + 2 on the north wall
        var testMat = M("TestPanel");
        var sound = Src(lobby, new Vector3(0, 1.5f, 5), Clip("tree_laugh"), false, 0f, 1f, 20f, false);
        Func<int, Vector3> Spot = i => new Vector3(-4.8f + (i % 4) * 3.2f, 2.05f - (i / 4) * 0.78f, 5.85f);
        Func<int, int, string, FirCheckButton> Test = (slot, test, title) =>
        {
            TextMeshPro label;
            var b = MakeButton<FirCheckButton>(lobby, "Test " + test, Spot(slot), n, out label);
            b.test = test; b.title = title; b.label = label; b.hub = UB(hub);
            return b;
        };
        var t1 = Test(0, 1, "1. Teleport"); t1.destination = roomArrive;
        Test(1, 2, "2. Button's own script");
        Test(2, 3, "3. Event to another script");
        Test(3, 4, "4. Values between scripts");
        var typed = MakeButton<FirCheckTyped>(lobby, "Test 5", Spot(4), n, out var typedLabel);
        typed.hub = hub; typed.label = typedLabel;
        Test(5, 6, "6. Network sync");
        var t7 = Test(6, 7, "7. Skeleton (by event)");
        var t8 = Test(7, 8, "8. Tree (by event)");
        var t9 = Test(8, 9, "9. Lighting (material)"); t9.testMaterial = testMat;
        var t10 = Test(9, 10, "10. Sound"); t10.sound = sound;
        // test 11 is in the test room: teleport back
        var t11 = MakeButton<FirCheckButton>(room, "Test 11", new Vector3(0, 1.3f, 3.85f), n, out var backLabel);
        t11.test = 11; t11.title = "11. Teleport back"; t11.label = backLabel; t11.destination = spawn; t11.hub = UB(hub);

        // the lighting test panel (lantern-lit, so _Dark shows) on the east wall
        var e = Vector3.left;
        var panel = Box(lobby, "TestPanel", new Vector3(6.95f, 1.6f, 3.5f), new Vector3(0.05f, 1.2f, 1.6f), testMat);
        Text("PanelLabel", lobby, new Vector3(6.9f, 2.45f, 3.5f), Face(e), new Vector2(2f, 0.3f), 0.15f, "test 9 panel").color = Color.white;

        // ---- your skeleton
        var skelGo = new GameObject("Skeleton");
        skelGo.transform.SetParent(lobby, false);
        skelGo.transform.localPosition = new Vector3(-2.5f, 0, 1.5f);
        skelGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var pivots = SkeletonRig(skelGo.transform, "Rig", M("BoneWhite"));
        var sc = skelGo.AddComponent<BoxCollider>(); sc.center = new Vector3(0, 0.95f, 0); sc.size = new Vector3(0.7f, 1.9f, 0.5f);
        var skel = Add<FirCheckSkeleton>(skelGo);
        skel.pivots = pivots;
        skel.voice = Src(pivots[2], Vector3.zero, Clip("skeleton_white"), false, 1f, 1f, 15f, false);
        skel.label = Text("SkeletonLabel", lobby, new Vector3(-2.5f, 2.35f, 1.5f), Face(Vector3.back), new Vector2(2.2f, 0.45f), 0.14f,
                          "<b>White skeleton</b>\n<color=#ff6060>its script isn't running</color>");
        skel.label.color = Color.white;
        SetInteract(skel, "Press the skeleton", 3f);
        rigs["White"] = pivots;
        t7.other = UB(skel);

        // ---- a talking tree: the original Fir Warning model
        var treeGo = new GameObject("Tree");
        treeGo.transform.SetParent(lobby, false);
        treeGo.transform.localPosition = new Vector3(2.5f, 0, 1.5f);
        treeGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
        Transform mouth;
        var head = TreeModel(treeGo.transform, 1.9f, true, out mouth);
        var tc = treeGo.AddComponent<BoxCollider>(); tc.center = new Vector3(0, 0.95f, 0); tc.size = new Vector3(1.3f, 1.9f, 1.3f);
        var tree = Add<FirCheckTree>(treeGo);
        tree.head = head; tree.mouth = mouth;
        tree.voice = Src(head, new Vector3(0, 0.75f, 0.35f), Clip("tree_talk"), false, 1f, 1f, 15f, false);
        tree.label = Text("TreeLabel", lobby, new Vector3(2.5f, 2.35f, 1.5f), Face(Vector3.back), new Vector2(2.2f, 0.45f), 0.14f,
                          "<b>Tree</b>\n<color=#ff6060>its script isn't running</color>");
        tree.label.color = Color.white;
        SetInteract(tree, "Press the tree", 3f);
        t8.other = UB(tree);

        // ---- the map: new rooms to the south, full of gags (through the doorway in the lobby's south wall)
        BuildMap(root, spawn);
        BuildShare(root);

        // ---- copy everything into Udon, then check what UdonSharp stored
        foreach (var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true)) Sync(b);
        var info = Text("BuildInfo", lobby, new Vector3(-6.98f, 1.0f, -1.6f), Face(w), new Vector2(5f, 1.5f), 0.15f, "");
        info.color = Color.white;
        info.text = BuildReport(root);

        SetupDescriptor(spawn);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.3f);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root.gameObject;
        foreach (var r in report) Debug.Log("Fir Warning systems check: " + r);
        if (!quiet) EditorUtility.DisplayDialog("Fir Warning", "Built the systems check.\n\n" + string.Join("\n", report.Take(14)) +
                                                "\n\nNow press Build & Test (or upload) and press every button in the lobby.", "OK");
        return true;
    }

    static void StringLights(Transform parent, string name, float z0, float z1, float w, float h)
    {
        if (z0 > z1) { float tmp = z0; z0 = z1; z1 = tmp; }
        var g = Group(parent, name + " Lights", Vector3.zero);
        string[] cols = { "BulbRed", "BulbGreen", "BulbBlue", "BulbGold" };
        int n = 0;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (w / 2f - 0.06f);
            Box(g, "Wire", new Vector3(x, h - 0.2f, (z0 + z1) / 2f), new Vector3(0.015f, 0.015f, Mathf.Abs(z1 - z0) - 0.2f), M("Black"), false);
            // bulbs sit in the middle of 0.5 m cells so the Bulb shader can give each its own twinkle
            for (float z = Mathf.Floor(z0 * 2f) / 2f + 0.25f; z < z1 - 0.1f; z += 0.5f)
            {
                if (z < z0 + 0.1f) continue;
                float swag = Mathf.Sin(Mathf.PI * Mathf.Repeat((z - z0) / 2f, 1f)) * 0.15f;
                var b = Prim(PrimitiveType.Cube, "Bulb", g, new Vector3(x, h - 0.27f - swag, z), new Vector3(0.075f, 0.11f, 0.075f), Bulb(cols[n % 4]));
                GameObjectUtility.SetStaticEditorFlags(b, StaticEditorFlags.BatchingStatic);
                n++;
            }
        }
    }

    static Material Bulb(string name)
    {
        if (mats.TryGetValue(name, out var m)) return m;
        var c = name == "BulbRed" ? new Color(1f, 0.08f, 0.06f) : name == "BulbGreen" ? new Color(0.1f, 1f, 0.2f)
              : name == "BulbBlue" ? new Color(0.15f, 0.35f, 1f) : name == "BulbWarm" ? new Color(1f, 0.85f, 0.55f)
              : name == "BulbGhost" ? new Color(0.45f, 1f, 0.6f) : new Color(1f, 0.75f, 0.15f);
        string path = Gen + "/Check_" + name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("FirHorror/Bulb");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_Color", c);
        m.SetFloat("_Dark", 0f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    // =================================================================== details (decoration only: no scripts, no colliders, static)

    static GameObject DPrim(PrimitiveType t, string n, Transform p, Vector3 pos, Vector3 sc, Material m)
    {
        var g = Prim(t, n, p, pos, sc, m);
        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
        return g;
    }

    static GameObject DPrim(PrimitiveType t, string n, Transform p, Vector3 pos, Vector3 sc, Material m, Quaternion rot)
    {
        var g = DPrim(t, n, p, pos, sc, m);
        g.transform.localRotation = rot;
        return g;
    }

    /// <summary>
    /// Skirting boards, wainscot panelling with a dado rail, and crown moulding round a room (interior x within
    /// +-w/2, z from zN (north face) to zS (south face)); the cross walls leave gaps for doorways at the given x.
    /// </summary>
    static void Trim(Transform parent, string name, float zN, float zS, float w, float h, float doorN, float doorS, bool wainscot)
    {
        var g = Group(parent, name + " Trim", Vector3.zero);
        float len = zN - zS, zc = (zN + zS) / 2f;
        foreach (float sd in new[] { -1f, 1f })
        {
            float x = sd * (w / 2f - 0.02f);
            Box(g, "Skirting", new Vector3(x - sd * 0.0f, 0.09f, zc), new Vector3(0.05f, 0.18f, len), M("TrimDark"), false);
            Box(g, "Crown", new Vector3(x, h - 0.07f, zc), new Vector3(0.07f, 0.14f, len), M("Trim"), false);
            if (wainscot)
            {
                Box(g, "Wainscot", new Vector3(x + sd * 0.005f, 0.55f, zc), new Vector3(0.02f, 0.9f, len), M("Wainscot"), false);
                Box(g, "Rail", new Vector3(x - sd * 0.01f, 1.02f, zc), new Vector3(0.06f, 0.06f, len), M("TrimDark"), false);
            }
        }
        foreach (var cw in new[] { new { z = zN - 0.02f, door = doorN }, new { z = zS + 0.02f, door = doorS } })
        {
            Box(g, "Crown", new Vector3(0, h - 0.07f, cw.z), new Vector3(w, 0.14f, 0.07f), M("Trim"), false);
            float l0 = -w / 2f, r1 = w / 2f;
            float l1 = float.IsNaN(cw.door) ? r1 : cw.door - 1.32f, r0 = float.IsNaN(cw.door) ? r1 : cw.door + 1.32f;
            if (l1 - l0 > 0.05f)
            {
                Box(g, "Skirting", new Vector3((l0 + l1) / 2f, 0.09f, cw.z), new Vector3(l1 - l0, 0.18f, 0.05f), M("TrimDark"), false);
                if (wainscot) Box(g, "Wainscot", new Vector3((l0 + l1) / 2f, 0.55f, cw.z), new Vector3(l1 - l0, 0.9f, 0.02f), M("Wainscot"), false);
            }
            if (r1 - r0 > 0.05f)
            {
                Box(g, "Skirting", new Vector3((r0 + r1) / 2f, 0.09f, cw.z), new Vector3(r1 - r0, 0.18f, 0.05f), M("TrimDark"), false);
                if (wainscot) Box(g, "Wainscot", new Vector3((r0 + r1) / 2f, 0.55f, cw.z), new Vector3(r1 - r0, 0.9f, 0.02f), M("Wainscot"), false);
            }
        }
    }

    /// <summary>A wooden frame round a doorway in a wall centred at z (posts and a lintel, proud of both faces).</summary>
    static void DoorFrame(Transform parent, float x, float z)
    {
        var g = Group(parent, "DoorFrame", new Vector3(x, 0, z));
        Box(g, "PostL", new Vector3(-1.27f, 1.43f, 0), new Vector3(0.14f, 2.86f, 0.32f), M("TrimDark"), false);
        Box(g, "PostR", new Vector3(1.27f, 1.43f, 0), new Vector3(0.14f, 2.86f, 0.32f), M("TrimDark"), false);
        Box(g, "Lintel", new Vector3(0, 2.88f, 0), new Vector3(2.68f, 0.16f, 0.32f), M("TrimDark"), false);
    }

    /// <summary>A wreath with berries and a red bow, hung flat on a wall facing `normal`.</summary>
    static void Wreath(Transform parent, Vector3 pos, Vector3 normal, float r = 0.28f)
    {
        var g = Group(parent, "Wreath", pos);
        g.localRotation = Face(normal);
        for (int i = 0; i < 14; i++)
        {
            float a = i * Mathf.PI * 2f / 14f;
            DPrim(PrimitiveType.Sphere, "Fir", g, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0), Vector3.one * r * 0.62f, M("Needles"));
            if (i % 3 == 0) DPrim(PrimitiveType.Sphere, "Berry", g, new Vector3(Mathf.Cos(a + 0.2f) * r * 1.05f, Mathf.Sin(a + 0.2f) * r * 1.05f, -r * 0.25f), Vector3.one * r * 0.16f, M("GiftRed"));
        }
        DPrim(PrimitiveType.Cube, "BowL", g, new Vector3(-r * 0.25f, -r, -r * 0.3f), new Vector3(r * 0.45f, r * 0.3f, r * 0.12f), M("GiftRed"), Quaternion.Euler(0, 0, 20));
        DPrim(PrimitiveType.Cube, "BowR", g, new Vector3(r * 0.25f, -r, -r * 0.3f), new Vector3(r * 0.45f, r * 0.3f, r * 0.12f), M("GiftRed"), Quaternion.Euler(0, 0, -20));
        DPrim(PrimitiveType.Cube, "Knot", g, new Vector3(0, -r, -r * 0.34f), Vector3.one * r * 0.18f, M("GiftRed"));
    }

    /// <summary>A candy cane, about a metre tall, its hook facing along +x of the parent unless flipped.</summary>
    static void CandyCane(Transform parent, Vector3 pos, bool flip)
    {
        var g = Group(parent, "CandyCane", pos);
        if (flip) g.localRotation = Quaternion.Euler(0, 180, 0);
        for (int i = 0; i < 8; i++)
            DPrim(PrimitiveType.Cylinder, "Stripe", g, new Vector3(0, 0.06f + i * 0.12f, 0), new Vector3(0.09f, 0.06f, 0.09f), M(i % 2 == 0 ? "CaneRed" : "CaneWhite"));
        for (int i = 0; i < 4; i++)
        {
            float a = (i + 0.5f) * Mathf.PI / 4f;
            DPrim(PrimitiveType.Cylinder, "Hook", g, new Vector3(0.12f - Mathf.Cos(a) * 0.12f, 0.96f + Mathf.Sin(a) * 0.12f, 0), new Vector3(0.09f, 0.055f, 0.09f),
                  M(i % 2 == 0 ? "CaneWhite" : "CaneRed"), Quaternion.Euler(0, 0, -90f + (i + 0.5f) * 45f));
        }
    }

    /// <summary>A pendant lamp hanging from the ceiling at (x, ceiling, z): cord, brass shade, warm bulb.</summary>
    static void PendantLamp(Transform parent, Vector3 ceiling, float drop, string glow = "GlowWarm")
    {
        var g = Group(parent, "Lamp", ceiling);
        DPrim(PrimitiveType.Cylinder, "Cord", g, new Vector3(0, -drop / 2f, 0), new Vector3(0.015f, drop / 2f, 0.015f), M("Black"));
        DPrim(PrimitiveType.Cylinder, "Shade", g, new Vector3(0, -drop - 0.06f, 0), new Vector3(0.42f, 0.07f, 0.42f), M("Brass"));
        DPrim(PrimitiveType.Sphere, "Bulb", g, new Vector3(0, -drop - 0.15f, 0), Vector3.one * 0.16f, M(glow));
    }

    /// <summary>A window onto a snowy night, set in a wall facing `normal`, with frame, glazing bars and sill.</summary>
    static void SnowWindow(Transform parent, Vector3 pos, Vector3 normal, float w, float h)
    {
        var g = Group(parent, "Window", pos);
        g.localRotation = Face(normal);
        DPrim(PrimitiveType.Cube, "Glass", g, new Vector3(0, 0, 0.005f), new Vector3(w, h, 0.01f), M("WindowSnow"));
        DPrim(PrimitiveType.Cube, "FrameT", g, new Vector3(0, h / 2f, -0.02f), new Vector3(w + 0.16f, 0.08f, 0.06f), M("Trim"));
        DPrim(PrimitiveType.Cube, "FrameB", g, new Vector3(0, -h / 2f, -0.02f), new Vector3(w + 0.16f, 0.08f, 0.06f), M("Trim"));
        DPrim(PrimitiveType.Cube, "FrameL", g, new Vector3(-w / 2f, 0, -0.02f), new Vector3(0.08f, h, 0.06f), M("Trim"));
        DPrim(PrimitiveType.Cube, "FrameR", g, new Vector3(w / 2f, 0, -0.02f), new Vector3(0.08f, h, 0.06f), M("Trim"));
        DPrim(PrimitiveType.Cube, "BarV", g, new Vector3(0, 0, -0.015f), new Vector3(0.04f, h, 0.03f), M("Trim"));
        DPrim(PrimitiveType.Cube, "BarH", g, new Vector3(0, 0, -0.015f), new Vector3(w, 0.04f, 0.03f), M("Trim"));
        DPrim(PrimitiveType.Cube, "Sill", g, new Vector3(0, -h / 2f - 0.05f, -0.08f), new Vector3(w + 0.3f, 0.05f, 0.18f), M("Trim"));
        // a little drift of snow on the sill
        DPrim(PrimitiveType.Sphere, "Snow", g, new Vector3(-w * 0.2f, -h / 2f - 0.02f, -0.07f), new Vector3(w * 0.5f, 0.05f, 0.12f), M("Snow"));
    }

    /// <summary>A framed poster (one of the package's poster textures) on a wall facing `normal`.</summary>
    static void Poster(Transform parent, string tex, Vector3 pos, Vector3 normal, float w, float h)
    {
        var g = Group(parent, "Poster " + tex, pos);
        g.localRotation = Face(normal);
        DPrim(PrimitiveType.Cube, "Frame", g, new Vector3(0, 0, 0.012f), new Vector3(w + 0.1f, h + 0.1f, 0.03f), M("GiftGold"));
        DPrim(PrimitiveType.Cube, "Print", g, new Vector3(0, 0, -0.006f), new Vector3(w, h, 0.01f), PosterMat(tex));
    }

    static Material PosterMat(string tex)
    {
        string n = "Poster_" + tex;
        if (!mats.ContainsKey(n)) AddMat(n, tex, Color.white, 1, Color.black);
        return mats[n];
    }

    /// <summary>A rug (a border and a field) lying on the floor.</summary>
    static void Rug(Transform parent, Vector3 pos, Vector2 size, string field, string border)
    {
        Box(parent, "RugBorder", pos + new Vector3(0, 0.006f, 0), new Vector3(size.x, 0.012f, size.y), M(border), false);
        Box(parent, "Rug", pos + new Vector3(0, 0.014f, 0), new Vector3(size.x - 0.3f, 0.012f, size.y - 0.3f), M(field), false);
    }

    /// <summary>A Christmas stocking hanging on a hook, toe pointing along +x of its group.</summary>
    static void Stocking(Transform parent, Vector3 pos, Vector3 normal, string colour)
    {
        var g = Group(parent, "Stocking", pos);
        g.localRotation = Face(normal);
        DPrim(PrimitiveType.Cube, "Cuff", g, new Vector3(0, 0, -0.05f), new Vector3(0.24f, 0.1f, 0.08f), M("CaneWhite"));
        DPrim(PrimitiveType.Cube, "Leg", g, new Vector3(0, -0.22f, -0.05f), new Vector3(0.2f, 0.36f, 0.07f), M(colour));
        DPrim(PrimitiveType.Cube, "Foot", g, new Vector3(0.08f, -0.42f, -0.05f), new Vector3(0.3f, 0.14f, 0.07f), M(colour));
    }

    /// <summary>A sagging garland of fir between two points, with a few baubles.</summary>
    static void Garland(Transform parent, Vector3 a, Vector3 b, float sag)
    {
        int n = Mathf.Max(6, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.12f));
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            var p = Vector3.Lerp(a, b, t) + new Vector3(0, -Mathf.Sin(t * Mathf.PI) * sag, 0);
            DPrim(PrimitiveType.Sphere, "Garland", parent, p, Vector3.one * 0.16f, M("Needles"));
            if (i % 5 == 2) DPrim(PrimitiveType.Sphere, "Bauble", parent, p + new Vector3(0, -0.09f, 0), Vector3.one * 0.07f, M(i % 10 == 2 ? "GiftRed" : "GiftGold"));
        }
    }

    /// <summary>A stand of little candles with glowing flames.</summary>
    static void Candles(Transform parent, Vector3 pos, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float a = i * 2.4f, r = 0.1f + 0.06f * (i % 3);
            float hgt = 0.12f + 0.06f * ((i * 5) % 3);
            var p = pos + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
            DPrim(PrimitiveType.Cylinder, "Candle", parent, p + new Vector3(0, hgt / 2f, 0), new Vector3(0.05f, hgt / 2f, 0.05f), M("Wax"));
            DPrim(PrimitiveType.Sphere, "Flame", parent, p + new Vector3(0, hgt + 0.025f, 0), new Vector3(0.025f, 0.045f, 0.025f), M("GlowFlame"));
        }
    }

    /// <summary>A cobweb strung across a ceiling corner (a few thin strands).</summary>
    static void Cobweb(Transform parent, Vector3 corner, float sx, float sz)
    {
        for (int i = 0; i < 4; i++)
        {
            float k = 0.25f + i * 0.22f;
            var a = corner + new Vector3(sx * k, -0.02f, 0);
            var b = corner + new Vector3(0, -0.02f, sz * k);
            var mid = (a + b) / 2f + new Vector3(0, -0.06f, 0);
            float len = Vector3.Distance(a, b);
            DPrim(PrimitiveType.Cube, "Strand", parent, mid, new Vector3(0.006f, 0.006f, len), M("Cobweb"), Quaternion.LookRotation(b - a));
        }
        for (int i = 0; i < 3; i++)
        {
            var end = corner + new Vector3(sx * 1.0f * (i == 0 ? 1 : 0.6f), -0.02f - 0.05f * i, sz * (i == 2 ? 1 : 0.6f));
            DPrim(PrimitiveType.Cube, "Spoke", parent, (corner + end) / 2f, new Vector3(0.006f, 0.006f, Vector3.Distance(corner, end)), M("Cobweb"), Quaternion.LookRotation(end - corner));
        }
    }

    /// <summary>The standard dressing for a map room: trim, string lights, a doorway frame, a wreath over it and candy canes beside it.</summary>
    static void DressRoom(Transform g, string name, float zN, float zS, float w, float h, float doorN, float doorS, bool wainscot)
    {
        Trim(g, name, zN, zS, w, h, doorN, doorS, wainscot);
        StringLights(g, name, zS, zN, w, h);
        if (!float.IsNaN(doorN))
        {
            DoorFrame(g, doorN, zN + 0.1f);
            if (h >= 3.55f) Wreath(g, new Vector3(doorN, 3.25f, zN - 0.03f), Vector3.back);
            CandyCane(g, new Vector3(doorN - 1.75f, 0, zN - 0.35f), true);
            CandyCane(g, new Vector3(doorN + 1.75f, 0, zN - 0.35f), false);
        }
    }

    // =================================================================== the map

    class Slot { public Vector3 pos; public float yaw; public bool used; }

    /// <summary>
    /// The rooms of the map, south of the lobby along z (each room's north wall has a doorway at x = 0).
    /// Gag rooms line their side walls with slots; the original gags and skeleton routines fill them.
    /// </summary>
    static void BuildMap(Transform root, Transform spawn)
    {
        var map = Group(root, "Map", Vector3.zero);
        // name, z0 (north wall), z1 (south wall), width, height, floor, wall, ceiling
        var rooms = new[]
        {
            new { name = "GIFT WRAP HALL", z0 = -6f, z1 = -26f, w = 14f, h = 4.0f, floor = "Floor", wall = "Wall", ceil = "Ceiling" },
            new { name = "RAINBOW HALL", z0 = -26f, z1 = -42f, w = 16f, h = 5.0f, floor = "Floor2", wall = "Wall2", ceil = "Ceiling" },
            new { name = "TOY WORKSHOP", z0 = -42f, z1 = -62f, w = 14f, h = 4.0f, floor = "Floor2", wall = "BrickWall", ceil = "Ceiling" },
            new { name = "CHIMNEY LOUNGE", z0 = -62f, z1 = -80f, w = 16f, h = 3.6f, floor = "Rug", wall = "WallGreen", ceil = "Ceiling" },
            new { name = "TALENT SHOW", z0 = -80f, z1 = -98f, w = 18f, h = 5.5f, floor = "Floor2", wall = "WallDark", ceil = "Ceiling" },
            new { name = "THE BONE YARD", z0 = -98f, z1 = -118f, w = 18f, h = 3.4f, floor = "Concrete", wall = "Wall", ceil = "Ceiling" },
        };
        // where each room's north doorway is (x): the Talent Show's stage fills the middle, so its exit is on the east side
        float[] doorX = { 0f, 0f, 0f, 0f, 0f, 7.5f };
        var rt = new Transform[rooms.Length];
        for (int i = 0; i < rooms.Length; i++)
        {
            var r = rooms[i];
            var g = Group(map, r.name, Vector3.zero);
            rt[i] = g;
            float len = r.z0 - r.z1, zc = (r.z0 + r.z1) / 2f;
            Box(g, "Floor", new Vector3(0, -0.1f, zc), new Vector3(r.w, 0.2f, len), M(r.floor));
            Box(g, "Ceiling", new Vector3(0, r.h + 0.1f, zc), new Vector3(r.w, 0.2f, len), M(r.ceil));
            Box(g, "WallW", new Vector3(-r.w / 2 - 0.1f, r.h / 2, zc), new Vector3(0.2f, r.h, len), M(r.wall));
            Box(g, "WallE", new Vector3(r.w / 2 + 0.1f, r.h / 2, zc), new Vector3(0.2f, r.h, len), M(r.wall));
            float prevW = i == 0 ? 14.4f : rooms[i - 1].w, prevH = i == 0 ? 3.6f : rooms[i - 1].h;
            if (i > 0) DoorWall(g, "WallN", r.z0 - 0.1f, Mathf.Max(r.w, prevW) + 0.4f, Mathf.Max(r.h, prevH), M(r.wall), doorX[i]);
            if (i == rooms.Length - 1) Box(g, "WallS", new Vector3(0, r.h / 2, r.z1 - 0.1f), new Vector3(r.w + 0.4f, r.h, 0.2f), M(r.wall));
            // trim, string lights, a frame round the doorway in, a wreath and candy canes
            float zN = i == 0 ? -6.2f : r.z0 - 0.2f;
            float doorS = i < rooms.Length - 1 ? doorX[i + 1] : float.NaN;
            DressRoom(g, r.name, zN, r.z1, r.w, r.h, doorX[i], doorS, r.wall != "BrickWall");
            // the room's name over the doorway, readable as you walk up to it
            Text("RoomName", g, new Vector3(doorX[i], 3.12f, r.z0 + 0.03f), Face(Vector3.forward), new Vector2(i == 0 ? 6f : 4.5f, i == 0 ? 0.5f : 0.4f), 0.24f,
                 "<b>" + r.name + "</b>" + (i == 0 ? "\n<size=60%>the map: gags, skeletons and combo acts this way</size>" : "")).color = new Color(1f, 0.85f, 0.4f);
        }

        // ---- the original gags and skeleton routines, spread over the gag rooms
        var slots = new Dictionary<int, List<Slot>[]>();
        foreach (int i in new[] { 0, 2, 3, 5 })
        {
            var r = rooms[i];
            var sides = new List<Slot>[2];
            for (int sd = 0; sd < 2; sd++)
            {
                sides[sd] = new List<Slot>();
                float x = (sd == 0 ? -1 : 1) * (r.w / 2 - 1.0f);
                for (float z = r.z0 - 2.4f; z >= r.z1 + 1.6f; z -= 1.45f)
                    sides[sd].Add(new Slot { pos = new Vector3(x, 0, z), yaw = sd == 0 ? 90f : -90f });
            }
            slots[i] = sides;
        }
        // the Chimney Lounge keeps room for the fireplace (east wall) and the closet (west wall)
        Reserve(slots[3][1], -70f, -74.5f);
        Reserve(slots[3][0], -67.5f, -70.5f);

        int[] gagRoom = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
                          3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5 };
        int[] skelRoom = { 0, 0, 0, 2, 2, 2, 3, 3, 5, 5, 5, 5 };
        BuildSkeletonRoutines(map, slots, skelRoom);
        BuildGags(map, slots, gagRoom, rooms.Select(r => r.w).ToArray());

        // ---- room furnishings
        GiftWrapHall(rt[0]);
        RainbowHall(rt[1]);
        ToyWorkshop(rt[2]);
        ChimneyLounge(rt[3]);
        TalentShow(rt[4]);
        BoneYard(rt[5], spawn);
    }

    static void Reserve(List<Slot> side, float zFrom, float zTo)
    {
        foreach (var sl in side) if (sl.pos.z <= zFrom && sl.pos.z >= zTo) sl.used = true;
    }

    /// <summary>Takes k slots in a row on one side (the side with room first, alternating); returns their middle.</summary>
    static Slot TakeSlots(List<Slot>[] sides, int k, ref int turn)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var side = sides[(turn + attempt) % 2];
            for (int a = 0; a + k <= side.Count; a++)
            {
                bool free = true;
                for (int b = 0; b < k; b++) if (side[a + b].used) { free = false; break; }
                if (!free) continue;
                for (int b = 0; b < k; b++) side[a + b].used = true;
                turn++;
                var mid = new Vector3(side[a].pos.x, 0, (side[a].pos.z + side[a + k - 1].pos.z) / 2f);
                return new Slot { pos = mid, yaw = side[a].yaw };
            }
        }
        return null;
    }

    /// <summary>A wall across the room at z with a 2.4 x 2.8 m doorway centred at doorX.</summary>
    static void DoorWall(Transform g, string name, float z, float w, float h, Material m, float doorX = 0f)
    {
        float gw = 2.4f, gh = 2.8f;
        float l0 = -w / 2, l1 = doorX - gw / 2, r0 = doorX + gw / 2, r1 = w / 2;
        if (l1 - l0 > 0.01f) Box(g, name + " L", new Vector3((l0 + l1) / 2, h / 2, z), new Vector3(l1 - l0, h, 0.2f), m);
        if (r1 - r0 > 0.01f) Box(g, name + " R", new Vector3((r0 + r1) / 2, h / 2, z), new Vector3(r1 - r0, h, 0.2f), m);
        Box(g, name + " Top", new Vector3(doorX, (gh + h) / 2, z), new Vector3(gw, h - gh, 0.2f), m);
    }

    /// <summary>A speech bubble (hidden until its gag talks): dark backing, white text, turned to face you by the gag.</summary>
    static TextMeshPro Bubble(Transform parent, Vector3 pos)
    {
        var t = Text("Bubble", parent, pos, Quaternion.identity, new Vector2(1.8f, 0.5f), 0.11f, "");
        t.color = Color.white;
        Prim(PrimitiveType.Cube, "Backing", t.transform, new Vector3(0, 0, 0.012f), new Vector3(1.9f, 0.58f, 0.01f), M("Board"));
        t.gameObject.SetActive(false);
        return t;
    }

    // ---- the original 53 gags (Data/gags.txt), placed in the map's gag rooms

    static void BuildGags(Transform map, Dictionary<int, List<Slot>[]> slots, int[] gagRoom, float[] widths)
    {
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/gags.txt");
        if (ta == null) { report.Add("problem: Data/gags.txt is missing, so no gags"); return; }
        var g = Group(map, "Gags", Vector3.zero);
        var turns = new Dictionary<int, int>();
        int n = 0, placed = 0;
        foreach (var raw in ta.text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Trim() == "" || line.StartsWith("#")) continue;
            var f = line.Split('|');
            if (f.Length < 10) continue;
            int room = n < gagRoom.Length ? gagRoom[n] : 5;
            n++;
            if (!turns.ContainsKey(room)) turns[room] = n % 2;
            int turn = turns[room];
            Slot sl = TakeSlots(slots[room], 1, ref turn);
            turns[room] = turn;
            if (sl == null) { report.Add("problem: no room left for gag " + f[0]); continue; }
            string kind = f[1];
            var pos = sl.pos;
            if (kind == "frame")
            {
                // hung on the wall behind its slot
                float wx = (pos.x < 0 ? -1 : 1) * (widths[room] / 2 - 0.06f);
                pos = new Vector3(wx, 1.6f, pos.z);
            }
            string back = f[9];
            if (kind == "waddle")
            {
                float d = back != "" ? float.Parse(back, System.Globalization.CultureInfo.InvariantCulture) : 4f;
                back = Mathf.Min(d, widths[room] - 3f).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            Gag(g, f[0], kind, pos, sl.yaw, f[7], f[8].Replace("\\n", "\n"), back.Replace("\\n", "\n"));
            placed++;
        }
        report.Add(placed + " of " + n + " original gags placed in the map");
    }

    /// <summary>One gag: its prop, its voice line, its speech bubble and a FirGag set up for its kind.</summary>
    static void Gag(Transform parent, string id, string kind, Vector3 pos, float yaw, string say, string front, string back)
    {
        var go = new GameObject("Gag " + id + " (" + kind + ")");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var clip = Clip("Gags/gag_" + id);
        var gag = Add<FirGag>(go);
        gag.line = say;
        gag.holdTime = Mathf.Max(1.5f, clip != null ? clip.length : 2f);
        gag.voice = Src(go.transform, new Vector3(0, 1.0f, 0), clip, false, 1f, 1f, 14f, false);
        gag.bubble = Bubble(go.transform, kind == "frame" ? new Vector3(0, 0.75f, 0.35f) : new Vector3(0, 2.25f, 0.2f));
        gag.bubbleObject = gag.bubble.gameObject; gag.bubbleTransform = gag.bubble.transform;
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.5f, 0); col.size = new Vector3(0.8f, 1.0f, 0.8f);
        string verb = "Poke";
        Transform mouth = null;
        Func<Transform, Vector3, float, bool, string, Transform> GagTree = (at, p, h, hat, name) =>
        {
            var tr = Group(at, name, p);
            TreeModel(tr, h, hat, out mouth);
            return tr;
        };
        switch (kind)
        {
            case "sign":
            {
                Box(go.transform, "Post", new Vector3(0, 0.6f, 0), new Vector3(0.06f, 1.2f, 0.06f), M("DeskWood"), false);
                var board = Group(go.transform, "Board", new Vector3(0, 1.35f, 0));
                Box(board, "Panel", Vector3.zero, new Vector3(1.0f, 0.6f, 0.04f), M("Trim"), false);
                Text("Front", board, new Vector3(0, 0, 0.025f), Face(Vector3.forward), new Vector2(0.92f, 0.52f), 0.12f, "<color=#1a1a1a><b>" + front + "</b></color>");
                Text("Back", board, new Vector3(0, 0, -0.025f), Face(Vector3.back), new Vector2(0.92f, 0.52f), 0.12f, "<color=#a00000><b>" + back + "</b></color>");
                gag.target = board; gag.rotateBy = new Vector3(0, 180, 0);
                gag.inTime = 0.45f; gag.outTime = 0.45f; gag.holdTime += 1.5f;
                col.center = new Vector3(0, 1.0f, 0); col.size = new Vector3(1.0f, 1.7f, 0.25f);
                verb = "Flip the sign";
                break;
            }
            case "pop_gift":
            case "pop_crate":
            {
                bool gift = kind == "pop_gift";
                var bx = Prim(PrimitiveType.Cube, "Box", go.transform, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.6f, 0.6f), gift ? M("GiftRed") : M("Cardboard"));
                if (gift)
                {
                    Prim(PrimitiveType.Cube, "Ribbon", bx.transform, Vector3.zero, new Vector3(1.02f, 1.02f, 0.15f), M("GiftGold"));
                    Prim(PrimitiveType.Cube, "Ribbon2", bx.transform, Vector3.zero, new Vector3(0.15f, 1.02f, 1.02f), M("GiftGold"));
                }
                var t = GagTree(go.transform, new Vector3(0, 0.05f, 0), 0.6f, gift, "PopTree");
                gag.target = t; gag.hideWhenIdle = true;
                gag.moveBy = new Vector3(0, 0.62f, 0); gag.inTime = 0.18f; gag.outTime = 0.3f; gag.wobble = 4f;
                gag.sfx = Src(go.transform, new Vector3(0, 0.6f, 0), Clip("whoosh"), false, 1f, 0.8f, 10f, false);
                col.center = new Vector3(0, 0.3f, 0); col.size = new Vector3(0.65f, 0.65f, 0.65f);
                verb = gift ? "Open the present" : "Open the box";
                break;
            }
            case "spin":
                gag.target = GagTree(go.transform, Vector3.zero, 0.95f, false, "SpinTree"); gag.inTime = 0.25f; gag.outTime = 0.25f;
                gag.spin = 720f / gag.holdTime;   // exactly two turns, so it ends facing you
                verb = "Spin the tree";
                break;
            case "inflate_tree":
                gag.target = GagTree(go.transform, Vector3.zero, 0.9f, false, "PuffTree");
                gag.scaleBy = 1.9f; gag.wobble = 4f; gag.inTime = 0.5f; gag.outTime = 0.4f;
                break;
            case "inflate_crate":
            {
                var crate = Group(go.transform, "Crate", new Vector3(0, 0.35f, 0));
                Prim(PrimitiveType.Cube, "Box", crate, Vector3.zero, new Vector3(0.7f, 0.7f, 0.7f), M("Cardboard"));
                gag.target = crate; gag.scaleBy = 1.6f; gag.wobble = 6f; gag.inTime = 0.5f; gag.outTime = 0.3f;
                col.center = new Vector3(0, 0.35f, 0);
                verb = "Poke the box";
                break;
            }
            case "inflate_orn":
            {
                Prim(PrimitiveType.Cylinder, "Stand", go.transform, new Vector3(0, 0.35f, 0), new Vector3(0.05f, 0.35f, 0.05f), M("Metal"));
                var orn = Group(go.transform, "Ornament", new Vector3(0, 0.9f, 0));
                Prim(PrimitiveType.Sphere, "Ball", orn, Vector3.zero, Vector3.one * 0.3f, M("HatRed"));
                Prim(PrimitiveType.Cylinder, "Cap", orn, new Vector3(0, 0.16f, 0), new Vector3(0.07f, 0.03f, 0.07f), M("GiftGold"));
                Prim(PrimitiveType.Sphere, "Shine", orn, new Vector3(-0.06f, 0.07f, 0.12f), Vector3.one * 0.05f, M("Glint"));
                gag.target = orn; gag.scaleBy = 2.3f; gag.wobble = 6f; gag.inTime = 0.6f; gag.outTime = 0.25f;
                col.center = new Vector3(0, 0.7f, 0); col.size = new Vector3(0.5f, 1.4f, 0.5f);
                verb = "Poke the ornament";
                break;
            }
            case "talk":
                gag.target = GagTree(go.transform, Vector3.zero, 1.0f, false, "TalkTree"); gag.inTime = 0.1f; gag.outTime = 0.1f;
                verb = "Talk";
                break;
            case "shiver":
                gag.target = GagTree(go.transform, Vector3.zero, 0.9f, true, "ColdTree"); gag.wobble = 7f; gag.inTime = 0.2f; gag.outTime = 0.3f;
                break;
            case "frame":
            {
                var frame = Group(go.transform, "Frame", Vector3.zero);
                Prim(PrimitiveType.Cube, "Border", frame, Vector3.zero, new Vector3(0.75f, 0.95f, 0.03f), M("GiftGold"));
                Prim(PrimitiveType.Cube, "Canvas", frame, new Vector3(0, 0, 0.012f), new Vector3(0.62f, 0.82f, 0.01f), M("Paper"));
                Text("Caption", frame, new Vector3(0, 0, 0.02f), Face(Vector3.forward), new Vector2(0.56f, 0.76f), 0.08f, "<color=#1a1410><b>" + front + "</b></color>");
                float drop = 1.15f;
                gag.target = frame; gag.moveBy = new Vector3(0, -drop, 0.2f); gag.rotateBy = new Vector3(0, 0, -70f);
                gag.inTime = 0.35f; gag.outTime = 0.5f;
                gag.sfx = Src(go.transform, new Vector3(0, -drop, 0.2f), Clip("toilet_clunk"), false, 1f, 1f, 12f, false);
                gag.voice.transform.localPosition = new Vector3(0, 0, 0.3f);
                col.center = Vector3.zero; col.size = new Vector3(0.8f, 1.0f, 0.15f);
                verb = "Straighten the picture";
                break;
            }
            case "peek":
                gag.target = GagTree(go.transform, Vector3.zero, 0.75f, false, "PeekTree");
                gag.hideWhenIdle = true; gag.walkIn = true;
                gag.moveBy = new Vector3(0.7f, 0, 0); gag.inTime = 0.4f; gag.outTime = 0.4f;
                Object.DestroyImmediate(col); col = null;
                gag.zoneCenter = new Vector3(0, 1.5f, 1.5f); gag.zoneSize = new Vector3(3f, 3f, 3f);
                break;
            case "waddle":
            {
                float dist = back != "" ? float.Parse(back, System.Globalization.CultureInfo.InvariantCulture) : 4f;
                gag.target = GagTree(go.transform, Vector3.zero, 0.5f, true, "WaddleTree");
                gag.hideWhenIdle = true; gag.walkIn = true;
                gag.moveBy = new Vector3(0, 0, dist); gag.bob = 0.06f; gag.wobble = 3f;
                gag.inTime = dist / 1.3f; gag.outTime = dist / 1.3f; gag.holdTime = 0.8f;
                Object.DestroyImmediate(col); col = null;
                gag.zoneCenter = new Vector3(0, 1.5f, dist / 2f); gag.zoneSize = new Vector3(3f, 3f, dist + 1f);
                break;
            }
            default:
                report.Add("problem: unknown gag kind " + kind + " (" + id + ")");
                break;
        }
        gag.mouth = mouth;
        if (gag.target == null) gag.target = go.transform;
        SetInteract(gag, verb, 3f);
    }

    // ---- the original 12 skeleton routines (Data/skeleton_gags.txt)

    static void BuildSkeletonRoutines(Transform map, Dictionary<int, List<Slot>[]> slots, int[] skelRoom)
    {
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/skeleton_gags.txt");
        if (ta == null) { report.Add("problem: Data/skeleton_gags.txt is missing, so no skeleton routines"); return; }
        var g = Group(map, "SkeletonRoutines", Vector3.zero);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int n = 0, placed = 0;
        var turns = new Dictionary<int, int>();
        foreach (var raw in ta.text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Trim() == "" || line.StartsWith("#")) continue;
            var f = line.Split('|');
            if (f.Length < 11) continue;
            int room = n < skelRoom.Length ? skelRoom[n] : 5;
            n++;
            string id = f[0];
            int act = int.Parse(f[1], inv);
            var colours = f[6].Split(',');
            int k = Mathf.Max(1, Mathf.CeilToInt((colours.Length * 0.7f + 0.5f) / 1.45f));
            if (!turns.ContainsKey(room)) turns[room] = 0;
            int turn = turns[room];
            Slot sl = TakeSlots(slots[room], k, ref turn);
            turns[room] = turn;
            if (sl == null) { report.Add("problem: no room left for skeleton routine " + id); continue; }
            var go = new GameObject("Skeleton " + id + " (act " + act + ")");
            go.transform.SetParent(g, false);
            go.transform.localPosition = sl.pos;
            go.transform.localRotation = Quaternion.Euler(0, sl.yaw, 0);
            var pivots = new List<Transform>();
            var meshes = new List<Transform>();
            for (int r = 0; r < colours.Length; r++)
            {
                string c = colours[r].Trim();
                var bone = c == "red" ? M("BoneRed") : c == "green" ? M("BoneGreen") : M("BoneWhite");
                var rig = SkeletonRig(go.transform, "Rig" + r + " " + c, bone);
                rig[0].localPosition = new Vector3((r - (colours.Length - 1) / 2f) * 0.7f, 0, 0);
                rig[0].localRotation = Quaternion.identity;
                pivots.AddRange(rig);
                meshes.AddRange(lastRigMeshes);
            }
            var sg = Add<FirSkeletonGag>(go);
            sg.act = act; sg.rigCount = colours.Length;
            sg.pivots = pivots.ToArray(); sg.meshes = meshes.ToArray();
            sg.voiceDelay = float.Parse(f[8], inv);
            sg.line = f[9];
            sg.baseLength = act == 3 ? 5f : act == 2 ? 5.5f : 4f;
            sg.voice = Src(go.transform, new Vector3(0, 1.6f, 0.1f), Clip("Gags/skel_" + id), false, 1f, 1f, 14f, false);
            sg.bubble = Bubble(go.transform, new Vector3(0, 2.35f, 0.2f));
        sg.bubbleObject = sg.bubble.gameObject; sg.bubbleTransform = sg.bubble.transform;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.9f, 0); col.size = new Vector3(colours.Length * 0.7f + 0.2f, 1.8f, 0.5f);
            if (act == 5)
            {
                Box(go.transform, "MicStand", new Vector3(0, 0.75f, 0.45f), new Vector3(0.03f, 1.5f, 0.03f), M("Metal"), false);
                Prim(PrimitiveType.Sphere, "Mic", go.transform, new Vector3(0, 1.52f, 0.42f), new Vector3(0.07f, 0.1f, 0.07f), M("Black"));
            }
            if (act == 7) Prim(PrimitiveType.Cube, "Phone", pivots[6], new Vector3(0, -0.4f, 0.03f), new Vector3(0.05f, 0.2f, 0.06f), M("GiftRed"));
            if (f[10].Trim() != "")
            {
                var plac = Group(go.transform, "Placard", new Vector3(0, 0, 0.75f));
                Box(plac, "Post", new Vector3(0, 0.35f, 0), new Vector3(0.04f, 0.7f, 0.04f), M("DeskWood"), false);
                Box(plac, "Board", new Vector3(0, 0.78f, 0), new Vector3(1.1f, 0.3f, 0.03f), M("Trim"), false);
                Text("Text", plac, new Vector3(0, 0.78f, 0.02f), Face(Vector3.forward), new Vector2(1.0f, 0.26f), 0.14f, "<color=#1a1a1a><b>" + f[10] + "</b></color>");
            }
            SetInteract(sg, act == 3 || act == 8 ? "Poke the skeleton" : "Say hi", 3f);
            placed++;
        }
        report.Add(placed + " of " + n + " skeleton routines placed in the map");
    }

    // ---- the rooms' furnishings

    static void GiftWrapHall(Transform g)
    {
        // a mountain of presents in the middle, wrapping tables along the aisle
        string[] gm = { "GiftRed", "GiftGreen", "GiftGold", "GiftBlue" };
        for (int i = 0; i < 9; i++)
        {
            float a = i * 2.4f, r = 0.4f + 0.18f * (i % 3);
            float s = 0.35f + 0.12f * ((i * 7) % 3);
            Prim(PrimitiveType.Cube, "Present", g, new Vector3(Mathf.Cos(a) * r, s / 2 + (i > 5 ? 0.45f : 0), -16f + Mathf.Sin(a) * r) + new Vector3(-3.2f, 0, 0), Vector3.one * s, M(gm[i % 4]));
        }
        TreeAt(g, "HallTree", new Vector3(-3.2f, 0.6f, -16f), 180f, 2.2f, true);
        // ribbon spools by the pile, a stack of parcels, lamps, a snowy window and a poster
        string[] spools = { "CaneRed", "GiftGold", "GiftGreen" };
        for (int i = 0; i < 3; i++)
            DPrim(PrimitiveType.Cylinder, "Spool", g, new Vector3(-4.9f + i * 0.55f, 0.16f, -13.9f), new Vector3(0.32f, 0.12f, 0.32f), M(spools[i]), Quaternion.Euler(90, 0, 0));
        for (int k = 0; k < 4; k++)
        {
            float sz = 0.7f - k * 0.13f;
            DPrim(PrimitiveType.Cube, "Parcel", g, new Vector3(3.0f + (k % 2) * 0.05f, 0.35f + k * 0.6f - k * k * 0.04f, -19f), new Vector3(sz, 0.55f, sz), M(gm[(k + 1) % 4]), Quaternion.Euler(0, k * 17f, 0));
        }
        foreach (float z in new[] { -10f, -16f, -22f }) PendantLamp(g, new Vector3(0, 4f, z), 0.9f);
        SnowWindow(g, new Vector3(-4.5f, 1.9f, -25.97f), Vector3.forward, 1.8f, 1.3f);
        Poster(g, "poster_safety", new Vector3(4.5f, 1.9f, -25.97f), Vector3.forward, 0.9f, 1.2f);
    }

    static void RainbowHall(Transform g)
    {
        string[] colours = { "Red", "Orange", "Yellow", "Green", "Blue", "Indigo", "Violet" };
        string[] hex = { "#ff3030", "#ff9020", "#ffe030", "#40ff50", "#4080ff", "#7a5cff", "#c060ff" };
        string[] clips = { "skeleton_red", "Rainbow/rb_orange", "Rainbow/rb_yellow", "Rainbow/rb_green", "Rainbow/rb_blue", "Rainbow/rb_indigo", "Rainbow/rb_violet" };
        // a riser at the far end, the seven of them in a row facing the doorway, their names on a board behind
        Box(g, "Riser", new Vector3(0, 0.15f, -38.2f), new Vector3(11f, 0.3f, 2.2f), M("Stage"));
        Box(g, "NameBoard", new Vector3(0, 2.75f, -39.45f), new Vector3(11f, 0.6f, 0.04f), M("Board"));
        for (int i = 0; i < 7; i++)
        {
            // they face the doorway (turned half round), so their right is the room's -x: run the line from +x
            // to -x, so neighbours are where the combo acts expect (yellow on orange's right) and the rainbow
            // reads red to violet, left to right, as you walk in
            float x = 4.5f - i * 1.5f;
            GagSkeleton(g, colours[i], new Vector3(x, 0.3f, -38.3f), 180f, 1f, i, hex[i], clips[i],
                        new[] { M("Bone" + colours[i]) }, new Vector3(x, 2.75f, -39.42f), Vector3.forward);
        }
        // a rainbow arch on the far wall, framing the doorway behind the riser
        string[] band = { "BoneRed", "BoneOrange", "BoneYellow", "BoneGreen", "BoneBlue", "BoneIndigo", "BoneViolet" };
        for (int b = 0; b < 7; b++)
        {
            float rad = 4.4f - b * 0.14f;
            for (int k = 0; k < 26; k++)
            {
                float a = Mathf.PI * (k + 0.5f) / 26f;
                DPrim(PrimitiveType.Cube, "Arch", g, new Vector3(Mathf.Cos(a) * rad, 0.3f + Mathf.Sin(a) * rad, -41.96f), new Vector3(0.56f, 0.13f, 0.02f), M(band[b]), Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90f));
            }
        }
        // stage lights on stands at the riser's corners, lamps and posters
        foreach (float sx in new[] { -6.2f, 6.2f })
        {
            DPrim(PrimitiveType.Cylinder, "LightStand", g, new Vector3(sx, 1.1f, -37.2f), new Vector3(0.06f, 1.1f, 0.06f), M("Black"));
            DPrim(PrimitiveType.Cylinder, "LightHead", g, new Vector3(sx, 2.25f, -37.2f), new Vector3(0.3f, 0.2f, 0.3f), M("Black"), Quaternion.Euler(60, sx > 0 ? -40 : 40, 0));
            DPrim(PrimitiveType.Sphere, "LightGlow", g, new Vector3(sx - Mathf.Sign(sx) * 0.12f, 2.2f, -37.4f), Vector3.one * 0.18f, M("GlowWarm"));
        }
        foreach (float sx in new[] { -4f, 4f }) PendantLamp(g, new Vector3(sx, 5f, -30.5f), 1.4f);
        Poster(g, "poster_original", new Vector3(-5f, 2.2f, -26.23f), Vector3.back, 0.9f, 1.2f);
        Poster(g, "poster_warning", new Vector3(5f, 2.2f, -26.23f), Vector3.back, 0.9f, 1.2f);
        var console = Console(g, "RainbowConsole", new Vector3(0, 1.25f, -31.5f), Vector3.forward, "COMBO ACTS  <size=70%>the rainbow</size>");
        ComboAct(console, 0, 0, "Traffic Lights", new[] { "Green", "Yellow", "Red" },
                 new[] { "traffic_green", "traffic_yellow", "traffic_red" }, new[] { 0, 1, 2 }, 1.3f);
        ComboAct(console, 1, 1, "Knock Knock", new[] { "Orange", "Yellow" },
                 new[] { "knock_1", "knock_2", "knock_3", "knock_4", "knock_5", "knock_6" }, new[] { 0, 1, 0, 1, 0, 1 }, 1.4f);
        ComboAct(console, 2, 2, "The Bone-Tones", new[] { "Blue", "Indigo", "Violet" },
                 new[] { "bonetones_1", "bonetones_2", "bonetones_3" }, new[] { -1, 1, -1 }, 2.0f);
        ComboAct(console, 5, 5, "RAINBOW FINALE", colours,
                 new[] { "finale_1", "finale_2" }, new[] { -1, -1 }, 3.6f);
    }

    static void ToyWorkshop(Transform g)
    {
        // two long workbenches down the middle with half-built trees on them
        for (int b = 0; b < 2; b++)
        {
            float x = b == 0 ? -3.2f : 3.2f, z = -52f;
            Box(g, "Bench" + b, new Vector3(x, 0.45f, z), new Vector3(1.4f, 0.9f, 8f), M("DeskWood"));
            for (int k = 0; k < 4; k++) TreeAt(g, "Unfinished", new Vector3(x, 0.9f, z - 2.7f + k * 1.8f), 180f + k * 40f, 0.55f, k == 1);
            foreach (float lz in new[] { -49f, -55f }) PendantLamp(g, new Vector3(x, 4f, lz), 1.3f);
        }
        // a pegboard of tools on the far wall, crates in the corner, a poster by the door
        Box(g, "Pegboard", new Vector3(-4.2f, 1.6f, -61.97f), new Vector3(2.6f, 1.4f, 0.03f), M("Pegboard"), false);
        for (int t = 0; t < 5; t++)
        {
            float tx = -5.2f + t * 0.5f;
            DPrim(PrimitiveType.Cylinder, "Handle", g, new Vector3(tx, 1.5f, -61.92f), new Vector3(0.05f, 0.2f, 0.05f), M("DeskWood"));
            DPrim(PrimitiveType.Cube, "Head", g, new Vector3(tx, 1.74f, -61.92f), t % 2 == 0 ? new Vector3(0.22f, 0.08f, 0.06f) : new Vector3(0.08f, 0.14f, 0.04f), M("Metal"));
        }
        DPrim(PrimitiveType.Cube, "Saw", g, new Vector3(-3.3f, 1.95f, -61.93f), new Vector3(0.7f, 0.18f, 0.01f), M("Metal"), Quaternion.Euler(0, 0, -8));
        for (int c = 0; c < 4; c++)
            DPrim(PrimitiveType.Cube, "Crate", g, new Vector3(5.3f + (c % 2) * 0.75f, 0.35f + (c / 2) * 0.7f, -61.2f), new Vector3(0.7f, 0.7f, 0.7f), M("Cardboard"), Quaternion.Euler(0, c * 11f, 0));
        Poster(g, "poster_recall", new Vector3(4.5f, 2.0f, -42.23f), Vector3.back, 0.9f, 1.2f);
    }

    static void ChimneyLounge(Transform g)
    {
        // the fireplace on the east wall, armchairs facing it, and the closet on the west wall
        Box(g, "Fireplace", new Vector3(7.4f, 0.8f, -72.2f), new Vector3(1.0f, 1.6f, 2.4f), M("BrickWall"));
        Box(g, "Chimney", new Vector3(7.6f, 2.6f, -72.2f), new Vector3(0.6f, 2.0f, 1.6f), M("BrickWall"));
        Prim(PrimitiveType.Cube, "Fire", g, new Vector3(6.88f, 0.42f, -72.2f), new Vector3(0.05f, 0.5f, 1.1f), M("GlowOrange"));
        Prim(PrimitiveType.Cube, "Mantel", g, new Vector3(7.2f, 1.62f, -72.2f), new Vector3(1.3f, 0.06f, 2.8f), M("DeskWood"));
        for (int i = 0; i < 2; i++)
        {
            var chair = Group(g, "Armchair" + i, new Vector3(4.6f, 0, -71.2f - i * 2.0f));
            chair.localRotation = Quaternion.Euler(0, 90, 0);
            Box(chair, "Seat", new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.8f), M("Curtain"));
            Box(chair, "Back", new Vector3(0, 0.75f, -0.35f), new Vector3(0.9f, 0.6f, 0.15f), M("Curtain"));
        }
        BuildSkeletonCloset(g, new Vector3(-7.2f, 0, -69.0f));
        // a rug and a side table by the fire, stockings and a garland on the mantel, a wreath on the chimney
        Rug(g, new Vector3(5.1f, 0, -72.2f), new Vector2(3.6f, 4.4f), "Rug", "TrimDark");
        DPrim(PrimitiveType.Cylinder, "SideTable", g, new Vector3(4.6f, 0.3f, -72.2f), new Vector3(0.5f, 0.3f, 0.5f), M("DeskWood"));
        DPrim(PrimitiveType.Cylinder, "LampBase", g, new Vector3(4.6f, 0.75f, -72.2f), new Vector3(0.08f, 0.15f, 0.08f), M("Brass"));
        DPrim(PrimitiveType.Cylinder, "LampShade", g, new Vector3(4.6f, 1.0f, -72.2f), new Vector3(0.36f, 0.12f, 0.36f), M("GlowWarm"));
        string[] sock = { "CaneRed", "GiftGreen", "CaneRed" };
        for (int i = 0; i < 3; i++) Stocking(g, new Vector3(6.52f, 1.52f, -71.4f - i * 0.8f), Vector3.left, sock[i]);
        Garland(g, new Vector3(6.5f, 1.7f, -73.55f), new Vector3(6.5f, 1.7f, -70.85f), 0.12f);
        Wreath(g, new Vector3(7.28f, 2.7f, -72.2f), Vector3.left, 0.32f);
        // a bookshelf and a window on the far wall
        Box(g, "Bookshelf", new Vector3(-4.6f, 1.1f, -79.78f), new Vector3(2.2f, 2.2f, 0.4f), M("TrimDark"), false);
        for (int sh = 0; sh < 4; sh++)
            for (int b = 0; b < 9; b++)
            {
                float bh = 0.32f - 0.05f * ((b * 7 + sh) % 3);
                DPrim(PrimitiveType.Cube, "Book", g, new Vector3(-5.5f + b * 0.21f, 0.2f + sh * 0.52f + bh / 2f, -79.55f), new Vector3(0.16f, bh, 0.28f), M("Book" + (1 + (b + sh) % 3)));
            }
        SnowWindow(g, new Vector3(4.5f, 1.8f, -79.97f), Vector3.forward, 1.8f, 1.3f);
    }

    static void TalentShow(Transform g)
    {
        string[] names2 = { "Pink", "Teal", "Gold", "Silver", "Black", "Brown", "Glow", "Rainbow", "Tiny", "Giant" };
        string[] hex2 = { "#ff70c0", "#30e0e0", "#ffc840", "#d0d8e8", "#9a9aa8", "#c08048", "#a0ff90", "#ffffff", "#ffffff", "#ffffff" };
        float[] scale2 = { 1, 1, 1, 1, 1, 1, 1, 1, 0.55f, 1.35f };
        string[] colours = { "Red", "Orange", "Yellow", "Green", "Blue", "Indigo", "Violet" };
        var rainbowBones = colours.Select(c => M("Bone" + c)).ToArray();
        // the stage across the far end, curtains, and the ten of them lined up on it
        Box(g, "Stage", new Vector3(0, 0.3f, -94.5f), new Vector3(14f, 0.6f, 5f), M("Stage"));
        Box(g, "CurtainBack", new Vector3(0, 2.9f, -97.7f), new Vector3(14f, 5.2f, 0.1f), M("Curtain"));
        Box(g, "CurtainL", new Vector3(-7.4f, 2.9f, -94.5f), new Vector3(1.2f, 5.2f, 0.12f), M("Curtain"));
        Text("ExitSign", g, new Vector3(8.95f, 2.6f, -95.5f), Face(Vector3.left), new Vector2(2.4f, 0.4f), 0.2f, "<b>THE BONE YARD</b>  >").color = new Color(1f, 0.85f, 0.4f);
        Box(g, "NameBoard", new Vector3(-0.15f, 3.35f, -97.6f), new Vector3(12.2f, 0.6f, 0.04f), M("Board"));
        Text("ShowTitle", g, new Vector3(0, 4.4f, -97.62f), Face(Vector3.forward), new Vector2(10f, 0.8f), 0.6f, "<b>TONIGHT: THE SKELETON CREW</b>").color = new Color(1f, 0.85f, 0.4f);
        for (int i = 0; i < 10; i++)
        {
            float x = 5.3f - i * 1.15f;   // turned to face the audience: run from +x so the giant is on tiny's right
            var bones = names2[i] == "Rainbow" ? rainbowBones : new[] { M(names2[i] == "Tiny" || names2[i] == "Giant" ? "BoneWhite" : "Bone" + names2[i]) };
            string title = names2[i] == "Rainbow" ? "<color=#ff3030>R</color><color=#ff9020>A</color><color=#ffe030>I</color><color=#40ff50>N</color><color=#4080ff>B</color><color=#7a5cff>O</color><color=#c060ff>W</color>" : null;
            // facing the audience (north)
            GagSkeleton(g, names2[i], new Vector3(x, 0.6f, -95.5f), 180f, scale2[i], 8 + i, hex2[i], "Rainbow/rb_" + names2[i].ToLower(),
                        bones, new Vector3(x, 3.35f, -97.57f), Vector3.forward, title);
        }
        // footlights along the stage's edge and marquee bulbs round the title
        for (float fx = -6.6f; fx <= 6.61f; fx += 0.45f)
            GameObjectUtility.SetStaticEditorFlags(Prim(PrimitiveType.Sphere, "Footlight", g, new Vector3(fx, 0.62f, -92.05f), Vector3.one * 0.1f, Bulb("BulbWarm")), StaticEditorFlags.BatchingStatic);
        for (int k = 0; k < 64; k++)
        {
            float t = k / 64f, px, py;
            if (t < 0.25f) { px = -5.2f + t / 0.25f * 10.4f; py = 4.95f; }
            else if (t < 0.5f) { px = 5.2f; py = 4.95f - (t - 0.25f) / 0.25f * 1.1f; }
            else if (t < 0.75f) { px = 5.2f - (t - 0.5f) / 0.25f * 10.4f; py = 3.85f; }
            else { px = -5.2f; py = 3.85f + (t - 0.75f) / 0.25f * 1.1f; }
            GameObjectUtility.SetStaticEditorFlags(Prim(PrimitiveType.Sphere, "Marquee", g, new Vector3(px, py, -97.6f), Vector3.one * 0.09f, Bulb(k % 2 == 0 ? "BulbWarm" : "BulbGold")), StaticEditorFlags.BatchingStatic);
        }
        // two rows of seats for the audience, with an aisle in the middle and a walkway along the east wall
        var seats = new List<Vector3>();
        foreach (float rz in new[] { -82.8f, -84.6f, -86.4f })
            for (float sx = -7.2f; sx <= 5.21f; sx += 0.9f)
            {
                if (Mathf.Abs(sx) < 1.7f) continue;
                Box(g, "Seat", new Vector3(sx, 0.23f, rz), new Vector3(0.7f, 0.46f, 0.6f), M("Seat"));
                Box(g, "SeatBack", new Vector3(sx, 0.75f, rz - 0.27f), new Vector3(0.7f, 0.6f, 0.08f), M("Seat"), false);
                seats.Add(new Vector3(sx, 0.47f, rz + 0.05f));
            }
        // the audience: 30 babies in the seats, every one of them throwable
        BuildAudience(g, seats);
        foreach (float sx in new[] { -6.6f, 6.6f })
        {
            DPrim(PrimitiveType.Cylinder, "LightStand", g, new Vector3(sx, 1.4f, -91.4f), new Vector3(0.06f, 1.4f, 0.06f), M("Black"));
            DPrim(PrimitiveType.Sphere, "LightGlow", g, new Vector3(sx, 2.85f, -91.4f), Vector3.one * 0.26f, M("GlowWarm"));
        }
        var console = Console(g, "TalentConsole", new Vector3(0, 1.25f, -87.5f), Vector3.forward, "COMBO ACTS  <size=70%>the talent show</size>");
        ComboAct(console, 3, 3, "Medal Ceremony", new[] { "Gold", "Silver", "Brown" },
                 new[] { "medal_gold", "medal_silver", "medal_brown" }, new[] { 0, 1, 2 }, 1.3f);
        ComboAct(console, 4, 4, "Upsy-Daisy", new[] { "Tiny", "Giant" },
                 new[] { "upsy_1", "upsy_2", "upsy_3", "upsy_4", "upsy_5" }, new[] { 0, 1, 0, 1, 0 }, 1.3f);
        ComboAct(console, 5, 5, "SHOW FINALE", names2,
                 new[] { "finale_1", "finale_2" }, new[] { -1, -1 }, 3.6f);
    }

    static void BoneYard(Transform g, Transform spawn)
    {
        // low beams, and the way back to the lobby at the far end
        for (int b = 0; b < 5; b++) Box(g, "Beam" + b, new Vector3(0, 3.2f, -101f - b * 4f), new Vector3(18f, 0.2f, 0.3f), M("DeskWood"), false);
        // ghostly lanterns, candles, a pile of skulls and cobwebs in the corners
        foreach (float z in new[] { -103f, -109f, -115f }) PendantLamp(g, new Vector3(0, 3.4f, z), 0.7f, "GlowGhost");
        Candles(g, new Vector3(-2.6f, 0, -105.5f), 5);
        Candles(g, new Vector3(2.6f, 0, -111f), 6);
        Candles(g, new Vector3(-2.2f, 0, -115.5f), 4);
        for (int k = 0; k < 7; k++)
        {
            var sp = new Vector3(3.2f + Mathf.Cos(k * 2.1f) * 0.3f * (k % 3), 0.1f + (k > 3 ? 0.16f : 0f), -105f + Mathf.Sin(k * 2.1f) * 0.3f * (k % 3));
            DPrim(PrimitiveType.Sphere, "Skull", g, sp, new Vector3(0.2f, 0.19f, 0.22f), M("BoneWhite"), Quaternion.Euler(0, k * 50f, 0));
            var fwd = Quaternion.Euler(0, k * 50f, 0) * Vector3.forward;
            var side = Quaternion.Euler(0, k * 50f, 0) * Vector3.right;
            DPrim(PrimitiveType.Sphere, "Socket", g, sp + fwd * 0.09f + side * 0.045f + Vector3.up * 0.02f, Vector3.one * 0.05f, M("Black"));
            DPrim(PrimitiveType.Sphere, "Socket", g, sp + fwd * 0.09f - side * 0.045f + Vector3.up * 0.02f, Vector3.one * 0.05f, M("Black"));
        }
        Cobweb(g, new Vector3(-9f, 3.4f, -98.2f), 1.3f, -1.3f);
        Cobweb(g, new Vector3(-9f, 3.4f, -118f), 1.3f, 1.3f);
        Cobweb(g, new Vector3(9f, 3.4f, -118f), -1.3f, 1.3f);
        var back = MakeButton<FirCheckButton>(g, "Back to the lobby", new Vector3(0, 1.3f, -117.85f), Vector3.forward, out var label);
        back.test = 11; back.title = "Back to the lobby"; back.label = label; back.destination = spawn;
        Text("EndNote", g, new Vector3(0, 2.6f, -117.97f), Face(Vector3.forward), new Vector2(8f, 0.5f), 0.22f,
             "<b>THE END</b> (for now): the horror comes next, once the systems check passes").color = Color.white;
    }

    // ---- the throwable babies (your uploaded model: Models/stewie_parts.txt)

    class ModelPart { public string tex; public List<Vector3> v = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<int> t = new List<int>(); public Mesh mesh; }
    static List<ModelPart> stewieParts;

    static List<ModelPart> LoadStewie()
    {
        if (stewieParts != null) return stewieParts;
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Models/stewie_parts.txt");
        if (ta == null) { report.Add("problem: Models/stewie_parts.txt is missing, so no audience"); return null; }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var list = new List<ModelPart>();
        ModelPart cur = null;
        foreach (var raw in ta.text.Split('\n'))
        {
            var p = raw.Trim().Split(' ');
            if (p.Length == 0 || p[0] == "" || p[0] == "#") continue;
            if (p[0] == "sub") { cur = new ModelPart { tex = p[1] }; list.Add(cur); }
            else if (p[0] == "v") { cur.v.Add(new Vector3(float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv))); cur.uv.Add(new Vector2(float.Parse(p[4], inv), float.Parse(p[5], inv))); }
            else if (p[0] == "t") { cur.t.Add(int.Parse(p[1])); cur.t.Add(int.Parse(p[2])); cur.t.Add(int.Parse(p[3])); }
        }
        foreach (var sp in list)
        {
            string path = Gen + "/Stewie_" + sp.tex + ".asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = m == null;
            if (isNew) m = new Mesh();
            m.Clear();
            m.name = "Stewie " + sp.tex;
            m.SetVertices(sp.v); m.SetUVs(0, sp.uv); m.SetTriangles(sp.t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            sp.mesh = m;
            if (!mats.ContainsKey("Stewie_" + sp.tex)) AddMat("Stewie_" + sp.tex, sp.tex, Color.white, 1, Color.black);
        }
        stewieParts = list;
        return list;
    }

    /// <summary>30 of the uploaded baby model sitting in the Talent Show's seats, facing the stage. Pick one up and throw it.</summary>
    static void BuildAudience(Transform g, List<Vector3> seats)
    {
        var parts = LoadStewie();
        if (parts == null) return;
        var aud = Group(g, "Audience", Vector3.zero);
        int n = Mathf.Min(30, seats.Count);
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Baby " + (i + 1));
            go.transform.SetParent(aud, false);
            go.transform.localPosition = seats[i];
            go.transform.localRotation = Quaternion.Euler(0, 180f + ((i * 37) % 21 - 10), 0);   // the model faces +z: turned to watch the stage, not quite in step
            foreach (var sp in parts)
            {
                var pm = new GameObject("Mesh " + sp.tex);
                pm.transform.SetParent(go.transform, false);
                pm.AddComponent<MeshFilter>().sharedMesh = sp.mesh;
                NoShadows(pm.AddComponent<MeshRenderer>(), M("Stewie_" + sp.tex));
            }
            go.layer = 13;   // VRChat's Pickup layer
            foreach (Transform ch in go.GetComponentsInChildren<Transform>(true)) ch.gameObject.layer = 13;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.45f, -0.06f); col.size = new Vector3(0.36f, 0.9f, 0.34f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1.5f; rb.useGravity = true; rb.isKinematic = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            var pick = go.AddComponent<VRCPickup>();
            pick.InteractionText = "Pick up the baby";
            pick.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes;
            pick.proximity = 2f;
            go.AddComponent<VRCObjectSync>();
            var th = Add<FirThrowable>(go);
            th.pickup = pick; th.body = rb;
            th.audioSrc = Src(go.transform, new Vector3(0, 0.5f, 0), null, false, 1f, 1f, 14f, false);
            th.grabClip = Clip("whoosh"); th.landClip = Clip("toilet_clunk");
        }
        Text("AudienceSign", g, new Vector3(-8.95f, 2.4f, -84.6f), Face(Vector3.right), new Vector2(3.4f, 0.6f), 0.2f,
             "<b>THE AUDIENCE</b>\n<size=70%>pick one up and throw it</size>").color = new Color(1f, 0.85f, 0.4f);
        report.Add(n + " throwable babies in the Talent Show's seats");
    }

    /// <summary>A Combo Acts console: a dark board on legs, facing `normal`, with a title. Buttons go on with ComboAct.</summary>
    static Transform Console(Transform parent, string name, Vector3 pos, Vector3 normal, string title)
    {
        var c = Group(parent, name, pos);
        c.localRotation = Face(normal);
        Box(c, "ConsoleBoard", new Vector3(0, 0, 0.12f), new Vector3(2.7f, 1.5f, 0.06f), M("Board"));
        Box(c, "LegL", new Vector3(-1.1f, -0.85f, 0.12f), new Vector3(0.08f, 0.8f, 0.06f), M("Board"));
        Box(c, "LegR", new Vector3(1.1f, -0.85f, 0.12f), new Vector3(0.08f, 0.8f, 0.06f), M("Board"));
        Text("ComboTitle", c, new Vector3(0, 0.6f, 0.085f), Quaternion.identity, new Vector2(2.5f, 0.24f), 0.2f, "<b>" + title + "</b>").color = new Color(1f, 0.85f, 0.4f);
        return c;
    }

    /// <summary>A decorative talking-tree model (no script).</summary>
    static Transform TreeAt(Transform parent, string name, Vector3 pos, float yaw, float height, bool hat)
    {
        var t = Group(parent, name, pos);
        t.localRotation = Quaternion.Euler(0, yaw, 0);
        Transform mouth;
        TreeModel(t, height, hat, out mouth);
        return t;
    }

    /// <summary>The RED SKELETON closet: open it and a white skeleton steps out ("A skeleton."), then a red one ("Red skeleton, ha.").</summary>
    static void BuildSkeletonCloset(Transform g, Vector3 at)
    {
        var c = Group(g, "SkeletonCloset", at);
        Box(c, "ClosetBody", new Vector3(0, 1.15f, 0), new Vector3(0.7f, 2.3f, 1.5f), M("DeskWood"));
        Prim(PrimitiveType.Cube, "Doorway", c, new Vector3(0.352f, 1.05f, 0), new Vector3(0.01f, 2.1f, 1.3f), M("Pupil"));
        // hinged on the +Z edge so it swings open away from where the skeletons walk off (-Z)
        var hinge = Group(c, "Hinge", new Vector3(0.38f, 0, 0.66f));
        var door = Box(hinge, "Door", new Vector3(0, 1.05f, -0.66f), new Vector3(0.05f, 2.1f, 1.3f), M("Trim"));
        Prim(PrimitiveType.Sphere, "Knob", door.transform, new Vector3(0.04f, 0, -0.5f), Vector3.one * 0.07f, M("GiftGold"));
        Text("Sign", door.transform, new Vector3(0.03f, 0.55f, 0), Face(Vector3.right), new Vector2(0.9f, 0.3f), 0.09f,
             "<color=#3a1a10><b>CLOSET</b>\n<size=70%>do not open</size></color>");
        var white = SkeletonRig(c, "WhiteSkeleton", M("BoneWhite"));
        var red = SkeletonRig(c, "RedSkeleton", M("BoneRed"));
        var mist = Group(c, "Mist", Vector3.zero).gameObject;
        float[,] puffs = { { 0.5f, 0.1f, 0.9f, 0.7f }, { 0.9f, -0.35f, 0.8f, 0.6f }, { 1.0f, 0.4f, 0.7f, 0.55f },
                           { 1.35f, 0.0f, 0.9f, 0.7f }, { 1.6f, -0.5f, 0.6f, 0.45f }, { 1.7f, 0.55f, 0.55f, 0.4f },
                           { 0.9f, -1.3f, 0.8f, 0.9f }, { 0.8f, -2.3f, 0.7f, 0.8f } };
        for (int i = 0; i < puffs.GetLength(0); i++)
            Prim(PrimitiveType.Sphere, "Puff", mist.transform, new Vector3(puffs[i, 0], 0.02f, puffs[i, 1]),
                 new Vector3(puffs[i, 2], 0.08f + 0.03f * (i % 2), puffs[i, 3]), M("Mist"));
        var cl = Add<FirCloset>(door);
        cl.hinge = hinge; cl.white = white; cl.red = red; cl.mist = mist;
        cl.creak = Src(c, new Vector3(0.4f, 1.2f, 0), Clip("creak"), false, 1f, 1f, 14f, false);
        cl.whiteVoice = Src(white[2], new Vector3(0, 0.1f, 0.1f), Clip("skeleton_white"), false, 1f, 1f, 16f, false);
        cl.redVoice = Src(red[2], new Vector3(0, 0.1f, 0.1f), Clip("skeleton_red"), false, 1f, 1f, 16f, false);
        cl.bubble = Bubble(c, new Vector3(1.2f, 2.5f, -0.6f));
        cl.bubbleObject = cl.bubble.gameObject; cl.bubbleTransform = cl.bubble.transform;
        SetInteract(cl, "Open the closet", 2.5f);
    }

    /// <summary>One of the gag skeletons: your skeleton model in its colour(s), its routine (act) and its voice line.</summary>
    static void GagSkeleton(Transform parent, string name, Vector3 pos, float yaw, float scale, int act, string hex, string clip,
                            Material[] bones, Vector3 labelPos, Vector3 labelNormal, string title = null)
    {
        var go = new GameObject(name + " Skeleton");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = Vector3.one * scale;
        var ps = SkeletonRig(go.transform, "Rig", bones[0], bones);
        var ms = lastRigMeshes;
        rigs[name] = ps;
        var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.95f, 0); col.size = new Vector3(0.7f, 1.9f, 0.5f);
        var rs = Add<FirRainbowSkeleton>(go);
        string shown = title ?? "<color=" + hex + ">" + name.ToUpper() + "</color>";
        rs.act = act; rs.colourName = shown;
        rs.pivots = ps; rs.meshes = ms;
        rs.voice = Src(ps[2], Vector3.zero, Clip(clip), false, 1f, 1f, 15f, false);
        rs.label = Text(name + "Label", parent, labelPos, Face(labelNormal), new Vector2(1.1f, 0.5f), 0.16f, "<b>" + shown + "</b>\n<size=70%>press me</size>");
        rs.label.color = Color.white;
        SetInteract(rs, "Press the " + name.ToLower() + " skeleton", 3f);
    }

    /// <summary>
    /// A combination act on the console (slot 0-5): its button, the skeletons taking part (by name), and its
    /// lines (Audio/Combo/name.ogg) with who says each (-1 = everyone). Lines follow each other with a short gap,
    /// timed from the real clip lengths.
    /// </summary>
    static void ComboAct(Transform console, int slot, int combo, string title, string[] who, string[] clips, int[] speakers, float tail)
    {
        var go = new GameObject("Combo " + title);
        go.transform.SetParent(console, false);
        go.transform.localPosition = new Vector3(-0.85f + (slot % 3) * 0.85f, 0.2f - (slot / 3) * 0.62f, 0);
        var size = new Vector3(0.62f, 0.2f, 0.1f);
        go.AddComponent<BoxCollider>().size = size;
        Prim(PrimitiveType.Cube, "Visual", go.transform, Vector3.zero, size, M(combo == 5 ? "ButtonGold" : "Button"));
        Text("Press", go.transform, new Vector3(0, 0, -size.z / 2 - 0.003f), Quaternion.identity, new Vector2(0.58f, 0.17f), 0.1f, "PLAY").color = Color.white;
        var label = Text("Label", go.transform, new Vector3(0, 0.24f, -size.z / 2 - 0.003f), Quaternion.identity, new Vector2(0.8f, 0.26f), 0.075f,
                         "<b>" + title + "</b>\n<color=#ff6060>script not running</color>");
        label.color = Color.white;
        var c = Add<FirSkeletonCombo>(go);
        c.combo = combo; c.title = title; c.label = label;
        var piv = new List<Transform>();
        foreach (var n in who)
        {
            if (!rigs.TryGetValue(n, out var r)) { report.Add("problem: combo " + title + " has no skeleton " + n); return; }
            piv.AddRange(r);
        }
        c.pivots = piv.ToArray();
        var src = new AudioSource[clips.Length];
        var start = new float[clips.Length];
        float t = 0.6f;
        for (int i = 0; i < clips.Length; i++)
        {
            int sp = speakers[i] >= 0 ? speakers[i] : who.Length / 2;
            var clip = Clip("Combo/" + clips[i]);
            src[i] = Src(c.pivots[sp * 10 + 2], Vector3.zero, clip, false, 1f, 1f, combo == 5 ? 30f : 18f, false);
            start[i] = t;
            t += (clip != null ? clip.length : 2f) + 0.35f;
        }
        c.lines = src; c.lineStart = start; c.lineSpeaker = speakers;
        c.length = t + tail;
        SetInteract(c, "Play: " + title, 3f);
    }

    // =================================================================== what the builder can see

    static string BuildReport(Transform root)
    {
        string usharp = PackageVersion(typeof(UdonSharpBehaviour).Assembly);
        string sdk = PackageVersion(typeof(VRC.SDKBase.VRCPlayerApi).Assembly);
        report.Add("UdonSharp " + usharp + ", VRChat Worlds SDK " + sdk + ", Unity " + Application.unityVersion);
        int behaviours = 0, backed = 0, programs = 0, linksStored = 0, links = 0;
        var missing = new List<string>();
        foreach (var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true))
        {
            behaviours++;
            var u = Backing(b);
            if (u == null) { missing.Add(b.GetType().Name + " has no UdonBehaviour"); continue; }
            backed++;
            var ps = GetMember(u, "programSource");
            if (ps != null) programs++; else missing.Add(b.GetType().Name + " has no program");
            foreach (var f in b.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                bool isLink = typeof(VRC.Udon.UdonBehaviour).IsAssignableFrom(f.FieldType) || typeof(UdonSharpBehaviour).IsAssignableFrom(f.FieldType);
                if (!isLink || f.GetValue(b) == null) continue;
                links++;
                if (StoredInUdon(u, f.Name)) linksStored++;
            }
        }
        report.Add(behaviours + " scripts, " + backed + " with an UdonBehaviour, " + programs + " with a program");
        report.Add(linksStored + " of " + links + " links between scripts found in the Udon data at build time");
        foreach (var m in missing.Take(6)) report.Add("problem: " + m);
        return "<b>BUILD INFO</b>\n" + string.Join("\n", report.Take(9)) + "\n<size=70%>(built " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")</size>";
    }

    static string PackageVersion(Assembly a)
    {
        try
        {
            var pi = UnityEditor.PackageManager.PackageInfo.FindForAssembly(a);
            return pi != null ? pi.version : "unknown";
        }
        catch (Exception) { return "unknown"; }
    }

    static object GetMember(object o, string name)
    {
        var t = o.GetType();
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) return f.GetValue(o);
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return p != null ? p.GetValue(o) : null;
    }

    /// <summary>Whether the UdonBehaviour's public variable table holds a non-null value for this name.</summary>
    static bool StoredInUdon(VRC.Udon.UdonBehaviour u, string name)
    {
        try
        {
            var table = GetMember(u, "publicVariables");
            if (table == null) return false;
            var m = table.GetType().GetMethods().FirstOrDefault(x => x.Name == "TryGetVariableValue" && x.GetParameters().Length == 2 && !x.IsGenericMethod);
            if (m == null) return false;
            var args = new object[] { name, null };
            return (bool)m.Invoke(table, args) && args[1] != null;
        }
        catch (Exception) { return false; }
    }

    // =================================================================== props

    static T MakeButton<T>(Transform parent, string name, Vector3 pos, Vector3 normal, out TextMeshPro label) where T : UdonSharpBehaviour
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Face(normal);
        var size = new Vector3(0.9f, 0.28f, 0.12f);
        go.AddComponent<BoxCollider>().size = size;
        Prim(PrimitiveType.Cube, "Visual", go.transform, Vector3.zero, size, M("Button"));
        Text("Press", go.transform, new Vector3(0, 0, -size.z / 2 - 0.003f), Quaternion.identity, new Vector2(0.85f, 0.24f), 0.13f, "PRESS").color = Color.white;
        label = Text("Label", go.transform, new Vector3(0, 0.36f, -size.z / 2 - 0.003f), Quaternion.identity, new Vector2(2.9f, 0.4f), 0.12f, name + "\n<color=#ff6060>script not running</color>");
        label.color = Color.white;
        var b = Add<T>(go);
        SetInteract(b, name, 4f);
        return b;
    }

    /// <summary>
    /// The original Fir Warning talking tree: a jagged, tiered fir canopy with big eyes, red lips, teeth and
    /// red scream eyes, on a pot (or a stand when tall). Returns the head pivot (canopy and face) and the mouth.
    /// </summary>
    static Transform TreeModel(Transform root, float height, bool hat, out Transform mouth)
    {
        float potH = Mathf.Max(0.08f, height * 0.09f);
        float trunkH = height * 0.08f;
        float canopyH = height - potH - trunkH;
        float radius = height * 0.36f;
        Prim(PrimitiveType.Cylinder, "Pot", root, new Vector3(0, potH / 2, 0), new Vector3(radius * 0.55f, potH / 2, radius * 0.55f), M(height > 1.8f ? "Stand" : "Pot"));
        Prim(PrimitiveType.Cylinder, "Trunk", root, new Vector3(0, potH + trunkH / 2, 0), new Vector3(radius * 0.18f, trunkH / 2 + 0.02f, radius * 0.18f), M("Trunk"));
        var head = Group(root, "Head", new Vector3(0, potH + trunkH, 0));
        var canopy = new GameObject("Canopy");
        canopy.transform.SetParent(head, false);
        canopy.AddComponent<MeshFilter>().sharedMesh = FirMesh(Mathf.Clamp(Mathf.RoundToInt(height * 2.2f), 3, 6), canopyH, radius, 14, 0.7f);
        NoShadows(canopy.AddComponent<MeshRenderer>(), M("Needles"));
        float faceY = canopyH * 0.5f;
        float rAt = radius * (1f - faceY / canopyH) * 1.05f;
        float eye = Mathf.Max(0.035f, canopyH * 0.055f);
        float faceZ = rAt * 0.92f;
        var eyeL = Prim(PrimitiveType.Sphere, "EyeL", head, new Vector3(-eye * 1.25f, faceY + eye * 1.1f, faceZ), Vector3.one * eye * 2f, M("Eye"));
        var eyeR = Prim(PrimitiveType.Sphere, "EyeR", head, new Vector3(eye * 1.25f, faceY + eye * 1.1f, faceZ), Vector3.one * eye * 2f, M("Eye"));
        Prim(PrimitiveType.Sphere, "PupilL", head, eyeL.transform.localPosition + new Vector3(0, 0, eye * 0.85f), Vector3.one * eye * 0.8f, M("Pupil"));
        Prim(PrimitiveType.Sphere, "PupilR", head, eyeR.transform.localPosition + new Vector3(0, 0, eye * 0.85f), Vector3.one * eye * 0.8f, M("Pupil"));
        var mouthPivot = Group(head, "Mouth", new Vector3(0, faceY - eye * 1.4f, faceZ - eye * 0.2f));
        Prim(PrimitiveType.Sphere, "Lips", mouthPivot, Vector3.zero, new Vector3(eye * 3.2f, eye * 1.6f, eye * 1.2f), M("Mouth"));
        Prim(PrimitiveType.Sphere, "Throat", mouthPivot, new Vector3(0, 0, eye * 0.25f), new Vector3(eye * 2.4f, eye * 1.1f, eye * 0.9f), M("Black"));
        var toothMesh = FirMesh(1, eye * 0.55f, eye * 0.16f, 5, 1f);
        for (int row = 0; row < 2; row++)
            for (int k = 0; k < 6; k++)
            {
                var tooth = new GameObject("Tooth");
                tooth.transform.SetParent(mouthPivot, false);
                tooth.transform.localPosition = new Vector3((-1.05f + k * 0.42f) * eye, (row == 0 ? 0.42f : -0.42f) * eye, eye * 0.5f);
                tooth.transform.localRotation = Quaternion.Euler(row == 0 ? 180 : 0, 0, 0);
                tooth.AddComponent<MeshFilter>().sharedMesh = toothMesh;
                NoShadows(tooth.AddComponent<MeshRenderer>(), M("Teeth"));
            }
        if (hat) Hat(head, new Vector3(0, canopyH - 0.08f * height, 0), height * 0.55f, "Hat");
        mouth = mouthPivot;
        return head;
    }

    static Mesh FirMesh(int tiers, float height, float radius, int seg, float jag)
    {
        string key = "fir" + tiers + "_" + height.ToString("F3") + "_" + radius.ToString("F3") + "_" + seg + "_" + jag;
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tri = new List<int>();
        float tierH = height / (tiers * 0.7f + 0.3f);
        for (int k = 0; k < tiers; k++)
        {
            float y0 = k * tierH * 0.7f;
            float y1 = Mathf.Min(height, y0 + tierH);
            float r = radius * (1f - (float)k / (tiers + 0.4f));
            var apex = new Vector3(0, y1, 0);
            var inner = new Vector3(0, y0 + tierH * 0.25f, 0);
            int pts = seg * 2;
            for (int i = 0; i < pts; i++)
            {
                float a0 = i * Mathf.PI * 2 / pts, a1 = (i + 1) * Mathf.PI * 2 / pts;
                float r0 = i % 2 == 0 ? r : r * jag, r1 = (i + 1) % 2 == 0 ? r : r * jag;
                float d0 = i % 2 == 0 ? -tierH * 0.06f : 0, d1 = (i + 1) % 2 == 0 ? -tierH * 0.06f : 0;
                var p0 = new Vector3(Mathf.Cos(a0) * r0, y0 + d0, Mathf.Sin(a0) * r0);
                var p1 = new Vector3(Mathf.Cos(a1) * r1, y0 + d1, Mathf.Sin(a1) * r1);
                var outward = new Vector3(Mathf.Cos((a0 + a1) / 2), 0.3f, Mathf.Sin((a0 + a1) / 2));
                AddTri(v, uv, col, tri, apex, p0, p1, outward, height, 1f, 0.55f);
                AddTri(v, uv, col, tri, inner, p1, p0, Vector3.down, height, 0.3f, 0.45f);
            }
        }
        var m = new Mesh { name = key };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(tri, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        meshCache[key] = m;
        return m;
    }

    static void AddTri(List<Vector3> v, List<Vector2> uv, List<Color> col, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 outward,
                       float h, float shadeA, float shadeB)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { var t = b; b = c; c = t; }
        int s = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        foreach (var p in new[] { a, b, c })
            uv.Add(new Vector2((Mathf.Atan2(p.z, p.x) / (Mathf.PI * 2) + 0.5f) * 3f, p.y / Mathf.Max(0.1f, h) * 3f));
        col.Add(Color.white * shadeA); col.Add(Color.white * shadeB); col.Add(Color.white * shadeB);
        tri.Add(s); tri.Add(s + 1); tri.Add(s + 2);
    }

    static GameObject Hat(Transform parent, Vector3 pos, float size, string name)
    {
        var hat = new GameObject(name);
        hat.transform.SetParent(parent, false);
        hat.transform.localPosition = pos;
        hat.transform.localRotation = Quaternion.Euler(0, 0, 14);
        var cone = new GameObject("Cone");
        cone.transform.SetParent(hat.transform, false);
        cone.AddComponent<MeshFilter>().sharedMesh = FirMesh(1, size * 0.55f, size * 0.17f, 12, 1f);
        NoShadows(cone.AddComponent<MeshRenderer>(), M("HatRed"));
        Prim(PrimitiveType.Cylinder, "Brim", hat.transform, new Vector3(0, 0.01f, 0), new Vector3(size * 0.38f, size * 0.03f, size * 0.38f), M("HatWhite"));
        Prim(PrimitiveType.Sphere, "Pompom", hat.transform, new Vector3(0, size * 0.55f, 0), Vector3.one * size * 0.09f, M("HatWhite"));
        return hat;
    }

    // ---- your skeleton model (Models/skeleton_parts.txt, made from the Skeleton.zip you uploaded)

    class SkelPart { public string name, parent; public Vector3 pivot; public List<Vector3> v = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<int> t = new List<int>(); public Mesh mesh; }
    static List<SkelPart> skelParts;

    static List<SkelPart> LoadSkeletonParts()
    {
        if (skelParts != null) return skelParts;
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Models/skeleton_parts.txt");
        if (ta == null) { report.Add("problem: Models/skeleton_parts.txt is missing, so the skeleton is made of boxes"); return null; }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        Func<string, float> F = x => float.Parse(x, inv);
        var list = new List<SkelPart>();
        SkelPart cur = null;
        foreach (var raw in ta.text.Split('\n'))
        {
            var p = raw.Trim().Split(' ');
            if (p.Length == 0 || p[0] == "" || p[0] == "#") continue;
            if (p[0] == "part") { cur = new SkelPart { name = p[1], parent = p[2], pivot = new Vector3(F(p[3]), F(p[4]), F(p[5])) }; list.Add(cur); }
            else if (p[0] == "v") { cur.v.Add(new Vector3(F(p[1]), F(p[2]), F(p[3]))); cur.uv.Add(new Vector2(F(p[4]), F(p[5]))); }
            else if (p[0] == "t") { cur.t.Add(int.Parse(p[1])); cur.t.Add(int.Parse(p[2])); cur.t.Add(int.Parse(p[3])); }
        }
        foreach (var sp in list)
        {
            string path = Gen + "/Skeleton_" + sp.name + ".asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = m == null;
            if (isNew) m = new Mesh();
            m.Clear();
            m.name = "Skeleton " + sp.name;
            m.SetVertices(sp.v); m.SetUVs(0, sp.uv); m.SetTriangles(sp.t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            sp.mesh = m;
        }
        skelParts = list;
        return list;
    }

    /// <summary>Your skeleton, facing the parent's +Z. Returns 10 pivots: root, torso, skull, shoulderL, shoulderR, elbowL, elbowR, legL, legR, jaw.</summary>
    static Transform[] lastRigMeshes;
    static readonly Dictionary<string, Transform[]> rigs = new Dictionary<string, Transform[]>();

    static Transform[] SkeletonRig(Transform parent, string name, Material bone, Material[] perPart = null)
    {
        var parts = LoadSkeletonParts();
        if (parts == null)
        {
            var r = Group(parent, name, Vector3.zero);
            var ps = new Transform[10];
            ps[0] = r;
            string[] nm = { "", "torso", "skull", "shL", "shR", "elL", "elR", "legL", "legR", "jaw" };
            Vector3[] at = { Vector3.zero, new Vector3(0, 0.95f, 0), new Vector3(0, 1.55f, 0), new Vector3(-0.2f, 1.4f, 0), new Vector3(0.2f, 1.4f, 0),
                             new Vector3(-0.2f, 1.1f, 0), new Vector3(0.2f, 1.1f, 0), new Vector3(-0.1f, 0.9f, 0), new Vector3(0.1f, 0.9f, 0), new Vector3(0, 1.48f, 0.05f) };
            var pm = new Transform[10];
            pm[0] = Prim(PrimitiveType.Cube, "Pelvis", r, new Vector3(0, 0.9f, 0), new Vector3(0.25f, 0.1f, 0.12f), bone).transform;
            for (int i = 1; i < 10; i++) { ps[i] = Group(r, nm[i], at[i]); pm[i] = Prim(PrimitiveType.Cube, "Bone", ps[i], new Vector3(0, -0.12f, 0), new Vector3(0.08f, 0.25f, 0.08f), bone).transform; }
            lastRigMeshes = pm;
            return ps;
        }
        var root = Group(parent, name, Vector3.zero);
        var by = new Dictionary<string, Transform>();
        var mesh = new Dictionary<string, Transform>();
        int partIndex = 0;
        foreach (var sp in parts)
        {
            Transform t = sp.parent == "-" ? root : Group(by[sp.parent], sp.name, sp.pivot);
            var go = new GameObject(sp.name + " Mesh");
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = sp.mesh;
            NoShadows(go.AddComponent<MeshRenderer>(), perPart != null && perPart.Length > 1 ? perPart[partIndex % perPart.Length] : bone);
            partIndex++;
            by[sp.name] = t;
            mesh[sp.name] = go.transform;
        }
        string[] order = { "pelvis", "torso", "skull", "upperL", "upperR", "foreL", "foreR", "legL", "legR", "jaw" };
        lastRigMeshes = order.Select(o => mesh[o]).ToArray();
        return new[] { root, by["torso"], by["skull"], by["upperL"], by["upperR"], by["foreL"], by["foreR"], by["legL"], by["legR"], by["jaw"] };
    }

    // =================================================================== basics

    static Transform Group(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    static Transform Marker(Transform parent, string name, Vector3 pos, float yaw)
    {
        var t = Group(parent, name, pos);
        t.localRotation = Quaternion.Euler(0, yaw, 0);
        return t;
    }

    /// <summary>A box without a collider (small props), or with one.</summary>
    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider)
    {
        var go = Box(parent, name, pos, size, mat);
        if (!collider) Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        return go;
    }

    /// <summary>A static box with a collider; its texture tiles every 2 m instead of stretching.</summary>
    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
        NoShadows(go.AddComponent<MeshRenderer>(), mat);
        go.AddComponent<BoxCollider>().size = size;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static Mesh BoxMesh(Vector3 size)
    {
        string key = "box" + size.x.ToString("F3") + "_" + size.y.ToString("F3") + "_" + size.z.ToString("F3");
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        var h = size * 0.5f;
        foreach (var N in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
        {
            var V = Mathf.Abs(N.y) > 0.5f ? Vector3.forward : Vector3.up;
            var U = Vector3.Cross(N, V);
            float hu = Mathf.Abs(Vector3.Dot(h, U)), hv = Mathf.Abs(Vector3.Dot(h, V)), hn = Mathf.Abs(Vector3.Dot(h, N));
            var c = N * hn;
            int s = v.Count;
            var corners = new[] { c - U * hu - V * hv, c - U * hu + V * hv, c + U * hu + V * hv, c + U * hu - V * hv };
            foreach (var p in corners)
            {
                v.Add(p); n.Add(N);
                uv.Add(new Vector2(Vector3.Dot(p, U) / 2f, Vector3.Dot(p, V) / 2f));
            }
            tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }
        var m = new Mesh { name = key };
        m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        meshCache[key] = m;
        return m;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);   // only the objects with scripts get colliders
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        NoShadows(go.GetComponent<MeshRenderer>(), mat);
        return go;
    }

    static void NoShadows(MeshRenderer r, Material mat)
    {
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    static Quaternion Face(Vector3 normal) { return Quaternion.LookRotation(-normal, Vector3.up); }

    static TextMeshPro Text(string name, Transform parent, Vector3 pos, Quaternion rot, Vector2 size, float lineHeight, string text)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>();
        t.rectTransform.sizeDelta = size;
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        t.fontSharedMaterial = fontMat;
        t.text = text;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        // lineHeight is in metres; a 3D TextMeshPro's font size is in points of about 0.1 m
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.1f;
        t.fontSizeMax = lineHeight * 10f;
        t.richText = true;
        return t;
    }

    static AudioSource Src(Transform parent, Vector3 pos, AudioClip clip, bool loop, float spatial, float vol, float maxDist, bool playOnAwake)
    {
        var go = new GameObject("Audio" + (clip != null ? " " + clip.name : ""));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip; a.loop = loop; a.spatialBlend = spatial; a.volume = vol; a.playOnAwake = playOnAwake;
        a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 1f; a.maxDistance = maxDist; a.dopplerLevel = 0;
        return a;
    }

    static AudioClip Clip(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + name + ".ogg");
        if (c == null) report.Add("problem: sound " + name + " didn't load");
        return c;
    }

    // ---- materials: the Lantern shader, fullbright (the test panel is lantern-lit so test 9 shows)

    static Material M(string name) { return mats[name]; }

    static Action<string, string, Color, float, Color> AddMatImpl;
    static void AddMat(string n, string tex, Color c, float full, Color glow) { AddMatImpl(n, tex, c, full, glow); }

    static void MakeMaterials()
    {
        var sh = Shader.Find("FirHorror/Lantern");
        if (sh == null) report.Add("problem: the FirHorror/Lantern shader didn't load");
        Action<string, string, Color, float, Color> A = AddMat;
        AddMatImpl = (n, texName, c, full, glow) =>
        {
            string path = Gen + "/Check_" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = m == null;
            if (isNew) m = new Material(sh != null ? sh : Shader.Find("Unlit/Texture"));
            if (sh != null) m.shader = sh;
            m.name = "Check_" + n;
            m.mainTexture = texName != null ? Tex(texName) : null;
            m.SetColor("_Color", c);
            m.SetFloat("_Fullbright", full);
            m.SetColor("_Glow", glow);
            m.SetFloat("_VertexColor", n == "Needles" ? 1f : 0f);
            m.SetFloat("_Reach", 14f);
            m.SetFloat("_Dark", 0f);
            m.SetFloat("_Ambient", 0.35f);
            m.SetColor("_AmbColor", new Color(0.2f, 0.2f, 0.2f, 1f));
            m.SetColor("_LanColor", new Color(1f, 0.95f, 0.85f, 1f));
            m.enableInstancing = true;
            if (isNew) AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
            mats[n] = m;
        };
        var white = Color.white;
        // the original lobby: red carpet, wallpaper, ceiling tiles (fullbright)
        A("Floor", "carpet_red", white, 1, Color.black);
        A("Wall", "wallpaper", new Color(0.8f, 0.8f, 0.8f), 1, Color.black);
        A("Ceiling", "ceiling", new Color(0.7f, 0.7f, 0.7f), 1, Color.black);
        // the test room: wood floor, wallpaper in another tint
        A("Floor2", "wood_floor", white, 1, Color.black);
        A("Wall2", "wallpaper", new Color(0.7f, 0.75f, 0.95f), 1, Color.black);
        A("Button", null, new Color(0.15f, 0.45f, 0.75f), 1, Color.black);
        A("Board", null, new Color(0.07f, 0.06f, 0.06f), 1, Color.black);
        A("ButtonGold", null, new Color(0.85f, 0.6f, 0.1f), 1, Color.black);
        // the map's rooms and props
        A("WallGreen", "wallpaper", new Color(0.65f, 0.9f, 0.65f), 1, Color.black);
        A("WallDark", "wallpaper", new Color(0.4f, 0.32f, 0.45f), 1, Color.black);
        A("BrickWall", "brick", white, 1, Color.black);
        A("Concrete", "concrete", white, 1, Color.black);
        A("Rug", "carpet_red", new Color(0.7f, 0.5f, 0.35f), 1, Color.black);
        A("Stage", "wood_floor", new Color(0.75f, 0.55f, 0.4f), 1, Color.black);
        A("Curtain", null, new Color(0.55f, 0.04f, 0.06f), 1, Color.black);
        A("DeskWood", "wood_floor", new Color(0.8f, 0.7f, 0.6f), 1, Color.black);
        A("Trim", null, new Color(0.85f, 0.83f, 0.78f), 1, Color.black);
        A("Cardboard", "cardboard", white, 1, Color.black);
        A("Metal", "metal", white, 1, Color.black);
        A("Paper", "paper", white, 1, Color.black);
        A("GiftRed", null, new Color(0.7f, 0.08f, 0.1f), 1, Color.black);
        A("GiftGreen", null, new Color(0.1f, 0.45f, 0.15f), 1, Color.black);
        A("GiftGold", null, new Color(0.85f, 0.65f, 0.15f), 1, Color.black);
        A("GiftBlue", null, new Color(0.15f, 0.25f, 0.6f), 1, Color.black);
        A("Glint", null, white, 1, new Color(0.6f, 0.6f, 0.5f));
        A("GlowOrange", null, new Color(0.8f, 0.35f, 0.05f), 1, new Color(0.6f, 0.25f, 0.02f));
        A("Mist", null, new Color(0.05f, 0.14f, 0.1f), 1, new Color(0.03f, 0.1f, 0.07f));
        // details
        A("TrimDark", "wood_floor", new Color(0.45f, 0.3f, 0.2f), 1, Color.black);
        A("Wainscot", "wood_floor", new Color(0.55f, 0.38f, 0.26f), 1, Color.black);
        A("Brass", "metal", new Color(0.95f, 0.75f, 0.35f), 1, Color.black);
        A("GlowWarm", null, new Color(1f, 0.9f, 0.6f), 1, new Color(0.6f, 0.5f, 0.3f));
        A("GlowGhost", null, new Color(0.5f, 1f, 0.6f), 1, new Color(0.3f, 0.7f, 0.4f));
        A("GlowFlame", null, new Color(1f, 0.7f, 0.2f), 1, new Color(0.8f, 0.45f, 0.1f));
        A("Wax", null, new Color(0.95f, 0.92f, 0.82f), 1, Color.black);
        A("CaneRed", null, new Color(0.85f, 0.06f, 0.08f), 1, Color.black);
        A("CaneWhite", null, new Color(0.97f, 0.97f, 0.97f), 1, Color.black);
        A("Snow", null, new Color(0.95f, 0.97f, 1f), 1, new Color(0.1f, 0.1f, 0.12f));
        A("WindowSnow", "window_snow", new Color(0.8f, 0.85f, 1f), 1, Color.black);
        A("Cobweb", null, new Color(0.85f, 0.85f, 0.85f), 1, Color.black);
        A("Book1", null, new Color(0.5f, 0.1f, 0.1f), 1, Color.black);
        A("Book2", null, new Color(0.1f, 0.3f, 0.15f), 1, Color.black);
        A("Book3", null, new Color(0.15f, 0.2f, 0.45f), 1, Color.black);
        A("Seat", null, new Color(0.5f, 0.06f, 0.08f), 1, Color.black);
        A("Pegboard", null, new Color(0.72f, 0.58f, 0.4f), 1, Color.black);
        A("TestPanel", null, new Color(0.8f, 0.8f, 0.8f), 0, Color.black);
        A("BoneWhite", "skeleton", new Color(1.05f, 1.02f, 0.96f), 1, new Color(0.1f, 0.1f, 0.09f));
        // the rainbow
        A("BoneRed", "skeleton", new Color(1f, 0.16f, 0.12f), 1, new Color(0.2f, 0.02f, 0.01f));
        A("BoneOrange", "skeleton", new Color(1f, 0.5f, 0.1f), 1, new Color(0.2f, 0.08f, 0.01f));
        A("BoneYellow", "skeleton", new Color(1f, 0.92f, 0.18f), 1, new Color(0.18f, 0.16f, 0.02f));
        A("BoneGreen", "skeleton", new Color(0.25f, 1.1f, 0.3f), 1, new Color(0.02f, 0.16f, 0.03f));
        A("BoneBlue", "skeleton", new Color(0.25f, 0.5f, 1.1f), 1, new Color(0.02f, 0.06f, 0.2f));
        A("BoneIndigo", "skeleton", new Color(0.4f, 0.3f, 1f), 1, new Color(0.06f, 0.03f, 0.18f));
        A("BoneViolet", "skeleton", new Color(0.8f, 0.35f, 1.1f), 1, new Color(0.14f, 0.04f, 0.2f));
        // the second line-up
        A("BonePink", "skeleton", new Color(1f, 0.45f, 0.75f), 1, new Color(0.18f, 0.05f, 0.1f));
        A("BoneTeal", "skeleton", new Color(0.15f, 0.9f, 0.9f), 1, new Color(0.02f, 0.14f, 0.14f));
        A("BoneGold", "skeleton", new Color(1f, 0.78f, 0.25f), 1, new Color(0.3f, 0.2f, 0.02f));
        A("BoneSilver", "skeleton", new Color(0.82f, 0.85f, 0.92f), 1, new Color(0.12f, 0.12f, 0.14f));
        A("BoneBlack", "skeleton", new Color(0.13f, 0.13f, 0.15f), 1, new Color(0.03f, 0f, 0f));
        A("BoneBrown", "skeleton", new Color(0.55f, 0.33f, 0.15f), 1, new Color(0.05f, 0.02f, 0f));
        A("BoneGlow", "skeleton", new Color(0.6f, 1f, 0.6f), 1, new Color(0.35f, 0.85f, 0.35f));
        // the original tree materials
        A("Needles", "needles", white, 1, Color.black);
        A("Pot", null, new Color(0.55f, 0.12f, 0.1f), 1, Color.black);
        A("Stand", null, new Color(0.25f, 0.2f, 0.15f), 1, Color.black);
        A("Trunk", null, new Color(0.3f, 0.2f, 0.12f), 1, Color.black);
        A("Eye", null, new Color(0.9f, 0.9f, 0.85f), 1, new Color(0.22f, 0.22f, 0.2f));
        A("Pupil", null, new Color(0.01f, 0.01f, 0.01f), 1, Color.black);
        A("Mouth", null, new Color(0.7f, 0.05f, 0.08f), 1, new Color(0.08f, 0f, 0f));
        A("Teeth", null, new Color(0.85f, 0.82f, 0.7f), 1, new Color(0.08f, 0.07f, 0.05f));
        A("Black", null, new Color(0.03f, 0.03f, 0.03f), 1, Color.black);
        A("HatRed", null, new Color(0.75f, 0.05f, 0.08f), 1, Color.black);
        A("HatWhite", null, new Color(0.95f, 0.95f, 0.95f), 1, Color.black);
    }

    static Texture2D Tex(string name)
    {
        if (baked.TryGetValue(name, out var t) && t != null) return t;
        t = AssetDatabase.LoadAssetAtPath<Texture2D>(Gen + "/Tex_" + name + ".asset");   // baked earlier
        if (t == null) { Debug.LogWarning("Fir Warning: missing texture " + name); report.Add("problem: texture " + name + " is missing"); }
        return t;
    }

    public static int BakeTextures()
    {
        baked.Clear();
        Directory.CreateDirectory(Gen);
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/textures_baked.txt");
        if (ta == null) { report.Add("problem: Data/textures_baked.txt is missing"); return 0; }
        bool android = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
        var fmt = android ? TextureFormat.ASTC_6x6 : TextureFormat.DXT1;
        int n = 0;
        foreach (var raw in ta.text.Split('\n'))
        {
            var line = raw.Trim();
            if (line == "" || line.StartsWith("#")) continue;
            var f = line.Split('|');
            if (f.Length < 5) continue;
            string name = f[0];
            byte[] png;
            try { png = Convert.FromBase64String(f[4]); }
            catch (FormatException) { report.Add("problem: texture " + name + " has bad data"); continue; }
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!decoded.LoadImage(png)) { report.Add("problem: texture " + name + " couldn't be decoded"); Object.DestroyImmediate(decoded); continue; }
            var tex = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, true, false) { name = name };
            tex.SetPixels32(decoded.GetPixels32());
            tex.Apply(true, false);
            Object.DestroyImmediate(decoded);
            tex.wrapMode = f[1] == "clamp" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 2;
            EditorUtility.CompressTexture(tex, fmt, TextureCompressionQuality.Normal);

            string path = Gen + "/Tex_" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(tex, existing);   // keep the asset (and every reference to it)
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(tex);
                tex = existing;
            }
            else AssetDatabase.CreateAsset(tex, path);
            baked[name] = tex;
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Fir Warning: baked " + n + " textures (" + (android ? "ASTC 6x6, Android" : "DXT1, PC") + ")");
        return n;
    }

    static Material FontMaterial()
    {
        string path = Gen + "/FontMobile.mat";
        var font = TMP_Settings.defaultFontAsset;
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(font.material); AssetDatabase.CreateAsset(m, path); }
        m.CopyPropertiesFromMaterial(font.material);
        var mobile = Shader.Find("TextMeshPro/Mobile/Distance Field");
        if (mobile != null) m.shader = mobile;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetupDescriptor(Transform spawn)
    {
        var desc = Object.FindObjectOfType<VRCSceneDescriptor>();
        if (desc == null) desc = new GameObject("VRCWorld").AddComponent<VRCSceneDescriptor>();
        desc.spawns = new[] { spawn };
        desc.RespawnHeightY = -20f;
        EditorUtility.SetDirty(desc);
    }

    // =================================================================== UdonSharp glue (reflection keeps this working across U# versions)

    static readonly string[] Scripts = { "FirCheckHub", "FirCheckButton", "FirCheckTyped", "FirCheckSkeleton", "FirCheckTree", "FirRainbowSkeleton", "FirSkeletonCombo", "FirGag", "FirSkeletonGag", "FirCloset", "FirThrowable", "FirShare" };

    static bool EnsureProgramAssets()
    {
        bool created = false;
        foreach (var s in Scripts)
        {
            string asset = Root + "/Scripts/" + s + ".asset";
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(asset) != null) continue;
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(Root + "/Scripts/" + s + ".cs");
            if (script == null) { Debug.LogError("Fir Warning: missing script " + s); return false; }
            var pa = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            pa.sourceCsScript = script;
            AssetDatabase.CreateAsset(pa, asset);
            created = true;
        }
        if (created)
        {
            AssetDatabase.SaveAssets();
            var m = typeof(UdonSharpProgramAsset).GetMethod("CompileAllCsPrograms", BindingFlags.Public | BindingFlags.Static);
            if (m != null) m.Invoke(null, m.GetParameters().Select(p => p.ParameterType == typeof(bool) ? (object)true : p.DefaultValue).ToArray());
        }
        return true;
    }

    /// <summary>
    /// Adds a U# behaviour the way UdonSharp wants: UdonSharpUndo.AddComponent (public; it creates the backing
    /// UdonBehaviour and links the program). If that isn't there, AddComponent plus UdonSharp's own setup.
    /// </summary>
    static T Add<T>(GameObject go) where T : UdonSharpBehaviour
    {
        var undo = FindAnyType("UdonSharpEditor.UdonSharpUndo");
        var add = undo?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                       .FirstOrDefault(m => m.Name == "AddComponent" && !m.IsGenericMethod && m.GetParameters().Length == 2
                                            && m.GetParameters()[0].ParameterType == typeof(GameObject) && m.GetParameters()[1].ParameterType == typeof(Type));
        if (add != null)
        {
            try
            {
                var made = add.Invoke(null, new object[] { go, typeof(T) }) as T;
                if (made != null) return made;
            }
            catch (Exception e) { report.Add("problem: UdonSharpUndo.AddComponent failed: " + (e.InnerException ?? e).Message); }
        }
        var b = go.AddComponent<T>();
        Setup(b);
        return b;
    }

    static Type EditorUtil => FindAnyType("UdonSharpEditor.UdonSharpEditorUtility");

    static Type FindAnyType(string full) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(full)).FirstOrDefault(t => t != null);

    /// <summary>Whether UdonSharp currently reports compile errors (then no U# script works and serialising fails).</summary>
    static bool UdonSharpHasErrors()
    {
        try
        {
            var m = typeof(UdonSharpProgramAsset).GetMethod("AnyUdonSharpScriptHasError", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return m != null && m.GetParameters().Length == 0 && (bool)m.Invoke(null, null);
        }
        catch (Exception) { return false; }
    }

    static object[] Args(MethodInfo m, object first)
    {
        var ps = m.GetParameters();
        var args = new object[ps.Length];
        args[0] = first;
        for (int i = 1; i < ps.Length; i++)
        {
            if (ps[i].HasDefaultValue) { args[i] = ps[i].DefaultValue; continue; }
            var t = ps[i].ParameterType;
            var def = t.GetField("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                   ?? t.GetProperty("Default", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            args[i] = def ?? (t.IsValueType ? Activator.CreateInstance(t) : null);
        }
        return args;
    }

    static void Setup(UdonSharpBehaviour b)
    {
        // RunBehaviourSetup is internal in UdonSharp 1.x, so look at non-public methods too
        var ms = EditorUtil?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Where(x => x.Name == "RunBehaviourSetup").ToArray() ?? new MethodInfo[0];
        var m = ms.FirstOrDefault(x => x.GetParameters().Length == 1) ?? ms.FirstOrDefault();
        if (m == null) { report.Add("problem: UdonSharp's RunBehaviourSetup wasn't found"); return; }
        m.Invoke(null, Args(m, b));
    }

    static VRC.Udon.UdonBehaviour Backing(UdonSharpBehaviour b)
    {
        var m = EditorUtil?.GetMethod("GetBackingUdonBehaviour", BindingFlags.Public | BindingFlags.Static);
        var u = m?.Invoke(null, new object[] { b }) as VRC.Udon.UdonBehaviour;
        if (u == null)
        {
            var all = b.GetComponents<VRC.Udon.UdonBehaviour>();
            if (all.Length == 1) u = all[0];
        }
        return u;
    }

    // ---- gags for everyone: one synced FirShare that tells the other players what someone pressed
    static void BuildShare(Transform root)
    {
        var go = new GameObject("Share");
        go.transform.SetParent(root, false);
        var sh = Add<FirShare>(go);
        var gags = root.GetComponentsInChildren<FirGag>(true);
        var sgs = root.GetComponentsInChildren<FirSkeletonGag>(true);
        var cls = root.GetComponentsInChildren<FirCloset>(true);
        var rbs = root.GetComponentsInChildren<FirRainbowSkeleton>(true);
        var cbs = root.GetComponentsInChildren<FirSkeletonCombo>(true);
        var me = UB(sh);
        for (int i = 0; i < gags.Length; i++) { gags[i].share = me; gags[i].shareIndex = i; }
        for (int i = 0; i < sgs.Length; i++) { sgs[i].share = me; sgs[i].shareIndex = i; }
        for (int i = 0; i < cls.Length; i++) { cls[i].share = me; cls[i].shareIndex = i; }
        for (int i = 0; i < rbs.Length; i++) { rbs[i].share = me; rbs[i].shareIndex = i; }
        for (int i = 0; i < cbs.Length; i++) { cbs[i].share = me; cbs[i].shareIndex = i; }
        sh.gags = gags.Select(x => UB(x)).ToArray();
        sh.skeletonGags = sgs.Select(x => UB(x)).ToArray();
        sh.closets = cls.Select(x => UB(x)).ToArray();
        sh.gagSkeletons = rbs.Select(x => UB(x)).ToArray();
        sh.combos = cbs.Select(x => UB(x)).ToArray();
        report.Add("shared with everyone: " + gags.Count(g => !g.walkIn) + " pressed gags, " + sgs.Length + " routines, " + cls.Length +
                   " closet, " + rbs.Length + " gag skeletons, " + cbs.Length + " combo acts");
    }

    static VRC.Udon.UdonBehaviour UB(UdonSharpBehaviour b)
    {
        var u = Backing(b);
        if (u == null) report.Add("problem: no UdonBehaviour behind " + b.name);
        return u;
    }

    static void SetInteract(UdonSharpBehaviour b, string text, float proximity)
    {
        var u = Backing(b);
        if (u == null) return;
        u.interactText = text;
        u.proximity = proximity;
    }

    static void Sync(UdonSharpBehaviour b)
    {
        var ms = EditorUtil?.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name == "CopyProxyToUdon").ToArray() ?? new MethodInfo[0];
        var m = ms.FirstOrDefault(x => x.GetParameters().Length == 1) ?? ms.FirstOrDefault(x => x.GetParameters().Length >= 1);
        if (m != null)
        {
            try { m.Invoke(null, Args(m, b)); }
            catch (Exception e) { report.Add("problem: UdonSharp couldn't copy " + b.name + ": " + (e.InnerException ?? e).Message); }
        }
        EditorUtility.SetDirty(b);
        var u = Backing(b);
        if (u != null) EditorUtility.SetDirty(u);
    }
}
#endif
