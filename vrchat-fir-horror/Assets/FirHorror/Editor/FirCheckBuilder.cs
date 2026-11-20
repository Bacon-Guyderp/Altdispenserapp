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
        // the east wall has a doorway (z -3.2 to -0.8) through to the Root Cellar, the horror expansion
        Box(lobby, "WallE S", new Vector3(7.1f, 1.8f, -4.7f), new Vector3(0.2f, 3.6f, 3.0f), M("Wall"));
        Box(lobby, "WallE N", new Vector3(7.1f, 1.8f, 2.7f), new Vector3(0.2f, 3.6f, 7.0f), M("Wall"));
        Box(lobby, "WallE Top", new Vector3(7.1f, 3.2f, -2.0f), new Vector3(0.2f, 0.8f, 2.4f), M("Wall"));
        DoorFrameX(lobby, 7.0f, -2.0f);
        Text("CellarDoorSign", lobby, new Vector3(6.98f, 3.25f, -2.0f), Face(Vector3.left), new Vector2(2.6f, 0.5f), 0.2f,
             "<b>THE ROOT CELLAR</b>\n<size=70%>the horror expansion</size>").color = new Color(0.9f, 0.3f, 0.25f);
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
        BuildCellar(root);
        BuildStation(lobby, spawn);
        BuildPark(root);
        BuildPrecinct(root);
        BuildPizzeria(root);
        BuildPixel(root);
        BuildAnnex(root);
        BuildBlocklands(root);
        BuildLabGags(root);
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
        BuildMapSkeletons(map, slots);

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
            // the model faces +z, so yaw 0 faces the doorway (north). Red is at +x, so the rainbow reads red to
            // violet, left to right, as you walk in
            float x = 4.5f - i * 1.5f;
            GagSkeleton(g, colours[i], new Vector3(x, 0.3f, -38.3f), 0f, 1f, i, hex[i], clips[i],
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
            float x = -5.3f + i * 1.15f;  // facing the audience (+z), their right is +x: the giant is on tiny's right
            var bones = names2[i] == "Rainbow" ? rainbowBones : new[] { M(names2[i] == "Tiny" || names2[i] == "Giant" ? "BoneWhite" : "Bone" + names2[i]) };
            string title = names2[i] == "Rainbow" ? "<color=#ff3030>R</color><color=#ff9020>A</color><color=#ffe030>I</color><color=#40ff50>N</color><color=#4080ff>B</color><color=#7a5cff>O</color><color=#c060ff>W</color>" : null;
            // facing the audience (north)
            GagSkeleton(g, names2[i], new Vector3(x, 0.6f, -95.5f), 0f, scale2[i], 8 + i, hex2[i], "Rainbow/rb_" + names2[i].ToLower(),
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
                Box(g, "SeatBack", new Vector3(sx, 0.75f, rz + 0.3f), new Vector3(0.7f, 0.6f, 0.08f), M("Seat"), false);   // behind the baby, away from the stage
                seats.Add(new Vector3(sx, 0.47f, rz - 0.12f));
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
        A("LabWhite", null, new Color(0.92f, 0.93f, 0.95f), 1, Color.black);
        A("LabFloor", "concrete", new Color(0.75f, 0.77f, 0.8f), 1, Color.black);
        A("LabGrey", null, new Color(0.55f, 0.58f, 0.62f), 1, Color.black);
        A("PortalPurple", null, new Color(0.7f, 0.3f, 1f), 1, new Color(0.45f, 0.15f, 0.7f));
        A("PortalGreen", null, new Color(0.3f, 1f, 0.45f), 1, new Color(0.15f, 0.65f, 0.25f));
        A("PadUp", null, new Color(0.8f, 0.2f, 0.2f), 1, new Color(0.4f, 0.05f, 0.05f));
        A("PadDown", null, new Color(0.2f, 0.9f, 0.3f), 1, new Color(0.1f, 0.5f, 0.15f));
        A("Glass", null, new Color(0.7f, 0.85f, 0.95f), 1, new Color(0.15f, 0.2f, 0.25f));
        A("Dirt", null, new Color(0.5f, 0.33f, 0.2f), 1, Color.black);
        A("Stone", null, new Color(0.55f, 0.55f, 0.57f), 1, Color.black);
        A("GrassBlock", null, new Color(0.4f, 0.75f, 0.3f), 1, Color.black);
        A("OreBlue", null, new Color(0.3f, 0.6f, 1f), 1, new Color(0.1f, 0.25f, 0.5f));
        A("Ghost", null, new Color(0.6f, 0.85f, 1f), 1, new Color(0.15f, 0.25f, 0.35f));
        A("PixelGreen", null, new Color(0.35f, 0.75f, 0.3f), 1, Color.black);
        A("PixelGreenDark", null, new Color(0.25f, 0.6f, 0.25f), 1, Color.black);
        A("PixelSand", null, new Color(0.93f, 0.82f, 0.55f), 1, Color.black);
        A("PixelSky", null, new Color(0.45f, 0.6f, 0.95f), 1, new Color(0.08f, 0.1f, 0.18f));
        A("PixelRoof", null, new Color(0.8f, 0.25f, 0.2f), 1, Color.black);
        A("PixelRoofBlue", null, new Color(0.25f, 0.4f, 0.85f), 1, Color.black);
        A("PixelPine", null, new Color(0.55f, 0.35f, 0.2f), 1, Color.black);
        A("ChatOrange", null, new Color(0.95f, 0.55f, 0.1f), 1, Color.black);
        A("TileOff", null, new Color(0.15f, 0.15f, 0.25f), 1, Color.black);
        A("TileOn", null, new Color(0.4f, 1f, 0.6f), 1, new Color(0.2f, 0.6f, 0.3f));
        A("PoliceBlue", null, new Color(0.08f, 0.14f, 0.35f), 1, Color.black);
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

    static readonly string[] Scripts = { "FirCheckHub", "FirCheckButton", "FirCheckTyped", "FirCheckSkeleton", "FirCheckTree", "FirRainbowSkeleton", "FirSkeletonCombo", "FirGag", "FirSkeletonGag", "FirCloset", "FirThrowable", "FirShare", "FirHunt", "FirHuntButton", "FirStalker", "FirWanderer", "FirTicker", "FirRide", "FirSeat", "FirTeleport", "FirNight", "FirEncounter", "FirTiles", "FirSay", "FirPortal", "FirPad", "FirVoxel" };   // (the Root Cellar reuses FirHunt and FirStalker)

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

    // =================================================================== the Test Annex and Blocklands, reached from the Station

    /// <summary>A voice that speaks (with a caption bubble at the speaker) when you walk into its zone.</summary>
    static void Announce(Transform parent, string name, Vector3 speaker, Vector3 zoneCenter, Vector3 zoneSize, string clip, string line)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = speaker;
        var dummy = Group(go.transform, "Nothing", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "Speaker", go.transform, Vector3.zero, new Vector3(0.6f, 0.03f, 0.6f), M("Black")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Sphere, "Eye", go.transform, new Vector3(0, 0, -0.04f), Vector3.one * 0.18f, M("GlowGhost"));
        var gag = Add<FirGag>(go);
        var c = Clip(clip);
        gag.target = dummy; gag.walkIn = true; gag.line = "<b>PROCTOR:</b> " + line;
        gag.zoneCenter = zoneCenter - speaker; gag.zoneSize = zoneSize;
        gag.inTime = 0.1f; gag.holdTime = Mathf.Max(1.5f, c != null ? c.length : 3f); gag.outTime = 0.1f;
        gag.voice = Src(go.transform, Vector3.zero, c, false, 0.3f, 1f, 30f, false);
        gag.bubble = Bubble(go.transform, new Vector3(0, -0.7f, -0.1f));
        gag.bubbleObject = gag.bubble.gameObject; gag.bubbleTransform = gag.bubble.transform;
    }

    static Transform PortalRing(Transform parent, string name, Vector3 pos, float yaw, string mat)
    {
        var t = Group(parent, name, pos);
        t.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cylinder, "Ring", t, new Vector3(0, 1.15f, 0), new Vector3(1.6f, 0.04f, 2.3f), M(mat)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        var swirl = Group(t, "Swirl", new Vector3(0, 1.15f, 0));
        Prim(PrimitiveType.Cylinder, "Inner", swirl, Vector3.zero, new Vector3(1.4f, 0.03f, 2.1f), M("Black")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Cube, "Streak", swirl, new Vector3(0, 0, -0.03f), new Vector3(1.2f, 0.06f, 0.01f), M(mat));
        Prim(PrimitiveType.Cube, "Streak", swirl, new Vector3(0, 0, -0.03f), new Vector3(0.06f, 1.8f, 0.01f), M(mat));
        return t;
    }

    static Transform Crate(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        Prim(PrimitiveType.Cube, "Box", go.transform, Vector3.zero, Vector3.one * 0.6f, M("LabGrey"));
        foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            Prim(PrimitiveType.Sphere, "Skull", go.transform, n * 0.305f, new Vector3(0.22f, 0.22f, 0.22f) - new Vector3(Mathf.Abs(n.x), 0, Mathf.Abs(n.z)) * 0.2f, M("BoneWhite"));
        go.layer = 13;
        foreach (Transform ch in go.GetComponentsInChildren<Transform>(true)) ch.gameObject.layer = 13;
        go.AddComponent<BoxCollider>().size = Vector3.one * 0.6f;
        var rb = go.AddComponent<Rigidbody>(); rb.mass = 4f; rb.useGravity = true; rb.isKinematic = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
        var pick = go.AddComponent<VRCPickup>(); pick.InteractionText = "Pick up the test crate"; pick.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pick.proximity = 2f;
        go.AddComponent<VRCObjectSync>();
        return go.transform;
    }

    static FirPad Pad(Transform parent, string name, Vector3 pos, Transform[] crates, GameObject door, GameObject[] showWhenDown)
    {
        var p = Group(parent, name, pos);
        Prim(PrimitiveType.Cylinder, "Base", p, new Vector3(0, 0.05f, 0), new Vector3(1.6f, 0.05f, 1.6f), M("LabGrey"));
        var top = Prim(PrimitiveType.Cylinder, "Light", p, new Vector3(0, 0.11f, 0), new Vector3(1.1f, 0.02f, 1.1f), M("PadUp"));
        var pad = Add<FirPad>(p.gameObject);
        pad.crates = crates; pad.crateSync = crates.Select(c => c.GetComponent<VRCObjectSync>()).ToArray();
        pad.door = door; pad.showWhenDown = showWhenDown ?? new GameObject[0];
        pad.padLight = top.GetComponent<MeshRenderer>(); pad.upMat = M("PadUp"); pad.downMat = M("PadDown");
        pad.sfx = Src(p, new Vector3(0, 0.5f, 0), null, false, 1f, 1f, 12f, false);
        pad.downClip = Clip("unlock"); pad.upClip = Clip("switch");
        return pad;
    }

    static void BuildAnnex(Transform root)
    {
        var g = Group(root, "Test Annex", Vector3.zero);
        float h = 6f;
        Box(g, "Floor", new Vector3(338f, -0.1f, 42f), new Vector3(36f, 0.2f, 44f), M("LabFloor"));
        Box(g, "Ceiling", new Vector3(338f, h + 0.1f, 42f), new Vector3(36f, 0.2f, 44f), M("LabWhite"));
        Box(g, "WallW", new Vector3(319.9f, h / 2, 42f), new Vector3(0.2f, h, 44.4f), M("LabWhite"));
        Box(g, "WallE", new Vector3(356.1f, h / 2, 42f), new Vector3(0.2f, h, 44.4f), M("LabWhite"));
        Box(g, "WallS", new Vector3(338f, h / 2, 19.9f), new Vector3(36.4f, h, 0.2f), M("LabWhite"));
        Box(g, "WallN", new Vector3(338f, h / 2, 64.1f), new Vector3(36.4f, h, 0.2f), M("LabWhite"));
        Action<string, float, bool> Across = (n, z, gap) =>
        {
            if (!gap) { Box(g, n, new Vector3(338f, h / 2, z), new Vector3(36f, h, 0.2f), M("LabWhite")); return; }
            Box(g, n + " W", new Vector3(328f, h / 2, z), new Vector3(16f, h, 0.2f), M("LabWhite"));
            Box(g, n + " E", new Vector3(348f, h / 2, z), new Vector3(16f, h, 0.2f), M("LabWhite"));
            Box(g, n + " Top", new Vector3(338f, (2.8f + h) / 2f, z), new Vector3(4f, h - 2.8f, 0.2f), M("LabWhite"));
        };
        Across("Wall28", 28f, true); Across("Wall34", 34f, false); Across("Wall40", 40f, true); Across("Wall50", 50f, true); Across("Wall60", 60f, true);
        foreach (float lz in new[] { 24f, 31f, 37f, 45f, 55f, 62f }) PendantLamp(g, new Vector3(338f, h, lz), 0.8f);
        var arrive = Marker(g, "Arrive", new Vector3(338f, 0.05f, 22f), 0f);
        StationStop("TEST ANNEX", arrive, g, new Vector3(335.3f, 1.3f, 20.15f), Vector3.forward);
        Text("Title", g, new Vector3(338f, 4.6f, 20.05f), Face(Vector3.forward), new Vector2(14f, 0.8f), 0.55f, "<b>BONEWELL TESTING ANNEX</b>").color = new Color(0.2f, 0.25f, 0.35f);
        Text("Sub", g, new Vector3(338f, 3.9f, 20.05f), Face(Vector3.forward), new Vector2(14f, 0.4f), 0.2f, "three tests. one crate each. zero refunds.").color = new Color(0.35f, 0.4f, 0.5f);
        Text("T1", g, new Vector3(338f, 3.3f, 28.12f), Face(Vector3.back), new Vector2(3f, 0.4f), 0.3f, "<b>TEST 1</b>").color = new Color(0.2f, 0.25f, 0.35f);
        Text("T2", g, new Vector3(338f, 3.3f, 40.12f), Face(Vector3.back), new Vector2(3f, 0.4f), 0.3f, "<b>TEST 2</b>").color = new Color(0.2f, 0.25f, 0.35f);
        Text("T3", g, new Vector3(338f, 3.3f, 50.12f), Face(Vector3.back), new Vector2(3f, 0.4f), 0.3f, "<b>TEST 3</b>").color = new Color(0.2f, 0.25f, 0.35f);

        // test 1: a wall with a locked (fake) door; a portal over it
        Box(g, "FakeDoor", new Vector3(338f, 1.3f, 33.85f), new Vector3(2.2f, 2.6f, 0.1f), M("Metal"), false);
        Text("FakeDoorSign", g, new Vector3(338f, 1.6f, 33.79f), Face(Vector3.back), new Vector2(2f, 0.4f), 0.2f, "<b>LOCKED</b>\n<size=70%>(it's a test)</size>").color = new Color(0.9f, 0.2f, 0.2f);
        var pa = PortalRing(g, "Portal A", new Vector3(330f, 0, 31.2f), 0f, "PortalPurple");
        var pb = PortalRing(g, "Portal B", new Vector3(346f, 0, 37.0f), 180f, "PortalGreen");
        var p1 = Add<FirPortal>(g.Find("Portal A").gameObject);
        p1.a = pa; p1.b = pb; p1.spinA = pa.Find("Swirl"); p1.spinB = pb.Find("Swirl");
        p1.sfx = Src(pa, new Vector3(0, 1f, 0), Clip("whoosh"), false, 0f, 0.8f, 5f, false);

        // test 2: a crate onto the button opens the door
        var crate1 = Crate(g, "Test crate 1", new Vector3(330f, 0.35f, 44f));
        var door2 = Box(g, "Door 2", new Vector3(338f, 1.4f, 50f), new Vector3(4f, 2.8f, 0.15f), M("Metal"));
        Pad(g, "Button 1", new Vector3(346f, 0, 45f), new[] { crate1 }, door2, null);

        // test 3: the crate's behind glass; a portal goes there
        Box(g, "Glass", new Vector3(344f, h / 2, 55f), new Vector3(0.1f, h, 10f), M("Glass"));
        var pc = PortalRing(g, "Portal C", new Vector3(340.5f, 0, 55.5f), -90f, "PortalPurple");
        var pd = PortalRing(g, "Portal D", new Vector3(350f, 0, 57.5f), 90f, "PortalGreen");
        var p2 = Add<FirPortal>(pc.gameObject);
        p2.a = pc; p2.b = pd; p2.spinA = pc.Find("Swirl"); p2.spinB = pd.Find("Swirl");
        p2.sfx = Src(pc, new Vector3(0, 1f, 0), Clip("whoosh"), false, 0f, 0.8f, 5f, false);
        var crate2 = Crate(g, "Test crate 2", new Vector3(353f, 0.35f, 53f));
        var door3 = Box(g, "Door 3", new Vector3(338f, 1.4f, 60f), new Vector3(4f, 2.8f, 0.15f), M("Metal"));
        var done = Text("TestComplete", g, new Vector3(338f, 3.2f, 63.95f), Face(Vector3.back), new Vector2(8f, 1f), 0.6f, "<b>TESTING COMPLETE</b>\n<size=45%>well done. (this sign is the reward.)</size>");
        done.color = new Color(0.2f, 0.7f, 0.3f);
        Pad(g, "Button 2", new Vector3(330f, 0, 57f), new[] { crate2 }, door3, new[] { done.gameObject });
        int b0 = huntButtons.Count;
        var respawns = new[] { new { p = new Vector3(334f, 1.3f, 40.15f), pad = "Button 1" }, new { p = new Vector3(334f, 1.3f, 50.15f), pad = "Button 2" } };
        foreach (var r in respawns)
        {
            var b = HB(g, "Respawn crate", r.p, Vector3.forward, new Vector3(0.9f, 0.3f, 0.1f), M("Button"), 51, 0, "Send the crate home", "<b>RESPAWN CRATE</b>", 0.09f, true);
            b.hunt = UB(g.Find(r.pad).GetComponent<FirPad>());
        }
        TeleportButton(g, "Back to the lobby", new Vector3(335f, 1.3f, 63.85f), Vector3.back, lobbyArrive, new Vector3(1.0f, 0.3f, 0.1f));

        // PROCTOR, the announcer
        var an = Group(g, "Proctor", Vector3.zero);
        Announce(an, "Proctor 0", new Vector3(341.5f, 3.6f, 27.85f), new Vector3(338f, 1f, 24f), new Vector3(10f, 3f, 5f), "Lab/lb_proctor0", "Welcome to the Bonewell Testing Annex. You have volunteered. Please do not check whether you volunteered.");
        Announce(an, "Proctor 1", new Vector3(341.5f, 3.6f, 33.85f), new Vector3(338f, 1f, 30.5f), new Vector3(14f, 3f, 4f), "Lab/lb_proctor1", "Test one. The door is locked. The portal is not. Draw your own conclusions.");
        Announce(an, "Proctor 2", new Vector3(341.5f, 3.6f, 39.85f), new Vector3(341f, 1f, 36.5f), new Vector3(14f, 3f, 4f), "Lab/lb_proctor2", "Excellent. You walked through a hole. Science thanks you.");
        Announce(an, "Proctor 3", new Vector3(341.5f, 3.6f, 49.85f), new Vector3(338f, 1f, 42f), new Vector3(8f, 3f, 3f), "Lab/lb_proctor3", "Test two. Place the crate on the button. The crate does not mind. We asked.");
        Announce(an, "Proctor 4", new Vector3(334.5f, 3.6f, 59.85f), new Vector3(338f, 1f, 51.5f), new Vector3(6f, 3f, 2.5f), "Lab/lb_proctor4", "The door is open. Please do not grow attached to the crate. It has already forgotten you.");
        Announce(an, "Proctor 5", new Vector3(326f, 3.6f, 59.85f), new Vector3(328f, 1f, 55f), new Vector3(8f, 3f, 6f), "Lab/lb_proctor5", "Test three. The crate is on the other side of the glass. So is a portal. Coincidence? Yes.");
        Announce(an, "Proctor 6", new Vector3(341f, 3.6f, 63.85f), new Vector3(338f, 1f, 62f), new Vector3(6f, 3f, 3f), "Lab/lb_proctor6", "Testing complete. Your reward is the satisfaction of a test completed. And this sign.");
        report.Add("Test Annex: 3 tests, 2 portal pairs, 2 crates on 2 buttons, 7 announcements");
    }

    static void CubeHead(string who, string mat)
    {
        if (!rigs.TryGetValue(who, out var r)) return;
        Prim(PrimitiveType.Cube, "CubeHead", r[2], new Vector3(0, 0.05f, 0.02f), Vector3.one * 0.34f, M(mat));
        foreach (float ex in new[] { -0.07f, 0.07f }) Prim(PrimitiveType.Cube, "CubeEye", r[2], new Vector3(ex, 0.08f, 0.195f), new Vector3(0.06f, 0.06f, 0.01f), M("Black"));
    }

    static void BuildBlocklands(Transform root)
    {
        var g = Group(root, "Blocklands", Vector3.zero);
        float h = 14f;
        Box(g, "Grass", new Vector3(400f, -0.1f, 40f), new Vector3(40f, 0.2f, 40f), M("PixelGreen"));
        Box(g, "Sky", new Vector3(400f, h + 0.1f, 40f), new Vector3(40f, 0.2f, 40f), M("Sky"));
        Box(g, "WallW", new Vector3(379.9f, h / 2, 40f), new Vector3(0.2f, h, 40.4f), M("PixelSky"));
        Box(g, "WallE", new Vector3(420.1f, h / 2, 40f), new Vector3(0.2f, h, 40.4f), M("PixelSky"));
        Box(g, "WallS", new Vector3(400f, h / 2, 19.9f), new Vector3(40.4f, h, 0.2f), M("PixelSky"));
        Box(g, "WallN", new Vector3(400f, h / 2, 60.1f), new Vector3(40.4f, h, 0.2f), M("PixelSky"));
        // blocky hills along the back, trees, clouds
        for (int k = 0; k < 20; k++)
        {
            int hh = 1 + (k * 7) % 4;
            for (int y = 0; y < hh; y++) DPrim(PrimitiveType.Cube, "Hill", g, new Vector3(380.5f + k * 2f, 0.5f + y, 58.5f - (y % 2)), new Vector3(2f, 1f, 2f), M(y == hh - 1 ? "PixelGreen" : "Dirt"));
        }
        foreach (var tp in new[] { new Vector3(385f, 0, 28f), new Vector3(415f, 0, 30f), new Vector3(386f, 0, 54f), new Vector3(414f, 0, 54f), new Vector3(400f, 0, 54f) }) PixelTree(g, tp, 1.8f);
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cube, "Cloud", g, new Vector3(383f + k * 6.5f, 12.5f, 26f + (k * 11) % 30), new Vector3(4f, 1f, 2f), M("CaneWhite"));
        var arrive = Marker(g, "Arrive", new Vector3(400f, 0.05f, 22f), 0f);
        StationStop("BLOCKLANDS", arrive, g, new Vector3(397.3f, 1.3f, 20.15f), Vector3.forward);
        Text("Title", g, new Vector3(400f, 6f, 20.05f), Face(Vector3.forward), new Vector2(14f, 1.2f), 0.9f, "<b>BLOCKLANDS</b>").color = new Color(0.35f, 0.25f, 0.15f);
        Text("Sub", g, new Vector3(400f, 5f, 20.05f), Face(Vector3.forward), new Vector2(14f, 0.4f), 0.22f, "press a block in the quarry to mine it. press a ghost block on the plot to build.").color = Color.white;

        var vgo = Group(g, "World", new Vector3(400f, 1f, 30f));
        var vox = Add<FirVoxel>(vgo.gameObject);
        int b0 = huntButtons.Count;
        // the quarry: 8 x 6 blocks, grass on the edge, stone inside, ore here and there
        var blocks = new List<GameObject>();
        for (int iz = 0; iz < 6; iz++) for (int ix = 0; ix < 8; ix++)
        {
            int i = iz * 8 + ix;
            bool edge = ix == 0 || ix == 7 || iz == 0 || iz == 5;
            string mat = (i * 37) % 11 == 3 ? "GlowGold" : (i * 53) % 13 == 5 ? "OreBlue" : edge ? "GrassBlock" : "Stone";
            var b = HB(g, "Block " + i, new Vector3(390.5f + ix, 0.5f, 34.5f + iz), Vector3.back, Vector3.one, M(mat), 41, i, "Mine this block", null);
            blocks.Add(b.gameObject);
        }
        vox.blocks = blocks.ToArray();
        Text("QuarrySign", g, new Vector3(394f, 2.2f, 33.9f), Face(Vector3.back), new Vector2(5f, 0.4f), 0.25f, "<b>THE QUARRY</b>  <size=70%>(press to mine)</size>").color = Color.white;
        // the building plot: 4 x 3, two layers
        var ghosts = new List<GameObject>(); var solids = new List<GameObject>(); var below = new List<int>();
        for (int layer = 0; layer < 2; layer++) for (int iz = 0; iz < 3; iz++) for (int ix = 0; ix < 4; ix++)
        {
            int j = layer * 12 + iz * 4 + ix;
            var pos = new Vector3(404.5f + ix, 0.5f + layer, 30.5f + iz);
            var gb = HB(g, "Ghost " + j, pos, Vector3.back, Vector3.one * 0.96f, null, 42, j, "Build a block here", null);
            Prim(PrimitiveType.Cube, "Marker", gb.transform, Vector3.zero, Vector3.one * 0.32f, M("Ghost"));   // a small floating cube: "build here"
            ghosts.Add(gb.gameObject);
            solids.Add(Box(g, "Built " + j, pos, Vector3.one, M(layer == 0 ? "Dirt" : "Stone")));
            below.Add(layer == 0 ? -1 : j - 12);
        }
        vox.slotGhost = ghosts.ToArray(); vox.slotSolid = solids.ToArray(); vox.slotBelow = below.ToArray();
        Box(g, "PocketBoard", new Vector3(406f, 3.0f, 29.4f), new Vector3(2.4f, 0.8f, 0.04f), M("Board"), false);
        vox.pocketText = Text("Pocket", g, new Vector3(406f, 3.0f, 29.37f), Face(Vector3.back), new Vector2(2.3f, 0.7f), 0.18f, "");
        vox.pocketText.color = Color.white;
        HB(g, "Reset the world", new Vector3(410.5f, 1.3f, 29.4f), Vector3.back, new Vector3(1.0f, 0.3f, 0.1f), M("Button"), 43, 0, "Put every block back", "<b>RESET WORLD</b>", 0.09f, true);
        vox.sfx = Src(vgo, Vector3.zero, null, false, 0.5f, 1f, 20f, false);
        vox.mineClip = Clip("toilet_clunk"); vox.placeClip = Clip("switch"); vox.nopeClip = Clip("wrong");
        var me = UB(vox);
        foreach (var b in huntButtons.Skip(b0)) b.hunt = me;
        // the farm and the sheep
        for (int k = 0; k < 12; k++) DPrim(PrimitiveType.Cube, "Wheat", g, new Vector3(384f + (k % 4) * 0.8f, 0.35f, 44f + (k / 4) * 0.8f), new Vector3(0.5f, 0.7f, 0.5f), M("GlowYellow"));
        for (int k = 0; k < 3; k++)
        {
            var sp = new Vector3(412f + k * 1.6f, 0, 44.5f + (k % 2) * 1.4f);
            DPrim(PrimitiveType.Cube, "SheepBody", g, sp + new Vector3(0, 0.55f, 0), new Vector3(0.8f, 0.6f, 1.1f), M("CaneWhite"));
            DPrim(PrimitiveType.Cube, "SheepHead", g, sp + new Vector3(0, 0.75f, 0.65f), new Vector3(0.45f, 0.45f, 0.4f), M("Trunk"));
            foreach (float lx in new[] { -0.25f, 0.25f }) foreach (float lz in new[] { -0.35f, 0.35f })
                DPrim(PrimitiveType.Cube, "SheepLeg", g, sp + new Vector3(lx, 0.13f, lz), new Vector3(0.18f, 0.26f, 0.18f), M("Trunk"));
        }
        // the villagers, with block heads
        var folk = new[] { new { who = "Digby", hex = "#795548", act = 23, p = new Vector3(388.6f, 0, 36.5f), yaw = 90f, head = "Dirt" },
                           new { who = "Brick", hex = "#d84315", act = 25, p = new Vector3(410.5f, 0, 32f), yaw = -90f, head = "Stone" },
                           new { who = "Sprout", hex = "#9ccc65", act = 28, p = new Vector3(388.5f, 0, 45f), yaw = 90f, head = "GrassBlock" },
                           new { who = "Woolly", hex = "#eeeeee", act = 16, p = new Vector3(411.0f, 0, 48.5f), yaw = 180f, head = "CaneWhite" } };
        foreach (var f in folk)
        {
            var face = Quaternion.Euler(0, f.yaw, 0) * Vector3.forward;
            GagSkeleton(g, f.who, f.p, f.yaw, 1f, f.act, f.hex, "Lab/lb_" + f.who.ToLower(), new[] { BoneMat(f.who, f.hex) }, f.p + new Vector3(0, 2.3f, 0) + face * 0.1f, face);
            CubeHead(f.who, f.head);
        }
        report.Add("Blocklands: " + blocks.Count + " blocks to mine, " + ghosts.Count + " places to build, 4 villagers");
    }

    static void BuildLabGags(Transform root)
    {
        var g = Group(root, "Lab Gags", Vector3.zero);
        int placed = 0;
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/lab_gags.txt");
        if (ta == null) { report.Add("problem: Data/lab_gags.txt is missing"); return; }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var raw in ta.text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Trim() == "" || line.StartsWith("#")) continue;
            var p = line.Split('|');
            if (p.Length < 10) continue;
            Gag(g, p[0], p[1], new Vector3(float.Parse(p[2], inv), float.Parse(p[3], inv), float.Parse(p[4], inv)), float.Parse(p[5], inv),
                p[7], p[8].Replace("\\n", "\n"), p[9].Replace("\\n", "\n"));
            placed++;
        }
        report.Add(placed + " gags in the Test Annex and Blocklands");
    }

    // =================================================================== Pixel Hollow (a retro pixel-RPG zone), reached from the Station

    static void PixelSkeleton(Transform parent, string who, string hex, int act, Vector3 pos, float yaw, float scale = 1f)
    {
        var face = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        GagSkeleton(parent, who, pos, yaw, scale, act, hex, "Pixel/px_" + who.ToLower(), new[] { BoneMat(who, hex) },
                    pos + new Vector3(0, 2.15f * scale, 0) + face * 0.1f, face);
    }

    /// <summary>A blocky pixel tree: a brown trunk and stepped green cubes.</summary>
    static void PixelTree(Transform g, Vector3 at, float s)
    {
        DPrim(PrimitiveType.Cube, "PixelTrunk", g, at + new Vector3(0, 0.5f * s, 0), new Vector3(0.4f, 1f, 0.4f) * s, M("Trunk"));
        for (int k = 0; k < 3; k++)
            DPrim(PrimitiveType.Cube, "PixelLeaves", g, at + new Vector3(0, (1.2f + k * 0.6f) * s, 0), new Vector3(1.8f - k * 0.5f, 0.6f, 1.8f - k * 0.5f) * s, M(k % 2 == 0 ? "PixelGreen" : "PixelGreenDark"));
    }

    /// <summary>A pixel-RPG encounter across the path at z: a fence with a gap, the monster in the gap, and the battle menu.</summary>
    static FirEncounter Encounter(Transform g, string name, float z, string mat, string intro, string[] options, int[] correct, string[] replies,
                                  string bonk, string snack, string notYet, string spare)
    {
        var e = Group(g, "Encounter " + name, Vector3.zero);
        // the fence (you can't get round)
        Box(e, "FenceW", new Vector3(269.25f, 0.75f, z), new Vector3(18.5f, 1.5f, 0.3f), M("TrimDark"));
        Box(e, "FenceE", new Vector3(290.75f, 0.75f, z), new Vector3(18.5f, 1.5f, 0.3f), M("TrimDark"));
        for (float fx = 260.5f; fx < 300f; fx += 1.5f) if (Mathf.Abs(fx - 280f) > 1.6f) DPrim(PrimitiveType.Cube, "FencePost", e, new Vector3(fx, 0.9f, z), new Vector3(0.2f, 1.8f, 0.35f), M("Trunk"));
        var blocker = new GameObject("Blocker");
        blocker.transform.SetParent(e, false);
        blocker.transform.localPosition = new Vector3(280f, 1.5f, z);
        blocker.AddComponent<BoxCollider>().size = new Vector3(3f, 3f, 0.6f);
        // the monster: a chunky, cute, blocky thing with big eyes
        var mon = Group(e, name, new Vector3(280f, 0, z));
        Prim(PrimitiveType.Cube, "Body", mon, new Vector3(0, 0.55f, 0), new Vector3(1.1f, 1.1f, 0.9f), M(mat));
        foreach (float ex in new[] { -0.22f, 0.22f })
        {
            Prim(PrimitiveType.Cube, "Eye", mon, new Vector3(ex, 0.75f, -0.46f), new Vector3(0.24f, 0.28f, 0.04f), M("CaneWhite"));
            Prim(PrimitiveType.Cube, "Pupil", mon, new Vector3(ex, 0.72f, -0.49f), new Vector3(0.11f, 0.14f, 0.02f), M("Black"));
        }
        Prim(PrimitiveType.Cube, "Mouth", mon, new Vector3(0, 0.42f, -0.46f), new Vector3(0.3f, 0.06f, 0.03f), M("Black"));
        // the battle menu: a dialog box over the path, the buttons on a stand to the side
        Box(e, "DialogBox", new Vector3(280f, 3.0f, z + 0.05f), new Vector3(5.6f, 1.3f, 0.06f), M("Black"), false);
        var frame = Box(e, "DialogFrame", new Vector3(280f, 3.0f, z + 0.09f), new Vector3(5.8f, 1.5f, 0.02f), M("CaneWhite"), false);
        var dialog = Text("Dialog", e, new Vector3(280f, 3.0f, z - 0.0f), Face(Vector3.back), new Vector2(5.3f, 1.15f), 0.16f, "");
        dialog.color = Color.white; dialog.alignment = TextAlignmentOptions.TopLeft;
        var stand = Group(e, "Menu", new Vector3(284.6f, 1.25f, z - 1.4f));
        stand.localRotation = Face(Vector3.back);
        Box(stand, "MenuBoard", new Vector3(0, 0, 0.04f), new Vector3(2.6f, 1.5f, 0.04f), M("Black"), false);
        var meter = Text("Meter", stand, new Vector3(0, 0.6f, -0.01f), Quaternion.identity, new Vector2(2.4f, 0.2f), 0.12f, "");
        meter.color = new Color(1f, 0.6f, 0.7f);
        var labels = new TextMeshPro[3];
        for (int i = 0; i < 3; i++)
        {
            var b = HB(stand, "Chat " + i, new Vector3(0, 0.3f - i * 0.28f, 0f), Vector3.back, new Vector3(2.3f, 0.22f, 0.05f), M("ChatOrange"), 22, i, "Say this", null, 0.09f, true);
            labels[i] = Text("Option", b.press, new Vector3(0, 0, -0.03f), Quaternion.identity, new Vector2(2.2f, 0.2f), 0.09f, "");
            labels[i].color = Color.white;
        }
        string[] verbs = { "BONK", "SNACK", "PARDON" };
        int[] acts = { 21, 23, 24 };
        for (int i = 0; i < 3; i++)
            HB(stand, verbs[i], new Vector3(-0.8f + i * 0.8f, -0.58f, 0f), Vector3.back, new Vector3(0.7f, 0.22f, 0.05f), M(i == 2 ? "ButtonGold" : "ChatOrange"), acts[i], 0, verbs[i], "<b>" + verbs[i] + "</b>", 0.1f, true);
        var enc = Add<FirEncounter>(e.gameObject);
        enc.monsterName = name.ToUpper(); enc.intro = intro; enc.options = options; enc.correct = correct; enc.replies = replies;
        enc.bonkText = bonk; enc.snackText = snack; enc.notYetText = notYet; enc.spareText = spare;
        enc.dialog = dialog; enc.optionLabels = labels; enc.meter = meter; enc.blocker = blocker; enc.monster = mon; enc.stepAside = new Vector3(2.4f, 0, 0.4f);
        enc.sfx = Src(e, new Vector3(280f, 1.5f, z), null, false, 1f, 1f, 12f, false);
        enc.blipClip = Clip("tree_note"); enc.goodClip = Clip("pickup"); enc.badClip = Clip("wrong"); enc.friendClip = Clip("music_box");
        return enc;
    }

    static void BuildPixel(Transform root)
    {
        var g = Group(root, "Pixel Hollow", Vector3.zero);
        int b0 = huntButtons.Count;
        float h = 10f;
        Box(g, "Ground", new Vector3(280f, -0.1f, 52f), new Vector3(40f, 0.2f, 64f), M("PixelGreen"));
        Box(g, "Sky", new Vector3(280f, h + 0.1f, 52f), new Vector3(40f, 0.2f, 64f), M("Sky"));
        Box(g, "WallW", new Vector3(259.9f, h / 2, 52f), new Vector3(0.2f, h, 64.4f), M("PixelSky"));
        Box(g, "WallE", new Vector3(300.1f, h / 2, 52f), new Vector3(0.2f, h, 64.4f), M("PixelSky"));
        Box(g, "WallS", new Vector3(280f, h / 2, 19.9f), new Vector3(40.4f, h, 0.2f), M("PixelSky"));
        Box(g, "WallN", new Vector3(280f, h / 2, 84.1f), new Vector3(40.4f, h, 0.2f), M("PixelSky"));
        // checkerboard grass and a sandy path up the middle
        for (int ix = 0; ix < 10; ix++) for (int iz = 0; iz < 16; iz++)
            if ((ix + iz) % 2 == 0) DPrim(PrimitiveType.Cube, "Check", g, new Vector3(262f + ix * 4f, 0.004f, 22f + iz * 4f), new Vector3(4f, 0.008f, 4f), M("PixelGreenDark"));
        Box(g, "Path", new Vector3(280f, 0.01f, 52f), new Vector3(3f, 0.012f, 64f), M("PixelSand"), false);
        for (int k = 0; k < 7; k++) DPrim(PrimitiveType.Cube, "Cloud", g, new Vector3(264f + k * 5.5f, 8.5f + (k % 3) * 0.4f, 30f + (k * 13) % 50), new Vector3(3f, 0.8f, 1.5f), M("CaneWhite"));
        foreach (var tp in new[] { new Vector3(264f, 0, 46f), new Vector3(296f, 0, 47f), new Vector3(265f, 0, 56f), new Vector3(295f, 0, 57f), new Vector3(266f, 0, 68f),
                                   new Vector3(294f, 0, 69f), new Vector3(264f, 0, 77f), new Vector3(296f, 0, 78f), new Vector3(271f, 0, 49f), new Vector3(289f, 0, 50f) })
            PixelTree(g, tp, 1.4f);
        var arrive = Marker(g, "Arrive", new Vector3(280f, 0.05f, 22f), 0f);
        StationStop("PIXEL HOLLOW", arrive, g, new Vector3(277.3f, 1.3f, 20.15f), Vector3.forward);
        Text("Title", g, new Vector3(280f, 6.5f, 20.05f), Face(Vector3.forward), new Vector2(14f, 1.2f), 0.9f, "<b>PIXEL HOLLOW</b>").color = new Color(1f, 0.9f, 0.3f);
        Text("Sub", g, new Vector3(280f, 5.6f, 20.05f), Face(Vector3.forward), new Vector2(14f, 0.4f), 0.22f, "a tiny adventure. you don't have to fight anyone. (please don't fight anyone.)").color = Color.white;

        // ---- the village: an inn, a shop, folk, a save point
        foreach (var hs in new[] { new { x = 266f, title = "INN", col = "PixelRoof" }, new { x = 294f, title = "SHOP", col = "PixelRoofBlue" } })
        {
            var hg = Group(g, "House " + hs.title, new Vector3(hs.x, 0, 30f));
            Box(hg, "Walls", new Vector3(0, 1.5f, 0), new Vector3(5f, 3f, 4f), M("PixelSand"));
            Box(hg, "Roof", new Vector3(0, 3.4f, 0), new Vector3(5.6f, 0.8f, 4.6f), M(hs.col), false);
            Box(hg, "Roof2", new Vector3(0, 4.0f, 0), new Vector3(3.6f, 0.6f, 4.6f), M(hs.col), false);
            DPrim(PrimitiveType.Cube, "Door", hg, new Vector3(hs.x < 280 ? 2.51f : -2.51f, 1.0f, 0), new Vector3(0.02f, 2f, 1.1f), M("TrimDark"));
            Text("Sign", hg, new Vector3(hs.x < 280 ? 2.53f : -2.53f, 2.5f, 0), Face(hs.x < 280 ? Vector3.right : Vector3.left), new Vector2(2f, 0.4f), 0.3f, "<b>" + hs.title + "</b>").color = new Color(1f, 0.9f, 0.3f);
        }
        PixelSkeleton(g, "Fizz", "#ffa726", 28, new Vector3(270f, 0, 27f), 90f);
        PixelSkeleton(g, "Pebble", "#8d6e63", 20, new Vector3(290f, 0, 27f), -90f);
        PixelSkeleton(g, "Nib", "#4fc3f7", 23, new Vector3(274.5f, 0, 36f), 90f, 0.65f);
        PixelSkeleton(g, "Brindle", "#bdbdbd", 26, new Vector3(285.5f, 0, 36f), -90f);
        var savePt = Group(g, "Save Point", new Vector3(284f, 0, 25.5f));
        var star = Group(savePt, "Star", new Vector3(0, 1.2f, 0));
        Prim(PrimitiveType.Cube, "StarA", star, Vector3.zero, new Vector3(0.45f, 0.45f, 0.08f), M("GlowYellow")).transform.localRotation = Quaternion.Euler(0, 0, 45);
        Prim(PrimitiveType.Cube, "StarB", star, Vector3.zero, new Vector3(0.45f, 0.45f, 0.08f), M("GlowYellow"));
        savePt.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.8f, 2.4f, 0.8f);
        savePt.gameObject.GetComponent<BoxCollider>().center = new Vector3(0, 1.2f, 0);
        var saveCap = Text("SaveCaption", savePt, new Vector3(0, 2.1f, 0), Face(Vector3.back), new Vector2(3.4f, 0.5f), 0.13f, "");
        saveCap.color = new Color(1f, 1f, 0.75f);
        var say = Add<FirSay>(savePt.gameObject);
        say.line = "* The sparkle twinkles. Your bones feel very sturdy.\n* Progress saved. Spiritually."; say.caption = saveCap; say.spin = star;
        say.voice = Src(savePt, new Vector3(0, 1.2f, 0), Clip("Pixel/px_save"), false, 1f, 1f, 10f, false);
        SetInteract(say, "Touch the sparkle", 2.5f);

        // ---- three encounters up the path, with the tile gate between the first and second
        var e1 = Encounter(g, "Shy Pinecone", 44f, "PixelPine", "* It's hiding behind its own scales.",
            new[] { "Compliment its scales", "Ask about the weather", "Stare silently",
                    "Ask its name", "Tell a pine joke", "Offer a high five",
                    "Say goodbye", "Ask to be friends", "Point at a squirrel" },
            new[] { 0, 1, 1 },
            new[] { "* PINECONE: ...you think they're nice? I polished them myself.", "* PINECONE: The weather is. Um. Present.", "* PINECONE hides deeper. You both feel awkward.",
                    "* PINECONE: It's... Pinecone. I'm a pinecone.", "* You: why did the pine go to school? To get a little sappier.\n* PINECONE giggles. A scale falls off. It doesn't mind.", "* PINECONE has no hands. It stares at yours, enviously.",
                    "* PINECONE: ...you're leaving already?", "* PINECONE: F-friends? Okay. OKAY!", "* PINECONE screams and rolls round in a circle." },
            "* You bonk the PINECONE. It's covered in spikes. Ow.", "* PINECONE: I'm on a seed diet. I AM the seed diet.",
            "* PINECONE isn't ready to let you by. It doesn't know you yet.", "* PINECONE rolls aside, blushing (somehow).");
        var tg = Group(g, "Tile Gate", Vector3.zero);
        Box(tg, "FenceW", new Vector3(269.25f, 0.75f, 54f), new Vector3(18.5f, 1.5f, 0.3f), M("TrimDark"));
        Box(tg, "FenceE", new Vector3(290.75f, 0.75f, 54f), new Vector3(18.5f, 1.5f, 0.3f), M("TrimDark"));
        var gate = Box(tg, "Gate", new Vector3(280f, 1.2f, 54f), new Vector3(3f, 2.4f, 0.25f), M("PixelRoofBlue"));
        var panel = Group(tg, "Tiles", new Vector3(284.4f, 1.5f, 53.8f));
        panel.localRotation = Face(Vector3.back);
        Box(panel, "Backing", new Vector3(0, 0, 0.05f), new Vector3(1.6f, 1.6f, 0.04f), M("Black"), false);
        var tiles = new MeshRenderer[9];
        for (int i = 0; i < 9; i++)
        {
            var b = HB(panel, "Tile " + i, new Vector3(-0.48f + (i % 3) * 0.48f, 0.48f - (i / 3) * 0.48f, 0f), Vector3.back, new Vector3(0.42f, 0.42f, 0.05f), M("TileOff"), 31, i, "Flip this tile", null, 0.1f, true);
            tiles[i] = b.press.Find("Visual").GetComponent<MeshRenderer>();
        }
        HB(panel, "Start over", new Vector3(0, -0.95f, 0f), Vector3.back, new Vector3(0.8f, 0.2f, 0.05f), M("Button"), 32, 0, "Start the tiles over", "<b>START OVER</b>", 0.08f, true);
        var tb = Text("TileBoard", tg, new Vector3(280f, 3.0f, 53.84f), Face(Vector3.back), new Vector2(5f, 0.8f), 0.16f, "");
        tb.color = Color.white;
        var ft = Add<FirTiles>(tg.gameObject);
        ft.tiles = tiles; ft.offMat = M("TileOff"); ft.onMat = M("TileOn"); ft.gate = gate; ft.board = tb;
        ft.startPressed = new[] { 0, 4, 8 };
        ft.sfx = Src(tg, new Vector3(280f, 1.5f, 54f), null, false, 1f, 1f, 12f, false);
        ft.flipClip = Clip("switch"); ft.solvedClip = Clip("unlock");
        var e2 = Encounter(g, "Grumbling Stump", 64f, "Trunk", "* It grumbles about kids these days.",
            new[] { "Agree kids are terrible", "Ask about the old days", "Sit on it",
                    "Count its rings", "Say it looks young", "Ask where its top half went",
                    "Wish it a happy birthday", "Plant a seed next to it", "Ask for directions" },
            new[] { 1, 0, 1 },
            new[] { "* STUMP: Don't encourage me.", "* STUMP: Ah, the old days! I was a tree! Tall! Leafy! Then the woodcutter came.", "* STUMP: Oh, sure. Sit on the stump. Everybody does.",
                    "* You count 312 rings.\n* STUMP: Three hundred and twelve! Somebody finally noticed!", "* STUMP: Flattery. Hmph. ...go on.", "* STUMP: We don't talk about that.",
                    "* STUMP: It's not my birthday. ...is it?", "* STUMP: A sapling? For me? ...don't look at me, I've got sap in my eye.", "* STUMP: Down the path. Past me. Which you can't. Ha." },
            "* You bonk the STUMP. It's a stump. It's fine. You're not.", "* STUMP: No teeth. Never had teeth. Thanks, though.",
            "* STUMP: Not so fast. I don't even know you.", "* STUMP shuffles aside on its roots, grumbling. Fondly.");
        var e3 = Encounter(g, "Nervous Lamp", 74f, "GlowGhost", "* Its light flickers nervously. It's a ghost. And a lamp.",
            new[] { "Turn it off and on again", "Tell it it's bright", "Ask if it's haunted",
                    "Tell a ghost story", "Hum a lullaby", "Shine your own light",
                    "Ask what scares it", "Say the trees are coming", "Promise to visit" },
            new[] { 1, 1, 2 },
            new[] { "* LAMP: AAAH! Don't do that! That's how I got like this!", "* LAMP: Y-you think so? I'm only forty watts.", "* LAMP: I'm a lamp AND a ghost. It's complicated.",
                    "* LAMP flickers wildly. That made it worse.", "* LAMP's flicker slows down. It hums along. Badly.", "* You don't have a light. You're just pointing.",
                    "* LAMP: The dark. I'm a lamp. It's a whole thing.", "* LAMP: THE TREES?! *flickers violently*", "* LAMP: You'd... visit? Nobody visits a lamp." },
            "* You bonk the LAMP. The bulb goes out. Then back on. It's even more nervous now.", "* LAMP: I only eat electricity. And moths. Mostly moths.",
            "* LAMP is too nervous to move. Help it calm down first.", "* LAMP floats aside, glowing warmly. It'll leave a light on for you.");

        // ---- the castle at the end: the duo, their act, and a chest
        var cs = Group(g, "Castle", Vector3.zero);
        Box(cs, "CastleWall", new Vector3(280f, 2.5f, 83.6f), new Vector3(16f, 5f, 0.6f), M("Cardboard"), false);
        foreach (float tx in new[] { 272.5f, 287.5f }) Box(cs, "Tower", new Vector3(tx, 3.5f, 83.2f), new Vector3(2.2f, 7f, 1.4f), M("Cardboard"), false);
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Cube, "Crenel", cs, new Vector3(273.5f + k * 1.9f, 5.25f, 83.6f), new Vector3(0.9f, 0.5f, 0.6f), M("Cardboard"));
        Text("CastleSign", cs, new Vector3(280f, 4.2f, 83.28f), Face(Vector3.back), new Vector2(8f, 0.6f), 0.4f, "<b>CASTLE CARDBOARD</b>").color = new Color(0.3f, 0.2f, 0.1f);
        PixelSkeleton(cs, "Rattleton", "#cfd8dc", 20, new Vector3(281.2f, 0, 80f), 180f, 0.7f);
        PixelSkeleton(cs, "Lumen", "#fff176", 4, new Vector3(278.8f, 0, 80f), 180f, 1.25f);
        if (rigs.TryGetValue("Rattleton", out var rr))
        {
            Prim(PrimitiveType.Cylinder, "Saucepan", rr[2], new Vector3(0, 0.13f, 0), new Vector3(0.26f, 0.08f, 0.26f), M("Metal"));
            Prim(PrimitiveType.Cube, "PanHandle", rr[2], new Vector3(0.22f, 0.16f, 0), new Vector3(0.22f, 0.03f, 0.04f), M("Black"));
            Prim(PrimitiveType.Cube, "CardboardArmour", rr[1], new Vector3(0, 0.2f, 0), new Vector3(0.42f, 0.45f, 0.3f), M("Cardboard"));
            Prim(PrimitiveType.Cube, "WoodenSword", rr[6], new Vector3(0, -0.2f, 0.25f), new Vector3(0.04f, 0.04f, 0.6f), M("DeskWood"));
        }
        if (rigs.TryGetValue("Lumen", out var lr))
        {
            Prim(PrimitiveType.Cylinder, "KnitHat", lr[2], new Vector3(0, 0.14f, 0), new Vector3(0.24f, 0.09f, 0.24f), M("CaneRed"));
            Prim(PrimitiveType.Sphere, "Bobble", lr[2], new Vector3(0, 0.25f, 0), Vector3.one * 0.08f, M("CaneWhite"));
            Prim(PrimitiveType.Cylinder, "Raincoat", lr[1], new Vector3(0, 0.15f, 0), new Vector3(0.42f, 0.3f, 0.32f), M("GlowYellow"));
            Prim(PrimitiveType.Sphere, "Lantern", lr[6], new Vector3(0, -0.3f, 0.05f), Vector3.one * 0.14f, M("GlowWarm"));
        }
        var console = Console(cs, "DuoConsole", new Vector3(285.6f, 1.25f, 77.6f), Vector3.back, "<size=80%>COMBO ACT</size>");
        ComboAct(console, 0, 6, "The Quest", new[] { "Rattleton", "Lumen" }, new[] { "quest_1", "quest_2", "quest_3", "quest_4", "quest_5" }, new[] { 0, 1, 0, 1, -1 }, 1.6f);
        var chest = Group(cs, "Chest", new Vector3(275.5f, 0, 79.5f));
        Box(chest, "Box", new Vector3(0, 0.3f, 0), new Vector3(1.0f, 0.6f, 0.7f), M("TrimDark"));
        Prim(PrimitiveType.Cube, "Lid", chest, new Vector3(0, 0.65f, 0), new Vector3(1.05f, 0.12f, 0.75f), M("GiftGold"));
        var chestCap = Text("ChestCaption", chest, new Vector3(0, 1.5f, 0), Face(Vector3.back), new Vector2(3.6f, 0.6f), 0.13f, "");
        chestCap.color = new Color(1f, 1f, 0.75f);
        var cSay = Add<FirSay>(chest.gameObject);
        cSay.line = "* You open the chest. Inside, a smaller chest.\n* Inside that, a note: \"nice try.\""; cSay.caption = chestCap; cSay.showFor = 7f;
        cSay.voice = Src(chest, new Vector3(0, 0.6f, 0), Clip("Pixel/px_chest"), false, 1f, 1f, 10f, false);
        SetInteract(cSay, "Open the chest", 2.5f);

        foreach (var b in huntButtons.Skip(b0))
        {
            // each button belongs to the nearest encounter, or the tile gate
            var p = b.transform.position;
            if (Mathf.Abs(p.z - 53.8f) < 1.5f) b.hunt = UB(ft);
            else b.hunt = UB(new[] { e1, e2, e3 }.OrderBy(x => Mathf.Abs(x.blocker.transform.position.z - p.z)).First());
        }

        // ---- the village's gags (Data/pixel_gags.txt)
        int placed = 0;
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/pixel_gags.txt");
        if (ta == null) report.Add("problem: Data/pixel_gags.txt is missing");
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
        report.Add("Pixel Hollow: 3 encounters, a tile gate, 6 skeletons (the duo has a combo act), " + placed + " gags, a save point and a chest");
    }

    // =================================================================== Pine Pete's Pizza Palace (night-shift horror), reached from the Station

    static readonly string[] VoicemailLines = {
        "Hello? Hello, hello! Welcome to your first night at Pine Pete's Pizza Palace.",
        "I used to work this shift. Now I'm a skeleton. Unrelated! Probably.",
        "Anyway. The trees get a bit restless at night. They like to wander.",
        "Pete comes down the west side. Holly comes down the east. Sprucey, who knows.",
        "If one of them shows up at your door, shut it. Don't leave it shut all night, though. The power is on a budget.",
        "Check the cameras. Check the lights. And whatever you do, don't let Pete in.",
        "He'll want to give you a hug. A very, very pointy hug.",
        "Okay! Good luck! Have a pizza!" };

    static void PizzaSkeleton(Transform parent, string who, string hex, int act, Vector3 pos, float yaw)
    {
        var face = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        GagSkeleton(parent, who, pos, yaw, 1f, act, hex, "Pizza/pz_" + who.ToLower(), new[] { BoneMat(who, hex) }, pos + new Vector3(0, 2.15f, 0) + face * 0.1f, face);
    }

    /// <summary>An animatronic tree mascot: the jagged fir with glowing red eyes, and its costume bits.</summary>
    static Transform Animatronic(Transform parent, string name, float height, string costume)
    {
        var root = Group(parent, name, Vector3.zero);
        Transform mouth;
        var head = TreeModel(root, height, false, out mouth);
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) if (r.name.StartsWith("Pupil")) r.sharedMaterial = M("GlowRedEye");
        float top = height * 0.98f;
        if (costume == "chef")
        {
            Prim(PrimitiveType.Cylinder, "ChefHat", root, new Vector3(0, top + 0.12f, 0), new Vector3(0.45f, 0.2f, 0.45f), M("CaneWhite"));
            Prim(PrimitiveType.Sphere, "ChefPuff", root, new Vector3(0, top + 0.38f, 0), new Vector3(0.6f, 0.35f, 0.6f), M("CaneWhite"));
            Prim(PrimitiveType.Cylinder, "Pizza", root, new Vector3(0.45f, height * 0.45f, 0.35f), new Vector3(0.6f, 0.02f, 0.6f), M("GlowOrange")).transform.localRotation = Quaternion.Euler(70, 0, 0);
        }
        else if (costume == "bow")
        {
            foreach (float bx in new[] { -0.14f, 0.14f }) Prim(PrimitiveType.Sphere, "Bow", root, new Vector3(bx, top - 0.05f, 0.1f), new Vector3(0.26f, 0.18f, 0.1f), M("CaneRed"));
            Prim(PrimitiveType.Sphere, "Knot", root, new Vector3(0, top - 0.05f, 0.12f), Vector3.one * 0.1f, M("CaneRed"));
            Prim(PrimitiveType.Cylinder, "Microphone", root, new Vector3(-0.4f, height * 0.45f, 0.35f), new Vector3(0.06f, 0.2f, 0.06f), M("Black"));
        }
        else
        {
            Prim(PrimitiveType.Cylinder, "Drum", root, new Vector3(0, height * 0.32f, 0.4f), new Vector3(0.5f, 0.18f, 0.5f), M("GiftBlue")).transform.localRotation = Quaternion.Euler(80, 0, 0);
        }
        return root;
    }

    static Transform BigFace(Transform parent, string costume)
    {
        var f = ScareFace(parent);
        if (costume == "chef") Prim(PrimitiveType.Cylinder, "ChefHat", f, new Vector3(0, 0.95f, 0.35f), new Vector3(0.9f, 0.3f, 0.9f), M("CaneWhite"));
        if (costume == "bow") foreach (float bx in new[] { -0.3f, 0.3f }) Prim(PrimitiveType.Sphere, "Bow", f, new Vector3(bx, 0.85f, 0.2f), new Vector3(0.5f, 0.35f, 0.15f), M("CaneRed"));
        if (costume == "drum") Prim(PrimitiveType.Cylinder, "Drum", f, new Vector3(0, -0.8f, 0.1f), new Vector3(0.9f, 0.25f, 0.9f), M("GiftBlue")).transform.localRotation = Quaternion.Euler(80, 0, 0);
        return f;
    }

    static void BuildPizzeria(Transform root)
    {
        var g = Group(root, "Pine Petes", Vector3.zero);
        float h = 4f;
        int b0 = huntButtons.Count;
        // ---- the shell: x 200-240, z 20-60
        Box(g, "Floor", new Vector3(220f, -0.1f, 40f), new Vector3(40f, 0.2f, 40f), M("Floor"));
        Box(g, "Ceiling", new Vector3(220f, h + 0.1f, 40f), new Vector3(40f, 0.2f, 40f), M("Ceiling"));
        Box(g, "WallW", new Vector3(199.9f, h / 2, 40f), new Vector3(0.2f, h, 40.4f), M("WallDark"));
        Box(g, "WallE", new Vector3(240.1f, h / 2, 40f), new Vector3(0.2f, h, 40.4f), M("WallDark"));
        Box(g, "WallS", new Vector3(220f, h / 2, 19.9f), new Vector3(40.4f, h, 0.2f), M("WallDark"));
        Box(g, "WallN", new Vector3(220f, h / 2, 60.1f), new Vector3(40.4f, h, 0.2f), M("WallDark"));
        var arrive = Marker(g, "Arrive", new Vector3(220f, 0.05f, 22f), 0f);
        StationStop("PINE PETE'S PIZZA", arrive, g, new Vector3(217.3f, 1.3f, 20.15f), Vector3.forward);
        Text("Name", g, new Vector3(220f, 3.4f, 20.05f), Face(Vector3.forward), new Vector2(12f, 0.7f), 0.5f, "<b>PINE PETE'S PIZZA PALACE</b>").color = new Color(1f, 0.6f, 0.1f);
        Text("Sub", g, new Vector3(220f, 2.85f, 20.05f), Face(Vector3.forward), new Vector2(12f, 0.35f), 0.18f, "where the fun never stops (we tried. it won't stop.)").color = new Color(0.9f, 0.9f, 0.9f);
        // walls of the back: the stage's back wall, the parts room, the kitchen, the halls and the office
        Action<string, float, float, float, float> WallX = (n, x, za, zb, t) => Box(g, n, new Vector3(x, h / 2, (za + zb) / 2f), new Vector3(t, h, Mathf.Abs(zb - za)), M("WallDark"));
        Action<string, float, float, float, float> WallZ = (n, z, xa, xb, t) => Box(g, n, new Vector3((xa + xb) / 2f, h / 2, z), new Vector3(Mathf.Abs(xb - xa), h, t), M("WallDark"));
        WallZ("StageBack", 45f, 210f, 230f, 0.2f);
        WallX("PartsE", 210f, 36f, 45f, 0.2f);  WallZ("PartsS L", 36f, 200f, 204f, 0.2f); WallZ("PartsS R", 36f, 206.4f, 210f, 0.2f);
        WallZ("PartsN L", 45f, 200f, 203.7f, 0.2f); WallZ("PartsN R", 45f, 206.3f, 210f, 0.2f);
        WallX("KitchenW", 230f, 36f, 45f, 0.2f); WallZ("KitchenS L", 36f, 230f, 233.6f, 0.2f); WallZ("KitchenS R", 36f, 236f, 240f, 0.2f);
        WallZ("KitchenN L", 45f, 230f, 233.7f, 0.2f); WallZ("KitchenN R", 45f, 236.3f, 240f, 0.2f);
        WallX("WestHallW", 203.7f, 45f, 55.5f, 0.2f); WallX("WestHallE", 206.3f, 45f, 52.9f, 0.2f);
        WallZ("WestHallN", 55.5f, 203.7f, 216f, 0.2f); WallZ("WestHallS", 52.9f, 206.3f, 216f, 0.2f);
        WallX("EastHallE", 236.3f, 45f, 55.5f, 0.2f); WallX("EastHallW", 233.7f, 45f, 52.9f, 0.2f);
        WallZ("EastHallN", 55.5f, 224f, 236.3f, 0.2f); WallZ("EastHallS", 52.9f, 224f, 233.7f, 0.2f);
        WallZ("OfficeS", 51f, 216f, 224f, 0.2f); WallZ("OfficeN", 58f, 216f, 224f, 0.2f);
        WallX("OfficeW S", 216f, 51f, 53f, 0.2f); WallX("OfficeW N", 216f, 55.4f, 58f, 0.2f);
        WallX("OfficeE S", 224f, 51f, 53f, 0.2f); WallX("OfficeE N", 224f, 55.4f, 58f, 0.2f);
        foreach (float dx in new[] { 216f, 224f }) Box(g, "OfficeDoorTop", new Vector3(dx, 3.3f, 54.2f), new Vector3(0.2f, 1.4f, 2.4f), M("WallDark"));
        Text("HallSignW", g, new Vector3(205f, 3.2f, 45.12f), Face(Vector3.back), new Vector2(2.4f, 0.3f), 0.16f, "<b>STAFF ONLY</b>").color = new Color(1f, 0.3f, 0.3f);
        Text("HallSignE", g, new Vector3(235f, 3.2f, 45.12f), Face(Vector3.back), new Vector2(2.4f, 0.3f), 0.16f, "<b>STAFF ONLY</b>").color = new Color(1f, 0.3f, 0.3f);

        // ---- the stage, the dining room, the arcade, the counter, the party table
        Box(g, "Stage", new Vector3(220f, 0.25f, 43.5f), new Vector3(16f, 0.5f, 3f), M("Stage"));
        Box(g, "Curtain", new Vector3(220f, 2.2f, 44.85f), new Vector3(16f, 3.4f, 0.1f), M("Curtain"), false);
        Text("StageSign", g, new Vector3(220f, 3.7f, 44.78f), Face(Vector3.back), new Vector2(10f, 0.45f), 0.32f, "<b>PINE PETE & FRIENDS!</b>").color = new Color(1f, 0.85f, 0.3f);
        for (float fx = 212.5f; fx <= 227.6f; fx += 0.5f)
            GameObjectUtility.SetStaticEditorFlags(Prim(PrimitiveType.Sphere, "StageBulb", g, new Vector3(fx, 0.52f, 42.02f), Vector3.one * 0.08f, Bulb(((int)(fx * 2)) % 2 == 0 ? "BulbWarm" : "BulbRed")), StaticEditorFlags.BatchingStatic);
        foreach (float tz in new[] { 26.6f, 29.4f }) foreach (float tx in new[] { 213f, 217f, 223f, 227f })
        {
            Box(g, "Table", new Vector3(tx, 0.38f, tz), new Vector3(1.6f, 0.76f, 1.0f), M("CaneWhite"));
            DPrim(PrimitiveType.Cylinder, "Pizza", g, new Vector3(tx, 0.78f, tz), new Vector3(0.55f, 0.015f, 0.55f), M("GlowOrange"));
            DPrim(PrimitiveType.Cylinder, "PartyHat", g, new Vector3(tx + 0.5f, 0.9f, tz + 0.2f), new Vector3(0.12f, 0.12f, 0.12f), M(tx < 220 ? "GiftBlue" : "CaneRed"));
        }
        Box(g, "PartyTable", new Vector3(220f, 0.38f, 38.6f), new Vector3(1.8f, 0.76f, 1.0f), M("GiftGold"));
        DPrim(PrimitiveType.Cylinder, "Cake", g, new Vector3(220f, 0.9f, 38.6f), new Vector3(0.5f, 0.14f, 0.5f), M("BonePink"));
        Box(g, "Counter", new Vector3(226f, 0.55f, 24.5f), new Vector3(4f, 1.1f, 0.8f), M("CaneRed"));
        for (int k = 0; k < 5; k++)
        {
            Box(g, "Arcade", new Vector3(239.2f, 0.9f, 24.8f + k * 1.9f), new Vector3(0.9f, 1.8f, 0.9f), M(k % 2 == 0 ? "GiftBlue" : "BoneViolet"));
            DPrim(PrimitiveType.Cube, "ArcadeScreen", g, new Vector3(238.74f, 1.3f, 24.8f + k * 1.9f), new Vector3(0.01f, 0.45f, 0.6f), M("Water"));
        }
        Box(g, "PartsTable", new Vector3(203f, 0.45f, 40f), new Vector3(2f, 0.9f, 1f), M("Metal"));
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Sphere, "SpareHead", g, new Vector3(202.3f + k * 0.45f, 1.05f, 40f), Vector3.one * 0.3f, M("ScareGreen"));
        Text("PartsSign", g, new Vector3(205f, 3.1f, 36.12f), Face(Vector3.back), new Vector2(3f, 0.35f), 0.18f, "<b>PARTS & SERVICE</b>").color = Color.white;
        Box(g, "KitchenCounter", new Vector3(239.2f, 0.45f, 43.0f), new Vector3(1.0f, 0.9f, 3f), M("Metal"));
        Box(g, "Oven", new Vector3(239.2f, 0.8f, 39.0f), new Vector3(1.0f, 1.6f, 1.6f), M("Black"));
        DPrim(PrimitiveType.Cube, "OvenGlow", g, new Vector3(238.68f, 0.7f, 39.0f), new Vector3(0.01f, 0.5f, 1.0f), M("GlowOrange"));
        Text("KitchenSign", g, new Vector3(235f, 3.1f, 36.12f), Face(Vector3.back), new Vector2(3f, 0.35f), 0.18f, "<b>KITCHEN</b>").color = Color.white;
        foreach (float lx in new[] { 208f, 220f, 232f }) foreach (float lz in new[] { 25f, 32f }) PendantLamp(g, new Vector3(lx, h, lz), 0.7f);

        // ---- the people (day staff and guests)
        PizzaSkeleton(g, "Basil", "#7cb342", 28, new Vector3(238.0f, 0, 43.0f), -90f);
        PizzaSkeleton(g, "Pepper", "#e53935", 20, new Vector3(226f, 0, 25.6f), 180f);
        PizzaSkeleton(g, "Candle", "#fff59d", 23, new Vector3(218.6f, 0, 38.6f), 90f);
        PizzaSkeleton(g, "Frosting", "#f8bbd0", 16, new Vector3(221.4f, 0, 38.6f), -90f);
        PizzaSkeleton(g, "Gary", "#90a4ae", 21, new Vector3(201.6f, 0, 43.4f), 90f);
        PizzaSkeleton(g, "Tokens", "#ffd54f", 27, new Vector3(237.4f, 0, 35.0f), -90f);
        var wg = Group(g, "Wanderers", Vector3.zero);
        MakeWanderer(wg, "Mop", "#4dd0e1", new[] { new Vector3(208f, 0, 24f), new Vector3(208f, 0, 32f), new Vector3(232f, 0, 32f), new Vector3(232f, 0, 24f) },
                     new[] { 1f, 1f, 1f, 1f }, 0.6f, 0, "Pizza/pz_mop", "I mop every night. Every morning there are footprints. Big, rooty footprints.");

        // ---- the night: the office, its doors, lights, cameras and power
        var nightGo = Group(g, "Night", new Vector3(220f, 1.5f, 54.5f));
        var night = Add<FirNight>(nightGo.gameObject);
        night.officeMin = new Vector3(216f, -1f, 51f); night.officeMax = new Vector3(224f, 5f, 58f);
        night.petePerHour = new[] { 3, 5, 7, 9, 11, 13 };
        night.hollyPerHour = new[] { 2, 4, 6, 8, 10, 12 };
        night.sprucePerHour = new[] { 0, 3, 5, 7, 9, 11 };
        night.pete = Animatronic(g, "Pine Pete", 2.4f, "chef");
        night.holly = Animatronic(g, "Holly", 2.2f, "bow");
        night.sprucey = Animatronic(g, "Sprucey", 1.6f, "drum");
        Func<string, Vector3[], float[], Transform[]> Spots = (n, ps, ys) => ps.Select((p, i) => Marker(g, n + " " + i, p, ys[i])).ToArray();
        night.peteSpots = Spots("Pete spot", new[] { new Vector3(216f, 0.5f, 43.5f), new Vector3(214f, 0, 33.5f), new Vector3(204.5f, 0, 41.5f), new Vector3(205f, 0, 49f), new Vector3(214.9f, 0, 54.2f) }, new[] { 180f, 0f, 0f, 0f, 90f });
        night.hollySpots = Spots("Holly spot", new[] { new Vector3(224f, 0.5f, 43.5f), new Vector3(226f, 0, 33.5f), new Vector3(235.5f, 0, 41.5f), new Vector3(235f, 0, 49f), new Vector3(225.1f, 0, 54.2f) }, new[] { 180f, 0f, 0f, 0f, -90f });
        night.spruceSpotsW = Spots("Sprucey west", new[] { new Vector3(220f, 0.5f, 43.5f), new Vector3(220f, 0, 32.5f), new Vector3(207.5f, 0, 38.5f), new Vector3(205f, 0, 51.5f), new Vector3(214.7f, 0, 53.4f) }, new[] { 180f, 0f, 0f, 0f, 90f });
        night.spruceSpotsE = Spots("Sprucey east", new[] { new Vector3(220f, 0.5f, 43.5f), new Vector3(220f, 0, 32.5f), new Vector3(232.5f, 0, 38.5f), new Vector3(235f, 0, 51.5f), new Vector3(225.3f, 0, 53.4f) }, new[] { 180f, 0f, 0f, 0f, -90f });
        night.camNames = new[] { "SHOW STAGE", "DINING ROOM", "PARTS & SERVICE", "KITCHEN", "WEST HALL", "EAST HALL" };
        // doors (slide up into the ceiling when open), the dark in each doorway, the door lights
        foreach (int side in new[] { 0, 1 })
        {
            float dx = side == 0 ? 216f : 224f, sgn = side == 0 ? -1f : 1f;
            var panel = Box(g, side == 0 ? "DoorL" : "DoorR", new Vector3(dx, 1.3f, 54.2f), new Vector3(0.15f, 2.6f, 2.4f), M("Metal"));
            Prim(PrimitiveType.Cube, "Stripes", panel.transform, new Vector3(0, -0.3f, 0), new Vector3(1.05f, 0.08f, 1.02f), M("GlowYellow"));
            var shadow = Prim(PrimitiveType.Cube, side == 0 ? "ShadowL" : "ShadowR", g, new Vector3(dx + sgn * 0.9f, 1.3f, 54.2f), new Vector3(1.5f, 2.6f, 2.35f), M("Black"));
            var lamp = Prim(PrimitiveType.Sphere, side == 0 ? "LampL" : "LampR", g, new Vector3(dx + sgn * 0.9f, 2.7f, 54.2f), Vector3.one * 0.3f, M("GlowWarm"));
            if (side == 0) { night.doorPanelL = panel.transform; night.shadowL = shadow; night.lampL = lamp; }
            else { night.doorPanelR = panel.transform; night.shadowR = shadow; night.lampR = lamp; }
            var nrm = side == 0 ? Vector3.right : Vector3.left;
            HB(g, side == 0 ? "Door L button" : "Door R button", new Vector3(dx - sgn * 0.12f, 1.5f, 52.3f), nrm, new Vector3(0.45f, 0.3f, 0.1f), M("CaneRed"), 7, side, "Door", "<b>DOOR</b>", 0.1f, true);
            HB(g, side == 0 ? "Light L button" : "Light R button", new Vector3(dx - sgn * 0.12f, 1.0f, 52.3f), nrm, new Vector3(0.45f, 0.3f, 0.1f), M("ButtonGold"), 8, side, "Light", "<b>LIGHT</b>", 0.1f, true);
        }
        // the desk, the monitor and its camera buttons, the clock, the power, the phone
        Box(g, "Desk", new Vector3(220f, 0.4f, 56.6f), new Vector3(3.2f, 0.8f, 1.2f), M("TrimDark"));
        Box(g, "Monitor", new Vector3(220f, 1.45f, 57.3f), new Vector3(1.6f, 1.0f, 0.1f), M("Black"));
        night.monitorText = Text("MonitorText", g, new Vector3(220f, 1.5f, 57.24f), Face(Vector3.back), new Vector2(1.45f, 0.8f), 0.11f, "");
        night.monitorText.color = new Color(0.6f, 1f, 0.6f);
        for (int c = 1; c <= 6; c++)
            HB(g, "Cam " + c, new Vector3(218.6f + (c - 1) * 0.42f, 0.98f, 55.97f), Vector3.back, new Vector3(0.36f, 0.3f, 0.06f), M("Button"), 9, c, "Camera " + c, "<b>CAM\n" + c + "</b>", 0.09f, true);
        Box(g, "ClockBoard", new Vector3(220f, 3.0f, 57.88f), new Vector3(4.6f, 1.2f, 0.04f), M("Board"), false);
        night.clockText = Text("Clock", g, new Vector3(218.3f, 3.1f, 57.84f), Face(Vector3.back), new Vector2(1.4f, 0.6f), 0.45f, "");
        night.clockText.color = Color.white;
        night.powerText = Text("Power", g, new Vector3(221.6f, 3.05f, 57.84f), Face(Vector3.back), new Vector2(1.8f, 0.8f), 0.2f, "");
        night.powerText.color = new Color(0.6f, 1f, 0.6f);
        Box(g, "StatusBoard", new Vector3(220f, 2.4f, 51.12f), new Vector3(4.4f, 1.0f, 0.04f), M("Board"), false);
        night.statusText = Text("Status", g, new Vector3(220f, 2.4f, 51.16f), Face(Vector3.forward), new Vector2(4.2f, 0.9f), 0.15f, "");
        night.statusText.color = Color.white;
        HB(g, "START", new Vector3(218.7f, 1.4f, 51.2f), Vector3.forward, new Vector3(0.9f, 0.35f, 0.1f), M("ButtonGold"), 1, 0, "Start the night shift", "<b>START</b>", 0.2f, true);
        HB(g, "RESET", new Vector3(221.3f, 1.4f, 51.2f), Vector3.forward, new Vector3(0.7f, 0.3f, 0.1f), M("Button"), 2, 0, "End the night", "<b>RESET</b>", 0.13f, true);
        var phoneGo = HB(g, "Phone", new Vector3(221.25f, 0.98f, 55.97f), Vector3.back, new Vector3(0.5f, 0.3f, 0.06f), M("CaneRed"), 10, 0, "Play the voicemail", "<b>VOICE\nMAIL (1)</b>", 0.08f, true);
        night.phone = Src(phoneGo.transform, Vector3.zero, Clip("Pizza/pz_voicemail"), false, 0.6f, 1f, 8f, false);
        night.captionText = Text("Captions", g, new Vector3(220f, 2.2f, 57.84f), Face(Vector3.back), new Vector2(4.4f, 0.4f), 0.12f, "");
        night.captionText.color = new Color(1f, 1f, 0.75f);
        // the voicemail's captions, timed by how much each line has to say
        float total = night.phone.clip != null ? night.phone.clip.length : 45f, chars = VoicemailLines.Sum(l => l.Length), at = 0.3f;
        var times = new List<float>();
        foreach (var l in VoicemailLines) { times.Add(at); at += (total - 0.6f) * l.Length / chars; }
        night.phoneCaptions = VoicemailLines; night.phoneCaptionAt = times.ToArray();
        night.sfx = Src(nightGo, Vector3.zero, null, false, 0f, 1f, 6f, false);
        night.doorClip = Clip("door_bang"); night.lightClip = Clip("static"); night.chimeClip = Clip("music_box"); night.screamClip = Clip("scream2");
        night.stepClip = Clip("servo"); night.powerOutClip = Clip("tree_sing_wrong"); night.knockClip = Clip("creak"); night.startClip = Clip("phone");
        night.scareFaces = new[] { BigFace(nightGo, "chef"), BigFace(nightGo, "bow"), BigFace(nightGo, "drum") };
        var me = UB(night);
        foreach (var b in huntButtons.Skip(b0)) b.hunt = me;
        Text("OfficeSign", g, new Vector3(220f, 3.5f, 51.12f), Face(Vector3.forward), new Vector2(4f, 0.4f), 0.22f, "<b>SECURITY</b>").color = new Color(1f, 0.3f, 0.3f);

        // ---- the pizzeria's gags (Data/pizza_gags.txt)
        int placed = 0;
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/pizza_gags.txt");
        if (ta == null) report.Add("problem: Data/pizza_gags.txt is missing");
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
        report.Add("Pine Pete's Pizza Palace: 3 animatronics, " + (huntButtons.Count - b0) + " office controls, 7 staff and guests, " + placed + " gags, a " + total.ToString("F0") + " s voicemail");
    }

    // =================================================================== Precinct 206 (deadpan cop comedy), reached from the Station

    static void CopSkeleton(Transform parent, string who, string hex, int act, Vector3 pos, float yaw, string title)
    {
        var face = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        GagSkeleton(parent, who, pos, yaw, 1f, act, hex, "Precinct/pc_" + who.ToLower(), new[] { BoneMat(who, hex) },
                    pos + new Vector3(0, 2.15f, 0) + face * 0.1f, face, "<color=" + hex + ">" + title + "</color>");
    }

    /// <summary>A police-blue cap on a skeleton's skull (by name, from the rigs the skeleton builder recorded).</summary>
    static void CopCap(string who)
    {
        if (!rigs.TryGetValue(who, out var r)) return;
        var skull = r[2];
        Prim(PrimitiveType.Cylinder, "Cap", skull, new Vector3(0, 0.15f, 0.0f), new Vector3(0.27f, 0.05f, 0.27f), M("PoliceBlue"));
        Prim(PrimitiveType.Cube, "Peak", skull, new Vector3(0, 0.12f, 0.12f), new Vector3(0.2f, 0.02f, 0.12f), M("Black"));
        Prim(PrimitiveType.Cube, "Badge", skull, new Vector3(0, 0.17f, 0.13f), new Vector3(0.05f, 0.05f, 0.01f), M("Brass"));
    }

    static void Desk(Transform g, Vector3 at)
    {
        Box(g, "Desk", at + new Vector3(0, 0.38f, 0), new Vector3(1.6f, 0.76f, 0.8f), M("DeskWood"));
        DPrim(PrimitiveType.Cube, "Monitor", g, at + new Vector3(-0.3f, 0.98f, 0.15f), new Vector3(0.5f, 0.38f, 0.05f), M("Black"));
        DPrim(PrimitiveType.Cube, "Screen", g, at + new Vector3(-0.3f, 0.98f, 0.12f), new Vector3(0.44f, 0.32f, 0.01f), M("Water"));
        DPrim(PrimitiveType.Cube, "Papers", g, at + new Vector3(0.4f, 0.78f, -0.05f), new Vector3(0.35f, 0.04f, 0.28f), M("Paper"));
        DPrim(PrimitiveType.Cylinder, "Mug", g, at + new Vector3(0.65f, 0.83f, 0.2f), new Vector3(0.09f, 0.06f, 0.09f), M("CaneWhite"));
        DPrim(PrimitiveType.Cube, "Chair", g, at + new Vector3(0, 0.25f, -0.75f), new Vector3(0.5f, 0.5f, 0.5f), M("Black"));
    }

    static void BuildPrecinct(Transform root)
    {
        var g = Group(root, "Precinct 206", Vector3.zero);
        float x0 = 140f, x1 = 176f, z0 = 20f, z1 = 56f, h = 4.2f, cx = 158f, cz = 38f;
        Box(g, "Floor", new Vector3(cx, -0.1f, cz), new Vector3(36f, 0.2f, 36f), M("Floor2"));
        Box(g, "Ceiling", new Vector3(cx, h + 0.1f, cz), new Vector3(36f, 0.2f, 36f), M("Ceiling"));
        Box(g, "WallW", new Vector3(x0 - 0.1f, h / 2, cz), new Vector3(0.2f, h, 36.4f), M("Wall2"));
        Box(g, "WallE", new Vector3(x1 + 0.1f, h / 2, cz), new Vector3(0.2f, h, 36.4f), M("Wall2"));
        Box(g, "WallN", new Vector3(cx, h / 2, z1 + 0.1f), new Vector3(36.4f, h, 0.2f), M("Wall2"));
        Box(g, "WallS", new Vector3(cx, h / 2, z0 - 0.1f), new Vector3(36.4f, h, 0.2f), M("Wall2"));
        Trim(g, "Precinct", z1, z0, 36f, h, float.NaN, float.NaN, true);
        foreach (float lz in new[] { 25f, 33f, 41f, 50f }) foreach (float lx in new[] { 146f, 158f, 170f }) PendantLamp(g, new Vector3(lx, h, lz), 0.7f);
        var arrive = Marker(g, "Arrive", new Vector3(158f, 0.05f, 21.5f), 0f);
        StationStop("PRECINCT 206", arrive, g, new Vector3(155.3f, 1.3f, 20.15f), Vector3.forward);
        Text("PrecinctName", g, new Vector3(158f, 3.5f, 20.05f), Face(Vector3.forward), new Vector2(10f, 0.6f), 0.45f, "<b>POLICE  PRECINCT 206</b>").color = new Color(0.2f, 0.3f, 0.7f);
        Text("Motto", g, new Vector3(158f, 3.0f, 20.05f), Face(Vector3.forward), new Vector2(10f, 0.35f), 0.17f, "<i>\"To Protect and To Serve. Mostly To Serve. Coffee.\"</i>").color = new Color(0.25f, 0.25f, 0.3f);

        // ---- the front desk
        Box(g, "FrontDesk", new Vector3(158f, 0.55f, 26f), new Vector3(12f, 1.1f, 0.8f), M("DeskWood"));
        Text("DeskSign", g, new Vector3(158f, 1.3f, 25.58f), Face(Vector3.back), new Vector2(3f, 0.3f), 0.18f, "<b>FRONT DESK</b>  <size=70%>all complaints to the desk (it doesn't mind)</size>").color = Color.white;
        DPrim(PrimitiveType.Cube, "Bell", g, new Vector3(156.5f, 1.15f, 25.9f), new Vector3(0.12f, 0.06f, 0.12f), M("Brass"));
        CopSkeleton(g, "Marrow", "#c0c8d8", 26, new Vector3(158f, 0, 27.2f), 180f, "SGT. MARROW");

        // ---- the bullpen: desks in two rows, an aisle up the middle
        foreach (float dz in new[] { 33f, 39f }) foreach (float dx in new[] { 145f, 150.5f, 165.5f, 171f }) Desk(g, new Vector3(dx, 0, dz));
        CopSkeleton(g, "Clavicle", "#9fb4c7", 3, new Vector3(145f, 0, 33.9f), 180f, "OFFICER CLAVICLE");
        CopSkeleton(g, "Tibia", "#7f9ccf", 27, new Vector3(150.5f, 0, 39.9f), 180f, "OFFICER TIBIA");
        CopSkeleton(g, "Fibula", "#a7c7e7", 5, new Vector3(165.5f, 0, 33.9f), 180f, "OFFICER FIBULA");
        CopSkeleton(g, "Humerus", "#5b7db1", 15, new Vector3(171f, 0, 39.9f), 180f, "OFFICER HUMERUS");
        DPrim(PrimitiveType.Cylinder, "Banana", g, new Vector3(150.9f, 0.82f, 39.1f), new Vector3(0.05f, 0.12f, 0.05f), M("GlowYellow"), Quaternion.Euler(0, 0, 70));

        // ---- the line-up: height lines, and five identical trees
        for (int k = 0; k <= 8; k++)
        {
            float y = 0.3f + k * 0.3f;
            DPrim(PrimitiveType.Cube, "HeightLine", g, new Vector3(140.02f, y, 34f), new Vector3(0.01f, 0.02f, 11f), M("CaneWhite"));
            Text("Height", g, new Vector3(140.03f, y + 0.08f, 28.7f), Face(Vector3.right), new Vector2(0.4f, 0.15f), 0.1f, (y * 3.28f).ToString("F0") + " ft").color = Color.white;
        }
        for (int k = 0; k < 5; k++)
        {
            TreeAt(g, "Suspect " + (k + 1), new Vector3(141.2f, 0, 30f + k * 2f), 90f, 1.9f, false);
            Text("Number", g, new Vector3(141.75f, 0.45f, 30f + k * 2f), Face(Vector3.right), new Vector2(0.4f, 0.3f), 0.25f, "<b>" + (k + 1) + "</b>").color = Color.white;
        }
        Text("LineupSign", g, new Vector3(140.03f, 3.3f, 34f), Face(Vector3.right), new Vector2(6f, 0.4f), 0.25f, "<b>THE LINE-UP</b>  <size=70%>(witnesses: please pick the tree)</size>").color = new Color(1f, 0.85f, 0.4f);

        // ---- the interrogation room (north-west): the suspect is a vending machine
        Box(g, "IntWallS L", new Vector3(143f, h / 2, 46f), new Vector3(6f, h, 0.2f), M("WallDark"));
        Box(g, "IntWallS R", new Vector3(149.2f, h / 2, 46f), new Vector3(1.6f, h, 0.2f), M("WallDark"));
        Box(g, "IntWallS Top", new Vector3(147.2f, 3.5f, 46f), new Vector3(2.4f, 1.4f, 0.2f), M("WallDark"));
        Box(g, "IntWallE", new Vector3(150f, h / 2, 51f), new Vector3(0.2f, h, 10f), M("WallDark"));
        Box(g, "OneWayMirror", new Vector3(150.12f, 1.8f, 51f), new Vector3(0.02f, 1.2f, 3f), M("Water"), false);
        Text("MirrorSign", g, new Vector3(150.15f, 2.65f, 51f), Face(Vector3.right), new Vector2(3f, 0.3f), 0.14f, "ONE-WAY MIRROR <size=70%>(the other way is a wall)</size>").color = Color.white;
        Box(g, "IntTable", new Vector3(145f, 0.38f, 51f), new Vector3(1.4f, 0.76f, 0.9f), M("Metal"));
        PendantLamp(g, new Vector3(145f, h, 51f), 1.6f);
        Box(g, "VendingMachine", new Vector3(143f, 0.9f, 51f), new Vector3(0.9f, 1.8f, 0.8f), M("CaneRed"));
        DPrim(PrimitiveType.Cube, "VendGlass", g, new Vector3(143.46f, 1.1f, 51f), new Vector3(0.01f, 1.1f, 0.6f), M("Water"));
        CopSkeleton(g, "Femur", "#4a5a7a", 19, new Vector3(147f, 0, 51f), -90f, "DET. FEMUR");
        Text("IntSign", g, new Vector3(147.2f, 2.95f, 45.88f), Face(Vector3.back), new Vector2(2.4f, 0.3f), 0.15f, "<b>INTERROGATION</b>").color = Color.white;

        // ---- the chief's office (north-east)
        Box(g, "ChiefWallS L", new Vector3(167.4f, h / 2, 46f), new Vector3(2.8f, h, 0.2f), M("Wainscot"));
        Box(g, "ChiefWallS R", new Vector3(173.6f, h / 2, 46f), new Vector3(4.8f, h, 0.2f), M("Wainscot"));
        Box(g, "ChiefWallS Top", new Vector3(170f, 3.5f, 46f), new Vector3(2.4f, 1.4f, 0.2f), M("Wainscot"));
        Box(g, "ChiefWallW", new Vector3(166f, h / 2, 51f), new Vector3(0.2f, h, 10f), M("Wainscot"));
        Box(g, "ChiefDesk", new Vector3(171f, 0.4f, 51.2f), new Vector3(2.2f, 0.8f, 1.0f), M("TrimDark"));
        DPrim(PrimitiveType.Cube, "Nameplate", g, new Vector3(171f, 0.88f, 50.75f), new Vector3(0.5f, 0.12f, 0.05f), M("Brass"));
        Text("ChiefSign", g, new Vector3(170f, 2.95f, 45.88f), Face(Vector3.back), new Vector2(2.4f, 0.3f), 0.15f, "<b>CHIEF</b>  <size=70%>(knock first. then wait. then knock again.)</size>").color = Color.white;
        CopSkeleton(g, "Sternum", "#1f2f5f", 20, new Vector3(171f, 0, 52.4f), 180f, "CAPT. STERNUM");

        // ---- the briefing room (between them): chairs, a whiteboard, a podium
        Box(g, "Whiteboard", new Vector3(158f, 3.05f, 55.95f), new Vector3(4.6f, 1.3f, 0.04f), M("CaneWhite"), false);
        Text("Briefing", g, new Vector3(158f, 3.05f, 55.92f), Face(Vector3.back), new Vector2(4.4f, 1.2f), 0.15f,
             "<color=#202020><b>TODAY'S BRIEFING</b>\n1. The trees are back.\n2. Nobody touch the evidence. It's still warm.\n3. Who keeps drawing a moustache on the suspect? (It's a tree.)</color>");
        Box(g, "Podium", new Vector3(158f, 0.55f, 53.6f), new Vector3(0.8f, 1.1f, 0.5f), M("TrimDark"));
        CopSkeleton(g, "Patella", "#6f86b6", 4, new Vector3(158f, 0, 54.6f), 180f, "LT. PATELLA");
        for (int r = 0; r < 2; r++) for (int k = 0; k < 4; k++)
            DPrim(PrimitiveType.Cube, "BriefChair", g, new Vector3(154.6f + k * 2.2f, 0.25f, 49.4f + r * 1.6f), new Vector3(0.5f, 0.5f, 0.5f), M("Black"));

        // ---- the evidence locker (east wall) and the holding cell (south-east)
        for (int k = 0; k < 3; k++)
        {
            Box(g, "Shelf", new Vector3(175.6f, 0.5f + k * 0.8f, 40f), new Vector3(0.7f, 0.05f, 7.6f), M("Metal"), false);
            for (int b = 0; b < 6; b++) DPrim(PrimitiveType.Cube, "EvidenceBox", g, new Vector3(175.6f, 0.75f + k * 0.8f, 36.8f + b * 1.25f), new Vector3(0.5f, 0.4f, 0.6f), M("Cardboard"), Quaternion.Euler(0, b * 9f, 0));
        }
        Text("EvidenceSign", g, new Vector3(175.9f, 3.0f, 40f), Face(Vector3.left), new Vector2(5f, 0.35f), 0.2f, "<b>EVIDENCE</b>  <size=70%>(do not eat)</size>").color = new Color(1f, 0.85f, 0.4f);
        CopSkeleton(g, "Scapula", "#8899aa", 21, new Vector3(174.4f, 0, 37.6f), -90f, "INSP. SCAPULA");
        for (float bz = 20.25f; bz < 28f; bz += 0.3f) DPrim(PrimitiveType.Cylinder, "Bar", g, new Vector3(168f, 1.4f, bz), new Vector3(0.05f, 1.4f, 0.05f), M("Metal"));
        for (float bx = 168f; bx < 176f; bx += 0.3f) DPrim(PrimitiveType.Cylinder, "Bar", g, new Vector3(bx, 1.4f, 28f), new Vector3(0.05f, 1.4f, 0.05f), M("Metal"));
        foreach (var bc in new[] { new { p = new Vector3(168f, 1.4f, 24f), sz = new Vector3(0.1f, 2.8f, 8f) }, new { p = new Vector3(172f, 1.4f, 28f), sz = new Vector3(8f, 2.8f, 0.1f) } })
        {
            var bars = new GameObject("CellBars");   // the bars' collider (the bars themselves are thin decorations)
            bars.transform.SetParent(g, false);
            bars.transform.localPosition = bc.p;
            bars.AddComponent<BoxCollider>().size = bc.sz;
        }
        Text("CellSign", g, new Vector3(167.9f, 3.0f, 24f), Face(Vector3.left), new Vector2(4f, 0.35f), 0.2f, "<b>HOLDING</b>  <size=70%>(hold it)</size>").color = Color.white;
        CopSkeleton(g, "Coccyx", "#b0c4de", 23, new Vector3(166.4f, 0, 24f), -90f, "CADET COCCYX");

        // ---- the tail, and the hot pursuit (wanderers)
        var wg = Group(g, "Wanderers", Vector3.zero);
        var aisle = new[] { new Vector3(154.5f, 0, 30f), new Vector3(154.5f, 0, 43f), new Vector3(161.5f, 0, 43f), new Vector3(161.5f, 0, 30f) };
        MakeWanderer(wg, "Ulna", "#7a8fb5", aisle, new[] { 1f, 1f, 1f, 1f }, 0.9f, 2500, "Precinct/pc_ulna", "I think somebody is following me. It's probably nothing. It's probably Radius.");
        MakeWanderer(wg, "Radius", "#3e5487", aisle, new[] { 1f, 1f, 1f, 1f }, 0.9f, 0, "Precinct/pc_radius", "I'm tailing a suspect. Don't look. Don't look! You looked.");
        var desk = Enumerable.Range(0, 12).Select(k => new Vector3(171f + Mathf.Cos(k * Mathf.PI / 6f) * 1.75f, 0, 33f + Mathf.Sin(k * Mathf.PI / 6f) * 1.75f)).ToArray();
        MakeWanderer(wg, "Pelvis", "#c9d6ea", desk, Enumerable.Repeat(2.6f, 12).ToArray(), 1.1f, 1200, "Precinct/pc_pelvis", "Can't stop! I don't even know what I did!");
        MakeWanderer(wg, "Vertebra", "#2b3f6b", desk, Enumerable.Repeat(2.6f, 12).ToArray(), 1.1f, 0, "Precinct/pc_vertebra", "Hot pursuit! I've been chasing him round this desk since Tuesday!");
        foreach (var w in new[] { "Marrow", "Clavicle", "Tibia", "Fibula", "Humerus", "Femur", "Sternum", "Patella", "Scapula", "Coccyx" }) CopCap(w);

        // ---- the precinct's gags (Data/precinct_gags.txt)
        int placed = 0;
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/precinct_gags.txt");
        if (ta == null) report.Add("problem: Data/precinct_gags.txt is missing");
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
        int cops = g.GetComponentsInChildren<FirRainbowSkeleton>(true).Length + g.GetComponentsInChildren<FirWanderer>(true).Length;
        report.Add("Precinct 206: " + cops + " skeleton cops, " + placed + " gags");
    }

    // =================================================================== Boneanza Park (the amusement park), reached from the lobby's Station

    static Transform stationBoard;      // the lobby's Station: one button per far-away area
    static int stationSlots;
    static Transform lobbyArrive;

    /// <summary>The Station: a board in the lobby with a teleport button for each far-off area (and its way back).</summary>
    static void BuildStation(Transform lobby, Transform spawn)
    {
        lobbyArrive = spawn;
        stationSlots = 0;
        var g = Group(lobby, "Station", new Vector3(-6.2f, 0, -1.0f));
        g.localRotation = Face(Vector3.right);
        Box(g, "Post", new Vector3(0, 0.6f, 0.05f), new Vector3(0.1f, 1.2f, 0.1f), M("Black"), false);
        Box(g, "Board", new Vector3(0, 1.75f, 0.05f), new Vector3(2.0f, 1.5f, 0.06f), M("Board"));
        Text("Title", g, new Vector3(0, 2.35f, -0.01f), Quaternion.identity, new Vector2(1.9f, 0.3f), 0.2f, "<b>THE STATION</b>").color = new Color(1f, 0.85f, 0.4f);
        Text("Sub", g, new Vector3(0, 2.12f, -0.01f), Quaternion.identity, new Vector2(1.9f, 0.2f), 0.09f, "press to go; each place has a way back").color = new Color(0.8f, 0.8f, 0.8f);
        stationBoard = g;
    }

    /// <summary>Adds a destination to the Station, and a way back to the lobby at the destination.</summary>
    static void StationStop(string title, Transform arrive, Transform backParent, Vector3 backPos, Vector3 backNormal)
    {
        int i = stationSlots++;
        var b = TeleportButton(stationBoard, title, new Vector3(-0.48f + (i % 2) * 0.96f, 1.85f - (i / 2) * 0.3f, -0.03f), Vector3.back, arrive, new Vector3(0.88f, 0.24f, 0.06f));
        TeleportButton(backParent, "Back to the lobby", backPos, backNormal, lobbyArrive, new Vector3(1.0f, 0.3f, 0.1f));
    }

    static FirTeleport TeleportButton(Transform parent, string title, Vector3 pos, Vector3 normal, Transform dest, Vector3 size)
    {
        var go = new GameObject(title);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Face(normal);
        go.AddComponent<BoxCollider>().size = size;
        Prim(PrimitiveType.Cube, "Visual", go.transform, Vector3.zero, size, M("ButtonGold"));
        Text("Label", go.transform, new Vector3(0, 0, -size.z / 2 - 0.003f), Quaternion.identity, new Vector2(size.x * 0.95f, size.y * 0.9f), size.y * 0.42f, "<b>" + title + "</b>").color = new Color(0.15f, 0.1f, 0.05f);
        var t = Add<FirTeleport>(go);
        t.destination = dest;
        t.sfx = Src(go.transform, Vector3.zero, Clip("whoosh"), false, 0f, 0.8f, 5f, false);
        SetInteract(t, "Go: " + title, 3f);
        return t;
    }

    /// <summary>A ride seat: a VRCStation the ride carries (press to sit, jump to get off, landing at `exit`).</summary>
    static GameObject RideSeat(Transform parent, string name, Vector3 pos, float yaw, Transform exit)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.45f, 0); col.size = new Vector3(0.7f, 0.9f, 0.7f);
        var st = go.AddComponent<VRC.SDK3.Components.VRCStation>();
        st.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.ImmobilizeForVehicle;
        st.disableStationExit = false;
        st.canUseStationFromStation = false;
        st.stationEnterPlayerLocation = go.transform;
        st.stationExitPlayerLocation = exit;
        var seat = Add<FirSeat>(go);
        seat.station = st;
        SetInteract(seat, "Ride (jump to get off)", 2.5f);
        rideSeats++;
        return go;
    }
    static int rideSeats;

    static void ParkSkeleton(Transform parent, string who, string hex, int act, Vector3 pos, float yaw, float scale = 1f)
    {
        var face = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        GagSkeleton(parent, who, pos, yaw, scale, act, hex, "Park/pk_" + who.ToLower(), new[] { BoneMat(who, hex) },
                    pos + new Vector3(0, 2.15f * scale, 0) + face * 0.1f, face);
    }

    /// <summary>A jump scare that is always armed (a tree bursts out screaming as you pass, or ride past).</summary>
    static void ParkScare(Transform parent, string name, Vector3 at, float yaw, float reach, string clip, float zoneW = 2.6f)
    {
        var g = Group(parent, name, at);
        g.localRotation = Quaternion.Euler(0, yaw, 0);
        var tree = Group(g, "Tree", Vector3.zero);
        Transform mouth;
        TreeModel(tree, 2.3f, false, out mouth);
        foreach (var r in tree.GetComponentsInChildren<MeshRenderer>(true)) if (r.name.StartsWith("Pupil")) r.sharedMaterial = M("GlowRedEye");
        var sg = Add<FirGag>(g.gameObject);
        sg.target = tree; sg.hideWhenIdle = true; sg.walkIn = true; sg.scare = false;
        sg.zoneCenter = new Vector3(0, 1f, reach); sg.zoneSize = new Vector3(zoneW, 2.6f, zoneW);
        sg.moveBy = new Vector3(0, 0, Mathf.Min(1.2f, reach - 0.6f)); sg.scaleBy = 1.15f; sg.wobble = 6f;
        sg.inTime = 0.12f; sg.holdTime = 1.0f; sg.outTime = 0.45f;
        sg.sfx = Src(tree, new Vector3(0, 1.6f, 0.3f), Clip(clip), false, 1f, 1f, 16f, false);
        sg.mouth = mouth; sg.line = "";
        parkScares++;
    }
    static int parkScares;

    /// <summary>A stall: a counter facing yaw, posts, a striped awning and a name; returns the stall's group.</summary>
    static Transform Stall(Transform parent, string title, Vector3 pos, float yaw, string stripeA, string stripeB)
    {
        var g = Group(parent, "Stall " + title, pos);
        g.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(g, "Counter", new Vector3(0, 0.5f, 0.6f), new Vector3(2.4f, 1.0f, 0.6f), M("DeskWood"));
        foreach (float px in new[] { -1.15f, 1.15f })
        {
            Box(g, "Post", new Vector3(px, 1.4f, 0.85f), new Vector3(0.08f, 2.8f, 0.08f), M("CaneWhite"), false);
            Box(g, "Post", new Vector3(px, 1.4f, -0.9f), new Vector3(0.08f, 2.8f, 0.08f), M("CaneWhite"), false);
        }
        for (int k = 0; k < 6; k++)
            DPrim(PrimitiveType.Cube, "Awning", g, new Vector3(-1.05f + k * 0.42f, 2.75f, 0f), new Vector3(0.42f, 0.06f, 2.1f), M(k % 2 == 0 ? stripeA : stripeB), Quaternion.Euler(-12f, 0, 0));
        Text("Name", g, new Vector3(0, 2.45f, 0.93f), Face(Vector3.forward), new Vector2(2.3f, 0.35f), 0.2f, "<b>" + title + "</b>").color = new Color(1f, 0.9f, 0.4f);
        return g;
    }

    static void BuildPark(Transform root)
    {
        rideSeats = 0; parkScares = 0;
        var g = Group(root, "Boneanza Park", Vector3.zero);
        float x0 = 60f, x1 = 120f, z0 = 20f, z1 = 80f, h = 22f, cx = 90f, cz = 50f;
        Box(g, "Grass", new Vector3(cx, -0.1f, cz), new Vector3(60f, 0.2f, 60f), M("Grass"));
        Box(g, "Sky", new Vector3(cx, h + 0.1f, cz), new Vector3(60f, 0.2f, 60f), M("Sky"));
        Box(g, "WallW", new Vector3(x0 - 0.1f, h / 2, cz), new Vector3(0.2f, h, 60.4f), M("Sky"));
        Box(g, "WallE", new Vector3(x1 + 0.1f, h / 2, cz), new Vector3(0.2f, h, 60.4f), M("Sky"));
        Box(g, "WallN", new Vector3(cx, h / 2, z1 + 0.1f), new Vector3(60.4f, h, 0.2f), M("Sky"));
        Box(g, "WallS", new Vector3(cx, h / 2, z0 - 0.1f), new Vector3(60.4f, h, 0.2f), M("Sky"));
        // a striped fence round the edge, and paths
        for (float fx = x0 + 0.5f; fx < x1; fx += 1f)
        {
            DPrim(PrimitiveType.Cube, "Fence", g, new Vector3(fx, 0.5f, z0 + 0.2f), new Vector3(0.5f, 1.0f, 0.06f), M(((int)fx) % 2 == 0 ? "CaneRed" : "CaneWhite"));
            DPrim(PrimitiveType.Cube, "Fence", g, new Vector3(fx, 0.5f, z1 - 0.2f), new Vector3(0.5f, 1.0f, 0.06f), M(((int)fx) % 2 == 0 ? "CaneRed" : "CaneWhite"));
        }
        Box(g, "MainPath", new Vector3(90f, 0.005f, 40f), new Vector3(3f, 0.01f, 36f), M("Concrete"), false);
        Box(g, "CrossPath", new Vector3(90f, 0.005f, 58f), new Vector3(56f, 0.01f, 3f), M("Concrete"), false);
        Box(g, "LowerPath", new Vector3(92f, 0.005f, 28f), new Vector3(52f, 0.01f, 3f), M("Concrete"), false);
        // the gate arch (inside the south fence) with the park's name
        foreach (float gx in new[] { 86.5f, 93.5f }) Box(g, "GatePost", new Vector3(gx, 3f, 21.2f), new Vector3(0.5f, 6f, 0.5f), M("CaneRed"));
        Box(g, "GateTop", new Vector3(90f, 6.3f, 21.2f), new Vector3(7.6f, 0.8f, 0.4f), M("GiftGold"));
        Text("ParkName", g, new Vector3(90f, 6.3f, 21.42f), Face(Vector3.forward), new Vector2(7.2f, 0.7f), 0.55f, "<b>BONEANZA PARK</b>").color = new Color(0.8f, 0.05f, 0.1f);
        Text("ParkSub", g, new Vector3(90f, 5.4f, 21.45f), Face(Vector3.forward), new Vector2(7f, 0.4f), 0.2f, "rides! games! snacks that go right through you!").color = new Color(0.2f, 0.15f, 0.3f);
        for (int k = 0; k < 18; k++)
            GameObjectUtility.SetStaticEditorFlags(Prim(PrimitiveType.Sphere, "GateBulb", g, new Vector3(86.6f + k * 0.4f, 6.8f, 21.45f), Vector3.one * 0.12f, Bulb(k % 2 == 0 ? "BulbWarm" : "BulbRed")), StaticEditorFlags.BatchingStatic);
        var arrive = Marker(g, "Arrive", new Vector3(90f, 0.05f, 23.5f), 0f);
        StationStop("BONEANZA PARK", arrive, g, new Vector3(88.0f, 1.3f, 20.15f), Vector3.forward);

        // ---- the big wheel (rotor turns about z; gondolas stay level)
        var fw = Group(g, "Big Wheel", Vector3.zero);
        var hub = new Vector3(75f, 9.5f, 64f);
        foreach (float lz in new[] { -1.4f, 1.4f })
            foreach (float lx in new[] { -4f, 4f })
            {
                var foot = new Vector3(hub.x + lx, 0, hub.z + lz);
                var mid = (foot + hub) / 2f; var d = hub - foot;
                DPrim(PrimitiveType.Cylinder, "Leg", fw, mid, new Vector3(0.3f, d.magnitude / 2f, 0.3f), M("Metal"), Quaternion.FromToRotation(Vector3.up, d.normalized));
            }
        var wheel = Group(fw, "Rotor", hub);
        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f;
            DPrim(PrimitiveType.Cylinder, "Spoke", wheel, Quaternion.Euler(0, 0, a) * new Vector3(0, 3.75f, 0), new Vector3(0.15f, 3.75f, 0.15f), M("CaneWhite"), Quaternion.Euler(0, 0, a));
        }
        for (int k = 0; k < 32; k++)
        {
            float a = k * 360f / 32f;
            Prim(PrimitiveType.Cube, "Rim", wheel, Quaternion.Euler(0, 0, a) * new Vector3(0, 7.5f, 0), new Vector3(1.5f, 0.18f, 0.18f), M(k % 2 == 0 ? "CaneRed" : "GiftGold")).transform.localRotation = Quaternion.Euler(0, 0, a);
            Prim(PrimitiveType.Sphere, "RimBulb", wheel, Quaternion.Euler(0, 0, a) * new Vector3(0, 7.7f, 0), Vector3.one * 0.15f, Bulb(k % 3 == 0 ? "BulbRed" : k % 3 == 1 ? "BulbGold" : "BulbBlue"));
        }
        Prim(PrimitiveType.Cylinder, "Hub", wheel, Vector3.zero, new Vector3(1.2f, 0.4f, 1.2f), M("GiftGold")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        var wheelExit = Marker(fw, "Wheel exit", new Vector3(75f, 0.05f, 61.5f), 180f);
        var gondolas = new List<Transform>();
        string[] gcol = { "CaneRed", "GiftBlue", "GiftGreen", "GiftGold" };
        for (int k = 0; k < 8; k++)
        {
            var gp = Group(wheel, "Gondola " + k, Quaternion.Euler(0, 0, k * 45f) * new Vector3(0, -7.5f, 0));
            Box(gp, "Floor", new Vector3(0, -1.5f, 0), new Vector3(1.4f, 0.1f, 1.2f), M(gcol[k % 4]), false);
            Box(gp, "Roof", new Vector3(0, -0.05f, 0), new Vector3(1.5f, 0.1f, 1.3f), M(gcol[k % 4]), false);
            foreach (float px in new[] { -0.65f, 0.65f }) foreach (float pz in new[] { -0.55f, 0.55f })
                Box(gp, "Post", new Vector3(px, -0.75f, pz), new Vector3(0.06f, 1.4f, 0.06f), M("Metal"), false);
            DPrim(PrimitiveType.Cylinder, "Hanger", gp, new Vector3(0, 0.1f, 0), new Vector3(0.08f, 0.15f, 0.08f), M("Metal"));
            RideSeat(gp, "Wheel seat " + k, new Vector3(0, -1.45f, 0.2f), 180f, wheelExit);
            gondolas.Add(gp);
        }
        var loopRider = gondolas[4];
        ParkSkeleton(loopRider, "Loop", "#ff6ad5", 18, new Vector3(0, -1.45f, -0.25f), 180f, 0.7f);
        var fr = Add<FirRide>(fw.gameObject);
        fr.kind = 0; fr.rotor = wheel; fr.axis = Vector3.forward; fr.degPerSec = 6f; fr.upright = gondolas.ToArray();
        Text("WheelSign", g, new Vector3(75f, 2.6f, 60.2f), Face(Vector3.back), new Vector2(4f, 0.4f), 0.28f, "<b>THE BIG WHEEL</b>").color = new Color(1f, 0.9f, 0.4f);
        ParkSkeleton(g, "Gears", "#b0b8c0", 20, new Vector3(79.5f, 0, 59.5f), 180f);

        // ---- the carousel
        var car = Group(g, "Carousel", new Vector3(104f, 0, 64f));
        DPrim(PrimitiveType.Cylinder, "Pole", car, new Vector3(0, 2.5f, 0), new Vector3(0.5f, 2.5f, 0.5f), M("GiftGold"));
        var spin = Group(car, "Rotor", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "Platform", spin, new Vector3(0, 0.15f, 0), new Vector3(9f, 0.15f, 9f), M("DeskWood"));
        Prim(PrimitiveType.Cylinder, "Canopy", spin, new Vector3(0, 4.6f, 0), new Vector3(9.4f, 0.3f, 9.4f), M("CaneRed"));
        Prim(PrimitiveType.Cylinder, "Top", spin, new Vector3(0, 5.2f, 0), new Vector3(5f, 0.35f, 5f), M("CaneWhite"));
        var carExit = Marker(car, "Carousel exit", new Vector3(0, 0.05f, -5.4f), 180f);
        var horses = new List<Transform>();
        string[] hcol = { "CaneWhite", "GiftGold", "Book1", "GiftBlue" };
        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f * Mathf.Deg2Rad;
            var hp = Group(spin, "Horse " + k, new Vector3(Mathf.Cos(a) * 3.4f, 0.6f, Mathf.Sin(a) * 3.4f));
            hp.localRotation = Quaternion.Euler(0, -k * 45f, 0);
            DPrim(PrimitiveType.Cylinder, "HorsePole", spin, hp.localPosition + new Vector3(0, 1.8f, 0), new Vector3(0.06f, 2.0f, 0.06f), M("GiftGold"));
            Prim(PrimitiveType.Cube, "Body", hp, new Vector3(0, 0.5f, 0), new Vector3(0.35f, 0.4f, 1.0f), M(hcol[k % 4]));
            Prim(PrimitiveType.Cube, "Neck", hp, new Vector3(0, 0.85f, 0.45f), new Vector3(0.22f, 0.5f, 0.22f), M(hcol[k % 4])).transform.localRotation = Quaternion.Euler(25, 0, 0);
            Prim(PrimitiveType.Cube, "Head", hp, new Vector3(0, 1.1f, 0.65f), new Vector3(0.22f, 0.25f, 0.45f), M(hcol[k % 4]));
            foreach (float lx in new[] { -0.12f, 0.12f }) foreach (float lz in new[] { -0.35f, 0.35f })
                Prim(PrimitiveType.Cube, "Leg", hp, new Vector3(lx, 0.15f, lz), new Vector3(0.07f, 0.4f, 0.07f), M(hcol[k % 4])).transform.localRotation = Quaternion.Euler(lz * 60f, 0, 0);
            if (k != 2) RideSeat(hp, "Horse seat " + k, new Vector3(0, 0.55f, -0.1f), 0f, carExit);
            horses.Add(hp);
        }
        ParkSkeleton(horses[2], "Gallop", "#9b6bff", 31, new Vector3(0, 0.6f, -0.1f), 0f, 0.75f);
        var cr = Add<FirRide>(car.gameObject);
        cr.kind = 0; cr.rotor = spin; cr.axis = Vector3.up; cr.degPerSec = 18f; cr.bobbers = horses.ToArray(); cr.bobHeight = 0.25f; cr.bobSpeed = 1.6f;

        // ---- the teacups
        var tc = Group(g, "Teacups", new Vector3(92f, 0, 50f));
        var plat = Group(tc, "Rotor", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "Platform", plat, new Vector3(0, 0.08f, 0), new Vector3(8f, 0.08f, 8f), M("CaneWhite"));
        var tcExit = Marker(tc, "Teacups exit", new Vector3(0, 0.05f, -4.6f), 180f);
        var cups = new List<Transform>();
        string[] ccol = { "GiftBlue", "BonePink", "GiftGreen", "GiftGold" };
        for (int k = 0; k < 4; k++)
        {
            float a = (k * 90f + 45f) * Mathf.Deg2Rad;
            var cp = Group(plat, "Cup " + k, new Vector3(Mathf.Cos(a) * 2.3f, 0.15f, Mathf.Sin(a) * 2.3f));
            Prim(PrimitiveType.Cylinder, "Cup", cp, new Vector3(0, 0.35f, 0), new Vector3(1.6f, 0.35f, 1.6f), M(ccol[k]));
            Prim(PrimitiveType.Cylinder, "Saucer", cp, new Vector3(0, 0.03f, 0), new Vector3(2.0f, 0.03f, 2.0f), M("CaneWhite"));
            Prim(PrimitiveType.Cube, "Handle", cp, new Vector3(0.85f, 0.45f, 0), new Vector3(0.12f, 0.35f, 0.08f), M(ccol[k]));
            if (k != 1) RideSeat(cp, "Cup seat " + k, new Vector3(0, 0.2f, 0), 0f, tcExit);
            cups.Add(cp);
        }
        ParkSkeleton(cups[1], "Dizzy", "#7fffd4", 24, new Vector3(0, 0.2f, 0), 0f, 0.7f);
        var tr = Add<FirRide>(tc.gameObject);
        tr.kind = 0; tr.rotor = plat; tr.axis = Vector3.up; tr.degPerSec = 12f; tr.spinners = cups.ToArray(); tr.spinnerDegPerSec = 70f;
        ParkSkeleton(g, "Whirly", "#ff9f43", 29, new Vector3(92f, 0, 43.2f), 0f);

        // ---- the drop tower
        var dt = Group(g, "Drop Tower", new Vector3(108f, 0, 36f));
        DPrim(PrimitiveType.Cylinder, "Mast", dt, new Vector3(0, 9.5f, 0), new Vector3(1.0f, 9.5f, 1.0f), M("CaneRed"));
        DPrim(PrimitiveType.Cylinder, "Cap", dt, new Vector3(0, 19.2f, 0), new Vector3(1.6f, 0.3f, 1.6f), M("GiftGold"));
        Text("DropSign", dt, new Vector3(0, 17.5f, -0.52f), Face(Vector3.back), new Vector2(2.4f, 0.5f), 0.4f, "<b>DROP</b>").color = Color.white;
        var ring = Group(dt, "Ring", new Vector3(0, 0.3f, 0));
        Prim(PrimitiveType.Cylinder, "RingDisc", ring, new Vector3(0, 0.2f, 0), new Vector3(3.6f, 0.08f, 3.6f), M("Metal"));
        var dtExit = Marker(dt, "Drop exit", new Vector3(0, 0.05f, -3.0f), 180f);
        for (int k = 0; k < 4; k++)
        {
            float a = k * 90f;
            var seatPos = Quaternion.Euler(0, a, 0) * new Vector3(0, 0.25f, 1.45f);
            Prim(PrimitiveType.Cube, "SeatBack", ring, seatPos + Quaternion.Euler(0, a, 0) * new Vector3(0, 0.6f, -0.25f), new Vector3(0.6f, 1.0f, 0.1f), M("Black")).transform.localRotation = Quaternion.Euler(0, a, 0);
            RideSeat(ring, "Drop seat " + k, seatPos, a, dtExit);
        }
        var dr = Add<FirRide>(dt.gameObject);
        dr.kind = 2; dr.rotor = ring; dr.dropHeight = 12.5f; dr.upTime = 9f; dr.holdTime = 2.5f; dr.dropTime = 1.4f; dr.restTime = 8f;
        dr.sfx = Src(ring, new Vector3(0, 1.5f, 0), Clip("scream1"), false, 1f, 1f, 40f, false);
        ParkSkeleton(g, "Shriek", "#ff3b3b", 23, new Vector3(110.5f, 0, 33f), -90f);

        // ---- the ghost train: a dark shed, two carts on a loop through it, and things that jump out
        var gt = Group(g, "Ghost Train", Vector3.zero);
        Box(gt, "WallW", new Vector3(62f, 2.5f, 34f), new Vector3(0.3f, 5f, 16f), M("Black"));
        Box(gt, "WallN", new Vector3(71f, 2.5f, 42f), new Vector3(18f, 5f, 0.3f), M("Black"));
        Box(gt, "WallS", new Vector3(71f, 2.5f, 26f), new Vector3(18f, 5f, 0.3f), M("Black"));
        Box(gt, "WallE S", new Vector3(80f, 2.5f, 26.5f), new Vector3(0.3f, 5f, 1.0f), M("Black"));
        Box(gt, "WallE M", new Vector3(80f, 2.5f, 33f), new Vector3(0.3f, 5f, 8f), M("Black"));
        Box(gt, "WallE N", new Vector3(80f, 2.5f, 40.5f), new Vector3(0.3f, 5f, 3f), M("Black"));
        Box(gt, "WallE TopS", new Vector3(80f, 3.75f, 28f), new Vector3(0.3f, 2.5f, 2f), M("Black"));
        Box(gt, "WallE TopN", new Vector3(80f, 3.75f, 38f), new Vector3(0.3f, 2.5f, 2f), M("Black"));
        Box(gt, "Roof", new Vector3(71f, 5.1f, 34f), new Vector3(18.4f, 0.2f, 16.4f), M("Black"));
        Text("GhostSign", gt, new Vector3(80.2f, 4.0f, 33f), Face(Vector3.right), new Vector2(7f, 0.9f), 0.7f, "<b>GHOST TRAIN</b>").color = new Color(0.5f, 1f, 0.6f);
        Text("GhostSub", gt, new Vector3(80.2f, 3.2f, 33f), Face(Vector3.right), new Vector2(7f, 0.4f), 0.2f, "sit in a cart. keep your arms inside. we can't promise the same for them.").color = Color.white;
        foreach (float ez in new[] { 31f, 35f })
        {
            Prim(PrimitiveType.Sphere, "Eye", gt, new Vector3(80.2f, 2.4f, ez), Vector3.one * 0.4f, M("GlowRedEye"));
        }
        Box(gt, "Platform", new Vector3(85f, 0.15f, 33f), new Vector3(2f, 0.3f, 8f), M("DeskWood"));
        var route = new[] { new Vector3(83f, 0, 30f), new Vector3(83f, 0, 36f), new Vector3(83f, 0, 38f), new Vector3(78f, 0, 38f), new Vector3(66f, 0, 39f),
                            new Vector3(64f, 0, 35f), new Vector3(75f, 0, 33.5f), new Vector3(66f, 0, 30f), new Vector3(64f, 0, 27.6f), new Vector3(78f, 0, 28f), new Vector3(83f, 0, 28f) };
        var pace = new[] { 0.35f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
        var gtExit = Marker(gt, "Ghost exit", new Vector3(85f, 0.35f, 33f), 90f);
        var carts = new List<Transform>();
        for (int c = 0; c < 2; c++)
        {
            var cart = Group(gt, "Cart " + c, route[0]);
            Box(cart, "Body", new Vector3(0, 0.35f, 0), new Vector3(1.2f, 0.6f, 1.8f), M("CaneRed"), false);
            Prim(PrimitiveType.Sphere, "Skull", cart, new Vector3(0, 0.75f, 0.85f), new Vector3(0.4f, 0.38f, 0.42f), M("BoneWhite"));
            foreach (float sx in new[] { -0.08f, 0.08f }) Prim(PrimitiveType.Sphere, "Socket", cart, new Vector3(sx, 0.8f, 1.04f), Vector3.one * 0.1f, M("GlowRedEye"));
            RideSeat(cart, "Cart seat " + c, new Vector3(0, 0.3f, -0.2f), 0f, gtExit);
            carts.Add(cart);
        }
        var gr = Add<FirRide>(gt.gameObject);
        gr.kind = 1; gr.carts = carts.ToArray(); gr.route = route; gr.pace = pace; gr.speed = 2.0f;
        var sc = Group(gt, "Scares", Vector3.zero);
        ParkScare(sc, "Ghost scare 1", new Vector3(70f, 0, 41.3f), 180f, 2.5f, "scream1", 2.4f);
        ParkScare(sc, "Ghost scare 2", new Vector3(62.8f, 0, 35f), 90f, 1.6f, "scream2", 2.4f);
        ParkScare(sc, "Ghost scare 3", new Vector3(75f, 0, 31.0f), 0f, 2.4f, "scream3", 2.4f);
        ParkScare(sc, "Ghost scare 4", new Vector3(64f, 0, 26.6f), 0f, 1.0f, "scream1", 2.4f);
        ParkScare(sc, "Ghost scare 5", new Vector3(78f, 0, 30.2f), 180f, 2.2f, "scream2", 2.4f);
        ParkScare(sc, "Fortune scare", new Vector3(96.6f, 0, 79.2f), 180f, 3f, "scream3");
        ParkScare(sc, "Hedge scare", new Vector3(114.8f, 0, 40f), -90f, 3f, "scream1");
        ParkSkeleton(g, "Ghoul", "#2f4f4f", 19, new Vector3(84.6f, 0, 40.5f), 90f);

        // ---- snack stalls (east row, facing west) and game booths (west row, facing east)
        var foods = new[] { new { who = "Taffy", hex = "#ff9ecb", act = 28, title = "SALT WATER TAFFY" }, new { who = "Popcorn", hex = "#fff6c0", act = 16, title = "POPCORN" },
                            new { who = "Cotton", hex = "#b8e2ff", act = 18, title = "COTTON CANDY" }, new { who = "Churro", hex = "#c8803c", act = 27, title = "CHURROS" },
                            new { who = "Pretzel", hex = "#a0622d", act = 3, title = "PRETZELS" }, new { who = "Sundae", hex = "#fff0f5", act = 8, title = "ICE CREAM" },
                            new { who = "Slushy", hex = "#3d7dff", act = 29, title = "SLUSHIES" } };
        for (int i = 0; i < foods.Length; i++)
        {
            var f = foods[i];
            var st = Stall(g, f.title, new Vector3(116.4f, 0, 30f + i * 4.5f), -90f, i % 2 == 0 ? "CaneRed" : "GiftBlue", "CaneWhite");
            ParkSkeleton(st, f.who, f.hex, f.act, new Vector3(0, 0, -0.3f), 0f);
        }
        var games = new[] { new { who = "Hoop", hex = "#ffd23f", act = 30, title = "RING TOSS" }, new { who = "Brawn", hex = "#8b0000", act = 10, title = "TEST YOUR STRENGTH" },
                            new { who = "Dunk", hex = "#4fc3f7", act = 32, title = "DUNK THE SKELETON" } };
        for (int i = 0; i < games.Length; i++)
        {
            var f = games[i];
            var st = Stall(g, f.title, new Vector3(63.6f, 0, 46f + i * 4.5f), 90f, "GiftGreen", "GiftGold");
            ParkSkeleton(st, f.who, f.hex, f.act, new Vector3(0, i == 2 ? 0.6f : 0f, -0.3f), 0f);
            if (i == 0) for (int b = 0; b < 6; b++) DPrim(PrimitiveType.Cylinder, "Bottle", st, new Vector3(-0.9f + b * 0.36f, 1.15f, 0.6f), new Vector3(0.1f, 0.15f, 0.1f), M("GiftGreen"));
            if (i == 1) { DPrim(PrimitiveType.Cylinder, "Striker", st, new Vector3(0.9f, 1.6f, -0.6f), new Vector3(0.12f, 1.6f, 0.12f), M("CaneRed")); DPrim(PrimitiveType.Sphere, "Bell", st, new Vector3(0.9f, 3.25f, -0.6f), Vector3.one * 0.3f, M("GiftGold")); }
            if (i == 2) DPrim(PrimitiveType.Cylinder, "Tank", st, new Vector3(0, 0.3f, -0.3f), new Vector3(1.2f, 0.3f, 1.2f), M("Water"));
        }
        // the ticket booth, the fortune teller, the juggler, the clown and the balloon seller
        var tb = Stall(g, "TICKETS", new Vector3(84.5f, 0, 24.5f), 0f, "CaneRed", "GiftGold");
        ParkSkeleton(tb, "Ticket", "#ffcc00", 20, new Vector3(0, 0, -0.3f), 0f);
        var tent = Group(g, "Fortune Tent", new Vector3(100f, 0, 76.5f));
        Prim(PrimitiveType.Cylinder, "Tent", tent, new Vector3(0, 1.3f, 0.4f), new Vector3(3.0f, 1.3f, 2.6f), M("Curtain"));
        Prim(PrimitiveType.Cylinder, "Peak", tent, new Vector3(0, 2.8f, 0.4f), new Vector3(2.2f, 0.3f, 2.0f), M("GiftGold"));
        DPrim(PrimitiveType.Sphere, "CrystalBall", tent, new Vector3(0, 1.0f, -1.3f), Vector3.one * 0.35f, M("GlowGhost"));
        Box(tent, "Table", new Vector3(0, 0.4f, -1.3f), new Vector3(0.9f, 0.8f, 0.6f), M("Curtain"));
        Text("TentSign", tent, new Vector3(0, 2.75f, -1.0f), Face(Vector3.back), new Vector2(3f, 0.35f), 0.22f, "<b>MADAME FORTUNE</b>").color = new Color(1f, 0.85f, 0.4f);
        ParkSkeleton(tent, "Fortune", "#6a0dad", 26, new Vector3(0, 0, -0.75f), 0f);
        DPrim(PrimitiveType.Cylinder, "JugglerStage", g, new Vector3(90f, 0.2f, 74.5f), new Vector3(2.4f, 0.2f, 2.4f), M("CaneRed"));
        ParkSkeleton(g, "Jester", "#ff4500", 25, new Vector3(90f, 0.4f, 74.5f), 180f);
        ParkSkeleton(g, "Confetti", "#ff1493", 21, new Vector3(96f, 0, 40f), -90f);
        ParkSkeleton(g, "Balloon", "#00ced1", 22, new Vector3(84f, 0, 47f), 90f);
        for (int b = 0; b < 7; b++)
        {
            var bp = new Vector3(84f + Mathf.Sin(b * 2.1f) * 0.5f, 3.0f + (b % 3) * 0.35f, 47f + Mathf.Cos(b * 2.1f) * 0.5f);
            DPrim(PrimitiveType.Sphere, "Balloon", g, bp, new Vector3(0.45f, 0.55f, 0.45f), M(new[] { "CaneRed", "GiftBlue", "GiftGreen", "GiftGold", "BonePink" }[b % 5]));
            var str = (new Vector3(84.4f, 1.3f, 47f) + bp) / 2f; var sd = bp - new Vector3(84.4f, 1.3f, 47f);
            DPrim(PrimitiveType.Cylinder, "String", g, str, new Vector3(0.01f, sd.magnitude / 2f, 0.01f), M("CaneWhite"), Quaternion.FromToRotation(Vector3.up, sd.normalized));
        }

        // ---- the park's wanderers: kids running between rides, their parent, and visitors
        var wg = Group(g, "Wanderers", Vector3.zero);
        var main = new[] { new Vector3(86f, 0, 28.5f), new Vector3(86f, 0, 58f), new Vector3(98f, 0, 58f), new Vector3(98f, 0, 28.5f) };
        var tring = Enumerable.Range(0, 16).Select(k => new Vector3(92f + Mathf.Cos(k * Mathf.PI / 8f) * 5.6f, 0, 50f + Mathf.Sin(k * Mathf.PI / 8f) * 5.6f)).ToArray();
        var pw = new[] {
            new { who = "Sprinkle", hex = "#ffb6c1", route = tring, pace = Enumerable.Repeat(2.6f, 16).ToArray(), speed = 1.1f, head = 0 },
            new { who = "Lollipop", hex = "#ff6347", route = tring, pace = Enumerable.Repeat(2.6f, 16).ToArray(), speed = 1.1f, head = 1600 },
            new { who = "Fudge", hex = "#5c3317", route = tring, pace = Enumerable.Repeat(1.8f, 16).ToArray(), speed = 1.1f, head = 6000 },
            new { who = "Gumdrop", hex = "#7cfc00", route = new[] { new Vector3(112f, 0, 30f), new Vector3(112f, 0, 57f) }, pace = new[] { 1f, 1f }, speed = 0.9f, head = 0 },
            new { who = "Toffee", hex = "#d2a679", route = new[] { new Vector3(84f, 0, 70.3f), new Vector3(117f, 0, 70.3f) }, pace = new[] { 1f, 1f }, speed = 0.9f, head = 7000 },
            new { who = "Licorice", hex = "#1c1c1c", route = new[] { new Vector3(69f, 0, 58f), new Vector3(81f, 0, 58f) }, pace = new[] { 1f, 2.4f }, speed = 1.0f, head = 3000 },
            new { who = "Nougat", hex = "#f5deb3", route = new[] { new Vector3(88f, 0, 25.5f), new Vector3(95f, 0, 25.5f), new Vector3(95f, 0, 31f), new Vector3(88f, 0, 31f) }, pace = new[] { 1f, 1f, 1f, 1f }, speed = 0.8f, head = 0 },
            new { who = "Marshmallow", hex = "#fffafa", route = main, pace = new[] { 1f, 1f, 1f, 1f }, speed = 1.0f, head = 11000 } };
        foreach (var w in pw)
            MakeWanderer(wg, w.who, w.hex, w.route, w.pace, w.speed, w.head, "Park/pk_" + w.who.ToLower(), ParkLine(w.who));

        // ---- the park's gags (Data/park_gags.txt)
        int placed = 0;
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/park_gags.txt");
        if (ta == null) report.Add("problem: Data/park_gags.txt is missing");
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
        int skeletons = g.GetComponentsInChildren<FirRainbowSkeleton>(true).Length + g.GetComponentsInChildren<FirWanderer>(true).Length;
        report.Add("Boneanza Park: 5 working rides (" + rideSeats + " seats), " + skeletons + " skeletons, " + placed + " gags, " + parkScares + " jump scares");
    }

    static string ParkLine(string who)
    {
        switch (who)
        {
            case "Sprinkle": return "Mum! Mum! Can we go on the ghost train? Can we? Can we?";
            case "Gumdrop": return "I've had nine candy apples. I'm not hungry. I just like the noise.";
            case "Lollipop": return "Race you to the carousel! Last one there is a pile of bones!";
            case "Fudge": return "Has anyone seen my kid? About this tall. Skeletal. Sticky.";
            case "Toffee": return "I lost my ticket. And my left foot. The ticket's more important.";
            case "Licorice": return "The ghost train isn't scary. I definitely didn't scream. That was my knees.";
            case "Nougat": return "I won a goldfish! It's a skeleton fish. Very low maintenance.";
            case "Marshmallow": return "I'm toasting marshmallows! Ah. That one was me. Ow.";
        }
        return "";
    }

    // =================================================================== the Root Cellar (horror expansion), east of the lobby

    static readonly float CX = 22f;   // the cellar's rooms are centred on x = 22
    static readonly float[] CZ0 = { -8f, -26f, -44f, -62f, -78f };
    static readonly float[] CZ1 = { -26f, -44f, -62f, -78f, -96f };
    static readonly float[] CDoor = { 0f, 0f, 4f, -4f, 0f };   // each room's north doorway, local x

    static void BuildCellar(Transform root)
    {
        // ---- the potting shed: the safe room with the cellar game's START (lit, outside the game)
        var shed = Group(root, "Shed", new Vector3(CX, 0, 0));
        Box(shed, "Floor", new Vector3(0, -0.1f, -3f), new Vector3(18f, 0.2f, 10f), M("Floor2"));
        Box(shed, "Ceiling", new Vector3(0, 3.7f, -3f), new Vector3(18f, 0.2f, 10f), M("Ceiling"));
        Box(shed, "WallN", new Vector3(0, 1.8f, 2.1f), new Vector3(18.4f, 3.6f, 0.2f), M("Wainscot"));
        Box(shed, "WallE", new Vector3(9.1f, 1.8f, -3f), new Vector3(0.2f, 3.6f, 10.4f), M("Wainscot"));
        Box(shed, "WallW S", new Vector3(-9.1f, 1.8f, -5.6f), new Vector3(0.2f, 3.6f, 4.8f), M("Wainscot"));
        Box(shed, "WallW N", new Vector3(-9.1f, 1.8f, 0.6f), new Vector3(0.2f, 3.6f, 2.8f), M("Wainscot"));
        Box(shed, "WallW Top", new Vector3(-9.1f, 3.2f, -2f), new Vector3(0.2f, 0.8f, 2.4f), M("Wainscot"));
        // the corridor from the lobby's east doorway (z -3.2 to -0.8)
        var cor = Group(root, "Cellar Corridor", Vector3.zero);
        Box(cor, "Floor", new Vector3(10.1f, -0.1f, -2f), new Vector3(5.8f, 0.2f, 2.4f), M("Floor2"));
        Box(cor, "Ceiling", new Vector3(10.1f, 3.1f, -2f), new Vector3(5.8f, 0.2f, 2.8f), M("Ceiling"));
        Box(cor, "WallS", new Vector3(10.1f, 1.5f, -3.3f), new Vector3(5.8f, 3f, 0.2f), M("Wainscot"));
        Box(cor, "WallN", new Vector3(10.1f, 1.5f, -0.7f), new Vector3(5.8f, 3f, 0.2f), M("Wainscot"));
        PendantLamp(cor, new Vector3(10.1f, 3.0f, -2f), 0.45f);
        Text("CorridorSign", cor, new Vector3(10.1f, 2.3f, -0.82f), Face(Vector3.back), new Vector2(4.6f, 0.4f), 0.2f, "<b>THE ROOT CELLAR</b> this way  >").color = new Color(0.9f, 0.3f, 0.25f);
        PendantLamp(shed, new Vector3(-4f, 3.6f, -3f), 0.5f);
        PendantLamp(shed, new Vector3(4f, 3.6f, -3f), 0.5f);
        for (int k = 0; k < 6; k++)
            DPrim(PrimitiveType.Cylinder, "Pot", shed, new Vector3(-7.5f + k * 0.55f, 0.2f, 1.6f), new Vector3(0.4f, 0.2f, 0.4f), M("Pot"));
        Box(shed, "Shelf", new Vector3(-6f, 1.4f, 1.85f), new Vector3(3.6f, 0.05f, 0.4f), M("DeskWood"), false);
        for (int k = 0; k < 5; k++)
            DPrim(PrimitiveType.Cylinder, "Jar", shed, new Vector3(-7.4f + k * 0.7f, 1.6f, 1.85f), new Vector3(0.2f, 0.15f, 0.2f), M("Water"));
        // the story, the status board, START and RESET on the north wall
        var nb = Vector3.back;
        Box(shed, "StartBoard", new Vector3(2.2f, 1.85f, 1.995f), new Vector3(8.6f, 3.0f, 0.01f), M("Board"));
        Text("Title", shed, new Vector3(2.2f, 3.0f, 1.97f), Face(nb), new Vector2(8f, 0.6f), 0.45f, "<b>THE ROOT CELLAR</b>").color = new Color(0.9f, 0.25f, 0.2f);
        Text("Story", shed, new Vector3(-0.3f, 1.65f, 1.97f), Face(nb), new Vector2(3.4f, 1.8f), 0.12f,
             "Under the factory, the trees grew roots. The night shift went down there and never came up (they're still down there; they're fine; they're skeletons).\n\n" +
             "<b>Two</b> trees hunt down here, the second wakes up halfway, and the lights don't stay on. Five rooms. The way out is the woodchipper.").color = Color.white;
        cellarHunt = null;
        var hg = Group(root, "Cellar Hunt", new Vector3(CX, 1.5f, -3f));
        var h2 = Add<FirHunt>(hg.gameObject);
        cellarHunt = h2;
        h2.lobbyBoard = Text("Status", shed, new Vector3(3.9f, 2.2f, 1.97f), Face(nb), new Vector2(3.6f, 0.7f), 0.13f, "");
        h2.lobbyBoard.color = Color.white;
        int b0 = huntButtons.Count, s0 = scares.Count, o0 = huntOnly.Count;
        HB(shed, "START", new Vector3(3.9f, 1.3f, 1.9f), nb, new Vector3(1.4f, 0.5f, 0.12f), M("ButtonGold"), 1, 0, "Start the Root Cellar (or join it)", "<b>START</b>", 0.28f, true);
        HB(shed, "RESET", new Vector3(3.9f, 0.7f, 1.9f), nb, new Vector3(0.8f, 0.3f, 0.12f), M("Button"), 2, 0, "End the Root Cellar game", "<b>RESET</b>", 0.14f, true);
        Text("Players", shed, new Vector3(3.9f, 0.4f, 1.97f), Face(nb), new Vector2(3.4f, 0.25f), 0.1f, "best with 2 players  -  harder than the first game").color = new Color(0.75f, 0.75f, 0.75f);

        // ---- the five rooms
        var cel = Group(root, "Cellar", new Vector3(CX, 0, 0));
        var rooms = new[] {
            new { name = "THE GREENHOUSE", h = 4.0f, floor = "Concrete", wall = "WallGreen" },
            new { name = "THE SAWMILL", h = 4.5f, floor = "Floor2", wall = "BrickWall" },
            new { name = "THE TOOL ROOM", h = 3.6f, floor = "Floor2", wall = "WallDark" },
            new { name = "THE GENERATOR", h = 3.4f, floor = "Concrete", wall = "Concrete" },
            new { name = "THE WOODCHIPPER", h = 4.5f, floor = "Concrete", wall = "BrickWall" } };
        float w = 18f;
        for (int i = 0; i < 5; i++)
        {
            var r = rooms[i];
            float z0 = CZ0[i], z1 = CZ1[i], len = z0 - z1, zc = (z0 + z1) / 2f;
            var g = Group(cel, r.name, Vector3.zero);
            Box(g, "Floor", new Vector3(0, -0.1f, zc), new Vector3(w, 0.2f, len), M(r.floor));
            Box(g, "Ceiling", new Vector3(0, r.h + 0.1f, zc), new Vector3(w, 0.2f, len), M("Ceiling"));
            Box(g, "WallW", new Vector3(-w / 2 - 0.1f, r.h / 2, zc), new Vector3(0.2f, r.h, len), M(r.wall));
            Box(g, "WallE", new Vector3(w / 2 + 0.1f, r.h / 2, zc), new Vector3(0.2f, r.h, len), M(r.wall));
            DoorWall(g, "WallN", z0 - 0.1f, w + 0.4f, Mathf.Max(r.h, i == 0 ? 3.6f : rooms[i - 1].h), M(r.wall), CDoor[i]);
            if (i == 4) Box(g, "WallS", new Vector3(0, r.h / 2, z1 - 0.1f), new Vector3(w + 0.4f, r.h, 0.2f), M(r.wall));
            DoorFrame(g, CDoor[i], z0 - 0.1f);
            Text("RoomName", g, new Vector3(CDoor[i], 3.12f, z0 + 0.03f), Face(Vector3.forward), new Vector2(4.5f, 0.4f), 0.24f, "<b>" + r.name + "</b>").color = new Color(0.9f, 0.3f, 0.25f);
            // roots: thick, twisted, across the ceiling and down the walls
            for (int k = 0; k < (int)(len / 3f); k++)
            {
                float rz = z0 - 1.5f - k * 3f, rx = Mathf.Sin(k * 2.3f + i) * 5f;
                DPrim(PrimitiveType.Cylinder, "Root", g, new Vector3(rx, r.h - 0.15f, rz), new Vector3(0.22f, w / 2.2f, 0.22f), M("Trunk"), Quaternion.Euler(0, k * 37f + i * 20f, 90));
                DPrim(PrimitiveType.Cylinder, "Root", g, new Vector3(k % 2 == 0 ? -w / 2 + 0.12f : w / 2 - 0.12f, r.h / 2, rz + 1f), new Vector3(0.16f, r.h / 2, 0.16f), M("Trunk"), Quaternion.Euler(8f * Mathf.Sin(k), 0, 6f * Mathf.Cos(k)));
            }
            PendantLamp(g, new Vector3(0, r.h, zc + len / 4f), 0.6f);
            PendantLamp(g, new Vector3(0, r.h, zc - len / 4f), 0.6f);
        }

        // ---- the game: doors, checkpoints, boards
        h2.sfx = Src(hg, Vector3.zero, null, false, 0f, 1f, 5f, false);
        h2.heartbeat = Src(hg, Vector3.zero, Clip("heartbeat"), true, 0f, 0f, 5f, false);
        h2.ambient = Src(hg, Vector3.zero, null, false, 1f, 0.9f, 14f, false);
        h2.ambientClips = new[] { Clip("whisper"), Clip("rustle"), Clip("tree_laugh"), Clip("servo"), Clip("static"), Clip("music_box_broken"), Clip("creak"), Clip("door_bang") };
        h2.startClip = Clip("door_bang"); h2.unlockClip = Clip("unlock"); h2.wrongClip = Clip("wrong"); h2.pickupClip = Clip("pickup");
        h2.switchClip = Clip("switch"); h2.endingClip = Clip("ending"); h2.screamClip = Clip("scream3"); h2.stunClip = Clip("toilet_flush");
        h2.scareFace = ScareFace(hg);
        h2.checkpoints = new Transform[5];
        for (int r = 0; r < 5; r++) h2.checkpoints[r] = Marker(cel, "Checkpoint " + r, new Vector3(CDoor[r], 0.05f, CZ0[r] - 1.6f), 180f);
        var doors = new GameObject[5];
        for (int r = 1; r < 5; r++) doors[r] = LockedDoor(cel, "Door " + r, new Vector3(CDoor[r], 0, CZ0[r] - 0.1f));
        h2.doors = doors;
        h2.roomBoards = new TextMeshPro[5];
        for (int r = 0; r < 5; r++)
        {
            float bx = CDoor[r] - 3.3f, bz = CZ0[r] - 0.12f;
            Box(cel, "TaskBoard " + r, new Vector3(bx, 1.95f, bz - 0.02f), new Vector3(2.5f, 1.25f, 0.03f), M("Board"), false);
            var t = Text("TaskText " + r, cel, new Vector3(bx, 1.95f, bz - 0.04f), Face(Vector3.back), new Vector2(2.35f, 1.15f), 0.14f, "");
            t.color = Color.white;
            h2.roomBoards[r] = t;
        }
        var items = new List<GameObject>(); var done = new List<GameObject>(); var itemRoom = new List<int>();
        Func<int, string, Vector3, Vector3, FirHuntButton> Item = (room, name, pos, normal) =>
        {
            var b = HB(cel, name, pos, normal, new Vector3(0.32f, 0.32f, 0.32f), null, 3, items.Count, "Pick up the " + name.ToLower(), null);
            items.Add(b.gameObject); done.Add(null); itemRoom.Add(room);
            return b;
        };

        // ---- room 1, the Greenhouse: 4 seed packets, then the big pot
        foreach (float bx in new[] { -4f, 4f })
        {
            Box(cel, "PlantBed", new Vector3(bx, 0.3f, -17f), new Vector3(1.6f, 0.6f, 12f), M("DeskWood"));
            for (int k = 0; k < 7; k++)
                DPrim(PrimitiveType.Sphere, "Plant", cel, new Vector3(bx + Mathf.Sin(k * 1.7f) * 0.3f, 0.75f, -11.8f - k * 1.7f), new Vector3(0.6f, 0.5f + 0.1f * (k % 3), 0.6f), M("Hedge"));
        }
        Box(cel, "WallShelf", new Vector3(8.75f, 1.75f, -11f), new Vector3(0.5f, 0.05f, 1.4f), M("DeskWood"), false);
        var seeds = new[] { new Vector3(-4.6f, 0.75f, -14.2f), new Vector3(4.5f, 0.75f, -21.3f), new Vector3(-7.8f, 0.1f, -24.6f), new Vector3(8.7f, 1.9f, -11f) };
        foreach (var p in seeds)
        {
            var b = Item(0, "Seed packet", p, Vector3.forward);
            Prim(PrimitiveType.Cube, "Packet", b.transform, Vector3.zero, new Vector3(0.18f, 0.24f, 0.02f), M("GlowYellow"));
            Prim(PrimitiveType.Cube, "Stripe", b.transform, new Vector3(0, -0.04f, -0.011f), new Vector3(0.18f, 0.06f, 0.005f), M("GlowGreen"));
        }
        var pot = HB(cel, "Big pot", new Vector3(2.6f, 0.55f, -25.4f), Vector3.forward, new Vector3(1.2f, 1.1f, 1.0f), null, 4, 0, "Plant the seeds", null);
        Prim(PrimitiveType.Cylinder, "Pot", pot.transform, new Vector3(0, -0.15f, 0), new Vector3(1.1f, 0.4f, 1.1f), M("Pot"));
        Text("PotSign", cel, new Vector3(2.6f, 1.5f, -25.85f), Face(Vector3.forward), new Vector2(2f, 0.4f), 0.13f, "<b>THE BIG POT</b>\n<size=75%>plant 4 seeds to open the door</size>").color = new Color(1f, 0.85f, 0.4f);
        var sprout = Group(cel, "Sprouted", new Vector3(2.6f, 0.4f, -25.4f));
        TreeAt(sprout, "Sapling", new Vector3(0, 0.3f, 0), 0f, 1.0f, false);

        // ---- room 2, the Sawmill: numbers carved in the stumps (red, yellow, green, blue), saw blades on the walls
        var stumps = new[] { new Vector3(-6f, 0, -30f), new Vector3(6f, 0, -33f), new Vector3(-5f, 0, -40f), new Vector3(5.5f, 0, -41f) };
        Color[] cc = { new Color(1f, 0.19f, 0.19f), new Color(1f, 0.88f, 0.19f), new Color(0.25f, 1f, 0.31f), new Color(0.25f, 0.5f, 1f) };
        h2.rainbowDigits = new TextMeshPro[4];
        for (int k = 0; k < 4; k++)
        {
            Box(cel, "Stump", stumps[k] + new Vector3(0, 0.35f, 0), new Vector3(0.9f, 0.7f, 0.9f), M("Trunk"));
            var t = Text("Carved " + k, cel, stumps[k] + new Vector3(0, 0.72f, 0), Quaternion.LookRotation(Vector3.down, Vector3.forward), new Vector2(0.6f, 0.6f), 0.45f, "?");
            t.color = cc[k];
            h2.rainbowDigits[k] = t;
            huntOnly.Add(t.gameObject);
        }
        int blades = 0;
        foreach (var bp in new[] { new Vector3(-8.95f, 2.6f, -29f), new Vector3(-8.95f, 2.6f, -33.5f), new Vector3(-8.95f, 2.6f, -38f), new Vector3(-8.95f, 2.6f, -42.2f),
                                   new Vector3(8.95f, 2.6f, -30f), new Vector3(8.95f, 2.6f, -35f), new Vector3(8.95f, 2.6f, -39.5f) })
        {
            DPrim(PrimitiveType.Cylinder, "SawBlade", cel, bp, new Vector3(0.9f, 0.01f, 0.9f), M("Metal"), Quaternion.Euler(0, 0, 90));
            DPrim(PrimitiveType.Cylinder, "Hub", cel, bp + new Vector3(bp.x < 0 ? 0.02f : -0.02f, 0, 0), new Vector3(0.18f, 0.012f, 0.18f), M("Black"), Quaternion.Euler(0, 0, 90));
            blades++;
        }
        Box(cel, "LogPile", new Vector3(0, 0.3f, -36.5f), new Vector3(2.2f, 0.6f, 1.2f), M("Trunk"));
        Text("SawSign", cel, new Vector3(-3.3f, 2.9f, -26.14f), Face(Vector3.back), new Vector2(2.4f, 0.3f), 0.12f, "<b>SAFETY FIRST</b> (we stopped counting at day 0)").color = new Color(1f, 0.85f, 0.4f);
        h2.rainbowDisplay = Keypad(cel, "Sawmill keypad", new Vector3(1.2f, 1.35f, -43.86f), Vector3.forward, 1);

        // ---- room 3, the Tool Room: notes give the order of 4 tools; the tool lock by the exit (x -4)
        h2.toyNames = new[] { "HAMMER", "SAW", "DRILL", "WRENCH", "AXE" };
        var lockG = Group(cel, "Tool lock", new Vector3(-0.2f, 1.35f, -61.86f));
        lockG.localRotation = Face(Vector3.forward);
        Box(lockG, "Backing", new Vector3(0, 0.1f, 0.03f), new Vector3(2.3f, 0.9f, 0.03f), M("Board"), false);
        Text("Title", lockG, new Vector3(0, 0.42f, -0.02f), Quaternion.identity, new Vector2(2.1f, 0.2f), 0.11f, "<b>THE TOOL LOCK</b>  press the tools in order").color = Color.white;
        h2.toyDisplay = Text("Display", lockG, new Vector3(0, 0.24f, -0.02f), Quaternion.identity, new Vector2(1.5f, 0.2f), 0.16f, "* * * *");
        h2.toyDisplay.color = Color.white;
        for (int k = 0; k < 5; k++)
            HB(lockG, "Tool " + h2.toyNames[k], new Vector3(-0.84f + k * 0.42f, -0.08f, 0f), Vector3.back, new Vector3(0.36f, 0.36f, 0.06f), M("ButtonGold"), 6, 200 + k,
               "Press the " + h2.toyNames[k].ToLower(), "<b>" + h2.toyNames[k] + "</b>", 0.07f, true);
        Box(cel, "Workbench", new Vector3(-3f, 0.45f, -58.5f), new Vector3(2.0f, 0.9f, 0.8f), M("DeskWood"));
        var noteSpots = new[] {
            new { p = new Vector3(-8.93f, 1.5f, -48f), n = Vector3.right },
            new { p = new Vector3(8.93f, 1.5f, -55f), n = Vector3.left },
            new { p = new Vector3(-3f, 1.12f, -58.08f), n = Vector3.forward },
            new { p = new Vector3(2.6f, 2.2f, -44.13f), n = Vector3.back } };
        h2.toyNotes = new TextMeshPro[4];
        for (int k = 0; k < 4; k++)
        {
            var ng = Group(cel, "Note " + k, noteSpots[k].p);
            ng.localRotation = Face(noteSpots[k].n);
            Prim(PrimitiveType.Cube, "Paper", ng, new Vector3(0, 0, 0.006f), new Vector3(0.34f, 0.26f, 0.01f), M("Paper"));
            var t = Text("Text", ng, new Vector3(0, 0, -0.002f), Quaternion.identity, new Vector2(0.3f, 0.22f), 0.07f, "#?");
            t.color = new Color(1f, 0.95f, 0.7f);
            h2.toyNotes[k] = t;
            huntOnly.Add(ng.gameObject);
        }

        // ---- room 4, the Generator: 3 fuses, then the fuse box
        Box(cel, "Generator", new Vector3(2.5f, 0.7f, -70f), new Vector3(2f, 1.4f, 1.4f), M("Metal"));
        DPrim(PrimitiveType.Cylinder, "Exhaust", cel, new Vector3(2.9f, 2.0f, -70f), new Vector3(0.25f, 0.6f, 0.25f), M("Black"));
        Box(cel, "FuseShelf", new Vector3(-8.75f, 0.9f, -66f), new Vector3(0.5f, 0.05f, 1.2f), M("DeskWood"), false);
        var fuses = new[] { new Vector3(-8.65f, 1.05f, -66f), new Vector3(7.0f, 0.1f, -76.5f), new Vector3(2.0f, 1.55f, -70f) };
        foreach (var p in fuses)
        {
            var b = Item(3, "Fuse", p, Vector3.forward);
            Prim(PrimitiveType.Cylinder, "Glass", b.transform, Vector3.zero, new Vector3(0.07f, 0.1f, 0.07f), M("GlowYellow")).transform.localRotation = Quaternion.Euler(0, 0, 90);
            foreach (float e in new[] { -0.11f, 0.11f }) Prim(PrimitiveType.Cylinder, "Cap", b.transform, new Vector3(e, 0, 0), new Vector3(0.08f, 0.02f, 0.08f), M("Brass")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        }
        HB(cel, "Fuse box", new Vector3(2.4f, 1.35f, -77.86f), Vector3.forward, new Vector3(0.7f, 0.9f, 0.18f), M("Metal"), 4, 3, "Fix the fuse box",
           "<b>FUSE BOX</b>\n<size=70%>3 fuses missing</size>");

        // ---- room 5, the Woodchipper: the foreman's note, the keypad, the machine
        var chip = Group(cel, "Woodchipper", new Vector3(-4f, 0, -94.6f));
        Box(chip, "Body", new Vector3(0, 1.1f, 0), new Vector3(3f, 2.2f, 2f), M("CaneRed"));
        Box(chip, "Hopper", new Vector3(0, 2.4f, 0.6f), new Vector3(2.2f, 0.6f, 1.0f), M("Metal"), false);
        Prim(PrimitiveType.Cube, "Mouth", chip, new Vector3(0, 1.4f, 1.02f), new Vector3(1.6f, 0.9f, 0.02f), M("Black"));
        var roar = Prim(PrimitiveType.Cube, "Roar", chip, new Vector3(0, 1.4f, 1.04f), new Vector3(1.5f, 0.85f, 0.02f), M("GlowOrange"));
        Text("ChipperSign", chip, new Vector3(0, 2.45f, 1.12f), Face(Vector3.forward), new Vector2(2.6f, 0.3f), 0.17f, "<b>THE WOODCHIPPER</b>").color = Color.white;
        int crew = 0;
        Box(cel, "NoteBoard", new Vector3(-8.97f, 1.6f, -86f), new Vector3(0.03f, 1.0f, 1.9f), M("Board"), false);
        h2.talentDisplay = Keypad(cel, "Chipper keypad", new Vector3(2.7f, 1.35f, -95.86f), Vector3.forward, 4);
        var win = Text("WinSign", cel, new Vector3(0, 3.2f, -95.95f), Face(Vector3.forward), new Vector2(8f, 0.6f), 0.4f, "<b>YOU ESCAPED THE CELLAR!</b>");
        win.color = new Color(0.45f, 1f, 0.55f);
        h2.stageShow = new[] { sprout.gameObject, roar, win.gameObject };
        h2.stageShowAt = new[] { 1, 5, 5 };

        // ---- the night shift: 12 skeletons (they're the code)
        var shift = new[] {
            new { who = "Moss", p = new Vector3(-7.8f, 0, -17f), yaw = 90f }, new { who = "Fern", p = new Vector3(7.8f, 0, -14f), yaw = -90f },
            new { who = "Bark", p = new Vector3(-7.8f, 0, -36f), yaw = 90f }, new { who = "Steel", p = new Vector3(7.8f, 0, -37.5f), yaw = -90f },
            new { who = "Pewter", p = new Vector3(-7.8f, 0, -31.5f), yaw = 90f },
            new { who = "Midnight", p = new Vector3(-7.8f, 0, -51f), yaw = 90f }, new { who = "Charcoal", p = new Vector3(7.8f, 0, -50f), yaw = -90f },
            new { who = "Mauve", p = new Vector3(7.8f, 0, -58.5f), yaw = -90f },
            new { who = "Burgundy", p = new Vector3(-7.8f, 0, -72f), yaw = 90f }, new { who = "Chartreuse", p = new Vector3(7.8f, 0, -66.5f), yaw = -90f },
            new { who = "Mocha", p = new Vector3(-7.8f, 0, -82f), yaw = 90f }, new { who = "Wine", p = new Vector3(7.8f, 0, -88f), yaw = -90f } };
        foreach (var sk in shift) { ExtraSkeleton(cel, Extra40.First(x => x[0] == sk.who), sk.p, sk.yaw); crew++; }
        h2.talentCode = crew.ToString() + blades.ToString();
        Text("ForemanNote", cel, new Vector3(-8.94f, 1.6f, -86f), Face(Vector3.right), new Vector2(1.75f, 0.9f), 0.11f,
             "<b>FOREMAN'S NOTE</b>\nThe chipper's code: how many skeletons work the night shift down here (all five rooms), then how many saw blades hang in the sawmill.").color = Color.white;

        // ---- two trees
        var stalkers = new List<FirStalker>();
        foreach (var spec in new[] { new { name = "Cellar Stalker A", h = 2.6f, wake = 0, speed = 1.5f, lairX = 4.5f },
                                     new { name = "Cellar Stalker B", h = 3.0f, wake = 2, speed = 1.35f, lairX = -5f } })
        {
            var sgo = new GameObject(spec.name);
            sgo.transform.SetParent(cel, false);
            sgo.transform.localPosition = new Vector3(spec.lairX, 0, -24f);
            var st = Add<FirStalker>(sgo);
            var body = Group(sgo.transform, "Body", Vector3.zero);
            Transform smouth;
            var shead = TreeModel(body, spec.h, spec.wake > 0, out smouth);
            foreach (var r in body.GetComponentsInChildren<MeshRenderer>(true)) if (r.name.StartsWith("Pupil")) r.sharedMaterial = M("GlowRedEye");
            st.body = body.gameObject; st.head = shead; st.mouth = smouth;
            st.roomZ0 = CZ0; st.roomZ1 = CZ1; st.doorX = CDoor.Select(x => x + CX).ToArray();
            st.areaMinX = CX - 9.5f; st.areaMaxX = CX + 9.5f;
            st.wakeStage = spec.wake; st.baseSpeed = spec.speed; st.speedPerRoom = 0.22f;
            st.lairs = Enumerable.Range(0, 5).Select(i => Marker(cel, spec.name + " lair " + i, new Vector3(spec.lairX, 0, CZ1[i] + 2.2f), 0f)).ToArray();
            st.furnace = Marker(cel, spec.name + " in the chipper", new Vector3(-4f + (spec.wake > 0 ? 0.6f : -0.6f), 0, -94.0f), 0f);
            st.voice = Src(sgo.transform, new Vector3(0, 1.8f, 0), null, false, 1f, 1f, 22f, false);
            st.stunClip = Clip("toilet_clunk"); st.wakeClip = Clip("tree_laugh");
            stalkers.Add(st);
        }
        h2.stalkers = stalkers.Select(x => UB(x)).ToArray();
        h2.stalkerBodies = stalkers.Select(x => x.transform).ToArray();
        h2.stalkerVisuals = stalkers.Select(x => x.body).ToArray();

        // ---- toilets and jump scares
        var tg = Group(cel, "Toilets", Vector3.zero);
        foreach (var tp in new[] { new Vector3(-6.5f, 0, -9.5f), new Vector3(6.5f, 0, -24.8f), new Vector3(6.5f, 0, -27.5f), new Vector3(-6.5f, 0, -43f),
                                   new Vector3(-6.5f, 0, -45.5f), new Vector3(6.5f, 0, -61f), new Vector3(6.5f, 0, -63.5f), new Vector3(-6.5f, 0, -77f),
                                   new Vector3(6.5f, 0, -79.5f), new Vector3(-6.5f, 0, -91f) })
            Toilet(tg, tp, tp.z * 31f);
        var sc = Group(cel, "Scares", Vector3.zero);
        ScareZone(sc, "Scare C0a", new Vector3(-8.4f, 0, -21f), 90f, 4.5f, "scream1");
        ScareZone(sc, "Scare C0b", new Vector3(8.4f, 0, -19f), -90f, 4.5f, "scream3");
        ScareZone(sc, "Scare C1a", new Vector3(-8.4f, 0, -41f), 90f, 5.5f, "scream2");
        ScareZone(sc, "Scare C1b", new Vector3(8.4f, 0, -28.5f), -90f, 5.5f, "scream1");
        ScareZone(sc, "Scare C2a", new Vector3(-8.4f, 0, -56f), 90f, 5.5f, "scream3");
        ScareZone(sc, "Scare C2b", new Vector3(8.4f, 0, -46.5f), -90f, 4.5f, "scream2");
        ScareZone(sc, "Scare C3a", new Vector3(-8.4f, 0, -67.5f), 90f, 5.5f, "scream1");
        ScareZone(sc, "Scare C3b", new Vector3(8.4f, 0, -74f), -90f, 5.5f, "scream3");
        ScareZone(sc, "Scare C4a", new Vector3(-8.4f, 0, -91.5f), 90f, 5.5f, "scream2");
        ScareZone(sc, "Scare C4b", new Vector3(8.4f, 0, -80.5f), -90f, 5.5f, "scream1");

        // ---- the game's shape
        h2.items = items.ToArray(); h2.itemDone = done.ToArray(); h2.itemRoom = itemRoom.ToArray();
        h2.rooms = 5;
        h2.roomKind = new[] { 0, 1, 2, 0, 3 };
        h2.boardTitles = new[] { "THE GREENHOUSE", "THE SAWMILL", "THE TOOL ROOM", "THE GENERATOR", "THE WOODCHIPPER" };
        h2.boardTasks = new[] {
            "Find the 4 seed packets, then plant them in the big pot by the exit.",
            "Four numbers are carved into the stumps, glowing <color=#ff3030>red</color>, <color=#ffe030>yellow</color>, <color=#40ff50>green</color> and <color=#4080ff>blue</color>. Type them in that order on the exit keypad.",
            "The night shift left 4 numbered notes. Press the tools on the exit panel in their order. A wrong tool starts you over.",
            "Find the 3 fuses, then fix the fuse box by the exit.",
            "Read the foreman's note (west wall), then type the code on the keypad by the woodchipper. Bring the trees close..." };
        h2.winText = "The trees went into the woodchipper.";
        h2.gameWhere = "the potting shed";
        h2.areaMinX = CX - 9.5f; h2.areaMaxX = CX + 9.5f; h2.areaTopZ = -8.2f;
        h2.flicker = true;
        h2.huntOnly = huntOnly.Skip(o0).ToArray();
        h2.freeOnly = new GameObject[0];
        var me = UB(h2);
        foreach (var b in huntButtons.Skip(b0)) b.hunt = me;
        foreach (var g in scares.Skip(s0)) g.hunt = me;
        report.Add("the Root Cellar: " + items.Count + " items, " + (huntButtons.Count - b0) + " buttons, 4 doors, " + (scares.Count - s0) + " jump scares, 2 trees, " +
                   crew + " skeletons on the night shift; chipper code " + h2.talentCode + " (" + crew + " skeletons, " + blades + " saw blades)");
    }

    /// <summary>A locked door filling a 2.4 x 2.8 doorway, with LOCKED on both sides.</summary>
    static GameObject LockedDoor(Transform parent, string name, Vector3 pos)
    {
        var d = Group(parent, name, pos);
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
        return d.gameObject;
    }

    /// <summary>The tree face that fills your view when you're caught (kept outside the dark rooms, so it stays bright).</summary>
    static Transform ScareFace(Transform parent)
    {
        var face = Group(parent, "ScareFace", Vector3.zero);
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
        return face;
    }

    // =================================================================== the 40 new skeletons (Audio/Extra/xs_<name>.ogg)

    // name, colour, act, where: 0/2/3/5 = that map room's free wall spots, L = lobby, B = Bonetown picnic, W = Bonetown wanderer, C = Root Cellar
    static readonly string[][] Extra40 = {
        new[] { "Ash", "#b2b2b2", "18", "0" }, new[] { "Slate", "#5a6a7a", "26", "0" }, new[] { "Rose", "#ff7090", "31", "0" },
        new[] { "Saffron", "#f4c430", "20", "0" }, new[] { "Tangerine", "#ff8c1a", "30", "0" },
        new[] { "Lemon", "#fff44f", "23", "2" }, new[] { "Sage", "#9caf88", "25", "2" }, new[] { "Seafoam", "#8fe3c8", "21", "2" },
        new[] { "Aqua", "#30d5f0", "28", "2" }, new[] { "Sapphire", "#1f4fd0", "27", "2" },
        new[] { "Periwinkle", "#8c9cff", "22", "3" }, new[] { "Orchid", "#da70d6", "32", "3" }, new[] { "Fuchsia", "#ff40c0", "24", "3" },
        new[] { "Cherry", "#d2042d", "19", "5" }, new[] { "Maroon", "#800030", "29", "5" }, new[] { "Copper", "#c87533", "13", "5" },
        new[] { "Bronze", "#cd7f32", "26", "5" }, new[] { "Caramel", "#c68e4a", "6", "5" },
        new[] { "Frost", "#cfefff", "20", "L" }, new[] { "Holly", "#2e8b57", "18", "L" },
        new[] { "Honey", "#ffc040", "28", "B" }, new[] { "Butter", "#fff0a0", "32", "B" }, new[] { "Jade", "#00a86b", "14", "B" }, new[] { "Blush", "#ffa0b0", "23", "B" },
        new[] { "Pistachio", "#93c572", "0", "W" }, new[] { "Salmon", "#fa8072", "0", "W" }, new[] { "Apricot", "#ffb07c", "0", "W" }, new[] { "Lilac", "#c8a2c8", "0", "W" },
        new[] { "Moss", "#6a8f3a", "25", "C" }, new[] { "Bark", "#7a5230", "29", "C" }, new[] { "Fern", "#4f9a4a", "30", "C" }, new[] { "Midnight", "#191970", "22", "C" },
        new[] { "Steel", "#8a9aa8", "27", "C" }, new[] { "Pewter", "#96a8a1", "21", "C" }, new[] { "Charcoal", "#36454f", "19", "C" }, new[] { "Mauve", "#b784a7", "24", "C" },
        new[] { "Burgundy", "#800020", "31", "C" }, new[] { "Mocha", "#8b5a3c", "11", "C" }, new[] { "Chartreuse", "#c8ff00", "14", "C" }, new[] { "Wine", "#722f37", "18", "C" } };

    /// <summary>A free-standing gag skeleton facing yaw, with its name floating over its head.</summary>
    static void ExtraSkeleton(Transform parent, string[] e, Vector3 pos, float yaw)
    {
        var face = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        GagSkeleton(parent, e[0], pos, yaw, 1f, int.Parse(e[2]), e[1], "Extra/xs_" + e[0].ToLower(),
                    new[] { BoneMat(e[0], e[1]) }, pos + new Vector3(0, 2.15f, 0) + face * 0.1f, face);
    }

    /// <summary>The 18 new skeletons that go in the original map's free wall spots.</summary>
    static void BuildMapSkeletons(Transform map, Dictionary<int, List<Slot>[]> slots)
    {
        var g = Group(map, "New skeletons", Vector3.zero);
        int placed = 0, want = 0;
        var turns = new Dictionary<int, int>();
        foreach (var e in Extra40)
        {
            if (!int.TryParse(e[3], out int room)) continue;
            want++;
            if (!turns.ContainsKey(room)) turns[room] = want % 2;
            int turn = turns[room];
            var sl = TakeSlots(slots[room], 1, ref turn);
            turns[room] = turn;
            if (sl == null) { report.Add("problem: no room left for the " + e[0] + " skeleton"); continue; }
            ExtraSkeleton(g, e, sl.pos, sl.yaw);
            placed++;
        }
        report.Add(placed + " of " + want + " new skeletons placed in the map");
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
            MakeWanderer(wg, wd.who, wd.hex, wd.route, wd.pace, wd.speed, wd.head, "Town/wan_" + wd.who.ToLower(), TownLine("wan_" + wd.who.ToLower()));
        // ---- four more wanderers and four picnickers (of the 40 new skeletons)
        var extraWan = new[] {
            new { who = "Pistachio", route = new[] { new Vector3(-16.5f, 0, 16.6f), new Vector3(-42.5f, 0, 16.6f) }, pace = new[] { 1f, 1f }, speed = 0.9f, head = 4000 },
            new { who = "Salmon", route = new[] { new Vector3(-41f, 0, -8.4f), new Vector3(-15.5f, 0, -8.4f) }, pace = new[] { 1f, 1f }, speed = 1.0f, head = 12000 },
            new { who = "Apricot", route = big, pace = new[] { 2.4f, 2.4f, 2.4f, 2.4f }, speed = 1.1f, head = 30000 },
            new { who = "Lilac", route = sq, pace = new[] { 1f, 1f, 1f, 1f }, speed = 0.95f, head = 10000 } };
        foreach (var wd in extraWan)
        {
            var e = Extra40.First(x => x[0] == wd.who);
            MakeWanderer(wg, wd.who, e[1], wd.route, wd.pace, wd.speed, wd.head, "Extra/xs_" + wd.who.ToLower(), ExtraLine(wd.who));
        }
        var picnic = new[] { new { who = "Honey", p = new Vector3(-25.2f, 0, -0.4f) }, new { who = "Butter", p = new Vector3(-34.5f, 0, 0.0f) },
                             new { who = "Jade", p = new Vector3(-25.2f, 0, 8.6f) }, new { who = "Blush", p = new Vector3(-34.6f, 0, 8.6f) } };
        foreach (var pc in picnic)
        {
            var toFountain = new Vector3(-30f, 0, 4f) - pc.p;
            float yaw = Mathf.Atan2(toFountain.x, toFountain.z) * Mathf.Rad2Deg;
            Rug(g, pc.p + toFountain.normalized * 0.5f, new Vector2(1.4f, 1.4f), "GiftRed", "CaneWhite");
            ExtraSkeleton(g, Extra40.First(x => x[0] == pc.who), pc.p, yaw);
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
        report.Add("Bonetown: " + homes.Length + " residents, " + band.Length + " in the band, " + wan.Length + " wanderers, 4 combo acts, " + placed + " gags; plus 4 picnickers and 4 more wanderers");
    }

    static void MakeWanderer(Transform wg, string who, string hex, Vector3[] route, float[] pace, float speed, int head, string clipName, string line)
    {
        var go = new GameObject(who + " Wanderer");
        go.transform.SetParent(wg, false);
        go.transform.localPosition = route[0];
        var ps = SkeletonRig(go.transform, "Rig", BoneMat(who, hex));
        var col = go.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.95f, 0); col.size = new Vector3(0.6f, 1.9f, 0.45f);
        go.layer = 17;   // Walkthrough: you can press them, but they walk through you instead of shoving you
        var wv = Add<FirWanderer>(go);
        wv.pivots = ps; wv.route = route; wv.pace = pace; wv.walkSpeed = speed; wv.headStartMs = head;
        wv.voice = Src(ps[2], Vector3.zero, Clip(clipName), false, 1f, 1f, 15f, false);
        wv.line = "<b><color=" + hex + ">" + who.ToUpper() + ":</color></b> " + line;
        wv.bubble = Bubble(go.transform, new Vector3(0, 2.3f, 0f));
        wv.bubbleObject = wv.bubble.gameObject; wv.bubbleTransform = wv.bubble.transform;
        SetInteract(wv, "Say hi to " + who, 3f);
    }

    static string ExtraLine(string who)
    {
        switch (who)
        {
            case "Pistachio": return "Just walking my invisible dog. He's a very good boy. Mostly bones.";
            case "Salmon": return "I swim upstream every single day. Well. I walk. Upstream is that way.";
            case "Apricot": return "Can't talk! Training for the skeleton marathon! It's all downhill from here!";
            case "Lilac": return "Lovely evening. Have you been down to the root cellar? Don't.";
        }
        return "";
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
    static FirHunt hunt, cellarHunt;
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
            // easy to spot: at eye height on the side walls, two per side, big, on dark plates
            new { p = new Vector3(-7.93f, 1.8f, -30.5f), n = Vector3.right, c = new Color(1f, 0.19f, 0.19f) },
            new { p = new Vector3(-7.93f, 1.8f, -34.5f), n = Vector3.right, c = new Color(1f, 0.88f, 0.19f) },
            new { p = new Vector3(7.93f, 1.8f, -30.5f), n = Vector3.left, c = new Color(0.25f, 1f, 0.31f) },
            new { p = new Vector3(7.93f, 1.8f, -34.5f), n = Vector3.left, c = new Color(0.25f, 0.5f, 1f) } };
        string[] digitName = { "RED", "YELLOW", "GREEN", "BLUE" };
        hunt.rainbowDigits = new TextMeshPro[4];
        for (int k = 0; k < 4; k++)
        {
            var plate = Group(map, "DigitPlate " + k, digitSpots[k].p);
            plate.localRotation = Face(digitSpots[k].n);
            Box(plate, "Plate", new Vector3(0, 0, 0.02f), new Vector3(0.9f, 1.05f, 0.02f), M("Board"), false);
            Text("Colour", plate, new Vector3(0, -0.38f, 0f), Quaternion.identity, new Vector2(0.85f, 0.2f), 0.13f, "<b>" + digitName[k] + "</b>").color = digitSpots[k].c;
            var t = Text("GlowDigit " + k, plate, new Vector3(0, 0.08f, 0f), Quaternion.identity, new Vector2(0.8f, 0.75f), 0.7f, "?");
            t.color = digitSpots[k].c;
            hunt.rainbowDigits[k] = t;
            huntOnly.Add(plate.gameObject);
        }
        hunt.rainbowDisplay = Keypad(map, "Rainbow keypad", new Vector3(2.7f, 1.35f, -41.86f), Vector3.forward, 1);
        // the order, spelled out over the keypad
        Box(map, "OrderStrip", new Vector3(2.7f, 2.35f, -41.88f), new Vector3(1.9f, 0.32f, 0.02f), M("Board"), false);
        Text("Order", map, new Vector3(2.7f, 2.35f, -41.86f), Face(Vector3.forward), new Vector2(1.85f, 0.28f), 0.15f,
             "<b><color=#ff3030>RED</color> > <color=#ffe030>YELLOW</color> > <color=#40ff50>GREEN</color> > <color=#4080ff>BLUE</color></b>").color = Color.white;

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
        hunt.stalkers = new[] { UB(st) }; hunt.stalkerBodies = new[] { sgo.transform }; hunt.stalkerVisuals = new[] { body.gameObject };
        hunt.rooms = 6;
        hunt.roomKind = new[] { 0, 1, 2, 0, 3, 0 };
        hunt.boardTitles = new[] { "THE POWER IS OUT", "LIGHTS OUT, RAINBOW", "THE TOY LOCK", "DECORATE THE TREE", "THE STAGE DOOR", "THE FURNACE" };
        hunt.boardTasks = new[] {
            "Find the 5 batteries hidden in the hall, then press the power box by the exit.",
            "4 glowing numbers are on the side walls (two each side). Type them on the exit keypad in this order:\n<color=#ff3030>red</color>, <color=#ffe030>yellow</color>, <color=#40ff50>green</color>, <color=#4080ff>blue</color>.",
            "The elves left 4 numbered notes. Press the toys on the exit panel in their order. A wrong toy starts you over.",
            "Find the 4 ornaments, then hang them on the bare tree by the far wall.",
            "Read the stage manager's memo, then type the code on the keypad by the exit (east side).",
            "Open the 3 gas valves, then pull the furnace lever. Lure the tree close first..." };
        hunt.winText = "The tree went into the furnace.";
        hunt.gameWhere = "the lobby";
        hunt.areaMinX = -9.5f; hunt.areaMaxX = 9.5f; hunt.areaTopZ = -6.2f;

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
        Poster(lobby, "poster_warning", new Vector3(6.97f, 1.85f, -4.7f), Vector3.left, 0.9f, 1.2f);
        Poster(lobby, "poster_original", new Vector3(6.97f, 1.85f, 1.0f), Vector3.left, 0.9f, 1.2f);
        Poster(lobby, "poster_safety", new Vector3(6.97f, 1.85f, 3.6f), Vector3.left, 0.9f, 1.2f);
        ExtraSkeleton(lobby, Extra40.First(x => x[0] == "Frost"), new Vector3(-3.3f, 0, -4.6f), 0f);
        ExtraSkeleton(lobby, Extra40.First(x => x[0] == "Holly"), new Vector3(3.3f, 0, -4.6f), 0f);
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
    /// <summary>
    /// Once everything is built: the throwables learn which game they belong to (by where they are), and each
    /// game gets its own materials to darken. The Root Cellar's are copied first, so the two games never share one.
    /// </summary>
    static void FinishHunt(Transform root)
    {
        if (hunt == null) return;
        var keepBright = new HashSet<string> { "Check_Button", "Check_ButtonGold", "Check_Paper" };
        Func<Material, bool> Darkens = m => m != null && m.name.StartsWith("Check_") && !m.name.StartsWith("Check_Bulb") && !m.name.StartsWith("Check_Lobby_")
                                           && !m.name.StartsWith("Check_Cellar_") && !keepBright.Contains(m.name);
        // the Root Cellar: its own copies of everything it uses
        var cellar = root.Find("Cellar");
        if (cellar != null && cellarHunt != null)
        {
            var copies2 = new Dictionary<Material, Material>();
            foreach (var r in cellar.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponent<TextMeshPro>() != null || !Darkens(r.sharedMaterial)) continue;
                if (!copies2.TryGetValue(r.sharedMaterial, out var c)) { c = PrefixedCopy(r.sharedMaterial, "Cellar_"); copies2[r.sharedMaterial] = c; }
                r.sharedMaterial = c;
            }
            cellarHunt.darkMats = copies2.Values.ToArray();
            var me2 = UB(cellarHunt);
            foreach (var th in cellar.GetComponentsInChildren<FirThrowable>(true)) { th.hunt = me2; th.stalkers = cellarHunt.stalkerBodies; }
            report.Add("the Root Cellar's dark: " + copies2.Count + " materials of its own");
        }
        var me = UB(hunt);
        foreach (var th in root.GetComponentsInChildren<FirThrowable>(true))
            if (cellar == null || !th.transform.IsChildOf(cellar)) { th.hunt = me; th.stalkers = hunt.stalkerBodies; }
        var map = root.Find("Map");
        var dark = new HashSet<Material>();
        foreach (var r in map.GetComponentsInChildren<Renderer>(true))
        {
            if (r.GetComponent<TextMeshPro>() != null) continue;
            if (Darkens(r.sharedMaterial)) dark.Add(r.sharedMaterial);
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

    static Material PrefixedCopy(Material m, string prefix)
    {
        string n = prefix + m.name.Replace("Check_", "");
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
