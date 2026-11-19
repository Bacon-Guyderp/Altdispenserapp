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

    [MenuItem("Fir Warning/Build World", priority = 1)]
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
        PendantLamp(lobby, new Vector3(-3.5f, 3.6f, -0.5f), 0.45f);
        PendantLamp(lobby, new Vector3(3.5f, 3.6f, -0.5f), 0.45f);
        // the west wall has a doorway (z 2.8 to 5.2) through to Bonetown, the expansion
        Box(lobby, "WallW S", new Vector3(-7.1f, 1.8f, -1.7f), new Vector3(0.2f, 3.6f, 9.0f), M("Wall"));
        Box(lobby, "WallW N", new Vector3(-7.1f, 1.8f, 5.7f), new Vector3(0.2f, 3.6f, 1.0f), M("Wall"));
        Box(lobby, "WallW Top", new Vector3(-7.1f, 3.2f, 4.0f), new Vector3(0.2f, 0.8f, 2.4f), M("Wall"));
        DoorFrameX(lobby, -7.0f, 4.0f);
        Text("TownDoorSign", lobby, new Vector3(-6.98f, 3.25f, 4.0f), Face(Vector3.right), new Vector2(2.6f, 0.5f), 0.2f,
             "<b>BONETOWN</b>\n<size=70%>the expansion: skeletons live here</size>").color = new Color(1f, 0.85f, 0.4f);
        Box(lobby, "WallE", new Vector3(7.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 12.4f), M("Wall"));
        var spawn = Marker(lobby, "Spawn", new Vector3(0, 0.05f, -2.6f), 0);
        // ---- the test room (teleport target), far from the lobby
        var room = Group(root, "TestRoom", new Vector3(40, 0, 0));
        Box(room, "Floor", new Vector3(0, -0.1f, 0), new Vector3(14, 0.2f, 12), M("Floor2"));
        Box(room, "Ceiling", new Vector3(0, 3.7f, 0), new Vector3(14, 0.2f, 12), M("Ceiling"));
        Box(room, "WallN", new Vector3(0, 1.8f, 6.1f), new Vector3(14.4f, 3.6f, 0.2f), M("Wall2"));
        Box(room, "WallS", new Vector3(0, 1.8f, -6.1f), new Vector3(14.4f, 3.6f, 0.2f), M("Wall2"));
        Box(room, "WallW", new Vector3(-7.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 12.4f), M("Wall2"));
        Box(room, "WallE", new Vector3(7.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 12.4f), M("Wall2"));
        var roomArrive = Marker(room, "Arrive", new Vector3(0, 0.05f, -2.6f), 0);
        Trim(room, "TestRoom", 6.0f, -6.0f, 14f, 3.6f, float.NaN, float.NaN, true);
        StringLights(room, "TestRoom", -6f, 6f, 14f, 3.6f);
        Text("RoomTitle", room, new Vector3(0, 2.3f, -5.98f), Face(Vector3.forward), new Vector2(9, 0.5f), 0.3f,
             "<b>SYSTEMS CHECK ROOM</b>: the teleport worked if you're reading this").color = Color.white;

        // dark notice boards behind the text, so it reads clearly against the wallpaper
        Box(room, "TestBoard", new Vector3(0, 1.85f, 5.995f), new Vector3(13.2f, 3.3f, 0.01f), M("Board"));
        Box(room, "InfoBoard", new Vector3(-6.995f, 1.65f, 0.45f), new Vector3(0.01f, 3.0f, 9.4f), M("Board"));
        Box(room, "PanelBoard", new Vector3(6.995f, 1.75f, 3.5f), new Vector3(0.01f, 1.8f, 2.2f), M("Board"));

        var n = Vector3.back;   // the north wall faces the players
        Text("Title", room, new Vector3(0, 3.15f, 5.98f), Face(n), new Vector2(10, 0.6f), 0.5f, "<b>FIR WARNING</b>  systems check").color = new Color(0.9f, 0.15f, 0.15f);
        Text("Help", room, new Vector3(0, 2.72f, 5.98f), Face(n), new Vector2(12, 0.35f), 0.17f,
             "Press every button. Each one says <color=#70ff90>PASS</color> or <color=#ff6060>FAIL</color> on itself. Stuck on <color=#ffd040>testing...</color> = that test stopped. Then send a photo of the walls.").color = Color.white;

        // ---- the hub (everything that talks to another script talks to this)
        var hubGo = new GameObject("Hub");
        hubGo.transform.SetParent(root, false);
        var hub = Add<FirCheckHub>(hubGo);
        var w = Vector3.right;   // west wall faces east
        hub.board = Text("HubBoard", room, new Vector3(-6.98f, 2.2f, 2.5f), Face(w), new Vector2(5f, 1.6f), 0.2f,
                         "<color=#ff6060><b>HUB: not running</b></color>\nIf this never changes, UdonSharp didn't run in this world.");
        hub.board.color = Color.white;

        // ---- the test buttons: 1 in the lobby (teleport here), 2-10 on the test room's north wall, 11 on its south wall
        var testMat = M("TestPanel");
        var sound = Src(room, new Vector3(0, 1.5f, 5), Clip("tree_laugh"), false, 0f, 1f, 20f, false);
        Func<int, Vector3> Spot = i => new Vector3(-4.8f + (i % 4) * 3.2f, 2.05f - (i / 4) * 0.78f, 5.85f);
        Func<int, int, string, FirCheckButton> Test = (slot, test, title) =>
        {
            TextMeshPro label;
            var b = MakeButton<FirCheckButton>(slot < 0 ? lobby : room, "Test " + test, slot < 0 ? new Vector3(-6.85f, 1.3f, -3.6f) : Spot(slot), slot < 0 ? Vector3.right : n, out label);
            b.test = test; b.title = title; b.label = label; b.hub = UB(hub);
            return b;
        };
        var t1 = Test(-1, 1, "Systems check room"); t1.destination = roomArrive;
        Test(0, 2, "2. Button's own script");
        Test(1, 3, "3. Event to another script");
        Test(2, 4, "4. Values between scripts");
        var typed = MakeButton<FirCheckTyped>(room, "Test 5", Spot(3), n, out var typedLabel);
        typed.hub = hub; typed.label = typedLabel;
        Test(4, 6, "6. Network sync");
        var t7 = Test(5, 7, "7. Skeleton (by event)");
        var t8 = Test(6, 8, "8. Tree (by event)");
        var t9 = Test(7, 9, "9. Lighting (material)"); t9.testMaterial = testMat;
        var t10 = Test(8, 10, "10. Sound"); t10.sound = sound;
        // test 11 is in the test room: teleport back
        var t11 = MakeButton<FirCheckButton>(room, "Test 11", new Vector3(0, 1.3f, -5.85f), Vector3.forward, out var backLabel);
        t11.test = 11; t11.title = "11. Teleport back"; t11.label = backLabel; t11.destination = spawn; t11.hub = UB(hub);

        // the lighting test panel (lantern-lit, so _Dark shows) on the east wall
        var e = Vector3.left;
        var panel = Box(room, "TestPanel", new Vector3(6.95f, 1.6f, 3.5f), new Vector3(0.05f, 1.2f, 1.6f), testMat);
        Text("PanelLabel", room, new Vector3(6.9f, 2.45f, 3.5f), Face(e), new Vector2(2f, 0.3f), 0.15f, "test 9 panel").color = Color.white;

        // ---- your skeleton
        var skelGo = new GameObject("Skeleton");
        skelGo.transform.SetParent(room, false);
        skelGo.transform.localPosition = new Vector3(-2.5f, 0, 1.5f);
        skelGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var pivots = SkeletonRig(skelGo.transform, "Rig", M("BoneWhite"));
        var sc = skelGo.AddComponent<BoxCollider>(); sc.center = new Vector3(0, 0.95f, 0); sc.size = new Vector3(0.7f, 1.9f, 0.5f);
        var skel = Add<FirCheckSkeleton>(skelGo);
        skel.pivots = pivots;
        skel.voice = Src(pivots[2], Vector3.zero, Clip("skeleton_white"), false, 1f, 1f, 15f, false);
        skel.label = Text("SkeletonLabel", room, new Vector3(-2.5f, 2.35f, 1.5f), Face(Vector3.back), new Vector2(2.2f, 0.45f), 0.14f,
                          "<b>White skeleton</b>\n<color=#ff6060>its script isn't running</color>");
        skel.label.color = Color.white;
        SetInteract(skel, "Press the skeleton", 3f);
        rigs["White"] = pivots;
        t7.other = UB(skel);

        // ---- a talking tree: the original Fir Warning model
        var treeGo = new GameObject("Tree");
        treeGo.transform.SetParent(room, false);
        treeGo.transform.localPosition = new Vector3(2.5f, 0, 1.5f);
        treeGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
        Transform mouth;
        var head = TreeModel(treeGo.transform, 1.9f, true, out mouth);
        var tc = treeGo.AddComponent<BoxCollider>(); tc.center = new Vector3(0, 0.95f, 0); tc.size = new Vector3(1.3f, 1.9f, 1.3f);
        var tree = Add<FirCheckTree>(treeGo);
        tree.head = head; tree.mouth = mouth;
        tree.voice = Src(head, new Vector3(0, 0.75f, 0.35f), Clip("tree_talk"), false, 1f, 1f, 15f, false);
        tree.label = Text("TreeLabel", room, new Vector3(2.5f, 2.35f, 1.5f), Face(Vector3.back), new Vector2(2.2f, 0.45f), 0.14f,
                          "<b>Tree</b>\n<color=#ff6060>its script isn't running</color>");
        tree.label.color = Color.white;
        SetInteract(tree, "Press the tree", 3f);
        t8.other = UB(tree);

        // ---- the map: new rooms to the south, full of gags (through the doorway in the lobby's south wall)
        BuildMap(root, spawn);
        BuildHunt(root, lobby, spawn);
        BuildTown(root);
        BuildShare(root);

        // ---- copy everything into Udon, then check what UdonSharp stored
        foreach (var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true)) Sync(b);
        var info = Text("BuildInfo", room, new Vector3(-6.98f, 1.0f, -1.6f), Face(w), new Vector2(5f, 1.5f), 0.15f, "");
        info.color = Color.white;
        info.text = BuildReport(root);

        FinishHunt(root);
        SetupDescriptor(spawn);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.3f);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root.gameObject;
        foreach (var r in report) Debug.Log("Fir Warning systems check: " + r);
        if (!quiet) EditorUtility.DisplayDialog("Fir Warning", "Built the world.\n\n" + string.Join("\n", report.Take(14)) +
                                                "\n\nNow press Build & Test (or upload). START is on the lobby's north wall; the systems check room is through the button on its west wall.", "OK");
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

    /// <summary>A door frame for a doorway in a wall that runs along z (an east or west wall).</summary>
    static void DoorFrameX(Transform parent, float x, float z)
    {
        var g = Group(parent, "DoorFrame", new Vector3(x, 0, z));
        g.localRotation = Quaternion.Euler(0, 90, 0);
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
             "<b>THE END</b> of the map. In the game, the furnace is your way out").color = Color.white;
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
        // the horror game
        A("DoorWood", "wood_floor", new Color(0.5f, 0.32f, 0.2f), 1, Color.black);
        A("ScareGreen", "needles", new Color(0.35f, 0.75f, 0.35f), 1, Color.black);
        A("GlowRedEye", null, new Color(1f, 0.1f, 0.05f), 1, new Color(1f, 0.05f, 0.02f));
        A("GlowYellow", null, new Color(1f, 0.85f, 0.2f), 1, new Color(0.7f, 0.55f, 0.05f));
        A("GlowRed", null, new Color(0.9f, 0.1f, 0.1f), 1, new Color(0.6f, 0.05f, 0.05f));
        A("GlowGold", null, new Color(1f, 0.75f, 0.2f), 1, new Color(0.6f, 0.4f, 0.05f));
        A("GlowGreen", null, new Color(0.2f, 1f, 0.3f), 1, new Color(0.1f, 0.6f, 0.15f));
        A("Grass", null, new Color(0.36f, 0.72f, 0.3f), 1, Color.black);
        A("Sky", null, new Color(0.62f, 0.8f, 1f), 1, new Color(0.12f, 0.16f, 0.2f));
        A("Hedge", "needles", new Color(0.45f, 0.8f, 0.4f), 1, Color.black);
        A("Water", null, new Color(0.35f, 0.65f, 1f), 1, new Color(0.1f, 0.22f, 0.35f));
        A("Porcelain", null, new Color(0.95f, 0.95f, 0.93f), 1, Color.black);
        A("ToiletWater", null, new Color(0.45f, 0.7f, 0.9f), 1, new Color(0.05f, 0.1f, 0.15f));
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

    static readonly string[] Scripts = { "FirCheckHub", "FirCheckButton", "FirCheckTyped", "FirCheckSkeleton", "FirCheckTree", "FirRainbowSkeleton", "FirSkeletonCombo", "FirGag", "FirSkeletonGag", "FirCloset", "FirThrowable", "FirShare", "FirHunt", "FirHuntButton", "FirStalker", "FirWanderer", "FirTicker" };

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

    // =================================================================== Bonetown (the expansion), west of the lobby

    static Color Hex(string h)
    {
        h = h.TrimStart('#');
        return new Color(Convert.ToInt32(h.Substring(0, 2), 16) / 255f, Convert.ToInt32(h.Substring(2, 2), 16) / 255f, Convert.ToInt32(h.Substring(4, 2), 16) / 255f);
    }

    static Material BoneMat(string name, string hex)
    {
        string n = "Bone" + name;
        if (mats.TryGetValue(n, out var m)) return m;
        var c = Hex(hex);
        AddMat(n, "skeleton", new Color(c.r * 1.05f + 0.05f, c.g * 1.05f + 0.05f, c.b * 1.05f + 0.05f), 1, c * (name == "Neon" ? 0.45f : 0.15f));
        return mats[n];
    }

    static void BuildTown(Transform root)
    {
        var g = Group(root, "Bonetown", Vector3.zero);
        float x0 = -12f, x1 = -48f, z0 = -14f, z1 = 22f, h = 6f;
        float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f;
        // ---- the corridor from the lobby's west doorway (z 2.8 to 5.2)
        Box(g, "CorridorFloor", new Vector3(-9.6f, -0.1f, 4f), new Vector3(5.0f, 0.2f, 2.4f), M("Floor"));
        Box(g, "CorridorCeiling", new Vector3(-9.6f, 3.1f, 4f), new Vector3(5.0f, 0.2f, 2.8f), M("Ceiling"));
        Box(g, "CorridorS", new Vector3(-9.6f, 1.5f, 2.7f), new Vector3(5.0f, 3f, 0.2f), M("Wall"));
        Box(g, "CorridorN", new Vector3(-9.6f, 1.5f, 5.3f), new Vector3(5.0f, 3f, 0.2f), M("Wall"));
        PendantLamp(g, new Vector3(-9.6f, 3.0f, 4f), 0.45f);
        Text("CorridorSign", g, new Vector3(-9.6f, 2.3f, 5.18f), Face(Vector3.back), new Vector2(4f, 0.4f), 0.2f, "<b>BONETOWN</b> this way  <").color = new Color(1f, 0.85f, 0.4f);
        // ---- the town: grass, sky, a hedge round the walls
        Box(g, "Grass", new Vector3(cx, -0.1f, cz), new Vector3(36f, 0.2f, 36f), M("Grass"));
        Box(g, "Sky", new Vector3(cx, h + 0.1f, cz), new Vector3(36f, 0.2f, 36f), M("Sky"));
        Box(g, "WallW", new Vector3(x1 - 0.1f, h / 2, cz), new Vector3(0.2f, h, 36.4f), M("Sky"));
        Box(g, "WallN", new Vector3(cx, h / 2, z1 + 0.1f), new Vector3(36.4f, h, 0.2f), M("Sky"));
        Box(g, "WallS", new Vector3(cx, h / 2, z0 - 0.1f), new Vector3(36.4f, h, 0.2f), M("Sky"));
        // the east wall, with the doorway in from the corridor
        Box(g, "WallE S", new Vector3(x0 + 0.1f, h / 2, (z0 + 2.8f) / 2f), new Vector3(0.2f, h, 2.8f - z0), M("Sky"));
        Box(g, "WallE N", new Vector3(x0 + 0.1f, h / 2, (5.2f + z1) / 2f), new Vector3(0.2f, h, z1 - 5.2f), M("Sky"));
        Box(g, "WallE Top", new Vector3(x0 + 0.1f, (3f + h) / 2f, 4f), new Vector3(0.2f, h - 3f, 2.4f), M("Sky"));
        foreach (var hw in new[] {
            new { p = new Vector3(x1 + 0.25f, 0.6f, cz), s = new Vector3(0.3f, 1.2f, 36f) },
            new { p = new Vector3(cx, 0.6f, z1 - 0.25f), s = new Vector3(36f, 1.2f, 0.3f) },
            new { p = new Vector3(cx, 0.6f, z0 + 0.25f), s = new Vector3(36f, 1.2f, 0.3f) },
            new { p = new Vector3(x0 - 0.25f, 0.6f, (z0 + 2.6f) / 2f), s = new Vector3(0.3f, 1.2f, 2.6f - z0) },
            new { p = new Vector3(x0 - 0.25f, 0.6f, (5.4f + z1) / 2f), s = new Vector3(0.3f, 1.2f, z1 - 5.4f) } })
            Box(g, "Hedge", hw.p, hw.s, M("Hedge"), false);
        Text("TownTitle", g, new Vector3(x0 - 0.02f, 4.2f, 4f), Face(Vector3.left), new Vector2(6f, 0.8f), 0.6f, "<b>BONETOWN</b>").color = new Color(1f, 0.95f, 0.85f);
        Text("TownWelcome", g, new Vector3(x0 - 0.02f, 3.45f, 4f), Face(Vector3.left), new Vector2(7f, 0.4f), 0.17f,
             "where the skeletons live.  Press anyone to say hi. Everyone here is harmless.").color = new Color(0.2f, 0.25f, 0.4f);
        // paths: from the doorway to the fountain, and a ring round it
        Box(g, "Path", new Vector3(-18.5f, 0.005f, 4f), new Vector3(13f, 0.01f, 2.0f), M("Concrete"), false);
        for (int k = 0; k < 24; k++)
        {
            float a = k * Mathf.PI * 2f / 24f;
            DPrim(PrimitiveType.Cube, "PathRing", g, new Vector3(-30f + Mathf.Cos(a) * 3.3f, 0.006f, 4f + Mathf.Sin(a) * 3.3f), new Vector3(1.0f, 0.01f, 1.4f), M("Concrete"), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
        }
        // the fountain
        var f = Group(g, "Fountain", new Vector3(-30f, 0, 4f));
        Box(f, "Basin", new Vector3(0, 0.25f, 0), new Vector3(3.6f, 0.5f, 3.6f), M("Concrete"), true);
        Prim(PrimitiveType.Cylinder, "Rim", f, new Vector3(0, 0.27f, 0), new Vector3(4.0f, 0.27f, 4.0f), M("Concrete"));
        Prim(PrimitiveType.Cylinder, "Water", f, new Vector3(0, 0.55f, 0), new Vector3(3.6f, 0.01f, 3.6f), M("Water"));
        Prim(PrimitiveType.Cylinder, "Column", f, new Vector3(0, 1.1f, 0), new Vector3(0.4f, 0.6f, 0.4f), M("Concrete"));
        Prim(PrimitiveType.Sphere, "Skull", f, new Vector3(0, 1.9f, 0), new Vector3(0.5f, 0.48f, 0.55f), M("BoneWhite"));
        foreach (float sx in new[] { -0.11f, 0.11f }) Prim(PrimitiveType.Sphere, "Socket", f, new Vector3(sx, 1.95f, 0.22f), Vector3.one * 0.13f, M("Black"));
        Prim(PrimitiveType.Cylinder, "Spout", f, new Vector3(0, 1.0f, 0), new Vector3(1.6f, 0.02f, 1.6f), M("Water"));
        Text("FountainSign", f, new Vector3(0, 0.3f, 2.02f), Face(Vector3.forward), new Vector2(3f, 0.25f), 0.12f, "<b>THE FOUNTAIN OF YOUTH</b>  <size=70%>(out of order)</size>").color = new Color(0.15f, 0.15f, 0.2f);
        // lamp posts, benches and a few trees round the plaza
        foreach (var lp in new[] { new Vector3(-22.5f, 0, -4f), new Vector3(-35f, 0, -4f), new Vector3(-22.5f, 0, 12f), new Vector3(-35f, 0, 12f) })
        {
            DPrim(PrimitiveType.Cylinder, "LampPost", g, lp + new Vector3(0, 1.6f, 0), new Vector3(0.12f, 1.6f, 0.12f), M("Black"));
            DPrim(PrimitiveType.Sphere, "LampGlow", g, lp + new Vector3(0, 3.3f, 0), Vector3.one * 0.45f, M("GlowWarm"));
        }
        foreach (var bp in new[] { new { p = new Vector3(-21f, 0, 1.2f), y = 90f }, new { p = new Vector3(-21f, 0, 7.2f), y = 90f }, new { p = new Vector3(-39f, 0, 1f), y = -90f }, new { p = new Vector3(-39f, 0, 7f), y = -90f } })
        {
            var b = Group(g, "Bench", bp.p);
            b.localRotation = Quaternion.Euler(0, bp.y, 0);
            Box(b, "Seat", new Vector3(0, 0.42f, 0), new Vector3(1.8f, 0.08f, 0.5f), M("DeskWood"));
            Box(b, "Back", new Vector3(0, 0.75f, -0.22f), new Vector3(1.8f, 0.5f, 0.06f), M("DeskWood"), false);
            foreach (float lx in new[] { -0.75f, 0.75f }) Box(b, "Leg", new Vector3(lx, 0.2f, 0), new Vector3(0.08f, 0.4f, 0.45f), M("Black"), false);
        }
        TreeAt(g, "TownTree", new Vector3(-14.0f, 0, -1.3f), -90f, 2.6f, false);
        TreeAt(g, "TownTree", new Vector3(-13.8f, 0, 16.6f), -90f, 2.6f, true);
        TreeAt(g, "TownTree", new Vector3(-46f, 0, 18.5f), 135f, 2.2f, false);

        // ---- the residents: six homes along the north side, and four along the south (the snack shack, the news, two homes)
        var homes = new[] {
            new { who = "Mint", hex = "#7dffc0", act = 16, side = 1, x = -16.5f, wall = "Wall2", prop = "boxes" },
            new { who = "Peach", hex = "#ffb48a", act = 4, side = 1, x = -21.5f, wall = "Wall", prop = "broom" },
            new { who = "Lavender", hex = "#c8a8ff", act = 3, side = 1, x = -26.5f, wall = "WallDark", prop = "bed" },
            new { who = "Coral", hex = "#ff7f6a", act = 10, side = 1, x = -31.5f, wall = "BrickWall", prop = "gym" },
            new { who = "Sky", hex = "#7ec8ff", act = 15, side = 1, x = -36.5f, wall = "WallGreen", prop = "xylophone" },
            new { who = "Navy", hex = "#2c3e8c", act = 12, side = 1, x = -41.5f, wall = "Wall", prop = "wardrobe" },
            new { who = "Rust", hex = "#b5502a", act = 1, side = -1, x = -17.5f, wall = "BrickWall", prop = "shack" },
            new { who = "Olive", hex = "#8a9a3a", act = 11, side = -1, x = -24.5f, wall = "WallDark", prop = "news" },
            new { who = "Cream", hex = "#fff2c8", act = 6, side = -1, x = -31.0f, wall = "Wall2", prop = "rug" },
            new { who = "Plum", hex = "#8e3b7a", act = 13, side = -1, x = -37.0f, wall = "WallGreen", prop = "kitchen" } };
        float[] roomW = { 5f, 5f, 5f, 5f, 5f, 5f, 6f, 6f, 5.5f, 5.5f };
        for (int i = 0; i < homes.Length; i++)
        {
            var hm = homes[i];
            float w = roomW[i], s = hm.side;
            float back = s > 0 ? z1 : z0, front = back - s * 4.5f, mid = (back + front) / 2f;
            var u = Group(g, "Home " + hm.who, Vector3.zero);
            Box(u, "BackWall", new Vector3(hm.x, 1.75f, back - s * 0.12f), new Vector3(w, 3.5f, 0.04f), M(hm.wall), false);
            Box(u, "SideA", new Vector3(hm.x - w / 2f, 1.75f, mid), new Vector3(0.2f, 3.5f, 4.5f), M("Trim"));
            Box(u, "SideB", new Vector3(hm.x + w / 2f, 1.75f, mid), new Vector3(0.2f, 3.5f, 4.5f), M("Trim"));
            Box(u, "Roof", new Vector3(hm.x, 3.6f, mid), new Vector3(w + 0.4f, 0.2f, 4.9f), M(s > 0 ? "CaneRed" : "GiftBlue"));
            Box(u, "Header", new Vector3(hm.x, 3.15f, front), new Vector3(w, 0.7f, 0.2f), M("Trim"), false);
            Rug(u, new Vector3(hm.x, 0, mid), new Vector2(w - 0.8f, 3.6f), "Rug", "TrimDark");
            string title = hm.prop == "shack" ? "SNACK SHACK" : hm.prop == "news" ? "BONETOWN NEWS" : hm.who.ToUpper() + "'S PLACE";
            Text("Nameplate", u, new Vector3(hm.x, 3.2f, front - s * 0.12f), Face(new Vector3(0, 0, -s)), new Vector2(w - 0.4f, 0.5f), 0.3f, "<b>" + title + "</b>").color = new Color(0.25f, 0.15f, 0.1f);
            var skPos = new Vector3(hm.x + 0.5f, 0, mid + s * 0.2f);
            HomeProps(u, hm.prop, hm.x, mid, s, w);
            GagSkeleton(u, hm.who, skPos, s > 0 ? 180f : 0f, 1f, hm.act, hm.hex, "Town/res_" + hm.who.ToLower(),
                        new[] { BoneMat(hm.who, hm.hex) }, new Vector3(hm.x, 2.55f, front - s * 0.12f), new Vector3(0, 0, -s));
        }

        // ---- the bandstand (west side): four of them, and a console of combo acts
        Box(g, "Bandstand", new Vector3(-45.5f, 0.25f, 3f), new Vector3(4.6f, 0.5f, 9f), M("Stage"));
        for (int k = 0; k < 7; k++)
            DPrim(PrimitiveType.Cube, "Bunting", g, new Vector3(-43.3f, 3.6f - (k % 2) * 0.2f, -1f + k * 1.33f), new Vector3(0.05f, 0.35f, 0.5f), M(k % 2 == 0 ? "CaneRed" : "GiftGold"), Quaternion.Euler(0, 0, 0));
        Text("BandSign", g, new Vector3(x1 + 0.02f, 5.1f, 3f), Face(Vector3.right), new Vector2(8f, 0.7f), 0.5f, "<b>THE BONETOWN BAND</b>").color = new Color(0.9f, 0.2f, 0.3f);
        var band = new[] {
            new { who = "Scarlet", hex = "#ff2a2a", act = 0, z = 5.1f },
            new { who = "Cobalt", hex = "#2050e0", act = 9, z = 3.9f },
            new { who = "Emerald", hex = "#20c070", act = 14, z = 2.1f },
            new { who = "Mustard", hex = "#e0b030", act = 5, z = 0.9f } };
        foreach (var b in band)
            GagSkeleton(g, b.who, new Vector3(-45.6f, 0.5f, b.z), 90f, 1f, b.act, b.hex, "Town/band_" + b.who.ToLower(),
                        new[] { BoneMat(b.who, b.hex) }, new Vector3(x1 + 0.02f, 2.8f, b.z), Vector3.right);
        // instruments: a drum by Mustard, a mic stand by Cobalt
        DPrim(PrimitiveType.Cylinder, "Drum", g, new Vector3(-44.6f, 0.75f, 0.9f), new Vector3(0.6f, 0.25f, 0.6f), M("CaneRed"));
        DPrim(PrimitiveType.Cylinder, "MicStand", g, new Vector3(-44.7f, 1.2f, 3.9f), new Vector3(0.04f, 0.7f, 0.04f), M("Black"));
        DPrim(PrimitiveType.Sphere, "Mic", g, new Vector3(-44.7f, 1.95f, 3.9f), Vector3.one * 0.1f, M("Metal"));
        var console = Console(g, "BandConsole", new Vector3(-42.3f, 1.25f, -2.2f), Vector3.right, "COMBO ACTS  <size=70%>the band</size>");
        ComboAct(console, 0, 6, "The Squabble", new[] { "Scarlet", "Cobalt" },
                 new[] { "squabble_1", "squabble_2", "squabble_3", "squabble_4", "squabble_5" }, new[] { 0, 1, 0, 1, -1 }, 1.6f);
        ComboAct(console, 1, 7, "Town Band", new[] { "Scarlet", "Mustard", "Emerald", "Cobalt" },
                 new[] { "band_1", "band_2", "band_3", "band_4" }, new[] { -1, 0, 3, -1 }, 2.0f);
        ComboAct(console, 2, 8, "The Proposal", new[] { "Emerald", "Mustard" },
                 new[] { "propose_1", "propose_2", "propose_3", "propose_4" }, new[] { 0, 1, 0, 1 }, 1.8f);
        ComboAct(console, 5, 9, "BONETOWN FINALE", new[] { "Scarlet", "Cobalt", "Emerald", "Mustard" },
                 new[] { "town_finale_1", "town_finale_2" }, new[] { -1, -1 }, 3.6f);

        // ---- the wanderers: harmless, walking and running round the town on their own loops
        var sq = new[] { new Vector3(-24f, 0, -2f), new Vector3(-24f, 0, 10f), new Vector3(-36f, 0, 10f), new Vector3(-36f, 0, -2f) };
        var big = new[] { new Vector3(-16f, 0, -7f), new Vector3(-16f, 0, 14f), new Vector3(-40f, 0, 14f), new Vector3(-40f, 0, -7f) };
        var ring = Enumerable.Range(0, 16).Select(k => new Vector3(-30f + Mathf.Cos(k * Mathf.PI / 8f) * 3.3f, 0, 4f + Mathf.Sin(k * Mathf.PI / 8f) * 3.3f)).ToArray();
        var wan = new[] {
            new { who = "Lime", hex = "#a8ff30", route = sq, pace = new[] { 1f, 1f, 1f, 1f }, speed = 0.9f, head = 0 },
            new { who = "Amber", hex = "#ffb020", route = sq.Reverse().ToArray(), pace = new[] { 1f, 1f, 1f, 1f }, speed = 1.0f, head = 20000 },
            new { who = "Turquoise", hex = "#30e8d0", route = ring, pace = Enumerable.Repeat(2.6f, 16).ToArray(), speed = 1.1f, head = 0 },
            new { who = "Crimson", hex = "#c0103a", route = ring, pace = Enumerable.Repeat(2.6f, 16).ToArray(), speed = 1.1f, head = 1400 },
            new { who = "Cocoa", hex = "#6b4226", route = big, pace = new[] { 2.1f, 2.1f, 2.1f, 2.1f }, speed = 1.1f, head = 0 },
            new { who = "Neon", hex = "#d0ff00", route = big.Reverse().ToArray(), pace = new[] { 2.8f, 1f, 2.8f, 1f }, speed = 1.1f, head = 15000 },
            new { who = "Magenta", hex = "#ff30d0", route = new[] { new Vector3(-19f, 0, -6f), new Vector3(-19f, 0, 13f), new Vector3(-26.5f, 0, 13f), new Vector3(-26.5f, 0, -6f) }, pace = new[] { 2.7f, 1f, 2.7f, 1f }, speed = 1.1f, head = 5000 },
            new { who = "Ivory", hex = "#fffaf0", route = new[] { new Vector3(-14.5f, 0, 4f), new Vector3(-20f, 0, 7.5f), new Vector3(-20f, 0, 0.5f) }, pace = new[] { 1f, 1f, 1f }, speed = 0.7f, head = 0 },
            new { who = "Denim", hex = "#4a6fa5", route = new[] { new Vector3(-37.5f, 0, -7f), new Vector3(-37.5f, 0, 13f), new Vector3(-33f, 0, 13f), new Vector3(-33f, 0, -7f) }, pace = new[] { 1f, 1f, 1f, 1f }, speed = 1.0f, head = 9000 },
            new { who = "Bubblegum", hex = "#ff8ad8", route = new[] { new Vector3(-17f, 0, -6.5f), new Vector3(-30f, 0, -7.5f), new Vector3(-41f, 0, -6f), new Vector3(-41f, 0, 14.5f), new Vector3(-30f, 0, 15f), new Vector3(-17f, 0, 14.5f) }, pace = new[] { 1f, 1f, 1f, 1f, 1f, 1f }, speed = 1.0f, head = 3000 } };
        var wg = Group(g, "Wanderers", Vector3.zero);
        foreach (var wd in wan)
        {
            var go = new GameObject(wd.who + " Wanderer");
            go.transform.SetParent(wg, false);
            go.transform.localPosition = wd.route[0];
            var ps = SkeletonRig(go.transform, "Rig", BoneMat(wd.who, wd.hex));
            var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.95f, 0); col.size = new Vector3(0.6f, 1.9f, 0.45f);
            go.layer = 17;   // Walkthrough: you can press them, but they walk through you instead of shoving you
            var wv = Add<FirWanderer>(go);
            wv.pivots = ps; wv.route = wd.route; wv.pace = wd.pace; wv.walkSpeed = wd.speed; wv.headStartMs = wd.head;
            var clip = Clip("Town/wan_" + wd.who.ToLower());
            wv.voice = Src(ps[2], Vector3.zero, clip, false, 1f, 1f, 15f, false);
            var line = TownLine("wan_" + wd.who.ToLower());
            wv.line = "<b><color=" + wd.hex + ">" + wd.who.ToUpper() + ":</color></b> " + line;
            wv.bubble = Bubble(go.transform, new Vector3(0, 2.3f, 0f));
            wv.bubbleObject = wv.bubble.gameObject; wv.bubbleTransform = wv.bubble.transform;
            SetInteract(wv, "Say hi to " + wd.who, 3f);
        }

        // ---- the news ticker over the news desk
        var tick = Text("Ticker", g, new Vector3(-24.5f, 2.2f, z0 + 0.17f), Face(Vector3.forward), new Vector2(5.4f, 0.4f), 0.17f, "");
        tick.color = new Color(1f, 0.95f, 0.5f);
        var tk = Add<FirTicker>(tick.gameObject);
        tk.text = tick;
        tk.headlines = new[] {
            "LOCAL SKELETON WINS 'MOST LIKELY TO RATTLE' FOR 300TH YEAR",
            "SNACK SHACK SOUP 'GOES RIGHT THROUGH YOU', SAY CUSTOMERS",
            "TWO WANDERERS STILL PLAYING TAG. NOBODY KNOWS WHO IS 'IT'",
            "FOUNTAIN OF YOUTH STILL OUT OF ORDER. NOBODY MINDS",
            "BAND ANNOUNCES NEW SONG: 'NO BODY KNOWS'",
            "WEATHER: SUNNY, WITH A CHANCE OF FEMURS",
            "CREAM TRIPS OVER RUG AGAIN. RUG 'NOT SORRY'",
            "NAVY SEEN HIDING IN WARDROBE. LANDLORD STILL WAITING" };
        Text("TickerLabel", g, new Vector3(-24.5f, 2.55f, z0 + 0.17f), Face(Vector3.forward), new Vector2(5.4f, 0.25f), 0.12f, "<b>LIVE</b>").color = new Color(1f, 0.3f, 0.3f);

        // ---- the town's gags (Data/town_gags.txt)
        int placed = 0;
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/town_gags.txt");
        if (ta == null) report.Add("problem: Data/town_gags.txt is missing");
        else
        {
            var gg = Group(g, "Gags", Vector3.zero);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Trim() == "" || line.StartsWith("#")) continue;
                var p = line.Split('|');
                if (p.Length < 10) continue;
                Gag(gg, p[0], p[1], new Vector3(float.Parse(p[2], inv), float.Parse(p[3], inv), float.Parse(p[4], inv)), float.Parse(p[5], inv),
                    p[7], p[8].Replace("\\n", "\n"), p[9].Replace("\\n", "\n"));
                placed++;
            }
        }
        report.Add("Bonetown: " + homes.Length + " residents, " + band.Length + " in the band, " + wan.Length + " wanderers, 4 combo acts, " + placed + " gags");
    }

    static string TownLine(string name)
    {
        return townLines.TryGetValue(name, out var s) ? s : "";
    }
    static readonly Dictionary<string, string> townLines = new Dictionary<string, string> {
        { "wan_lime", "Out for my evening stroll. Want to come? I'm very slow. And very dead." },
        { "wan_magenta", "Can't stop! I'm late for a very important... nothing! Bye!" },
        { "wan_turquoise", "Tag! You're it! ...Oh. You're not playing. That's fine." },
        { "wan_crimson", "You can't catch me! I'm not even running from anything!" },
        { "wan_amber", "Lovely day for a walk. I left my legs on, just in case." },
        { "wan_ivory", "Hello, neighbour! Have you met everyone? Everyone's bones." },
        { "wan_cocoa", "I jog every morning. Lose weight? I haven't got any to lose!" },
        { "wan_denim", "Ooh, a visitor! We don't get many living ones." },
        { "wan_neon", "Wheee! Running is the best! I never get out of breath. No lungs!" },
        { "wan_bubblegum", "Shopping day! I'm buying a new skull. This one's a bit hollow." } };

    static void HomeProps(Transform u, string prop, float x, float mid, float s, float w)
    {
        float backZ = mid + s * 1.6f;   // near the back wall
        switch (prop)
        {
            case "boxes":
                for (int k = 0; k < 5; k++)
                    DPrim(PrimitiveType.Cube, "MovingBox", u, new Vector3(x - 1.4f + (k % 3) * 0.6f, 0.25f + (k / 3) * 0.5f, backZ), Vector3.one * 0.5f, M("Cardboard"), Quaternion.Euler(0, k * 13f, 0));
                break;
            case "broom":
                DPrim(PrimitiveType.Cylinder, "Broom", u, new Vector3(x - 0.7f, 0.85f, mid), new Vector3(0.05f, 0.75f, 0.05f), M("DeskWood"), Quaternion.Euler(0, 0, 12));
                DPrim(PrimitiveType.Cube, "Bristles", u, new Vector3(x - 0.85f, 0.12f, mid), new Vector3(0.3f, 0.22f, 0.12f), M("GiftGold"));
                DPrim(PrimitiveType.Cube, "Radio", u, new Vector3(x - 1.6f, 0.2f, backZ), new Vector3(0.5f, 0.35f, 0.25f), M("CaneRed"));
                break;
            case "bed":
                Box(u, "Bed", new Vector3(x - 1.2f, 0.25f, mid), new Vector3(1.0f, 0.5f, 2.0f), M("DeskWood"));
                DPrim(PrimitiveType.Cube, "Blanket", u, new Vector3(x - 1.2f, 0.52f, mid - s * 0.2f), new Vector3(1.02f, 0.06f, 1.4f), M("GiftBlue"));
                DPrim(PrimitiveType.Cube, "Pillow", u, new Vector3(x - 1.2f, 0.56f, mid + s * 0.75f), new Vector3(0.7f, 0.12f, 0.35f), M("CaneWhite"));
                break;
            case "gym":
                foreach (float dx in new[] { -1.6f, -1.0f })
                {
                    DPrim(PrimitiveType.Cylinder, "Bar", u, new Vector3(x + dx, 0.12f, backZ), new Vector3(0.05f, 0.35f, 0.05f), M("Metal"), Quaternion.Euler(90, 0, 0));
                    foreach (float dz in new[] { -0.32f, 0.32f })
                        DPrim(PrimitiveType.Cylinder, "Weight", u, new Vector3(x + dx, 0.12f, backZ + dz), new Vector3(0.24f, 0.05f, 0.24f), M("Black"), Quaternion.Euler(90, 0, 0));
                }
                break;
            case "xylophone":
                string[] bars = { "BoneRed", "BoneOrange", "BoneYellow", "BoneGreen", "BoneBlue", "BoneViolet" };
                for (int k = 0; k < 6; k++)
                    DPrim(PrimitiveType.Cube, "Bar", u, new Vector3(x - 1.8f + k * 0.2f, 0.7f, mid), new Vector3(0.16f, 0.04f, 0.5f - k * 0.05f), M(bars[k]));
                DPrim(PrimitiveType.Cube, "Stand", u, new Vector3(x - 1.3f, 0.34f, mid), new Vector3(1.2f, 0.68f, 0.05f), M("DeskWood"));
                break;
            case "wardrobe":
                Box(u, "Wardrobe", new Vector3(x - 1.4f, 1.0f, backZ), new Vector3(1.1f, 2.0f, 0.6f), M("TrimDark"));
                Text("RentNote", u, new Vector3(x - 1.4f, 1.3f, backZ - s * 0.31f), Face(new Vector3(0, 0, -s)), new Vector2(0.8f, 0.4f), 0.12f, "<b>RENT DUE</b>\n<size=70%>one (1) skull</size>").color = new Color(0.9f, 0.1f, 0.1f);
                break;
            case "shack":
                Box(u, "Counter", new Vector3(x, 0.5f, mid - s * 1.0f), new Vector3(w - 0.6f, 1.0f, 0.6f), M("DeskWood"));
                for (int k = 0; k < 8; k++)
                    DPrim(PrimitiveType.Cube, "Awning", u, new Vector3(x - w / 2f + 0.4f + k * (w - 0.8f) / 7f, 2.75f, mid - s * 2.5f), new Vector3((w - 0.8f) / 8f, 0.06f, 0.8f), M(k % 2 == 0 ? "CaneRed" : "CaneWhite"), Quaternion.Euler(s * -20f, 0, 0));
                Text("Menu", u, new Vector3(x - 1.6f, 1.9f, mid + s * 2.1f), Face(new Vector3(0, 0, -s)), new Vector2(2.2f, 1.1f), 0.12f,
                     "<b>MENU</b>\nBone broth .... 2\nSpare ribs .... 3\nFinger food .... 1\n<color=#a00000>Soup of the day</color> .... free\n<size=70%>(it goes right through you)</size>").color = new Color(0.15f, 0.1f, 0.05f);
                break;
            case "news":
                Box(u, "Desk", new Vector3(x, 0.45f, mid - s * 0.9f), new Vector3(2.6f, 0.9f, 0.7f), M("DeskWood"));
                DPrim(PrimitiveType.Cube, "Papers", u, new Vector3(x - 0.6f, 0.92f, mid - s * 0.9f), new Vector3(0.4f, 0.02f, 0.3f), M("Paper"));
                break;
            case "rug":
                DPrim(PrimitiveType.Cube, "RuckedRug", u, new Vector3(x - 0.2f, 0.07f, mid - s * 0.6f), new Vector3(1.2f, 0.12f, 0.35f), M("TrimDark"), Quaternion.Euler(0, 15, 0));
                break;
            case "kitchen":
                Box(u, "Fridge", new Vector3(x - 1.9f, 0.9f, backZ), new Vector3(0.8f, 1.8f, 0.6f), M("CaneWhite"));
                Box(u, "Table", new Vector3(x - 0.9f, 0.38f, mid - s * 0.3f), new Vector3(1.0f, 0.76f, 0.8f), M("DeskWood"));
                DPrim(PrimitiveType.Cylinder, "Plate", u, new Vector3(x - 0.9f, 0.78f, mid - s * 0.3f), new Vector3(0.3f, 0.01f, 0.3f), M("CaneWhite"));
                DPrim(PrimitiveType.Sphere, "Food", u, new Vector3(x - 0.9f, 0.82f, mid - s * 0.3f), new Vector3(0.18f, 0.08f, 0.18f), M("GlowOrange"));
                break;
        }
    }

    // =================================================================== the horror game (FirHunt)

    static readonly float[] RZ0 = { -6f, -26f, -42f, -62f, -80f, -98f };
    static readonly float[] RZ1 = { -26f, -42f, -62f, -80f, -98f, -118f };
    static readonly float[] RDoor = { 0f, 0f, 0f, 0f, 0f, 7.5f };
    static FirHunt hunt;
    static readonly List<FirHuntButton> huntButtons = new List<FirHuntButton>();
    static readonly List<FirGag> scares = new List<FirGag>();
    static readonly List<GameObject> huntOnly = new List<GameObject>();

    static void BuildHunt(Transform root, Transform lobby, Transform spawn)
    {
        huntButtons.Clear(); scares.Clear(); huntOnly.Clear();
        var map = root.Find("Map");
        var hg = Group(root, "Hunt", new Vector3(0, 1.5f, -3f));
        hunt = Add<FirHunt>(hg.gameObject);
        hunt.sfx = Src(hg, Vector3.zero, null, false, 0f, 1f, 5f, false);
        hunt.heartbeat = Src(hg, Vector3.zero, Clip("heartbeat"), true, 0f, 0f, 5f, false);
        hunt.ambient = Src(hg, Vector3.zero, null, false, 1f, 0.9f, 14f, false);
        hunt.ambientClips = new[] { Clip("whisper"), Clip("rustle"), Clip("tree_laugh"), Clip("servo"), Clip("static"), Clip("tree_sing_wrong"), Clip("music_box_broken") };
        hunt.startClip = Clip("door_bang"); hunt.unlockClip = Clip("unlock"); hunt.wrongClip = Clip("wrong"); hunt.pickupClip = Clip("pickup");
        hunt.switchClip = Clip("switch"); hunt.endingClip = Clip("ending"); hunt.screamClip = Clip("scream2"); hunt.stunClip = Clip("toilet_flush");

        // ---- the face that fills your view when you're caught (outside the map, so it stays bright)
        var face = Group(hg, "ScareFace", Vector3.zero);
        Prim(PrimitiveType.Sphere, "Canopy", face, new Vector3(0, 0.05f, 0.35f), new Vector3(1.5f, 1.7f, 0.6f), M("ScareGreen"));
        foreach (float sx in new[] { -0.2f, 0.2f })
        {
            Prim(PrimitiveType.Sphere, "Eye", face, new Vector3(sx, 0.17f, 0.04f), Vector3.one * 0.24f, M("Eye"));
            Prim(PrimitiveType.Sphere, "Pupil", face, new Vector3(sx * 0.9f, 0.15f, -0.07f), Vector3.one * 0.11f, M("GlowRedEye"));
        }
        Prim(PrimitiveType.Sphere, "Mouth", face, new Vector3(0, -0.22f, 0.04f), new Vector3(0.55f, 0.32f, 0.14f), M("Mouth"));
        Prim(PrimitiveType.Sphere, "Throat", face, new Vector3(0, -0.22f, 0.0f), new Vector3(0.42f, 0.22f, 0.1f), M("Black"));
        for (int k = 0; k < 6; k++)
            Prim(PrimitiveType.Cube, "Tooth", face, new Vector3(-0.15f + k * 0.06f, -0.13f, -0.02f), new Vector3(0.04f, 0.07f, 0.02f), M("Teeth"));
        hunt.scareFace = face;

        // ---- checkpoints (each room's entrance) and the doors between rooms
        hunt.checkpoints = new Transform[6];
        for (int r = 0; r < 6; r++) hunt.checkpoints[r] = Marker(map, "Checkpoint " + r, new Vector3(RDoor[r], 0.05f, RZ0[r] - 1.6f), 180f);
        var doors = new GameObject[6];
        for (int r = 1; r < 6; r++)
        {
            var d = Group(map, "Door " + r, new Vector3(RDoor[r], 0, RZ0[r] - 0.1f));
            Box(d, "Panel", new Vector3(0, 1.4f, 0), new Vector3(2.4f, 2.8f, 0.12f), M("DoorWood"));
            for (int s = 0; s < 2; s++)
            {
                float zz = s == 0 ? 0.065f : -0.065f;
                var n = s == 0 ? Vector3.forward : Vector3.back;
                Prim(PrimitiveType.Cube, "Bar", d, new Vector3(0, 1.25f, zz), new Vector3(2.2f, 0.12f, 0.02f), M("Metal"));
                Prim(PrimitiveType.Cube, "Lock", d, new Vector3(0, 1.25f, zz * 1.4f), new Vector3(0.22f, 0.28f, 0.06f), M("Brass"));
                Text("Locked", d, new Vector3(0, 2.1f, zz * 1.2f), Face(n), new Vector2(2.2f, 0.5f), 0.2f,
                     s == 0 ? "<b>LOCKED</b>\n<size=70%>solve the room to open it</size>" : "<b>LOCKED</b>\n<size=70%>solve this room's puzzle</size>").color = new Color(1f, 0.8f, 0.3f);
            }
            doors[r] = d.gameObject;
        }
        hunt.doors = doors;

        // ---- each room's task board (inside, on the north wall, left of the doorway)
        hunt.roomBoards = new TextMeshPro[6];
        for (int r = 0; r < 6; r++)
        {
            float bx = RDoor[r] - 3.3f, bz = RZ0[r] - (r == 0 ? 0.32f : 0.12f);
            Box(map, "TaskBoard " + r, new Vector3(bx, 1.95f, bz - 0.02f), new Vector3(2.5f, 1.25f, 0.03f), M("Board"), false);
            var t = Text("TaskText " + r, map, new Vector3(bx, 1.95f, bz - 0.04f), Face(Vector3.back), new Vector2(2.35f, 1.15f), 0.14f, "");
            t.color = Color.white;
            hunt.roomBoards[r] = t;
        }

        var items = new List<GameObject>(); var done = new List<GameObject>(); var itemRoom = new List<int>();
        Func<int, string, Vector3, Vector3, Vector3, FirHuntButton> Item = (room, name, pos, normal, size) =>
        {
            var b = HB(map, name, pos, normal, size, null, 3, items.Count, "Pick up the " + name.ToLower(), null);
            items.Add(b.gameObject); done.Add(null); itemRoom.Add(room);
            return b;
        };

        // ---- room 0, Gift Wrap Hall: 5 batteries, then the power box
        var batt = new[] { new Vector3(3.0f, 2.18f, -19f), new Vector3(-2.0f, 0.08f, -14.9f), new Vector3(-4.35f, 0.38f, -13.9f), new Vector3(-4.5f, 0.08f, -24.9f), new Vector3(4.6f, 0.08f, -7.6f) };
        for (int i = 0; i < batt.Length; i++)
        {
            var b = Item(0, "Battery", batt[i], Vector3.forward, new Vector3(0.3f, 0.3f, 0.3f));
            Prim(PrimitiveType.Cylinder, "Cell", b.transform, Vector3.zero, new Vector3(0.08f, 0.07f, 0.08f), M("GlowYellow")).transform.localRotation = Quaternion.Euler(0, 0, 90);
            Prim(PrimitiveType.Cylinder, "Tip", b.transform, new Vector3(0.08f, 0, 0), new Vector3(0.03f, 0.012f, 0.03f), M("Metal")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        }
        var power = HB(map, "Power box", new Vector3(2.4f, 1.35f, -25.86f), Vector3.forward, new Vector3(0.7f, 0.9f, 0.18f), M("Metal"), 4, 0, "Power the door",
                       "<b>POWER BOX</b>\n<size=70%>5 batteries go here</size>");

        // ---- room 1, Rainbow Hall: 4 glowing numbers, a keypad
        var digitSpots = new[] {
            new { p = new Vector3(-7.95f, 3.7f, -30.5f), n = Vector3.right, c = new Color(1f, 0.19f, 0.19f) },
            new { p = new Vector3(-2.6f, 0.15f, -37.07f), n = Vector3.forward, c = new Color(1f, 0.88f, 0.19f) },
            new { p = new Vector3(7.95f, 0.7f, -35.0f), n = Vector3.left, c = new Color(0.25f, 1f, 0.31f) },
            new { p = new Vector3(6.6f, 4.3f, -26.15f), n = Vector3.back, c = new Color(0.25f, 0.5f, 1f) } };
        hunt.rainbowDigits = new TextMeshPro[4];
        for (int k = 0; k < 4; k++)
        {
            var t = Text("GlowDigit " + k, map, digitSpots[k].p, Face(digitSpots[k].n), new Vector2(0.6f, 0.6f), k == 1 ? 0.24f : 0.5f, "?");
            t.color = digitSpots[k].c;
            hunt.rainbowDigits[k] = t;
            huntOnly.Add(t.gameObject);
        }
        hunt.rainbowDisplay = Keypad(map, "Rainbow keypad", new Vector3(2.7f, 1.35f, -41.86f), Vector3.forward, 1);

        // ---- room 2, Toy Workshop: notes give the order, the toy lock on the exit wall
        hunt.toyNames = new[] { "STAR", "BELL", "SOCK", "DRUM", "BONE" };
        var lockG = Group(map, "Toy lock", new Vector3(2.7f, 1.35f, -61.86f));
        lockG.localRotation = Face(Vector3.forward);
        Box(lockG, "Backing", new Vector3(0, 0.1f, 0.03f), new Vector3(2.3f, 0.9f, 0.03f), M("Board"), false);
        Text("Title", lockG, new Vector3(0, 0.42f, -0.02f), Quaternion.identity, new Vector2(2.1f, 0.2f), 0.11f, "<b>THE TOY LOCK</b>  press the toys in order").color = Color.white;
        hunt.toyDisplay = Text("Display", lockG, new Vector3(0, 0.24f, -0.02f), Quaternion.identity, new Vector2(1.5f, 0.2f), 0.16f, "* * * *");
        hunt.toyDisplay.color = Color.white;
        for (int k = 0; k < 5; k++)
            HB(lockG, "Toy " + hunt.toyNames[k], new Vector3(-0.84f + k * 0.42f, -0.08f, 0f), Vector3.back, new Vector3(0.36f, 0.36f, 0.06f), M("ButtonGold"), 6, 200 + k,
               "Press the " + hunt.toyNames[k].ToLower(), "<b>" + hunt.toyNames[k] + "</b>", 0.09f, true);
        var noteSpots = new[] {
            new { p = new Vector3(-2.47f, 1.12f, -48.4f), n = Vector3.right },
            new { p = new Vector3(2.47f, 1.12f, -56.2f), n = Vector3.left },
            new { p = new Vector3(5.65f, 1.62f, -60.82f), n = Vector3.forward },
            new { p = new Vector3(-4.2f, 2.58f, -61.93f), n = Vector3.forward } };
        hunt.toyNotes = new TextMeshPro[4];
        for (int k = 0; k < 4; k++)
        {
            var ng = Group(map, "Note " + k, noteSpots[k].p);
            ng.localRotation = Face(noteSpots[k].n);
            Prim(PrimitiveType.Cube, "Paper", ng, new Vector3(0, 0, 0.006f), new Vector3(0.34f, 0.26f, 0.01f), M("Paper"));
            var t = Text("Text", ng, new Vector3(0, 0, -0.002f), Quaternion.identity, new Vector2(0.3f, 0.22f), 0.08f, "#?");
            t.color = new Color(1f, 0.95f, 0.7f);
            hunt.toyNotes[k] = t;
            huntOnly.Add(ng.gameObject);
        }

        // ---- room 3, Chimney Lounge: 4 ornaments, the bare tree
        var orn = new[] { new Vector3(6.75f, 1.76f, -70.95f), new Vector3(4.6f, 0.6f, -73.2f), new Vector3(-4.6f, 2.32f, -79.6f), new Vector3(-2.0f, 0.1f, -76.5f) };
        string[] ornMat = { "GlowRed", "GlowGold", "GlowRed", "GlowGold" };
        for (int i = 0; i < orn.Length; i++)
        {
            var b = Item(3, "Ornament", orn[i], Vector3.forward, new Vector3(0.3f, 0.3f, 0.3f));
            Prim(PrimitiveType.Sphere, "Ball", b.transform, Vector3.zero, Vector3.one * 0.15f, M(ornMat[i]));
            Prim(PrimitiveType.Cylinder, "Cap", b.transform, new Vector3(0, 0.08f, 0), new Vector3(0.04f, 0.015f, 0.04f), M("Brass"));
        }
        TreeAt(map, "BareTree", new Vector3(2.4f, 0, -78.3f), 180f, 2.0f, false);
        var bare = HB(map, "Bare tree", new Vector3(2.4f, 1.0f, -78.3f), Vector3.forward, new Vector3(1.3f, 2.0f, 1.3f), null, 4, 3, "Hang the ornaments", null);
        var hung = Group(map, "Hung ornaments", new Vector3(2.4f, 0, -78.3f));
        for (int k = 0; k < 8; k++)
        {
            float a = k * 2.4f, y = 0.9f + (k % 4) * 0.28f, rad = 0.62f - (k % 4) * 0.12f;
            Prim(PrimitiveType.Sphere, "Ornament", hung, new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad), Vector3.one * 0.13f, M(k % 2 == 0 ? "GlowRed" : "GlowGold"));
        }
        Text("BareSign", map, new Vector3(2.4f, 2.45f, -79.92f), Face(Vector3.forward), new Vector2(2f, 0.3f), 0.15f, "<b>the bare tree</b>").color = new Color(1f, 0.85f, 0.4f);

        // ---- room 4, Talent Show: the stage manager's memo, a keypad on the east walkway
        var show = map.Find("TALENT SHOW");
        int performers = show.GetComponentsInChildren<FirRainbowSkeleton>(true).Length;
        int seats = show.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Seat" && t.GetComponent<BoxCollider>() != null);
        hunt.talentCode = performers.ToString() + seats.ToString();
        Box(map, "MemoBoard", new Vector3(-8.97f, 1.55f, -90.4f), new Vector3(0.03f, 1.0f, 1.9f), M("Board"), false);
        Text("Memo", map, new Vector3(-8.94f, 1.55f, -90.4f), Face(Vector3.right), new Vector2(1.75f, 0.9f), 0.12f,
             "<b>STAGE MANAGER'S MEMO</b>\nStage door code: the number of performers on our stage tonight, then the number of seats in the house.\n<size=80%>(The babies don't count.)</size>").color = Color.white;
        hunt.talentDisplay = Keypad(map, "Stage door keypad", new Vector3(8.87f, 1.35f, -93.6f), Vector3.left, 4);

        // ---- room 5, Bone Yard: 3 gas valves, the furnace and its lever
        var valves = new[] {
            new { p = new Vector3(5.5f, 1.3f, -117.85f), n = Vector3.forward },
            new { p = new Vector3(-4.5f, 1.2f, -98.15f), n = Vector3.back },
            new { p = new Vector3(-3.3f, 1.2f, -110f), n = Vector3.right } };
        DPrim(PrimitiveType.Cylinder, "GasPipe", map, new Vector3(-3.5f, 1.7f, -110f), new Vector3(0.2f, 1.7f, 0.2f), M("Metal"));
        for (int i = 0; i < valves.Length; i++)
        {
            var b = Item(5, "Valve", valves[i].p, valves[i].n, new Vector3(0.45f, 0.45f, 0.12f));
            SetInteract(b, "Open the gas valve", 2.5f);
            Prim(PrimitiveType.Cylinder, "Wheel", b.transform, Vector3.zero, new Vector3(0.38f, 0.02f, 0.38f), M("GlowRed")).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Cube, "Spoke", b.transform, new Vector3(0, 0, -0.012f), new Vector3(0.36f, 0.04f, 0.02f), M("Metal"));
            var dg = Group(map, "Valve open", valves[i].p);
            dg.localRotation = Face(valves[i].n);
            Prim(PrimitiveType.Cylinder, "Wheel", dg, Vector3.zero, new Vector3(0.38f, 0.02f, 0.38f), M("GlowGreen")).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Cube, "Spoke", dg, new Vector3(0, 0, -0.012f), new Vector3(0.04f, 0.36f, 0.02f), M("Metal"));
            done[done.Count - 1] = dg.gameObject;
        }
        var furnace = Group(map, "Furnace", new Vector3(-4.8f, 0, -117.2f));
        Box(furnace, "Brick", new Vector3(0, 1.1f, -0.15f), new Vector3(2.4f, 2.2f, 1.3f), M("BrickWall"));
        Prim(PrimitiveType.Cube, "Mouth", furnace, new Vector3(0, 0.75f, 0.52f), new Vector3(1.3f, 1.1f, 0.02f), M("Black"));
        var fire = Prim(PrimitiveType.Cube, "Fire", furnace, new Vector3(0, 0.75f, 0.54f), new Vector3(1.25f, 1.05f, 0.02f), M("GlowOrange"));
        Text("FurnaceSign", furnace, new Vector3(0, 1.85f, 0.52f), Face(Vector3.forward), new Vector2(2.2f, 0.3f), 0.17f, "<b>THE FURNACE</b>").color = new Color(1f, 0.6f, 0.3f);
        HB(map, "Furnace lever", new Vector3(-2.9f, 1.25f, -117.85f), Vector3.forward, new Vector3(0.4f, 0.6f, 0.16f), M("Metal"), 4, 5, "Pull the furnace lever",
           "<b>LEVER</b>\n<size=70%>gas first</size>", 0.09f);
        var win = Text("WinSign", map, new Vector3(0, 2.7f, -117.95f), Face(Vector3.forward), new Vector2(8f, 0.6f), 0.4f, "<b>YOU ESCAPED!</b>");
        win.color = new Color(0.45f, 1f, 0.55f);
        hunt.stageShow = new[] { hung.gameObject, fire, win.gameObject };
        hunt.stageShowAt = new[] { 4, 6, 6 };

        hunt.items = items.ToArray(); hunt.itemDone = done.ToArray(); hunt.itemRoom = itemRoom.ToArray();

        // ---- the hunting tree
        var sgo = new GameObject("Stalker");
        sgo.transform.SetParent(map, false);
        sgo.transform.localPosition = new Vector3(4f, 0, -24f);
        var st = Add<FirStalker>(sgo);
        var body = Group(sgo.transform, "Body", Vector3.zero);
        Transform smouth;
        var shead = TreeModel(body, 2.5f, false, out smouth);
        foreach (var r in body.GetComponentsInChildren<MeshRenderer>(true))
            if (r.name.StartsWith("Pupil")) r.sharedMaterial = M("GlowRedEye");
        st.body = body.gameObject; st.head = shead; st.mouth = smouth;
        st.roomZ0 = RZ0; st.roomZ1 = RZ1; st.doorX = RDoor;
        var lairPos = new[] { new Vector3(4.5f, 0, -24.2f), new Vector3(-6.8f, 0, -40.6f), new Vector3(3.6f, 0, -60.2f), new Vector3(-1.5f, 0, -78.6f), new Vector3(-5f, 0, -90.5f), new Vector3(6f, 0, -115.5f) };
        st.lairs = lairPos.Select((p, i) => Marker(map, "Lair " + i, p, 0f)).ToArray();
        st.furnace = Marker(map, "In the furnace", new Vector3(-4.8f, 0, -117.4f), 0f);
        st.voice = Src(sgo.transform, new Vector3(0, 1.8f, 0), null, false, 1f, 1f, 22f, false);
        st.stunClip = Clip("toilet_clunk"); st.wakeClip = Clip("tree_laugh");
        hunt.stalker = UB(st); hunt.stalkerBody = sgo.transform;

        // ---- throwable toilets: hit the tree with one to stun it
        var tg = Group(map, "Toilets", Vector3.zero);
        var toiletAt = new[] { new Vector3(5.2f, 0, -12f), new Vector3(-1.4f, 0, -21.6f), new Vector3(-6.5f, 0, -28.2f), new Vector3(6.8f, 0, -34f),
                               new Vector3(1.2f, 0, -45f), new Vector3(-1.2f, 0, -58.6f), new Vector3(-3.5f, 0, -65f), new Vector3(1.0f, 0, -75.5f),
                               new Vector3(-8.0f, 0, -81.4f), new Vector3(-8.0f, 0, -88.6f), new Vector3(-6.0f, 0, -101f), new Vector3(4.5f, 0, -109f) };
        for (int i = 0; i < toiletAt.Length; i++) Toilet(tg, toiletAt[i], i * 47f);
        Text("ToiletTip", map, new Vector3(-3.3f, 1.12f, RZ0[0] - 0.36f), Face(Vector3.back), new Vector2(2.4f, 0.25f), 0.1f,
             "<color=#ffd060>Tip:</color> throw a toilet at the tree to stun it").color = Color.white;

        // ---- jump scares: trees that burst out as you pass (armed only during the game)
        var sc = Group(map, "Scares", Vector3.zero);
        ScareZone(sc, "Scare 0a", new Vector3(-6.2f, 0, -18.5f), 90f, 3.6f, "scream1");
        ScareZone(sc, "Scare 0b", new Vector3(6.2f, 0, -23f), -90f, 3.6f, "scream3");
        ScareZone(sc, "Scare 1a", new Vector3(-7.4f, 0, -33f), 90f, 5.5f, "scream2");
        ScareZone(sc, "Scare 1b", new Vector3(7.4f, 0, -28.6f), -90f, 5.5f, "scream1");
        ScareZone(sc, "Scare 2a", new Vector3(-6.2f, 0, -50f), 90f, 4.5f, "scream3");
        ScareZone(sc, "Scare 2b", new Vector3(6.2f, 0, -59.5f), -90f, 5.5f, "scream2");
        ScareZone(sc, "Scare 3a", new Vector3(-7.4f, 0, -76f), 90f, 5.5f, "scream1");
        ScareZone(sc, "Scare 3b", new Vector3(7.4f, 0, -65.5f), -90f, 5.5f, "scream3");
        ScareZone(sc, "Scare 4a", new Vector3(-8.4f, 0, -89f), 90f, 4.0f, "scream2");
        ScareZone(sc, "Scare 4b", new Vector3(6.3f, 0, -93.8f), 90f, 1.5f, "scream1");
        ScareZone(sc, "Scare 5a", new Vector3(-8.2f, 0, -108f), 90f, 5.5f, "scream3");
        ScareZone(sc, "Scare 5b", new Vector3(8.2f, 0, -100.6f), -90f, 1.7f, "scream2");

        // ---- the start room: the lobby's north wall
        var nb = Vector3.back;
        Box(lobby, "StartBoard", new Vector3(0, 1.85f, 5.995f), new Vector3(13.2f, 3.3f, 0.01f), M("Board"));
        Text("GameTitle", lobby, new Vector3(0, 3.1f, 5.97f), Face(nb), new Vector2(10f, 0.7f), 0.6f, "<b>FIR WARNING</b>").color = new Color(0.9f, 0.15f, 0.15f);
        Text("Tagline", lobby, new Vector3(0, 2.62f, 5.97f), Face(nb), new Vector2(12f, 0.35f), 0.2f,
             "<i>The animatronic Douglas firs were only meant to sing.</i>").color = new Color(1f, 0.85f, 0.4f);
        Text("Story", lobby, new Vector3(-4.2f, 1.55f, 5.97f), Face(nb), new Vector2(4.2f, 1.7f), 0.135f,
             "<b>THE STORY</b>\nThe toy factory's singing Christmas trees were recalled after \"the incident\". Tonight the power came back on " +
             "and one of them got up. It won't move while someone is looking at it...\n\nGet through six rooms, solve each one's puzzle to unlock " +
             "the next, and lead the tree to the furnace at the end.").color = Color.white;
        Text("HowTo", lobby, new Vector3(4.2f, 1.55f, 5.97f), Face(nb), new Vector2(4.2f, 1.7f), 0.135f,
             "<b>HOW TO PLAY</b>\n- Each room's board (by its door) says what to do.\n- Keep an eye on the tree: it only moves when nobody is watching. " +
             "With two, one of you watches while the other works.\n- Throw a toilet at it to stun it.\n- Caught? You go back to the room's entrance.\n" +
             "- The lobby is safe. Gags and skeletons are everywhere, game or not.").color = Color.white;
        hunt.lobbyBoard = Text("GameStatus", lobby, new Vector3(0, 2.15f, 5.97f), Face(nb), new Vector2(3.6f, 0.6f), 0.13f, "");
        hunt.lobbyBoard.color = Color.white;
        HB(lobby, "START", new Vector3(0, 1.4f, 5.9f), nb, new Vector3(1.6f, 0.55f, 0.12f), M("ButtonGold"), 1, 0, "Start the game (or join it)", "<b>START</b>", 0.3f, true);
        HB(lobby, "RESET", new Vector3(0, 0.75f, 5.9f), nb, new Vector3(0.8f, 0.3f, 0.12f), M("Button"), 2, 0, "End the game", "<b>RESET</b>", 0.14f, true);
        Text("Players", lobby, new Vector3(0, 0.42f, 5.97f), Face(nb), new Vector2(3.4f, 0.25f), 0.1f, "best with 2 players  -  about 45 minutes").color = new Color(0.75f, 0.75f, 0.75f);
        // the east wall: the posters and a decorated tree in the corner; the way into the map is the south doorway
        Poster(lobby, "poster_warning", new Vector3(6.97f, 1.85f, -3.6f), Vector3.left, 0.9f, 1.2f);
        Poster(lobby, "poster_original", new Vector3(6.97f, 1.85f, -0.6f), Vector3.left, 0.9f, 1.2f);
        Poster(lobby, "poster_safety", new Vector3(6.97f, 1.85f, 2.4f), Vector3.left, 0.9f, 1.2f);
        TreeAt(lobby, "LobbyTree", new Vector3(-5.6f, 0, 1.2f), 90f, 2.4f, true);
        string[] gm2 = { "GiftRed", "GiftGreen", "GiftGold", "GiftBlue" };
        for (int i = 0; i < 6; i++)
            DPrim(PrimitiveType.Cube, "Present", lobby, new Vector3(-5.6f + Mathf.Cos(i * 1.1f) * 0.95f, 0.17f, 1.2f + Mathf.Sin(i * 1.1f) * 0.85f), Vector3.one * (0.3f + 0.05f * (i % 3)), M(gm2[i % 4]), Quaternion.Euler(0, i * 23f, 0));
        Text("CheckNote", lobby, new Vector3(-6.97f, 2.35f, -3.6f), Face(Vector3.right), new Vector2(2.6f, 0.5f), 0.11f,
             "<b>Systems check</b>\n<size=85%>for the world's builder: tests that every script runs</size>").color = new Color(0.8f, 0.8f, 0.8f);

        hunt.huntOnly = huntOnly.ToArray();
        hunt.freeOnly = new GameObject[0];
        var me = UB(hunt);
        foreach (var b in huntButtons) b.hunt = me;
        foreach (var g in scares) g.hunt = me;
        report.Add("the horror game: " + items.Count + " items, " + huntButtons.Count + " buttons, 5 doors, " + scares.Count + " jump scares, " + toiletAt.Length +
                   " toilets; talent show code " + hunt.talentCode + " (" + performers + " performers, " + seats + " seats)");
    }

    /// <summary>Once everything is built: the throwables learn about the tree, and the map's materials are handed to the game to darken.</summary>
    static void FinishHunt(Transform root)
    {
        if (hunt == null) return;
        var me = UB(hunt);
        foreach (var th in root.GetComponentsInChildren<FirThrowable>(true)) { th.hunt = me; th.stalker = hunt.stalkerBody; }
        var map = root.Find("Map");
        var keepBright = new HashSet<string> { "Check_Button", "Check_ButtonGold", "Check_Paper" };
        var dark = new HashSet<Material>();
        foreach (var r in map.GetComponentsInChildren<Renderer>(true))
        {
            if (r.GetComponent<TextMeshPro>() != null) continue;
            var m = r.sharedMaterial;
            if (m == null || !m.name.StartsWith("Check_") || m.name.StartsWith("Check_Bulb") || m.name.StartsWith("Check_Lobby_") || keepBright.Contains(m.name)) continue;
            dark.Add(m);
        }
        // anything outside the map using one of those (the lobby, the test room, the scare face) gets its own bright copy
        int copies = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.GetComponent<TextMeshPro>() != null || r.transform.IsChildOf(map)) continue;
            if (r.sharedMaterial != null && dark.Contains(r.sharedMaterial)) { r.sharedMaterial = BrightCopy(r.sharedMaterial); copies++; }
        }
        hunt.darkMats = dark.ToArray();
        report.Add("the dark: " + dark.Count + " map materials go lantern-lit during the game (" + copies + " lobby parts keep bright copies)");
    }

    static Material BrightCopy(Material m)
    {
        string n = "Lobby_" + m.name.Replace("Check_", "");
        if (mats.TryGetValue(n, out var c)) return c;
        string path = Gen + "/Check_" + n + ".mat";
        c = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (c == null) { c = new Material(m); AssetDatabase.CreateAsset(c, path); }
        else { c.shader = m.shader; c.CopyPropertiesFromMaterial(m); }
        c.name = "Check_" + n;
        EditorUtility.SetDirty(c);
        mats[n] = c;
        return c;
    }

    /// <summary>A horror-game button: a box (or just a collider) facing `normal`, with optional text on its face.</summary>
    static FirHuntButton HB(Transform parent, string name, Vector3 pos, Vector3 normal, Vector3 size, Material mat, int action, int value, string interact,
                            string face, float faceH = 0.1f, bool pushes = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Face(normal);
        go.AddComponent<BoxCollider>().size = size;
        var front = Group(go.transform, "Front", Vector3.zero);   // the part that pushes in
        if (mat != null) Prim(PrimitiveType.Cube, "Visual", front, Vector3.zero, size, mat);
        if (face != null)
            Text("Label", front, new Vector3(0, 0, -size.z / 2 - 0.003f), Quaternion.identity, new Vector2(size.x * 0.95f, size.y * 0.9f), faceH, face).color = Color.white;
        var b = Add<FirHuntButton>(go);
        b.action = action; b.value = value;
        if (pushes) b.press = front;
        SetInteract(b, interact, 2.5f);
        huntButtons.Add(b);
        return b;
    }

    /// <summary>A 3 x 4 keypad (1-9, C, 0, OK) with a display; returns the display.</summary>
    static TextMeshPro Keypad(Transform parent, string name, Vector3 pos, Vector3 normal, int room)
    {
        var g = Group(parent, name, pos);
        g.localRotation = Face(normal);
        Box(g, "Backing", new Vector3(0, 0.05f, 0.03f), new Vector3(0.8f, 1.2f, 0.03f), M("Board"), false);
        Box(g, "Screen", new Vector3(0, 0.48f, 0.0f), new Vector3(0.66f, 0.16f, 0.02f), M("Black"), false);
        var disp = Text("Display", g, new Vector3(0, 0.48f, -0.012f), Quaternion.identity, new Vector2(0.62f, 0.15f), 0.12f, "_ _ _ _");
        disp.color = new Color(0.5f, 1f, 0.6f);
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
        int[] vals = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 0, 11 };
        for (int i = 0; i < 12; i++)
        {
            float x = -0.22f + (i % 3) * 0.22f, y = 0.24f - (i / 3) * 0.22f;
            HB(g, "Key " + keys[i], new Vector3(x, y, 0f), Vector3.back, new Vector3(0.18f, 0.18f, 0.05f), M(i == 11 ? "ButtonGold" : "Button"), 5, room * 100 + vals[i],
               i == 9 ? "Clear" : i == 11 ? "Enter" : "Press " + keys[i], "<b>" + keys[i] + "</b>", 0.09f, true);
        }
        return disp;
    }

    /// <summary>A jump scare: a tree hidden against a wall (facing yaw) bursts out when you walk into the zone `reach` metres in front of it.</summary>
    static void ScareZone(Transform parent, string name, Vector3 at, float yaw, float reach, string clip)
    {
        var g = Group(parent, name, at);
        g.localRotation = Quaternion.Euler(0, yaw, 0);
        var tree = Group(g, "Tree", Vector3.zero);
        Transform mouth;
        TreeModel(tree, 2.3f, false, out mouth);
        foreach (var r in tree.GetComponentsInChildren<MeshRenderer>(true)) if (r.name.StartsWith("Pupil")) r.sharedMaterial = M("GlowRedEye");
        var sg = Add<FirGag>(g.gameObject);
        sg.target = tree; sg.hideWhenIdle = true; sg.walkIn = true; sg.scare = true;
        sg.zoneCenter = new Vector3(0, 1f, reach); sg.zoneSize = new Vector3(3.2f, 2.6f, 2.6f);
        sg.moveBy = new Vector3(0, 0, Mathf.Min(1.4f, reach - 0.8f)); sg.scaleBy = 1.15f; sg.wobble = 6f;
        sg.inTime = 0.12f; sg.holdTime = 1.1f; sg.outTime = 0.45f;
        sg.sfx = Src(tree, new Vector3(0, 1.6f, 0.3f), Clip(clip), false, 1f, 1f, 16f, false);
        sg.mouth = mouth; sg.line = "";
        scares.Add(sg);
    }

    /// <summary>A throwable toilet (a pickup with a rigidbody and object sync, like the babies).</summary>
    static void Toilet(Transform g, Vector3 pos, float yaw)
    {
        var go = new GameObject("Toilet");
        go.transform.SetParent(g, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cylinder, "Base", go.transform, new Vector3(0, 0.12f, 0.05f), new Vector3(0.24f, 0.12f, 0.3f), M("Porcelain"));
        Prim(PrimitiveType.Sphere, "Bowl", go.transform, new Vector3(0, 0.33f, 0.1f), new Vector3(0.4f, 0.2f, 0.52f), M("Porcelain"));
        Prim(PrimitiveType.Cylinder, "Water", go.transform, new Vector3(0, 0.425f, 0.1f), new Vector3(0.26f, 0.005f, 0.36f), M("ToiletWater"));
        Prim(PrimitiveType.Cylinder, "Seat", go.transform, new Vector3(0, 0.44f, 0.1f), new Vector3(0.4f, 0.015f, 0.5f), M("Porcelain"));
        Prim(PrimitiveType.Cube, "Tank", go.transform, new Vector3(0, 0.6f, -0.2f), new Vector3(0.42f, 0.38f, 0.18f), M("Porcelain"));
        Prim(PrimitiveType.Cube, "Lid", go.transform, new Vector3(0, 0.8f, -0.2f), new Vector3(0.45f, 0.03f, 0.2f), M("Porcelain"));
        Prim(PrimitiveType.Cube, "Handle", go.transform, new Vector3(-0.17f, 0.72f, -0.1f), new Vector3(0.08f, 0.02f, 0.02f), M("Metal"));
        go.layer = 13;
        foreach (Transform ch in go.GetComponentsInChildren<Transform>(true)) ch.gameObject.layer = 13;
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.41f, -0.02f); col.size = new Vector3(0.44f, 0.82f, 0.62f);
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 3f; rb.useGravity = true; rb.isKinematic = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var pick = go.AddComponent<VRCPickup>();
        pick.InteractionText = "Pick up the toilet";
        pick.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes;
        pick.ThrowVelocityBoostScale = 1.4f;
        pick.proximity = 2f;
        go.AddComponent<VRCObjectSync>();
        var th = Add<FirThrowable>(go);
        th.pickup = pick; th.body = rb;
        th.audioSrc = Src(go.transform, new Vector3(0, 0.5f, 0), null, false, 1f, 1f, 14f, false);
        th.grabClip = Clip("whoosh"); th.landClip = Clip("toilet_clunk");
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
        var wns = root.GetComponentsInChildren<FirWanderer>(true);
        var me = UB(sh);
        for (int i = 0; i < gags.Length; i++) { gags[i].share = me; gags[i].shareIndex = i; }
        for (int i = 0; i < sgs.Length; i++) { sgs[i].share = me; sgs[i].shareIndex = i; }
        for (int i = 0; i < cls.Length; i++) { cls[i].share = me; cls[i].shareIndex = i; }
        for (int i = 0; i < rbs.Length; i++) { rbs[i].share = me; rbs[i].shareIndex = i; }
        for (int i = 0; i < cbs.Length; i++) { cbs[i].share = me; cbs[i].shareIndex = i; }
        for (int i = 0; i < wns.Length; i++) { wns[i].share = me; wns[i].shareIndex = i; }
        sh.gags = gags.Select(x => UB(x)).ToArray();
        sh.skeletonGags = sgs.Select(x => UB(x)).ToArray();
        sh.closets = cls.Select(x => UB(x)).ToArray();
        sh.gagSkeletons = rbs.Select(x => UB(x)).ToArray();
        sh.combos = cbs.Select(x => UB(x)).ToArray();
        sh.wanderers = wns.Select(x => UB(x)).ToArray();
        report.Add("shared with everyone: " + gags.Count(g => !g.walkIn) + " pressed gags, " + sgs.Length + " routines, " + cls.Length +
                   " closet, " + rbs.Length + " gag skeletons, " + cbs.Length + " combo acts, " + wns.Length + " wanderers");
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
