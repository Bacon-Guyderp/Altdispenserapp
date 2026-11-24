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
        mats.Clear(); report.Clear(); captions = null; meshCache.Clear(); rigs.Clear(); skelParts = null; stewieParts = null;
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
        BuildNorth(root);
        BuildMystery(root);
        BuildInn(root);
        BuildSpeed(root);
        BuildPlatforms(root);
        BuildNorthCarolina(root);
        BuildCircus(root);
        BuildSkits(root);
        BuildCornFlakes(root);
        BuildApartments(root);
        BuildTownDistricts(root);
        BuildCrystalCove(root);
        BuildCritterMeadow(root);
        BuildShadowBelow(root);
        BuildInkPlaza(root);
        BuildDreadDepths(root);
        BuildHerosGlade(root);
        BuildHoneycombHollow(root);
        BuildDragonShore(root);
        BuildHeroTower(root);
        BuildGloomCity(root);
        BuildMyrtleBeach(root);
        BuildBoone(root);
        BuildDachshunds(root);
        BuildExtremeChristmas(root);
        BuildCellbound(root);
        BuildMarkiplierRoom(root);
        BuildFlamingoRoom(root);
        BuildRyanTrahanRoom(root);
        BuildScreamRoom(root);
        BuildInventorsBackyard(root);
        BuildNotebookSchool(root);
        BuildSeabedTown(root);
        BuildBattleIsland(root);
        BuildContainment(root);
        BuildPortalGarage(root);
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
        rs.caption = CaptionFor(clip);
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
        c.lineText = clips.Select(CaptionFor).ToArray();
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
        A("InnRed", null, new Color(0.5f, 0.05f, 0.08f), 1, new Color(0.08f, 0f, 0f));
        A("InnGold", null, new Color(1f, 0.75f, 0.25f), 1, new Color(0.3f, 0.2f, 0.05f));
        A("InnBlack", null, new Color(0.08f, 0.04f, 0.05f), 1, Color.black);
        A("RidgeBlue1", null, new Color(0.35f, 0.5f, 0.75f), 1, new Color(0.05f, 0.08f, 0.12f));
        A("RidgeBlue2", null, new Color(0.45f, 0.6f, 0.85f), 1, new Color(0.08f, 0.1f, 0.15f));
        A("RidgeBlue3", null, new Color(0.6f, 0.72f, 0.92f), 1, new Color(0.1f, 0.12f, 0.18f));
        A("CornYellow", null, new Color(1f, 0.85f, 0.45f), 1, Color.black);
        A("Milk", null, new Color(0.97f, 0.97f, 0.95f), 1, new Color(0.15f, 0.15f, 0.15f));
        A("Brick", null, new Color(0.6f, 0.28f, 0.2f), 1, Color.black);
        A("GemPink", null, new Color(1f, 0.4f, 0.7f), 1, new Color(0.4f, 0.1f, 0.25f));
        A("GemPurple", null, new Color(0.6f, 0.3f, 0.9f), 1, new Color(0.2f, 0.08f, 0.3f));
        A("GemWhite", null, new Color(0.95f, 0.95f, 1f), 1, new Color(0.35f, 0.35f, 0.4f));
        A("DarkGround", null, new Color(0.1f, 0.05f, 0.16f), 1, new Color(0.02f, 0f, 0.04f));
        A("DarkCliff", null, new Color(0.5f, 0.2f, 0.55f), 1, new Color(0.12f, 0.03f, 0.12f));
        A("DarkGlowPink", null, new Color(1f, 0.35f, 0.8f), 1, new Color(0.6f, 0.15f, 0.45f));
        A("DarkGlowCyan", null, new Color(0.3f, 1f, 1f), 1, new Color(0.15f, 0.55f, 0.55f));
        A("InkOrange", null, new Color(1f, 0.5f, 0.05f), 1, new Color(0.3f, 0.12f, 0f));
        A("InkBlue", null, new Color(0.1f, 0.4f, 1f), 1, new Color(0f, 0.1f, 0.3f));
        A("TurfGrey", null, new Color(0.35f, 0.35f, 0.38f), 1, new Color(0f, 0f, 0f));
        A("CaveRock", null, new Color(0.25f, 0.22f, 0.3f), 1, new Color(0.02f, 0.02f, 0.03f));
        A("AlienGlow", null, new Color(0.3f, 1f, 0.5f), 1, new Color(0.15f, 0.5f, 0.25f));
        A("SuitOrange", null, new Color(1f, 0.45f, 0.1f), 1, new Color(0.1f, 0.04f, 0f));
        A("Visor", null, new Color(0.2f, 1f, 0.3f), 1, new Color(0.1f, 0.5f, 0.15f));
        A("Honey", null, new Color(1f, 0.75f, 0.2f), 1, new Color(0.2f, 0.12f, 0f));
        A("HeroRed", null, new Color(0.8f, 0.1f, 0.1f), 1, new Color(0.05f, 0f, 0f));
        A("HeroBlue", null, new Color(0.1f, 0.2f, 0.7f), 1, new Color(0f, 0f, 0.05f));
        A("GloomStone", null, new Color(0.15f, 0.15f, 0.2f), 1, new Color(0.01f, 0.01f, 0.02f));
        A("BeamWhite", null, new Color(0.95f, 0.95f, 0.85f), 1, new Color(0.6f, 0.6f, 0.5f));
        A("FoliageOrange", null, new Color(0.95f, 0.45f, 0.1f), 1, new Color(0.05f, 0.02f, 0f));
        A("FoliageRed", null, new Color(0.8f, 0.15f, 0.1f), 1, new Color(0.04f, 0f, 0f));
        A("DoxieBrown", null, new Color(0.55f, 0.28f, 0.1f), 1, new Color(0f, 0f, 0f));
        A("DoxieBlack", null, new Color(0.12f, 0.08f, 0.06f), 1, new Color(0f, 0f, 0f));
        A("Bun", null, new Color(0.85f, 0.6f, 0.3f), 1, new Color(0f, 0f, 0f));
        A("NightSky", null, new Color(0.12f, 0.15f, 0.3f), 1, new Color(0.03f, 0.04f, 0.1f));
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

    static readonly string[] Scripts = { "FirCheckHub", "FirCheckButton", "FirCheckTyped", "FirCheckSkeleton", "FirCheckTree", "FirRainbowSkeleton", "FirSkeletonCombo", "FirGag", "FirSkeletonGag", "FirCloset", "FirThrowable", "FirShare", "FirHunt", "FirHuntButton", "FirStalker", "FirWanderer", "FirTicker", "FirRide", "FirSeat", "FirTeleport", "FirNight", "FirEncounter", "FirTiles", "FirSay", "FirPortal", "FirPad", "FirVoxel", "FirCodeLock", "FirBoost", "FirResident", "FirInkTurf", "FirChannels" };   // (the Root Cellar reuses FirHunt and FirStalker)

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

    // =================================================================== captions (Data/captions.txt, written by make_voices.py)

    static Dictionary<string, string> captions;

    static string CaptionFor(string clip)
    {
        if (captions == null)
        {
            captions = new Dictionary<string, string>();
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/captions.txt");
            if (ta == null) report.Add("problem: Data/captions.txt is missing, so no captions");
            else foreach (var raw in ta.text.Split('\n'))
            {
                var l = raw.TrimEnd('\r');
                int bar = l.IndexOf('|');
                if (l.StartsWith("#") || bar < 0) continue;
                captions[l.Substring(0, bar)] = l.Substring(bar + 1);
            }
        }
        if (string.IsNullOrEmpty(clip)) return "";
        var key = clip.Substring(clip.LastIndexOf('/') + 1);
        return captions.TryGetValue(key, out var c) ? c : "";
    }

    // =================================================================== the Marrow Arms: Bonetown's apartment building (through Bonetown's west doorway)

    const float AptH = 3.4f;

    /// <summary>A wall along x (at z) from xa to xb, with 1.3 m doorways at the given x centres.</summary>
    static void WallAlongX(Transform g, string mat, float z, float xa, float xb, params float[] doors) { WallX(g, mat, z, xa, xb, 0f, AptH, doors); }

    static void WallX(Transform g, string mat, float z, float xa, float xb, float y0, float h, params float[] doors)
    {
        float lo = Mathf.Min(xa, xb), hi = Mathf.Max(xa, xb);
        var cuts = doors.OrderBy(d => d).ToList();
        float from = lo;
        foreach (var d in cuts)
        {
            if (d - 0.65f > from) Box(g, "Wall", new Vector3((from + d - 0.65f) / 2f, y0 + h / 2f, z), new Vector3(d - 0.65f - from, h, 0.15f), M(mat));
            Box(g, "Lintel", new Vector3(d, y0 + (2.3f + h) / 2f, z), new Vector3(1.3f, h - 2.3f, 0.15f), M(mat));
            from = d + 0.65f;
        }
        if (hi > from) Box(g, "Wall", new Vector3((from + hi) / 2f, y0 + h / 2f, z), new Vector3(hi - from, h, 0.15f), M(mat));
    }

    static void WallAlongZ(Transform g, string mat, float x, float za, float zb) { WallZ(g, mat, x, za, zb, 0f, AptH); }

    static void WallZ(Transform g, string mat, float x, float za, float zb, float y0, float h)
    {
        float lo = Mathf.Min(za, zb), hi = Mathf.Max(za, zb);
        Box(g, "Wall", new Vector3(x, y0 + h / 2f, (lo + hi) / 2f), new Vector3(0.15f, h, hi - lo), M(mat));
    }

    /// <summary>A little car (for the parking lot).</summary>
    static void Car(Transform g, Vector3 at, string paint)
    {
        Box(g, "CarBody", at + new Vector3(0, 0.55f, 0), new Vector3(3.6f, 0.7f, 1.6f), M(paint));
        Box(g, "CarCabin", at + new Vector3(-0.2f, 1.2f, 0), new Vector3(2f, 0.6f, 1.5f), M("Glass"), false);
        foreach (float wx in new[] { -1.2f, 1.2f }) foreach (float wz in new[] { -0.8f, 0.8f })
            DPrim(PrimitiveType.Cylinder, "Wheel", g, at + new Vector3(wx, 0.3f, wz), new Vector3(0.6f, 0.1f, 0.6f), M("Black"), Quaternion.Euler(90, 0, 0));
    }

    /// <summary>A window seen from outside: a frame and a warm glow.</summary>
    static void OutWindow(Transform g, Vector3 at, Vector3 normal)
    {
        var rot = Face(normal);
        var w = Group(g, "Window", at);
        w.localRotation = rot;
        Prim(PrimitiveType.Cube, "Frame", w, Vector3.zero, new Vector3(1.7f, 1.3f, 0.04f), M("CaneWhite"));
        Prim(PrimitiveType.Cube, "Glow", w, new Vector3(0, 0, -0.025f), new Vector3(1.5f, 1.1f, 0.02f), M("GlowWarm"));
        Prim(PrimitiveType.Cube, "Bar", w, new Vector3(0, 0, -0.04f), new Vector3(0.05f, 1.1f, 0.02f), M("CaneWhite"));
    }

    class AptRoom { public Vector3 inside; public int zone; public bool open; }
    class AptSpot { public Vector3 pos; public float yaw; public int room; }

    /// <summary>Where a stop joins the walking graph: its room's doorway, or (in an open space) the nearest node in the same zone.</summary>
    static int Anchor(Vector3 spot, int room, List<AptRoom> rooms, List<int> roomNode, List<Vector3> nodes, List<int> nodeZone)
    {
        if (!rooms[room].open) return roomNode[room];
        int best = -1; float bd = 1e9f;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodeZone[i] != rooms[room].zone || Mathf.Abs(nodes[i].y - spot.y) > 1f) continue;
            float d = Vector3.Distance(nodes[i], spot);
            if (d < bd) { bd = d; best = i; }
        }
        if (best < 0) report.Add("problem: no way to reach the spot at " + spot);
        return Mathf.Max(best, 0);
    }

    /// <summary>The shortest way between two graph nodes (both ends included).</summary>
    static List<int> Dijkstra(int a, int b, List<Vector3> nodes, List<List<int>> links)
    {
        var dist = Enumerable.Repeat(float.MaxValue, nodes.Count).ToArray();
        var prev = Enumerable.Repeat(-1, nodes.Count).ToArray();
        var done = new bool[nodes.Count];
        dist[a] = 0f;
        for (int step = 0; step < nodes.Count; step++)
        {
            int u = -1;
            for (int i = 0; i < nodes.Count; i++) if (!done[i] && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0 || dist[u] == float.MaxValue) break;
            done[u] = true;
            if (u == b) break;
            foreach (int v in links[u])
            {
                float nd = dist[u] + Vector3.Distance(nodes[u], nodes[v]);
                if (nd < dist[v]) { dist[v] = nd; prev[v] = u; }
            }
        }
        var way = new List<int>();
        if (dist[b] == float.MaxValue) { report.Add("problem: the walking graph has no way from " + nodes[a] + " to " + nodes[b]); way.Add(a); way.Add(b); return way; }
        for (int v = b; v >= 0; v = prev[v]) way.Insert(0, v);
        return way;
    }

    static void BuildApartments(Transform root)
    {
        const float dz = 7f, dx = -8f;   // the building is drawn in its own frame: 8 m west (room for a front yard) and 7 m north (its door lines up with Bonetown's at z 11)
        var off = new Vector3(dx, 0, dz);
        var g = Group(root, "The Marrow Arms", off);
        float X0 = -52f, X1 = -88f, Z0 = -12f, Z1 = 20f;
        Text("WaySign", root, new Vector3(-47.75f, 3.9f, 11f), Face(Vector3.right), new Vector2(5f, 0.8f), 0.4f, "<b>THE MARROW ARMS</b>  >\n<size=50%>apartments  ·  residents only  ·  (and visitors)  ·  (and everyone)</size>").color = new Color(0.25f, 0.2f, 0.35f);
        // ---- the shell: floor, ceiling, outer walls
        Box(g, "Floor", new Vector3((X0 + X1) / 2f, -0.1f, (Z0 + Z1) / 2f), new Vector3(36f, 0.2f, 32f), M("Floor2"));
        Box(g, "Ceiling", new Vector3((X0 + X1) / 2f, AptH + 0.1f, (Z0 + Z1) / 2f), new Vector3(36f, 0.2f, 32f), M("Ceiling"));
        WallAlongX(g, "Wall2", Z1, X0, X1);
        WallAlongX(g, "Wall2", Z0, X0, X1);
        WallAlongZ(g, "Wall2", X1, Z0, Z1);
        WallAlongZ(g, "Wall2", X0, Z0, 2.8f);
        WallAlongZ(g, "Wall2", X0, 5.2f, Z1);
        Box(g, "EntranceTop", new Vector3(X0, (3f + AptH) / 2f, 4f), new Vector3(0.15f, AptH - 3f, 2.4f), M("Wall2"));
        // ---- the hallway and the flats along it
        float[] xc = { -61.75f, -69.25f, -76.75f, -84.25f };
        WallAlongX(g, "Wall", 5.5f, -58f, X1, xc);
        WallAlongX(g, "Wall", 2.5f, -58f, X1, xc);
        foreach (float px in new[] { -65.5f, -73f, -80.5f }) { WallAlongZ(g, "Wall", px, 5.5f, Z1); WallAlongZ(g, "Wall", px, Z0, 2.5f); }
        WallAlongZ(g, "Wall2", -58f, Z0, 2.5f);
        WallAlongZ(g, "Wall2", -58f, 5.5f, Z1);
        WallAlongX(g, "Wall2", 9f, X0, -58f, -55f);
        WallAlongX(g, "Wall2", -1f, X0, -58f, -55f);
        Box(g, "Runner", new Vector3(-73f, 0.006f, 4f), new Vector3(30f, 0.01f, 1.6f), M("Rug"), false);
        for (int k = 0; k < 4; k++) PendantLamp(g, new Vector3(-62f - k * 7.5f, AptH, 4f), 0.3f);
        Text("FireExit", g, new Vector3(X1 + 0.09f, 1.6f, 4f), Face(Vector3.right), new Vector2(2.6f, 0.8f), 0.2f, "<b>FIRE EXIT</b>\n<size=60%>(painted on. please do not run into it again)</size>").color = new Color(0.2f, 0.7f, 0.3f);
        // ---- the rooms, and a graph of the ways between them (worked out into each resident's walks below)
        const float F2 = AptH + 0.2f;   // the second floor (the top of the ground floor's ceiling)
        var rooms = new List<AptRoom>();
        var nodes = new List<Vector3>(); var nodeZone = new List<int>(); var links = new List<List<int>>();
        const int ZIn = 0, ZRoom = 1, ZYard = 2, ZTown = 3, ZUp = 4;
        Func<float, float, float, int, int> N = (x, y, z, zone) => { nodes.Add(new Vector3(x, y, z) + off); nodeZone.Add(zone); links.Add(new List<int>()); return nodes.Count - 1; };
        Action<int, int> L = (p, q) => { links[p].Add(q); links[q].Add(p); };
        Func<int, int, bool, int> AddRoom = (inNode, zone, open) => { rooms.Add(new AptRoom { inside = inNode >= 0 ? nodes[inNode] : Vector3.zero, zone = zone, open = open }); return rooms.Count - 1; };
        var roomNode = new List<int>();
        Func<int, int, bool, int> Room = (inNode, zone, open) => { roomNode.Add(inNode); return AddRoom(inNode, zone, open); };
        int nHub = N(-57f, 0, 4f, ZIn);
        var hall = new int[4];
        for (int u = 0; u < 4; u++) { hall[u] = N(xc[u], 0, 4f, ZIn); L(u == 0 ? nHub : hall[u - 1], hall[u]); }
        int nLob = N(-55f, 0, 4f, ZIn); L(nHub, nLob);
        int nGymO = N(-55f, 0, 7.8f, ZIn), nGymI = N(-55f, 0, 10.2f, ZRoom); L(nLob, nGymO); L(nGymO, nGymI);
        int nComO = N(-55f, 0, 0.2f, ZIn), nComI = N(-55f, 0, -2.2f, ZRoom); L(nLob, nComO); L(nComO, nComI);
        int nEin = N(-53.2f, 0, 4f, ZIn); L(nLob, nEin);
        int nEout = N(-50.8f, 0, 4f, ZYard); L(nEin, nEout);
        int nY = N(-46f, 0, 4f, ZYard); L(nEout, nY);
        int nB = N(-39.4f, 0, 4f, ZTown); L(nY, nB);
        int nYs = N(-46f, 0, -15.15f, ZYard); L(nY, nYs);
        int nYs2 = N(-46f, 0, -17f, ZYard); L(nYs, nYs2);
        int nYws = N(-98f, 0, -17f, ZYard); L(nYs2, nYws);
        int nYn = N(-46f, 0, 29f, ZYard); L(nY, nYn);
        int nYw = N(-98f, 0, 29f, ZYard); L(nYn, nYw); L(nYw, nYws);
        int nR0 = N(-52f, 0, -15.15f, ZYard); L(nYs, nR0);
        int nR1 = N(-58.2f, F2, -15.15f, ZUp); L(nR0, nR1);
        int nLand = N(-59.2f, F2, -13.4f, ZUp); L(nR1, nLand);
        int nBalE = N(-55f, F2, -13.2f, ZUp); L(nBalE, nLand);
        var bal = new int[4];
        for (int u = 0; u < 4; u++) { bal[u] = N(xc[u], F2, -13.2f, ZUp); L(u == 0 ? nLand : bal[u - 1], bal[u]); }
        int nW1 = N(-89.1f, F2, -13.2f, ZUp); L(bal[3], nW1);
        int nW2 = N(-89.1f, F2, 3.8f, ZUp); L(nW1, nW2);
        int nW3 = N(-86f, F2, 3.8f, ZUp); L(nW2, nW3);
        int nRoofS = N(-55f, F2, -9f, ZUp); L(nBalE, nRoofS);
        int nRoofN = N(-55f, F2, 8f, ZUp); L(nRoofS, nRoofN);
        int nT = N(-70f, F2, 11f, ZUp); L(nRoofN, nT);
        int nT2 = N(-84f, F2, 11f, ZUp); L(nT, nT2); L(nW3, nT2);
        int rLobby = Room(-1, ZIn, true);
        int rGym = Room(nGymI, ZRoom, false);
        int rCommon = Room(nComI, ZRoom, false);
        int lIn = N(xc[0], 0, 1.4f, ZRoom); L(lIn, hall[0]);
        int rLaundry = Room(lIn, ZRoom, false);
        var rApt = new int[12];
        for (int u = 0; u < 4; u++) { int ni = N(xc[u], 0, 6.6f, ZRoom); L(ni, hall[u]); rApt[u + 1] = Room(ni, ZRoom, false); }
        for (int u = 1; u < 4; u++) { int ni = N(xc[u], 0, 1.4f, ZRoom); L(ni, hall[u]); rApt[u + 4] = Room(ni, ZRoom, false); }
        for (int u = 0; u < 4; u++) { int ni = N(xc[u], F2, -10.9f, ZRoom); L(ni, bal[u]); rApt[u + 8] = Room(ni, ZRoom, false); }
        int rHall = Room(-1, ZIn, true);
        int rOut = Room(-1, ZTown, true);
        int rRoof = Room(-1, ZUp, true);
        int rYard = Room(-1, ZYard, true);
        // ---- the places people go
        var spots = new Dictionary<string, AptSpot>();
        Action<string, float, float, float, int, float> S6 = (n, x, z, yaw, r, y) => spots[n] = new AptSpot { pos = new Vector3(x, y, z) + off, yaw = yaw, room = r };
        Action<string, float, float, float, int> S = (n, x, z, yaw, r) => S6(n, x, z, yaw, r, 0f);
        string[] names = { "", "Clavicle", "Mortimer", "Granny Tibia", "Coccyx", "Ribsy", "Phalange", "Scapula", "Humerus", "Mandible", "Stapes", "Hyoid" };
        string[] hexes = { "", "#ff8a65", "#9575cd", "#b0bec5", "#4db6ac", "#f06292", "#aed581", "#ffd54f", "#ffb300", "#8bc34a", "#80deea", "#ff8a80" };
        for (int a = 1; a <= 11; a++)
        {
            bool south = a >= 5 && a <= 7, upstairs = a >= 8;
            float c = upstairs ? xc[a - 8] : south ? xc[a - 4] : xc[a - 1];
            float y0 = upstairs ? F2 : 0f, ceil = upstairs ? 3.2f : AptH;
            Func<float, float> Zm = z => south ? 8f - z : upstairs ? z - 17.5f : z;
            Func<float, float> Ym = y => south ? 180f - y : y;
            Func<Vector3, Vector3> P = v => new Vector3(c + v.x, v.y + y0, Zm(v.z));
            int r = rApt[a];
            S6("a" + a + ":bed", c + 2.2f, Zm(17.6f), Ym(180f), r, y0);
            S6("a" + a + ":cook", c - 2.2f, Zm(18.6f), Ym(0f), r, y0);
            S6("a" + a + ":chair", c - 2.0f, Zm(12.1f), Ym(0f), r, y0);
            S6("a" + a + ":couch", c + 2.55f, Zm(10.5f), -90f, r, y0);
            S6("a" + a + ":mid", c + 0.3f, Zm(14f), Ym(180f), r, y0);
            S6("a" + a + ":plant", c - 2.6f, Zm(7.4f), -90f, r, y0);
            var ag = Group(g, "Apartment " + a + " (" + names[a] + ")", Vector3.zero);
            Box(ag, "Bed", P(new Vector3(2.2f, 0.25f, 17.6f)), new Vector3(1.3f, 0.5f, 2.3f), M("DeskWood"));
            Box(ag, "Blanket", P(new Vector3(2.2f, 0.51f, 17.3f)), new Vector3(1.32f, 0.04f, 1.6f), M(new[] { "GiftRed", "GiftBlue", "GiftGreen", "Rug", "GiftGold", "Curtain", "Wainscot", "InkOrange", "GiftGreen", "InkBlue", "BonePink" }[a - 1]), false);
            Box(ag, "Pillow", P(new Vector3(2.2f, 0.56f, 18.45f)), new Vector3(1.0f, 0.12f, 0.4f), M("CaneWhite"), false);
            Box(ag, "Counter", P(new Vector3(-2.2f, 0.45f, 19.45f)), new Vector3(2.8f, 0.9f, 0.7f), M("Wainscot"));
            foreach (float bx in new[] { -2.8f, -2.2f }) DPrim(PrimitiveType.Cylinder, "Burner", ag, P(new Vector3(bx, 0.91f, 19.4f)), new Vector3(0.35f, 0.01f, 0.35f), M("Black"));
            Box(ag, "Table", P(new Vector3(-2.0f, 0.38f, 13.0f)), new Vector3(1.2f, 0.76f, 0.8f), M("DeskWood"));
            Box(ag, "Couch", P(new Vector3(2.6f, 0.22f, 10.5f)), new Vector3(0.8f, 0.45f, 2.0f), M("Seat"));
            Box(ag, "CouchBack", P(new Vector3(3.05f, 0.6f, 10.5f)), new Vector3(0.15f, 0.8f, 2.0f), M("Seat"), false);
            Box(ag, "TVStand", P(new Vector3(-1.2f, 0.3f, 10.5f)), new Vector3(0.4f, 0.6f, 1.2f), M("Black"));
            Box(ag, "TV", P(new Vector3(-1.15f, 0.95f, 10.5f)), new Vector3(0.06f, 0.6f, 1.0f), M("GlowGreen"), false);
            Box(ag, "Rug", P(new Vector3(0.5f, 0.008f, 12f)), new Vector3(3f, 0.01f, 4f), M("Rug"), false);
            DPrim(PrimitiveType.Cylinder, "PlantPot", ag, P(new Vector3(-3.3f, 0.25f, 7.4f)), new Vector3(0.4f, 0.25f, 0.4f), M("Pot"));
            DPrim(PrimitiveType.Sphere, "Plant", ag, P(new Vector3(-3.3f, 0.8f, 7.4f)), new Vector3(0.7f, 0.8f, 0.7f), M("Needles"));
            Box(ag, "Window", P(new Vector3(0f, 1.9f, 19.9f)), new Vector3(2.2f, 1.2f, 0.02f), M("WindowSnow"), false);
            PendantLamp(ag, P(new Vector3(0f, ceil, 13f)), 0.4f);
            // the number by the door, out in the hallway
            float signZ = south ? 2.58f : upstairs ? -12.1f : 5.42f;
            Text("DoorSign", ag, new Vector3(c + 1.1f, y0 + 2.0f, signZ), Face(south ? Vector3.forward : Vector3.back), new Vector2(1.0f, 0.5f), 0.14f,
                 "<b>APT " + a + "</b>\n<size=70%>" + names[a].ToUpper() + "</size>").color = new Color(0.95f, 0.9f, 0.75f);
            // what makes it theirs
            if (a == 1) { Box(ag, "YogaMat", P(new Vector3(0.3f, 0.01f, 15.5f)), new Vector3(0.7f, 0.02f, 1.8f), M("GiftGreen"), false); foreach (float ddx in new[] { -0.3f, 0.3f }) DPrim(PrimitiveType.Cylinder, "Dumbbell", ag, P(new Vector3(1.2f + ddx, 0.1f, 16f)), new Vector3(0.2f, 0.1f, 0.2f), M("Metal")); }
            if (a == 2) { DPrim(PrimitiveType.Sphere, "GuitarBody", ag, P(new Vector3(3.2f, 0.45f, 12.2f)), new Vector3(0.45f, 0.55f, 0.15f), M("DeskWood")); DPrim(PrimitiveType.Cube, "GuitarNeck", ag, P(new Vector3(3.2f, 1.0f, 12.2f)), new Vector3(0.07f, 0.7f, 0.04f), M("Black")); Box(ag, "Poster", P(new Vector3(-3.66f, 1.8f, 15f)), new Vector3(0.02f, 1.0f, 0.7f), M("WallDark"), false); }
            if (a == 3) { for (int k = 0; k < 3; k++) { DPrim(PrimitiveType.Cylinder, "Pot", ag, P(new Vector3(-3.3f, 0.2f, 8.4f + k * 0.8f)), new Vector3(0.3f, 0.2f, 0.3f), M("Pot")); DPrim(PrimitiveType.Sphere, "Plastic plant", ag, P(new Vector3(-3.3f, 0.6f, 8.4f + k * 0.8f)), Vector3.one * 0.5f, M("GiftGreen")); } }
            if (a == 4) { Box(ag, "Monitor", P(new Vector3(-2.0f, 1.0f, 13.3f)), new Vector3(0.6f, 0.4f, 0.04f), M("GlowGreen"), false); for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cylinder, "SoupCan", ag, P(new Vector3(-3.0f + (k % 3) * 0.2f, 0.98f + (k / 3) * 0.2f, 19.4f)), new Vector3(0.14f, 0.1f, 0.14f), M("GiftRed")); }
            if (a == 5) { for (int k = 0; k < 8; k++) Box(ag, "Photo", P(new Vector3(-3.66f, 1.4f + (k % 2) * 0.5f, 10f + (k / 2) * 0.6f)), new Vector3(0.02f, 0.35f, 0.45f), M(k % 3 == 0 ? "Paper" : k % 3 == 1 ? "GiftGold" : "Book1"), false); }
            if (a == 6) { for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cylinder, "Broom", ag, P(new Vector3(-3.5f, 0.75f, 15f + k * 0.35f)), new Vector3(0.05f, 0.75f, 0.05f), M("DeskWood")); }
            if (a == 8) { DPrim(PrimitiveType.Cylinder, "MicStand", ag, P(new Vector3(0.3f, 0.7f, 16f)), new Vector3(0.04f, 0.7f, 0.04f), M("Black")); DPrim(PrimitiveType.Sphere, "Mic", ag, P(new Vector3(0.3f, 1.45f, 16f)), Vector3.one * 0.1f, M("Metal")); Box(ag, "Poster", P(new Vector3(-3.66f, 1.6f, 15f)), new Vector3(0.02f, 1f, 0.7f), M("InkOrange"), false); }
            if (a == 9) { for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Sphere, "Seedling", ag, P(new Vector3(-3.3f, 0.95f, 15f + k * 0.4f)), Vector3.one * 0.15f, M("GiftGreen")); Box(ag, "SeedShelf", P(new Vector3(-3.3f, 0.45f, 15.6f)), new Vector3(0.6f, 0.9f, 1.8f), M("DeskWood")); }
            if (a == 10) { for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Sphere, "Star chart", ag, P(new Vector3(-3.66f, 1.2f + (k % 2) * 0.7f, 9f + (k / 2) * 1.2f)), new Vector3(0.02f, 0.25f, 0.25f), M("GlowGold")); }
            if (a == 11) { for (int k = 0; k < 9; k++) Box(ag, "Moving box", P(new Vector3(-0.8f + (k % 3) * 0.55f, 0.25f + (k / 3) * 0.5f, 16.5f)), new Vector3(0.5f, 0.48f, 0.5f), M("Cardboard"), false); }
            if (a == 7) { DPrim(PrimitiveType.Sphere, "DiscoBall", ag, P(new Vector3(0.3f, 2.9f, 14f)), Vector3.one * 0.4f, M("Glint")); Box(ag, "Mirror", P(new Vector3(-3.66f, 1.5f, 15f)), new Vector3(0.02f, 1.8f, 2.4f), M("Glass"), false); }
        }
        // the gym, the common room, the laundry, the lobby, the hallway, outside
        var gy = Group(g, "The Gym", Vector3.zero);
        Box(gy, "Treadmill", new Vector3(-52.9f, 0.1f, 12f), new Vector3(0.9f, 0.2f, 2f), M("Black"));
        Box(gy, "TreadmillBar", new Vector3(-52.9f, 1.0f, 13f), new Vector3(0.9f, 0.08f, 0.08f), M("Metal"), false);
        Box(gy, "Rack", new Vector3(-55f, 0.4f, 19.5f), new Vector3(2f, 0.8f, 0.5f), M("Metal"));
        Box(gy, "Mirror", new Vector3(-55f, 1.8f, 19.9f), new Vector3(4f, 1.6f, 0.02f), M("Glass"), false);
        Box(gy, "Mat", new Vector3(-55.5f, 0.01f, 14f), new Vector3(3f, 0.02f, 3f), M("GiftBlue"), false);
        Text("GymSign", gy, new Vector3(-55f, 2.9f, 9.08f), Face(Vector3.forward), new Vector2(3f, 0.4f), 0.2f, "<b>GYM</b> <size=60%>(one dumbbell. please share)</size>").color = Color.white;
        S("gym:jacks", -55.5f, 13.5f, 0f, rGym); S("gym:lift", -55.5f, 17.8f, 0f, rGym); S("gym:wipe", -56.8f, 16f, -90f, rGym); S("gym:fix", -53.8f, 12f, 90f, rGym);
        var cm = Group(g, "The Common Room", Vector3.zero);
        Box(cm, "Couch", new Vector3(-55f, 0.22f, -6.9f), new Vector3(2.6f, 0.45f, 0.8f), M("Seat"));
        Box(cm, "CouchBack", new Vector3(-55f, 0.6f, -6.45f), new Vector3(2.6f, 0.8f, 0.15f), M("Seat"), false);
        Box(cm, "TV", new Vector3(-55f, 1.3f, -11.85f), new Vector3(2f, 1.1f, 0.05f), M("GlowGreen"), false);
        Box(cm, "Table", new Vector3(-52.8f, 0.38f, -9.5f), new Vector3(0.8f, 0.76f, 1.2f), M("DeskWood"));
        Box(cm, "DanceFloor", new Vector3(-55.5f, 0.006f, -3.6f), new Vector3(3f, 0.01f, 2.6f), M("Stage"), false);
        DPrim(PrimitiveType.Sphere, "DiscoBall", cm, new Vector3(-55.5f, 2.9f, -3.6f), Vector3.one * 0.4f, M("Glint"));
        Text("CommonSign", cm, new Vector3(-55f, 2.9f, -1.08f), Face(Vector3.back), new Vector2(3f, 0.4f), 0.2f, "<b>COMMON ROOM</b> <size=60%>bingo tuesdays</size>").color = Color.white;
        S("common:couchA", -55.6f, -7.2f, 180f, rCommon); S("common:couchB", -54.4f, -7.2f, 180f, rCommon); S("common:dance", -55.5f, -3.6f, 180f, rCommon); S("common:read", -53.6f, -9.5f, 90f, rCommon);
        var ld = Group(g, "The Laundry", Vector3.zero);
        float lx = xc[0];
        for (int k = 0; k < 3; k++)
        {
            Box(ld, "Washer", new Vector3(lx - 2.6f + k * 1.1f, 0.45f, -11.4f), new Vector3(0.9f, 0.9f, 0.8f), M("Porcelain"));
            DPrim(PrimitiveType.Cylinder, "Porthole", ld, new Vector3(lx - 2.6f + k * 1.1f, 0.5f, -10.99f), new Vector3(0.5f, 0.01f, 0.5f), M("Glass"), Quaternion.Euler(90, 0, 0));
        }
        Box(ld, "FoldingTable", new Vector3(lx, 0.45f, -6f), new Vector3(2.4f, 0.9f, 1f), M("DeskWood"));
        Text("LaundrySign", ld, new Vector3(lx + 1.1f, 2.0f, 2.58f), Face(Vector3.forward), new Vector2(1.2f, 0.5f), 0.14f, "<b>LAUNDRY</b>\n<size=70%>socks go in. socks do not come out</size>").color = new Color(0.95f, 0.9f, 0.75f);
        var dryer = MovingGag(ld, "The Dryer", new Vector3(lx + 2.3f, 0, -11.2f), new Vector3(0, 0.6f, 0.2f), new Vector3(1.1f, 1.2f, 1f), "Apart/ap_dryer", "The dryer ate a sock. And a femur. Femur would like it back.", new Vector3(0, 2f, 0.6f));
        Box(dryer.transform, "Dryer", new Vector3(0, 0.5f, 0), new Vector3(1f, 1f, 0.8f), M("Porcelain"));
        var drum = Group(dryer.transform, "Drum", new Vector3(0, 0.55f, 0.41f));
        Prim(PrimitiveType.Cylinder, "Door", drum, Vector3.zero, new Vector3(0.6f, 0.01f, 0.6f), M("Glass")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Cube, "Sock", drum, new Vector3(0.12f, 0.08f, -0.02f), new Vector3(0.2f, 0.08f, 0.02f), M("GiftRed"));
        Prim(PrimitiveType.Cube, "Femur", drum, new Vector3(-0.08f, -0.08f, -0.02f), new Vector3(0.3f, 0.05f, 0.02f), M("BoneWhite"));
        dryer.target = drum; dryer.rotateBy = new Vector3(0, 0, 720f); dryer.inTime = 2f; dryer.wobble = 4f;
        S("laundry:foldA", lx - 0.6f, -5.2f, 180f, rLaundry); S("laundry:foldB", lx + 0.6f, -6.8f, 0f, rLaundry); S("laundry:fix", lx - 1.5f, -10.3f, 180f, rLaundry);
        var lb = Group(g, "The Lobby", Vector3.zero);
        Box(lb, "Desk", new Vector3(-53.5f, 0.4f, 7.7f), new Vector3(1.6f, 0.8f, 0.6f), M("DeskWood"));
        Box(lb, "Mat", new Vector3(-53f, 0.008f, 4f), new Vector3(1.6f, 0.01f, 2.2f), M("Rug"), false);
        var mail = MovingGag(lb, "The Mailboxes", new Vector3(-56.8f, 0, -0.85f), new Vector3(0, 1.3f, 0.1f), new Vector3(1.8f, 1f, 0.3f), "Apart/ap_mail", "You've got mail! It's a bill. For the mailbox.", new Vector3(0, 2.4f, 0.4f));
        for (int k = 0; k < 8; k++) Box(mail.transform, "Box " + (k + 1), new Vector3(-0.66f + (k % 4) * 0.44f, 1.1f + (k / 4) * 0.4f, 0f), new Vector3(0.4f, 0.36f, 0.12f), M("Brass"), false);
        var letter = Group(mail.transform, "Letter", new Vector3(-0.22f, 1.1f, 0.08f));
        Prim(PrimitiveType.Cube, "Envelope", letter, Vector3.zero, new Vector3(0.3f, 0.2f, 0.01f), M("Paper"));
        mail.target = letter; mail.moveBy = new Vector3(0, 0.4f, 0.4f); mail.spin = 360f; mail.hideWhenIdle = true;
        var lift = MovingGag(lb, "The Lift", new Vector3(-57.92f, 0, 7.4f), new Vector3(0.3f, 1.1f, 0), new Vector3(0.6f, 2.2f, 1.6f), "Apart/ap_lift", "Going up! ...Just kidding. It has never gone up.", new Vector3(0.6f, 2.6f, 0));
        Box(lift.transform, "Shaft", new Vector3(0.03f, 1.1f, 0), new Vector3(0.02f, 2.2f, 1.4f), M("BrickWall"), false);
        var liftDoor = Group(lift.transform, "Door", new Vector3(0.06f, 1.1f, 0));
        Prim(PrimitiveType.Cube, "Panel", liftDoor, Vector3.zero, new Vector3(0.03f, 2.2f, 1.4f), M("Metal"));
        Text("Dial", lift.transform, new Vector3(0.08f, 2.5f, 0), Face(Vector3.right), new Vector2(1.4f, 0.3f), 0.13f, "<b>LIFT</b>  <size=70%>floors 2-9</size>").color = new Color(1f, 0.85f, 0.4f);
        lift.target = liftDoor; lift.moveBy = new Vector3(0, 0, 1.35f); lift.inTime = 1.2f; lift.holdTime = 2f;
        Text("Stairs", lb, new Vector3(-57.91f, 1.7f, 0.8f), Face(Vector3.right), new Vector2(1.6f, 0.8f), 0.13f, "<b>STAIRS</b>\n<size=70%>to floors 3-9: closed for bone density testing.\nfloor 2: use the ramp outside</size>").color = new Color(0.9f, 0.3f, 0.3f);
        Text("Directory", lb, new Vector3(-52.09f, 1.6f, 7.4f), Face(Vector3.left), new Vector2(2.2f, 1.6f), 0.11f,
             "<b>RESIDENTS</b>\n1 Clavicle   2 Mortimer   3 Granny Tibia\n4 Coccyx   5 Ribsy   6 Phalange   7 Scapula\n<b>UPSTAIRS</b> 8 Humerus   9 Mandible\n10 Stapes   11 Hyoid (just moved in)\n<size=80%>superintendent: Femur (at the desk, or under the lift)</size>").color = new Color(0.95f, 0.9f, 0.75f);
        var clock = Text("MarrowClock", lb, new Vector3(-52.09f, 2.75f, 4f), Face(Vector3.left), new Vector2(2.2f, 0.6f), 0.22f, "");
        clock.color = new Color(1f, 0.85f, 0.4f);
        S("lobby:mail", -56.6f, 0.1f, 180f, rLobby); S("lobby:polish", -57.3f, 0.5f, 180f, rLobby); S("lobby:desk", -53.5f, 6.9f, 0f, rLobby); S("lobby:lift", -57.1f, 7.4f, -90f, rLobby);
        S("hall:knock2", xc[1], 4.9f, 0f, rHall); S("hall:knock4", xc[3], 4.9f, 0f, rHall); S("hall:bulb", -73f, 4f, 0f, rHall);
        S("hall:sweepE", -66f, 3.6f, -90f, rHall); S("hall:sweepW", -80f, 4.3f, 90f, rHall); S("hall:jog", -75f, 4f, 90f, rHall); S("hall:dance", -71f, 4f, 0f, rHall);
        S("out:fountain", -30f - dx, 6.15f - dz, 0f, rOut); S("out:sweep", -44f - dx, 11f - dz, 90f, rOut);   // out in Bonetown (world positions, so take off the shift)
        S6("roof:tower", -80f, 12.6f, -40f, rRoof, F2); S6("roof:garden", -66f, 14.6f, 0f, rRoof, F2); S6("roof:telescope", -62f, 16.6f, 0f, rRoof, F2);
        S6("roof:bbq", -74f, 7.2f, 0f, rRoof, F2); S6("roof:stage", -84f, 8f, 90f, rRoof, F2);
        S("yard:boxes", -44.5f, -4f, 90f, rYard); S("yard:garden", -60f, 23.3f, 0f, rYard);
        S("lobby:board", -52.8f, 6.2f, 90f, rLobby);
        // ---- the second floor: flats 8-11 over the south row, opening onto a balcony; a ramp up to it from the yard
        var up = Group(g, "Second floor", Vector3.zero);
        const float H2 = 3.2f;
        WallX(up, "BrickWall", -12f, -58f, X1, F2, H2, xc);
        WallX(up, "BrickWall", 2.5f, -58f, X1, F2, H2);
        foreach (float px in new[] { -58f, -65.5f, -73f, -80.5f, -88f }) WallZ(up, px == -58f || px == -88f ? "BrickWall" : "Wall", px, -12f, 2.5f, F2, H2);
        Box(up, "Roof2", new Vector3(-73f, F2 + H2 + 0.1f, -4.75f), new Vector3(30.3f, 0.2f, 14.8f), M("Concrete"));
        Box(up, "Balcony", new Vector3(-70f, F2 - 0.1f, -13.15f), new Vector3(36f, 0.2f, 2.1f), M("DeskWood"));
        Box(up, "RailE", new Vector3(-55.1f, F2 + 0.5f, -14.15f), new Vector3(6.2f, 1f, 0.08f), M("Metal"));
        Box(up, "RailW", new Vector3(-74.1f, F2 + 0.5f, -14.15f), new Vector3(27.8f, 1f, 0.08f), M("Metal"));
        Box(up, "RailEnd", new Vector3(-51.95f, F2 + 0.5f, -13.15f), new Vector3(0.08f, 1f, 2.1f), M("Metal"));
        // the side walkway: from the balcony's west end, round the end of the building to the roof terrace
        Box(up, "SideWalk", new Vector3(-89.1f, F2 - 0.1f, -4.8f), new Vector3(2.2f, 0.2f, 19f), M("DeskWood"));
        Box(up, "SideRail", new Vector3(-90.15f, F2 + 0.5f, -4.8f), new Vector3(0.08f, 1f, 19f), M("Metal"));
        Box(up, "SideRailS", new Vector3(-89.1f, F2 + 0.5f, -14.25f), new Vector3(2.2f, 1f, 0.08f), M("Metal"));
        Box(up, "SideRailN", new Vector3(-89.1f, F2 + 0.5f, 4.65f), new Vector3(2.2f, 1f, 0.08f), M("Metal"));
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cylinder, "SidePost", up, new Vector3(-90.1f, F2 / 2f, -13f + k * 5.5f), new Vector3(0.18f, F2 / 2f, 0.18f), M("Metal"));
        for (int k = 0; k < 7; k++) DPrim(PrimitiveType.Cylinder, "BalconyPost", up, new Vector3(-53f - k * 5.8f, F2 / 2f, -14.1f), new Vector3(0.18f, F2 / 2f, 0.18f), M("Metal"));
        var ramp = Box(up, "Ramp", new Vector3(-55.1f, 1.75f, -15.15f), new Vector3(7.17f, 0.2f, 1.5f), M("DeskWood"));
        ramp.transform.localRotation = Quaternion.Euler(0, 0, -30.14f);
        foreach (float rz in new[] { -14.4f, -15.9f }) DPrim(PrimitiveType.Cube, "RampRail", up, new Vector3(-55.1f, 2.6f, rz), new Vector3(7.17f, 0.06f, 0.06f), M("Metal"), Quaternion.Euler(0, 0, -30.14f));
        Box(up, "Landing", new Vector3(-59.2f, F2 - 0.1f, -14.9f), new Vector3(2f, 0.2f, 2.4f), M("DeskWood"));
        Box(up, "LandingRail", new Vector3(-59.2f, F2 + 0.5f, -16.05f), new Vector3(2f, 1f, 0.08f), M("Metal"));
        Box(up, "LandingRailW", new Vector3(-60.15f, F2 + 0.5f, -15.1f), new Vector3(0.08f, 1f, 1.8f), M("Metal"));
        DPrim(PrimitiveType.Cylinder, "LandingPost", up, new Vector3(-59.2f, F2 / 2f, -15.9f), new Vector3(0.18f, F2 / 2f, 0.18f), M("Metal"));
        // ---- the roof terrace (over the hallway and the north flats, and the lobby end)
        var roof = Group(g, "Roof terrace", Vector3.zero);
        Box(roof, "ParapetN", new Vector3(-70f, F2 + 0.5f, 19.95f), new Vector3(36.2f, 1f, 0.2f), M("BrickWall"));
        Box(roof, "ParapetE", new Vector3(-52.05f, F2 + 0.5f, 4f), new Vector3(0.2f, 1f, 32f), M("BrickWall"));
        Box(roof, "ParapetW", new Vector3(-87.95f, F2 + 0.5f, 12.5f), new Vector3(0.2f, 1f, 15f), M("BrickWall"));   // open from z 2.5 to 5: the way in from the side walkway
        Box(roof, "Deck", new Vector3(-70f, F2 + 0.006f, 11f), new Vector3(30f, 0.01f, 14f), M("DeskWood"), false);
        // the water tower
        foreach (float tlx in new[] { -1.4f, 1.4f }) foreach (float lz in new[] { -1.4f, 1.4f }) DPrim(PrimitiveType.Cylinder, "TowerLeg", roof, new Vector3(-82f + tlx, F2 + 1.5f, 15f + lz), new Vector3(0.15f, 1.5f, 0.15f), M("Metal"));
        Prim(PrimitiveType.Cylinder, "Tank", roof, new Vector3(-82f, F2 + 4.2f, 15f), new Vector3(3.2f, 1.2f, 3.2f), M("DeskWood"));
        Prim(PrimitiveType.Sphere, "TankTop", roof, new Vector3(-82f, F2 + 5.5f, 15f), new Vector3(3.3f, 1.2f, 3.3f), M("Trunk"));
        // the garden (with Gerald), the telescope, the barbecue, the open-mic corner, string lights
        Box(roof, "GardenBed", new Vector3(-66f, F2 + 0.25f, 16f), new Vector3(4f, 0.5f, 1.5f), M("Dirt"));
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Sphere, "Plant", roof, new Vector3(-67.6f + k * 0.8f, F2 + 0.75f, 16f), new Vector3(0.5f, 0.6f, 0.5f), M("GiftGreen"));
        DPrim(PrimitiveType.Sphere, "Gerald", roof, new Vector3(-66f, F2 + 0.75f, 15.6f), Vector3.one * 0.3f, M("GlowRed"));
        Text("GeraldSign", roof, new Vector3(-66f, F2 + 0.6f, 15.24f), Face(Vector3.back), new Vector2(1f, 0.2f), 0.08f, "GERALD").color = Color.white;
        foreach (float a3 in new[] { 0f, 120f, 240f }) DPrim(PrimitiveType.Cylinder, "TripodLeg", roof, new Vector3(-62f, F2 + 0.5f, 17.5f) + Quaternion.Euler(0, a3, 0) * new Vector3(0, 0, 0.25f), new Vector3(0.04f, 0.5f, 0.04f), M("Black"), Quaternion.Euler(0, a3, 0) * Quaternion.Euler(15, 0, 0));
        DPrim(PrimitiveType.Cylinder, "Telescope", roof, new Vector3(-62f, F2 + 1.2f, 17.6f), new Vector3(0.18f, 0.5f, 0.18f), M("CaneWhite"), Quaternion.Euler(-50, 0, 0));
        Box(roof, "Grill", new Vector3(-74f, F2 + 0.5f, 8f), new Vector3(1f, 1f, 0.6f), M("Black"));
        DPrim(PrimitiveType.Sphere, "Coals", roof, new Vector3(-74f, F2 + 1.02f, 8f), new Vector3(0.8f, 0.05f, 0.45f), M("GlowFlame"));
        Box(roof, "MicRug", new Vector3(-84f, F2 + 0.01f, 8f), new Vector3(2.4f, 0.01f, 2.4f), M("Stage"), false);
        for (int k = 0; k < 4; k++) Box(roof, "LawnChair", new Vector3(-79.5f, F2 + 0.25f, 6.5f + k * 1.1f), new Vector3(0.6f, 0.5f, 0.6f), M(k % 2 == 0 ? "InkOrange" : "InkBlue"));
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cylinder, "LightPole", roof, new Vector3(-56f - k * 9.5f, F2 + 1.4f, 19.3f), new Vector3(0.08f, 1.4f, 0.08f), M("Metal"));
        for (int k = 0; k < 30; k++) DPrim(PrimitiveType.Sphere, "StringLight", roof, new Vector3(-56f - k * 0.98f, F2 + 2.6f - Mathf.Abs(Mathf.Sin(k * 0.33f)) * 0.4f, 19.3f), Vector3.one * 0.12f, Bulb(k % 2 == 0 ? "BulbWarm" : "BulbGold"));
        Box(roof, "PigeonCoop", new Vector3(-56f, F2 + 0.6f, 17f), new Vector3(1.4f, 1.2f, 1f), M("DoorWood"));
        DPrim(PrimitiveType.Sphere, "Dish", roof, new Vector3(-56f, F2 + 0.8f, 0f), new Vector3(1f, 1f, 0.3f), M("CaneWhite"), Quaternion.Euler(-30, 40, 0));
        // ---- the outside of the building: brick, windows, the entrance
        var ext = Group(g, "Outside", Vector3.zero);
        Box(ext, "SkinN", new Vector3(-70f, 1.8f, 20.1f), new Vector3(36.3f, 3.6f, 0.05f), M("BrickWall"), false);
        Box(ext, "SkinS", new Vector3(-70f, 1.8f, -12.1f), new Vector3(36.3f, 3.6f, 0.05f), M("BrickWall"), false);
        Box(ext, "SkinW", new Vector3(-88.1f, 1.8f, 4f), new Vector3(0.05f, 3.6f, 32.3f), M("BrickWall"), false);
        Box(ext, "SkinE S", new Vector3(-51.9f, 1.8f, (-12f + 2.8f) / 2f), new Vector3(0.05f, 3.6f, 14.8f), M("BrickWall"), false);
        Box(ext, "SkinE N", new Vector3(-51.9f, 1.8f, (5.2f + 20f) / 2f), new Vector3(0.05f, 3.6f, 14.8f), M("BrickWall"), false);
        Box(ext, "SkinE Top", new Vector3(-51.9f, 3.3f, 4f), new Vector3(0.05f, 0.6f, 2.4f), M("BrickWall"), false);
        foreach (float wx in xc)
        {
            OutWindow(ext, new Vector3(wx, 1.9f, 20.14f), Vector3.forward);
            OutWindow(ext, new Vector3(wx, 1.9f, -12.14f), Vector3.back);
            OutWindow(ext, new Vector3(wx + 2.2f, F2 + 1.7f, -12.1f), Vector3.back);
            OutWindow(ext, new Vector3(wx, F2 + 1.7f, 2.6f), Vector3.forward);
        }
        foreach (float wz in new[] { -6f, 15f }) OutWindow(ext, new Vector3(-51.86f, 1.9f, wz), Vector3.right);
        Box(ext, "Awning", new Vector3(-50.9f, 3.05f, 4f), new Vector3(2.3f, 0.12f, 3.2f), M("GiftRed"), false);
        foreach (float az in new[] { 2.6f, 5.4f }) DPrim(PrimitiveType.Cylinder, "AwningPost", ext, new Vector3(-49.9f, 1.5f, az), new Vector3(0.08f, 1.5f, 0.08f), M("Brass"));
        foreach (float lz in new[] { 2.3f, 5.7f }) DPrim(PrimitiveType.Sphere, "DoorLamp", ext, new Vector3(-51.7f, 2.4f, lz), Vector3.one * 0.3f, M("GlowWarm"));
        Text("BuildingSign", ext, new Vector3(-51.92f, F2 + 0.5f, 4f), Face(Vector3.right), new Vector2(10f, 0.9f), 0.55f, "<b>THE MARROW ARMS</b>").color = new Color(1f, 0.9f, 0.6f);
        Text("Est", ext, new Vector3(-51.92f, 2.6f, 7.8f), Face(Vector3.right), new Vector2(2.4f, 0.5f), 0.14f, "est. 1891\n<size=70%>(the bones are older)</size>").color = new Color(1f, 0.9f, 0.6f);
        // ---- the yards: front (path, lawn, the for-rent sign, Hyoid's boxes), south (parking), back (dumpster, washing line, shed), north (pool, garden, hoop)
        var yd = Group(g, "Yards", Vector3.zero);
        Box(yd, "FrontPath", new Vector3(-46f, 0.005f, 4f), new Vector3(12f, 0.01f, 2f), M("Concrete"), false);
        Box(yd, "SidePath", new Vector3(-46f, 0.005f, -6.5f), new Vector3(1.6f, 0.01f, 21f), M("Concrete"), false);
        Box(yd, "Parking", new Vector3(-75f, 0.004f, -21f), new Vector3(36f, 0.01f, 5f), M("GloomStone"), false);
        for (int k = 0; k < 6; k++) Box(yd, "ParkingLine", new Vector3(-60f - k * 6f, 0.01f, -21f), new Vector3(0.1f, 0.01f, 4.6f), M("CaneWhite"), false);
        Car(yd, new Vector3(-66f, 0, -21f), "GiftBlue");
        Car(yd, new Vector3(-78f, 0, -21f), "GlowYellow");
        Text("ParkingSign", yd, new Vector3(-90f, 1.6f, -21f), Face(Vector3.right), new Vector2(2.4f, 0.6f), 0.16f, "<b>RESIDENT PARKING</b>\n<size=70%>(nobody here can drive. no feet)</size>").color = Color.white;
        DPrim(PrimitiveType.Cylinder, "MailPost", yd, new Vector3(-43f, 0.6f, 6.5f), new Vector3(0.1f, 0.6f, 0.1f), M("DeskWood"));
        Box(yd, "MailBox", new Vector3(-43f, 1.3f, 6.5f), new Vector3(0.5f, 0.35f, 0.3f), M("PoliceBlue"));
        Box(yd, "Bench", new Vector3(-47f, 0.42f, 8.5f), new Vector3(1.8f, 0.08f, 0.5f), M("DeskWood"));
        for (int k = 0; k < 5; k++) Box(yd, "Moving box", new Vector3(-43.3f + (k % 2) * 0.3f, 0.25f + (k / 2) * 0.5f, -4.4f + (k % 3) * 0.4f), new Vector3(0.5f, 0.48f, 0.5f), M("Cardboard"), false);
        var rent = Gg(yd, "For rent", new Vector3(-42.5f, 0, 0.5f), 90f, "Apart/ap_forrent", 1.4f, 2f);
        DPrim(PrimitiveType.Cylinder, "RentPost", rent.transform, new Vector3(0, 0.7f, 0), new Vector3(0.08f, 0.7f, 0.08f), M("DeskWood"));
        var board = Group(rent.transform, "Board", new Vector3(0, 1.6f, 0));
        Prim(PrimitiveType.Cube, "Board", board, Vector3.zero, new Vector3(1.2f, 0.7f, 0.05f), M("CaneWhite"));
        Text("RentText", board, new Vector3(0, 0, -0.03f), Quaternion.identity, new Vector2(1.1f, 0.6f), 0.1f, "<b>FOR RENT</b>\nflats 8-11, 2nd floor\n<size=70%>stairs not included</size>").color = new Color(0.8f, 0.1f, 0.1f);
        rent.target = board; rent.wobble = 12f;
        var dump = Gg(yd, "The dumpster", new Vector3(-95f, 0, -6f), 90f, "Apart/ap_raccoon", 2f, 1.6f);
        Box(dump.transform, "Bin", new Vector3(0, 0.6f, 0), new Vector3(2f, 1.2f, 1.2f), M("GiftGreen"), false);
        var lid = Group(dump.transform, "Lid", new Vector3(0, 1.2f, 0.6f));
        Prim(PrimitiveType.Cube, "Lid", lid, new Vector3(0, 0.03f, -0.6f), new Vector3(2.05f, 0.06f, 1.25f), M("Black"));
        var coon = Mini(lid, "Raccoon", new Vector3(0, -0.9f, -0.6f), 0f, 0.45f, "#9e9e9e");
        Prim(PrimitiveType.Cube, "Mask", coon[2], new Vector3(0, 0.06f, 0.17f), new Vector3(0.3f, 0.08f, 0.02f), M("Black"));
        dump.target = lid; dump.rotateBy = new Vector3(-80f, 0, 0); dump.inTime = 0.5f;
        foreach (float pz in new[] { 4f, 14f }) DPrim(PrimitiveType.Cylinder, "LinePole", yd, new Vector3(-94f, 1.1f, pz), new Vector3(0.08f, 1.1f, 0.08f), M("Metal"));
        DPrim(PrimitiveType.Cube, "Line", yd, new Vector3(-94f, 2.1f, 9f), new Vector3(0.02f, 0.02f, 10f), M("CaneWhite"));
        string[] wash = { "GiftRed", "CaneWhite", "InkBlue", "GlowYellow", "BonePink", "GiftGreen" };
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cube, "Washing", yd, new Vector3(-94f, 1.7f, 5f + k * 1.5f), new Vector3(0.03f, 0.7f, 0.8f), M(wash[k]));
        Box(yd, "Shed", new Vector3(-93.5f, 1.2f, 24f), new Vector3(3f, 2.4f, 3f), M("DoorWood"));
        Box(yd, "ShedRoof", new Vector3(-93.5f, 2.5f, 24f), new Vector3(3.4f, 0.2f, 3.4f), M("Trunk"), false);
        foreach (var pr in new[] { new { p = new Vector3(-70f, 0.15f, 24f), sz = new Vector3(8.4f, 0.3f, 0.2f) }, new { p = new Vector3(-70f, 0.15f, 28f), sz = new Vector3(8.4f, 0.3f, 0.2f) },
                                   new { p = new Vector3(-74.1f, 0.15f, 26f), sz = new Vector3(0.2f, 0.3f, 4f) }, new { p = new Vector3(-65.9f, 0.15f, 26f), sz = new Vector3(0.2f, 0.3f, 4f) } })
            Box(yd, "PoolRim", pr.p, pr.sz, M("Concrete"));
        Box(yd, "Pool", new Vector3(-70f, 0.12f, 26f), new Vector3(8f, 0.02f, 3.8f), M("Water"), false);
        for (int k = 0; k < 3; k++) Box(yd, "PoolChair", new Vector3(-74f + k * 4f, 0.25f, 22f), new Vector3(0.7f, 0.5f, 1.8f), M(k % 2 == 0 ? "CaneWhite" : "InkBlue"));
        Box(yd, "YardGarden", new Vector3(-60f, 0.15f, 25f), new Vector3(4f, 0.3f, 2.4f), M("Dirt"));
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Sphere, "Tomato plant", yd, new Vector3(-61.5f + (k % 3) * 1.5f, 0.6f, 24.4f + (k / 3) * 1.2f), new Vector3(0.6f, 0.7f, 0.6f), M(k % 2 == 0 ? "GiftGreen" : "Hedge"));
        DPrim(PrimitiveType.Cylinder, "HoopPole", yd, new Vector3(-86f, 1.6f, 31f), new Vector3(0.12f, 1.6f, 0.12f), M("Metal"));
        Box(yd, "Backboard", new Vector3(-86f, 3.2f, 30.6f), new Vector3(1.4f, 0.9f, 0.05f), M("CaneWhite"), false);
        DPrim(PrimitiveType.Cylinder, "Hoop", yd, new Vector3(-86f, 2.9f, 30.2f), new Vector3(0.5f, 0.02f, 0.5f), M("InkOrange"));
        foreach (var tp in new[] { new Vector3(-44f, 0, 22f), new Vector3(-44f, 0, -12f), new Vector3(-100f, 0, 8f), new Vector3(-100f, 0, -10f) })
        {
            DPrim(PrimitiveType.Cylinder, "YardTrunk", yd, tp + new Vector3(0, 1.5f, 0), new Vector3(0.3f, 1.5f, 0.3f), M("Trunk"));
            DPrim(PrimitiveType.Sphere, "YardTree", yd, tp + new Vector3(0, 3.6f, 0), new Vector3(2.6f, 2.4f, 2.6f), M("Hedge"));
        }
        // ---- the grounds: grass (the town's hedge, with gates, goes round them; the sky is the town's)
        var gr = Group(root, "Marrow Arms grounds", Vector3.zero);
        Box(gr, "Ground", new Vector3(-79f, -0.12f, 11f), new Vector3(62f, 0.2f, 56f), M("Grass"));
        // ---- who lives here, and what they do all day (hours on the Bonetown clock)
        var people = new[] {
            new { who = "Clavicle", home = "apt 1", hex = hexes[1], clip = "ap_clavicle", day = new object[] {
                0f, "a1:bed", 1, "asleep (dreaming of protein)",  5f, "gym:jacks", 6, "morning jumping jacks",  6.5f, "gym:lift", 15, "lifting the dumbbell (it's heavier than him)",
                8f, "a1:cook", 3, "making a protein shake. it goes straight through",  9.5f, "a1:mid", 13, "stretching",  12f, "hall:jog", 6, "doing laps of the hallway",
                14f, "laundry:foldA", 12, "washing his gym clothes (one sock)",  16f, "common:couchA", 5, "watching sport. shouting at it",  19f, "a1:chair", 4, "reading a book about calcium",
                21f, "a1:mid", 13, "evening yoga",  22.5f, "a1:bed", 1, "asleep by half ten. every night." } },
            new { who = "Mortimer", home = "apt 2", hex = hexes[2], clip = "ap_mortimer", day = new object[] {
                0f, "a2:couch", 11, "playing the guitar at midnight",  3f, "a2:bed", 1, "asleep (finally)",  13f, "a2:cook", 3, "breakfast at 1 pm",
                14f, "laundry:foldB", 12, "doing laundry (all of it black)",  16f, "a2:couch", 11, "writing a sad song",  18.5f, "out:fountain", 11, "busking by the fountain",
                21f, "a2:chair", 2, "cereal for dinner",  22.5f, "a2:couch", 11, "practising. loudly." } },
            new { who = "Granny Tibia", home = "apt 3", hex = hexes[3], clip = "ap_tibia", day = new object[] {
                0f, "a3:bed", 1, "asleep (snoring like a xylophone)",  6f, "a3:plant", 10, "watering her plants. they're plastic",  7.5f, "a3:cook", 3, "baking bone-shaped biscuits",
                9f, "lobby:mail", 17, "checking the mail (nothing, again)",  10.5f, "common:read", 4, "knitting in the common room",  14f, "a3:couch", 5, "watching her soaps",
                17f, "a3:chair", 4, "doing the crossword. in pen.",  22.5f, "hall:knock2", 16, "knocking on apartment 2: TOO LOUD!",  23.25f, "a3:bed", 1, "back to bed, grumbling" } },
            new { who = "Coccyx", home = "apt 4", hex = hexes[4], clip = "ap_coccyx", day = new object[] {
                0f, "a4:bed", 1, "asleep",  7f, "a4:cook", 3, "making coffee (it just falls out)",  8f, "a4:chair", 14, "working from home (typing very fast)",
                11f, "a4:mid", 9, "on a call. on mute. talking anyway",  12f, "lobby:mail", 17, "collecting a parcel (it's soup)",  13.5f, "a4:chair", 14, "back at work. 400 unread emails",
                17.5f, "gym:lift", 15, "pretending to go to the gym",  19f, "a4:couch", 5, "watching cooking shows, eating soup",  23f, "a4:bed", 1, "asleep" } },
            new { who = "Ribsy", home = "apt 5", hex = hexes[5], clip = "ap_ribsy", day = new object[] {
                0f, "a5:bed", 1, "asleep (talking in her sleep)",  7f, "a5:cook", 3, "breakfast and the neighbourhood news",  8.5f, "lobby:polish", 17, "reading other people's postcards",
                10f, "hall:knock4", 16, "knocking on apartment 4: \"is that soup?\"",  11.5f, "a5:mid", 9, "on the phone. it's all gossip",  14f, "common:couchB", 5, "watching TV, narrating it",
                17f, "laundry:foldB", 12, "\"just folding\" (listening)",  19.5f, "a5:mid", 9, "phoning everyone about the laundry room",  22.5f, "a5:bed", 1, "asleep" } },
            new { who = "Phalange", home = "apt 6", hex = hexes[6], clip = "ap_phalange", day = new object[] {
                0f, "a6:bed", 1, "asleep (in a very tidy way)",  5.5f, "a6:mid", 8, "cleaning before the sun's up",  7f, "hall:sweepW", 8, "sweeping the hallway (1st time)",
                9f, "laundry:foldA", 12, "ironing socks",  11f, "hall:sweepE", 8, "sweeping the hallway (5th time)",  13f, "lobby:polish", 8, "polishing the mailboxes",
                15f, "a6:cook", 8, "cleaning the kitchen. it was clean",  18f, "gym:wipe", 8, "wiping down the gym. nobody used it",  20.5f, "a6:chair", 4, "reading \"Dust: The Enemy Within\"",
                22f, "a6:bed", 1, "asleep" } },
            new { who = "Scapula", home = "apt 7", hex = hexes[7], clip = "ap_scapula", day = new object[] {
                0f, "a7:bed", 1, "asleep (still tapping one foot)",  8f, "a7:mid", 13, "warming up",  9.5f, "common:dance", 7, "teaching a dance class (nobody came)",
                12f, "a7:cook", 3, "lunch: a calcium smoothie",  13.5f, "gym:jacks", 6, "cardio",  15.5f, "hall:dance", 7, "dancing down the hallway",
                17f, "a7:couch", 5, "watching dance videos",  20f, "common:dance", 7, "dance class (Granny Tibia came, to complain)",  22.5f, "a7:bed", 1, "asleep" } },
            new { who = "Femur", home = "superintendent", hex = "#8d6e63", clip = "ap_femur", day = new object[] {
                0f, "lobby:desk", 2, "asleep at the desk (on duty)",  6f, "lobby:lift", 18, "fixing the lift (day 4,018)",  8.5f, "hall:bulb", 19, "changing a light bulb (it was fine)",
                10f, "laundry:fix", 18, "fixing a washer. finding socks",  12f, "lobby:desk", 2, "lunch break at the desk",  13f, "roof:tower", 18, "fixing the water tower",
                16.5f, "lobby:lift", 18, "fixing the lift again (it was the water tower)",  19.5f, "out:sweep", 8, "sweeping the path outside",  20.5f, "lobby:desk", 2, "night shift (asleep at 9)" } },
            new { who = "Humerus", home = "apt 8", hex = hexes[8], clip = "ap_humerus", day = new object[] {
                0f, "a8:bed", 1, "asleep (dreaming of applause)",  9f, "a8:mid", 9, "writing jokes. none of them are good",  11f, "lobby:board", 17, "reading the directory, looking for an audience",
                14f, "common:dance", 9, "testing jokes on the common room (tough crowd)",  17f, "roof:stage", 9, "open mic night on the roof",  22f, "a8:bed", 1, "asleep" } },
            new { who = "Mandible", home = "apt 9", hex = hexes[9], clip = "ap_mandible", day = new object[] {
                0f, "a9:bed", 1, "asleep",  6f, "roof:garden", 10, "watering Gerald (the tomato)",  9f, "a9:cook", 3, "tomato soup (not Gerald)",
                11f, "roof:garden", 9, "talking to Gerald",  13f, "yard:garden", 10, "weeding the garden in the yard",  16.5f, "a9:couch", 5, "watching gardening shows",
                20f, "a9:chair", 4, "reading \"Tomatoes and You\"",  22.5f, "a9:bed", 1, "asleep" } },
            new { who = "Stapes", home = "apt 10", hex = hexes[10], clip = "ap_stapes", day = new object[] {
                0f, "roof:telescope", 0, "stargazing",  4f, "a10:bed", 1, "asleep (so are the stars)",  12f, "a10:cook", 3, "a very small breakfast",
                13f, "a10:chair", 4, "reading about very small stars",  15f, "lobby:board", 17, "reading the directory (it's taller than him)",  18f, "a10:couch", 5, "watching space documentaries",
                21f, "roof:telescope", 0, "stargazing" } },
            new { who = "Hyoid", home = "apt 11", hex = hexes[11], clip = "ap_hyoid", day = new object[] {
                0f, "a11:bed", 1, "asleep on a pile of boxes",  7f, "yard:boxes", 17, "picking up box 37 of 200",  9.5f, "a11:mid", 12, "unpacking. it's all spoons",
                12f, "yard:boxes", 17, "box 38 of 200. also spoons",  14.5f, "a11:mid", 12, "unpacking. more spoons",
                17f, "a11:couch", 5, "too tired to unpack",  19f, "roof:bbq", 3, "a welcome barbecue (for himself)",  22f, "a11:bed", 1, "asleep" } } };
        var rg = Group(root, "Marrow Arms residents", Vector3.zero);
        var built = new List<FirResident>();
        int stopsTotal = 0;
        foreach (var p in people)
        {
            int n = p.day.Length / 4;
            var res = Group(rg, "Resident " + p.who, spots[(string)p.day[1]].pos);
            var piv = SkeletonRig(res, "Rig", BoneMat(p.who, p.hex));
            var col = res.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.95f, 0); col.size = new Vector3(0.7f, 1.9f, 0.5f);
            var fr = Add<FirResident>(res.gameObject);
            fr.dayMs = 720000; fr.walkSpeed = 1.4f;
            fr.pivots = piv; fr.who = p.who; fr.home = p.home;
            fr.hour = new float[n]; fr.spot = new Vector3[n]; fr.yaw = new float[n]; fr.pose = new int[n]; fr.doing = new string[n];
            var stopRoom = new int[n];
            for (int i = 0; i < n; i++)
            {
                var sp = spots[(string)p.day[i * 4 + 1]];
                fr.hour[i] = (float)p.day[i * 4]; fr.spot[i] = sp.pos; fr.yaw[i] = sp.yaw; stopRoom[i] = sp.room;
                fr.pose[i] = (int)p.day[i * 4 + 2]; fr.doing[i] = (string)p.day[i * 4 + 3];
            }
            // each walk: from the last stop to this one, along the graph between the rooms' doorways
            var legs = new List<Vector3>(); fr.legStart = new int[n]; fr.legLen = new int[n];
            for (int i = 0; i < n; i++)
            {
                int pi = (i + n - 1) % n;
                var way = new List<Vector3> { fr.spot[pi] };
                bool sameRoom = stopRoom[pi] == stopRoom[i] && !rooms[stopRoom[i]].open;
                if (!sameRoom)
                {
                    int na = Anchor(fr.spot[pi], stopRoom[pi], rooms, roomNode, nodes, nodeZone), nb = Anchor(fr.spot[i], stopRoom[i], rooms, roomNode, nodes, nodeZone);
                    if (na != nb) foreach (int k in Dijkstra(na, nb, nodes, links)) way.Add(nodes[k]);
                }
                way.Add(fr.spot[i]);
                var clean = new List<Vector3>();
                foreach (var w in way) if (clean.Count == 0 || Vector3.Distance(clean[clean.Count - 1], w) > 0.01f) clean.Add(w);
                if (clean.Count == 1) clean.Add(clean[0]);
                fr.legStart[i] = legs.Count; fr.legLen[i] = clean.Count;
                legs.AddRange(clean);
            }
            fr.legPts = legs.ToArray();
            fr.voice = Src(piv[2], Vector3.zero, Clip("Apart/" + p.clip), false, 1f, 1f, 15f, false);
            fr.line = CaptionFor(p.clip);
            fr.caption = Text("Caption", res, new Vector3(0, 2.2f, 0), Quaternion.identity, new Vector2(2.4f, 0.7f), 0.15f, "<b>" + p.who + "</b>");
            fr.caption.color = Color.white;
            fr.captionTransform = fr.caption.transform;
            if (p.who == "Femur")
            {
                fr.clockText = clock;
                Prim(PrimitiveType.Cylinder, "Cap", piv[2], new Vector3(0, 0.12f, 0.02f), new Vector3(0.26f, 0.05f, 0.26f), M("PoliceBlue"));
            }
            if (p.who == "Stapes") res.localScale = Vector3.one * 0.6f;
            if (p.who == "Hyoid") Prim(PrimitiveType.Cube, "Box", piv[6], new Vector3(-0.1f, -0.35f, 0.15f), new Vector3(0.4f, 0.3f, 0.3f), M("Cardboard"));
            if (p.who == "Granny Tibia") Prim(PrimitiveType.Sphere, "Bun", piv[2], new Vector3(0, 0.14f, -0.08f), Vector3.one * 0.13f, M("BoneSilver"));
            SetInteract(fr, "Say hi to " + p.who, 3f);
            built.Add(fr);
            stopsTotal += n;
        }
        // ---- checks: every walk fits in the time before the next stop, and nobody stands in anyone's spot at the same time
        foreach (var fr in built)
            for (int i = 0; i < fr.hour.Length; i++)
            {
                float gap = (i + 1 < fr.hour.Length ? fr.hour[i + 1] : fr.hour[0] + 24f) - fr.hour[i];
                float walk = fr.PathLength(i) / fr.walkSpeed;
                float secPerHour = fr.dayMs / 1000f / 24f;
                if (walk > gap * secPerHour * 0.9f) report.Add("problem: " + fr.who + " can't get to \"" + fr.doing[i] + "\" in time (" + walk.ToString("0") + " s to walk, " + (gap * secPerHour).ToString("0") + " s there)");
            }
        for (float h = 0f; h < 24f; h += 0.25f)
            for (int a = 0; a < built.Count; a++)
                for (int b = a + 1; b < built.Count; b++)
                {
                    int sa = built[a].StopAt(h), sb = built[b].StopAt(h);
                    if (Vector3.Distance(built[a].spot[sa], built[b].spot[sb]) < 0.6f)
                        report.Add("problem: " + built[a].who + " and " + built[b].who + " are both at \"" + built[a].doing[sa] + "\" at " + h + "h");
                }
        report.Add("The Marrow Arms: 11 apartments on two floors, a gym, a common room, a laundry, a lobby, a roof terrace and yards all round; " + built.Count + " residents with " + stopsTotal + " stops in their days, captioned; 5 gags");
    }

    // =================================================================== Bonetown, bigger: Main Street to the south, Clavicle Avenue to the north

    /// <summary>A shop on Main Street: open at the front (facing north), a counter, a sign and an awning.</summary>
    static Transform Shop(Transform g, string title, float x, float w, string wall, string awning)
    {
        var s = Group(g, "Shop " + title, Vector3.zero);
        float zf = -36f, zb = -44f, zm = (zf + zb) / 2f;
        Box(s, "Back", new Vector3(x, 2f, zb), new Vector3(w, 4f, 0.2f), M(wall));
        Box(s, "SideA", new Vector3(x - w / 2f, 2f, zm), new Vector3(0.2f, 4f, 8f), M(wall));
        Box(s, "SideB", new Vector3(x + w / 2f, 2f, zm), new Vector3(0.2f, 4f, 8f), M(wall));
        Box(s, "Roof", new Vector3(x, 4.1f, zm), new Vector3(w + 0.4f, 0.2f, 8.4f), M("TrimDark"));
        Box(s, "Facade", new Vector3(x, 4.7f, zf), new Vector3(w + 0.4f, 1.2f, 0.2f), M(wall));
        Text("Sign", s, new Vector3(x, 4.7f, zf + 0.12f), Face(Vector3.forward), new Vector2(w - 0.4f, 0.9f), 0.5f, "<b>" + title + "</b>").color = new Color(1f, 0.95f, 0.8f);
        Box(s, "Awning", new Vector3(x, 3.2f, zf + 0.7f), new Vector3(w, 0.1f, 1.4f), M(awning), false);
        Box(s, "Counter", new Vector3(x, 0.5f, -38.6f), new Vector3(w - 2f, 1f, 0.6f), M("DeskWood"));
        Box(s, "Floor", new Vector3(x, 0.01f, zm), new Vector3(w - 0.2f, 0.02f, 8f), M("Floor2"), false);
        return s;
    }

    /// <summary>A house on Clavicle Avenue (facing south): walls, a pitched roof, a door, windows, a fence and a mailbox.</summary>
    static void House(Transform g, string name, float x, float w, string wall, string roof)
    {
        var h = Group(g, name, Vector3.zero);
        float zf = 57f, zb = 64f, zm = (zf + zb) / 2f;
        Box(h, "Body", new Vector3(x, 1.75f, zm), new Vector3(w, 3.5f, zb - zf), M(wall));
        foreach (float sgn in new[] { -1f, 1f })
            Box(h, "RoofSide", new Vector3(x, 4.3f, zm + sgn * 1.75f), new Vector3(w + 0.6f, 0.2f, 4.3f), M(roof), false).transform.localRotation = Quaternion.Euler(sgn * 32f, 0, 0);
        Box(h, "Door", new Vector3(x, 1.05f, zf - 0.03f), new Vector3(1.1f, 2.1f, 0.05f), M("DoorWood"), false);
        foreach (float wx in new[] { -w / 3.2f, w / 3.2f }) OutWindow(h, new Vector3(x + wx, 1.9f, zf - 0.05f), Vector3.back);
        Box(h, "FenceL", new Vector3(x - w / 4f - 0.6f, 0.45f, 54.5f), new Vector3(w / 2f - 1.2f, 0.9f, 0.1f), M("CaneWhite"));
        Box(h, "FenceR", new Vector3(x + w / 4f + 0.6f, 0.45f, 54.5f), new Vector3(w / 2f - 1.2f, 0.9f, 0.1f), M("CaneWhite"));
        DPrim(PrimitiveType.Cylinder, "MailPost", h, new Vector3(x + w / 2f - 0.5f, 0.5f, 54.2f), new Vector3(0.08f, 0.5f, 0.08f), M("DeskWood"));
        Box(h, "MailBox", new Vector3(x + w / 2f - 0.5f, 1.1f, 54.2f), new Vector3(0.4f, 0.3f, 0.25f), M("GiftRed"));
    }

    static void BuildTownDistricts(Transform root)
    {
        var g = Group(root, "Bonetown districts", Vector3.zero);
        const float X0 = -12f, X1 = -110f, ZS = -60f, ZN = 70f, H = 16f;
        // ---- ground, sky, the outer walls (the east one is Bonetown's, with the lobby doorway)
        Box(g, "GrassSouth", new Vector3((X0 + X1) / 2f, -0.1f, (ZS - 17f) / 2f), new Vector3(X0 - X1, 0.2f, -17f - ZS), M("Grass"));
        Box(g, "GrassSouthStrip", new Vector3(-30f, -0.1f, -15.5f), new Vector3(36f, 0.2f, 3f), M("Grass"));
        Box(g, "GrassNorth", new Vector3((X0 + X1) / 2f, -0.1f, (39f + ZN) / 2f), new Vector3(X0 - X1, 0.2f, ZN - 39f), M("Grass"));
        Box(g, "GrassNorthStrip", new Vector3(-30f, -0.1f, 30.5f), new Vector3(36f, 0.2f, 17f), M("Grass"));
        Box(g, "Sky", new Vector3((X0 + X1) / 2f, H + 0.1f, (ZS + ZN) / 2f), new Vector3(X0 - X1, 0.2f, ZN - ZS), M("Sky"));
        Box(g, "WallW", new Vector3(X1 - 0.1f, H / 2f, (ZS + ZN) / 2f), new Vector3(0.2f, H, ZN - ZS), M("Sky"));
        Box(g, "WallN", new Vector3((X0 + X1) / 2f, H / 2f, ZN + 0.1f), new Vector3(X0 - X1, H, 0.2f), M("Sky"));
        Box(g, "WallS", new Vector3((X0 + X1) / 2f, H / 2f, ZS - 0.1f), new Vector3(X0 - X1, H, 0.2f), M("Sky"));
        // ---- the hedge round the Marrow Arms' grounds, with three gates
        var fence = Group(g, "Marrow Arms hedge", Vector3.zero);
        Box(fence, "HedgeS", new Vector3(-81.5f, 0.6f, -17f), new Vector3(57f, 1.2f, 0.4f), M("Hedge"));
        Box(fence, "HedgeS2", new Vector3(-49f, 0.6f, -17f), new Vector3(2f, 1.2f, 0.4f), M("Hedge"));
        Box(fence, "HedgeN", new Vector3(-81.5f, 0.6f, 39f), new Vector3(57f, 1.2f, 0.4f), M("Hedge"));
        Box(fence, "HedgeN2", new Vector3(-49f, 0.6f, 39f), new Vector3(2f, 1.2f, 0.4f), M("Hedge"));
        Box(fence, "HedgeE S", new Vector3(-48f, 0.6f, (-17f + 9.8f) / 2f), new Vector3(0.4f, 1.2f, 26.8f), M("Hedge"));
        Box(fence, "HedgeE N", new Vector3(-48f, 0.6f, (12.2f + 39f) / 2f), new Vector3(0.4f, 1.2f, 26.8f), M("Hedge"));
        foreach (var gate in new[] { new { at = new Vector3(-48f, 0, 11f), across = Vector3.forward }, new { at = new Vector3(-51.5f, 0, -17f), across = Vector3.right }, new { at = new Vector3(-51.5f, 0, 39f), across = Vector3.right } })
        {
            foreach (float sd in new[] { -1.3f, 1.3f }) DPrim(PrimitiveType.Cylinder, "GatePost", fence, gate.at + gate.across * sd + new Vector3(0, 1.6f, 0), new Vector3(0.25f, 1.6f, 0.25f), M("CaneWhite"));
            DPrim(PrimitiveType.Cube, "GateBeam", fence, gate.at + new Vector3(0, 3.25f, 0), gate.across.x != 0 ? new Vector3(2.9f, 0.3f, 0.3f) : new Vector3(0.3f, 0.3f, 2.9f), M("CaneWhite"));
        }
        // ---- the lanes out of old Bonetown (at its west end), Main Street and Clavicle Avenue
        Box(g, "LaneSouth", new Vector3(-46f, 0.006f, -20.5f), new Vector3(3f, 0.01f, 13f), M("Concrete"), false);
        Box(g, "LaneNorth", new Vector3(-46f, 0.006f, 34.5f), new Vector3(3f, 0.01f, 25f), M("Concrete"), false);
        Box(g, "MainStreet", new Vector3((X0 + X1) / 2f, 0.005f, -30f), new Vector3(X0 - X1, 0.01f, 6f), M("GloomStone"), false);
        Box(g, "Avenue", new Vector3((X0 + X1) / 2f, 0.005f, 50f), new Vector3(X0 - X1, 0.01f, 6f), M("GloomStone"), false);
        for (int k = 0; k < 24; k++)
        {
            DPrim(PrimitiveType.Cube, "Dash", g, new Vector3(-15f - k * 4f, 0.012f, -30f), new Vector3(1.6f, 0.01f, 0.15f), M("GlowYellow"));
            DPrim(PrimitiveType.Cube, "Dash", g, new Vector3(-15f - k * 4f, 0.012f, 50f), new Vector3(1.6f, 0.01f, 0.15f), M("GlowYellow"));
        }
        Text("MainStreetSign", g, new Vector3(-43.2f, 2.6f, -26.6f), Face(Vector3.right), new Vector2(3f, 0.4f), 0.22f, "<b>MAIN STREET</b>").color = Color.white;
        DPrim(PrimitiveType.Cylinder, "SignPost", g, new Vector3(-43.3f, 1.3f, -26.6f), new Vector3(0.08f, 1.3f, 0.08f), M("Metal"));
        Text("AvenueSign", g, new Vector3(-43.2f, 2.6f, 46.6f), Face(Vector3.right), new Vector2(3f, 0.4f), 0.22f, "<b>CLAVICLE AVENUE</b>").color = Color.white;
        DPrim(PrimitiveType.Cylinder, "SignPost", g, new Vector3(-43.3f, 1.3f, 46.6f), new Vector3(0.08f, 1.3f, 0.08f), M("Metal"));
        for (int k = 0; k < 10; k++)
            foreach (float lz in new[] { -26.6f, -33.4f, 46.6f, 53.4f })
            {
                if (Mathf.Abs(-18f - k * 10f + 46f) < 2f) continue;
                DPrim(PrimitiveType.Cylinder, "StreetLamp", g, new Vector3(-18f - k * 10f, 1.8f, lz), new Vector3(0.1f, 1.8f, 0.1f), M("Black"));
                DPrim(PrimitiveType.Sphere, "StreetLampGlow", g, new Vector3(-18f - k * 10f, 3.7f, lz), Vector3.one * 0.4f, M("GlowWarm"));
            }

        // ======== MAIN STREET (south): shops, the town hall, the market, the bus stop, and the park behind
        var shops = new[] {
            new { t = "BAKERY", x = -17f, wall = "Wall2", aw = "GlowYellow", who = "Bun Bonewell", hex = "#ffe0b2", clip = "t2_baker", act = 3 },
            new { t = "SKULL CUTS", x = -26.5f, wall = "WallGreen", aw = "CaneRed", who = "Snips", hex = "#90caf9", clip = "t2_barber", act = 20 },
            new { t = "TOYS", x = -36f, wall = "Wall", aw = "GiftBlue", who = "Tinker", hex = "#ffab40", clip = "t2_toys", act = 16 },
            new { t = "POST OFFICE", x = -58f, wall = "BrickWall", aw = "PoliceBlue", who = "Stamp", hex = "#5c6bc0", clip = "t2_postie", act = 20 },
            new { t = "THE BONEPLEX", x = -69f, wall = "WallDark", aw = "GemPurple", who = "Reel", hex = "#ef5350", clip = "t2_usher", act = 13 },
            new { t = "CAFE CALCIUM", x = -80f, wall = "Wall2", aw = "Trunk", who = "Crema", hex = "#bcaaa4", clip = "t2_cafe", act = 3 },
            new { t = "FLOWERS", x = -91f, wall = "WallGreen", aw = "BonePink", who = "Petal", hex = "#f48fb1", clip = "t2_florist", act = 4 },
            new { t = "FOR LEASE", x = -102f, wall = "Wall", aw = "TrimDark", who = "", hex = "", clip = "", act = 0 } };
        foreach (var sh in shops)
        {
            var s = Shop(g, sh.t, sh.x, 9f, sh.wall, sh.aw);
            if (sh.who != "") Folk(s, sh.who, sh.hex, sh.act, new Vector3(sh.x, 0, -39.4f), 0f, "Town2/" + sh.clip);
        }
        // shop dressing
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Capsule, "Loaf", g, new Vector3(-19.5f + k * 1.1f, 1.15f, -38.6f), new Vector3(0.25f, 0.3f, 0.25f), M("Bun"), Quaternion.Euler(0, 0, 90));
        DPrim(PrimitiveType.Cylinder, "BarberPole", g, new Vector3(-22.6f, 1.5f, -35.8f), new Vector3(0.25f, 1.2f, 0.25f), M("CaneRed"));
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cube, "Toy", g, new Vector3(-38.5f + k * 0.9f, 1.15f, -38.6f), Vector3.one * 0.3f, M(new[] { "GiftRed", "GiftBlue", "GlowYellow", "GiftGreen", "GemPurple", "InkOrange" }[k]));
        Box(g, "Marquee", new Vector3(-69f, 3.6f, -35.4f), new Vector3(8f, 0.8f, 0.3f), M("GlowWarm"), false);
        Text("NowShowing", g, new Vector3(-69f, 3.6f, -35.23f), Face(Vector3.forward), new Vector2(7.6f, 0.7f), 0.24f, "NOW SHOWING: <b>THE MAN WITH SKIN</b> (rated R for Revolting)").color = new Color(0.2f, 0.05f, 0.05f);
        for (int k = 0; k < 3; k++) { Prim(PrimitiveType.Cylinder, "CafeTable", g, new Vector3(-83f + k * 3f, 0.38f, -34.6f), new Vector3(0.8f, 0.38f, 0.8f), M("CaneWhite")); }
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Sphere, "Bouquet", g, new Vector3(-94f + k * 0.8f, 1.2f, -38.6f), Vector3.one * 0.35f, M(new[] { "BonePink", "GlowYellow", "GiftRed", "GemPurple" }[k % 4]));
        Text("Lease", g, new Vector3(-102f, 2f, -38.25f), Face(Vector3.forward), new Vector2(4f, 0.6f), 0.2f, "<b>FOR LEASE</b>\n<size=70%>ideal for a business. or a closet. or a closet business</size>").color = Color.white;
        // the town hall, with a clock tower
        var th = Group(g, "Town Hall", Vector3.zero);
        Box(th, "Hall", new Vector3(-46f, 3f, -44f), new Vector3(10f, 6f, 8f), M("Concrete"));
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cylinder, "Column", th, new Vector3(-49.5f + k * 2.33f, 2.5f, -39.6f), new Vector3(0.5f, 2.5f, 0.5f), M("CaneWhite"));
        Box(th, "Portico", new Vector3(-46f, 5.2f, -39.6f), new Vector3(10.4f, 0.4f, 1.6f), M("CaneWhite"));
        for (int k = 0; k < 3; k++) Box(th, "Step", new Vector3(-46f, 0.1f + k * 0.2f, -37.6f - k * 0.5f), new Vector3(8f, 0.2f, 1.6f - k * 0.5f), M("Concrete"));
        Box(th, "Tower", new Vector3(-46f, 8.5f, -45f), new Vector3(3.6f, 5f, 3.6f), M("Concrete"));
        Prim(PrimitiveType.Cylinder, "Spire", th, new Vector3(-46f, 11.6f, -45f), new Vector3(2.4f, 1.2f, 2.4f), M("PoliceBlue"));
        Text("HallSign", th, new Vector3(-46f, 5.85f, -38.75f), Face(Vector3.forward), new Vector2(9f, 0.6f), 0.4f, "<b>BONETOWN TOWN HALL</b>").color = new Color(0.3f, 0.25f, 0.2f);
        var clock = Gg(th, "The town clock", new Vector3(-46f, 0, -38.6f), 0f, "Town2/g_clock", 3f, 1.2f);
        var face = Group(clock.transform, "Face", new Vector3(0, 9f, -4.5f));
        Prim(PrimitiveType.Cylinder, "Dial", face, Vector3.zero, new Vector3(2.4f, 0.05f, 2.4f), M("CaneWhite")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        var hands = Group(face, "Hands", new Vector3(0, 0, 0.06f));
        Prim(PrimitiveType.Cube, "Hour", hands, new Vector3(0, 0.35f, 0), new Vector3(0.1f, 0.7f, 0.02f), M("Black"));
        Prim(PrimitiveType.Cube, "Minute", hands, new Vector3(0.45f, 0, 0), new Vector3(0.9f, 0.06f, 0.02f), M("Black"));
        hands.localRotation = Quaternion.identity;
        clock.target = hands; clock.rotateBy = new Vector3(0, 0, -390f); clock.inTime = 1.5f;
        Folk(g, "Mayor Marrowmont", "#ffd54f", 15, new Vector3(-43.5f, 0.4f, -37.6f), 0f, "Town2/t2_mayor");
        Gear("Mayor Marrowmont", 2, PrimitiveType.Cylinder, new Vector3(0, 0.18f, 0.02f), new Vector3(0.24f, 0.16f, 0.24f), "Black");
        Gear("Mayor Marrowmont", 1, PrimitiveType.Cube, new Vector3(0, 0.25f, 0.1f), new Vector3(0.36f, 0.06f, 0.02f), "GiftRed", new Vector3(0, 0, 30));
        // the market and the bus stop (north of Main Street)
        var vendors = new[] { new { who = "Turnip Tom", hex = "#aed581", clip = "t2_veg" }, new { who = "Gill", hex = "#4fc3f7", clip = "t2_fish" } };
        string[] mstripes = { "GiftGreen", "GiftBlue", "CaneRed", "GlowYellow" };
        for (int k = 0; k < 4; k++)
        {
            var st = Stall(g, new[] { "VEG", "FISH", "CHEESE", "BREAD" }[k], new Vector3(-60f - k * 8f, 0, -21.5f), 180f, mstripes[k], "CaneWhite");
            if (k < 2) Folk(st, vendors[k].who, vendors[k].hex, 3, new Vector3(0, 0, -0.3f), 0f, "Town2/" + vendors[k].clip);
        }
        Text("MarketSign", g, new Vector3(-72f, 3.8f, -17.3f), Face(Vector3.back), new Vector2(10f, 0.6f), 0.45f, "<b>BONETOWN MARKET</b>").color = new Color(1f, 0.9f, 0.4f);
        Box(g, "BusShelterRoof", new Vector3(-22f, 2.6f, -24.6f), new Vector3(4f, 0.1f, 1.6f), M("Glass"), false);
        Box(g, "BusShelterBack", new Vector3(-22f, 1.3f, -23.9f), new Vector3(4f, 2.6f, 0.08f), M("Glass"));
        Box(g, "BusBench", new Vector3(-22f, 0.42f, -24.3f), new Vector3(3f, 0.08f, 0.5f), M("DeskWood"));
        DPrim(PrimitiveType.Cylinder, "BusSignPost", g, new Vector3(-19.5f, 1.3f, -25.8f), new Vector3(0.06f, 1.3f, 0.06f), M("Metal"));
        Text("BusSign", g, new Vector3(-19.5f, 2.6f, -25.9f), Face(Vector3.forward), new Vector2(1.2f, 0.6f), 0.14f, "<b>BUS 9</b>\nnext bus: soon\n<size=60%>(since 1962)</size>").color = Color.white;
        Folk(g, "Patience", "#b0bec5", 0, new Vector3(-23f, 0, -25f), 0f, "Town2/t2_bus");
        // the park behind the shops: a pond with ducks, a merry-go-round, benches, trees
        var pk = Group(g, "The park", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "PondRim", pk, new Vector3(-70f, 0.1f, -52f), new Vector3(9f, 0.1f, 7f), M("Concrete"));
        Prim(PrimitiveType.Cylinder, "Pond", pk, new Vector3(-70f, 0.12f, -52f), new Vector3(8.4f, 0.1f, 6.4f), M("Water"));
        var ducks = Gg(pk, "The ducks", new Vector3(-70f, 0.2f, -52f), 0f, "Town2/g_ducks", 4f, 1f);
        var flock = Group(ducks.transform, "Ducks", Vector3.zero);
        for (int k = 0; k < 4; k++)
        {
            var dk = Group(flock, "Duck " + k, new Vector3(Mathf.Cos(k * 1.57f) * 1.6f, 0, Mathf.Sin(k * 1.57f) * 1.2f));
            dk.localRotation = Quaternion.Euler(0, k * 90f + 90f, 0);
            Prim(PrimitiveType.Sphere, "Body", dk, new Vector3(0, 0.15f, 0), new Vector3(0.3f, 0.2f, 0.45f), M("BoneWhite"));
            Prim(PrimitiveType.Sphere, "Head", dk, new Vector3(0, 0.35f, 0.18f), Vector3.one * 0.16f, M("BoneWhite"));
            Prim(PrimitiveType.Cube, "Beak", dk, new Vector3(0, 0.33f, 0.3f), new Vector3(0.08f, 0.03f, 0.1f), M("GlowYellow"));
        }
        ducks.target = flock; ducks.spin = 60f; ducks.bob = 0.06f;
        Box(pk, "ParkBench", new Vector3(-62f, 0.42f, -47.4f), new Vector3(1.8f, 0.08f, 0.5f), M("DeskWood"));
        Folk(pk, "Old Pigeonbone", "#9e9e9e", 1, new Vector3(-60.5f, 0, -47.8f), 180f, "Town2/t2_pigeon");
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Sphere, "Pigeon", pk, new Vector3(-60f + (k % 3) * 0.5f, 0.12f, -49.5f - (k / 3) * 0.5f), new Vector3(0.18f, 0.18f, 0.25f), M("LabGrey"));
        var mg = Group(pk, "Merry-go-round", new Vector3(-90f, 0, -52f));
        var rot = Group(mg, "Rotor", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "Deck", rot, new Vector3(0, 0.15f, 0), new Vector3(5f, 0.15f, 5f), M("CaneRed"));
        Prim(PrimitiveType.Cylinder, "Pole", rot, new Vector3(0, 1.4f, 0), new Vector3(0.2f, 1.2f, 0.2f), M("GlowGold"));
        Prim(PrimitiveType.Cylinder, "Canopy", rot, new Vector3(0, 2.7f, 0), new Vector3(5.2f, 0.15f, 5.2f), M("CaneWhite"));
        var mgExit = Marker(mg, "Merry-go-round exit", new Vector3(0, 0.05f, 3.6f), 0f);
        for (int k = 0; k < 4; k++)
        {
            var arm = Group(rot, "Horse " + k, Quaternion.Euler(0, k * 90f, 0) * new Vector3(0, 0, 1.8f));
            arm.localRotation = Quaternion.Euler(0, k * 90f + 90f, 0);
            Prim(PrimitiveType.Sphere, "Horse", arm, new Vector3(0, 0.75f, 0), new Vector3(0.35f, 0.45f, 0.8f), M(new[] { "CaneWhite", "BonePink", "GlowYellow", "GiftBlue" }[k]));
            DPrim(PrimitiveType.Cylinder, "HorsePole", arm, new Vector3(0, 1.4f, 0), new Vector3(0.05f, 1.2f, 0.05f), M("GlowGold"));
            RideSeat(arm, "Merry-go-round seat " + k, new Vector3(0, 0.75f, 0), 0f, mgExit);
        }
        var mr = Add<FirRide>(mg.gameObject); mr.kind = 0; mr.rotor = rot; mr.axis = Vector3.up; mr.degPerSec = 30f;
        foreach (var tp in new[] { new Vector3(-56f, 0, -56f), new Vector3(-80f, 0, -57f), new Vector3(-100f, 0, -48f), new Vector3(-106f, 0, -56f), new Vector3(-20f, 0, -52f), new Vector3(-32f, 0, -55f) })
            TreeAt(pk, "ParkTree", tp, 0f, 2.4f, false);
        for (int k = 0; k < 3; k++) Box(pk, "PicnicTable", new Vector3(-18f - k * 6f, 0.4f, -50f), new Vector3(2f, 0.08f, 0.9f), M("DeskWood"));

        // ======== CLAVICLE AVENUE (north): houses, the school, the library, the fire station; the farm and the lookout
        House(g, "House: the Femorellis", -18f, 9f, "Wall2", "CaneRed");
        Folk(g, "Mrs Femorelli", "#ffcc80", 4, new Vector3(-16f, 0, 55.6f), 180f, "Town2/t2_house1");
        // the school
        Box(g, "School", new Vector3(-36f, 2.5f, 61f), new Vector3(14f, 5f, 8f), M("BrickWall"));
        Text("SchoolSign", g, new Vector3(-36f, 4.2f, 56.95f), Face(Vector3.back), new Vector2(10f, 0.7f), 0.45f, "<b>BONETOWN ELEMENTARY</b>").color = Color.white;
        for (int k = 0; k < 4; k++) OutWindow(g, new Vector3(-41f + k * 3.3f, 2f, 56.95f), Vector3.back);
        Folk(g, "Miss Ossica", "#ce93d8", 20, new Vector3(-31f, 0, 55.6f), 180f, "Town2/t2_teacher");
        var bell = Gg(g, "School bell", new Vector3(-28f, 0, 55.8f), 180f, "Town2/g_schoolbell", 1f, 2.6f);
        DPrim(PrimitiveType.Cylinder, "BellPost", bell.transform, new Vector3(0, 1.2f, 0), new Vector3(0.08f, 1.2f, 0.08f), M("Metal"));
        var bb = Group(bell.transform, "Bell", new Vector3(0, 2.4f, 0));
        Prim(PrimitiveType.Sphere, "Bell", bb, new Vector3(0, -0.12f, 0), new Vector3(0.3f, 0.35f, 0.3f), M("Brass"));
        bell.target = bb; bell.wobble = 30f;
        // the library
        Box(g, "Library", new Vector3(-56f, 2.5f, 61f), new Vector3(10f, 5f, 8f), M("Concrete"));
        for (int k = 0; k < 3; k++) DPrim(PrimitiveType.Cylinder, "LibColumn", g, new Vector3(-59f + k * 3f, 2f, 56.6f), new Vector3(0.4f, 2f, 0.4f), M("CaneWhite"));
        Text("LibrarySign", g, new Vector3(-56f, 4.3f, 56.95f), Face(Vector3.back), new Vector2(8f, 0.6f), 0.4f, "<b>LIBRARY</b>  <size=60%>(shh)</size>").color = new Color(0.3f, 0.25f, 0.2f);
        Folk(g, "Mrs Quietbone", "#a1887f", 0, new Vector3(-53f, 0, 55.6f), 180f, "Town2/t2_librarian");
        Gear("Mrs Quietbone", 2, PrimitiveType.Cube, new Vector3(0, 0.03f, 0.16f), new Vector3(0.25f, 0.05f, 0.02f), "Black");
        // the fire station (a truck, a ladder, a cat on the roof)
        Box(g, "FireStation", new Vector3(-73f, 3f, 62f), new Vector3(12f, 6f, 9f), M("GiftRed"));
        Box(g, "FireDoor", new Vector3(-73f, 2f, 57.45f), new Vector3(5f, 4f, 0.05f), M("Black"), false);
        Text("FireSign", g, new Vector3(-73f, 5f, 57.45f), Face(Vector3.back), new Vector2(10f, 0.6f), 0.4f, "<b>FIRE STATION No. 1</b>").color = Color.white;
        Folk(g, "Captain Blaze", "#ff7043", 20, new Vector3(-68f, 0, 55.4f), 180f, "Town2/t2_fire");
        Gear("Captain Blaze", 2, PrimitiveType.Sphere, new Vector3(0, 0.12f, -0.02f), new Vector3(0.34f, 0.2f, 0.4f), "GlowYellow");
        var truck = Gg(g, "Fire truck", new Vector3(-76f, 0, 55.4f), 90f, "Town2/g_ladder", 2f, 2.2f);
        Box(truck.transform, "Truck", new Vector3(0, 0.9f, 0), new Vector3(1.8f, 1.4f, 4.4f), M("GiftRed"), false);
        Box(truck.transform, "Cab", new Vector3(0, 1.4f, 1.7f), new Vector3(1.8f, 1.2f, 1f), M("Glass"), false);
        foreach (float wz in new[] { -1.4f, 1.4f }) foreach (float wx in new[] { -0.9f, 0.9f }) Prim(PrimitiveType.Cylinder, "Wheel", truck.transform, new Vector3(wx, 0.35f, wz), new Vector3(0.7f, 0.1f, 0.7f), M("Black")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        var ladder = Group(truck.transform, "Ladder", new Vector3(0, 1.7f, -1.8f));
        foreach (float lx in new[] { -0.3f, 0.3f }) Prim(PrimitiveType.Cube, "Rail", ladder, new Vector3(lx, 0, 1.6f), new Vector3(0.06f, 0.06f, 3.4f), M("Metal"));
        for (int k = 0; k < 8; k++) Prim(PrimitiveType.Cube, "Rung", ladder, new Vector3(0, 0, 0.1f + k * 0.42f), new Vector3(0.6f, 0.04f, 0.04f), M("Metal"));
        truck.target = ladder; truck.rotateBy = new Vector3(-55f, 0, 0); truck.inTime = 1.5f;
        Prim(PrimitiveType.Sphere, "RoofCat", g, new Vector3(-70f, 6.25f, 63f), new Vector3(0.3f, 0.25f, 0.45f), M("BoneWhite"));
        House(g, "House: the Mowers", -90f, 9f, "WallGreen", "GiftBlue");
        Folk(g, "Mr Mowbray", "#8bc34a", 14, new Vector3(-88f, 0, 55.6f), 180f, "Town2/t2_house2");
        House(g, "House: Old Gruff", -102f, 9f, "Wall", "TrimDark");
        Folk(g, "Old Gruff", "#78909c", 0, new Vector3(-100f, 0, 55.6f), 180f, "Town2/t2_house3");
        for (int k = 0; k < 12; k++)
            if (k % 3 != 1) TreeAt(g, "AvenueTree", new Vector3(-52f - k * 5f, 0, 43f), 0f, 2.2f, false);
        for (int k = 0; k < 4; k++) Box(g, "AvenueBench", new Vector3(-59.5f - k * 15f, 0.42f, 44.8f), new Vector3(1.8f, 0.08f, 0.5f), M("DeskWood"));
        // the farm (north of old Bonetown, east of the lane)
        var fm = Group(g, "The farm", Vector3.zero);
        Box(fm, "Barn", new Vector3(-20f, 2.5f, 40f), new Vector3(8f, 5f, 6f), M("GiftRed"));
        foreach (float sgn in new[] { -1f, 1f }) Box(fm, "BarnRoof", new Vector3(-20f, 5.6f, 40f + sgn * 1.5f), new Vector3(8.4f, 0.2f, 3.6f), M("TrimDark"), false).transform.localRotation = Quaternion.Euler(sgn * 30f, 0, 0);
        Box(fm, "BarnDoor", new Vector3(-20f, 1.6f, 36.97f), new Vector3(3f, 3.2f, 0.05f), M("CaneWhite"), false);
        for (int r = 0; r < 4; r++) for (int c = 0; c < 5; c++) DPrim(PrimitiveType.Sphere, "Pumpkin", fm, new Vector3(-28f + c * 1.6f, 0.25f, 26f + r * 1.6f), new Vector3(0.6f, 0.45f, 0.6f), M("InkOrange"));
        Box(fm, "PenFence", new Vector3(-15f, 0.5f, 31f), new Vector3(0.1f, 1f, 6f), M("DeskWood"));
        Folk(fm, "Farmer Fibbs", "#a1887f", 22, new Vector3(-23.5f, 0, 35f), 180f, "Town2/t2_farmer");
        Gear("Farmer Fibbs", 2, PrimitiveType.Cylinder, new Vector3(0, 0.12f, 0), new Vector3(0.45f, 0.03f, 0.45f), "PixelSand");
        var crow = Gg(fm, "The scarecrow", new Vector3(-26f, 0, 32f), 180f, "Town2/g_scarecrow", 1.2f, 2.4f);
        DPrim(PrimitiveType.Cylinder, "Stake", crow.transform, new Vector3(0, 1f, 0), new Vector3(0.08f, 1f, 0.08f), M("DeskWood"));
        var sc = Group(crow.transform, "Scarecrow", new Vector3(0, 0.4f, 0));
        var scr = SkeletonRig(sc, "Rig", BoneMat("Scarecrow", "#d7ccc8"));
        scr[3].localRotation = Quaternion.Euler(0, 0, -85); scr[4].localRotation = Quaternion.Euler(0, 0, 85);
        Prim(PrimitiveType.Cylinder, "StrawHat", scr[2], new Vector3(0, 0.12f, 0), new Vector3(0.45f, 0.03f, 0.45f), M("PixelSand"));
        Prim(PrimitiveType.Sphere, "Crow", scr[4], new Vector3(0, -0.5f, 0), new Vector3(0.15f, 0.15f, 0.25f), M("Black"));
        crow.target = sc; crow.wobble = 8f;
        var cow = Gg(fm, "The cow", new Vector3(-17f, 0, 31f), -90f, "Town2/g_cow", 2f, 1.6f);
        var cw = Group(cow.transform, "Cow", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "Spine", cw, new Vector3(0, 1f, 0), new Vector3(0.1f, 0.8f, 0.1f), M("BoneWhite")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        for (int k = 0; k < 7; k++) Prim(PrimitiveType.Cylinder, "Rib", cw, new Vector3(0, 0.8f, -0.5f + k * 0.17f), new Vector3(0.6f, 0.01f, 0.04f), M("BoneWhite")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        Prim(PrimitiveType.Sphere, "Skull", cw, new Vector3(0, 1.05f, 1f), new Vector3(0.35f, 0.35f, 0.55f), M("BoneWhite"));
        foreach (float hx in new[] { -0.2f, 0.2f }) Prim(PrimitiveType.Cylinder, "Horn", cw, new Vector3(hx, 1.3f, 0.9f), new Vector3(0.05f, 0.12f, 0.05f), M("CaneWhite"));
        foreach (float lx in new[] { -0.2f, 0.2f }) foreach (float lz in new[] { -0.6f, 0.6f }) Prim(PrimitiveType.Cube, "Leg", cw, new Vector3(lx, 0.4f, lz), new Vector3(0.07f, 0.8f, 0.07f), M("BoneWhite"));
        cow.target = cw; cow.wobble = 5f; cow.bob = 0.05f;
        // the lookout hill (west of the lane is the hedge; it sits east of the lane, north of the barn)
        for (int k = 0; k < 6; k++) Box(g, "Hill step " + k, new Vector3(-36f, 0.2f + k * 0.4f, 31f), new Vector3(10f - k * 1.4f, 0.4f, 10f - k * 1.4f), M(k % 2 == 0 ? "Grass" : "Hedge"));
        Box(g, "LookoutBench", new Vector3(-36f, 2.85f, 31.6f), new Vector3(1.6f, 0.08f, 0.5f), M("DeskWood"));
        DPrim(PrimitiveType.Cylinder, "Viewer", g, new Vector3(-36f, 3.2f, 30.2f), new Vector3(0.15f, 0.4f, 0.15f), M("Metal"));
        Text("LookoutSign", g, new Vector3(-36f, 3.6f, 30f), Face(Vector3.back), new Vector2(2.4f, 0.4f), 0.14f, "<b>THE LOOKOUT</b>  <size=70%>(you can see the whole hedge from here)</size>").color = Color.white;

        // ---- people walking about the new streets
        var wg = Group(g, "Wanderers", Vector3.zero);
        MakeWanderer(wg, "Postie Pete", "#5c6bc0", new[] { new Vector3(-14f, 0, -28.5f), new Vector3(-108f, 0, -28.5f), new Vector3(-108f, 0, -31.5f), new Vector3(-14f, 0, -31.5f) }, new[] { 1f, 1f, 1f, 1f }, 1.0f, 0, "Town2/w_mail", CaptionFor("w_mail"));
        MakeWanderer(wg, "Jogging Jill", "#ec407a", new[] { new Vector3(-14f, 0, 48.5f), new Vector3(-108f, 0, 48.5f), new Vector3(-108f, 0, 51.5f), new Vector3(-14f, 0, 51.5f) }, new[] { 2.4f, 2.4f, 2.4f, 2.4f }, 1.1f, 9000, "Town2/w_jog", CaptionFor("w_jog"));
        MakeWanderer(wg, "Scooter Sid", "#26c6da", new[] { new Vector3(-14f, 0, -27.8f), new Vector3(-108f, 0, -27.8f), new Vector3(-108f, 0, -32.2f), new Vector3(-14f, 0, -32.2f) }, new[] { 3f, 3f, 3f, 3f }, 1.1f, 4000, "Town2/w_scooter", CaptionFor("w_scooter"));
        MakeWanderer(wg, "Dot and Biscuit", "#8d6e63", new[] { new Vector3(-20f, 0, 52.5f), new Vector3(-100f, 0, 52.5f) }, new[] { 1f, 1f }, 0.8f, 2000, "Town2/w_dog", CaptionFor("w_dog"));
        MakeWanderer(wg, "Tourist Tess", "#ffee58", new[] { new Vector3(-42f, 0, -6f), new Vector3(-46f, 0, -12f), new Vector3(-46f, 0, -34.5f), new Vector3(-30f, 0, -34.5f) }, new[] { 1f, 1f, 1f, 1f }, 0.9f, 0, "Town2/w_tourist", CaptionFor("w_tourist"));
        MakeWanderer(wg, "Tourist Ted", "#ffca28", new[] { new Vector3(-44.5f, 0, 12f), new Vector3(-46f, 0, 18f), new Vector3(-46f, 0, 46f), new Vector3(-80f, 0, 46f) }, new[] { 1f, 1f, 1f, 1f }, 0.9f, 15000, "Town2/w_tourist2", CaptionFor("w_tourist2"));
        report.Add("Bonetown districts: the town is now 98 x 130 m round the Marrow Arms' grounds: Main Street (8 shops, the town hall, a market, a bus stop, a park with a pond and a merry-go-round) and Clavicle Avenue (houses, a school, a library, a fire station, a farm, a lookout); 19 voiced townsfolk, 6 wanderers, 6 gags");
    }

    // =================================================================== three fan rooms (unofficial tributes: original skeleton gags about the kinds of videos, no likenesses or logos)

    /// <summary>A brass plaque on a stand that reads itself out when pressed.</summary>
    static void Plaque(Transform g, Vector3 at, string text, string clip)
    {
        var pl = Group(g, "The plaque", at);
        Box(pl, "Stand", new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.2f, 0.2f), M("DeskWood"));
        Box(pl, "Brass", new Vector3(0, 1.05f, -0.11f), new Vector3(1.3f, 0.7f, 0.02f), M("Brass"), false);
        Text("PlaqueText", pl, new Vector3(0, 1.05f, -0.125f), Face(Vector3.back), new Vector2(1.2f, 0.65f), 0.07f, text).color = new Color(0.2f, 0.12f, 0.05f);
        var col = pl.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(1.4f, 1.6f, 0.4f); col.center = new Vector3(0, 0.8f, 0);
        var cap = Text("Caption", pl, new Vector3(0, 2.0f, -0.12f), Face(Vector3.back), new Vector2(3f, 0.6f), 0.11f, "");
        cap.color = Color.white;
        var fs = Add<FirSay>(pl.gameObject);
        fs.line = CaptionFor(clip); fs.caption = cap;
        fs.voice = Src(pl, new Vector3(0, 1f, 0), Clip(clip.Contains("/") ? clip : "Fan/" + clip), false, 1f, 1f, 10f, false);
        SetInteract(fs, "Read the plaque", 2.5f);
    }

    static void BuildMarkiplierRoom(Transform root)
    {
        var g = AreaShell(root, "Markiplier Room", 2110f, 2146f, 20f, 50f, 6f, "Rug", "WallDark", "Ceiling",
                          "THE MARKIPLIER ROOM", new Color(0.95f, 0.3f, 0.45f), "a fan room: let's plays, horror games, heists and choose-your-own-adventures", "MARKIPLIER ROOM");
        Plaque(g, new Vector3(2134f, 0, 23.5f), "<b>THE MARKIPLIER ROOM</b>\nan unofficial fan tribute\n\ninspired by the let's plays, the horror game screams,\nthe heists and the choose-your-own-adventure films\n\n<size=80%>(not affiliated. press to hear more)</size>", "mk_plaque");
        // the let's-play corner: a desk, two monitors, a ring light, a chair, the streamer
        Box(g, "Desk", new Vector3(2116f, 0.38f, 44f), new Vector3(4f, 0.76f, 1.2f), M("Black"));
        var mon = Gg(g, "The monitor", new Vector3(2116f, 0.76f, 44.4f), 180f, "Fan/mk_scare", 2.4f, 1.4f);
        Box(mon.transform, "Screen", new Vector3(0, 0.55f, 0), new Vector3(2f, 1.1f, 0.06f), M("Black"), false);
        Box(mon.transform, "Game", new Vector3(0, 0.55f, 0.035f), new Vector3(1.85f, 0.95f, 0.01f), M("DarkGround"), false);
        for (int k = 0; k < 4; k++) Prim(PrimitiveType.Cube, "Corridor", mon.transform, new Vector3(0, 0.55f, 0.04f), new Vector3(1.6f - k * 0.38f, 0.8f - k * 0.18f, 0.005f), M(k % 2 == 0 ? "Black" : "DarkGround"));
        var face = Group(mon.transform, "Scare", new Vector3(0, 0.55f, 0.06f));
        Prim(PrimitiveType.Sphere, "Face", face, Vector3.zero, new Vector3(0.6f, 0.75f, 0.05f), M("BoneWhite"));
        foreach (float ex in new[] { -0.12f, 0.12f }) Prim(PrimitiveType.Sphere, "Eye", face, new Vector3(ex, 0.1f, 0.03f), Vector3.one * 0.14f, M("GlowRedEye"));
        Prim(PrimitiveType.Sphere, "Mouth", face, new Vector3(0, -0.18f, 0.03f), new Vector3(0.25f, 0.15f, 0.02f), M("Black"));
        mon.target = face; mon.hideWhenIdle = true; mon.scaleBy = 2.2f; mon.inTime = 0.12f;
        Prim(PrimitiveType.Cylinder, "RingLight", g, new Vector3(2119.5f, 1.7f, 44.5f), new Vector3(0.8f, 0.03f, 0.8f), M("GlowWarm")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Box(g, "GamingChair", new Vector3(2116f, 0.3f, 42.6f), new Vector3(0.7f, 0.6f, 0.7f), M("HeroRed"));
        Box(g, "ChairBack", new Vector3(2116f, 1f, 42.25f), new Vector3(0.7f, 1f, 0.12f), M("HeroRed"), false);
        Folk(g, "The Streamer", "#ff80ab", 2, new Vector3(2117.4f, 0, 42.4f), 160f, "Fan/mk_streamer");
        Gear("The Streamer", 2, PrimitiveType.Cube, new Vector3(0, 0.17f, 0), new Vector3(0.32f, 0.04f, 0.06f), "Black");
        foreach (float hx in new[] { -0.15f, 0.15f }) Gear("The Streamer", 2, PrimitiveType.Sphere, new Vector3(hx, 0.05f, 0), new Vector3(0.06f, 0.12f, 0.12f), "Black");
        // the viewer on the couch
        Box(g, "Couch", new Vector3(2128f, 0.25f, 36f), new Vector3(3f, 0.5f, 1f), M("Seat"));
        Box(g, "CouchBack", new Vector3(2128f, 0.7f, 36.45f), new Vector3(3f, 0.9f, 0.15f), M("Seat"), false);
        Folk(g, "Superfan", "#80cbc4", 9, new Vector3(2126.6f, 0, 35.2f), 180f, "Fan/mk_fan");
        DPrim(PrimitiveType.Cylinder, "Popcorn", g, new Vector3(2129f, 0.65f, 36f), new Vector3(0.3f, 0.15f, 0.3f), M("CaneRed"));
        // choose your own adventure: door A or door B
        Text("ChooseSign", g, new Vector3(2140f, 3.8f, 49.85f), Face(Vector3.back), new Vector2(8f, 0.6f), 0.4f, "<b>CHOOSE YOUR OWN ADVENTURE</b>").color = Color.white;
        var da = Gg(g, "Door A", new Vector3(2137f, 0, 49.6f), 0f, "Fan/mk_doora", 1.4f, 2.4f);
        Box(da.transform, "Frame", new Vector3(0, 1.15f, 0.15f), new Vector3(1.3f, 2.3f, 0.1f), M("Black"), false);
        var doorA = Group(da.transform, "Door", new Vector3(-0.55f, 0, 0));
        Prim(PrimitiveType.Cube, "Panel", doorA, new Vector3(0.55f, 1.1f, 0), new Vector3(1.1f, 2.2f, 0.06f), M("InkBlue"));
        Text("A", doorA, new Vector3(0.55f, 1.6f, -0.04f), Quaternion.identity, new Vector2(1f, 0.6f), 0.5f, "<b>A</b>").color = Color.white;
        Prim(PrimitiveType.Cube, "Sandwich", da.transform, new Vector3(0, 1f, 0.12f), new Vector3(0.4f, 0.15f, 0.3f), M("Bun"));
        da.target = doorA; da.rotateBy = new Vector3(0, 100f, 0); da.inTime = 0.6f;
        var db = Gg(g, "Door B", new Vector3(2143f, 0, 49.6f), 0f, "Fan/mk_doorb", 1.4f, 2.4f);
        Box(db.transform, "Frame", new Vector3(0, 1.15f, 0.15f), new Vector3(1.3f, 2.3f, 0.1f), M("Black"), false);
        var doorB = Group(db.transform, "Door", new Vector3(0, 0, 0));
        Prim(PrimitiveType.Cube, "Panel", doorB, new Vector3(0, 1.1f, 0), new Vector3(1.1f, 2.2f, 0.06f), M("HeroRed"));
        Text("B", doorB, new Vector3(0, 1.6f, -0.04f), Quaternion.identity, new Vector2(1f, 0.6f), 0.5f, "<b>B</b>").color = Color.white;
        db.target = doorB; db.moveBy = new Vector3(0, -2.3f, 0); db.inTime = 0.25f;
        // the heist vault
        var vault = Gg(g, "The vault", new Vector3(2145.6f, 0, 32f), -90f, "Fan/mk_vault", 2.6f, 2.6f);
        Box(vault.transform, "VaultWall", new Vector3(0, 1.3f, 0.2f), new Vector3(2.8f, 2.6f, 0.2f), M("Metal"), false);
        var vdoor = Group(vault.transform, "Vault door", new Vector3(-1f, 0, 0));
        Prim(PrimitiveType.Cylinder, "Door", vdoor, new Vector3(1f, 1.2f, 0), new Vector3(2f, 0.15f, 2f), M("Brass")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Cylinder, "Wheel", vdoor, new Vector3(1f, 1.2f, -0.12f), new Vector3(0.6f, 0.03f, 0.6f), M("Metal")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        vault.target = vdoor; vault.rotateBy = new Vector3(0, -110f, 0); vault.inTime = 1.2f;
        Text("HeistSign", g, new Vector3(2145.85f, 3.2f, 32f), Face(Vector3.left), new Vector2(4f, 0.5f), 0.3f, "<b>THE HEIST</b>").color = new Color(1f, 0.85f, 0.3f);
        // posters of (made-up) horror games
        string[] games = { "FIVE NIGHTS AT THE DENTIST", "AMNESIA: WHERE ARE MY KEYS", "OUTLAST (THE QUEUE)", "SILENT HILL (NOW WITH SOUND)" };
        for (int k = 0; k < 4; k++)
        {
            Box(g, "Poster", new Vector3(2110.06f, 2.6f, 26f + k * 4.5f), new Vector3(0.05f, 1.6f, 1.1f), M(k % 2 == 0 ? "DarkCliff" : "GloomStone"), false);
            Text("PosterTitle", g, new Vector3(2110.1f, 2.6f, 26f + k * 4.5f), Face(Vector3.right), new Vector2(1f, 1.4f), 0.13f, "<b>" + games[k] + "</b>").color = Color.white;
        }
        report.Add("The Markiplier Room (fan tribute): a let's-play corner with a screaming streamer, a superfan, a jump-scare monitor, door A and door B, a heist vault, game posters; 4 gags");
    }

    static void LawnFlamingo(Transform g, Vector3 at, float yaw)
    {
        var f = Group(g, "Lawn flamingo", at);
        f.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cylinder, "Leg", f, new Vector3(0, 0.45f, 0), new Vector3(0.03f, 0.45f, 0.03f), M("Black"));
        Prim(PrimitiveType.Sphere, "Body", f, new Vector3(0, 1f, 0), new Vector3(0.35f, 0.3f, 0.55f), M("BonePink"));
        Prim(PrimitiveType.Capsule, "Neck", f, new Vector3(0, 1.35f, 0.2f), new Vector3(0.07f, 0.25f, 0.07f), M("BonePink")).transform.localRotation = Quaternion.Euler(20, 0, 0);
        Prim(PrimitiveType.Sphere, "Head", f, new Vector3(0, 1.6f, 0.3f), Vector3.one * 0.14f, M("BonePink"));
        Prim(PrimitiveType.Cube, "Beak", f, new Vector3(0, 1.57f, 0.42f), new Vector3(0.04f, 0.04f, 0.14f), M("Black"));
    }

    static void BuildFlamingoRoom(Transform root)
    {
        var g = AreaShell(root, "Flamingo Room", 2160f, 2196f, 20f, 50f, 8f, "BonePink", "GemPink", "Ceiling",
                          "THE FLAMINGO ROOM", new Color(1f, 0.45f, 0.75f), "a fan room: chaos, trolling, blocky games and far too much pink", "FLAMINGO ROOM");
        Plaque(g, new Vector3(2184f, 0, 23.5f), "<b>THE FLAMINGO ROOM</b>\nan unofficial fan tribute\n\ninspired by the chaotic blocky games,\nthe trolling and the pink\n\n<size=80%>(not affiliated. press to hear more)</size>", "fl_plaque");
        var start = Marker(g, "Start", new Vector3(2164f, 0.05f, 23f), 0f);
        // flamingos everywhere
        for (int k = 0; k < 16; k++) LawnFlamingo(g, new Vector3(2162f + (k * 7 % 32), 0, 27f + (k * 11 % 20)), k * 47f);
        // FREE ADMIN (it isn't)
        Box(g, "AdminPedestal", new Vector3(2178f, 0.5f, 30f), new Vector3(1.2f, 1f, 1.2f), M("GlowGold"));
        var admin = TeleportButton(g, "FREE ADMIN", new Vector3(2178f, 1.3f, 29.35f), Vector3.back, start, new Vector3(1f, 0.35f, 0.1f));
        admin.sfx.clip = Clip("Fan/fl_admin"); admin.sfx.spatialBlend = 0f;
        Text("AdminSign", g, new Vector3(2178f, 2.2f, 29.4f), Face(Vector3.back), new Vector2(2.4f, 0.4f), 0.2f, "<b>FREE ADMIN!!!</b> <size=60%>(100% real)</size>").color = new Color(1f, 0.9f, 0.2f);
        // the troll door
        var td = Gg(g, "The door", new Vector3(2195.6f, 0, 36f), -90f, "Fan/fl_door", 1.4f, 2.4f);
        Box(td.transform, "Bricks", new Vector3(0, 1.1f, 0.15f), new Vector3(1.2f, 2.2f, 0.05f), M("BrickWall"), false);
        var tdoor = Group(td.transform, "Door", new Vector3(-0.55f, 0, 0));
        Prim(PrimitiveType.Cube, "Panel", tdoor, new Vector3(0.55f, 1.1f, 0), new Vector3(1.1f, 2.2f, 0.06f), M("DoorWood"));
        Text("Exit", tdoor, new Vector3(0.55f, 1.8f, -0.04f), Quaternion.identity, new Vector2(1f, 0.3f), 0.15f, "<b>SECRET EXIT</b>").color = Color.white;
        td.target = tdoor; td.rotateBy = new Vector3(0, 100f, 0); td.inTime = 0.5f;
        // the dancing flamingo
        var df = Gg(g, "The dancing flamingo", new Vector3(2170f, 0, 40f), 180f, "Fan/fl_flamingo", 1.2f, 2f);
        var bird = Group(df.transform, "Bird", Vector3.zero);
        LawnFlamingo(bird, Vector3.zero, 0f);
        df.target = bird; df.spin = 360f; df.bob = 0.2f;
        // a little blocky obby, and the floor is lava
        Box(g, "Lava", new Vector3(2186f, 0.01f, 42f), new Vector3(16f, 0.02f, 12f), M("GlowFlame"), false);
        Text("LavaSign", g, new Vector3(2186f, 0.6f, 35.9f), Face(Vector3.back), new Vector2(4f, 0.4f), 0.18f, "<b>THE FLOOR IS LAVA</b> <size=60%>(it's paint)</size>").color = Color.white;
        var hops = new[] { new Vector3(2180f, 0.4f, 37.5f), new Vector3(2182.5f, 0.8f, 39.5f), new Vector3(2185f, 1.2f, 41.5f), new Vector3(2187.5f, 1.6f, 43.5f), new Vector3(2190f, 2f, 45.5f), new Vector3(2192.5f, 2.4f, 47.5f) };
        for (int k = 0; k < hops.Length; k++) Box(g, "Obby block " + k, hops[k] - new Vector3(0, 0.2f, 0), new Vector3(1.6f, 0.4f, 1.6f), M(new[] { "InkBlue", "GlowGreen", "GlowYellow", "InkOrange", "GemPurple", "GlowGold" }[k]));
        Text("Win", g, new Vector3(2192.5f, 3.6f, 49.8f), Face(Vector3.back), new Vector2(3f, 0.6f), 0.4f, "<b>YOU WIN!</b>\n<size=50%>(there's no prize. you've been trolled)</size>").color = new Color(1f, 0.9f, 0.2f);
        // the skeletons
        Folk(g, "Mr Feathers", "#f8bbd0", 7, new Vector3(2166f, 0, 33f), 135f, "Fan/fl_costume");
        Gear("Mr Feathers", 1, PrimitiveType.Sphere, new Vector3(0, 0.1f, 0), new Vector3(0.6f, 0.7f, 0.5f), "BonePink");
        Gear("Mr Feathers", 2, PrimitiveType.Cube, new Vector3(0, 0.05f, 0.2f), new Vector3(0.06f, 0.06f, 0.2f), "Black");
        Folk(g, "Gullible Gary", "#bdbdbd", 0, new Vector3(2176f, 0, 28f), 30f, "Fan/fl_victim");
        report.Add("The Flamingo Room (fan tribute): sixteen lawn flamingos, FREE ADMIN (back to the start), a troll door, a dancing flamingo, the floor is lava, a blocky obby; 2 gags");
    }

    static void BuildRyanTrahanRoom(Transform root)
    {
        var g = AreaShell(root, "Ryan Trahan Room", 2210f, 2246f, 20f, 50f, 6f, "Floor2", "Wall2", "Ceiling",
                          "THE RYAN TRAHAN ROOM", new Color(0.3f, 0.75f, 1f), "a fan room: one penny, trading up, and a very long road trip", "RYAN TRAHAN ROOM");
        Plaque(g, new Vector3(2234f, 0, 23.5f), "<b>THE RYAN TRAHAN ROOM</b>\nan unofficial fan tribute\n\ninspired by the penny challenges,\nthe trading up and the long road trips\n\n<size=80%>(not affiliated. press to hear more)</size>", "rt_plaque");
        // the trade-up: press the bell to trade what's on the pedestal for something better
        Box(g, "Pedestal", new Vector3(2228f, 0.5f, 38f), new Vector3(1.4f, 1f, 1.4f), M("CaneWhite"));
        Text("TradeSign", g, new Vector3(2228f, 2.8f, 37.25f), Face(Vector3.back), new Vector2(4f, 0.5f), 0.3f, "<b>TRADE UP</b>").color = new Color(0.1f, 0.3f, 0.6f);
        var display = Group(g, "On the pedestal", new Vector3(2228f, 1f, 38f));
        string[] items = { "Penny", "Paperclip", "Pencil", "Bicycle", "Car" };
        var shows = new GameObject[5]; var lines = new AudioSource[5]; var caps = new string[5];
        for (int k = 0; k < 5; k++)
        {
            var it = Group(display, items[k], Vector3.zero);
            if (k == 0) Prim(PrimitiveType.Cylinder, "Penny", it, new Vector3(0, 0.1f, 0), new Vector3(0.25f, 0.02f, 0.25f), M("Brass")).transform.localRotation = Quaternion.Euler(70, 0, 0);
            if (k == 1) Prim(PrimitiveType.Cube, "Paperclip", it, new Vector3(0, 0.05f, 0), new Vector3(0.05f, 0.01f, 0.18f), M("Metal"));
            if (k == 2) Prim(PrimitiveType.Cylinder, "Pencil", it, new Vector3(0, 0.05f, 0), new Vector3(0.04f, 0.15f, 0.04f), M("GlowYellow")).transform.localRotation = Quaternion.Euler(0, 0, 90);
            if (k == 3) { foreach (float wz in new[] { -0.35f, 0.35f }) Prim(PrimitiveType.Cylinder, "Wheel", it, new Vector3(0, 0.3f, wz), new Vector3(0.55f, 0.02f, 0.55f), M("Black")).transform.localRotation = Quaternion.Euler(0, 0, 90); Prim(PrimitiveType.Cube, "Frame", it, new Vector3(0, 0.45f, 0), new Vector3(0.04f, 0.04f, 0.8f), M("GiftRed")); }
            if (k == 4) { Prim(PrimitiveType.Cube, "Body", it, new Vector3(0, 0.25f, 0), new Vector3(0.6f, 0.25f, 1.2f), M("InkBlue")); Prim(PrimitiveType.Cube, "Top", it, new Vector3(0, 0.45f, -0.05f), new Vector3(0.55f, 0.2f, 0.6f), M("Glass")); }
            shows[k] = it.gameObject;
            lines[k] = Src(display, Vector3.zero, Clip("Fan/rt_t" + (k + 1)), false, 1f, 1f, 12f, false);
            caps[k] = CaptionFor("rt_t" + (k + 1));
        }
        var bell = Group(g, "Trade bell", new Vector3(2229.2f, 1.05f, 37.4f));
        Prim(PrimitiveType.Sphere, "Bell", bell, Vector3.zero, new Vector3(0.18f, 0.1f, 0.18f), M("GlowGold"));
        bell.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.4f, 0.5f);
        var fc = Add<FirChannels>(bell.gameObject);
        fc.channels = shows; fc.lines = lines; fc.captions = caps;
        fc.caption = Text("TradeCaption", g, new Vector3(2228f, 2.2f, 37.25f), Face(Vector3.back), new Vector2(4f, 0.5f), 0.14f, "");
        fc.caption.color = new Color(0.1f, 0.2f, 0.4f);
        fc.click = Src(bell.transform, Vector3.zero, Clip("pickup"), false, 1f, 1f, 6f, false);
        SetInteract(fc, "Trade up", 2.5f);
        // the road-trip map
        Box(g, "Map", new Vector3(2246f - 0.06f, 2.4f, 36f), new Vector3(0.05f, 2.4f, 5f), M("PixelSand"), false);
        for (int k = 0; k < 9; k++)
        {
            var pin = new Vector3(2245.88f, 1.6f + Mathf.Sin(k * 0.9f) * 0.6f + 0.8f, 34f + k * 0.5f);
            DPrim(PrimitiveType.Sphere, "Pin", g, pin, Vector3.one * 0.1f, M("GlowRed"));
            if (k < 8) DPrim(PrimitiveType.Cube, "Route", g, (pin + new Vector3(2245.88f, 1.6f + Mathf.Sin((k + 1) * 0.9f) * 0.6f + 0.8f, 34f + (k + 1) * 0.5f)) / 2f, new Vector3(0.01f, 0.03f, 0.5f), M("GlowRed"));
        }
        Text("MapSign", g, new Vector3(2245.88f, 3.9f, 36f), Face(Vector3.left), new Vector2(5f, 0.4f), 0.22f, "<b>THE ROAD TRIP</b> <size=70%>(day 1 of a lot)</size>").color = new Color(0.1f, 0.2f, 0.4f);
        // the vlogger, the hitchhiker, the tent, the donation jar
        Folk(g, "The Vlogger", "#4fc3f7", 16, new Vector3(2220f, 0, 32f), 120f, "Fan/rt_vlog");
        Gear("The Vlogger", 6, PrimitiveType.Cylinder, new Vector3(0, -0.45f, 0.1f), new Vector3(0.03f, 0.3f, 0.03f), "Black", new Vector3(-30, 0, 0));
        Gear("The Vlogger", 6, PrimitiveType.Cube, new Vector3(0, -0.75f, 0.28f), new Vector3(0.1f, 0.06f, 0.12f), "Black");
        Folk(g, "Thumbs", "#ffb74d", 0, new Vector3(2240f, 0, 44f), 200f, "Fan/rt_hitch");
        Gear("Thumbs", 5, PrimitiveType.Cube, new Vector3(0, -0.25f, 0.12f), new Vector3(0.5f, 0.35f, 0.02f), "Cardboard");
        Prim(PrimitiveType.Sphere, "Tent", g, new Vector3(2215f, 0, 45f), new Vector3(3f, 2.4f, 3.4f), M("InkOrange"));
        Box(g, "SleepingBag", new Vector3(2219f, 0.1f, 46f), new Vector3(0.8f, 0.2f, 2f), M("GiftGreen"), false);
        var jar = Gg(g, "Donation jar", new Vector3(2234f, 0, 30f), 180f, "Fan/rt_jar", 1f, 1.4f);
        Box(jar.transform, "Table", new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 0.8f), M("DeskWood"), false);
        var jg = Group(jar.transform, "Jar", new Vector3(0, 0.8f, 0));
        Prim(PrimitiveType.Cylinder, "Glass", jg, new Vector3(0, 0.2f, 0), new Vector3(0.35f, 0.2f, 0.35f), M("Glass"));
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Cylinder, "Coin", jg, new Vector3(-0.08f + (k % 3) * 0.08f, 0.04f + (k / 3) * 0.03f, 0), new Vector3(0.07f, 0.01f, 0.07f), M(k % 2 == 0 ? "Brass" : "GiftRed"));
        jar.target = jg; jar.wobble = 8f;
        report.Add("The Ryan Trahan Room (fan tribute): a penny you can trade up (to a car), a road-trip map, a vlogger, a hitchhiker, a tent, a donation jar; 1 gag");
    }

    // =================================================================== the Scream Room (a room dedicated entirely to screaming)

    static void BuildScreamRoom(Transform root)
    {
        var g = AreaShell(root, "The Scream Room", 2260f, 2296f, 20f, 50f, 7f, "Rug", "GemPurple", "Ceiling",
                          "THE SCREAM ROOM", new Color(1f, 0.3f, 0.2f), "a room dedicated entirely to screaming. please scream responsibly.", "SCREAM ROOM");
        var pl = Group(g, "The plaque", new Vector3(2284f, 0, 23.5f));
        Box(pl, "Stand", new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.2f, 0.2f), M("DeskWood"));
        Text("PlaqueText", pl, new Vector3(0, 1.05f, -0.125f), Face(Vector3.back), new Vector2(1.2f, 0.65f), 0.09f, "<b>THE SCREAM ROOM</b>\neverything in here screams\n(press things)").color = new Color(0.9f, 0.85f, 0.7f);
        var pcol = pl.gameObject.AddComponent<BoxCollider>(); pcol.size = new Vector3(1.4f, 1.6f, 0.4f); pcol.center = new Vector3(0, 0.8f, 0);
        var pcap = Text("Caption", pl, new Vector3(0, 2.0f, -0.12f), Face(Vector3.back), new Vector2(3f, 0.6f), 0.11f, ""); pcap.color = Color.white;
        var ps = Add<FirSay>(pl.gameObject); ps.line = CaptionFor("sc_plaque"); ps.caption = pcap;
        ps.voice = Src(pl, new Vector3(0, 1f, 0), Clip("Scream/sc_plaque"), false, 1f, 1f, 10f, false);
        SetInteract(ps, "Read the plaque", 2.5f);
        // the scream booth and its meter
        Box(g, "BoothBack", new Vector3(2264f, 1.25f, 47.9f), new Vector3(2.6f, 2.5f, 0.1f), M("Black"));
        Box(g, "BoothL", new Vector3(2262.75f, 1.25f, 46.8f), new Vector3(0.1f, 2.5f, 2.2f), M("Glass"));
        Box(g, "BoothR", new Vector3(2265.25f, 1.25f, 46.8f), new Vector3(0.1f, 2.5f, 2.2f), M("Glass"));
        Box(g, "BoothRoof", new Vector3(2264f, 2.55f, 46.8f), new Vector3(2.6f, 0.1f, 2.3f), M("Black"), false);
        Text("BoothSign", g, new Vector3(2264f, 2.9f, 45.6f), Face(Vector3.back), new Vector2(2.6f, 0.4f), 0.2f, "<b>SCREAM BOOTH</b> <size=60%>(soundproof. it isn't)</size>").color = Color.white;
        var booth = Gg(g, "The scream booth", new Vector3(2264f, 0, 46.5f), 180f, "Scream/sc_booth", 2f, 2.4f);
        var mic = Group(booth.transform, "Microphone", new Vector3(0, 0, 0));
        Prim(PrimitiveType.Cylinder, "Stand", mic, new Vector3(0, 0.7f, 0), new Vector3(0.04f, 0.7f, 0.04f), M("Black"));
        Prim(PrimitiveType.Sphere, "Mic", mic, new Vector3(0, 1.45f, 0), Vector3.one * 0.12f, M("Metal"));
        booth.target = mic; booth.wobble = 25f;
        var meter = Gg(g, "The scream meter", new Vector3(2268f, 0, 47.6f), 180f, "Scream/sc_booth", 1f, 3f);
        Box(meter.transform, "MeterBack", new Vector3(0, 1.6f, 0), new Vector3(0.5f, 3f, 0.1f), M("Black"), false);
        Text("MeterTop", meter.transform, new Vector3(0, 3.25f, -0.06f), Quaternion.identity, new Vector2(1.2f, 0.3f), 0.12f, "TOO LOUD").color = new Color(1f, 0.2f, 0.2f);
        var bar = Group(meter.transform, "Level", new Vector3(0, 0.15f, -0.06f));
        Prim(PrimitiveType.Cube, "Bar", bar, new Vector3(0, 0.15f, 0), new Vector3(0.35f, 0.3f, 0.02f), M("GlowRed"));
        meter.target = bar; meter.scaleBy = 9.5f; meter.inTime = 0.4f;
        Text("Record", g, new Vector3(2268f, 4f, 49.85f), Face(Vector3.back), new Vector2(5f, 0.5f), 0.2f, "LOUDEST SCREAM TODAY: <b>140 decibels</b> <size=70%>(it was the goat)</size>").color = new Color(1f, 0.9f, 0.4f);
        // the screaming choir, and its console
        Box(g, "Risers", new Vector3(2280f, 0.25f, 46.5f), new Vector3(8f, 0.5f, 2.2f), M("Stage"));
        var choir = new[] { new { who = "Bellow", hex = "#5d4037", clip = "scream_1" }, new { who = "Yelpington", hex = "#1e88e5", clip = "scream_2" }, new { who = "Howler", hex = "#43a047", clip = "scream_3" }, new { who = "Shrieka", hex = "#e91e63", clip = "scream_4" } };
        for (int k = 0; k < 4; k++) Folk(g, choir[k].who, choir[k].hex, 2, new Vector3(2276.5f + k * 2.3f, 0.5f, 46.5f), 180f, "Combo/" + choir[k].clip);
        var console = Console(g, "ScreamConsole", new Vector3(2280f, 1.25f, 41.5f), Vector3.back, "<size=80%>THE SCREAMING CHOIR</size>");
        ComboAct(console, 0, 9, "The Screaming Choir", choir.Select(c => c.who).ToArray(), new[] { "scream_1", "scream_2", "scream_3", "scream_4", "scream_5" }, new[] { 0, 1, 2, 3, -1 }, 2f);
        // the goat, the kettle, the rubber chicken
        var goat = Gg(g, "The screaming goat", new Vector3(2292f, 0, 36f), -90f, "Scream/sc_goat", 1.6f, 1.6f);
        var gt = Group(goat.transform, "Goat", Vector3.zero);
        Prim(PrimitiveType.Sphere, "Body", gt, new Vector3(0, 0.75f, 0), new Vector3(0.5f, 0.5f, 0.9f), M("CaneWhite"));
        var gh = Group(gt, "Head", new Vector3(0, 1.05f, 0.5f));
        Prim(PrimitiveType.Sphere, "Head", gh, Vector3.zero, new Vector3(0.25f, 0.3f, 0.35f), M("CaneWhite"));
        foreach (float hx in new[] { -0.08f, 0.08f }) Prim(PrimitiveType.Cylinder, "Horn", gh, new Vector3(hx, 0.18f, -0.05f), new Vector3(0.04f, 0.1f, 0.04f), M("Trunk")).transform.localRotation = Quaternion.Euler(-30, 0, 0);
        Prim(PrimitiveType.Sphere, "Mouth", gh, new Vector3(0, -0.08f, 0.16f), new Vector3(0.12f, 0.1f, 0.06f), M("Black"));
        foreach (float lx in new[] { -0.15f, 0.15f }) foreach (float lz in new[] { -0.3f, 0.3f }) Prim(PrimitiveType.Cube, "Leg", gt, new Vector3(lx, 0.25f, lz), new Vector3(0.07f, 0.5f, 0.07f), M("CaneWhite"));
        goat.target = gh; goat.rotateBy = new Vector3(-35f, 0, 0); goat.wobble = 10f; goat.inTime = 0.2f;
        var kettle = Gg(g, "The kettle", new Vector3(2292f, 0, 28f), -90f, "Scream/sc_kettle", 1f, 1.4f);
        Box(kettle.transform, "Stove", new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.7f), M("Metal"), false);
        var kt = Group(kettle.transform, "Kettle", new Vector3(0, 0.9f, 0));
        Prim(PrimitiveType.Sphere, "Pot", kt, new Vector3(0, 0.15f, 0), new Vector3(0.4f, 0.35f, 0.4f), M("HeroRed"));
        Prim(PrimitiveType.Cylinder, "Spout", kt, new Vector3(0, 0.2f, 0.22f), new Vector3(0.06f, 0.1f, 0.06f), M("HeroRed")).transform.localRotation = Quaternion.Euler(60, 0, 0);
        var steam = Group(kettle.transform, "Steam", new Vector3(0, 1.2f, 0.3f));
        for (int k = 0; k < 3; k++) Prim(PrimitiveType.Sphere, "Puff", steam, new Vector3(0, k * 0.25f, k * 0.1f), Vector3.one * (0.15f + k * 0.08f), M("Mist"));
        kettle.target = steam; kettle.hideWhenIdle = true; kettle.moveBy = new Vector3(0, 0.6f, 0); kettle.wobble = 12f;
        var chick = Gg(g, "The rubber chicken", new Vector3(2272f, 0, 33f), 0f, "Scream/sc_chicken", 1f, 1.4f);
        Box(chick.transform, "Table", new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.8f, 0.8f), M("DeskWood"), false);
        var rc = Group(chick.transform, "Chicken", new Vector3(0, 0.8f, 0));
        Prim(PrimitiveType.Capsule, "Body", rc, new Vector3(0, 0.15f, 0), new Vector3(0.15f, 0.3f, 0.15f), M("GlowYellow")).transform.localRotation = Quaternion.Euler(0, 0, 80);
        Prim(PrimitiveType.Sphere, "Head", rc, new Vector3(0.3f, 0.2f, 0), Vector3.one * 0.12f, M("GlowYellow"));
        Prim(PrimitiveType.Cube, "Comb", rc, new Vector3(0.3f, 0.3f, 0), new Vector3(0.04f, 0.06f, 0.08f), M("GlowRed"));
        rc.localScale = Vector3.one;
        chick.target = rc; chick.scaleBy = 0.6f; chick.inTime = 0.15f; chick.wobble = 8f;
        // the void (scream into it)
        var vd = Group(g, "The Void", new Vector3(2262f, 0, 30f));
        Prim(PrimitiveType.Cylinder, "Hole", vd, new Vector3(0, 0.012f, 0), new Vector3(2.4f, 0.01f, 2.4f), M("Black"));
        Prim(PrimitiveType.Cylinder, "Rim", vd, new Vector3(0, 0.15f, 0), new Vector3(2.8f, 0.15f, 2.8f), M("Concrete"));
        var vcol = vd.gameObject.AddComponent<BoxCollider>(); vcol.size = new Vector3(2.8f, 0.6f, 2.8f); vcol.center = new Vector3(0, 0.3f, 0);
        Text("VoidSign", vd, new Vector3(0, 1.2f, -1.5f), Face(Vector3.back), new Vector2(2.4f, 0.4f), 0.16f, "<b>SCREAM INTO THE VOID</b>").color = Color.white;
        var vcap = Text("Caption", vd, new Vector3(0, 1.8f, -1.5f), Face(Vector3.back), new Vector2(3.2f, 0.6f), 0.12f, ""); vcap.color = new Color(0.85f, 0.85f, 1f);
        var vs = Add<FirSay>(vd.gameObject); vs.line = "* " + CaptionFor("sc_void"); vs.caption = vcap;
        vs.voice = Src(vd, new Vector3(0, 0.5f, 0), Clip("Scream/sc_void"), false, 1f, 1f, 10f, false);
        SetInteract(vs, "Scream into the void", 3f);
        // a skeleton version of a very famous (and very old) painting
        Box(g, "Frame", new Vector3(2260.06f, 3f, 40f), new Vector3(0.05f, 2.4f, 1.9f), M("GlowGold"), false);
        for (int k = 0; k < 6; k++) Box(g, "Sky stripe", new Vector3(2260.09f, 3.9f - k * 0.18f, 40f), new Vector3(0.02f, 0.18f, 1.7f), M(k % 2 == 0 ? "InkOrange" : "GlowRed"), false);
        Box(g, "Water", new Vector3(2260.09f, 2.4f, 40f), new Vector3(0.02f, 1.1f, 1.7f), M("InkBlue"), false);
        DPrim(PrimitiveType.Sphere, "Skull", g, new Vector3(2260.12f, 2.6f, 40f), new Vector3(0.02f, 0.5f, 0.38f), M("BoneWhite"));
        DPrim(PrimitiveType.Sphere, "ScreamingMouth", g, new Vector3(2260.14f, 2.45f, 40f), new Vector3(0.02f, 0.16f, 0.1f), M("Black"));
        foreach (float hz in new[] { -0.22f, 0.22f }) DPrim(PrimitiveType.Cube, "Hand", g, new Vector3(2260.14f, 2.6f, 40f + hz), new Vector3(0.02f, 0.35f, 0.07f), M("BoneWhite"));
        Text("PaintingLabel", g, new Vector3(2260.1f, 1.55f, 40f), Face(Vector3.right), new Vector2(2f, 0.3f), 0.09f, "after a famous 1893 painting (but with more bones)").color = Color.white;
        // the people
        Folk(g, "Ms Hush", "#a1887f", 0, new Vector3(2286f, 0, 30f), 200f, "Scream/sc_hush");
        Gear("Ms Hush", 6, PrimitiveType.Cube, new Vector3(0, -0.28f, 0.1f), new Vector3(0.03f, 0.12f, 0.03f), "BoneWhite");
        Folk(g, "Hollerbone", "#ff7043", 2, new Vector3(2276f, 0, 28f), 160f, "Scream/sc_hobby");
        report.Add("The Scream Room: a scream booth and meter, a screaming choir on the console, a screaming goat, a kettle, a rubber chicken, the void, a famous painting with more bones; 6 voiced skeletons and 5 gags");
    }

    // =================================================================== four more rooms: a summer backyard of inventions, a notebook middle school, an undersea town, a battle royale island

    static void BuildInventorsBackyard(Transform root)
    {
        var g = AreaShell(root, "The Inventors' Backyard", 2310f, 2350f, 20f, 56f, 18f, "Grass", "PixelSky", "Sky",
                          "THE INVENTORS' BACKYARD", new Color(1f, 0.6f, 0.1f), "two step-brothers, one summer, and something enormous built before lunch", "INVENTORS' BACKYARD");
        Plaque(g, new Vector3(2338f, 0, 23.5f), "<b>THE INVENTORS' BACKYARD</b>\na summer backyard, inspired by cartoons\nabout inventing something new\nevery single day of the holidays\n\n<size=80%>(press to hear more)</size>", "Four/iv_plaque");
        // the house, the fence, the big tree
        Box(g, "House", new Vector3(2330f, 3f, 54f), new Vector3(16f, 6f, 4f), M("Wall2"));
        foreach (float sgn in new[] { -1f, 1f }) Box(g, "HouseRoof", new Vector3(2330f, 6.9f, 54f + sgn * 1f), new Vector3(16.6f, 0.2f, 2.6f), M("CaneRed"), false).transform.localRotation = Quaternion.Euler(sgn * 35f, 0, 0);
        Box(g, "BackDoor", new Vector3(2330f, 1.1f, 51.97f), new Vector3(1.2f, 2.2f, 0.05f), M("DoorWood"), false);
        for (int k = 0; k < 18; k++) DPrim(PrimitiveType.Cube, "FencePlank", g, new Vector3(2311f + k * 2.2f, 0.9f, 20.3f), new Vector3(2f, 1.8f, 0.08f), M("DeskWood"));
        TreeAt(g, "BigTree", new Vector3(2314f, 0, 46f), 0f, 3.2f, false);
        // the contraption: a rollercoaster in the backyard (it vanishes when Mom pulls in)
        var mom = Gg(g, "Mom's car pulls in", new Vector3(2347f, 0, 26f), -90f, "Four/iv_mom", 1.6f, 1.6f);
        Box(mom.transform, "Sign", new Vector3(0, 1.2f, 0), new Vector3(1.4f, 0.8f, 0.06f), M("Board"), false);
        Text("SignText", mom.transform, new Vector3(0, 1.2f, -0.04f), Quaternion.identity, new Vector2(1.3f, 0.7f), 0.1f, "<b>MOM'S CAR</b>\n(press to see what happens)").color = Color.white;
        var coaster = Group(mom.transform, "The rollercoaster", Vector3.zero);
        var cw = coaster;   // built in the gag's frame (yaw -90): its local +x is world +z, local +z is world -x
        for (int k = 0; k < 30; k++)
        {
            float a = k * Mathf.PI * 2f / 30f;
            var p = new Vector3(Mathf.Sin(a) * 7f + 4f, 2.5f + Mathf.Sin(a * 2f) * 2f, 17f + Mathf.Cos(a) * 9f);
            Prim(PrimitiveType.Cube, "Track", cw, p, new Vector3(1.4f, 0.12f, 2f), M(k % 2 == 0 ? "InkOrange" : "GlowYellow")).transform.localRotation = Quaternion.Euler(0, a * Mathf.Rad2Deg + 90f, 0);
            if (k % 3 == 0) Prim(PrimitiveType.Cylinder, "Strut", cw, new Vector3(p.x, p.y / 2f, p.z), new Vector3(0.12f, p.y / 2f, 0.12f), M("Metal"));
        }
        for (int k = 0; k < 10; k++)
        {
            float a = k * Mathf.PI * 2f / 10f;
            Prim(PrimitiveType.Cube, "Loop", cw, new Vector3(4f, 5f + Mathf.Sin(a) * 3f, 26f + Mathf.Cos(a) * 3f), new Vector3(1.4f, 0.12f, 1.9f), M("InkBlue")).transform.localRotation = Quaternion.Euler(-a * Mathf.Rad2Deg, 0, 0);
        }
        mom.target = coaster; mom.scaleBy = 0.01f; mom.inTime = 0.4f; mom.holdTime = 4f; mom.outTime = 1.2f;
        // the inventors and their sister
        Box(g, "Workbench", new Vector3(2322f, 0.45f, 30f), new Vector3(3f, 0.9f, 1.2f), M("DeskWood"));
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Cube, "Blueprint", g, new Vector3(2321f + k * 0.6f, 0.92f, 30f), new Vector3(0.5f, 0.01f, 0.7f), M("InkBlue"));
        Folk(g, "Tinker Tri", "#ff8a65", 16, new Vector3(2321f, 0, 28.6f), 180f, "Four/iv_bro1");
        Folk(g, "Quiet Quill", "#81c784", 0, new Vector3(2323.4f, 0, 28.6f), 180f, "Four/iv_bro2", 1.1f);
        Folk(g, "Big Sis", "#ff80ab", 9, new Vector3(2340f, 0, 50f), 200f, "Four/iv_sis");
        // the gnome who's a secret agent, and the scientist next door
        Folk(g, "Agent Gnome", "#8d6e63", 0, new Vector3(2344f, 0, 40f), -90f, "Four/iv_gnome", 0.5f);
        Gear("Agent Gnome", 2, PrimitiveType.Capsule, new Vector3(0, 0.22f, 0), new Vector3(0.24f, 0.3f, 0.24f), "GiftRed");
        Gear("Agent Gnome", 2, PrimitiveType.Cube, new Vector3(0, 0.05f, 0.17f), new Vector3(0.26f, 0.06f, 0.02f), "Black");
        Box(g, "LabNextDoor", new Vector3(2347f, 2.5f, 47f), new Vector3(5f, 5f, 6f), M("GemPurple"));
        var doc = Gg(g, "The Shrink Ray 3000", new Vector3(2343.6f, 0, 47f), -90f, "Four/iv_doc", 1.6f, 2.2f);
        Box(doc.transform, "RayBase", new Vector3(0, 0.6f, 0), new Vector3(0.8f, 1.2f, 0.8f), M("Black"), false);
        var ray = Group(doc.transform, "Ray", new Vector3(0, 1.3f, 0));
        Prim(PrimitiveType.Cylinder, "Barrel", ray, new Vector3(0, 0, 0.4f), new Vector3(0.3f, 0.5f, 0.3f), M("Metal")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Sphere, "Tip", ray, new Vector3(0, 0, 0.95f), Vector3.one * 0.25f, M("GlowGreen"));
        doc.target = ray; doc.wobble = 20f; doc.rotateBy = new Vector3(-20f, 0, 0);
        Folk(g, "Doctor Dreadbone", "#9575cd", 15, new Vector3(2344f, 0, 49.5f), -120f, "Four/iv_doc");
        report.Add("The Inventors' Backyard: a rollercoaster in the garden (gone the moment Mom pulls in), two inventors, a big sister, a secret-agent gnome, a shrink ray next door; 2 gags");
    }

    static void BuildNotebookSchool(Transform root)
    {
        var g = AreaShell(root, "The Notebook School", 2360f, 2396f, 20f, 50f, 6f, "Paper", "Paper", "Paper",
                          "THE NOTEBOOK SCHOOL", new Color(0.1f, 0.1f, 0.1f), "a middle school drawn in pencil, in somebody's diary. sorry: journal.", "NOTEBOOK SCHOOL");
        Plaque(g, new Vector3(2384f, 0, 23.5f), "<b>THE NOTEBOOK SCHOOL</b>\ninspired by books drawn in a diary\nabout surviving middle school\n\n<size=80%>(press to hear more)</size>", "Four/nb_plaque");
        // blue lines and a red margin on every wall: it's all notebook paper
        for (int k = 0; k < 9; k++)
        {
            float y = 0.6f + k * 0.6f;
            foreach (float wx in new[] { 2360.05f, 2395.95f }) DPrim(PrimitiveType.Cube, "Line", g, new Vector3(wx, y, 35f), new Vector3(0.01f, 0.02f, 30f), M("InkBlue"));
            DPrim(PrimitiveType.Cube, "Line", g, new Vector3(2378f, y, 49.95f), new Vector3(36f, 0.02f, 0.01f), M("InkBlue"));
        }
        DPrim(PrimitiveType.Cube, "Margin", g, new Vector3(2364f, 3f, 49.94f), new Vector3(0.04f, 6f, 0.01f), M("GlowRed"));
        // lockers down one side
        for (int k = 0; k < 12; k++)
        {
            Box(g, "Locker", new Vector3(2361f, 1.1f, 27f + k * 1.0f), new Vector3(0.9f, 2.2f, 0.9f), M("CaneWhite"));
            DPrim(PrimitiveType.Cube, "LockerOutline", g, new Vector3(2361.46f, 1.1f, 27f + k * 1.0f), new Vector3(0.01f, 2.1f, 0.85f), M("Black"));
            DPrim(PrimitiveType.Cube, "LockerVent", g, new Vector3(2361.47f, 1.8f, 27f + k * 1.0f), new Vector3(0.01f, 0.25f, 0.5f), M("CaneWhite"));
        }
        // the cursed cheese on the court
        Box(g, "Court", new Vector3(2380f, 0.006f, 38f), new Vector3(12f, 0.01f, 8f), M("CaneWhite"), false);
        foreach (float cz in new[] { 34f, 42f }) DPrim(PrimitiveType.Cube, "CourtLine", g, new Vector3(2380f, 0.012f, cz), new Vector3(12f, 0.01f, 0.06f), M("Black"));
        var ch = Gg(g, "The cursed cheese", new Vector3(2380f, 0, 38f), 180f, "Four/nb_cheese", 1.2f, 0.8f);
        var cheese = Group(ch.transform, "Cheese", new Vector3(0, 0.1f, 0));
        Prim(PrimitiveType.Cube, "Cheese", cheese, Vector3.zero, new Vector3(0.3f, 0.12f, 0.25f), M("GlowYellow"));
        for (int k = 0; k < 3; k++) Prim(PrimitiveType.Sphere, "Stink", cheese, new Vector3(-0.1f + k * 0.1f, 0.25f + k * 0.08f, 0), Vector3.one * 0.06f, M("GlowGreen"));
        ch.target = cheese; ch.wobble = 15f; ch.bob = 0.05f;
        Text("CheeseSign", g, new Vector3(2380f, 1.5f, 33.9f), Face(Vector3.back), new Vector2(3f, 0.4f), 0.18f, "<b>DO NOT TOUCH THE CHEESE</b>").color = Color.black;
        // the gym rope
        DPrim(PrimitiveType.Cylinder, "Rope", g, new Vector3(2392f, 3f, 30f), new Vector3(0.08f, 3f, 0.08f), M("Trunk"));
        // the basement band
        Box(g, "Drum", new Vector3(2390f, 0.4f, 46f), new Vector3(0.8f, 0.8f, 0.8f), M("CaneRed"));
        DPrim(PrimitiveType.Cylinder, "Cymbal", g, new Vector3(2391f, 1.3f, 46.5f), new Vector3(0.6f, 0.01f, 0.6f), M("GlowGold"));
        Text("BandName", g, new Vector3(2390f, 3.4f, 49.9f), Face(Vector3.back), new Vector2(4f, 0.6f), 0.4f, "<b>LOUD BONES</b>").color = Color.black;
        // the kids (stick figures: black bones on white paper)
        Folk(g, "The New Kid", "#212121", 16, new Vector3(2372f, 0, 30f), 180f, "Four/nb_kid");
        Folk(g, "Best Friend Benny", "#212121", 5, new Vector3(2374f, 0, 30.5f), 200f, "Four/nb_friend", 1.1f);
        Folk(g, "Big Bro Dez", "#212121", 9, new Vector3(2388f, 0, 46.5f), 180f, "Four/nb_brother");
        Folk(g, "Coach Gruff", "#212121", 20, new Vector3(2390f, 0, 30f), -90f, "Four/nb_gym");
        Gear("Coach Gruff", 2, PrimitiveType.Sphere, new Vector3(0, -0.15f, 0.12f), Vector3.one * 0.06f, "Metal");
        report.Add("The Notebook School: lined-paper walls, lockers, the cursed cheese, a gym rope nobody can climb, a basement band; 4 stick-figure skeletons and 1 gag");
    }

    static void BuildSeabedTown(Transform root)
    {
        var g = AreaShell(root, "The Seabed Town", 2410f, 2450f, 20f, 56f, 14f, "PixelSand", "InkBlue", "InkBlue",
                          "THE SEABED TOWN", new Color(1f, 0.9f, 0.2f), "a little town at the bottom of the sea. the burgers are mostly kelp.", "SEABED TOWN");
        Plaque(g, new Vector3(2438f, 0, 23.5f), "<b>THE SEABED TOWN</b>\nan undersea town, inspired by cartoons\nabout life at the bottom of the sea\n\n<size=80%>(press to hear more)</size>", "Four/sb_plaque");
        // kelp, coral, rising bubbles
        for (int k = 0; k < 24; k++)
        {
            var kp = new Vector3(2412f + (k * 13 % 36), 0, 26f + (k * 7 % 28));
            for (int s = 0; s < 4; s++) DPrim(PrimitiveType.Capsule, "Kelp", g, kp + new Vector3(Mathf.Sin(s + k) * 0.15f, 0.6f + s * 1.1f, 0), new Vector3(0.15f, 0.6f, 0.15f), M("GiftGreen"));
        }
        for (int k = 0; k < 10; k++) DPrim(PrimitiveType.Sphere, "Coral", g, new Vector3(2412f + k * 3.8f, 0.3f, 54.5f - (k % 2)), new Vector3(1f, 0.8f, 0.8f), M(k % 3 == 0 ? "BonePink" : k % 3 == 1 ? "InkOrange" : "GemPurple"));
        var bub = Group(g, "Bubbles", new Vector3(2430f, 2f, 38f));
        var bubs = new List<Transform>();
        for (int k = 0; k < 20; k++)
        {
            var b = Group(bub, "Bubble", new Vector3(Mathf.Cos(k * 1.9f) * (4f + k % 9), (k * 5 % 9), Mathf.Sin(k * 1.9f) * (4f + k % 9)));
            Prim(PrimitiveType.Sphere, "Bubble", b, Vector3.zero, Vector3.one * (0.2f + (k % 3) * 0.1f), M("Glass"));
            bubs.Add(b);
        }
        var br = Add<FirRide>(bub.gameObject); br.kind = 0; br.rotor = bub; br.axis = Vector3.up; br.degPerSec = 5f; br.bobbers = bubs.ToArray(); br.bobHeight = 1.5f; br.bobSpeed = 0.6f;
        // the jellyfish field
        var jf = Group(g, "Jellyfish", new Vector3(2440f, 3f, 44f));
        var jl = new List<Transform>();
        for (int k = 0; k < 8; k++)
        {
            var j = Group(jf, "Jelly", new Vector3(Mathf.Cos(k * 0.8f) * 4f, (k % 3) * 0.8f, Mathf.Sin(k * 0.8f) * 4f));
            Prim(PrimitiveType.Sphere, "Bell", j, Vector3.zero, new Vector3(0.6f, 0.4f, 0.6f), M("BonePink"));
            for (int t = 0; t < 3; t++) Prim(PrimitiveType.Cylinder, "Tentacle", j, new Vector3(-0.15f + t * 0.15f, -0.4f, 0), new Vector3(0.03f, 0.3f, 0.03f), M("BonePink"));
            jl.Add(j);
        }
        var jr = Add<FirRide>(jf.gameObject); jr.kind = 0; jr.rotor = jf; jr.axis = Vector3.up; jr.degPerSec = 10f; jr.bobbers = jl.ToArray(); jr.bobHeight = 0.6f; jr.bobSpeed = 1.2f;
        var jg = Gg(g, "Jellyfish field sign", new Vector3(2440f, 0, 37f), 180f, "Four/sb_jelly", 1.2f, 2f);
        Box(jg.transform, "Post", new Vector3(0, 0.7f, 0), new Vector3(0.1f, 1.4f, 0.1f), M("DeskWood"), false);
        var jsign = Group(jg.transform, "Board", new Vector3(0, 1.5f, 0));
        Prim(PrimitiveType.Cube, "Board", jsign, Vector3.zero, new Vector3(1.2f, 0.5f, 0.05f), M("DeskWood"));
        Text("JellyText", jsign, new Vector3(0, 0, 0.03f), Face(Vector3.forward), new Vector2(1.1f, 0.4f), 0.12f, "<b>JELLYFISH FIELDS</b>").color = Color.white;
        jg.target = jsign; jg.wobble = 10f;
        // the burger shack: the Crusty Clam
        Box(g, "Shack", new Vector3(2420f, 2.2f, 48f), new Vector3(10f, 4.4f, 6f), M("DeskWood"));
        Box(g, "ShackRoof", new Vector3(2420f, 4.6f, 48f), new Vector3(10.6f, 0.4f, 6.6f), M("InkOrange"));
        Box(g, "Window", new Vector3(2420f, 1.6f, 44.97f), new Vector3(4f, 1.2f, 0.05f), M("Black"), false);
        Box(g, "ServeCounter", new Vector3(2420f, 1f, 44.6f), new Vector3(4f, 0.1f, 0.6f), M("CaneWhite"));
        Text("ShackSign", g, new Vector3(2420f, 3.6f, 44.95f), Face(Vector3.back), new Vector2(9f, 0.8f), 0.6f, "<b>THE CRUSTY CLAM</b>").color = new Color(1f, 0.9f, 0.3f);
        Folk(g, "Fry Cook Flip", "#ffeb3b", 16, new Vector3(2418.5f, 0, 45.6f), 180f, "Four/sb_cook");
        Folk(g, "Grumpy Till", "#80cbc4", 0, new Vector3(2421.5f, 0, 45.6f), 180f, "Four/sb_cashier");
        Folk(g, "Boss Pinch", "#e53935", 15, new Vector3(2425.8f, 0, 44f), 200f, "Four/sb_boss");
        foreach (float cx in new[] { -0.2f, 0.2f }) Gear("Boss Pinch", cx < 0 ? 5 : 6, PrimitiveType.Sphere, new Vector3(0, -0.3f, 0.05f), new Vector3(0.18f, 0.12f, 0.1f), "GlowRed");
        var flip = Gg(g, "The flipping burger", new Vector3(2416f, 0, 44.4f), 180f, "Four/sb_burger", 1f, 1.4f);
        Box(flip.transform, "Grill", new Vector3(0, 0.5f, 0), new Vector3(0.8f, 1f, 0.6f), M("Black"), false);
        var patty = Group(flip.transform, "Patty", new Vector3(0, 1.05f, 0));
        Prim(PrimitiveType.Cylinder, "Patty", patty, Vector3.zero, new Vector3(0.35f, 0.04f, 0.35f), M("Trunk"));
        flip.target = patty; flip.moveBy = new Vector3(0, 2.6f, 0); flip.rotateBy = new Vector3(540f, 0, 0); flip.inTime = 0.6f;
        // a rock house, a glass dome, the starfish
        Prim(PrimitiveType.Sphere, "RockHouse", g, new Vector3(2445f, 0, 28f), new Vector3(5f, 4f, 5f), M("Stone"));
        Prim(PrimitiveType.Sphere, "Dome", g, new Vector3(2414f, 0, 28f), new Vector3(6f, 6f, 6f), M("Glass"));
        DPrim(PrimitiveType.Cylinder, "DomeTree", g, new Vector3(2414f, 1.2f, 28f), new Vector3(0.3f, 1.2f, 0.3f), M("Trunk"));
        Folk(g, "Lounging Lou", "#ff8a80", 9, new Vector3(2441.5f, 0.1f, 28f), -90f, "Four/sb_star");
        report.Add("The Seabed Town: kelp, coral, rising bubbles, a jellyfish field, the Crusty Clam (a fry cook, a grumpy cashier, a money-loving boss), a rock house, a glass dome, a starfish; 2 gags");
    }

    static void BuildBattleIsland(Transform root)
    {
        var g = AreaShell(root, "Battle Island", 2460f, 2510f, 20f, 70f, 22f, "PixelGreen", "PixelSky", "Sky",
                          "BATTLE ISLAND", new Color(0.4f, 0.7f, 1f), "a hundred skeletons jump out of a bus. one of them wins. the rest dance anyway.", "BATTLE ISLAND");
        Plaque(g, new Vector3(2490f, 0, 23.5f), "<b>BATTLE ISLAND</b>\na battle royale island, inspired by the games\nwhere players jump out of a flying bus\n(here, everyone is a skeleton)\n\n<size=80%>(press to hear more)</size>", "Four/br_plaque");
        // the bus, flying round overhead on a balloon
        var bus = Group(g, "The bus route", new Vector3(2485f, 16f, 45f));
        var b = Group(bus, "The bus", new Vector3(14f, 0, 0));
        Prim(PrimitiveType.Cube, "Body", b, Vector3.zero, new Vector3(2.4f, 2.4f, 6f), M("InkBlue"));
        Prim(PrimitiveType.Cube, "Windows", b, new Vector3(0, 0.5f, 0), new Vector3(2.45f, 0.8f, 5f), M("Glass"));
        Prim(PrimitiveType.Sphere, "Balloon", b, new Vector3(0, 3.5f, 0), new Vector3(3.4f, 3f, 3.4f), M("GlowRed"));
        b.localRotation = Quaternion.Euler(0, 0, 0);
        var busr = Add<FirRide>(bus.gameObject); busr.kind = 0; busr.rotor = bus; busr.axis = Vector3.up; busr.degPerSec = 12f;
        var thank = Gg(g, "Thank the bus driver", new Vector3(2470f, 0, 26f), 180f, "Four/br_bus", 1.4f, 2f);
        Box(thank.transform, "Post", new Vector3(0, 0.7f, 0), new Vector3(0.1f, 1.4f, 0.1f), M("Metal"), false);
        var tb = Group(thank.transform, "Button", new Vector3(0, 1.5f, 0));
        Prim(PrimitiveType.Cube, "Panel", tb, Vector3.zero, new Vector3(1.2f, 0.5f, 0.06f), M("GlowYellow"));
        Text("Thanks", tb, new Vector3(0, 0, 0.04f), Face(Vector3.forward), new Vector2(1.1f, 0.4f), 0.11f, "<b>THANK THE BUS DRIVER</b>").color = Color.black;
        thank.target = tb; thank.wobble = 10f;
        // the storm (a purple ring that closes in)
        var st = Gg(g, "The storm", new Vector3(2485f, 0, 48f), 180f, "Four/br_storm", 2f, 1f);
        Prim(PrimitiveType.Cylinder, "Circle", st.transform, new Vector3(0, 0.01f, 0), new Vector3(2f, 0.005f, 2f), M("CaneWhite"));
        var storm = Group(st.transform, "Storm wall", Vector3.zero);
        for (int k = 0; k < 24; k++)
        {
            float a = k * Mathf.PI / 12f;
            Prim(PrimitiveType.Cube, "Storm", storm, new Vector3(Mathf.Cos(a) * 9f, 4f, Mathf.Sin(a) * 9f), new Vector3(2.4f, 8f, 0.1f), M("PortalPurple")).transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90f, 0);
        }
        st.target = storm; st.scaleBy = 0.15f; st.inTime = 3f; st.hideWhenIdle = true;
        // chests, a supply drop, ramps somebody built
        var chest = Gg(g, "A glowing chest", new Vector3(2466f, 0, 40f), 90f, "Four/br_chest", 1.4f, 1.2f);
        Box(chest.transform, "Box", new Vector3(0, 0.35f, 0), new Vector3(1.2f, 0.7f, 0.8f), M("GlowGold"), false);
        var lid = Group(chest.transform, "Lid", new Vector3(0, 0.7f, -0.4f));
        Prim(PrimitiveType.Cube, "Lid", lid, new Vector3(0, 0.1f, 0.4f), new Vector3(1.22f, 0.2f, 0.82f), M("GlowGold"));
        Prim(PrimitiveType.Cube, "Spoon", chest.transform, new Vector3(0, 0.8f, 0), new Vector3(0.05f, 0.3f, 0.03f), M("GlowYellow"));
        chest.target = lid; chest.rotateBy = new Vector3(-100f, 0, 0);
        var drop = Gg(g, "Supply drop", new Vector3(2504f, 0, 40f), -90f, "Four/br_drop", 1.6f, 1.6f);
        var crate = Group(drop.transform, "Crate", new Vector3(0, 8f, 0));
        Prim(PrimitiveType.Cube, "Crate", crate, new Vector3(0, 0.5f, 0), Vector3.one * 1f, M("InkBlue"));
        Prim(PrimitiveType.Sphere, "Chute", crate, new Vector3(0, 2.4f, 0), new Vector3(2.6f, 1.2f, 2.6f), M("CaneWhite"));
        drop.target = crate; drop.moveBy = new Vector3(0, -8f, 0); drop.inTime = 2.5f; drop.hideWhenIdle = true;
        for (int k = 0; k < 5; k++)
        {
            var rp = Box(g, "Ramp " + k, new Vector3(2496f, 0.9f + k * 1.6f, 58f + k * 2.6f), new Vector3(2.6f, 0.15f, 3.4f), M("DeskWood"));
            rp.transform.localRotation = Quaternion.Euler(-32f, 0, 0);
        }
        // the skeletons: a builder, a dancer, a lost newbie, more dancers
        Folk(g, "Builder Bob", "#ff7043", 12, new Vector3(2493f, 0, 56f), 180f, "Four/br_builder");
        Gear("Builder Bob", 6, PrimitiveType.Cube, new Vector3(0, -0.35f, 0.08f), new Vector3(0.06f, 0.4f, 0.06f), "Metal");
        Folk(g, "Dancing Dee", "#ab47bc", 7, new Vector3(2480f, 0, 34f), 180f, "Four/br_dancer");
        for (int k = 0; k < 3; k++) Folk(g, "Emote " + (k + 1), new[] { "#26c6da", "#ffee58", "#66bb6a" }[k], new[] { 18, 24, 29 }[k], new Vector3(2476f + k * 1.6f, 0, 36f), 180f, "Four/br_dancer", 0.95f);
        Folk(g, "Newbie Ned", "#bdbdbd", 2, new Vector3(2502f, 0, 30f), 220f, "Four/br_noob");
        Text("Victory", g, new Vector3(2485f, 8f, 69.85f), Face(Vector3.back), new Vector2(16f, 2f), 1.4f, "<b>VICTORY!</b>").color = new Color(1f, 0.85f, 0.2f);
        report.Add("Battle Island: a bus flying round on a balloon, a storm that closes in, a chest, a supply drop, ramps somebody built, a builder, a lost newbie, dancers; 4 gags");
    }

    // =================================================================== the containment facility (in the style of collaborative online horror fiction; every anomaly is new)

    /// <summary>A glass-fronted containment cell along the north wall, with its designation plate.</summary>
    static Transform Cell(Transform g, float x, string id, string name, string cls)
    {
        var c = Group(g, "Cell " + id, Vector3.zero);
        Box(c, "CellL", new Vector3(x - 3f, 2f, 51f), new Vector3(0.2f, 4f, 6f), M("LabGrey"));
        Box(c, "CellR", new Vector3(x + 3f, 2f, 51f), new Vector3(0.2f, 4f, 6f), M("LabGrey"));
        Box(c, "CellFloor", new Vector3(x, 0.01f, 51f), new Vector3(6f, 0.02f, 6f), M("TileOff"), false);
        DPrim(PrimitiveType.Cube, "HazardStripe", c, new Vector3(x, 0.02f, 48.1f), new Vector3(6f, 0.02f, 0.2f), M("GlowYellow"));
        Box(c, "Plate", new Vector3(x - 1.6f, 3.3f, 47.95f), new Vector3(2.4f, 0.8f, 0.05f), M("Black"), false);
        Text("Designation", c, new Vector3(x - 1.6f, 3.3f, 47.92f), Face(Vector3.back), new Vector2(2.3f, 0.7f), 0.12f, "<b>" + id + "</b>\n" + name + "\n<size=80%>class: " + cls + "</size>").color = new Color(1f, 0.85f, 0.3f);
        return c;
    }

    static void BuildContainment(Transform root)
    {
        var g = AreaShell(root, "The Containment Facility", 2520f, 2560f, 20f, 56f, 6f, "LabFloor", "LabGrey", "LabWhite",
                          "SITE BONE: CONTAINMENT", new Color(0.9f, 0.9f, 0.9f), "secure. contain. try not to make eye contact with the duck.", "SCP ROOM");
        Plaque(g, new Vector3(2548f, 0, 23.5f), "<b>SITE BONE</b>\na containment facility in the style of\ncollaborative online horror fiction\n(every anomaly in here is brand new)\n\n<size=80%>(press to hear more)</size>", "Vault/sp_plaque");
        // the breach alarm: red lights spin
        var alarm = Gg(g, "Breach alarm", new Vector3(2524f, 0, 30f), 90f, "Vault/sp_alarm", 1.2f, 2f);
        Box(alarm.transform, "Panel", new Vector3(0, 1.2f, 0.4f), new Vector3(0.8f, 0.6f, 0.1f), M("Black"), false);
        Prim(PrimitiveType.Cylinder, "BigRedButton", alarm.transform, new Vector3(0, 1.2f, 0.33f), new Vector3(0.3f, 0.05f, 0.3f), M("GlowRed")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        var beacon = Group(alarm.transform, "Beacons", new Vector3(0, 5.5f, 4f));
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Sphere, "Beacon", beacon, new Vector3(Mathf.Cos(k * 1.05f) * 6f, 0, Mathf.Sin(k * 1.05f) * 6f), new Vector3(0.5f, 0.3f, 0.5f), M("GlowRed"));
        beacon.localScale = Vector3.one;
        alarm.target = beacon; alarm.hideWhenIdle = true; alarm.spin = 360f; alarm.holdTime = 5f;
        // the cells
        Cell(g, 2526f, "BONE-014", "the sock dryer", "hmm");
        var dryer = Gg(g, "BONE-014", new Vector3(2526f, 0, 51f), 180f, "Vault/sp_sock", 1.4f, 1.4f);
        Box(dryer.transform, "Dryer", new Vector3(0, 0.5f, 0), new Vector3(1f, 1f, 0.8f), M("Porcelain"), false);
        var socks = Group(dryer.transform, "Socks", new Vector3(0, 0.5f, 0.6f));
        for (int k = 0; k < 14; k++) Prim(PrimitiveType.Cube, "Sock", socks, new Vector3(Mathf.Cos(k * 2.1f) * (0.3f + k * 0.06f), -0.4f + (k % 4) * 0.12f, 0.2f + k * 0.08f), new Vector3(0.12f, 0.06f, 0.3f), M(new[] { "GiftRed", "InkBlue", "GlowYellow", "GiftGreen", "BonePink", "CaneWhite" }[k % 6])).transform.localRotation = Quaternion.Euler(0, k * 40f, 0);
        dryer.target = socks; dryer.hideWhenIdle = true; dryer.scaleBy = 1.6f; dryer.inTime = 1f;
        Cell(g, 2533f, "BONE-027", "the polite void", "sorry");
        var box = Gg(g, "BONE-027", new Vector3(2533f, 0, 51f), 180f, "Vault/sp_box", 1.4f, 1.4f);
        Box(box.transform, "Box", new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.9f), M("Cardboard"), false);
        Box(box.transform, "Dark", new Vector3(0, 0.91f, 0), new Vector3(0.8f, 0.01f, 0.8f), M("Black"), false);
        var flap = Group(box.transform, "Flap", new Vector3(0, 0.9f, -0.45f));
        Prim(PrimitiveType.Cube, "Flap", flap, new Vector3(0, 0.01f, 0.45f), new Vector3(0.92f, 0.02f, 0.92f), M("Cardboard"));
        box.target = flap; box.rotateBy = new Vector3(-120f, 0, 0); box.inTime = 0.8f;
        Cell(g, 2540f, "BONE-101", "a chair that is slightly too comfortable", "uh-oh");
        var chair = Gg(g, "BONE-101", new Vector3(2540f, 0, 51f), 180f, "Vault/sp_chair", 1.4f, 1.6f);
        var ch = Group(chair.transform, "Chair", Vector3.zero);
        Prim(PrimitiveType.Cube, "Seat", ch, new Vector3(0, 0.45f, 0), new Vector3(1f, 0.5f, 1f), M("Curtain"));
        Prim(PrimitiveType.Cube, "Back", ch, new Vector3(0, 1.1f, 0.42f), new Vector3(1f, 1f, 0.2f), M("Curtain"));
        foreach (float ax in new[] { -0.5f, 0.5f }) Prim(PrimitiveType.Cube, "Arm", ch, new Vector3(ax, 0.8f, 0), new Vector3(0.18f, 0.25f, 1f), M("Curtain"));
        chair.target = ch; chair.scaleBy = 1.15f; chair.bob = 0.04f;
        Folk(g, "Volunteer 4", "#ff9800", 9, new Vector3(2541.2f, 0, 50f), 200f, "Vault/sp_volunteer");
        Gear("Volunteer 4", 1, PrimitiveType.Cube, new Vector3(0, 0.15f, 0), new Vector3(0.42f, 0.55f, 0.3f), "InkOrange");
        Cell(g, 2547f, "BONE-666½", "the rubber duck that knows your name", "watch it");
        var duck = Gg(g, "BONE-666½", new Vector3(2547f, 0, 51f), 180f, "Vault/sp_duck", 1.2f, 1.4f);
        Box(duck.transform, "Pedestal", new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1f, 0.6f), M("Black"), false);
        var dk = Group(duck.transform, "Duck", new Vector3(0, 1f, 0));
        Prim(PrimitiveType.Sphere, "Body", dk, new Vector3(0, 0.12f, 0), new Vector3(0.3f, 0.22f, 0.35f), M("GlowYellow"));
        Prim(PrimitiveType.Sphere, "Head", dk, new Vector3(0, 0.3f, -0.1f), Vector3.one * 0.18f, M("GlowYellow"));
        Prim(PrimitiveType.Cube, "Beak", dk, new Vector3(0, 0.28f, -0.21f), new Vector3(0.1f, 0.03f, 0.08f), M("InkOrange"));
        foreach (float ex in new[] { -0.05f, 0.05f }) Prim(PrimitiveType.Sphere, "Eye", dk, new Vector3(ex, 0.34f, -0.18f), Vector3.one * 0.035f, M("GlowRedEye"));
        duck.target = dk; duck.rotateBy = new Vector3(0, 180f, 0); duck.inTime = 1.4f;
        Cell(g, 2554f, "BONE-310", "the vending machine that only sells spoons", "fine, really");
        var vend = Gg(g, "BONE-310", new Vector3(2554f, 0, 51f), 180f, "Vault/sp_vend", 1.4f, 2.2f);
        Box(vend.transform, "Machine", new Vector3(0, 1f, 0.2f), new Vector3(1.2f, 2f, 0.8f), M("HeroRed"), false);
        Box(vend.transform, "Glass", new Vector3(0, 1.2f, -0.21f), new Vector3(0.9f, 1.3f, 0.02f), M("Glass"), false);
        for (int k = 0; k < 9; k++) Prim(PrimitiveType.Cube, "Spoon", vend.transform, new Vector3(-0.3f + (k % 3) * 0.3f, 0.75f + (k / 3) * 0.4f, -0.1f), new Vector3(0.04f, 0.2f, 0.02f), M("Metal"));
        var sp = Group(vend.transform, "The spoon", new Vector3(0, 0.25f, -0.25f));
        Prim(PrimitiveType.Cube, "Handle", sp, Vector3.zero, new Vector3(0.04f, 0.25f, 0.02f), M("GlowGold"));
        sp.localRotation = Quaternion.Euler(0, 0, 90);
        vend.target = sp; vend.moveBy = new Vector3(0, 0, -0.6f); vend.spin = 120f; vend.hideWhenIdle = true;
        // BONE-999: the door that always leads to the lobby (it's the room's way out)
        Box(g, "Door999", new Vector3(2559.9f, 1.2f, 34f), new Vector3(0.05f, 2.4f, 1.3f), M("Metal"), false);
        Text("Door999Plate", g, new Vector3(2559.85f, 2.9f, 34f), Face(Vector3.left), new Vector2(2.4f, 0.6f), 0.13f, "<b>BONE-999</b>\na door that always leads to the lobby\n<size=80%>class: annoying</size>").color = new Color(1f, 0.85f, 0.3f);
        var door = TeleportButton(g, "BONE-999 (open the door)", new Vector3(2559.8f, 1.2f, 32.9f), Vector3.left, lobbyArrive, new Vector3(0.5f, 0.3f, 0.08f));
        door.sfx.clip = Clip("Vault/sp_door"); door.sfx.spatialBlend = 0f;
        // the records terminal, the staff
        var term = Group(g, "Records terminal", new Vector3(2530f, 0, 36f));
        Box(term, "Desk", new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.9f, 0.8f), M("LabGrey"));
        Box(term, "Screen", new Vector3(0, 1.3f, 0.2f), new Vector3(1f, 0.7f, 0.05f), M("GlowGreen"), false);
        var tcol = term.gameObject.AddComponent<BoxCollider>(); tcol.size = new Vector3(1.6f, 1.8f, 0.8f); tcol.center = new Vector3(0, 0.9f, 0);
        var tcap = Text("Caption", term, new Vector3(0, 2.2f, 0.18f), Face(Vector3.back), new Vector2(3.4f, 0.7f), 0.11f, ""); tcap.color = new Color(0.5f, 1f, 0.6f);
        var ts = Add<FirSay>(term.gameObject); ts.line = "> " + CaptionFor("sp_terminal"); ts.caption = tcap;
        ts.voice = Src(term, new Vector3(0, 1.3f, 0), Clip("Vault/sp_terminal"), false, 1f, 1f, 10f, false);
        SetInteract(ts, "Read the records", 2.5f);
        Folk(g, "Dr Clipboard", "#eceff1", 20, new Vector3(2532.5f, 0, 35f), 160f, "Vault/sp_researcher");
        Gear("Dr Clipboard", 1, PrimitiveType.Cube, new Vector3(0, 0f, 0), new Vector3(0.48f, 0.9f, 0.34f), "LabWhite");
        Gear("Dr Clipboard", 6, PrimitiveType.Cube, new Vector3(0, -0.28f, 0.1f), new Vector3(0.22f, 0.3f, 0.02f), "DeskWood");
        Folk(g, "Guard Sternbone", "#37474f", 11, new Vector3(2556f, 0, 30f), -90f, "Vault/sp_guard");
        Gear("Guard Sternbone", 2, PrimitiveType.Sphere, new Vector3(0, 0.1f, 0), new Vector3(0.32f, 0.22f, 0.34f), "Black");
        report.Add("The Containment Facility: five new anomalies in cells (the sock dryer, the polite void, the too-comfortable chair, the duck that knows your name, the spoon vending machine), BONE-999 (a door to the lobby), a breach alarm, a records terminal, staff; 6 gags");
    }

    // =================================================================== the interdimensional garage (and the dimensions through its portal)

    static Transform GreenPortal(Transform g, Vector3 at, float yaw)
    {
        var p = Group(g, "Green portal", at);
        p.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Cylinder, "Swirl", p, new Vector3(0, 1.3f, 0), new Vector3(2.2f, 0.04f, 2.6f), M("GlowGreen")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Cylinder, "Core", p, new Vector3(0, 1.3f, 0.02f), new Vector3(1.4f, 0.04f, 1.8f), M("AlienGlow")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        return p;
    }

    static void BuildPortalGarage(Transform root)
    {
        var g = AreaShell(root, "The Interdimensional Garage", 2570f, 2606f, 20f, 50f, 6f, "Concrete", "Wall2", "Ceiling",
                          "THE INTERDIMENSIONAL GARAGE", new Color(0.4f, 1f, 0.5f), "a cranky genius, an anxious assistant, and a portal to everywhere (mostly worse places)", "PORTAL GARAGE");
        Plaque(g, new Vector3(2594f, 0, 23.5f), "<b>THE INTERDIMENSIONAL GARAGE</b>\ninspired by cartoons about a cranky genius\ndragging his family through portals\n\n<size=80%>(press to hear more)</size>", "Portal/rm_plaque");
        // the garage: workbench, tools on the wall, a saucer under a sheet
        Box(g, "Workbench", new Vector3(2574f, 0.45f, 40f), new Vector3(1.2f, 0.9f, 6f), M("DeskWood"));
        Box(g, "Pegboard", new Vector3(2570.1f, 2f, 40f), new Vector3(0.05f, 1.6f, 6f), M("Pegboard"), false);
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Cube, "Tool", g, new Vector3(2570.2f, 1.7f + (k % 2) * 0.5f, 37.5f + k * 0.7f), new Vector3(0.04f, 0.4f, 0.08f), M(k % 3 == 0 ? "HeroRed" : "Metal"));
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Cylinder, "Beaker", g, new Vector3(2574f, 1.05f, 38f + k * 0.9f), new Vector3(0.15f, 0.15f, 0.15f), M(k % 2 == 0 ? "GlowGreen" : "GemPurple"));
        Prim(PrimitiveType.Sphere, "Saucer under a sheet", g, new Vector3(2598f, 0.5f, 42f), new Vector3(5f, 1.2f, 4f), M("CaneWhite"));
        Folk(g, "Grandpa Genius", "#b0bec5", 15, new Vector3(2575.4f, 0, 42f), -90f, "Portal/rm_genius");
        Gear("Grandpa Genius", 1, PrimitiveType.Cube, new Vector3(0, 0, 0), new Vector3(0.5f, 0.9f, 0.34f), "LabWhite");
        Gear("Grandpa Genius", 2, PrimitiveType.Cube, new Vector3(0, 0.17f, -0.03f), new Vector3(0.3f, 0.12f, 0.3f), "InkBlue", new Vector3(0, 0, 0));
        Folk(g, "Nervous Nate", "#ffe082", 2, new Vector3(2577.5f, 0, 39f), -110f, "Portal/rm_anxious", 0.9f);
        Gear("Nervous Nate", 1, PrimitiveType.Cube, new Vector3(0, 0.1f, 0), new Vector3(0.44f, 0.6f, 0.32f), "GlowYellow");
        // the thingamaflux
        var flux = Gg(g, "The thingamaflux", new Vector3(2586f, 0, 46f), 180f, "Portal/rm_flux", 1.2f, 1.6f);
        Box(flux.transform, "Shelf", new Vector3(0, 0.45f, 0), new Vector3(0.8f, 0.9f, 0.6f), M("DeskWood"), false);
        var fx = Group(flux.transform, "Flux", new Vector3(0, 1f, 0));
        Prim(PrimitiveType.Capsule, "Body", fx, new Vector3(0, 0.15f, 0), new Vector3(0.2f, 0.18f, 0.2f), M("BonePink"));
        Prim(PrimitiveType.Sphere, "Knob", fx, new Vector3(0.15f, 0.25f, 0), Vector3.one * 0.1f, M("GemPurple"));
        Prim(PrimitiveType.Cube, "Handle", fx, new Vector3(-0.18f, 0.1f, 0), new Vector3(0.04f, 0.04f, 0.2f), M("Metal"));
        fx.localScale = Vector3.one * 1.4f;
        flux.target = fx; flux.rotateBy = new Vector3(0, 0, 360f); flux.inTime = 0.8f;
        // the TV that picks up other dimensions
        var tv = Group(g, "Dimension TV", new Vector3(2590f, 0, 30f));
        Box(tv, "Cabinet", new Vector3(0, 0.5f, 0), new Vector3(1.8f, 1f, 0.8f), M("DoorWood"));
        Box(tv, "Set", new Vector3(0, 1.45f, 0), new Vector3(1.6f, 0.9f, 0.6f), M("Black"));
        var snow = Group(tv, "Static", new Vector3(0, 1.45f, -0.31f));
        Prim(PrimitiveType.Cube, "Snow", snow, Vector3.zero, new Vector3(1.4f, 0.75f, 0.01f), M("Mist"));
        string[] chMat = { "GlowFlame", "GiftGreen", "LabGrey", "InkBlue" };
        var chans = new GameObject[4]; var lines = new AudioSource[4]; var caps = new string[4];
        for (int c = 0; c < 4; c++)
        {
            var ch = Group(tv, "Dimension channel " + (c + 1), new Vector3(0, 1.45f, -0.32f));
            Prim(PrimitiveType.Cube, "Picture", ch, Vector3.zero, new Vector3(1.4f, 0.75f, 0.01f), M(chMat[c]));
            var mini = Mini(ch, "On screen " + c, new Vector3(0, -0.35f, -0.02f), 180f, 0.35f, "#e0e0e0");
            if (c == 0) Prim(PrimitiveType.Sphere, "Flames", mini[2], new Vector3(0, 0.2f, 0), Vector3.one * 0.3f, M("GlowFlame"));
            chans[c] = ch.gameObject;
            lines[c] = Src(tv, new Vector3(0, 1.45f, -0.4f), Clip("Portal/rm_tv" + (c + 1)), false, 1f, 1f, 12f, false);
            caps[c] = CaptionFor("rm_tv" + (c + 1));
        }
        var remote = Group(g, "Dimension remote", new Vector3(2588f, 0.9f, 28.5f));
        Box(g, "RemoteTable", new Vector3(2588f, 0.45f, 28.5f), new Vector3(0.6f, 0.9f, 0.6f), M("DeskWood"));
        Prim(PrimitiveType.Cube, "Remote", remote, new Vector3(0, 0.03f, 0), new Vector3(0.12f, 0.05f, 0.3f), M("Black"));
        remote.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.3f, 0.6f);
        var fc = Add<FirChannels>(remote.gameObject);
        fc.channels = chans; fc.lines = lines; fc.captions = caps; fc.staticScreen = snow.gameObject;
        fc.caption = Text("DimCaption", g, new Vector3(2590f, 2.4f, 29.55f), Face(Vector3.back), new Vector2(3.4f, 0.5f), 0.12f, "");
        fc.caption.color = Color.white;
        fc.click = Src(remote.transform, Vector3.zero, Clip("switch"), false, 1f, 1f, 6f, false);
        SetInteract(fc, "Change the dimension", 2.5f);
        // ---- the dimensions, through the portal (their own big space east of the garage)
        var d = Group(root, "The Dimensions", Vector3.zero);
        float x0 = 2620f, x1 = 2690f, z0 = 20f, z1 = 80f, h = 25f;
        Box(d, "Ground", new Vector3((x0 + x1) / 2f, -0.1f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), M("PortalPurple"));
        Box(d, "Sky", new Vector3((x0 + x1) / 2f, h + 0.1f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), M("NightSky"));
        Box(d, "WallW", new Vector3(x0 - 0.1f, h / 2, (z0 + z1) / 2f), new Vector3(0.2f, h, z1 - z0), M("NightSky"));
        Box(d, "WallE", new Vector3(x1 + 0.1f, h / 2, (z0 + z1) / 2f), new Vector3(0.2f, h, z1 - z0), M("NightSky"));
        Box(d, "WallS", new Vector3((x0 + x1) / 2f, h / 2, z0 - 0.1f), new Vector3(x1 - x0, h, 0.2f), M("NightSky"));
        Box(d, "WallN", new Vector3((x0 + x1) / 2f, h / 2, z1 + 0.1f), new Vector3(x1 - x0, h, 0.2f), M("NightSky"));
        for (int k = 0; k < 7; k++) DPrim(PrimitiveType.Sphere, "Planet", d, new Vector3(2625f + k * 10f, 18f + (k % 3) * 2f, 78f), Vector3.one * (2f + (k % 3)), M(new[] { "GlowGreen", "InkOrange", "GemPurple", "InkBlue" }[k % 4]));
        var pa = GreenPortal(g, new Vector3(2604f, 0, 36f), 90f);
        var pb = GreenPortal(d, new Vector3(2626f, 0, 30f), 90f);
        var gp = Add<FirPortal>(pa.gameObject); gp.a = pa; gp.b = pb; gp.radius = 0.9f;
        gp.sfx = Src(pa, new Vector3(0, 1f, 0), Clip("Portal/rm_portal"), false, 0f, 1f, 5f, false);
        Text("PortalSign", g, new Vector3(2603.9f, 3f, 36f), Face(Vector3.left), new Vector2(3f, 0.4f), 0.2f, "<b>THE PORTAL</b> <size=60%>(walk in. walk back out to come home)</size>").color = new Color(0.4f, 1f, 0.5f);
        // the sideways dimension: the furniture is on the wall
        var sw = Group(d, "Sideways dimension", Vector3.zero);
        Box(sw, "SideWall", new Vector3(2640f, 6f, 79.8f), new Vector3(14f, 12f, 0.2f), M("Wall2"));
        for (int k = 0; k < 4; k++) Box(sw, "Sideways furniture", new Vector3(2635f + k * 3.2f, 3f + (k % 2) * 3f, 79.2f), new Vector3(1.4f, 0.9f, 1f), M(new[] { "Seat", "DeskWood", "Curtain", "GiftBlue" }[k]), false);
        var sws = Gg(d, "Sideways sign", new Vector3(2640f, 0, 76f), 0f, "Portal/rm_sideways", 1.4f, 2f);
        var sb = Group(sws.transform, "Sign", new Vector3(0, 1.4f, 0));
        Prim(PrimitiveType.Cube, "Board", sb, Vector3.zero, new Vector3(1.4f, 0.6f, 0.05f), M("Board"));
        Text("SignText", sb, new Vector3(0, 0, -0.03f), Quaternion.identity, new Vector2(1.3f, 0.5f), 0.12f, "<b>THE SIDEWAYS DIMENSION</b>").color = Color.white;
        sb.localRotation = Quaternion.Euler(0, 0, 90);
        sws.target = sb; sws.rotateBy = new Vector3(0, 0, -90f);
        // the sandwich dimension
        for (int k = 0; k < 6; k++)
        {
            var sp = new Vector3(2660f + (k % 3) * 3f, 0, 40f + (k / 3) * 4f);
            DPrim(PrimitiveType.Cube, "SandwichBread", d, sp + new Vector3(0, 0.15f, 0), new Vector3(1.2f, 0.3f, 1.2f), M("Bun"));
            DPrim(PrimitiveType.Cube, "SandwichFilling", d, sp + new Vector3(0, 0.4f, 0), new Vector3(1.25f, 0.2f, 1.25f), M(k % 2 == 0 ? "GiftGreen" : "GiftRed"));
            DPrim(PrimitiveType.Cube, "SandwichTop", d, sp + new Vector3(0, 0.65f, 0), new Vector3(1.2f, 0.3f, 1.2f), M("Bun"));
        }
        var sand = Gg(d, "A sandwich person", new Vector3(2669f, 0, 44f), -90f, "Portal/rm_sandwich", 1.4f, 2f);
        var sp2 = Group(sand.transform, "Sandwich", Vector3.zero);
        Prim(PrimitiveType.Cube, "Bread", sp2, new Vector3(0, 0.5f, 0), new Vector3(1f, 0.25f, 1f), M("Bun"));
        Prim(PrimitiveType.Cube, "Filling", sp2, new Vector3(0, 0.75f, 0), new Vector3(1.05f, 0.25f, 1.05f), M("GiftGreen"));
        Prim(PrimitiveType.Cube, "Top", sp2, new Vector3(0, 1f, 0), new Vector3(1f, 0.25f, 1f), M("Bun"));
        foreach (float ex in new[] { -0.2f, 0.2f }) Prim(PrimitiveType.Sphere, "Eye", sp2, new Vector3(ex, 1.05f, -0.51f), Vector3.one * 0.1f, M("Eye"));
        Prim(PrimitiveType.Cube, "Leg", sp2, new Vector3(-0.2f, 0.2f, 0), new Vector3(0.08f, 0.4f, 0.08f), M("BoneWhite"));
        Prim(PrimitiveType.Cube, "Leg", sp2, new Vector3(0.2f, 0.2f, 0), new Vector3(0.08f, 0.4f, 0.08f), M("BoneWhite"));
        sand.target = sp2; sand.bob = 0.12f; sand.wobble = 6f;
        Text("SandwichSign", d, new Vector3(2663f, 2.4f, 37.5f), Face(Vector3.back), new Vector2(6f, 0.5f), 0.3f, "<b>DIMENSION 44-B</b> <size=60%>(everyone is a sandwich)</size>").color = Color.white;
        // the pink garage
        var pg = Group(d, "The pink garage", Vector3.zero);
        Box(pg, "PinkBack", new Vector3(2682f, 2f, 66f), new Vector3(10f, 4f, 0.2f), M("BonePink"));
        Box(pg, "PinkSideA", new Vector3(2677f, 2f, 63f), new Vector3(0.2f, 4f, 6f), M("BonePink"));
        Box(pg, "PinkSideB", new Vector3(2687f, 2f, 63f), new Vector3(0.2f, 4f, 6f), M("BonePink"));
        Box(pg, "PinkRoof", new Vector3(2682f, 4.1f, 63f), new Vector3(10.4f, 0.2f, 6.4f), M("GemPink"));
        Box(pg, "PinkBench", new Vector3(2682f, 0.45f, 65f), new Vector3(6f, 0.9f, 1.2f), M("GemPink"));
        Folk(pg, "Pink Genius", "#f8bbd0", 13, new Vector3(2680f, 0, 63f), 180f, "Portal/rm_pink");
        Folk(pg, "Pink Nate", "#fce4ec", 7, new Vector3(2683f, 0, 63f), 180f, "Portal/rm_pink", 0.9f);
        // the galactic market
        var mk = Stall(d, "GALACTIC MARKET", new Vector3(2650f, 0, 58f), 180f, "AlienGlow", "GemPurple");
        Folk(mk, "Zorblax", "#76ff03", 3, new Vector3(0, 0, -0.3f), 0f, "Portal/rm_alien");
        Gear("Zorblax", 2, PrimitiveType.Sphere, new Vector3(0, 0.18f, 0.14f), Vector3.one * 0.08f, "Eye");
        foreach (float ax in new[] { -0.08f, 0.08f }) Gear("Zorblax", 2, PrimitiveType.Cylinder, new Vector3(ax, 0.25f, 0), new Vector3(0.02f, 0.12f, 0.02f), "AlienGlow");
        // the saucer: ride it round in a circle
        var ride = Group(d, "Saucer ride", new Vector3(2655f, 0, 30f));
        var rot = Group(ride, "Rotor", new Vector3(0, 4f, 0));
        var sauc = Group(rot, "Saucer", new Vector3(8f, 0, 0));
        Prim(PrimitiveType.Sphere, "Hull", sauc, Vector3.zero, new Vector3(3.4f, 0.6f, 3.4f), M("Metal"));
        Prim(PrimitiveType.Sphere, "Dome", sauc, new Vector3(0, 0.4f, 0), new Vector3(1.6f, 1f, 1.6f), M("Glass"));
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Sphere, "Light", sauc, new Vector3(Mathf.Cos(k * 1.05f) * 1.5f, -0.05f, Mathf.Sin(k * 1.05f) * 1.5f), Vector3.one * 0.18f, M("GlowGreen"));
        var saucExit = Marker(ride, "Saucer exit", new Vector3(0, 0.05f, -3f), 180f);
        RideSeat(sauc, "Saucer seat", new Vector3(0, 0.2f, 0), 90f, saucExit);
        var sr = Add<FirRide>(ride.gameObject); sr.kind = 0; sr.rotor = rot; sr.axis = Vector3.up; sr.degPerSec = 15f; sr.upright = new[] { sauc };
        var launch = Gg(d, "Saucer launch button", new Vector3(2655f, 0, 27f), 180f, "Portal/rm_saucer", 1f, 1.6f);
        Box(launch.transform, "Post", new Vector3(0, 0.5f, 0), new Vector3(0.3f, 1f, 0.3f), M("Metal"), false);
        var lb = Group(launch.transform, "Button", new Vector3(0, 1.05f, 0));
        Prim(PrimitiveType.Cylinder, "Button", lb, Vector3.zero, new Vector3(0.3f, 0.05f, 0.3f), M("GlowGreen"));
        launch.target = lb; launch.moveBy = new Vector3(0, -0.04f, 0); launch.wobble = 5f;
        report.Add("The Interdimensional Garage: a cranky genius, an anxious assistant, a thingamaflux, a TV of other dimensions (4 channels), and a portal to the dimensions: sideways, sandwiches, the pink garage, a galactic market and a saucer to ride; 4 gags");
    }

    // =================================================================== the second batch of Station stops: helpers

    /// <summary>A pressable gag that says its recorded line (captioned from Data/captions.txt). The caller sets target and motion.</summary>
    static FirGag Gg(Transform g, string name, Vector3 pos, float yaw, string clip, float w = 1.2f, float h = 2f)
    {
        var gag = MovingGag(g, name, pos, new Vector3(0, h / 2f, 0), new Vector3(w, h, w), clip, CaptionFor(clip), new Vector3(0, h + 0.6f, 0));
        gag.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return gag;
    }

    /// <summary>Collectables (per player, with a counter) and optional launch pads, run by a FirBoost.</summary>
    static FirBoost Collectables(Transform g, string label, Vector3[] at, Func<Transform, int, bool> shape, Vector3 boardPos, Vector3 boardNormal,
                                 Vector3[] padPos = null, Vector3[] padVel = null, string padMat = "GlowYellow")
    {
        var fb = Add<FirBoost>(Group(g, label + " collector", boardPos).gameObject);
        fb.counterLabel = label;
        var items = new List<Transform>();
        var ig = Group(g, label + " items", Vector3.zero);
        for (int i = 0; i < at.Length; i++) { var it = Group(ig, label + " " + i, at[i]); shape(it, i); items.Add(it); }
        fb.rings = items.ToArray();
        var pads = new List<Transform>();
        if (padPos != null)
            for (int i = 0; i < padPos.Length; i++)
            {
                var pd = Group(g, "Launch pad", padPos[i]);
                Prim(PrimitiveType.Cylinder, "Pad", pd, new Vector3(0, 0.03f, 0), new Vector3(1.6f, 0.03f, 1.6f), M(padMat));
                pads.Add(pd);
            }
        fb.pads = pads.ToArray();
        fb.padVelocity = padVel ?? new Vector3[0];
        Box(g, label + " board", boardPos, new Vector3(2.4f, 0.8f, 0.04f), M("Board"), false).transform.localRotation = Face(boardNormal);
        fb.counter = Text(label + " count", g, boardPos + boardNormal * 0.04f, Face(boardNormal), new Vector2(2.2f, 0.6f), 0.3f, "");
        fb.counter.color = new Color(1f, 0.85f, 0.2f);
        fb.sfx = Src(fb.transform, Vector3.zero, null, false, 0f, 1f, 5f, false);
        fb.boostClip = Clip("whoosh"); fb.springClip = Clip("whoosh"); fb.ringClip = Clip("pickup");
        return fb;
    }

    static void Ornament(Transform r, string mat) { Prim(PrimitiveType.Sphere, "Gem", r, Vector3.zero, Vector3.one * 0.35f, M(mat)); }

    static void Gear(string who, int bone, PrimitiveType t, Vector3 at, Vector3 size, string mat, Vector3 euler = default(Vector3))
    {
        if (!rigs.TryGetValue(who, out var r)) { report.Add("problem: no skeleton " + who + " to dress"); return; }
        Prim(t, "Gear", r[bone], at, size, M(mat)).transform.localRotation = Quaternion.Euler(euler);
    }

    /// <summary>A stick-figure rig (no script) inside a gag's target.</summary>
    static Transform[] Mini(Transform parent, string name, Vector3 at, float yaw, float scale, string hex)
    {
        var r = Group(parent, name, at);
        r.localRotation = Quaternion.Euler(0, yaw, 0);
        r.localScale = Vector3.one * scale;
        return SkeletonRig(r, "Rig", BoneMat(name, hex));
    }

    // =================================================================== 1 Crystal Cove (a beach town guarded by crystal heroes)

    static void BuildCrystalCove(Transform root)
    {
        var g = AreaShell(root, "Crystal Cove", 1030f, 1080f, 20f, 70f, 20f, "PixelSand", "PixelSky", "Sky",
                          "CRYSTAL COVE", new Color(1f, 0.45f, 0.75f), "a beach town, a temple in the cliff, and crystal heroes who are mostly bones", "CRYSTAL COVE");
        Box(g, "Ocean", new Vector3(1077f, 0.04f, 45f), new Vector3(6f, 0.06f, 50f), M("Water"), false);
        // the cliff, its temple and the great statue carved into it
        Box(g, "Cliff", new Vector3(1039f, 5f, 55f), new Vector3(14f, 10f, 26f), M("Stone"));
        Box(g, "TempleDoor", new Vector3(1046.05f, 1.6f, 50f), new Vector3(0.1f, 3.2f, 2.4f), M("Black"), false);
        DPrim(PrimitiveType.Sphere, "DoorStar", g, new Vector3(1046.1f, 2.4f, 50f), new Vector3(0.1f, 1f, 1f), M("GemPink"));
        var st = Group(g, "Statue", new Vector3(1046.3f, 3f, 60f));
        Prim(PrimitiveType.Cube, "Body", st, new Vector3(0, 3f, 0), new Vector3(0.6f, 6f, 3f), M("Concrete"));
        Prim(PrimitiveType.Sphere, "Head", st, new Vector3(0, 7f, 0), new Vector3(0.8f, 2.4f, 2.2f), M("Concrete"));
        for (int k = 0; k < 4; k++)
            Prim(PrimitiveType.Cube, "Arm", st, new Vector3(0.2f, 2.5f + (k / 2) * 2.2f, k % 2 == 0 ? -2f : 2f), new Vector3(0.5f, 0.7f, 2.4f), M("Concrete")).transform.localRotation = Quaternion.Euler(k % 2 == 0 ? 25 : -25, 0, 0);
        Prim(PrimitiveType.Sphere, "ChestGem", st, new Vector3(0.35f, 4.5f, 0), new Vector3(0.3f, 0.9f, 0.9f), M("GemPink"));
        // two warp pads: one on the beach, one on top of the cliff
        Func<Vector3, Transform> Pad = p =>
        {
            var t = Group(g, "Warp pad", p);
            Prim(PrimitiveType.Cylinder, "Pad", t, new Vector3(0, 0.05f, 0), new Vector3(2f, 0.05f, 2f), M("CaneWhite"));
            Prim(PrimitiveType.Cylinder, "Glow", t, new Vector3(0, 0.11f, 0), new Vector3(1.6f, 0.01f, 1.6f), M("DarkGlowCyan"));
            return t;
        };
        var wa = Pad(new Vector3(1052f, 0f, 30f)); var wb = Pad(new Vector3(1040f, 10f, 46f));
        var wp = Add<FirPortal>(wa.gameObject); wp.a = wa; wp.b = wb; wp.radius = 0.8f;
        wp.sfx = Src(wa, new Vector3(0, 1f, 0), Clip("Gem/gm_warp"), false, 0f, 1f, 5f, false);
        Text("PadSign", g, new Vector3(1052f, 2.2f, 31.2f), Face(Vector3.back), new Vector2(2.4f, 0.3f), 0.16f, "<b>WARP PAD</b> <size=70%>(to the top of the cliff)</size>").color = Color.white;
        Box(g, "Railing", new Vector3(1039f, 10.5f, 42.2f), new Vector3(14f, 1f, 0.1f), M("Trim"));
        // the beach house on stilts, the donut shop
        Box(g, "HouseDeck", new Vector3(1064f, 2.5f, 60f), new Vector3(7f, 0.3f, 6f), M("DeskWood"), false);
        Box(g, "House", new Vector3(1064f, 4.2f, 61f), new Vector3(5f, 3.2f, 4f), M("CaneWhite"), false);
        Box(g, "HouseRoof", new Vector3(1064f, 6f, 61f), new Vector3(5.6f, 0.4f, 4.6f), M("GiftRed"), false);
        foreach (float sx in new[] { -3f, 3f }) foreach (float sz in new[] { -2.5f, 2.5f }) DPrim(PrimitiveType.Cylinder, "Stilt", g, new Vector3(1064f + sx, 1.2f, 60f + sz), new Vector3(0.3f, 1.25f, 0.3f), M("DeskWood"));
        var shop = Stall(g, "THE BIG RING", new Vector3(1062f, 0, 36f), 180f, "BonePink", "CaneWhite");
        for (int k = 0; k < 16; k++)
        {
            float a = k * Mathf.PI / 8f;
            DPrim(PrimitiveType.Sphere, "Donut", g, new Vector3(1062f + Mathf.Cos(a) * 0.8f, 4.3f + Mathf.Sin(a) * 0.8f, 36.4f), Vector3.one * 0.55f, M(k % 2 == 0 ? "BonePink" : "GiftGold"));
        }
        Folk(shop, "Sprinkles", "#ffcc80", 3, new Vector3(0, 0, -0.3f), 0f, "Gem/gm_donut");
        Folk(g, "Pearlbone", "#f3f0ff", 13, new Vector3(1040f, 10f, 50f), 180f, "Gem/gm_pearl");
        Gear("Pearlbone", 1, PrimitiveType.Sphere, new Vector3(0, 0.25f, 0.1f), Vector3.one * 0.14f, "GemWhite");
        Folk(g, "Amethyst", "#9c6ade", 19, new Vector3(1048f, 0, 44f), 180f, "Gem/gm_amethyst", 1.25f);
        Gear("Amethyst", 1, PrimitiveType.Sphere, new Vector3(0, 0.25f, 0.1f), Vector3.one * 0.14f, "GemPurple");
        Folk(g, "Pebble", "#ff8fb1", 17, new Vector3(1058f, 0, 50f), 180f, "Gem/gm_kid", 0.8f);
        Gear("Pebble", 1, PrimitiveType.Sphere, new Vector3(0, 0.05f, 0.1f), Vector3.one * 0.12f, "GemPink");
        Gear("Pebble", 6, PrimitiveType.Cylinder, new Vector3(0, -0.2f, 0.12f), new Vector3(0.5f, 0.02f, 0.5f), "GemPink", new Vector3(90, 0, 0));
        // the fusion dance: two skeletons become one (much too tall) skeleton
        var fu = Gg(g, "The Fusion Dance", new Vector3(1054f, 0, 56f), 180f, "Gem/gm_fuse", 2f, 2.2f);
        var pair = Group(fu.transform, "Pair", Vector3.zero);
        Mini(pair, "FuseA", new Vector3(-0.35f, 0, 0), 20f, 1f, "#9c6ade");
        Mini(pair, "FuseB", new Vector3(0.35f, 0, 0), -20f, 1f, "#f3f0ff");
        fu.target = pair; fu.scaleBy = 1.9f; fu.wobble = 8f; fu.inTime = 1f;
        report.Add("Crystal Cove: a cliff temple with a four-armed statue, two warp pads, a beach house, the donut shop, 4 crystal skeletons and a fusion dance (1 gag)");
    }

    // =================================================================== 2 Critter Meadow (catch-the-critters adventure)

    static Transform Critter(Transform parent, string kind, Vector3 at)
    {
        var c = Group(parent, kind, at);
        if (kind == "Mossbun")
        {
            Prim(PrimitiveType.Sphere, "Body", c, new Vector3(0, 0.35f, 0), new Vector3(0.6f, 0.55f, 0.6f), M("PixelGreen"));
            foreach (float ex in new[] { -0.12f, 0.12f }) { Prim(PrimitiveType.Cube, "Leaf ear", c, new Vector3(ex, 0.75f, 0), new Vector3(0.1f, 0.4f, 0.04f), M("GiftGreen")); Prim(PrimitiveType.Sphere, "Eye", c, new Vector3(ex, 0.45f, 0.27f), Vector3.one * 0.08f, M("Black")); }
        }
        else if (kind == "Puffbat")
        {
            Prim(PrimitiveType.Sphere, "Body", c, new Vector3(0, 0.3f, 0), Vector3.one * 0.5f, M("GemPurple"));
            foreach (float wx in new[] { -0.4f, 0.4f }) Prim(PrimitiveType.Cube, "Wing", c, new Vector3(wx, 0.35f, 0), new Vector3(0.4f, 0.04f, 0.25f), M("Black")).transform.localRotation = Quaternion.Euler(0, 0, wx > 0 ? 20 : -20);
        }
        else if (kind == "Zapcorn")
        {
            Prim(PrimitiveType.Capsule, "Body", c, new Vector3(0, 0.4f, 0), new Vector3(0.35f, 0.4f, 0.35f), M("GlowYellow"));
            Prim(PrimitiveType.Cube, "Bolt tail", c, new Vector3(0, 0.4f, -0.3f), new Vector3(0.08f, 0.4f, 0.06f), M("GlowGold")).transform.localRotation = Quaternion.Euler(30, 0, 20);
        }
        else
        {
            Prim(PrimitiveType.Sphere, "Rock", c, new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.7f), M("Stone"));
            foreach (float ex in new[] { -0.12f, 0.12f }) Prim(PrimitiveType.Sphere, "Eye", c, new Vector3(ex, 0.4f, 0.33f), Vector3.one * 0.07f, M("Black"));
        }
        return c;
    }

    static void BuildCritterMeadow(Transform root)
    {
        var g = AreaShell(root, "Critter Meadow", 1100f, 1150f, 20f, 70f, 20f, "PixelGreen", "PixelSky", "Sky",
                          "CRITTER MEADOW", new Color(1f, 0.3f, 0.2f), "catch them? befriend them? mostly they just bite your shoelaces", "CRITTER MEADOW");
        // tall grass (walk in it, something jumps out)
        foreach (var gp in new[] { new Vector3(1112f, 0, 34f), new Vector3(1138f, 0, 30f), new Vector3(1120f, 0, 58f) })
            for (int k = 0; k < 40; k++)
                DPrim(PrimitiveType.Cube, "Tall grass", g, gp + new Vector3(((k * 37) % 30) / 10f - 1.5f, 0.45f, ((k * 53) % 30) / 10f - 1.5f), new Vector3(0.08f, 0.9f, 0.08f), M(k % 3 == 0 ? "PixelGreenDark" : "GiftGreen"), Quaternion.Euler(0, k * 17f, (k % 5) * 3f));
        var wild = Gg(g, "Wild critter", new Vector3(1112f, 0, 34f), 180f, "Critter/cr_grass", 3f, 1.2f);
        wild.GetComponent<BoxCollider>().isTrigger = true;
        wild.walkIn = true; wild.zoneCenter = new Vector3(0, 0.9f, 0); wild.zoneSize = new Vector3(3f, 1.8f, 3f);
        var mb = Critter(wild.transform, "Mossbun", new Vector3(0, -0.8f, 0));
        wild.target = mb; wild.moveBy = new Vector3(0, 0.9f, 0); wild.hideWhenIdle = true; wild.bob = 0.15f;
        Text("GrassSign", g, new Vector3(1112f, 1.6f, 31.2f), Face(Vector3.back), new Vector2(2.4f, 0.3f), 0.15f, "<b>TALL GRASS</b> <size=70%>(walk in. if you dare)</size>").color = Color.white;
        // the lab: three cubes to choose from
        Box(g, "Lab", new Vector3(1106f, 2f, 64f), new Vector3(8f, 4f, 6f), M("LabWhite"));
        Text("LabSign", g, new Vector3(1106f, 3.4f, 60.95f), Face(Vector3.back), new Vector2(6f, 0.5f), 0.3f, "<b>CRITTER LAB</b>").color = new Color(0.2f, 0.3f, 0.6f);
        Folk(g, "Professor Oakbone", "#d7ccc8", 20, new Vector3(1106f, 0, 59.5f), 180f, "Critter/cr_prof");
        string[] starters = { "Mossbun", "Puffbat", "Zapcorn" };
        for (int k = 0; k < 3; k++)
        {
            Box(g, "Pedestal", new Vector3(1103f + k * 3f, 0.5f, 56f), new Vector3(0.8f, 1f, 0.8f), M("LabGrey"));
            Critter(g, starters[k], new Vector3(1103f + k * 3f, 1f, 56f));
        }
        // the critter center and the gym
        var cc = Stall(g, "CRITTER CENTER", new Vector3(1140f, 0, 62f), 180f, "CaneRed", "CaneWhite");
        Folk(cc, "Nurse Patella", "#f8bbd0", 4, new Vector3(0, 0, -0.3f), 0f, "Critter/cr_nurse");
        Box(g, "Gym", new Vector3(1144f, 2.5f, 40f), new Vector3(6f, 5f, 8f), M("GiftBlue"));
        Text("GymSign", g, new Vector3(1140.95f, 4.2f, 40f), Face(Vector3.left), new Vector2(5f, 0.5f), 0.3f, "<b>PEBBLE GYM</b>").color = Color.white;
        Folk(g, "Leader Granite", "#8d8d8d", 10, new Vector3(1139.5f, 0, 40f), -90f, "Critter/cr_gym");
        DPrim(PrimitiveType.Cylinder, "Badge", g, new Vector3(1140.9f, 2.8f, 37.5f), new Vector3(0.7f, 0.03f, 0.7f), M("GlowGold"), Quaternion.Euler(0, 0, 90));
        // the battle arena: a trainer and a rock
        Box(g, "Arena", new Vector3(1126f, 0.01f, 44f), new Vector3(8f, 0.02f, 5f), M("PixelSand"), false);
        DPrim(PrimitiveType.Cylinder, "ArenaMark", g, new Vector3(1126f, 0.03f, 44f), new Vector3(1.5f, 0.01f, 1.5f), M("CaneWhite"));
        Folk(g, "Trainer Dusty", "#ff7043", 16, new Vector3(1129.5f, 0, 44f), -90f, "Critter/cr_trainer");
        Critter(g, "Rock", new Vector3(1127.5f, 0, 44f));
        var cube = Gg(g, "Catch cube", new Vector3(1123f, 0, 44f), 90f, "Critter/cr_catch", 1.2f, 1.4f);
        var cb = Group(cube.transform, "Cube", new Vector3(0, 0.3f, 0));
        Prim(PrimitiveType.Cube, "Top", cb, new Vector3(0, 0.1f, 0), new Vector3(0.4f, 0.2f, 0.4f), M("CaneRed"));
        Prim(PrimitiveType.Cube, "Bottom", cb, new Vector3(0, -0.1f, 0), new Vector3(0.4f, 0.2f, 0.4f), M("CaneWhite"));
        Critter(cb, "Puffbat", new Vector3(0, 0.2f, 0)).localScale = Vector3.one * 0.5f;
        cube.target = cb; cube.moveBy = new Vector3(0, 0.6f, 1.2f); cube.wobble = 25f; cube.inTime = 0.6f;
        // the rival, running laps
        var wg = Group(g, "Wanderers", Vector3.zero);
        MakeWanderer(wg, "Rival Ash", "#42a5f5", new[] { new Vector3(1108f, 0, 26f), new Vector3(1108f, 0, 48f), new Vector3(1134f, 0, 50f), new Vector3(1134f, 0, 26f) },
                     new[] { 2.4f, 2.4f, 2.4f, 2.4f }, 1.1f, 2000, "Critter/cr_rival", CaptionFor("cr_rival"));
        report.Add("Critter Meadow: tall grass with a wild critter, a lab with 3 starter critters, a critter center, a gym, a battle arena, a rival and 2 gags");
    }

    // =================================================================== 3 The Shadow Below (a classroom closet that opens on a dark world)

    static void BuildShadowBelow(Transform root)
    {
        var g = AreaShell(root, "The Shadow Below", 1170f, 1190f, 20f, 36f, 4f, "Floor2", "Wall", "Ceiling",
                          "ROOM 2B", new Color(0.25f, 0.25f, 0.35f), "an ordinary classroom. nothing to see. definitely don't open the supply closet.", "THE SHADOW BELOW");
        // the classroom
        Box(g, "Chalkboard", new Vector3(1180f, 1.8f, 35.9f), new Vector3(6f, 1.6f, 0.05f), M("WallDark"), false);
        Text("Chalk", g, new Vector3(1180f, 1.8f, 35.85f), Face(Vector3.back), new Vector2(5.6f, 1.4f), 0.2f, "TODAY: the history of the shadow world\n(a myth)\n(probably)").color = new Color(0.9f, 0.9f, 0.9f);
        for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++)
            Box(g, "Desk", new Vector3(1176f + c * 4f, 0.38f, 25f + r * 3f), new Vector3(1.2f, 0.76f, 0.7f), M("DeskWood"));
        Folk(g, "Ms Clavella", "#ffb74d", 0, new Vector3(1183f, 0, 33.5f), 180f, "Shade/sh_teacher");
        Box(g, "Closet", new Vector3(1189.9f, 1.1f, 31f), new Vector3(0.1f, 2.2f, 1.2f), M("DoorWood"), false);
        // the dark world: its own big space east of the classroom
        var d = Group(root, "The Dark World", Vector3.zero);
        float x0 = 1200f, x1 = 1270f, z0 = 20f, z1 = 100f, h = 25f;
        Box(d, "Floor", new Vector3((x0 + x1) / 2f, -0.1f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), M("DarkGround"));
        Box(d, "Sky", new Vector3((x0 + x1) / 2f, h + 0.1f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.2f, z1 - z0), M("NightSky"));
        Box(d, "WallW", new Vector3(x0 - 0.1f, h / 2, (z0 + z1) / 2f), new Vector3(0.2f, h, z1 - z0), M("NightSky"));
        Box(d, "WallE", new Vector3(x1 + 0.1f, h / 2, (z0 + z1) / 2f), new Vector3(0.2f, h, z1 - z0), M("NightSky"));
        Box(d, "WallS", new Vector3((x0 + x1) / 2f, h / 2, z0 - 0.1f), new Vector3(x1 - x0, h, 0.2f), M("NightSky"));
        Box(d, "WallN", new Vector3((x0 + x1) / 2f, h / 2, z1 + 0.1f), new Vector3(x1 - x0, h, 0.2f), M("NightSky"));
        var darkArrive = Marker(d, "Dark arrive", new Vector3(1235f, 0.05f, 23f), 0f);
        var closetTp = TeleportButton(g, "Open the supply closet", new Vector3(1189.8f, 1.2f, 29.8f), Vector3.left, darkArrive, new Vector3(0.6f, 0.3f, 0.08f));
        var wake = TeleportButton(d, "Wake up (back to class)", new Vector3(1235f, 1.3f, 20.15f), Vector3.forward, g.Find("Arrive"), new Vector3(1.2f, 0.3f, 0.1f));
        // the cliffs: tall pink-purple pillars along a glowing path
        for (int k = 0; k < 18; k++)
        {
            float side = k % 2 == 0 ? -1f : 1f;
            float px = 1235f + side * (5f + (k * 7 % 5)), pz = 26f + k * 1.4f;
            Box(d, "Cliff", new Vector3(px, (3f + k % 4 * 1.5f) / 2f, pz), new Vector3(2f + k % 3, 3f + k % 4 * 1.5f, 2f), M("DarkCliff"));
        }
        for (int k = 0; k < 14; k++) DPrim(PrimitiveType.Cube, "Path", d, new Vector3(1235f + Mathf.Sin(k * 0.5f) * 1.5f, 0.01f, 24f + k * 2f), new Vector3(1.6f, 0.02f, 1.6f), M("DarkGlowPink"));
        // the forest of glowing trees (west) and the candy field (east)
        for (int k = 0; k < 14; k++)
        {
            var tp = new Vector3(1205f + (k % 4) * 3.5f, 0, 40f + (k / 4) * 6f + (k % 2) * 2f);
            DPrim(PrimitiveType.Cylinder, "DarkTrunk", d, tp + new Vector3(0, 2f, 0), new Vector3(0.3f, 2f, 0.3f), M("Black"));
            DPrim(PrimitiveType.Sphere, "GlowLeaves", d, tp + new Vector3(0, 4.6f, 0), new Vector3(2.4f, 2f, 2.4f), M(k % 2 == 0 ? "DarkGlowCyan" : "DarkGlowPink"));
        }
        for (int k = 0; k < 10; k++) DPrim(PrimitiveType.Sphere, "Candy", d, new Vector3(1258f + (k % 4) * 2.5f, 0.6f, 40f + (k / 4) * 5f), new Vector3(0.9f, 1.2f, 0.9f), M(k % 3 == 0 ? "GemPink" : k % 3 == 1 ? "DarkGlowCyan" : "GlowGold"));
        Text("ForestSign", d, new Vector3(1216f, 2f, 38f), Face(Vector3.back), new Vector2(4f, 0.4f), 0.22f, "<b>THE GLIMMERWOOD</b>").color = new Color(0.6f, 1f, 1f);
        Text("CandySign", d, new Vector3(1262f, 2f, 38f), Face(Vector3.back), new Vector2(4f, 0.4f), 0.22f, "<b>THE CANDY FIELD</b> <size=60%>(don't eat it)</size>").color = new Color(1f, 0.6f, 0.9f);
        // floating card shapes, bobbing
        var cards = Group(d, "Floating cards", new Vector3(1235f, 8f, 50f));
        var bob = new List<Transform>();
        for (int k = 0; k < 8; k++)
        {
            var cp = Group(cards, "Card", new Vector3(Mathf.Cos(k * 0.8f) * 12f, (k % 3) * 1.5f, Mathf.Sin(k * 0.8f) * 12f));
            Prim(PrimitiveType.Cube, "Card", cp, Vector3.zero, new Vector3(1.4f, 2f, 0.05f), M(k % 2 == 0 ? "CaneWhite" : "GemPurple")).transform.localRotation = Quaternion.Euler(0, k * 40f, 10f);
            bob.Add(cp);
        }
        var cr = Add<FirRide>(cards.gameObject); cr.kind = 0; cr.rotor = cards; cr.axis = Vector3.up; cr.degPerSec = 6f; cr.bobbers = bob.ToArray(); cr.bobHeight = 0.8f; cr.bobSpeed = 0.9f;
        // the tile gate across the way to the castle
        var tg = Group(d, "Tile Gate", Vector3.zero);
        Box(tg, "FenceW", new Vector3(1216.5f, 1.25f, 58f), new Vector3(33f, 2.5f, 0.4f), M("DarkCliff"));
        Box(tg, "FenceE", new Vector3(1253.5f, 1.25f, 58f), new Vector3(33f, 2.5f, 0.4f), M("DarkCliff"));
        var gate = Box(tg, "Gate", new Vector3(1235f, 1.25f, 58f), new Vector3(4f, 2.5f, 0.3f), M("GemPurple"));
        var panel = Group(tg, "Tiles", new Vector3(1239.6f, 1.5f, 57.75f));
        panel.localRotation = Face(Vector3.back);
        Box(panel, "Backing", new Vector3(0, 0, 0.05f), new Vector3(1.6f, 1.6f, 0.04f), M("Black"), false);
        int b0 = huntButtons.Count;
        var tiles = new MeshRenderer[9];
        for (int i = 0; i < 9; i++)
        {
            var b = HB(panel, "Tile " + i, new Vector3(-0.48f + (i % 3) * 0.48f, 0.48f - (i / 3) * 0.48f, 0f), Vector3.back, new Vector3(0.42f, 0.42f, 0.05f), M("TileOff"), 31, i, "Flip this tile", null, 0.1f, true);
            tiles[i] = b.press.Find("Visual").GetComponent<MeshRenderer>();
        }
        HB(panel, "Start over", new Vector3(0, -0.95f, 0f), Vector3.back, new Vector3(0.8f, 0.2f, 0.05f), M("Button"), 32, 0, "Start the tiles over", "<b>START OVER</b>", 0.08f, true);
        var tb = Text("TileBoard", tg, new Vector3(1235f, 3.4f, 57.75f), Face(Vector3.back), new Vector2(5f, 0.8f), 0.16f, "");
        tb.color = Color.white;
        var ft = Add<FirTiles>(tg.gameObject);
        ft.tiles = tiles; ft.offMat = M("TileOff"); ft.onMat = M("TileOn"); ft.gate = gate; ft.board = tb;
        ft.startPressed = new[] { 1, 3, 5, 7 };
        ft.sfx = Src(tg, new Vector3(1235f, 1.5f, 58f), null, false, 1f, 1f, 12f, false);
        ft.flipClip = Clip("switch"); ft.solvedClip = Clip("unlock");
        foreach (var hb in huntButtons.Skip(b0)) hb.hunt = UB(ft);
        Folk(d, "Jinx the Jester", "#b388ff", 5, new Vector3(1230f, 0, 55.5f), 180f, "Shade/sh_jester");
        Gear("Jinx the Jester", 2, PrimitiveType.Cube, new Vector3(-0.12f, 0.18f, 0), new Vector3(0.08f, 0.3f, 0.08f), "GemPink", new Vector3(0, 0, 30));
        Gear("Jinx the Jester", 2, PrimitiveType.Cube, new Vector3(0.12f, 0.18f, 0), new Vector3(0.08f, 0.3f, 0.08f), "DarkGlowCyan", new Vector3(0, 0, -30));
        Folk(d, "Kris Calcite", "#4fc3f7", 20, new Vector3(1237.5f, 0, 30f), 180f, "Shade/sh_hero");
        Gear("Kris Calcite", 6, PrimitiveType.Cube, new Vector3(0, -0.4f, 0.05f), new Vector3(0.05f, 0.7f, 0.08f), "Metal");
        Folk(d, "Gnash", "#7e57c2", 10, new Vector3(1232.5f, 0, 30f), 180f, "Shade/sh_rude", 1.15f);
        // castle town: walls, towers, the king, the shop
        foreach (float tx in new[] { 1222f, 1248f }) { Box(d, "Tower", new Vector3(tx, 5f, 88f), new Vector3(4f, 10f, 4f), M("Black")); DPrim(PrimitiveType.Sphere, "TowerTop", d, new Vector3(tx, 10.5f, 88f), new Vector3(3f, 1.5f, 3f), M("GemPurple")); }
        Box(d, "CastleWall", new Vector3(1235f, 3f, 92f), new Vector3(30f, 6f, 1f), M("Black"));
        Box(d, "ThroneStep", new Vector3(1235f, 0.3f, 88.5f), new Vector3(4f, 0.6f, 3f), M("GemPurple"));
        Box(d, "Throne", new Vector3(1235f, 1.6f, 89.6f), new Vector3(1.6f, 2.6f, 0.4f), M("GlowGold"));
        Folk(d, "The Chess King", "#212121", 15, new Vector3(1235f, 0.6f, 88.5f), 180f, "Shade/sh_king", 1.2f);
        Gear("The Chess King", 2, PrimitiveType.Cylinder, new Vector3(0, 0.22f, 0.02f), new Vector3(0.28f, 0.12f, 0.28f), "GlowGold");
        var shop = Stall(d, "DARK CANDY", new Vector3(1252f, 0, 76f), 180f, "GemPurple", "Black");
        Folk(shop, "Seam", "#ce93d8", 3, new Vector3(0, 0, -0.3f), 0f, "Shade/sh_shop");
        // the shade spring: a roaring pillar of black and white light
        var sp = Group(d, "The Shade Spring", new Vector3(1235f, 0, 74f));
        Prim(PrimitiveType.Cylinder, "Core", sp, new Vector3(0, 12f, 0), new Vector3(1.6f, 12f, 1.6f), M("Black"));
        Prim(PrimitiveType.Cylinder, "Halo", sp, new Vector3(0, 12f, 0), new Vector3(2.4f, 12f, 2.4f), M("Mist"));
        Prim(PrimitiveType.Cylinder, "Basin", sp, new Vector3(0, 0.2f, 0), new Vector3(4f, 0.2f, 4f), M("DarkCliff"));
        sp.gameObject.AddComponent<BoxCollider>().size = new Vector3(4f, 3f, 4f);
        var cap = Text("Caption", sp, new Vector3(0, 3.4f, -2.2f), Face(Vector3.back), new Vector2(4f, 0.6f), 0.14f, "");
        cap.color = new Color(0.9f, 0.9f, 1f);
        var fs = Add<FirSay>(sp.gameObject);
        fs.line = "* " + CaptionFor("sh_spring"); fs.caption = cap;
        fs.voice = Src(sp, new Vector3(0, 1.5f, 0), Clip("Shade/sh_spring"), false, 1f, 1f, 20f, false);
        SetInteract(fs, "Look at the shade spring", 4f);
        report.Add("The Shadow Below: a classroom whose supply closet opens on a dark world (cliffs, the Glimmerwood, the candy field, a tile gate, a castle town and the shade spring); 7 voiced skeletons");
    }
    // =================================================================== 4 Ink Plaza (ink the turf)

    static void BuildInkPlaza(Transform root)
    {
        var g = AreaShell(root, "Ink Plaza", 1290f, 1330f, 20f, 60f, 12f, "Concrete", "WallDark", "Sky",
                          "INK PLAZA", new Color(1f, 0.55f, 0.05f), "stay fresh. ink the floor. the janitors have given up.", "INK PLAZA");
        // splats on the walls
        for (int k = 0; k < 18; k++)
        {
            bool west = k % 2 == 0;
            DPrim(PrimitiveType.Sphere, "Splat", g, new Vector3(west ? 1290.05f : 1329.95f, 1.5f + (k % 4) * 1.6f, 24f + k * 1.9f), new Vector3(0.05f, 1.2f + (k % 3) * 0.5f, 1.4f + (k % 2) * 0.6f), M(k % 3 == 0 ? "InkBlue" : "InkOrange"));
        }
        // the turf: 10 x 8 tiles you ink by walking on them
        var turf = Group(g, "Turf", Vector3.zero);
        var tiles = new List<MeshRenderer>();
        var origin = new Vector3(1301f, 0f, 33f);
        for (int z = 0; z < 8; z++) for (int x = 0; x < 10; x++)
            tiles.Add(Prim(PrimitiveType.Cube, "Turf " + x + "," + z, turf, origin + new Vector3(x * 2f, 0.012f, z * 2f), new Vector3(1.96f, 0.02f, 1.96f), M("TurfGrey")).GetComponent<MeshRenderer>());
        var it = Add<FirInkTurf>(turf.gameObject);
        it.tiles = tiles.ToArray(); it.columns = 10; it.origin = origin; it.tileSize = 2f;
        it.blankMat = M("TurfGrey"); it.inkMats = new[] { M("InkOrange"), M("InkBlue") };
        it.board = Text("TurfBoard", g, new Vector3(1310f, 4.2f, 59.85f), Face(Vector3.back), new Vector2(8f, 1.2f), 0.4f, "");
        it.board.color = Color.white;
        it.sfx = Src(turf, origin, null, false, 0f, 1f, 5f, false); it.splatClip = Clip("pickup");
        var pads = Group(g, "Ink pads", new Vector3(1310f, 0, 27f));
        HB(pads, "Orange ink", new Vector3(-2f, 1.1f, 0), Vector3.back, new Vector3(1f, 0.5f, 0.1f), M("InkOrange"), 71, 0, "Pick orange ink", "<b>ORANGE</b>", 0.14f, true).hunt = Backing(it);
        HB(pads, "Blue ink", new Vector3(0f, 1.1f, 0), Vector3.back, new Vector3(1f, 0.5f, 0.1f), M("InkBlue"), 71, 1, "Pick blue ink", "<b>BLUE</b>", 0.14f, true).hunt = Backing(it);
        HB(pads, "Clean the floor", new Vector3(2f, 1.1f, 0), Vector3.back, new Vector3(1f, 0.5f, 0.1f), M("Button"), 72, 0, "Clean the turf", "<b>CLEAN</b>", 0.14f, true).hunt = Backing(it);
        Box(pads, "Post", new Vector3(0, 0.5f, 0.1f), new Vector3(6f, 1f, 0.2f), M("Metal"));
        // the tower in the middle of the plaza, dripping
        Box(g, "Tower", new Vector3(1310f, 4f, 53f), new Vector3(3f, 8f, 3f), M("LabGrey"));
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cube, "Drip", g, new Vector3(1308.6f + k * 0.55f, 6.5f - (k % 3) * 0.8f, 51.48f), new Vector3(0.3f, 1.5f + (k % 3) * 0.8f, 0.04f), M(k % 2 == 0 ? "InkOrange" : "InkBlue"));
        // the folks
        Folk(g, "Squidge", "#ff9100", 18, new Vector3(1296f, 0, 30f), 90f, "Ink/ik_kid");
        foreach (float tx in new[] { -0.12f, 0f, 0.12f }) Gear("Squidge", 2, PrimitiveType.Capsule, new Vector3(tx, 0.05f, -0.15f), new Vector3(0.07f, 0.2f, 0.07f), "InkOrange", new Vector3(30, 0, 0));
        Box(g, "DJ booth", new Vector3(1324f, 1f, 54f), new Vector3(3f, 2f, 2f), M("Black"));
        Folk(g, "DJ Splatterbone", "#00e5ff", 9, new Vector3(1324f, 2f, 54f), -135f, "Ink/ik_dj");
        var shop = Stall(g, "FRESH FITS", new Vector3(1296f, 0, 50f), 90f, "InkOrange", "InkBlue");
        Folk(shop, "Ripple", "#ffd740", 3, new Vector3(0, 0, -0.3f), 0f, "Ink/ik_shop");
        Box(g, "NewsScreen", new Vector3(1326f, 3.5f, 22f), new Vector3(5f, 3f, 0.2f), M("InkBlue"), false);
        Folk(g, "Anchor Ebb", "#e1f5fe", 20, new Vector3(1326f, 0, 23.5f), 180f, "Ink/ik_news");
        // the ink cannon: it fires at the wall
        var can = Gg(g, "Ink cannon", new Vector3(1293f, 0, 40f), 90f, "Ink/ik_cannon", 1.4f, 1.6f);
        Prim(PrimitiveType.Cylinder, "Barrel", can.transform, new Vector3(0, 0.9f, 0.4f), new Vector3(0.4f, 0.6f, 0.4f), M("Metal")).transform.localRotation = Quaternion.Euler(80, 0, 0);
        Box(can.transform, "Base", new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.8f), M("Black"), false);
        var blob = Group(can.transform, "Blob", new Vector3(0, 0.95f, 0.9f));
        Prim(PrimitiveType.Sphere, "Ink", blob, Vector3.zero, Vector3.one * 0.35f, M("InkOrange"));
        can.target = blob; can.moveBy = new Vector3(0, 1.2f, 7f); can.scaleBy = 3f; can.hideWhenIdle = true; can.inTime = 0.5f;
        report.Add("Ink Plaza: a turf to ink (80 tiles, per player), ink pads, a dripping tower, 4 squid-kid skeletons and an ink cannon (1 gag)");
    }

    // =================================================================== 5 The Dread Depths (a space cave, a bounty hunter, energy to collect)

    static void BuildDreadDepths(Transform root)
    {
        var g = AreaShell(root, "The Dread Depths", 1350f, 1400f, 20f, 70f, 14f, "CaveRock", "CaveRock", "Black",
                          "THE DREAD DEPTHS", new Color(0.3f, 1f, 0.5f), "a space cave. collect energy. roll through tubes. do not tap the jar.", "THE DREAD DEPTHS");
        // stalactites and alien plants
        for (int k = 0; k < 24; k++)
            DPrim(PrimitiveType.Cylinder, "Stalactite", g, new Vector3(1353f + (k * 13 % 44), 12.5f, 23f + (k * 7 % 44)), new Vector3(0.6f, 1.5f + (k % 3), 0.6f), M("CaveRock"));
        for (int k = 0; k < 14; k++)
        {
            var pp = new Vector3(1354f + (k * 11 % 42), 0, 24f + (k * 17 % 42));
            DPrim(PrimitiveType.Cylinder, "Stalk", g, pp + new Vector3(0, 0.5f, 0), new Vector3(0.1f, 0.5f, 0.1f), M("PixelGreenDark"));
            DPrim(PrimitiveType.Sphere, "Bulb", g, pp + new Vector3(0, 1.1f, 0), Vector3.one * 0.4f, M("AlienGlow"));
        }
        // a ledge reached by the high-jump pad, energy on it
        Box(g, "Ledge", new Vector3(1392f, 2.5f, 45f), new Vector3(8f, 5f, 10f), M("CaveRock"));
        var energy = new List<Vector3>();
        for (int k = 0; k < 6; k++) energy.Add(new Vector3(1390f + (k % 2) * 3f, 6f, 42f + (k / 2) * 3f));
        for (int k = 0; k < 12; k++) energy.Add(new Vector3(1360f + (k % 4) * 6f, 1.3f, 28f + (k / 4) * 10f));
        var fb = Collectables(g, "ENERGY", energy.ToArray(), (t, i) => { Prim(PrimitiveType.Sphere, "Orb", t, Vector3.zero, Vector3.one * 0.4f, M("AlienGlow")); Prim(PrimitiveType.Sphere, "Core", t, Vector3.zero, Vector3.one * 0.2f, M("GemPink")); return true; },
                              new Vector3(1375f, 2.6f, 20.15f), Vector3.forward, new[] { new Vector3(1386f, 0.02f, 45f) }, new[] { new Vector3(1.5f, 10.5f, 0f) }, "AlienGlow");
        Text("JumpSign", g, new Vector3(1386f, 1.8f, 43.6f), Face(Vector3.back), new Vector2(2f, 0.3f), 0.14f, "<b>HIGH JUMP PAD</b>").color = new Color(0.3f, 1f, 0.5f);
        // the morph tube: in one end, out the other
        Func<Vector3, float, Transform> Tube = (p, yaw) =>
        {
            var t = Group(g, "Morph tube", p);
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            Prim(PrimitiveType.Cylinder, "Mouth", t, new Vector3(0, 0.6f, 0.4f), new Vector3(1.4f, 0.05f, 1.4f), M("Metal")).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Prim(PrimitiveType.Cylinder, "Dark", t, new Vector3(0, 0.6f, 0.42f), new Vector3(1.1f, 0.02f, 1.1f), M("Black")).transform.localRotation = Quaternion.Euler(90, 0, 0);
            return t;
        };
        var ta = Tube(new Vector3(1352f, 0, 40f), 90f); var tb2 = Tube(new Vector3(1366f, 0, 66f), 180f);
        var mp = Add<FirPortal>(ta.gameObject); mp.a = ta; mp.b = tb2; mp.radius = 0.7f;
        mp.sfx = Src(ta, new Vector3(0, 1f, 0), Clip("whoosh"), false, 0f, 0.8f, 5f, false);
        Text("TubeSign", g, new Vector3(1350.1f, 1.8f, 40f), Face(Vector3.right), new Vector2(2.4f, 0.4f), 0.14f, "<b>MORPH TUBE</b>\n<size=70%>curl up. roll through.</size>").color = Color.white;
        // the hunter in her suit
        Folk(g, "Hunter Vanta", "#ff8f00", 11, new Vector3(1372f, 0, 30f), 180f, "Dread/dr_hunter");
        Gear("Hunter Vanta", 2, PrimitiveType.Sphere, new Vector3(0, 0.08f, 0.01f), new Vector3(0.36f, 0.38f, 0.38f), "SuitOrange");
        Gear("Hunter Vanta", 2, PrimitiveType.Cube, new Vector3(0, 0.08f, 0.17f), new Vector3(0.24f, 0.1f, 0.06f), "Visor");
        Gear("Hunter Vanta", 3, PrimitiveType.Sphere, new Vector3(0, 0.05f, 0), Vector3.one * 0.3f, "SuitOrange");
        Gear("Hunter Vanta", 4, PrimitiveType.Sphere, new Vector3(0, 0.05f, 0), Vector3.one * 0.3f, "SuitOrange");
        Gear("Hunter Vanta", 6, PrimitiveType.Cylinder, new Vector3(0, -0.25f, 0), new Vector3(0.16f, 0.25f, 0.16f), "SuitOrange");
        Folk(g, "Doctor Orbit", "#e0f7fa", 20, new Vector3(1360f, 0, 60f), 180f, "Dread/dr_science");
        // the jar, the save station, the computer
        var jar = Gg(g, "Specimen jar", new Vector3(1364f, 0, 62f), 180f, "Dread/dr_jelly", 1.2f, 2.2f);
        Prim(PrimitiveType.Cylinder, "Glass", jar.transform, new Vector3(0, 1.1f, 0), new Vector3(1f, 0.9f, 1f), M("Glass"));
        Prim(PrimitiveType.Cylinder, "Lid", jar.transform, new Vector3(0, 2.05f, 0), new Vector3(1.05f, 0.06f, 1.05f), M("Metal"));
        var jelly = Group(jar.transform, "Jelly", new Vector3(0, 1.2f, 0));
        Prim(PrimitiveType.Sphere, "Dome", jelly, Vector3.zero, new Vector3(0.6f, 0.45f, 0.6f), M("AlienGlow"));
        foreach (float fx in new[] { -0.15f, 0f, 0.15f }) Prim(PrimitiveType.Sphere, "Fang", jelly, new Vector3(fx, -0.22f, 0), new Vector3(0.06f, 0.2f, 0.06f), M("GemPink"));
        jar.target = jelly; jar.scaleBy = 1.3f; jar.bob = 0.12f; jar.wobble = 10f;
        var save = Gg(g, "Save station", new Vector3(1385f, 0, 26f), 180f, "Dread/dr_save", 1.6f, 2.6f);
        Prim(PrimitiveType.Cylinder, "Base", save.transform, new Vector3(0, 0.1f, 0), new Vector3(1.6f, 0.1f, 1.6f), M("Metal"));
        Prim(PrimitiveType.Cylinder, "Top", save.transform, new Vector3(0, 2.6f, 0), new Vector3(1.6f, 0.1f, 1.6f), M("Metal"));
        var beam = Group(save.transform, "Beam", new Vector3(0, 0.2f, 0));
        Prim(PrimitiveType.Cylinder, "Light", beam, new Vector3(0, 1.2f, 0), new Vector3(1.3f, 1.2f, 1.3f), M("DarkGlowCyan"));
        save.target = beam; save.hideWhenIdle = true; save.scaleBy = 1.1f; save.spin = 90f;
        var comp = Gg(g, "Base computer", new Vector3(1356f, 0, 26f), 180f, "Dread/dr_computer", 1.4f, 2f);
        Box(comp.transform, "Console", new Vector3(0, 0.5f, 0), new Vector3(1.2f, 1f, 0.6f), M("LabGrey"), false);
        var scr = Group(comp.transform, "Screen", new Vector3(0, 1.4f, 0.1f));
        Prim(PrimitiveType.Cube, "Screen", scr, Vector3.zero, new Vector3(1f, 0.7f, 0.05f), M("GlowRed"));
        comp.target = scr; comp.wobble = 4f;
        for (int k = 0; k < 3; k++) DPrim(PrimitiveType.Cylinder, "Hatch", g, new Vector3(1399.9f, 1.4f, 30f + k * 15f), new Vector3(2.6f, 0.05f, 2.6f), M(k == 1 ? "GlowRed" : "InkBlue"), Quaternion.Euler(0, 0, 90));
        report.Add("The Dread Depths: a space cave with " + energy.Count + " energy orbs, a high-jump pad, a morph tube, a bounty hunter in her suit and 3 gags (the jar, the save station, the computer)");
    }

    // =================================================================== 6 Hero's Glade (a green-hooded hero, pots, chests, a sword that won't come out)

    static void BuildHerosGlade(Transform root)
    {
        var g = AreaShell(root, "Hero's Glade", 1420f, 1470f, 20f, 70f, 20f, "PixelGreen", "PixelSky", "Sky",
                          "HERO'S GLADE", new Color(0.3f, 0.85f, 0.3f), "smash the pots. open the chests. do NOT hit the chickens.", "HERO'S GLADE");
        for (int k = 0; k < 10; k++) TreeAt(g, "GladeTree", new Vector3(k < 5 ? 1423f : 1467f, 0, 26f + (k % 5) * 10f), k < 5 ? 90f : -90f, 2.4f, false);
        // the sword in the stone, in a clearing of flowers
        Prim(PrimitiveType.Cylinder, "Clearing", g, new Vector3(1445f, 0.01f, 60f), new Vector3(8f, 0.01f, 8f), M("PixelSand"));
        var sw = Gg(g, "The sword in the stone", new Vector3(1445f, 0, 60f), 180f, "Glade/gl_sword", 1.4f, 2f);
        Box(sw.transform, "Stone", new Vector3(0, 0.4f, 0), new Vector3(1.2f, 0.8f, 1.2f), M("Stone"));
        var blade = Group(sw.transform, "Sword", new Vector3(0, 0.8f, 0));
        Prim(PrimitiveType.Cube, "Blade", blade, new Vector3(0, 0.4f, 0), new Vector3(0.1f, 0.8f, 0.03f), M("Metal"));
        Prim(PrimitiveType.Cube, "Guard", blade, new Vector3(0, 0.82f, 0), new Vector3(0.4f, 0.06f, 0.06f), M("InkBlue"));
        Prim(PrimitiveType.Cube, "Grip", blade, new Vector3(0, 1.0f, 0), new Vector3(0.06f, 0.3f, 0.06f), M("GemPurple"));
        sw.target = blade; sw.moveBy = new Vector3(0, 0.04f, 0); sw.wobble = 3f;
        // pots, chest, chickens
        var pot = Gg(g, "A pot", new Vector3(1432f, 0, 40f), 90f, "Glade/gl_pot", 1f, 1.2f);
        var pt = Group(pot.transform, "Pot", Vector3.zero);
        Prim(PrimitiveType.Sphere, "Pot", pt, new Vector3(0, 0.4f, 0), new Vector3(0.7f, 0.8f, 0.7f), M("Pot"));
        pot.target = pt; pot.scaleBy = 0.05f; pot.inTime = 0.15f;
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Sphere, "More pots", g, new Vector3(1431.5f, 0.4f, 42f + k * 1f), new Vector3(0.7f, 0.8f, 0.7f), M("Pot"));
        var chest = Gg(g, "A chest", new Vector3(1458f, 0, 40f), -90f, "Glade/gl_chest", 1.4f, 1.4f);
        Box(chest.transform, "Box", new Vector3(0, 0.35f, 0), new Vector3(1.2f, 0.7f, 0.8f), M("DoorWood"), false);
        var lid = Group(chest.transform, "Lid", new Vector3(0, 0.7f, -0.4f));
        Prim(PrimitiveType.Cube, "Lid", lid, new Vector3(0, 0.1f, 0.4f), new Vector3(1.22f, 0.2f, 0.82f), M("GlowGold"));
        Prim(PrimitiveType.Cube, "Smaller chest", chest.transform, new Vector3(0, 0.85f, 0), new Vector3(0.35f, 0.25f, 0.25f), M("DoorWood"));
        chest.target = lid; chest.rotateBy = new Vector3(-100f, 0, 0); chest.inTime = 0.8f;
        var chick = Gg(g, "A chicken", new Vector3(1452f, 0, 30f), 180f, "Glade/gl_clucks", 1f, 1f);
        Prim(PrimitiveType.Sphere, "Chicken", chick.transform, new Vector3(0, 0.3f, 0), new Vector3(0.4f, 0.4f, 0.5f), M("CaneWhite"));
        Prim(PrimitiveType.Cube, "Comb", chick.transform, new Vector3(0, 0.55f, 0.12f), new Vector3(0.05f, 0.1f, 0.12f), M("CaneRed"));
        var flock = Group(chick.transform, "Flock", new Vector3(0, 6f, 0));
        for (int k = 0; k < 9; k++)
        {
            var c = Group(flock, "Angry chicken", new Vector3(Mathf.Cos(k * 0.7f) * 2f, (k % 3) * 0.5f, Mathf.Sin(k * 0.7f) * 2f));
            Prim(PrimitiveType.Sphere, "Body", c, Vector3.zero, new Vector3(0.4f, 0.4f, 0.5f), M("CaneWhite"));
            Prim(PrimitiveType.Sphere, "Angry eye", c, new Vector3(0, 0.1f, 0.22f), Vector3.one * 0.08f, M("GlowRed"));
        }
        chick.target = flock; chick.hideWhenIdle = true; chick.moveBy = new Vector3(0, -4.5f, 0); chick.spin = 120f; chick.inTime = 0.8f;
        // the fairy fountain
        Prim(PrimitiveType.Cylinder, "FairyPool", g, new Vector3(1460f, 0.1f, 62f), new Vector3(4f, 0.1f, 4f), M("Water"));
        Folk(g, "Twinkle", "#ff80ab", 16, new Vector3(1460f, 1.2f, 62f), 180f, "Glade/gl_fairy", 0.35f);
        Gear("Twinkle", 1, PrimitiveType.Cube, new Vector3(0, 0.2f, -0.12f), new Vector3(0.8f, 0.4f, 0.02f), "DarkGlowCyan");
        // the old man's cave
        Box(g, "CaveL", new Vector3(1426f, 1.5f, 64f), new Vector3(1f, 3f, 4f), M("Stone"));
        Box(g, "CaveR", new Vector3(1432f, 1.5f, 64f), new Vector3(1f, 3f, 4f), M("Stone"));
        Box(g, "CaveTop", new Vector3(1429f, 3.2f, 64f), new Vector3(7f, 0.6f, 4f), M("Stone"));
        Box(g, "CaveBack", new Vector3(1429f, 1.5f, 66f), new Vector3(7f, 3f, 0.4f), M("Black"));
        foreach (float fx in new[] { 1427f, 1431f }) DPrim(PrimitiveType.Sphere, "Fire", g, new Vector3(fx, 0.4f, 63f), new Vector3(0.4f, 0.7f, 0.4f), M("GlowFlame"));
        Folk(g, "Old Fibula", "#bcaaa4", 0, new Vector3(1429f, 0, 63.5f), 180f, "Glade/gl_old");
        Gear("Old Fibula", 2, PrimitiveType.Sphere, new Vector3(0, -0.05f, 0.12f), new Vector3(0.22f, 0.3f, 0.08f), "CaneWhite");
        // the hero
        Folk(g, "Lank", "#a5d6a7", 22, new Vector3(1445f, 0, 50f), 180f, "Glade/gl_hero");
        Gear("Lank", 2, PrimitiveType.Capsule, new Vector3(0, 0.12f, -0.12f), new Vector3(0.26f, 0.3f, 0.26f), "GiftGreen", new Vector3(-50, 0, 0));
        Gear("Lank", 6, PrimitiveType.Cube, new Vector3(0, -0.45f, 0.05f), new Vector3(0.05f, 0.6f, 0.05f), "Metal");
        // a heart container floating
        var hg = Group(g, "Heart container", new Vector3(1445f, 2f, 36f));
        foreach (float hx in new[] { -0.15f, 0.15f }) Prim(PrimitiveType.Sphere, "Lobe", hg, new Vector3(hx, 0.1f, 0), Vector3.one * 0.35f, M("GlowRed"));
        Prim(PrimitiveType.Cube, "Point", hg, new Vector3(0, -0.1f, 0), new Vector3(0.3f, 0.3f, 0.3f), M("GlowRed")).transform.localRotation = Quaternion.Euler(0, 0, 45);
        var hr = Add<FirRide>(hg.gameObject); hr.kind = 0; hr.rotor = hg; hr.axis = Vector3.up; hr.degPerSec = 60f;
        report.Add("Hero's Glade: a sword that won't come out, pots, a chest, chickens (don't), a fairy fountain, an old man's cave, a heart container; 3 voiced skeletons and 4 gags");
    }

    // =================================================================== 7 Honeycomb Hollow (a bear, a bird in his backpack, notes to collect)

    static void BuildHoneycombHollow(Transform root)
    {
        var g = AreaShell(root, "Honeycomb Hollow", 1490f, 1540f, 20f, 70f, 20f, "PixelGreen", "PixelSky", "Sky",
                          "HONEYCOMB HOLLOW", new Color(1f, 0.75f, 0.2f), "collect the notes. find the jigsaw piece. ignore the rhyming.", "HONEYCOMB HOLLOW");
        // the spiral hill: a stepped tower you can climb, a jigsaw piece on top
        var notes = new List<Vector3>();
        for (int k = 0; k < 10; k++)
        {
            float s = 14f - k * 1.2f;
            Box(g, "Hill step " + k, new Vector3(1515f, 0.2f + k * 0.4f, 50f), new Vector3(s, 0.4f, s), M(k % 2 == 0 ? "PixelGreen" : "PixelGreenDark"));
            notes.Add(new Vector3(1515f + s / 2f - 0.3f, 1.4f + k * 0.4f, 50f));
            notes.Add(new Vector3(1515f - s / 2f + 0.3f, 1.4f + k * 0.4f, 50f));
        }
        for (int k = 0; k < 10; k++) notes.Add(new Vector3(1496f + (k % 5) * 2.5f, 1.2f, 26f + (k / 5) * 4f));
        var fb = Collectables(g, "NOTES", notes.ToArray(), (t, i) =>
        {
            Prim(PrimitiveType.Sphere, "Head", t, Vector3.zero, new Vector3(0.3f, 0.24f, 0.12f), M("GlowGold"));
            Prim(PrimitiveType.Cube, "Stem", t, new Vector3(0.13f, 0.3f, 0), new Vector3(0.04f, 0.6f, 0.04f), M("GlowGold"));
            return true;
        }, new Vector3(1508f, 2.6f, 20.15f), Vector3.forward);
        var jig = Gg(g, "The jigsaw piece", new Vector3(1515f, 4f, 50f), 180f, "Honey/hn_jigsaw", 1.4f, 1.8f);
        var piece = Group(jig.transform, "Piece", new Vector3(0, 0.9f, 0));
        Prim(PrimitiveType.Cube, "Piece", piece, Vector3.zero, new Vector3(0.8f, 0.8f, 0.12f), M("GlowGold"));
        Prim(PrimitiveType.Sphere, "Knob", piece, new Vector3(0.48f, 0, 0), new Vector3(0.3f, 0.3f, 0.12f), M("GlowGold"));
        Prim(PrimitiveType.Sphere, "Knob", piece, new Vector3(0, 0.48f, 0), new Vector3(0.3f, 0.3f, 0.12f), M("GlowGold"));
        jig.target = piece; jig.moveBy = new Vector3(0, 0.8f, 0); jig.spin = 270f;
        // the bear and the bird in his backpack
        Folk(g, "Barnaby", "#a1887f", 16, new Vector3(1500f, 0, 40f), 180f, "Honey/hn_bear", 1.1f);
        Gear("Barnaby", 1, PrimitiveType.Cube, new Vector3(0, 0.2f, -0.2f), new Vector3(0.45f, 0.45f, 0.25f), "InkBlue");
        foreach (float ex in new[] { -0.13f, 0.13f }) Gear("Barnaby", 2, PrimitiveType.Sphere, new Vector3(ex, 0.17f, 0), Vector3.one * 0.1f, "Trunk");
        Folk(g, "Kazoo", "#ef5350", 5, new Vector3(1500f, 1.55f, 40.35f), 180f, "Honey/hn_bird", 0.4f);
        Gear("Kazoo", 2, PrimitiveType.Cube, new Vector3(0, 0, 0.2f), new Vector3(0.08f, 0.06f, 0.25f), "GlowYellow");
        // the witch's lair
        Box(g, "Lair", new Vector3(1532f, 6f, 64f), new Vector3(10f, 12f, 10f), M("WallDark"));
        DPrim(PrimitiveType.Sphere, "LairEye", g, new Vector3(1530f, 8f, 58.95f), new Vector3(1.2f, 1.2f, 0.1f), M("GlowGreen"));
        DPrim(PrimitiveType.Sphere, "LairEye", g, new Vector3(1534f, 8f, 58.95f), new Vector3(1.2f, 1.2f, 0.1f), M("GlowGreen"));
        Box(g, "LairMouth", new Vector3(1532f, 1.5f, 58.95f), new Vector3(3f, 3f, 0.1f), M("Black"), false);
        Folk(g, "Grizelda", "#76ff03", 15, new Vector3(1528f, 0, 57.5f), 180f, "Honey/hn_witch");
        Gear("Grizelda", 2, PrimitiveType.Cylinder, new Vector3(0, 0.2f, 0), new Vector3(0.45f, 0.02f, 0.45f), "Black");
        Gear("Grizelda", 2, PrimitiveType.Capsule, new Vector3(0, 0.38f, 0), new Vector3(0.2f, 0.3f, 0.2f), "Black");
        // the mole, the villager
        DPrim(PrimitiveType.Sphere, "Molehill", g, new Vector3(1528f, 0, 32f), new Vector3(2f, 1f, 2f), M("Dirt"));
        Folk(g, "Professor Burrows", "#6d4c41", 20, new Vector3(1528f, 0.3f, 32f), 180f, "Honey/hn_mole", 0.7f);
        Gear("Professor Burrows", 2, PrimitiveType.Cube, new Vector3(0, 0.05f, 0.18f), new Vector3(0.22f, 0.06f, 0.02f), "Black");
        Folk(g, "Wobblechop", "#ffcc80", 12, new Vector3(1508f, 0, 62f), 180f, "Honey/hn_gibber");
        for (int k = 0; k < 4; k++) DPrim(PrimitiveType.Capsule, "Hive", g, new Vector3(1495f + k * 1.2f, 3.2f, 66f), new Vector3(0.6f, 0.5f, 0.6f), M("Honey"));
        report.Add("Honeycomb Hollow: " + notes.Count + " notes to collect on a climbable hill, a jigsaw piece, a bear with a bird in his backpack, a witch's lair, a mole and 1 gag");
    }

    // =================================================================== 8 Dragon Shore (free the crystal dragons, collect gems)

    static void Dragonize(string who, string wingMat)
    {
        foreach (float wx in new[] { -0.35f, 0.35f }) Gear(who, 1, PrimitiveType.Cube, new Vector3(wx, 0.25f, -0.15f), new Vector3(0.6f, 0.35f, 0.03f), wingMat, new Vector3(0, wx > 0 ? -25 : 25, wx > 0 ? 15 : -15));
        foreach (float hx in new[] { -0.09f, 0.09f }) Gear(who, 2, PrimitiveType.Cylinder, new Vector3(hx, 0.17f, -0.05f), new Vector3(0.04f, 0.1f, 0.04f), "GlowGold", new Vector3(-30, 0, 0));
        Gear(who, 1, PrimitiveType.Cube, new Vector3(0, -0.55f, -0.35f), new Vector3(0.07f, 0.07f, 0.6f), wingMat, new Vector3(30, 0, 0));
    }

    static void BuildDragonShore(Transform root)
    {
        var g = AreaShell(root, "Dragon Shore", 1560f, 1610f, 20f, 70f, 20f, "PixelGreen", "PixelSky", "Sky",
                          "DRAGON SHORE", new Color(0.65f, 0.35f, 1f), "free the dragons. collect the gems. leave the sheep alone (they're fine).", "DRAGON SHORE");
        Box(g, "Lagoon", new Vector3(1604f, 0.04f, 45f), new Vector3(12f, 0.06f, 50f), M("Water"), false);
        foreach (float ax in new[] { 1570f, 1585f }) { Box(g, "ArchL", new Vector3(ax - 2f, 3f, 66f), new Vector3(1f, 6f, 1f), M("PixelSand")); Box(g, "ArchR", new Vector3(ax + 2f, 3f, 66f), new Vector3(1f, 6f, 1f), M("PixelSand")); Box(g, "ArchTop", new Vector3(ax, 6.3f, 66f), new Vector3(5.5f, 0.8f, 1.2f), M("PixelSand")); }
        // the glide tower: stairs up, a launch pad at the top that throws you out over the field
        for (int k = 0; k < 14; k++) Box(g, "Stair " + k, new Vector3(1566f, 0.175f + k * 0.35f, 30f + k * 0.7f), new Vector3(2f, 0.35f, 0.7f), M("Stone"));
        Box(g, "GlideTop", new Vector3(1566f, 4.65f, 41.5f), new Vector3(4f, 0.5f, 4f), M("Stone"));
        var gems = new List<Vector3>();
        for (int k = 0; k < 16; k++) gems.Add(new Vector3(1572f + (k % 4) * 5f, 1.2f, 28f + (k / 4) * 8f));
        for (int k = 0; k < 6; k++) gems.Add(new Vector3(1566f, 7f, 46f + k * 2.6f));
        string[] gemMats = { "GlowRed", "GlowGreen", "InkBlue", "GlowGold", "GemPurple" };
        Collectables(g, "GEMS", gems.ToArray(), (t, i) => { Prim(PrimitiveType.Cube, "Gem", t, Vector3.zero, Vector3.one * 0.3f, M(gemMats[i % 5])).transform.localRotation = Quaternion.Euler(45, 0, 45); return true; },
                     new Vector3(1576f, 2.6f, 20.15f), Vector3.forward, new[] { new Vector3(1566f, 4.92f, 42f) }, new[] { new Vector3(0f, 7f, 9f) }, "GemPurple");
        Text("GlideSign", g, new Vector3(1566f, 6.4f, 40.05f), Face(Vector3.back), new Vector2(2.4f, 0.3f), 0.15f, "<b>GLIDE PAD</b> <size=70%>(wheee)</size>").color = Color.white;
        // the dragons
        Folk(g, "Spark", "#9c27b0", 18, new Vector3(1582f, 0, 30f), 180f, "Dragon/dg_dragon");
        Dragonize("Spark", "GemPurple");
        Folk(g, "Elder Basalt", "#5d4037", 15, new Vector3(1585f, 0, 63f), 180f, "Dragon/dg_elder", 1.3f);
        Dragonize("Elder Basalt", "Trunk");
        Folk(g, "Buzz", "#ffee58", 16, new Vector3(1580f, 1.4f, 31f), 180f, "Dragon/dg_fly", 0.25f);
        Gear("Buzz", 1, PrimitiveType.Cube, new Vector3(0, 0.25f, -0.1f), new Vector3(1f, 0.3f, 0.02f), "DarkGlowCyan");
        Folk(g, "Rubble", "#78909c", 5, new Vector3(1598f, 0, 30f), -90f, "Dragon/dg_thief", 0.8f);
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Sphere, "Egg rock", g, new Vector3(1597f + (k % 3) * 0.4f, 0.2f, 28.5f + (k / 3) * 0.4f), new Vector3(0.3f, 0.4f, 0.3f), M("Stone"));
        // the crystal dragons: press one to free it (the crystal shrinks away)
        for (int k = 0; k < 2; k++)
        {
            var cd = Gg(g, "Crystal dragon " + (k + 1), new Vector3(1575f + k * 12f, 0, 52f), 180f, "Dragon/dg_frozen", 1.8f, 2.6f);
            var rr = Mini(cd.transform, "Frozen dragon " + (k + 1), Vector3.zero, 0f, 1f, k == 0 ? "#26a69a" : "#ef6c00");
            Prim(PrimitiveType.Cube, "WingL", rr[1], new Vector3(-0.35f, 0.25f, -0.15f), new Vector3(0.6f, 0.35f, 0.03f), M("GemPurple"));
            Prim(PrimitiveType.Cube, "WingR", rr[1], new Vector3(0.35f, 0.25f, -0.15f), new Vector3(0.6f, 0.35f, 0.03f), M("GemPurple"));
            var crystal = Group(cd.transform, "Crystal", new Vector3(0, 1.1f, 0));
            Prim(PrimitiveType.Cube, "Crystal", crystal, Vector3.zero, new Vector3(1.4f, 2.4f, 1.2f), M("Glass")).transform.localRotation = Quaternion.Euler(0, 20, 5);
            cd.target = crystal; cd.scaleBy = 0.02f; cd.inTime = 0.6f;
        }
        // the sheep (it becomes a butterfly)
        var sh = Gg(g, "A sheep", new Vector3(1592f, 0, 44f), 180f, "Dragon/dg_sheep", 1.2f, 1.2f);
        var sheep = Group(sh.transform, "Sheep", Vector3.zero);
        Prim(PrimitiveType.Sphere, "Wool", sheep, new Vector3(0, 0.55f, 0), new Vector3(0.8f, 0.6f, 1f), M("CaneWhite"));
        Prim(PrimitiveType.Sphere, "Head", sheep, new Vector3(0, 0.7f, 0.5f), new Vector3(0.3f, 0.3f, 0.35f), M("Black"));
        foreach (float lx in new[] { -0.2f, 0.2f }) foreach (float lz in new[] { -0.3f, 0.3f }) Prim(PrimitiveType.Cube, "Leg", sheep, new Vector3(lx, 0.15f, lz), new Vector3(0.08f, 0.3f, 0.08f), M("Black"));
        sh.target = sheep; sh.moveBy = new Vector3(0, 2.5f, 0); sh.scaleBy = 0.4f; sh.spin = 360f; sh.inTime = 0.8f;
        report.Add("Dragon Shore: " + gems.Count + " gems, a glide tower, 2 crystal dragons to free, a sheep, a dragonfly, the elder, a thief; 3 gags");
    }
    // =================================================================== 9 Hero Tower (a superhero team's headquarters)

    static void BuildHeroTower(Transform root)
    {
        var g = AreaShell(root, "Hero Tower", 1630f, 1670f, 20f, 60f, 10f, "LabFloor", "LabGrey", "LabWhite",
                          "HERO TOWER", new Color(0.9f, 0.15f, 0.15f), "headquarters of the Bone Brigade. please wipe your feet. and your capes.", "HERO TOWER");
        Box(g, "Window", new Vector3(1650f, 5f, 59.9f), new Vector3(30f, 6f, 0.05f), M("WindowSnow"), false);
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Cube, "City", g, new Vector3(1637f + k * 3.8f, 2.5f + (k % 3), 59.8f), new Vector3(2.4f, 5f + (k % 3) * 2f, 0.05f), M("LabGrey"));
        Text("Emblem", g, new Vector3(1630.1f, 5f, 40f), Face(Vector3.right), new Vector2(8f, 2f), 1.2f, "<b>BB</b>").color = new Color(0.9f, 0.15f, 0.15f);
        // the briefing table
        Prim(PrimitiveType.Cylinder, "Table", g, new Vector3(1650f, 0.45f, 40f), new Vector3(5f, 0.45f, 5f), M("Black"));
        Prim(PrimitiveType.Cylinder, "Holo", g, new Vector3(1650f, 1.3f, 40f), new Vector3(2f, 0.4f, 2f), M("DarkGlowCyan"));
        Folk(g, "Commander Sternum", "#90a4ae", 20, new Vector3(1650f, 0, 36.5f), 0f, "Hero/hr_assemble");
        // the heroes
        Folk(g, "Captain Calcium", "#1e88e5", 11, new Vector3(1640f, 0, 48f), 180f, "Hero/hr_captain");
        Gear("Captain Calcium", 5, PrimitiveType.Cylinder, new Vector3(0, -0.2f, 0.1f), new Vector3(0.55f, 0.02f, 0.55f), "HeroRed", new Vector3(90, 0, 0));
        Gear("Captain Calcium", 5, PrimitiveType.Cylinder, new Vector3(0, -0.2f, 0.115f), new Vector3(0.35f, 0.02f, 0.35f), "CaneWhite", new Vector3(90, 0, 0));
        Gear("Captain Calcium", 5, PrimitiveType.Cylinder, new Vector3(0, -0.2f, 0.13f), new Vector3(0.18f, 0.02f, 0.18f), "HeroBlue", new Vector3(90, 0, 0));
        Folk(g, "Thunderbone", "#ffd54f", 15, new Vector3(1650f, 0, 50f), 180f, "Hero/hr_thunder", 1.15f);
        Gear("Thunderbone", 1, PrimitiveType.Cube, new Vector3(0, 0.1f, -0.15f), new Vector3(0.6f, 0.9f, 0.03f), "HeroRed");
        Box(g, "Hammer head", new Vector3(1651.5f, 0.2f, 49f), new Vector3(0.6f, 0.4f, 0.35f), M("Metal"));
        DPrim(PrimitiveType.Cylinder, "Hammer handle", g, new Vector3(1651.5f, 0.65f, 49f), new Vector3(0.08f, 0.3f, 0.08f), M("DoorWood"));
        Text("Hammer note", g, new Vector3(1651.5f, 1.2f, 48.8f), Face(Vector3.back), new Vector2(1.2f, 0.3f), 0.1f, "(nobody can lift it)\n(nobody has tried)").color = Color.white;
        Folk(g, "Ironrib", "#c62828", 9, new Vector3(1660f, 0, 48f), 180f, "Hero/hr_armor");
        Gear("Ironrib", 1, PrimitiveType.Cube, new Vector3(0, 0.15f, 0), new Vector3(0.45f, 0.55f, 0.3f), "HeroRed");
        Gear("Ironrib", 1, PrimitiveType.Cylinder, new Vector3(0, 0.25f, 0.16f), new Vector3(0.14f, 0.01f, 0.14f), "DarkGlowCyan", new Vector3(90, 0, 0));
        Gear("Ironrib", 2, PrimitiveType.Cube, new Vector3(0, 0.08f, 0.01f), new Vector3(0.3f, 0.32f, 0.32f), "GlowGold");
        // the snap: half the room's (tiny) skeletons blow away
        var snap = Gg(g, "The Gauntlet", new Vector3(1662f, 0, 30f), 180f, "Hero/hr_snap", 1.4f, 2f);
        Box(snap.transform, "Pedestal", new Vector3(0, 0.5f, 0), new Vector3(0.8f, 1f, 0.8f), M("Black"), false);
        Prim(PrimitiveType.Cube, "Glove", snap.transform, new Vector3(0, 1.25f, 0), new Vector3(0.3f, 0.5f, 0.2f), M("GlowGold"));
        for (int k = 0; k < 5; k++) Prim(PrimitiveType.Sphere, "Stone", snap.transform, new Vector3(-0.12f + k * 0.06f, 1.4f, 0.11f), Vector3.one * 0.05f, M(new[] { "GlowRed", "GemPurple", "InkBlue", "GlowGreen", "GlowYellow" }[k]));
        var half = Group(snap.transform, "Half of them", new Vector3(0, 0, 1.2f));
        for (int k = 0; k < 3; k++) Mini(half, "Tiny hero " + k, new Vector3(-0.8f + k * 0.8f, 0, 0), 180f, 0.45f, new[] { "#ef5350", "#42a5f5", "#66bb6a" }[k]);
        snap.target = half; snap.scaleBy = 0.02f; snap.moveBy = new Vector3(0, 0.6f, 0); snap.inTime = 1.5f; snap.holdTime = 3f;
        // the after-credits scene
        var cr = Gg(g, "The credits", new Vector3(1640f, 0, 24f), 0f, "Hero/hr_credits", 2.4f, 2.6f);
        Box(cr.transform, "Screen", new Vector3(0, 1.6f, -0.3f), new Vector3(2.4f, 1.6f, 0.05f), M("Black"), false);
        var roll = Group(cr.transform, "Roll", new Vector3(0, 0.4f, -0.33f));
        Text("Names", roll, Vector3.zero, Face(Vector3.forward), new Vector2(2.2f, 1.6f), 0.11f, "<b>THE BONE BRIGADE</b>\nstarring: some skeletons\nbest boy: a skeleton\ncatering: none (skeletons)\n\n<i>stay for the scene after the credits</i>").color = Color.white;
        cr.target = roll; cr.moveBy = new Vector3(0, 1.4f, 0); cr.inTime = 3f;
        report.Add("Hero Tower: the Bone Brigade's headquarters, with 4 heroes, a hammer nobody has tried to lift, a gauntlet and an after-credits scene (2 gags)");
    }

    // =================================================================== 10 Gloom City (a brooding vigilante's rooftops at night)

    static void BuildGloomCity(Transform root)
    {
        var g = AreaShell(root, "Gloom City", 1690f, 1740f, 20f, 70f, 25f, "GloomStone", "NightSky", "NightSky",
                          "GLOOM CITY", new Color(0.7f, 0.7f, 0.8f), "it's always night here. it's always raining somewhere else.", "GLOOM CITY");
        // the skyline round the edges
        for (int k = 0; k < 12; k++)
        {
            float hgt = 8f + (k * 7 % 5) * 3f;
            Box(g, "Tower", new Vector3(1693f + k * 4.2f, hgt / 2f, 68f), new Vector3(3.6f, hgt, 3.6f), M("GloomStone"), false);
            for (int w = 0; w < 4; w++) DPrim(PrimitiveType.Cube, "Window", g, new Vector3(1692f + k * 4.2f + (w % 2) * 2f, 3f + w * 1.7f, 66.18f), new Vector3(0.5f, 0.7f, 0.02f), M(w % 3 == 0 ? "GlowWarm" : "Black"));
        }
        foreach (float wx in new[] { 1692f, 1738f }) for (int k = 0; k < 4; k++) Box(g, "SideTower", new Vector3(wx, 5f + k, 30f + k * 9f), new Vector3(3f, 10f + 2f * k, 6f), M("GloomStone"), false);
        // the gargoyle ledge, and the owl on it
        Box(g, "Ledge", new Vector3(1715f, 1.5f, 56f), new Vector3(6f, 3f, 3f), M("GloomStone"));
        Prim(PrimitiveType.Sphere, "Gargoyle", g, new Vector3(1712.5f, 3.4f, 54.8f), new Vector3(0.6f, 0.8f, 0.6f), M("Stone"));
        Folk(g, "The Night Owl", "#37474f", 11, new Vector3(1715f, 3f, 55.5f), 180f, "Gloom/gc_owl");
        foreach (float ex in new[] { -0.1f, 0.1f }) Gear("The Night Owl", 2, PrimitiveType.Cube, new Vector3(ex, 0.2f, 0), new Vector3(0.05f, 0.14f, 0.05f), "Black", new Vector3(0, 0, ex > 0 ? -10 : 10));
        Gear("The Night Owl", 1, PrimitiveType.Cube, new Vector3(0, -0.2f, -0.15f), new Vector3(0.7f, 1.3f, 0.03f), "Black", new Vector3(8, 0, 0));
        // the signal on the roof
        var sig = Gg(g, "The bone signal", new Vector3(1700f, 0, 44f), 90f, "Gloom/gc_signal", 1.6f, 1.8f);
        Prim(PrimitiveType.Cylinder, "Lamp", sig.transform, new Vector3(0, 0.8f, 0), new Vector3(0.9f, 0.5f, 0.9f), M("Metal")).transform.localRotation = Quaternion.Euler(-60, 0, 0);
        var beam = Group(sig.transform, "Beam", new Vector3(0, 1f, 0));
        beam.localRotation = Quaternion.Euler(-60, 0, 0);
        Prim(PrimitiveType.Cylinder, "Beam", beam, new Vector3(0, 9f, 0), new Vector3(0.8f, 9f, 0.8f), M("BeamWhite"));
        Prim(PrimitiveType.Cube, "Bone", beam, new Vector3(0, 18f, 0), new Vector3(3f, 0.1f, 0.8f), M("Black"));
        sig.target = beam; sig.hideWhenIdle = true; sig.wobble = 3f; sig.holdTime = 5f;
        // a flying hero circling overhead
        var flyer = Group(g, "Flying hero", new Vector3(1715f, 14f, 45f));
        var fl = Mini(flyer, "Flyer", new Vector3(10f, 0, 0), 0f, 1f, "#1565c0");
        fl[0].localRotation = Quaternion.Euler(80, 0, 0);
        Prim(PrimitiveType.Cube, "Cape", fl[1], new Vector3(0, 0, -0.15f), new Vector3(0.5f, 0.9f, 0.03f), M("HeroRed"));
        var fr = Add<FirRide>(flyer.gameObject); fr.kind = 0; fr.rotor = flyer; fr.axis = Vector3.up; fr.degPerSec = 25f;
        Folk(g, "Captain Krest", "#1976d2", 13, new Vector3(1724f, 0, 40f), -90f, "Gloom/gc_flyer");
        Gear("Captain Krest", 1, PrimitiveType.Cube, new Vector3(0, -0.2f, -0.15f), new Vector3(0.6f, 1.2f, 0.03f), "HeroRed");
        // the villain, the butler, the reporter
        Folk(g, "Mister Giggles", "#8e24aa", 5, new Vector3(1730f, 0, 58f), 210f, "Gloom/gc_giggles");
        Gear("Mister Giggles", 2, PrimitiveType.Sphere, new Vector3(0, 0.15f, -0.02f), new Vector3(0.3f, 0.15f, 0.3f), "GlowGreen");
        Box(g, "JackBox", new Vector3(1731.5f, 0.35f, 57f), new Vector3(0.7f, 0.7f, 0.7f), M("GemPurple"));
        Folk(g, "Alfrond", "#cfd8dc", 20, new Vector3(1706f, 0, 56f), 150f, "Gloom/gc_butler");
        Gear("Alfrond", 5, PrimitiveType.Cylinder, new Vector3(0, -0.25f, 0.15f), new Vector3(0.45f, 0.02f, 0.45f), "Metal");
        Folk(g, "Lois Lumbar", "#ffab91", 20, new Vector3(1708f, 0, 30f), 45f, "Gloom/gc_reporter");
        for (int k = 0; k < 4; k++)
        {
            DPrim(PrimitiveType.Cylinder, "Streetlamp", g, new Vector3(1700f + k * 10f, 1.8f, 24f), new Vector3(0.12f, 1.8f, 0.12f), M("Black"));
            DPrim(PrimitiveType.Sphere, "Lamp", g, new Vector3(1700f + k * 10f, 3.7f, 24f), Vector3.one * 0.4f, M("GlowWarm"));
        }
        report.Add("Gloom City: rooftops at night, a skyline, a vigilante on a gargoyle, a flying hero circling, a clown villain, a butler, a reporter and the bone signal (1 gag)");
    }

    // =================================================================== 11 Myrtle Beach (the Grand Strand, shrunk)

    static void PalmTree(Transform g, Vector3 at, float h)
    {
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cylinder, "PalmTrunk", g, at + new Vector3(k * 0.05f, 0.4f + k * h / 6f, 0), new Vector3(0.35f - k * 0.02f, h / 12f, 0.35f - k * 0.02f), M("Trunk"));
        for (int k = 0; k < 6; k++)
        {
            float a = k * 60f;
            DPrim(PrimitiveType.Cube, "Frond", g, at + new Vector3(0.3f, h + 0.2f, 0) + Quaternion.Euler(0, a, 0) * new Vector3(0, 0, 0.9f), new Vector3(0.35f, 0.05f, 1.8f), M("Hedge"), Quaternion.Euler(20, a, 0));
        }
    }

    static void BuildMyrtleBeach(Transform root)
    {
        var g = AreaShell(root, "Myrtle Beach", 1760f, 1820f, 20f, 80f, 25f, "PixelSand", "PixelSky", "Sky",
                          "MYRTLE BEACH", new Color(1f, 0.5f, 0.3f), "sun, sand, pancakes, mini golf, and exactly one hundred beach shops", "MYRTLE BEACH");
        Box(g, "Ocean", new Vector3(1815f, 0.04f, 50f), new Vector3(10f, 0.06f, 60f), M("Water"), false);
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cube, "Wave", g, new Vector3(1810.5f + (k % 2) * 0.6f, 0.08f, 25f + k * 10f), new Vector3(0.3f, 0.05f, 8f), M("CaneWhite"));
        // the boardwalk
        Box(g, "Boardwalk", new Vector3(1798f, 0.15f, 50f), new Vector3(5f, 0.3f, 58f), M("DeskWood"));
        for (int k = 0; k < 12; k++) DPrim(PrimitiveType.Cylinder, "Rail", g, new Vector3(1800.4f, 0.8f, 23f + k * 5f), new Vector3(0.1f, 0.5f, 0.1f), M("CaneWhite"));
        // the big wheel
        var fw = Group(g, "Big Wheel", Vector3.zero);
        var hub = new Vector3(1775f, 9f, 72f);
        var wheel = Group(fw, "Rotor", hub);
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cylinder, "Spoke", wheel, Quaternion.Euler(0, 0, k * 60f) * new Vector3(0, 3.5f, 0), new Vector3(0.12f, 3.5f, 0.12f), M("CaneWhite"), Quaternion.Euler(0, 0, k * 60f));
        for (int k = 0; k < 24; k++)
        {
            float a = k * 15f;
            Prim(PrimitiveType.Cube, "Rim", wheel, Quaternion.Euler(0, 0, a) * new Vector3(0, 7f, 0), new Vector3(1.85f, 0.15f, 0.15f), M("CaneWhite")).transform.localRotation = Quaternion.Euler(0, 0, a);
            Prim(PrimitiveType.Sphere, "RimBulb", wheel, Quaternion.Euler(0, 0, a) * new Vector3(0, 7.2f, 0), Vector3.one * 0.14f, Bulb(k % 2 == 0 ? "BulbBlue" : "BulbGold"));
        }
        foreach (float lx in new[] { -2.5f, 2.5f }) DPrim(PrimitiveType.Cube, "Leg", fw, hub + new Vector3(lx, -4.5f, -0.6f), new Vector3(0.3f, 9f, 0.3f), M("CaneWhite"), Quaternion.Euler(0, 0, lx > 0 ? -16 : 16));
        var wheelExit = Marker(fw, "Wheel exit", new Vector3(1775f, 0.05f, 68f), 180f);
        var gond = new List<Transform>();
        for (int k = 0; k < 6; k++)
        {
            var gp = Group(wheel, "Gondola " + k, Quaternion.Euler(0, 0, k * 60f) * new Vector3(0, -7f, 0));
            Box(gp, "Floor", new Vector3(0, -1.4f, 0), new Vector3(1.3f, 0.1f, 1.1f), M(k % 2 == 0 ? "InkBlue" : "GlowYellow"), false);
            Box(gp, "Roof", new Vector3(0, -0.05f, 0), new Vector3(1.4f, 0.1f, 1.2f), M(k % 2 == 0 ? "InkBlue" : "GlowYellow"), false);
            DPrim(PrimitiveType.Cylinder, "Hanger", gp, new Vector3(0, 0.1f, 0), new Vector3(0.08f, 0.15f, 0.08f), M("Metal"));
            RideSeat(gp, "Beach wheel seat " + k, new Vector3(0, -1.35f, 0.2f), 180f, wheelExit);
            gond.Add(gp);
        }
        var wr = Add<FirRide>(fw.gameObject); wr.kind = 0; wr.rotor = wheel; wr.axis = Vector3.forward; wr.degPerSec = 5f; wr.upright = gond.ToArray();
        Folk(g, "Wheelie", "#4fc3f7", 20, new Vector3(1780f, 0, 66f), 180f, "Myrtle/mb_wheel");
        // pancake houses 47, 48 and 49
        for (int k = 0; k < 3; k++)
        {
            var ph = Stall(g, "PANCAKE HOUSE #" + (47 + k), new Vector3(1768f + k * 7f, 0, 26f), 0f, "GlowYellow", "DeskWood");
            if (k == 1) Folk(ph, "Flapjack", "#ffe082", 3, new Vector3(0, 0, -0.3f), 0f, "Myrtle/mb_pancake");
        }
        // mini golf and its volcano
        Box(g, "Green", new Vector3(1770f, 0.01f, 46f), new Vector3(10f, 0.02f, 8f), M("GrassBlock"), false);
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Cylinder, "Volcano", g, new Vector3(1766f, 0.5f + k * 0.9f, 49f), new Vector3(5f - k * 0.9f, 0.5f, 5f - k * 0.9f), M("Dirt"));
        DPrim(PrimitiveType.Sphere, "Lava", g, new Vector3(1766f, 4.7f, 49f), new Vector3(1.2f, 0.5f, 1.2f), M("GlowFlame"));
        DPrim(PrimitiveType.Cylinder, "Hole", g, new Vector3(1773f, 0.025f, 44f), new Vector3(0.3f, 0.01f, 0.3f), M("Black"));
        DPrim(PrimitiveType.Cylinder, "Flag", g, new Vector3(1773f, 0.8f, 44f), new Vector3(0.03f, 0.8f, 0.03f), M("CaneWhite"));
        Folk(g, "Putter", "#81c784", 14, new Vector3(1772f, 0, 46f), 180f, "Myrtle/mb_golf");
        // the beach shop, the taffy, the shag dancers
        var shop = Stall(g, "BEACH STUFF", new Vector3(1790f, 0, 40f), 90f, "GiftBlue", "CaneWhite");
        Folk(shop, "Conchita", "#ffab40", 3, new Vector3(0, 0, -0.3f), 0f, "Myrtle/mb_shop");
        var taffy = Stall(g, "TAFFY", new Vector3(1790f, 0, 56f), 90f, "BonePink", "CaneWhite");
        Folk(taffy, "Pully", "#f48fb1", 3, new Vector3(0, 0, -0.3f), 0f, "Myrtle/mb_taffy");
        Box(g, "DanceFloor", new Vector3(1785f, 0.01f, 30f), new Vector3(4f, 0.02f, 4f), M("Stage"), false);
        Folk(g, "Shag Shelby", "#ce93d8", 7, new Vector3(1784f, 0, 30f), 90f, "Myrtle/mb_shag");
        Folk(g, "Shag Shawn", "#90caf9", 7, new Vector3(1786f, 0, 30f), -90f, "Myrtle/mb_shag");
        // the seagull
        var gull = Gg(g, "Seagull", new Vector3(1794f, 0, 48f), -90f, "Myrtle/mb_gull", 1.2f, 1.4f);
        Box(gull.transform, "Fries", new Vector3(0, 0.45f, 0.3f), new Vector3(0.3f, 0.4f, 0.2f), M("CaneRed"), false);
        var bird = Group(gull.transform, "Gull", new Vector3(0, 1.2f, 1.5f));
        Prim(PrimitiveType.Sphere, "Body", bird, Vector3.zero, new Vector3(0.3f, 0.3f, 0.5f), M("CaneWhite"));
        Prim(PrimitiveType.Cube, "Wings", bird, Vector3.zero, new Vector3(1.2f, 0.04f, 0.3f), M("LabGrey"));
        Prim(PrimitiveType.Cube, "Fry", bird, new Vector3(0, -0.1f, 0.28f), new Vector3(0.04f, 0.04f, 0.25f), M("GlowYellow"));
        bird.localScale = Vector3.one;
        gull.target = bird; gull.moveBy = new Vector3(0, 3f, -6f); gull.spin = 0f; gull.wobble = 12f; gull.inTime = 1f;
        // beach things
        for (int k = 0; k < 5; k++)
        {
            DPrim(PrimitiveType.Cylinder, "UmbrellaPole", g, new Vector3(1805f, 1.1f, 26f + k * 11f), new Vector3(0.06f, 1.1f, 0.06f), M("CaneWhite"));
            DPrim(PrimitiveType.Sphere, "Umbrella", g, new Vector3(1805f, 2.2f, 26f + k * 11f), new Vector3(2.4f, 0.4f, 2.4f), M(k % 2 == 0 ? "GiftRed" : "GiftBlue"));
            DPrim(PrimitiveType.Cube, "Towel", g, new Vector3(1806.2f, 0.01f, 26f + k * 11f), new Vector3(0.8f, 0.01f, 1.8f), M(k % 2 == 0 ? "GlowYellow" : "GemPink"));
        }
        foreach (var pp in new[] { new Vector3(1764f, 0, 36f), new Vector3(1784f, 0, 54f), new Vector3(1796f, 0, 75f), new Vector3(1764f, 0, 60f) }) PalmTree(g, pp, 5.5f);
        report.Add("Myrtle Beach: the boardwalk, a big wheel you can ride, pancake houses #47-49, mini golf with a volcano, a beach shop, taffy, shag dancers, a seagull (1 gag)");
    }

    // =================================================================== 12 Boone, NC (a mountain college town)

    static void BuildBoone(Transform root)
    {
        var g = AreaShell(root, "Boone", 1840f, 1900f, 20f, 80f, 25f, "PixelGreen", "RidgeBlue2", "Sky",
                          "BOONE, NC", new Color(1f, 0.8f, 0.2f), "a mountain town: a bridge in the sky, a rock where the snow falls up, and very good coffee", "BOONE, NC");
        // King Street
        string[] shops = { "COFFEE", "OUTFITTERS", "BOOKS", "GENERAL STORE", "BAGELS" };
        for (int k = 0; k < 5; k++)
        {
            Box(g, "Shopfront", new Vector3(1848f + k * 6f, 2.5f, 35f), new Vector3(5.6f, 5f, 4f), M(k % 2 == 0 ? "Brick" : "BrickWall"));
            Box(g, "Awning", new Vector3(1848f + k * 6f, 2.8f, 32.6f), new Vector3(5f, 0.15f, 1.2f), M(k % 2 == 0 ? "GiftGreen" : "CaneRed"), false);
            Text("ShopSign", g, new Vector3(1848f + k * 6f, 3.6f, 32.98f), Face(Vector3.back), new Vector2(5f, 0.6f), 0.4f, "<b>" + shops[k] + "</b>").color = new Color(1f, 0.95f, 0.8f);
        }
        Text("StreetSign", g, new Vector3(1860f, 6f, 32.98f), Face(Vector3.back), new Vector2(12f, 0.8f), 0.6f, "<b>KING STREET</b>").color = Color.white;
        Folk(g, "Barista Birch", "#8d6e63", 3, new Vector3(1848f, 0, 31.5f), 180f, "Boone/bn_coffee");
        Gear("Barista Birch", 1, PrimitiveType.Cube, new Vector3(0, 0.1f, 0.04f), new Vector3(0.42f, 0.6f, 0.3f), "CaneRed");
        Folk(g, "Freshman Femi", "#ffca28", 16, new Vector3(1862f, 0, 30f), 180f, "Boone/bn_student");
        Gear("Freshman Femi", 1, PrimitiveType.Cube, new Vector3(0, 0.2f, -0.2f), new Vector3(0.35f, 0.4f, 0.2f), "GiftBlue");
        // the porch and the banjo
        Box(g, "Porch", new Vector3(1882f, 0.25f, 30f), new Vector3(6f, 0.5f, 4f), M("DeskWood"));
        Box(g, "PorchRoof", new Vector3(1882f, 3.2f, 30f), new Vector3(6.4f, 0.2f, 4.4f), M("Trunk"), false);
        foreach (float px in new[] { -2.8f, 2.8f }) DPrim(PrimitiveType.Cylinder, "PorchPost", g, new Vector3(1882f + px, 1.7f, 28f), new Vector3(0.15f, 1.5f, 0.15f), M("DeskWood"));
        Folk(g, "Picker Hollis", "#a1887f", 11, new Vector3(1882f, 0.5f, 30.5f), 180f, "Boone/bn_banjo");
        Gear("Picker Hollis", 3, PrimitiveType.Cylinder, new Vector3(0.1f, -0.35f, 0.2f), new Vector3(0.35f, 0.03f, 0.35f), "CaneWhite", new Vector3(90, 0, 0));
        // the two peaks and the swinging bridge between them
        for (int k = 0; k < 16; k++) Box(g, "PeakW step " + k, new Vector3(1850f, 0.175f + k * 0.35f, 52f + k * 0.5f), new Vector3(6f, 0.35f, 0.5f), M("Stone"));
        Box(g, "PeakW", new Vector3(1850f, 2.8f, 64f), new Vector3(8f, 5.6f, 8f), M("Stone"));
        Box(g, "PeakE", new Vector3(1872f, 2.8f, 64f), new Vector3(8f, 5.6f, 8f), M("Stone"));
        for (int k = 0; k < 16; k++) Box(g, "PeakE step " + k, new Vector3(1872f, 0.175f + k * 0.35f, 76f - k * 0.5f), new Vector3(6f, 0.35f, 0.5f), M("Stone"));
        Box(g, "Bridge", new Vector3(1861f, 5.55f, 64f), new Vector3(14f, 0.1f, 1.4f), M("DeskWood"));
        foreach (float bz in new[] { -0.75f, 0.75f }) Box(g, "BridgeRail", new Vector3(1861f, 6.4f, 64f + bz), new Vector3(14f, 0.05f, 0.05f), M("Metal"));
        foreach (float bz in new[] { -0.75f, 0.75f }) for (int k = 0; k < 8; k++) Box(g, "BridgePost", new Vector3(1854.5f + k * 1.85f, 6f, 64f + bz), new Vector3(0.05f, 0.9f, 0.05f), M("Metal"), true);
        Text("BridgeSign", g, new Vector3(1850f, 6.8f, 60.05f), Face(Vector3.back), new Vector2(5f, 0.6f), 0.22f, "<b>THE MILE HIGH SWINGING BRIDGE</b>\n<size=60%>(scale model. 1 mile = 5.6 metres)</size>").color = Color.white;
        DPrim(PrimitiveType.Sphere, "Snowcap", g, new Vector3(1872f, 5.8f, 66f), new Vector3(5f, 0.6f, 3f), M("Snow"));
        Folk(g, "Trailhead Tom", "#689f38", 22, new Vector3(1864f, 5.6f, 64f), 90f, "Boone/bn_hiker");
        // the rock where the snow falls up
        Box(g, "Blowing rock", new Vector3(1894f, 1.5f, 56f), new Vector3(6f, 3f, 6f), M("Stone"));
        var rock = Gg(g, "The blowing rock", new Vector3(1891f, 3f, 56f), -90f, "Boone/bn_rock", 1.4f, 2f);
        var hat = Group(rock.transform, "Hat", new Vector3(0, 1.2f, 0.6f));
        Prim(PrimitiveType.Cylinder, "Brim", hat, Vector3.zero, new Vector3(0.5f, 0.02f, 0.5f), M("Trunk"));
        Prim(PrimitiveType.Cylinder, "Crown", hat, new Vector3(0, 0.1f, 0), new Vector3(0.3f, 0.12f, 0.3f), M("Trunk"));
        rock.target = hat; rock.moveBy = new Vector3(0, 4f, -1f); rock.spin = 400f; rock.inTime = 1.2f;
        // the ski slope
        Box(g, "Slope", new Vector3(1888f, 3f, 72f), new Vector3(8f, 0.3f, 14f), M("Snow"), false).transform.localRotation = Quaternion.Euler(-22, 0, 0);
        Folk(g, "Slalom Sally", "#e1f5fe", 6, new Vector3(1886f, 0, 63f), 180f, "Boone/bn_ski");
        foreach (float sx in new[] { -0.1f, 0.1f }) Gear("Slalom Sally", sx < 0 ? 7 : 8, PrimitiveType.Cube, new Vector3(0, -0.88f, 0.15f), new Vector3(0.08f, 0.02f, 1f), "GiftRed");
        // the old steam train, round the town
        var tr = Group(g, "Steam train", Vector3.zero);
        var route = new[] { new Vector3(1843f, 0, 26f), new Vector3(1843f, 0, 46f), new Vector3(1897f, 0, 46f), new Vector3(1897f, 0, 26f) };
        for (int k = 0; k < 4; k++)
        {
            var a = route[k]; var b = route[(k + 1) % 4];
            Box(tr, "Track", (a + b) / 2f + new Vector3(0, 0.02f, 0), new Vector3(Mathf.Abs(b.x - a.x) + 1.2f, 0.04f, Mathf.Abs(b.z - a.z) + 1.2f), M("Dirt"), false);
        }
        var loco = Group(tr, "Locomotive", route[0]);
        Prim(PrimitiveType.Cylinder, "Boiler", loco, new Vector3(0, 1f, 0.6f), new Vector3(0.9f, 0.9f, 0.9f), M("Black")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Cube, "Cab", loco, new Vector3(0, 1.1f, -0.7f), new Vector3(1.2f, 1.4f, 1f), M("GiftRed"));
        Prim(PrimitiveType.Cylinder, "Stack", loco, new Vector3(0, 1.9f, 1.2f), new Vector3(0.3f, 0.4f, 0.3f), M("Black"));
        var trainExit = Marker(tr, "Train exit", new Vector3(1845f, 0.05f, 28.5f), 0f);
        RideSeat(loco, "Train seat", new Vector3(0, 0.5f, -0.7f), 0f, trainExit);
        var trr = Add<FirRide>(tr.gameObject); trr.kind = 1; trr.carts = new[] { loco }; trr.route = route; trr.pace = new[] { 1f, 1f, 1f, 1f }; trr.speed = 2.2f;
        Folk(g, "Conductor Clem", "#455a64", 20, new Vector3(1846f, 0, 29f), 90f, "Boone/bn_train");
        Gear("Conductor Clem", 2, PrimitiveType.Cylinder, new Vector3(0, 0.15f, 0), new Vector3(0.28f, 0.08f, 0.28f), "InkBlue");
        // the fog
        var fog = Gg(g, "The fog", new Vector3(1878f, 0, 50f), 180f, "Boone/bn_fog", 2f, 2f);
        Box(fog.transform, "FogSign", new Vector3(0, 1f, 0), new Vector3(1.2f, 0.6f, 0.05f), M("Board"), false);
        Text("FogText", fog.transform, new Vector3(0, 1f, -0.04f), Quaternion.identity, new Vector2(1.1f, 0.5f), 0.1f, "FOG\nAHEAD\n(press)").color = Color.white;
        var bank = Group(fog.transform, "Fog bank", new Vector3(0, 1.5f, 6f));
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Sphere, "Fog", bank, new Vector3(-4f + k * 1.6f, (k % 2) * 0.5f, 0), new Vector3(3f, 2.5f, 3f), M("Mist"));
        fog.target = bank; fog.moveBy = new Vector3(0, 0, -5f); fog.scaleBy = 1.4f; fog.hideWhenIdle = true; fog.inTime = 1.5f;
        // fall colours
        string[] leaf = { "FoliageOrange", "FoliageRed", "GlowYellow", "Hedge" };
        for (int k = 0; k < 12; k++)
        {
            var tp = new Vector3(1843f + (k % 6) * 2f + (k / 6) * 52f, 0, 50f + (k % 3) * 9f);
            DPrim(PrimitiveType.Cylinder, "Trunk", g, tp + new Vector3(0, 1.5f, 0), new Vector3(0.25f, 1.5f, 0.25f), M("Trunk"));
            DPrim(PrimitiveType.Sphere, "Leaves", g, tp + new Vector3(0, 3.4f, 0), new Vector3(2.2f, 2f, 2.2f), M(leaf[k % 4]));
        }
        report.Add("Boone, NC: King Street, a bluegrass porch, a swinging bridge between two peaks, the rock where the snow falls up, a ski slope, a steam train you can ride, fog, fall colours; 2 gags");
    }

    // =================================================================== 13 The Dachshund Room

    static Transform Doxie(Transform parent, string name, Vector3 at, float yaw, float length, string mat)
    {
        var d = Group(parent, name, at);
        d.localRotation = Quaternion.Euler(0, yaw, 0);
        Prim(PrimitiveType.Capsule, "Body", d, new Vector3(0, 0.3f, 0), new Vector3(0.3f, length / 2f, 0.3f), M(mat)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Sphere, "Head", d, new Vector3(0, 0.45f, length / 2f + 0.05f), new Vector3(0.28f, 0.28f, 0.32f), M(mat));
        Prim(PrimitiveType.Capsule, "Snout", d, new Vector3(0, 0.4f, length / 2f + 0.25f), new Vector3(0.14f, 0.14f, 0.14f), M(mat)).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Prim(PrimitiveType.Sphere, "Nose", d, new Vector3(0, 0.42f, length / 2f + 0.39f), Vector3.one * 0.06f, M("Black"));
        foreach (float ex in new[] { -0.15f, 0.15f })
        {
            Prim(PrimitiveType.Cube, "Ear", d, new Vector3(ex, 0.38f, length / 2f), new Vector3(0.04f, 0.22f, 0.14f), M("DoxieBlack"));
            Prim(PrimitiveType.Sphere, "Eye", d, new Vector3(ex * 0.5f, 0.52f, length / 2f + 0.17f), Vector3.one * 0.05f, M("Black"));
        }
        foreach (float lx in new[] { -0.1f, 0.1f }) foreach (float lz in new[] { -length / 2f + 0.15f, length / 2f - 0.15f })
            Prim(PrimitiveType.Cube, "Leg", d, new Vector3(lx, 0.08f, lz), new Vector3(0.07f, 0.16f, 0.07f), M(mat));
        Prim(PrimitiveType.Cube, "Tail", d, new Vector3(0, 0.4f, -length / 2f - 0.1f), new Vector3(0.04f, 0.04f, 0.3f), M(mat)).transform.localRotation = Quaternion.Euler(-30, 0, 0);
        return d;
    }

    static void BuildDachshunds(Transform root)
    {
        var g = AreaShell(root, "The Dachshund Room", 1920f, 1950f, 20f, 50f, 5f, "Rug", "WallGreen", "Ceiling",
                          "THE DACHSHUND ROOM", new Color(0.6f, 0.3f, 0.1f), "a room for long dogs. mind the ankles.", "DACHSHUND ROOM");
        string[] coats = { "DoxieBrown", "DoxieBlack", "Bun", "DoxieBrown" };
        // dogs lying about
        for (int k = 0; k < 8; k++)
        {
            var bed = new Vector3(1923f + (k % 4) * 7f, 0, 47f - (k / 4) * 4f);
            DPrim(PrimitiveType.Cylinder, "DogBed", g, bed + new Vector3(0, 0.08f, 0), new Vector3(1.6f, 0.08f, 1f), M(k % 2 == 0 ? "GiftRed" : "GiftBlue"));
            Doxie(g, "Sleepy doxie " + k, bed + new Vector3(0, 0.1f, 0), 90f + k * 20f, 0.9f + (k % 3) * 0.3f, coats[k % 4]);
        }
        // portraits
        for (int k = 0; k < 6; k++)
        {
            Box(g, "Frame", new Vector3(1920.06f, 2.6f, 24f + k * 4.2f), new Vector3(0.05f, 1f, 1.4f), M("GlowGold"), false);
            Box(g, "Portrait", new Vector3(1920.1f, 2.6f, 24f + k * 4.2f), new Vector3(0.02f, 0.85f, 1.25f), M("Paper"), false);
            DPrim(PrimitiveType.Capsule, "Painted doxie", g, new Vector3(1920.13f, 2.5f, 24f + k * 4.2f), new Vector3(0.02f, 0.5f, 0.2f), M(coats[k % 4]), Quaternion.Euler(90, 0, 0));
        }
        Box(g, "Sausage couch", new Vector3(1935f, 0.35f, 36f), new Vector3(5f, 0.7f, 1f), M("DoxieBrown"));
        // the longest dachshund in the world, arriving through the dog door
        Box(g, "DogDoor", new Vector3(1949.95f, 0.45f, 30f), new Vector3(0.04f, 0.9f, 0.8f), M("Black"), false);
        var lg = Gg(g, "The longest dachshund", new Vector3(1948f, 0, 30f), 0f, "Doxie/dx_long", 2.4f, 1.4f);
        var longDog = Doxie(lg.transform, "Long dog", new Vector3(8.6f, 0, 0), -90f, 16f, "DoxieBrown");
        lg.target = longDog; lg.moveBy = new Vector3(-6f, 0, 0); lg.bob = 0.03f; lg.inTime = 4f; lg.outTime = 2f;
        // the hot dog costume, the stairs, the race, the barker, the skeleton
        var bun = Gg(g, "Hot dog costume", new Vector3(1926f, 0, 26f), 180f, "Doxie/dx_bun", 1.4f, 1f);
        var bd = Doxie(bun.transform, "Bun dog", Vector3.zero, 0f, 1.1f, "DoxieBrown");
        foreach (float bx in new[] { -0.2f, 0.2f }) Prim(PrimitiveType.Capsule, "Bun", bd, new Vector3(bx, 0.3f, 0), new Vector3(0.15f, 0.55f, 0.2f), M("Bun")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        bun.target = bd; bun.wobble = 10f; bun.bob = 0.05f;
        var stairs = Gg(g, "The stairs", new Vector3(1932f, 0, 26f), 180f, "Doxie/dx_stairs", 1.6f, 1.2f);
        for (int k = 0; k < 4; k++) Box(stairs.transform, "Step", new Vector3(0, 0.1f + k * 0.2f, -0.6f - k * 0.3f), new Vector3(1f, 0.2f + k * 0.4f, 0.3f), M("DeskWood"), false);
        var sd = Doxie(stairs.transform, "Stair dog", new Vector3(0, 0, 0.3f), 180f, 0.9f, "DoxieBlack");
        stairs.target = sd; stairs.moveBy = new Vector3(0, 0.15f, -0.35f); stairs.rotateBy = new Vector3(-25f, 0, 0); stairs.inTime = 1f; stairs.outTime = 0.3f;
        var race = Gg(g, "The wiener dog race", new Vector3(1940f, 0, 24f), 90f, "Doxie/dx_race", 2.4f, 1.2f);
        Box(g, "RaceTrack", new Vector3(1943f, 0.01f, 24f), new Vector3(9f, 0.02f, 2.4f), M("PixelSand"), false);
        var pack = Group(race.transform, "Racers", Vector3.zero);
        for (int k = 0; k < 4; k++) Doxie(pack, "Racer " + k, new Vector3(-0.9f + k * 0.6f, 0, (k % 2) * 0.15f), 0f, 0.8f, coats[k % 4]);
        race.target = pack; race.moveBy = new Vector3(0, 0, 6f); race.bob = 0.05f; race.inTime = 3f; race.outTime = 1f;
        var bark = Gg(g, "The barker", new Vector3(1946.5f, 0, 36f), 180f, "Doxie/dx_bark", 1.2f, 1f);
        var bk = Doxie(bark.transform, "Barker", Vector3.zero, 0f, 1f, "DoxieBlack");
        bark.target = bk; bark.bob = 0.12f; bark.wobble = 6f;
        var bones = Gg(g, "The dachshund skeleton", new Vector3(1926f, 0, 38f), 90f, "Doxie/dx_skeleton", 2f, 1.2f);
        var sk = Group(bones.transform, "Bones", Vector3.zero);
        Prim(PrimitiveType.Cylinder, "Spine", sk, new Vector3(0, 0.45f, 0), new Vector3(0.06f, 0.9f, 0.06f), M("BoneWhite")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        for (int k = 0; k < 9; k++) Prim(PrimitiveType.Cylinder, "Rib", sk, new Vector3(0, 0.3f, -0.6f + k * 0.15f), new Vector3(0.3f, 0.01f, 0.03f), M("BoneWhite")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        Prim(PrimitiveType.Sphere, "Skull", sk, new Vector3(0, 0.5f, 1f), new Vector3(0.22f, 0.22f, 0.4f), M("BoneWhite"));
        foreach (float lx in new[] { -0.1f, 0.1f }) foreach (float lz in new[] { -0.7f, 0.7f }) Prim(PrimitiveType.Cube, "Leg", sk, new Vector3(lx, 0.15f, lz), new Vector3(0.04f, 0.3f, 0.04f), M("BoneWhite"));
        bones.target = sk; bones.wobble = 8f; bones.bob = 0.05f;
        Folk(g, "Frankie Furter", "#ffab91", 16, new Vector3(1935f, 0, 31f), 180f, "Doxie/dx_owner");
        // one doxie that walks the room forever
        var walker = Group(g, "Walking doxie", Vector3.zero);
        var wd = Doxie(walker, "Patrol doxie", new Vector3(1923f, 0, 30f), 0f, 1.4f, "DoxieBrown");
        var wr = Add<FirRide>(walker.gameObject); wr.kind = 1; wr.carts = new[] { wd };
        wr.route = new[] { new Vector3(1922.5f, 0, 29f), new Vector3(1922.5f, 0, 42f), new Vector3(1947f, 0, 42f), new Vector3(1947f, 0, 29f) }; wr.pace = new[] { 1f, 1f, 1f, 1f }; wr.speed = 0.8f;
        report.Add("The Dachshund Room: 8 sleepy doxies, portraits, a sausage couch, a walking doxie, the longest dachshund in the world, a hot dog costume, stairs, a race, a barker and a dachshund skeleton (6 gags)");
    }

    // =================================================================== 14 Extremely Christmas

    static void BuildExtremeChristmas(Transform root)
    {
        var g = AreaShell(root, "Extremely Christmas", 1970f, 2030f, 20f, 80f, 25f, "Snow", "GiftRed", "NightSky",
                          "EXTREMELY CHRISTMAS", new Color(0.2f, 1f, 0.3f), "too much Christmas. far too much. there's no such thing as too much. there is. this is it.", "EXTREMELY CHRISTMAS");
        // the tree: enormous
        var tc = new Vector3(2000f, 0, 56f);
        DPrim(PrimitiveType.Cylinder, "TreeTrunk", g, tc + new Vector3(0, 1.5f, 0), new Vector3(1.4f, 1.5f, 1.4f), M("Trunk"));
        for (int k = 0; k < 7; k++)
        {
            float r = 9f - k * 1.2f;
            DPrim(PrimitiveType.Sphere, "TreeTier", g, tc + new Vector3(0, 3f + k * 2.4f, 0), new Vector3(r, 3.2f, r), M("Needles"));
            int n = 14 - k;
            for (int b = 0; b < n; b++)
            {
                float a = b * Mathf.PI * 2f / n + k;
                DPrim(PrimitiveType.Sphere, "Bauble", g, tc + new Vector3(Mathf.Cos(a) * r * 0.5f, 2.6f + k * 2.4f, Mathf.Sin(a) * r * 0.5f), Vector3.one * 0.5f, Bulb(b % 3 == 0 ? "BulbRed" : b % 3 == 1 ? "BulbGold" : "BulbBlue"));
            }
        }
        var tg = Gg(g, "The tree", tc + new Vector3(0, 0, -4.8f), 180f, "Xmas/xm_tree", 2f, 2f);
        var star = Group(tg.transform, "Star", new Vector3(0, 20.6f, -4.8f));
        for (int k = 0; k < 5; k++) Prim(PrimitiveType.Cube, "Point", star, Vector3.zero, new Vector3(0.3f, 1.8f, 0.15f), M("GlowGold")).transform.localRotation = Quaternion.Euler(0, 0, k * 36f);
        tg.target = star; tg.spin = 120f; tg.wobble = 5f;
        // the train round the tree
        var tr = Group(g, "Christmas train", Vector3.zero);
        var route = new List<Vector3>();
        for (int k = 0; k < 16; k++) { float a = k * Mathf.PI / 8f; route.Add(tc + new Vector3(Mathf.Cos(a) * 11f, 0, Mathf.Sin(a) * 11f)); }
        for (int k = 0; k < 16; k++) DPrim(PrimitiveType.Cube, "Track", g, (route[k] + route[(k + 1) % 16]) / 2f + new Vector3(0, 0.02f, 0), new Vector3(1.2f, 0.04f, 4.4f), M("Dirt"), Quaternion.LookRotation(route[(k + 1) % 16] - route[k]));
        var cars = new List<Transform>();
        var trainExit = Marker(tr, "Train exit", tc + new Vector3(0, 0.05f, -13f), 180f);
        for (int c = 0; c < 3; c++)
        {
            var car = Group(tr, "Car " + c, route[0]);
            Prim(PrimitiveType.Cube, "Car", car, new Vector3(0, 0.6f, 0), new Vector3(1.2f, 0.9f, 1.8f), M(c == 0 ? "GiftRed" : c == 1 ? "GiftGreen" : "GiftGold"));
            if (c == 0) Prim(PrimitiveType.Cylinder, "Stack", car, new Vector3(0, 1.4f, 0.6f), new Vector3(0.3f, 0.4f, 0.3f), M("Black"));
            RideSeat(car, "Christmas train seat " + c, new Vector3(0, 1.05f, -0.3f), 0f, trainExit);
            cars.Add(car);
        }
        var trr = Add<FirRide>(tr.gameObject); trr.kind = 1; trr.carts = cars.ToArray(); trr.route = route.ToArray(); trr.pace = Enumerable.Repeat(1f, 16).ToArray(); trr.speed = 1.8f;
        var bell = Gg(g, "Station bell", tc + new Vector3(2.5f, 0, -13f), 180f, "Xmas/xm_train", 1.2f, 2.2f);
        DPrim(PrimitiveType.Cylinder, "BellPost", bell.transform, new Vector3(0, 1f, 0), new Vector3(0.1f, 1f, 0.1f), M("Metal"));
        var bl = Group(bell.transform, "Bell", new Vector3(0, 2f, 0));
        Prim(PrimitiveType.Sphere, "Bell", bl, new Vector3(0, -0.15f, 0), new Vector3(0.35f, 0.4f, 0.35f), M("GlowGold"));
        bell.target = bl; bell.wobble = 25f;
        // Santa's corner: a throne, the workshop, reindeer and the sleigh
        Box(g, "ThroneStep", new Vector3(1980f, 0.25f, 72f), new Vector3(4f, 0.5f, 3f), M("GiftRed"));
        Box(g, "Throne", new Vector3(1980f, 1.5f, 73.3f), new Vector3(2f, 2.4f, 0.4f), M("GlowGold"));
        Folk(g, "Santa Bones", "#e53935", 15, new Vector3(1980f, 0.5f, 72f), 180f, "Xmas/xm_santa", 1.15f);
        Gear("Santa Bones", 2, PrimitiveType.Capsule, new Vector3(0, 0.2f, -0.03f), new Vector3(0.28f, 0.22f, 0.28f), "GiftRed", new Vector3(-20, 0, 0));
        Gear("Santa Bones", 2, PrimitiveType.Sphere, new Vector3(0, -0.1f, 0.1f), new Vector3(0.3f, 0.35f, 0.12f), "CaneWhite");
        Box(g, "Workbench", new Vector3(1990f, 0.45f, 72f), new Vector3(3f, 0.9f, 1.2f), M("DeskWood"));
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Cube, "Toy", g, new Vector3(1989f + k * 0.5f, 1.05f, 72f), Vector3.one * 0.25f, M(new[] { "GiftRed", "GiftBlue", "GiftGreen", "GiftGold", "BonePink" }[k]));
        Folk(g, "Jingle", "#66bb6a", 12, new Vector3(1990f, 0, 70.6f), 180f, "Xmas/xm_elf", 0.7f);
        Gear("Jingle", 2, PrimitiveType.Capsule, new Vector3(0, 0.2f, -0.04f), new Vector3(0.22f, 0.25f, 0.22f), "GiftGreen", new Vector3(-35, 0, 0));
        var sleigh = Group(g, "Sleigh", new Vector3(2018f, 0, 70f));
        Box(sleigh, "Sleigh", new Vector3(0, 0.6f, 0), new Vector3(2f, 1f, 3f), M("GiftRed"));
        foreach (float rx in new[] { -0.9f, 0.9f }) Box(sleigh, "Runner", new Vector3(rx, 0.05f, 0), new Vector3(0.1f, 0.1f, 3.4f), M("GlowGold"), false);
        Box(sleigh, "Sack", new Vector3(0, 1.4f, -0.6f), new Vector3(1.2f, 0.8f, 1f), M("Trunk"), false);
        for (int k = 0; k < 4; k++)
        {
            var at = new Vector3(2017f + (k % 2) * 2f, 0, 66f - (k / 2) * 2.4f);
            Transform skull;
            if (k == 0) { Folk(g, "Dasher Bones", "#8d6e63", 20, at, 180f, "Xmas/xm_reindeer", 0.9f); skull = rigs["Dasher Bones"][2]; }
            else skull = Mini(g, "Reindeer " + k, at, 180f, 0.9f, "#8d6e63")[2];
            foreach (float ax in new[] { -0.12f, 0.12f }) Prim(PrimitiveType.Cube, "Antler", skull, new Vector3(ax, 0.22f, 0), new Vector3(0.04f, 0.25f, 0.04f), M("Trunk")).transform.localRotation = Quaternion.Euler(0, 0, ax > 0 ? -25 : 25);
        }
        // the snowman, the cocoa, the gingerbread house, candy canes
        Folk(g, "Snowbones", "#f5f5f5", 9, new Vector3(1984f, 0, 40f), 180f, "Xmas/xm_snowman");
        Gear("Snowbones", 2, PrimitiveType.Cylinder, new Vector3(0, 0.22f, 0), new Vector3(0.25f, 0.15f, 0.25f), "Black");
        Gear("Snowbones", 1, PrimitiveType.Sphere, new Vector3(0, 0, 0), new Vector3(0.6f, 0.6f, 0.5f), "Snow");
        var cocoa = Stall(g, "HOT COCOA", new Vector3(2020f, 0, 36f), -90f, "Trunk", "CaneWhite");
        Folk(cocoa, "Marshmallow", "#bcaaa4", 3, new Vector3(0, 0, -0.3f), 0f, "Xmas/xm_cocoa");
        Box(g, "Gingerbread", new Vector3(1980f, 1.5f, 28f), new Vector3(5f, 3f, 4f), M("Trunk"));
        Box(g, "GingerRoof", new Vector3(1980f, 3.3f, 28f), new Vector3(5.6f, 0.6f, 4.6f), M("CaneWhite"));
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Sphere, "Gumdrop", g, new Vector3(1977.8f + k * 0.62f, 3.7f, 26f), Vector3.one * 0.3f, M(new[] { "GlowRed", "GlowGreen", "GlowYellow", "GemPink" }[k % 4]));
        for (int k = 0; k < 10; k++)
        {
            var cp = new Vector3(1973f + (k % 2) * 54f, 0, 26f + (k / 2) * 12f);
            for (int s = 0; s < 6; s++) DPrim(PrimitiveType.Cylinder, "Cane", g, cp + new Vector3(0, 0.4f + s * 0.8f, 0), new Vector3(0.4f, 0.4f, 0.4f), M(s % 2 == 0 ? "CaneRed" : "CaneWhite"));
            DPrim(PrimitiveType.Sphere, "CaneTop", g, cp + new Vector3(0, 5f, 0), Vector3.one * 0.6f, M("CaneRed"));
        }
        // lights on every wall
        for (int k = 0; k < 60; k++)
        {
            float t = k / 60f;
            DPrim(PrimitiveType.Sphere, "Wall light", g, new Vector3(1970.1f, 4f + Mathf.Sin(k * 0.9f) * 0.4f, 20f + t * 60f), Vector3.one * 0.25f, Bulb(k % 3 == 0 ? "BulbRed" : k % 3 == 1 ? "BulbGold" : "BulbBlue"));
            DPrim(PrimitiveType.Sphere, "Wall light", g, new Vector3(2029.9f, 4f + Mathf.Sin(k * 0.9f) * 0.4f, 20f + t * 60f), Vector3.one * 0.25f, Bulb(k % 3 == 0 ? "BulbBlue" : k % 3 == 1 ? "BulbRed" : "BulbGold"));
        }
        // the big switch: even more lights
        var sw = Gg(g, "The big switch", new Vector3(2010f, 0, 24f), 180f, "Xmas/xm_lights", 1.4f, 2f);
        Box(sw.transform, "Switchbox", new Vector3(0, 1f, 0), new Vector3(0.8f, 1.2f, 0.4f), M("Metal"), false);
        Text("SwitchSign", sw.transform, new Vector3(0, 1.8f, -0.21f), Quaternion.identity, new Vector2(1f, 0.3f), 0.1f, "<b>MORE LIGHTS</b>").color = Color.white;
        var extra = Group(sw.transform, "Extra lights", new Vector3(0, 0, 4f));
        for (int k = 0; k < 40; k++) Prim(PrimitiveType.Sphere, "Light", extra, new Vector3(-8f + (k % 10) * 1.8f, 6f + (k / 10) * 1.2f, 0), Vector3.one * 0.35f, Bulb(k % 3 == 0 ? "BulbRed" : k % 3 == 1 ? "BulbGold" : "BulbBlue"));
        sw.target = extra; sw.hideWhenIdle = true; sw.wobble = 2f; sw.holdTime = 5f;
        // presents all the way down
        var pr = Gg(g, "A present", new Vector3(2008f, 0, 44f), 180f, "Xmas/xm_present", 1.4f, 1.4f);
        Box(pr.transform, "Present", new Vector3(0, 0.4f, 0), new Vector3(0.9f, 0.8f, 0.9f), M("GiftBlue"), false);
        var inner = Group(pr.transform, "Inside", new Vector3(0, 0.8f, 0));
        for (int k = 0; k < 4; k++) Prim(PrimitiveType.Cube, "Smaller present", inner, new Vector3(0, 0.25f + k * 0.45f, 0), Vector3.one * (0.7f - k * 0.15f), M(new[] { "GiftRed", "GiftGreen", "GiftGold", "BonePink" }[k]));
        inner.localScale = Vector3.one * 0.2f;
        pr.target = inner; pr.scaleBy = 5f; pr.spin = 90f; pr.inTime = 0.8f;
        for (int k = 0; k < 30; k++) DPrim(PrimitiveType.Cube, "Pile present", g, tc + new Vector3(Mathf.Cos(k * 0.7f) * (5f + k % 3), 0.3f, Mathf.Sin(k * 0.7f) * (5f + k % 3)), Vector3.one * (0.5f + (k % 3) * 0.2f), M(new[] { "GiftRed", "GiftGreen", "GiftGold", "GiftBlue" }[k % 4]));
        // gently swirling snow
        var snow = Group(g, "Snowfall", tc + new Vector3(0, 0, 0));
        var flakes = new List<Transform>();
        for (int k = 0; k < 60; k++)
        {
            var f = Group(snow, "Flake", new Vector3(Mathf.Cos(k * 2.4f) * (6f + k % 20), 4f + (k * 7 % 18), Mathf.Sin(k * 2.4f) * (6f + k % 20)));
            Prim(PrimitiveType.Sphere, "Flake", f, Vector3.zero, Vector3.one * 0.15f, M("Snow"));
            flakes.Add(f);
        }
        var sr = Add<FirRide>(snow.gameObject); sr.kind = 0; sr.rotor = snow; sr.axis = Vector3.up; sr.degPerSec = 4f; sr.bobbers = flakes.ToArray(); sr.bobHeight = 1.2f; sr.bobSpeed = 0.5f;
        // the carol
        var console = Console(g, "CarolConsole", new Vector3(1992f, 1.25f, 66f), Vector3.back, "<size=80%>THE CAROL</size>");
        ComboAct(console, 0, 9, "The Twelve Bones of Christmas", new[] { "Santa Bones", "Jingle", "Dasher Bones", "Snowbones" },
                 new[] { "xmas_1", "xmas_2", "xmas_3", "xmas_4", "xmas_5" }, new[] { 0, 1, 2, 3, -1 }, 3f);
        report.Add("Extremely Christmas: an enormous tree, a train round it you can ride, Santa's throne, the workshop, a sleigh and reindeer, a snowman, cocoa, a gingerbread house, ten giant candy canes, 120 wall lights, snow, a carol; 4 gags");
    }

    // =================================================================== the Cellbound room (a tribute to the 1955 cartoon by Tex Avery and Michael Lah)

    static void Stripes(Transform torso)
    {
        for (int k = 0; k < 5; k++) Prim(PrimitiveType.Cube, "Stripe", torso, new Vector3(0, -0.05f + k * 0.11f, 0), new Vector3(0.42f, 0.055f, 0.3f), M(k % 2 == 0 ? "Black" : "CaneWhite"));
    }

    static void Pose(Transform[] r, int i, float x, float y, float z) { r[i].localRotation = r[i].localRotation * Quaternion.Euler(x, y, z); }

    static void BuildCellbound(Transform root)
    {
        var g = AreaShell(root, "Cellbound", 2050f, 2090f, 20f, 50f, 5f, "Concrete", "WallDark", "Ceiling",
                          "CELLBOUND (1955)", new Color(0.95f, 0.85f, 0.5f), "a tribute room to the cartoon by Tex Avery and Michael Lah: the long way out of prison", "CELLBOUND (1955)");
        // the plaque at the door
        var pl = Group(g, "The plaque", new Vector3(2064f, 0, 23.5f));
        Box(pl, "Stand", new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.2f, 0.2f), M("DeskWood"));
        Box(pl, "Brass", new Vector3(0, 1.05f, -0.11f), new Vector3(1.3f, 0.7f, 0.02f), M("Brass"), false);
        Text("PlaqueText", pl, new Vector3(0, 1.05f, -0.125f), Face(Vector3.back), new Vector2(1.2f, 0.65f), 0.07f,
             "<b>CELLBOUND</b> (MGM, 1955)\ndirected by Tex Avery and Michael Lah\n\na convict spends years tunnelling out of prison\n...and comes up inside the warden's television set\n\n<size=80%>(press to hear more)</size>").color = new Color(0.2f, 0.12f, 0.05f);
        pl.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.4f, 1.6f, 0.4f);
        pl.gameObject.GetComponent<BoxCollider>().center = new Vector3(0, 0.8f, 0);
        var pcap = Text("Caption", pl, new Vector3(0, 2.0f, -0.12f), Face(Vector3.back), new Vector2(3f, 0.6f), 0.11f, "");
        pcap.color = Color.white;
        var ps = Add<FirSay>(pl.gameObject);
        ps.line = CaptionFor("cb_plaque"); ps.caption = pcap;
        ps.voice = Src(pl, new Vector3(0, 1f, 0), Clip("Cell/cb_plaque"), false, 1f, 1f, 10f, false);
        SetInteract(ps, "Read the plaque", 2.5f);
        // the cell block (west): a cell with its door left open, the convict, a hole, the spoon
        Box(g, "CellFloor", new Vector3(2057f, 0.005f, 42f), new Vector3(10f, 0.01f, 12f), M("Stone"), false);
        for (int k = 0; k < 21; k++)
        {
            float bx = 2052f + k * 0.5f;
            if (bx > 2059.6f && bx < 2061.4f) continue;   // the open door
            Box(g, "Bar", new Vector3(bx, 1.5f, 36f), new Vector3(0.06f, 3f, 0.06f), M("Metal"));
        }
        Box(g, "BarTop", new Vector3(2057f, 3f, 36f), new Vector3(10f, 0.1f, 0.1f), M("Metal"), false);
        var door = Group(g, "Cell door", new Vector3(2059.6f, 0, 36f));
        door.localRotation = Quaternion.Euler(0, -70f, 0);
        for (int k = 0; k < 4; k++) Prim(PrimitiveType.Cube, "DoorBar", door, new Vector3(0.2f + k * 0.45f, 1.5f, 0), new Vector3(0.06f, 3f, 0.06f), M("Metal"));
        Box(g, "CellWallE", new Vector3(2062f, 1.5f, 42f), new Vector3(0.2f, 3f, 12f), M("BrickWall"));
        Box(g, "CellWallTop", new Vector3(2057f, 4f, 36f), new Vector3(10f, 2f, 0.2f), M("BrickWall"), false);
        Box(g, "Bunk", new Vector3(2053f, 0.45f, 45f), new Vector3(1.2f, 0.15f, 2.4f), M("DeskWood"));
        DPrim(PrimitiveType.Cylinder, "Hole", g, new Vector3(2057f, 0.012f, 44f), new Vector3(1.4f, 0.01f, 1.4f), M("Black"));
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Sphere, "Dirt", g, new Vector3(2055.4f + (k % 3) * 0.3f, 0.15f, 45.5f + (k / 3) * 0.4f), new Vector3(0.8f, 0.4f, 0.7f), M("Dirt"));
        Text("Tally", g, new Vector3(2052.1f, 1.8f, 41f), Face(Vector3.right), new Vector2(3f, 1f), 0.16f, "IIII IIII IIII IIII IIII IIII IIII IIII\nIIII IIII IIII IIII IIII IIII IIII IIII\nIIII IIII IIII (days dug)").color = new Color(0.85f, 0.85f, 0.85f);
        Folk(g, "Convict 4711", "#e0e0e0", 18, new Vector3(2057.8f, 0, 42.5f), 210f, "Cell/cb_convict");
        if (rigs.TryGetValue("Convict 4711", out var cv)) { Stripes(cv[1]); Prim(PrimitiveType.Cube, "Spoon", cv[6], new Vector3(0, -0.3f, 0.05f), new Vector3(0.03f, 0.2f, 0.03f), M("Metal")); }
        DPrim(PrimitiveType.Sphere, "IronBall", g, new Vector3(2058.6f, 0.2f, 43.4f), Vector3.one * 0.4f, M("Black"));
        var spoon = Gg(g, "The spoon", new Vector3(2055f, 0, 38f), 0f, "Cell/cb_spoon", 1f, 1.4f);
        Box(spoon.transform, "Shelf", new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.9f, 0.6f), M("Black"), false);
        var sp = Group(spoon.transform, "Spoon", new Vector3(0, 1.1f, 0));
        Prim(PrimitiveType.Cube, "Handle", sp, Vector3.zero, new Vector3(0.03f, 0.18f, 0.02f), M("Metal"));
        Prim(PrimitiveType.Sphere, "Bowl", sp, new Vector3(0, 0.11f, 0), new Vector3(0.05f, 0.06f, 0.02f), M("Metal"));
        spoon.target = sp; spoon.spin = 180f; spoon.moveBy = new Vector3(0, 0.15f, 0);
        // the tunnel: crawl in at the hole, come out behind the warden's TV
        var tun = Group(g, "The tunnel", Vector3.zero);
        for (int k = 0; k < 12; k++) DPrim(PrimitiveType.Cube, "Tunnel", tun, new Vector3(2057f + k * 2f, 0.008f, 44f + k * 0.2f), new Vector3(2.1f, 0.01f, 0.7f), M("Dirt"));
        var behindTv = Marker(g, "Behind the TV", new Vector3(2080.5f, 0.05f, 46.5f), 180f);
        TeleportButton(g, "Crawl into the tunnel", new Vector3(2057f, 0.5f, 42.9f), Vector3.back, behindTv, new Vector3(0.9f, 0.3f, 0.08f));
        // the warden's living room (east)
        Box(g, "Partition", new Vector3(2066f, 2.5f, 39f), new Vector3(0.2f, 5f, 22f), M("Wall"));
        Box(g, "Wallpaper", new Vector3(2078f, 2.5f, 49.85f), new Vector3(24f, 5f, 0.1f), M("Wall"), false);
        Box(g, "Carpet", new Vector3(2078f, 0.006f, 40f), new Vector3(14f, 0.01f, 12f), M("Rug"), false);
        Box(g, "Couch", new Vector3(2078f, 0.25f, 34f), new Vector3(3.4f, 0.5f, 1f), M("Seat"));
        Box(g, "CouchBack", new Vector3(2078f, 0.7f, 33.4f), new Vector3(3.4f, 0.9f, 0.2f), M("Seat"), false);
        Box(g, "Armchair", new Vector3(2072f, 0.25f, 38f), new Vector3(1.1f, 0.5f, 1.1f), M("Curtain"));
        PendantLamp(g, new Vector3(2078f, 5f, 40f), 1.2f);
        Folk(g, "The Warden", "#90a4ae", 20, new Vector3(2072.5f, 0, 40f), 135f, "Cell/cb_warden");
        if (rigs.TryGetValue("The Warden", out var wd)) Prim(PrimitiveType.Cylinder, "Cap", wd[2], new Vector3(0, 0.14f, 0.02f), new Vector3(0.28f, 0.06f, 0.28f), M("PoliceBlue"));
        Folk(g, "Mrs Warden", "#f48fb1", 4, new Vector3(2080f, 0, 35.5f), 0f, "Cell/cb_wife");
        // the TV: a big wooden set; press the remote to change the channel
        var tv = Group(g, "The TV", new Vector3(2078f, 0, 46f));
        Box(tv, "Cabinet", new Vector3(0, 0.9f, 0), new Vector3(2.4f, 1.8f, 1.4f), M("DoorWood"));
        Box(tv, "Bezel", new Vector3(0, 1.1f, -0.71f), new Vector3(1.8f, 1.3f, 0.02f), M("Black"), false);
        foreach (float kx in new[] { 0.95f, 1.1f }) DPrim(PrimitiveType.Cylinder, "Knob", tv, new Vector3(kx - 0.05f, 0.4f, -0.72f), new Vector3(0.1f, 0.02f, 0.1f), M("Brass"), Quaternion.Euler(90, 0, 0));
        DPrim(PrimitiveType.Cylinder, "Antenna", tv, new Vector3(-0.3f, 2.2f, 0), new Vector3(0.02f, 0.45f, 0.02f), M("Metal"), Quaternion.Euler(0, 0, 25));
        DPrim(PrimitiveType.Cylinder, "Antenna", tv, new Vector3(0.3f, 2.2f, 0), new Vector3(0.02f, 0.45f, 0.02f), M("Metal"), Quaternion.Euler(0, 0, -25));
        var snow = Group(tv, "Static", new Vector3(0, 1.1f, -0.72f));
        Prim(PrimitiveType.Cube, "Snow", snow, Vector3.zero, new Vector3(1.6f, 1.1f, 0.01f), M("Mist"));
        var screen = Group(tv, "Screen", new Vector3(0, 0.55f, -0.6f));
        string[] chName = { "News", "Cowboys", "Cooking", "Dance" };
        string[] chClip = { "cb_news", "cb_cowboy", "cb_cooking", "cb_dance" };
        var chans = new GameObject[4]; var chLines = new AudioSource[4]; var chCaps = new string[4];
        for (int c = 0; c < 4; c++)
        {
            var ch = Group(screen, "Channel " + chName[c], Vector3.zero);
            Prim(PrimitiveType.Cube, "Backdrop", ch, new Vector3(0, 0.55f, -0.11f), new Vector3(1.6f, 1.1f, 0.01f), M(new[] { "InkBlue", "PixelSand", "CaneWhite", "GemPurple" }[c]));
            var r = Mini(ch, "Convict on " + chName[c], new Vector3(0, -0.05f, 0.1f), 180f, 0.55f, "#e0e0e0");
            Stripes(r[1]);
            if (c == 0) { Prim(PrimitiveType.Cube, "Desk", ch, new Vector3(0, 0.2f, -0.2f), new Vector3(1f, 0.4f, 0.1f), M("DeskWood")); Pose(r, 3, -60, 0, 0); Pose(r, 4, -60, 0, 0); Prim(PrimitiveType.Cube, "Paper", r[6], new Vector3(0, -0.28f, 0.1f), new Vector3(0.25f, 0.18f, 0.01f), M("Paper")); }
            if (c == 1) { Prim(PrimitiveType.Cylinder, "Hat", r[2], new Vector3(0, 0.15f, 0), new Vector3(0.5f, 0.03f, 0.5f), M("Trunk")); Pose(r, 4, -90, 0, 0); Pose(r, 3, 0, 0, -40); Pose(r, 7, -20, 0, 0); }
            if (c == 2) { Prim(PrimitiveType.Cylinder, "ChefHat", r[2], new Vector3(0, 0.2f, 0), new Vector3(0.25f, 0.2f, 0.25f), M("CaneWhite")); Prim(PrimitiveType.Cylinder, "Pot", ch, new Vector3(0, 0.3f, -0.2f), new Vector3(0.4f, 0.2f, 0.4f), M("Metal")); Pose(r, 4, -70, 0, 0); Pose(r, 6, -40, 0, 0); }
            if (c == 3) { Pose(r, 3, -160, 0, 0); Pose(r, 4, -160, 0, 0); Pose(r, 7, -40, 0, 0); Pose(r, 1, 0, 0, 12); }
            chans[c] = ch.gameObject;
            chLines[c] = Src(tv, new Vector3(0, 1.1f, -0.8f), Clip("Cell/" + chClip[c]), false, 1f, 1f, 14f, false);
            chCaps[c] = CaptionFor(chClip[c]);
        }
        Box(g, "SideTable", new Vector3(2074f, 0.35f, 42f), new Vector3(0.6f, 0.7f, 0.6f), M("DeskWood"));
        var remote = Group(g, "The remote", new Vector3(2074f, 0.72f, 42f));
        Prim(PrimitiveType.Cube, "Remote", remote, new Vector3(0, 0.03f, 0), new Vector3(0.12f, 0.05f, 0.3f), M("Black"));
        Prim(PrimitiveType.Cube, "Button", remote, new Vector3(0, 0.06f, 0.08f), new Vector3(0.05f, 0.02f, 0.05f), M("GlowRed"));
        remote.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.3f, 0.6f);
        Text("RemoteSign", g, new Vector3(2074f, 1.1f, 41.69f), Face(Vector3.back), new Vector2(1f, 0.2f), 0.07f, "<b>REMOTE</b> (press)").color = Color.white;
        var fc = Add<FirChannels>(remote.gameObject);
        fc.channels = chans; fc.lines = chLines; fc.captions = chCaps; fc.staticScreen = snow.gameObject;
        fc.caption = Text("TVCaption", g, new Vector3(2078f, 2.4f, 45.25f), Face(Vector3.back), new Vector2(3.2f, 0.5f), 0.12f, "");
        fc.caption.color = Color.white;
        fc.click = Src(remote, Vector3.zero, Clip("switch"), false, 1f, 1f, 6f, false);
        SetInteract(fc, "Change the channel", 2.5f);
        report.Add("Cellbound (1955): a tribute room with a plaque, the cell (open door, spoon, tally marks, the hole), a tunnel to crawl through, and the warden's living room with a TV the convict is hiding in (4 channels); 3 voiced skeletons and 1 gag");
    }

    // =================================================================== the later Station areas: shared helpers

    /// <summary>A walled area: floor, ceiling (or sky), four walls, its name on the south wall, an arrival spot and a Station stop.</summary>
    static Transform AreaShell(Transform root, string name, float x0, float x1, float z0, float z1, float h, string floor, string wall, string ceil,
                               string title, Color titleCol, string sub, string station)
    {
        var g = Group(root, name, Vector3.zero);
        float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, w = x1 - x0, d = z1 - z0;
        Box(g, "Floor", new Vector3(cx, -0.1f, cz), new Vector3(w, 0.2f, d), M(floor));
        Box(g, "Ceiling", new Vector3(cx, h + 0.1f, cz), new Vector3(w, 0.2f, d), M(ceil));
        Box(g, "WallW", new Vector3(x0 - 0.1f, h / 2, cz), new Vector3(0.2f, h, d + 0.4f), M(wall));
        Box(g, "WallE", new Vector3(x1 + 0.1f, h / 2, cz), new Vector3(0.2f, h, d + 0.4f), M(wall));
        Box(g, "WallS", new Vector3(cx, h / 2, z0 - 0.1f), new Vector3(w + 0.4f, h, 0.2f), M(wall));
        Box(g, "WallN", new Vector3(cx, h / 2, z1 + 0.1f), new Vector3(w + 0.4f, h, 0.2f), M(wall));
        var arrive = Marker(g, "Arrive", new Vector3(cx, 0.05f, z0 + 2f), 0f);
        StationStop(station, arrive, g, new Vector3(cx - 2.7f, 1.3f, z0 + 0.15f), Vector3.forward);
        float ty = Mathf.Min(h - 1.2f, 5.2f);
        Text("Title", g, new Vector3(cx, ty, z0 + 0.05f), Face(Vector3.forward), new Vector2(w * 0.8f, 1.0f), 0.7f, "<b>" + title + "</b>").color = titleCol;
        Text("Sub", g, new Vector3(cx, ty - 0.75f, z0 + 0.05f), Face(Vector3.forward), new Vector2(w * 0.8f, 0.4f), 0.2f, sub).color = Color.white;
        return g;
    }

    static void Folk(Transform parent, string who, string hex, int act, Vector3 pos, float yaw, string clip, float scale = 1f)
    {
        var face = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        GagSkeleton(parent, who, pos, yaw, scale, act, hex, clip, new[] { BoneMat(who, hex) }, pos + new Vector3(0, 2.2f * scale + 0.05f, 0) + face * 0.1f, face);
    }

    /// <summary>Places the gags in a data file whose ids start with one of the given letters.</summary>
    static int PlaceGags(Transform parent, string file, string prefixes)
    {
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/" + file);
        if (ta == null) { report.Add("problem: Data/" + file + " is missing"); return 0; }
        var gg = Group(parent, "Gags", Vector3.zero);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        int placed = 0;
        foreach (var raw in ta.text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Trim() == "" || line.StartsWith("#")) continue;
            var p = line.Split('|');
            if (p.Length < 10 || prefixes.IndexOf(p[0][0]) < 0) continue;
            Gag(gg, p[0], p[1], new Vector3(float.Parse(p[2], inv), float.Parse(p[3], inv), float.Parse(p[4], inv)), float.Parse(p[5], inv),
                p[7], p[8].Replace("\\n", "\n"), p[9].Replace("\\n", "\n"));
            placed++;
        }
        return placed;
    }

    // =================================================================== the Redemption Inn (a cheerful hotel in a very warm place)

    static void BuildInn(Transform root)
    {
        var g = AreaShell(root, "The Redemption Inn", 560f, 600f, 20f, 60f, 7f, "Floor", "InnRed", "InnBlack",
                          "THE REDEMPTION INN", new Color(1f, 0.75f, 0.2f), "everyone deserves a second chance. and a third. and a fourth. we have time.", "THE REDEMPTION INN");
        // gold trim, chandeliers, a red carpet up the middle
        Box(g, "Carpet", new Vector3(580f, 0.01f, 38f), new Vector3(3f, 0.02f, 34f), M("CaneRed"), false);
        foreach (float tz in new[] { 20.2f, 59.8f }) Box(g, "Trim", new Vector3(580f, 6.6f, tz), new Vector3(40f, 0.2f, 0.1f), M("InnGold"), false);
        foreach (var c in new[] { new Vector3(572f, 6.2f, 32f), new Vector3(588f, 6.2f, 32f), new Vector3(572f, 6.2f, 48f), new Vector3(588f, 6.2f, 48f) })
        {
            DPrim(PrimitiveType.Cylinder, "Chandelier", g, c, new Vector3(1.6f, 0.1f, 1.6f), M("InnGold"));
            for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Sphere, "Candle", g, c + new Vector3(Mathf.Cos(k * 1.05f) * 0.7f, -0.2f, Mathf.Sin(k * 1.05f) * 0.7f), Vector3.one * 0.15f, M("GlowFlame"));
        }
        // the reception desk, the bar, the lounge, the elevator
        Box(g, "Reception", new Vector3(580f, 0.55f, 41f), new Vector3(6f, 1.1f, 1f), M("InnBlack"));
        Box(g, "ReceptionTop", new Vector3(580f, 1.12f, 41f), new Vector3(6.2f, 0.06f, 1.2f), M("InnGold"), false);
        DPrim(PrimitiveType.Cylinder, "Bell", g, new Vector3(578.5f, 1.2f, 40.7f), new Vector3(0.14f, 0.06f, 0.14f), M("InnGold"));
        Box(g, "KeyRack", new Vector3(580f, 2.4f, 44.82f), new Vector3(4f, 1.2f, 0.1f), M("InnBlack"), false);
        for (int k = 0; k < 12; k++) DPrim(PrimitiveType.Cube, "Key", g, new Vector3(578.3f + (k % 6) * 0.68f, 2.1f + (k / 6) * 0.6f, 44.75f), new Vector3(0.08f, 0.2f, 0.02f), M("InnGold"));
        Box(g, "Wall behind desk", new Vector3(580f, 3.5f, 45f), new Vector3(8f, 7f, 0.2f), M("InnBlack"));
        Box(g, "Bar", new Vector3(567f, 0.55f, 48f), new Vector3(1f, 1.1f, 8f), M("InnBlack"));
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Cylinder, "Bottle", g, new Vector3(561.2f, 1.6f, 45f + k * 1.1f), new Vector3(0.12f, 0.25f, 0.12f), M(k % 2 == 0 ? "GlowRed" : "GlowGreen"));
        Box(g, "Shelf", new Vector3(561.2f, 1.3f, 48f), new Vector3(0.6f, 0.05f, 7f), M("InnGold"), false);
        Box(g, "Chaise", new Vector3(570f, 0.3f, 30f), new Vector3(1f, 0.6f, 2.2f), M("Curtain"));
        Box(g, "Elevator", new Vector3(599.85f, 1.5f, 50f), new Vector3(0.1f, 3f, 2.4f), M("InnGold"), false);
        Text("ElevatorSign", g, new Vector3(599.78f, 3.3f, 50f), Face(Vector3.left), new Vector2(2.6f, 0.4f), 0.18f, "<b>ELEVATOR</b>  <size=70%>(down only)</size>").color = Color.white;
        var cellarShed = Marker(g, "Down below", new Vector3(22f, 0.05f, -2f), 180f);
        TeleportButton(g, "Going down", new Vector3(599.8f, 1.3f, 47.9f), Vector3.left, cellarShed, new Vector3(0.8f, 0.25f, 0.08f));
        // the staff and guests
        Folk(g, "Cinder", "#ff6f3c", 23, new Vector3(582.5f, 0, 37.5f), 180f, "Inn/in_cinder");
        Folk(g, "Dial", "#b71c1c", 20, new Vector3(576f, 0, 42.5f), 180f, "Inn/in_dial");
        Folk(g, "Brimstone", "#4e342e", 11, new Vector3(565.8f, 0, 48f), 90f, "Inn/in_brimstone");
        Folk(g, "Hop", "#ffab40", 16, new Vector3(597f, 0, 52.5f), -90f, "Inn/in_hop", 0.65f);
        Folk(g, "Dusty", "#d7ccc8", 25, new Vector3(588f, 0, 28f), 0f, "Inn/in_dusty");
        Folk(g, "Lustre", "#f48fb1", 4, new Vector3(570f, 0.3f, 31.8f), 90f, "Inn/in_lustre");
        foreach (var who in new[] { "Cinder", "Dial", "Brimstone", "Hop", "Dusty", "Lustre" })
            if (rigs.TryGetValue(who, out var r)) foreach (float hx in new[] { -0.09f, 0.09f })
                Prim(PrimitiveType.Cylinder, "Horn", r[2], new Vector3(hx, 0.16f, 0.02f), new Vector3(0.04f, 0.07f, 0.04f), M("GlowRed")).transform.localRotation = Quaternion.Euler(0, 0, hx > 0 ? -20 : 20);
        if (rigs.TryGetValue("Dial", out var dr)) Prim(PrimitiveType.Cylinder, "Microphone", dr[6], new Vector3(0, -0.25f, 0.1f), new Vector3(0.07f, 0.12f, 0.07f), M("InnGold"));
        // the house number, on the stage
        Box(g, "Stage", new Vector3(580f, 0.25f, 56.5f), new Vector3(10f, 0.5f, 3f), M("Stage"));
        Text("StageSign", g, new Vector3(580f, 4.5f, 59.85f), Face(Vector3.back), new Vector2(8f, 0.5f), 0.35f, "<b>TONIGHT: THE REDEMPTION SONG</b>").color = new Color(1f, 0.75f, 0.2f);
        var console = Console(g, "InnConsole", new Vector3(586f, 1.25f, 53.5f), Vector3.back, "<size=80%>THE HOUSE NUMBER</size>");
        ComboAct(console, 5, 9, "The Redemption Song", new[] { "Cinder", "Dial", "Brimstone", "Hop" },
                 new[] { "inn_1", "inn_2", "inn_3", "inn_4", "inn_5" }, new[] { 0, 1, 2, 3, -1 }, 3.2f);
        int placed = PlaceGags(g, "more_gags.txt", "I");
        report.Add("The Redemption Inn: 6 voiced guests and staff, a musical number, " + placed + " gags, and an elevator that only goes down");
    }

    // =================================================================== the speed zone

    static void BuildSpeed(Transform root)
    {
        var g = AreaShell(root, "The Speed Zone", 620f, 680f, 20f, 90f, 20f, "PixelGreen", "PixelSky", "Sky",
                          "THE SPEED ZONE", new Color(1f, 0.85f, 0.1f), "boost pads! springs! rings! running in a straight line, very fast!", "THE SPEED ZONE");
        // the checkered track, hills, the giant (decorative) loop, the finish line
        for (int k = 0; k < 33; k++)
            DPrim(PrimitiveType.Cube, "Track", g, new Vector3(650f, 0.006f, 23f + k * 2f), new Vector3(6f, 0.01f, 2f), M(k % 2 == 0 ? "PixelSand" : "Dirt"));
        for (int k = 0; k < 8; k++)
        {
            foreach (float side in new[] { -1f, 1f })
                DPrim(PrimitiveType.Sphere, "Hill", g, new Vector3(650f + side * (18f + (k % 3) * 2f), 0, 28f + k * 8f), new Vector3(8f, 5f + (k % 3), 8f), M(k % 2 == 0 ? "PixelGreen" : "PixelGreenDark"));
        }
        for (int k = 0; k < 28; k++)
        {
            float a = k * Mathf.PI * 2f / 28f;
            DPrim(PrimitiveType.Cube, "Loop", g, new Vector3(650f + Mathf.Sin(a) * 0.01f, 5.5f + Mathf.Sin(a) * 5f, 78f + Mathf.Cos(a) * 5f), new Vector3(6f, 0.4f, 1.2f), M(k % 2 == 0 ? "GiftBlue" : "CaneWhite"), Quaternion.Euler(-a * Mathf.Rad2Deg, 0, 0));
        }
        Box(g, "FinishL", new Vector3(646.5f, 2f, 87f), new Vector3(0.3f, 4f, 0.3f), M("CaneWhite"), false);
        Box(g, "FinishR", new Vector3(653.5f, 2f, 87f), new Vector3(0.3f, 4f, 0.3f), M("CaneWhite"), false);
        for (int k = 0; k < 14; k++) DPrim(PrimitiveType.Cube, "Checker", g, new Vector3(646.75f + k * 0.5f, 3.8f, 87f), new Vector3(0.5f, 0.5f, 0.05f), M(k % 2 == 0 ? "Black" : "CaneWhite"));
        Text("Finish", g, new Vector3(650f, 4.5f, 86.95f), Face(Vector3.back), new Vector2(7f, 0.6f), 0.45f, "<b>FINISH</b>").color = Color.white;
        // boost pads (yellow arrows) and springs (red)
        var fb = Add<FirBoost>(Group(g, "Boost", new Vector3(650f, 1f, 50f)).gameObject);
        var pads = new List<Transform>(); var vel = new List<Vector3>();
        foreach (float pz in new[] { 30f, 44f, 62f })
        {
            var pd = Group(g, "Boost pad", new Vector3(650f, 0.02f, pz));
            Prim(PrimitiveType.Cube, "Pad", pd, new Vector3(0, 0.02f, 0), new Vector3(2.2f, 0.04f, 2.2f), M("GlowYellow"));
            for (int k = 0; k < 3; k++) Prim(PrimitiveType.Cube, "Arrow", pd, new Vector3(0, 0.05f, -0.6f + k * 0.6f), new Vector3(0.8f, 0.02f, 0.18f), M("CaneRed")).transform.localRotation = Quaternion.Euler(0, 45, 0);
            pads.Add(pd); vel.Add(new Vector3(0, 1.5f, 16f));
        }
        foreach (float pz in new[] { 52f, 71f })
        {
            var sp = Group(g, "Spring", new Vector3(650f, 0.02f, pz));
            Prim(PrimitiveType.Cylinder, "Coil", sp, new Vector3(0, 0.15f, 0), new Vector3(1.2f, 0.15f, 1.2f), M("Metal"));
            Prim(PrimitiveType.Cylinder, "Top", sp, new Vector3(0, 0.32f, 0), new Vector3(1.4f, 0.03f, 1.4f), M("CaneRed"));
            pads.Add(sp); vel.Add(new Vector3(0, 9.5f, 3f));
        }
        fb.pads = pads.ToArray(); fb.padVelocity = vel.ToArray();
        // rings: rows along the track, and some high up (spring height)
        var rings = new List<Transform>();
        var rg = Group(g, "Rings", Vector3.zero);
        for (int k = 0; k < 24; k++)
        {
            bool high = k >= 18;
            var rp = high ? new Vector3(650f, 4.5f + (k % 2) * 0.8f, 53f + (k - 18) * 1.2f) : new Vector3(650f + ((k / 6) % 2 == 0 ? -1.5f : 1.5f), 1.2f, 25f + k * 2.6f);
            var r = Group(rg, "Ring " + k, rp);
            for (int s2 = 0; s2 < 8; s2++)
            {
                float a = s2 * Mathf.PI / 4f;
                Prim(PrimitiveType.Cube, "Bit", r, new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f, 0), new Vector3(0.12f, 0.12f, 0.06f), M("GlowGold"));
            }
            rings.Add(r);
        }
        fb.rings = rings.ToArray();
        Box(g, "RingBoard", new Vector3(654f, 2.4f, 20.15f), new Vector3(2.4f, 0.8f, 0.04f), M("Board"), false);
        fb.counter = Text("RingCount", g, new Vector3(654f, 2.4f, 20.19f), Face(Vector3.forward), new Vector2(2.2f, 0.6f), 0.3f, "");
        fb.counter.color = new Color(1f, 0.85f, 0.2f);
        fb.sfx = Src(fb.transform, Vector3.zero, null, false, 0f, 1f, 5f, false);
        fb.boostClip = Clip("whoosh"); fb.springClip = Clip("toilet_clunk"); fb.ringClip = Clip("pickup");
        // the runners
        var wg = Group(g, "Wanderers", Vector3.zero);
        MakeWanderer(wg, "Dash", "#ff1744", new[] { new Vector3(640f, 0, 24f), new Vector3(640f, 0, 84f), new Vector3(660f, 0, 84f), new Vector3(660f, 0, 24f) },
                     new[] { 3f, 3f, 3f, 3f }, 1.1f, 0, "Speed/sp_dash", "Gotta go! Gotta go! I don't know where. I've never known where. Gotta go!");
        Folk(g, "Coach", "#ffeb3b", 20, new Vector3(645.5f, 0, 24.5f), 180f, "Speed/sp_coach");
        Folk(g, "Puffed", "#90caf9", 32, new Vector3(655.5f, 0, 85f), 180f, "Speed/sp_tired");
        int placed = PlaceGags(g, "more_gags.txt", "J");   // the J gags sit in the speed zone, platforms, NC and the circus
        report.Add("The Speed Zone: " + pads.Count + " boost pads and springs, " + rings.Count + " rings, 3 runners; " + placed + " gags across the speed zone, platforms, NC and circus");
    }

    // =================================================================== the platform world

    static void BuildPlatforms(Transform root)
    {
        var g = AreaShell(root, "The Platform World", 700f, 760f, 20f, 80f, 20f, "PixelGreen", "PixelSky", "Sky",
                          "THE PLATFORM WORLD", new Color(1f, 0.4f, 0.2f), "jump! bonk blocks! go down a tube! touch the flag!", "THE PLATFORM WORLD");
        // the course: floating platforms climbing north
        var plats = new[] { new Vector3(726f, 0.35f, 30f), new Vector3(728.5f, 0.7f, 33f), new Vector3(731f, 1.05f, 36f), new Vector3(733.5f, 1.4f, 39.5f),
                            new Vector3(731f, 1.75f, 43f), new Vector3(728f, 2.1f, 46f), new Vector3(730f, 2.45f, 49.5f), new Vector3(733f, 2.8f, 53f),
                            new Vector3(736f, 3.15f, 56.5f), new Vector3(739f, 2.8f, 60f), new Vector3(742f, 2.45f, 63.5f), new Vector3(745f, 2.1f, 67f) };
        for (int i = 0; i < plats.Length; i++)
        {
            Box(g, "Platform " + i, plats[i] - new Vector3(0, 0.2f, 0), new Vector3(2.4f, 0.4f, 2.4f), M(i % 2 == 0 ? "PixelRoof" : "PixelSand"));
            DPrim(PrimitiveType.Cube, "Grass", g, plats[i] + new Vector3(0, 0.01f, 0), new Vector3(2.4f, 0.03f, 2.4f), M("PixelGreen"));
        }
        // bonk blocks over the path: press for a coin
        foreach (var bp in new[] { new Vector3(722f, 2.4f, 27f), new Vector3(724f, 2.4f, 27f), new Vector3(733.5f, 3.6f, 39.5f), new Vector3(736f, 5.4f, 56.5f) })
        {
            var b = MovingGag(g, "Bonk block", bp, Vector3.zero, Vector3.one * 0.8f, "Plat/pl_bonk", "<b>BONK!</b> A coin! Your head will feel this tomorrow.", new Vector3(0, 1.4f, 0));
            Prim(PrimitiveType.Cube, "Block", b.transform, Vector3.zero, Vector3.one * 0.8f, M("GlowGold"));
            Text("Mark", b.transform, new Vector3(0, 0, -0.41f), Quaternion.identity, new Vector2(0.6f, 0.6f), 0.5f, "<b>!</b>").color = new Color(0.4f, 0.2f, 0f);
            var coin = Group(b.transform, "Coin", new Vector3(0, 0.6f, 0));
            Prim(PrimitiveType.Cylinder, "Coin", coin, Vector3.zero, new Vector3(0.35f, 0.03f, 0.35f), M("GlowYellow")).transform.localRotation = Quaternion.Euler(90, 0, 0);
            b.target = coin; b.hideWhenIdle = true; b.moveBy = new Vector3(0, 0.9f, 0); b.spin = 540f; b.holdTime = 1.2f;
        }
        // warp tubes (orange): two pairs
        Func<Vector3, Transform> Tube = p =>
        {
            var t = Group(g, "Warp tube", p);
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI / 5f;
                Prim(PrimitiveType.Cube, "TubeWall", t, new Vector3(Mathf.Cos(a) * 0.75f, 0.6f, Mathf.Sin(a) * 0.75f), new Vector3(0.5f, 1.2f, 0.12f), M("ChatOrange")).transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90f, 0);
            }
            Prim(PrimitiveType.Cylinder, "Rim", t, new Vector3(0, 1.25f, 0), new Vector3(1.8f, 0.08f, 1.8f), M("ChatOrange"));
            Prim(PrimitiveType.Cylinder, "Dark", t, new Vector3(0, 0.02f, 0), new Vector3(1.3f, 0.01f, 1.3f), M("Black"));
            return t;
        };
        var t1 = Tube(new Vector3(712f, 0, 27f)); var t2 = Tube(new Vector3(748f, 0, 72f));
        var t3 = Tube(new Vector3(712f, 0, 60f)); var t4 = Tube(new Vector3(748f, 0, 30f));
        foreach (var pr in new[] { new[] { t1, t2 }, new[] { t3, t4 } })
        {
            var wp = Add<FirPortal>(pr[0].gameObject);
            wp.a = pr[0]; wp.b = pr[1]; wp.radius = 0.55f;
            wp.sfx = Src(pr[0], new Vector3(0, 1f, 0), Clip("whoosh"), false, 0f, 0.8f, 5f, false);
        }
        foreach (var tt in new[] { t1, t2, t3, t4 }) Text("TubeSign", g, tt.position + new Vector3(0, 1.8f, 0), Face(Vector3.back), new Vector2(1.6f, 0.3f), 0.17f, "<b>WARP TUBE</b>").color = Color.white;
        // a cloud you can ride over the course
        var cr = Group(g, "Cloud Ride", Vector3.zero);
        var croute = new[] { new Vector3(716f, 1.5f, 34f), new Vector3(716f, 3.5f, 66f), new Vector3(718f, 3.5f, 66f), new Vector3(718f, 1.5f, 34f) };
        var cloud = Group(cr, "Cloud", croute[0]);
        foreach (var cp in new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0.1f, 0.2f), new Vector3(0, 0.2f, -0.4f) }) Prim(PrimitiveType.Sphere, "Puff", cloud, cp, new Vector3(1.4f, 0.8f, 1.4f), M("CaneWhite"));
        var cloudExit = Marker(cr, "Cloud exit", new Vector3(714f, 0.05f, 34f), 0f);
        RideSeat(cloud, "Cloud seat", new Vector3(0, 0.35f, 0), 0f, cloudExit);
        var crr = Add<FirRide>(cr.gameObject);
        crr.kind = 1; crr.carts = new[] { cloud }; crr.route = croute; crr.pace = new[] { 1f, 1f, 1f, 1f }; crr.speed = 1.6f;
        // the flagpole at the end
        Box(g, "FlagBase", new Vector3(750f, 0.25f, 76f), new Vector3(1f, 0.5f, 1f), M("Stone"));
        DPrim(PrimitiveType.Cylinder, "Pole", g, new Vector3(750f, 4.5f, 76f), new Vector3(0.12f, 4f, 0.12f), M("CaneWhite"));
        DPrim(PrimitiveType.Sphere, "PoleTop", g, new Vector3(750f, 8.6f, 76f), Vector3.one * 0.3f, M("GlowGold"));
        var fl = MovingGag(g, "Flag", new Vector3(750f, 0, 76f), new Vector3(0, 1.2f, 0), new Vector3(1.2f, 2.4f, 1.2f), "Plat/pl_flag", "<b>COURSE CLEAR!</b> You win absolutely nothing. Congratulations!", new Vector3(0, 2.6f, -1f));
        var flag = Group(fl.transform, "Cloth", new Vector3(0.6f, 7.8f, 0));
        Prim(PrimitiveType.Cube, "Cloth", flag, Vector3.zero, new Vector3(1.0f, 0.7f, 0.03f), M("GiftGreen"));
        fl.target = flag; fl.moveBy = new Vector3(0, -6.5f, 0); fl.inTime = 1.6f; fl.outTime = 1.0f;
        // the builders
        Folk(g, "Bonny", "#ff7043", 20, new Vector3(726f, 0, 24f), 180f, "Plat/pl_bonny");
        if (rigs.TryGetValue("Bonny", out var bn)) Prim(PrimitiveType.Sphere, "HardHat", bn[2], new Vector3(0, 0.1f, 0), new Vector3(0.3f, 0.18f, 0.3f), M("GlowYellow"));
        Folk(g, "Coinsworth", "#ffd740", 5, new Vector3(737f, 0, 40f), -90f, "Plat/pl_coins");
        report.Add("The Platform World: " + plats.Length + " platforms, 4 bonk blocks, 2 pairs of warp tubes, a cloud ride and a flagpole");
    }

    // =================================================================== North Carolina

    static void LongleafPine(Transform g, Vector3 at, float h)
    {
        DPrim(PrimitiveType.Cylinder, "PineTrunk", g, at + new Vector3(0, h / 2f, 0), new Vector3(0.3f, h / 2f, 0.3f), M("Trunk"));
        for (int k = 0; k < 4; k++)
            DPrim(PrimitiveType.Sphere, "PineTuft", g, at + new Vector3(Mathf.Cos(k * 1.6f) * 0.7f, h + (k % 2) * 0.4f, Mathf.Sin(k * 1.6f) * 0.7f), new Vector3(1.4f, 0.7f, 1.4f), M("Hedge"));
    }

    static void BuildNorthCarolina(Transform root)
    {
        var g = AreaShell(root, "North Carolina", 780f, 840f, 20f, 80f, 25f, "PixelGreen", "Sky", "Sky",
                          "NORTH CAROLINA", new Color(0.9f, 0.2f, 0.2f), "from the mountains to the sea (both shrunk to fit)", "NORTH CAROLINA");
        // the Blue Ridge along the west wall: three layers, bluer with distance
        for (int k = 0; k < 7; k++)
        {
            Box(g, "Ridge far", new Vector3(781.5f, 5f + (k % 3) * 1.5f, 24f + k * 8f), new Vector3(2f, 10f + (k % 3) * 3f, 10f), M("RidgeBlue3"), false);
            Box(g, "Ridge mid", new Vector3(784f, 3.5f + ((k + 1) % 3) * 1f, 26f + k * 8f), new Vector3(2f, 7f + ((k + 1) % 3) * 2f, 9f), M("RidgeBlue2"), false);
            Box(g, "Ridge near", new Vector3(786.5f, 2f + ((k + 2) % 3) * 0.7f, 22f + k * 8f), new Vector3(2f, 4f + ((k + 2) % 3) * 1.4f, 8f), M("RidgeBlue1"), false);
        }
        // the Outer Banks: sand, dunes, the ocean, the lighthouse
        Box(g, "Beach", new Vector3(828f, 0.01f, 50f), new Vector3(24f, 0.02f, 60f), M("PixelSand"), false);
        Box(g, "Ocean", new Vector3(837f, 0.05f, 50f), new Vector3(6f, 0.06f, 60f), M("Water"), false);
        for (int k = 0; k < 6; k++) DPrim(PrimitiveType.Sphere, "Dune", g, new Vector3(822f + (k % 2) * 3f, 0, 26f + k * 9f), new Vector3(5f, 1.6f, 4f), M("PixelSand"));
        var lh = Group(g, "Lighthouse", new Vector3(828f, 0, 66f));
        for (int k = 0; k < 9; k++)
            Prim(PrimitiveType.Cylinder, "Band", lh, new Vector3(0, 1f + k * 2f, 0), new Vector3(2.4f - k * 0.08f, 1f, 2.4f - k * 0.08f), M(k % 2 == 0 ? "Black" : "CaneWhite")).transform.localRotation = Quaternion.Euler(0, k * 30f, 0);
        Box(lh, "Base", new Vector3(0, 0.5f, 0), new Vector3(3.2f, 1f, 3.2f), M("Brick"), true);
        Prim(PrimitiveType.Cylinder, "Gallery", lh, new Vector3(0, 18.2f, 0), new Vector3(2.6f, 0.15f, 2.6f), M("Black"));
        Prim(PrimitiveType.Cylinder, "Lantern", lh, new Vector3(0, 19.1f, 0), new Vector3(1.3f, 0.7f, 1.3f), M("GlowWarm"));
        Prim(PrimitiveType.Sphere, "Cap", lh, new Vector3(0, 20f, 0), new Vector3(1.2f, 0.6f, 1.2f), M("Black"));
        Text("LighthouseSign", g, new Vector3(826.3f, 1.4f, 66f), Face(Vector3.left), new Vector2(2.4f, 0.5f), 0.14f, "<b>THE LIGHTHOUSE</b>\n<size=70%>tallest brick lighthouse in the country (the real one)</size>").color = Color.white;
        // the longleaf pines, and cardinals in them
        foreach (var tp in new[] { new Vector3(792f, 0, 30f), new Vector3(796f, 0, 58f), new Vector3(800f, 0, 70f), new Vector3(790f, 0, 48f), new Vector3(806f, 0, 75f), new Vector3(812f, 0, 66f) })
        {
            LongleafPine(g, tp, 9f);
            DPrim(PrimitiveType.Sphere, "Cardinal", g, tp + new Vector3(0.6f, 8.6f, -0.6f), new Vector3(0.22f, 0.2f, 0.3f), M("CaneRed"));
        }
        // the barbecue shack: a long-running argument
        var bbq = Stall(g, "BARBECUE", new Vector3(797f, 0, 40f), 180f, "CaneRed", "CaneWhite");
        Folk(bbq, "Vern", "#8d6e63", 3, new Vector3(-0.6f, 0, -0.3f), 0f, "NC/nc_vern");
        Folk(bbq, "Kay", "#ef5350", 3, new Vector3(0.6f, 0, -0.3f), 0f, "NC/nc_kay");
        DPrim(PrimitiveType.Cylinder, "Smoker", g, new Vector3(800f, 0.6f, 42.5f), new Vector3(0.9f, 0.6f, 0.9f), M("Black"));
        Text("Sauces", g, new Vector3(797f, 2.1f, 38.98f), Face(Vector3.back), new Vector2(2.3f, 0.3f), 0.1f, "EASTERN (vinegar)  vs  LEXINGTON (a bit of tomato)").color = Color.white;
        var console = Console(g, "BBQConsole", new Vector3(801.5f, 1.25f, 36.5f), Vector3.back, "<size=80%>THE GREAT DEBATE</size>");
        ComboAct(console, 0, 6, "The Barbecue Debate", new[] { "Vern", "Kay" }, new[] { "bbq_1", "bbq_2", "bbq_3", "bbq_4", "bbq_5" }, new[] { 0, 1, 0, 1, -1 }, 1.6f);
        // sweet tea
        var tea = MovingGag(g, "Sweet Tea", new Vector3(806f, 0, 34f), new Vector3(0, 0.9f, 0), new Vector3(1f, 1.8f, 1f), "NC/nc_tea", "Sweet tea. So sweet it's legally a dessert.", new Vector3(0, 2.4f, 0));
        Box(tea.transform, "Table", new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.9f), M("DeskWood"), false);
        var jar = Group(tea.transform, "Jar", new Vector3(0, 0.9f, 0));
        Prim(PrimitiveType.Cylinder, "Tea", jar, new Vector3(0, 0.25f, 0), new Vector3(0.35f, 0.25f, 0.35f), M("GlowOrange"));
        tea.target = jar; tea.wobble = 10f;
        // Kitty Hawk: a dune, and the first flight (12 seconds, 120 feet)
        DPrim(PrimitiveType.Sphere, "Kill Devil Hill", g, new Vector3(818f, 0, 30f), new Vector3(7f, 2.4f, 6f), M("PixelSand"));
        var fly = MovingGag(g, "First Flight", new Vector3(817f, 0, 30f), new Vector3(0, 1.4f, 0), new Vector3(3.4f, 1.2f, 1.6f), "NC/nc_flight", "First in flight! Twelve seconds, a hundred and twenty feet.", new Vector3(0, 3f, 0));
        var plane = Group(fly.transform, "Flyer", new Vector3(0, 1.4f, 0));
        foreach (float wy in new[] { 0f, 0.5f }) Prim(PrimitiveType.Cube, "Wing", plane, new Vector3(0, wy, 0), new Vector3(3.2f, 0.04f, 0.6f), M("Paper"));
        foreach (float sx in new[] { -1.4f, -0.5f, 0.5f, 1.4f }) Prim(PrimitiveType.Cube, "Strut", plane, new Vector3(sx, 0.25f, 0), new Vector3(0.03f, 0.5f, 0.03f), M("DeskWood"));
        Prim(PrimitiveType.Cube, "Elevator", plane, new Vector3(0, 0.25f, 1.1f), new Vector3(1.2f, 0.03f, 0.3f), M("Paper"));
        fly.target = plane; fly.moveBy = new Vector3(0, 2.5f, 36.5f); fly.inTime = 12f; fly.holdTime = 2f; fly.outTime = 1f; fly.bob = 0.15f;
        // the venus flytrap
        var ft = MovingGag(g, "Venus Flytrap", new Vector3(805f, 0, 50f), new Vector3(0, 1f, 0), new Vector3(1.4f, 2f, 1.4f), "NC/nc_flytrap", "Venus flytraps only grow wild around here. I'm very picky about my neighbours. And my lunch.", new Vector3(0, 2.8f, 0));
        DPrim(PrimitiveType.Cylinder, "Stem", ft.transform, new Vector3(0, 0.5f, 0), new Vector3(0.12f, 0.5f, 0.12f), M("GiftGreen"));
        var jaw = Group(ft.transform, "TopJaw", new Vector3(0, 1.05f, 0));
        Prim(PrimitiveType.Cube, "Lobe", jaw, new Vector3(0, 0.05f, 0.3f), new Vector3(0.9f, 0.08f, 0.6f), M("GiftGreen"));
        Prim(PrimitiveType.Cube, "Inside", jaw, new Vector3(0, 0.0f, 0.3f), new Vector3(0.85f, 0.02f, 0.55f), M("GlowRed"));
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Cube, "Tooth", jaw, new Vector3(-0.4f + k * 0.16f, 0.05f, 0.62f), new Vector3(0.03f, 0.12f, 0.03f), M("GiftGreen"));
        Prim(PrimitiveType.Cube, "BottomLobe", ft.transform, new Vector3(0, 1.0f, 0.3f), new Vector3(0.9f, 0.08f, 0.6f), M("GiftGreen"));
        jaw.localRotation = Quaternion.Euler(-60, 0, 0);
        ft.target = jaw; ft.rotateBy = new Vector3(55f, 0, 0); ft.inTime = 0.15f; ft.wobble = 4f;
        // wild ponies on the beach
        var pony = MovingGag(g, "Wild Ponies", new Vector3(822f, 0, 44f), new Vector3(0, 0.8f, 0), new Vector3(1.4f, 1.6f, 2f), "NC/nc_pony", "The wild ponies of the Outer Banks. Do not feed them. Do not pet them. Do not challenge them to a race.", new Vector3(0, 2.4f, 0));
        var horse = Group(pony.transform, "Pony", Vector3.zero);
        Prim(PrimitiveType.Cube, "Body", horse, new Vector3(0, 0.8f, 0), new Vector3(0.45f, 0.5f, 1.1f), M("Trunk"));
        Prim(PrimitiveType.Cube, "Head", horse, new Vector3(0, 1.3f, 0.6f), new Vector3(0.25f, 0.3f, 0.45f), M("Trunk"));
        foreach (float lx in new[] { -0.15f, 0.15f }) foreach (float lz in new[] { -0.4f, 0.4f }) Prim(PrimitiveType.Cube, "Leg", horse, new Vector3(lx, 0.3f, lz), new Vector3(0.1f, 0.6f, 0.1f), M("Trunk"));
        pony.target = horse; pony.moveBy = new Vector3(0, 0, 10f); pony.bob = 0.2f; pony.inTime = 1.5f; pony.outTime = 2f;
        // the people
        Folk(g, "Keeper", "#eceff1", 26, new Vector3(825.8f, 0, 63f), -90f, "NC/nc_keeper");
        Folk(g, "Surfer", "#4dd0e1", 27, new Vector3(830f, 0, 38f), -90f, "NC/nc_surfer");
        DPrim(PrimitiveType.Cube, "Surfboard", g, new Vector3(831f, 1.0f, 39f), new Vector3(0.5f, 2f, 0.08f), M("ChatOrange"), Quaternion.Euler(0, 0, 10));
        Folk(g, "Hiker", "#7cb342", 22, new Vector3(791f, 0, 40f), 90f, "NC/nc_hiker");
        if (rigs.TryGetValue("Hiker", out var hk)) Prim(PrimitiveType.Cube, "Backpack", hk[1], new Vector3(0, 0.2f, -0.18f), new Vector3(0.3f, 0.35f, 0.18f), M("CaneRed"));
        report.Add("North Carolina: the Blue Ridge, the Outer Banks, a lighthouse, longleaf pines, a barbecue debate, the first flight, a flytrap, wild ponies, sweet tea, 5 voiced skeletons");
    }

    // =================================================================== the digital circus

    static void BuildCircus(Transform root)
    {
        var g = AreaShell(root, "The Digital Circus", 860f, 900f, 20f, 60f, 14f, "Black", "CaneWhite", "CaneRed",
                          "THE DIGITAL CIRCUS", new Color(1f, 0.25f, 0.4f), "a big top inside a computer. please enjoy the show. the show is mandatory.", "THE DIGITAL CIRCUS");
        // checkerboard floor, candy-stripe walls, a ring
        for (int ix = 0; ix < 20; ix++) for (int iz = 0; iz < 20; iz++)
            if ((ix + iz) % 2 == 0) DPrim(PrimitiveType.Cube, "Check", g, new Vector3(861f + ix * 2f, 0.004f, 21f + iz * 2f), new Vector3(2f, 0.008f, 2f), M("CaneWhite"));
        for (int k = 0; k < 20; k++)
            foreach (float wx in new[] { 860.05f, 899.95f })
                DPrim(PrimitiveType.Cube, "Stripe", g, new Vector3(wx, 7f, 21f + k * 2f), new Vector3(0.02f, 14f, 1f), M("CaneRed"));
        Prim(PrimitiveType.Cylinder, "Ring", g, new Vector3(880f, 0.15f, 42f), new Vector3(12f, 0.15f, 12f), M("CaneRed"));
        Prim(PrimitiveType.Cylinder, "RingFloor", g, new Vector3(880f, 0.16f, 42f), new Vector3(11f, 0.15f, 11f), M("PixelSand"));
        // floating shapes, turning
        var shapes = Group(g, "Floating shapes", new Vector3(880f, 9f, 40f));
        var bob = new List<Transform>();
        string[] sm = { "GlowGold", "GlowGreen", "GlowRedEye", "OreBlue", "PortalPurple" };
        for (int k = 0; k < 10; k++)
        {
            var sp = Group(shapes, "Shape " + k, new Vector3(Mathf.Cos(k * 0.63f) * 8f, (k % 3) * 1.2f, Mathf.Sin(k * 0.63f) * 8f));
            Prim(k % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, "Shape", sp, Vector3.zero, Vector3.one * (0.8f + (k % 3) * 0.3f), M(sm[k % 5])).transform.localRotation = Quaternion.Euler(k * 20f, k * 35f, 0);
            bob.Add(sp);
        }
        var fr = Add<FirRide>(shapes.gameObject);
        fr.kind = 0; fr.rotor = shapes; fr.axis = Vector3.up; fr.degPerSec = 8f; fr.bobbers = bob.ToArray(); fr.bobHeight = 0.6f; fr.bobSpeed = 1.1f;
        // the performers
        Box(g, "Pedestal", new Vector3(880f, 0.5f, 43f), new Vector3(1.2f, 0.4f, 1.2f), M("GiftBlue"));
        Folk(g, "Acrobat", "#e040fb", 18, new Vector3(880f, 0.7f, 43f), 180f, "Circus/ci_acrobat");
        Folk(g, "Strongman", "#ff5252", 10, new Vector3(874f, 0.3f, 41f), 150f, "Circus/ci_strongman");
        DPrim(PrimitiveType.Cube, "DrawnWeights", g, new Vector3(874f, 1.6f, 40.2f), new Vector3(1.6f, 0.4f, 0.01f), M("Paper"));
        Text("Weights", g, new Vector3(874f, 1.6f, 40.18f), Face(Vector3.back), new Vector2(1.5f, 0.35f), 0.14f, "<color=#202020>( 500 kg ) (drawn on)</color>");
        Folk(g, "Juggler", "#69f0ae", 25, new Vector3(886f, 0.3f, 41f), 210f, "Circus/ci_juggler");
        // the mime in his box (press him: the narrator explains)
        var mime = MovingGag(g, "The Mime", new Vector3(880f, 0.3f, 36.5f), new Vector3(0, 0.95f, 0), new Vector3(0.8f, 1.9f, 0.8f), "Circus/ci_mime", "The mime would like you to know he is trapped in a box. The box is also trapped. In a circus.", new Vector3(0, 2.4f, 0));
        var mimeRig = Group(mime.transform, "Mime", Vector3.zero);
        mimeRig.localRotation = Quaternion.Euler(0, 180, 0);
        var mp = SkeletonRig(mimeRig, "Rig", BoneMat("Mime", "#f5f5f5"));
        mp[3].localRotation = Quaternion.Euler(-90, 0, -20); mp[4].localRotation = Quaternion.Euler(-90, 0, 20);
        mime.target = mimeRig; mime.wobble = 6f;
        // the ringmaster: a skeleton with a TV for a head
        Folk(g, "Pixelrot", "#ffd600", 15, new Vector3(880f, 0.2f, 50f), 180f, "Circus/ci_ringmaster");
        if (rigs.TryGetValue("Pixelrot", out var pr))
        {
            Prim(PrimitiveType.Cube, "TV", pr[2], new Vector3(0, 0.08f, 0.02f), new Vector3(0.42f, 0.34f, 0.3f), M("Black"));
            Prim(PrimitiveType.Cube, "Screen", pr[2], new Vector3(0, 0.08f, 0.175f), new Vector3(0.34f, 0.26f, 0.01f), M("GlowGreen"));
            foreach (float ex in new[] { -0.07f, 0.07f }) Prim(PrimitiveType.Cube, "Eye", pr[2], new Vector3(ex, 0.12f, 0.183f), new Vector3(0.05f, 0.05f, 0.005f), M("Black"));
            Prim(PrimitiveType.Cube, "Smile", pr[2], new Vector3(0, 0.03f, 0.183f), new Vector3(0.16f, 0.03f, 0.005f), M("Black"));
            Prim(PrimitiveType.Cylinder, "TopHat", pr[2], new Vector3(0, 0.38f, 0.02f), new Vector3(0.24f, 0.14f, 0.24f), M("Black"));
        }
        // the EXIT (it isn't), the void (it is)
        var exitTp = TeleportButton(g, "EXIT", new Vector3(880f, 1.4f, 59.85f), Vector3.back, g.Find("Arrive"), new Vector3(1.4f, 0.5f, 0.1f));
        exitTp.sfx.clip = Clip("Circus/ci_exit"); exitTp.sfx.spatialBlend = 0f;
        Box(g, "ExitDoor", new Vector3(880f, 1.5f, 59.95f), new Vector3(2f, 3f, 0.04f), M("GlowGreen"), false);
        var voidGo = Group(g, "The Void", new Vector3(860.3f, 0, 46f));
        Box(voidGo, "Doorway", new Vector3(0, 1.5f, 0), new Vector3(0.1f, 3f, 2.2f), M("Black"), false);
        Text("VoidSign", voidGo, new Vector3(0.1f, 3.3f, 0), Face(Vector3.right), new Vector2(2.4f, 0.3f), 0.16f, "<b>THE VOID</b> <size=70%>(staff only)</size>").color = new Color(0.7f, 0.7f, 0.8f);
        voidGo.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.4f, 3f, 2.2f);
        voidGo.gameObject.GetComponent<BoxCollider>().center = new Vector3(0.2f, 1.5f, 0);
        var vcap = Text("Caption", voidGo, new Vector3(0.4f, 2.4f, 0), Face(Vector3.right), new Vector2(3f, 0.6f), 0.12f, "");
        vcap.color = new Color(0.85f, 0.85f, 1f);
        var vs = Add<FirSay>(voidGo.gameObject);
        vs.line = "* The void says hi.\n* The void would like to know how your day is going."; vs.caption = vcap;
        vs.voice = Src(voidGo, new Vector3(0.3f, 1.5f, 0), Clip("Circus/ci_void"), false, 1f, 1f, 10f, false);
        SetInteract(vs, "Say hello to the void", 2.5f);
        report.Add("The Digital Circus: a TV-headed ringmaster, 4 performers, floating shapes, an EXIT that isn't, and the void");
    }

    // =================================================================== the stick-figure skits (absurd, rapid fire)

    static FirGag Skit(Transform g, string name, Vector3 pos, float yaw, string clip, string line)
    {
        var gag = MovingGag(g, name, pos, new Vector3(0, 0.95f, 0), new Vector3(1.2f, 1.9f, 1.2f), "Asdf/as_" + clip, line, new Vector3(0, 2.4f, 0));
        gag.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        return gag;
    }

    static Transform[] Stick(Transform parent, string name)
    {
        var r = Group(parent, name, Vector3.zero);
        return SkeletonRig(r, "Rig", M("BoneBlack"));
    }

    static void BuildSkits(Transform root)
    {
        var g = AreaShell(root, "The Skits", 920f, 956f, 20f, 56f, 6f, "LabWhite", "LabWhite", "LabWhite",
                          "THE SKITS", new Color(0.1f, 0.1f, 0.1f), "short. absurd. press one.", "THE SKITS");
        int n = 0;
        // 1 the magic trick: he sinks out of sight... and pops back
        var magic = Skit(g, "Skit: magic trick", new Vector3(923f, 0, 26f), 90f, "magic", "Want to see a magic trick? ...I'm still here. It wasn't a good trick.");
        var m1 = Group(magic.transform, "Performer", Vector3.zero); Stick(m1, "Stick");
        Prim(PrimitiveType.Cylinder, "TopHat", m1, new Vector3(0, 1.85f, 0), new Vector3(0.25f, 0.15f, 0.25f), M("Black"));
        magic.target = m1; magic.moveBy = new Vector3(0, -0.6f, 0); magic.inTime = 0.3f; n++;
        // 2 the potato
        var potato = Skit(g, "Skit: potato", new Vector3(923f, 0, 31f), 90f, "potato", "I'm a potato. No, really. I'm a potato now.");
        var pt = Group(potato.transform, "Potato", new Vector3(0, 0.9f, 0));
        Prim(PrimitiveType.Sphere, "Potato", pt, Vector3.zero, new Vector3(0.6f, 0.45f, 0.45f), M("Trunk"));
        Stick(potato.transform, "Stick");
        potato.target = pt; potato.scaleBy = 2.2f; potato.hideWhenIdle = true; n++;
        // 3 the spoon (it's a fork)
        var spoon = Skit(g, "Skit: spoon", new Vector3(923f, 0, 36f), 90f, "spoon", "Look, a spoon! ...that's a fork. This is the worst day of my life.");
        Stick(spoon.transform, "Stick");
        var fork = Group(spoon.transform, "Fork", new Vector3(0.4f, 1.3f, 0.3f));
        Prim(PrimitiveType.Cube, "Handle", fork, Vector3.zero, new Vector3(0.04f, 0.4f, 0.02f), M("Metal"));
        for (int k = 0; k < 3; k++) Prim(PrimitiveType.Cube, "Tine", fork, new Vector3(-0.04f + k * 0.04f, 0.27f, 0), new Vector3(0.015f, 0.15f, 0.015f), M("Metal"));
        spoon.target = fork; spoon.spin = 360f; spoon.moveBy = new Vector3(0, 0.3f, 0); n++;
        // 4 the hat (and thirty more)
        var hat = Skit(g, "Skit: hats", new Vector3(923f, 0, 41f), 90f, "hat", "I bought a new hat. And thirty more, apparently.");
        Stick(hat.transform, "Stick");
        var hats = Group(hat.transform, "Hats", new Vector3(0, 1.8f, 0));
        for (int k = 0; k < 30; k++) Prim(PrimitiveType.Cylinder, "Hat", hats, new Vector3(Mathf.Sin(k * 2.4f) * 0.8f, k * 0.12f, Mathf.Cos(k * 2.4f) * 0.8f), new Vector3(0.22f, 0.08f, 0.22f), M(k % 3 == 0 ? "CaneRed" : k % 3 == 1 ? "GiftBlue" : "Black"));
        hat.target = hats; hat.hideWhenIdle = true; hat.moveBy = new Vector3(0, 0.6f, 0); hat.spin = 90f; n++;
        // 5 DO NOT PRESS (a cake falls from the sky)
        var cake = Skit(g, "Skit: do not press", new Vector3(923f, 0, 46f), 90f, "cake", "Who ordered the cake? Nobody? It's mine now.");
        Stick(cake.transform, "Stick");
        Box(cake.transform, "ButtonPost", new Vector3(0.7f, 0.5f, 0), new Vector3(0.2f, 1f, 0.2f), M("Metal"), false);
        Prim(PrimitiveType.Cylinder, "BigRedButton", cake.transform, new Vector3(0.7f, 1.05f, 0), new Vector3(0.3f, 0.05f, 0.3f), M("CaneRed"));
        Text("DoNotPress", cake.transform, new Vector3(0.7f, 1.4f, 0), Face(Vector3.forward), new Vector2(1f, 0.25f), 0.1f, "<color=#c00000><b>DO NOT PRESS</b></color>");
        var cakeObj = Group(cake.transform, "Cake", new Vector3(0, 6f, 0.2f));
        Prim(PrimitiveType.Cylinder, "Cake", cakeObj, Vector3.zero, new Vector3(0.6f, 0.2f, 0.6f), M("BonePink"));
        Prim(PrimitiveType.Cylinder, "Candle", cakeObj, new Vector3(0, 0.3f, 0), new Vector3(0.05f, 0.12f, 0.05f), M("GlowFlame"));
        cake.target = cakeObj; cake.moveBy = new Vector3(0, -4.4f, 0); cake.inTime = 0.6f; n++;
        // 6 quack
        var duck = Skit(g, "Skit: quack", new Vector3(953f, 0, 26f), -90f, "duck", "Quack. I'm not a duck. I'm a skeleton saying quack. Quack.");
        var dk = Group(duck.transform, "Quacker", Vector3.zero); Stick(dk, "Stick");
        Prim(PrimitiveType.Cube, "Beak", dk, new Vector3(0, 1.62f, 0.15f), new Vector3(0.14f, 0.05f, 0.15f), M("GlowYellow"));
        duck.target = dk; duck.bob = 0.15f; n++;
        // 7 knock knock (a door, nobody)
        var door = Skit(g, "Skit: knock knock", new Vector3(953f, 0, 31f), -90f, "knock", "Knock knock. Who's there? Nobody. I'm a skeleton. There is literally no body.");
        Box(door.transform, "Door", new Vector3(0.9f, 1.1f, 0), new Vector3(0.9f, 2.2f, 0.1f), M("DoorWood"), false);
        var kn = Group(door.transform, "Knocker", Vector3.zero); Stick(kn, "Stick");
        door.target = kn; door.wobble = 10f; n++;
        // 8 the stairs (there are none)
        var stairs = Skit(g, "Skit: stairs", new Vector3(953f, 0, 36f), -90f, "stairs", "I fell down the stairs. What stairs? ...Exactly.");
        var fallen = Group(stairs.transform, "Faller", Vector3.zero); Stick(fallen, "Stick");
        stairs.target = fallen; stairs.rotateBy = new Vector3(-85f, 0, 0); stairs.moveBy = new Vector3(0, 0.15f, -0.6f); n++;
        // 9 the balloon (it pops him)
        var balloon = Skit(g, "Skit: balloon", new Vector3(953f, 0, 41f), -90f, "balloon", "I'm going to pop this balloon! ...Waaaaah!");
        var bl = Group(balloon.transform, "Popper", Vector3.zero); Stick(bl, "Stick");
        Prim(PrimitiveType.Sphere, "Balloon", balloon.transform, new Vector3(0.6f, 2.0f, 0), new Vector3(0.6f, 0.7f, 0.6f), M("CaneRed"));
        balloon.target = bl; balloon.moveBy = new Vector3(0, 4f, -2f); balloon.rotateBy = new Vector3(0, 0, 540f); n++;
        // 10 the sandwich (he's the filling)
        var sw = Skit(g, "Skit: sandwich", new Vector3(953f, 0, 46f), -90f, "sandwich", "I made a sandwich. ...Wait. I'm the filling.");
        Stick(sw.transform, "Stick");
        var bread = Group(sw.transform, "Bread", Vector3.zero);
        Prim(PrimitiveType.Cube, "Top", bread, new Vector3(0, 2.0f, 0), new Vector3(0.9f, 0.15f, 0.7f), M("PixelSand"));
        Prim(PrimitiveType.Cube, "Bottom", bread, new Vector3(0, 0.05f, 0), new Vector3(0.9f, 0.1f, 0.7f), M("PixelSand"));
        sw.target = bread; sw.hideWhenIdle = true; sw.scaleBy = 1.05f; n++;
        report.Add("The Skits: " + n + " stick-figure skits");
    }

    // =================================================================== the corn flakes room

    static void BuildCornFlakes(Transform root)
    {
        var g = AreaShell(root, "The Corn Flakes Room", 980f, 1004f, 20f, 44f, 6f, "CornYellow", "CornYellow", "CaneWhite",
                          "THE CORN FLAKES ROOM", new Color(0.8f, 0.4f, 0.05f), "a room dedicated entirely to jokes about corn flakes. we're not sorry.", "CORN FLAKES ROOM");
        // the giant bowl of milk, flakes floating, a giant spoon
        Prim(PrimitiveType.Cylinder, "Bowl", g, new Vector3(992f, 0.6f, 34f), new Vector3(6f, 0.6f, 6f), M("CaneWhite"));
        Prim(PrimitiveType.Cylinder, "Milk", g, new Vector3(992f, 1.15f, 34f), new Vector3(5.5f, 0.05f, 5.5f), M("Milk"));
        for (int k = 0; k < 18; k++)
            DPrim(PrimitiveType.Cube, "Flake", g, new Vector3(992f + Mathf.Cos(k * 2.3f) * (0.6f + (k % 4) * 0.55f), 1.22f, 34f + Mathf.Sin(k * 2.3f) * (0.6f + (k % 4) * 0.55f)), new Vector3(0.45f, 0.03f, 0.35f), M("GlowGold"), Quaternion.Euler(k * 7f, k * 41f, k * 5f));
        DPrim(PrimitiveType.Cylinder, "SpoonHandle", g, new Vector3(995.5f, 2.6f, 34f), new Vector3(0.2f, 2.2f, 0.2f), M("Metal"), Quaternion.Euler(0, 0, 40));
        DPrim(PrimitiveType.Sphere, "SpoonBowl", g, new Vector3(994.2f, 1.25f, 34f), new Vector3(1.2f, 0.2f, 0.9f), M("Metal"));
        Folk(g, "Floater", "#fff59d", 9, new Vector3(990.5f, 0.75f, 34f), 90f, "Corn/cf_swim", 0.6f);
        Folk(g, "Chef", "#ffffff", 28, new Vector3(983f, 0, 40f), 135f, "Corn/cf_chef");
        if (rigs.TryGetValue("Chef", out var ch)) Prim(PrimitiveType.Cylinder, "ChefHat", ch[2], new Vector3(0, 0.2f, 0), new Vector3(0.24f, 0.18f, 0.24f), M("CaneWhite"));
        // the talking cereal box
        var box = MovingGag(g, "The Cereal Box", new Vector3(1000f, 0, 40.5f), new Vector3(0, 1.2f, 0), new Vector3(1.6f, 2.4f, 0.8f), "Corn/cf_box", "I'm the cereal box. Every joke in this room is about me. I've made my peace with it.", new Vector3(0, 3f, 0));
        box.transform.localRotation = Quaternion.Euler(0, -135, 0);
        var bx = Group(box.transform, "Box", Vector3.zero);
        Prim(PrimitiveType.Cube, "Box", bx, new Vector3(0, 1.2f, 0), new Vector3(1.5f, 2.4f, 0.6f), M("CaneRed"));
        Text("Brand", bx, new Vector3(0, 1.75f, 0.31f), Face(Vector3.forward), new Vector2(1.4f, 0.4f), 0.25f, "<b>CORN FLAKES</b>").color = new Color(1f, 0.9f, 0.3f);
        foreach (float ex in new[] { -0.3f, 0.3f }) Prim(PrimitiveType.Sphere, "Eye", bx, new Vector3(ex, 1.2f, 0.31f), Vector3.one * 0.25f, M("Eye"));
        Prim(PrimitiveType.Cube, "Mouth", bx, new Vector3(0, 0.75f, 0.31f), new Vector3(0.5f, 0.08f, 0.02f), M("Black"));
        box.target = bx; box.wobble = 5f; box.bob = 0.06f;
        // nine jokes on giant flakes round the walls
        string[] jokes = {
            "Corn flakes. The only breakfast that's scared of milk.",
            "I'd tell you a corn flake joke, but it's a bit corny. And flaky. And soggy by the end.",
            "What do you call a corn flake that won't stay in the bowl? A cereal escapist.",
            "Corn flakes never crack under pressure. They just go soggy.",
            "Corn flakes are just popcorn that gave up.",
            "Me and my corn flakes have a lot in common. We both fall apart in the morning.",
            "How do corn flakes stay in shape? Daily bowling.",
            "\"Doctor, I think I'm a corn flake.\" \"Well, that's a bit flaky of you.\"",
            "The corn flake went to therapy. It had layers to work through. Well. One layer." };
        var spots = new[] { new { p = new Vector3(981.2f, 0, 24f), y = 90f }, new { p = new Vector3(981.2f, 0, 28.5f), y = 90f }, new { p = new Vector3(981.2f, 0, 33f), y = 90f },
                            new { p = new Vector3(1002.8f, 0, 24f), y = -90f }, new { p = new Vector3(1002.8f, 0, 28.5f), y = -90f }, new { p = new Vector3(1002.8f, 0, 33f), y = -90f },
                            new { p = new Vector3(986f, 0, 42.8f), y = 180f }, new { p = new Vector3(992f, 0, 42.8f), y = 180f }, new { p = new Vector3(996.5f, 0, 42.8f), y = 180f } };
        for (int i = 0; i < 9; i++)
        {
            var jk = MovingGag(g, "Corn flake joke " + (i + 1), spots[i].p, new Vector3(0, 1.1f, 0), new Vector3(1.2f, 2.2f, 0.6f), "Corn/cf_" + (i + 1), jokes[i], new Vector3(0, 2.6f, 0));
            jk.transform.localRotation = Quaternion.Euler(0, spots[i].y, 0);
            DPrim(PrimitiveType.Cylinder, "Stand", jk.transform, new Vector3(0, 0.5f, 0), new Vector3(0.08f, 0.5f, 0.08f), M("Metal"));
            var flake = Group(jk.transform, "Flake", new Vector3(0, 1.4f, 0));
            Prim(PrimitiveType.Cube, "Flake", flake, Vector3.zero, new Vector3(1.0f, 0.8f, 0.06f), M("GlowGold")).transform.localRotation = Quaternion.Euler(0, 0, 8f * (i % 3 - 1));
            Text("Number", flake, new Vector3(0, 0, 0.04f), Face(Vector3.forward), new Vector2(0.9f, 0.4f), 0.3f, "<b>#" + (i + 1) + "</b>").color = new Color(0.5f, 0.25f, 0f);
            jk.target = flake; jk.spin = 180f; jk.wobble = 6f;
        }
        report.Add("The Corn Flakes Room: 9 corn flake jokes, a talking cereal box, a chef and a swimmer in the milk");
    }

    // =================================================================== The Mystery Hut (a roadside tourist trap in the woods), reached from the Station

    static FirGag Exhibit(Transform parent, string id, Vector3 pos, float yaw, string title, string clip, string line)
    {
        var face = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        var gag = MovingGag(parent, "Exhibit " + id, pos, new Vector3(0, 0.9f, 0), new Vector3(1.0f, 1.8f, 1.0f), "Mystery/my_" + clip, line, new Vector3(0, 2.4f, 0));
        gag.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(gag.transform, "Plinth", new Vector3(0, 0.45f, 0), new Vector3(0.9f, 0.9f, 0.9f), M("Curtain"), false);
        Text("Plaque", gag.transform, new Vector3(0, 0.6f, 0.46f), Face(Vector3.forward), new Vector2(0.85f, 0.3f), 0.07f, "<b>" + title + "</b>").color = new Color(1f, 0.85f, 0.4f);
        return gag;
    }

    static void BuildMystery(Transform root)
    {
        var g = Group(root, "The Mystery Hut", Vector3.zero);
        float h = 12f;
        Box(g, "ForestFloor", new Vector3(520f, -0.1f, 42f), new Vector3(40f, 0.2f, 44f), M("PixelGreenDark"));
        Box(g, "Sky", new Vector3(520f, h + 0.1f, 42f), new Vector3(40f, 0.2f, 44f), M("NightSky"));
        foreach (var w in new[] { new { p = new Vector3(499.9f, h / 2, 42f), s = new Vector3(0.2f, h, 44.4f) }, new { p = new Vector3(540.1f, h / 2, 42f), s = new Vector3(0.2f, h, 44.4f) },
                                  new { p = new Vector3(520f, h / 2, 19.9f), s = new Vector3(40.4f, h, 0.2f) }, new { p = new Vector3(520f, h / 2, 64.1f), s = new Vector3(40.4f, h, 0.2f) } })
            Box(g, "Wall", w.p, w.s, M("NightSky"));
        Box(g, "Path", new Vector3(520f, 0.005f, 28f), new Vector3(2.4f, 0.01f, 12f), M("Dirt"), false);
        // the woods (the trees are watching), a pond
        var trees = new[] { new Vector3(503f, 0, 24f), new Vector3(507f, 0, 33f), new Vector3(502.5f, 0, 41f), new Vector3(504f, 0, 60f), new Vector3(511f, 0, 58f), new Vector3(518f, 0, 61f),
                            new Vector3(527f, 0, 60f), new Vector3(536f, 0, 61f), new Vector3(537.5f, 0, 42f), new Vector3(533f, 0, 25f), new Vector3(538f, 0, 34f), new Vector3(513f, 0, 23.5f) };
        foreach (var tp in trees) TreeAt(g, "Watching tree", tp, Mathf.Atan2(520f - tp.x, 40f - tp.z) * Mathf.Rad2Deg, 3.2f, false);
        Prim(PrimitiveType.Cylinder, "Pond", g, new Vector3(507.5f, 0.02f, 48f), new Vector3(6f, 0.02f, 5f), M("Water"));
        var arrive = Marker(g, "Arrive", new Vector3(520f, 0.05f, 22f), 0f);
        StationStop("THE MYSTERY HUT", arrive, g, new Vector3(517.3f, 1.3f, 20.15f), Vector3.forward);

        // ---- the hut
        var hut = Group(g, "Hut", Vector3.zero);
        float hh = 3.6f;
        Box(hut, "Floor", new Vector3(520f, 0.02f, 40f), new Vector3(16f, 0.04f, 12f), M("Floor2"), false);
        Box(hut, "WallW", new Vector3(512f, hh / 2, 40f), new Vector3(0.2f, hh, 12f), M("Wainscot"));
        Box(hut, "WallE", new Vector3(528f, hh / 2, 40f), new Vector3(0.2f, hh, 12f), M("Wainscot"));
        Box(hut, "WallS L", new Vector3(515.4f, hh / 2, 34f), new Vector3(6.8f, hh, 0.2f), M("Wainscot"));
        Box(hut, "WallS R", new Vector3(524.6f, hh / 2, 34f), new Vector3(6.8f, hh, 0.2f), M("Wainscot"));
        Box(hut, "WallS Top", new Vector3(520f, 3.2f, 34f), new Vector3(2.4f, 0.8f, 0.2f), M("Wainscot"));
        Box(hut, "WallN L", new Vector3(515.4f, hh / 2, 46f), new Vector3(6.8f, hh, 0.2f), M("Wainscot"));
        Box(hut, "WallN R", new Vector3(524.6f, hh / 2, 46f), new Vector3(6.8f, hh, 0.2f), M("Wainscot"));
        Box(hut, "WallN Top", new Vector3(520f, 3.2f, 46f), new Vector3(2.4f, 0.8f, 0.2f), M("Wainscot"));
        Box(hut, "Roof", new Vector3(520f, hh + 0.15f, 40f), new Vector3(17f, 0.3f, 13f), M("TrimDark"));
        Box(hut, "RoofSign", new Vector3(520f, 4.6f, 33.9f), new Vector3(8f, 1.6f, 0.1f), M("TrimDark"), false);
        Text("HutName", hut, new Vector3(520f, 4.7f, 33.84f), Face(Vector3.back), new Vector2(7.6f, 1.2f), 0.8f, "<b>MYSTERY HUT</b>").color = new Color(1f, 0.85f, 0.2f);
        Text("HutSub", hut, new Vector3(520f, 4.05f, 33.84f), Face(Vector3.back), new Vector2(7.6f, 0.3f), 0.17f, "wonders of the woods!  genuine!  (some percent)").color = Color.white;
        PendantLamp(hut, new Vector3(516f, hh, 40f), 0.6f); PendantLamp(hut, new Vector3(524f, hh, 40f), 0.6f);
        // the exhibits
        var sq = Exhibit(hut, "Squirrel", new Vector3(513.2f, 0, 36.6f), 90f, "THE SIX-LEGGED SQUIRREL", "squirrel", "Behold! The six-legged squirrel! Please ignore the tape.");
        var sqp = Group(sq.transform, "Squirrel", new Vector3(0, 0.9f, 0));
        Prim(PrimitiveType.Cube, "Body", sqp, new Vector3(0, 0.15f, 0), new Vector3(0.18f, 0.18f, 0.35f), M("Trunk"));
        Prim(PrimitiveType.Cube, "Tail", sqp, new Vector3(0, 0.3f, -0.2f), new Vector3(0.12f, 0.3f, 0.1f), M("Trunk"));
        for (int k = 0; k < 6; k++) Prim(PrimitiveType.Cube, "Leg", sqp, new Vector3(k % 2 == 0 ? -0.08f : 0.08f, 0.04f, -0.12f + (k / 2) * 0.12f), new Vector3(0.03f, 0.1f, 0.03f), M("Trunk"));
        Prim(PrimitiveType.Cube, "Tape", sqp, new Vector3(0, 0.08f, 0.0f), new Vector3(0.2f, 0.03f, 0.05f), M("CaneWhite"));
        sq.target = sqp; sq.wobble = 15f;
        var tn = Exhibit(hut, "Toenail", new Vector3(513.2f, 0, 39.8f), 90f, "BIGFOOT'S TOENAIL", "toenail", "Bigfoot's toenail! He wants it back. He can't have it.");
        var tnp = Group(tn.transform, "Toenail", new Vector3(0, 0.9f, 0));
        Prim(PrimitiveType.Cube, "Nail", tnp, new Vector3(0, 0.12f, 0), new Vector3(0.6f, 0.08f, 0.45f), M("GlowYellow")).transform.localRotation = Quaternion.Euler(10, 0, 0);
        tn.target = tnp; tn.spin = 60f;
        var rk = Exhibit(hut, "Rock", new Vector3(513.2f, 0, 43f), 90f, "A ROCK THAT LOOKS LIKE A ROCK", "rock", "A rock that looks exactly like a rock. Uncanny.");
        var rkp = Group(rk.transform, "Rock", new Vector3(0, 0.9f, 0));
        Prim(PrimitiveType.Sphere, "Rock", rkp, new Vector3(0, 0.18f, 0), new Vector3(0.45f, 0.35f, 0.4f), M("Stone"));
        rk.target = rkp; rk.spin = 90f;
        var un = Exhibit(hut, "Unicorn", new Vector3(526.8f, 0, 36.6f), -90f, "THE GENUINE UNICORN", "unicorn", "The genuine unicorn! Do not look at the tape. Do not ask about the goat.");
        var unp = Group(un.transform, "Goat", new Vector3(0, 0.9f, 0));
        Prim(PrimitiveType.Cube, "Body", unp, new Vector3(0, 0.3f, 0), new Vector3(0.3f, 0.3f, 0.6f), M("CaneWhite"));
        Prim(PrimitiveType.Cube, "Head", unp, new Vector3(0, 0.5f, 0.35f), new Vector3(0.2f, 0.22f, 0.25f), M("CaneWhite"));
        Prim(PrimitiveType.Cylinder, "Horn", unp, new Vector3(0, 0.7f, 0.38f), new Vector3(0.05f, 0.12f, 0.05f), M("GlowYellow"));
        Prim(PrimitiveType.Cube, "Tape", unp, new Vector3(0, 0.62f, 0.38f), new Vector3(0.12f, 0.03f, 0.12f), M("Paper"));
        un.target = unp; un.bob = 0.08f; un.moveBy = new Vector3(0, 0, 0.0f);
        var mr = Exhibit(hut, "Mirror", new Vector3(526.8f, 0, 39.8f), -90f, "THE MIRROR OF TRUTH", "mirror", "The mirror of truth! It shows you as you truly are: a person who paid to look at a mirror.");
        var mrp = Group(mr.transform, "Mirror", new Vector3(0, 0.9f, 0));
        Prim(PrimitiveType.Cube, "Frame", mrp, new Vector3(0, 0.5f, 0), new Vector3(0.7f, 1.0f, 0.06f), M("Brass"));
        Prim(PrimitiveType.Cube, "Glass", mrp, new Vector3(0, 0.5f, 0.035f), new Vector3(0.6f, 0.9f, 0.01f), M("Water"));
        mr.target = mrp; mr.wobble = 4f;
        var sk = Exhibit(hut, "Shrink", new Vector3(526.8f, 0, 43f), -90f, "THE SHRINKING TREE", "shrink", "The incredible shrinking tree! Please don't ask it about its feelings.");
        var skp = Group(sk.transform, "Tree", new Vector3(0, 0.9f, 0));
        Transform skm; TreeModel(skp, 1.1f, false, out skm);
        sk.target = skp; sk.scaleBy = 0.25f; sk.mouth = skm;
        var jr = Exhibit(hut, "Jar", new Vector3(515f, 0, 45.2f), 180f, "HEAD IN A JAR", "jar", "Get me out of this jar. Also, the gift shop is that way.");
        var jrp = Group(jr.transform, "Jar", new Vector3(0, 0.9f, 0));
        Prim(PrimitiveType.Cylinder, "Glass", jrp, new Vector3(0, 0.25f, 0), new Vector3(0.4f, 0.25f, 0.4f), M("Water"));
        Prim(PrimitiveType.Sphere, "Skull", jrp, new Vector3(0, 0.25f, 0), new Vector3(0.24f, 0.24f, 0.26f), M("BoneWhite"));
        jr.target = jrp; jr.wobble = 8f;
        // the bottomless pit (in the floor), with a pebble that falls forever
        Prim(PrimitiveType.Cylinder, "Pit", hut, new Vector3(520f, 0.05f, 40.5f), new Vector3(1.6f, 0.01f, 1.6f), M("Black"));
        for (int k = 0; k < 8; k++) DPrim(PrimitiveType.Cylinder, "Rail", hut, new Vector3(520f + Mathf.Cos(k * Mathf.PI / 4f) * 1.0f, 0.45f, 40.5f + Mathf.Sin(k * Mathf.PI / 4f) * 1.0f), new Vector3(0.05f, 0.45f, 0.05f), M("Brass"));
        var pit = MovingGag(hut, "Exhibit Pit", new Vector3(521.7f, 0, 39.3f), new Vector3(0, 0.6f, 0), new Vector3(0.6f, 1.2f, 0.3f), "Mystery/my_pit", "The bottomless pit. Drop something in. ...still falling. ...still falling. That'll be one dollar.", new Vector3(0, 2.2f, 0));
        Box(pit.transform, "SignPost", new Vector3(0, 0.5f, 0), new Vector3(0.06f, 1f, 0.06f), M("DeskWood"), false);
        Text("PitSign", pit.transform, new Vector3(0, 1.05f, -0.05f), Face(Vector3.back), new Vector2(0.9f, 0.3f), 0.08f, "<b>THE BOTTOMLESS PIT</b>\n<size=70%>press to drop a pebble</size>").color = Color.white;
        var pebble = Group(pit.transform, "Pebble", new Vector3(-1.7f, 0.6f, 1.2f));
        Prim(PrimitiveType.Sphere, "Stone", pebble, Vector3.zero, Vector3.one * 0.08f, M("Stone"));
        pit.target = pebble; pit.hideWhenIdle = true; pit.moveBy = new Vector3(0, -6f, 0); pit.inTime = 2.5f; pit.outTime = 0.1f;
        // the gift shop, the decoder board
        Box(hut, "GiftCounter", new Vector3(525.6f, 0.55f, 35.6f), new Vector3(0.8f, 1.1f, 2.4f), M("TrimDark"));
        for (int k = 0; k < 5; k++) DPrim(PrimitiveType.Cylinder, "Mug", hut, new Vector3(525.6f, 1.2f, 34.7f + k * 0.45f), new Vector3(0.14f, 0.1f, 0.14f), M(k % 2 == 0 ? "CaneRed" : "GiftBlue"));
        Box(hut, "DecoderBoard", new Vector3(515.4f, 2.0f, 34.12f), new Vector3(5.2f, 1.6f, 0.03f), M("Board"), false);
        Text("Decoder", hut, new Vector3(515.4f, 2.0f, 34.16f), Face(Vector3.forward), new Vector2(5f, 1.5f), 0.12f,
             "<b>A CODE IN THE WOODS?</b>\nThe signs out there are written in a shift code: every letter has moved three steps along the alphabet. Move each one back.\n" +
             "<color=#ffd060>written:</color>  D E F G H I J K L M N O P Q R S T U V W X Y Z A B C\n<color=#70ff90>means:  </color>  A B C D E F G H I J K L M N O P Q R S T U V W X Y Z").color = Color.white;
        // the secret door: a bookcase in the north wall, and a keypad nobody mentions
        var bookcase = Group(hut, "Bookcase", new Vector3(520f, 0, 46f));
        Box(bookcase, "Case", new Vector3(0, 1.4f, 0), new Vector3(2.4f, 2.8f, 0.3f), M("TrimDark"));
        for (int s2 = 0; s2 < 4; s2++) for (int b = 0; b < 8; b++)
            Prim(PrimitiveType.Cube, "Book", bookcase, new Vector3(-0.95f + b * 0.27f, 0.35f + s2 * 0.65f, -0.16f), new Vector3(0.2f, 0.45f, 0.05f), M("Book" + (1 + (b + s2) % 3)));
        var lockGo = Group(hut, "Keypad", new Vector3(522.2f, 1.35f, 45.86f));
        lockGo.localRotation = Face(Vector3.back);
        Box(lockGo, "Backing", new Vector3(0, 0.05f, 0.03f), new Vector3(0.7f, 1.05f, 0.03f), M("Board"), false);
        var disp = Text("Display", lockGo, new Vector3(0, 0.45f, -0.01f), Quaternion.identity, new Vector2(0.6f, 0.14f), 0.1f, "");
        disp.color = new Color(0.5f, 1f, 0.6f);
        Text("KeypadNote", lockGo, new Vector3(0, -0.6f, -0.01f), Quaternion.identity, new Vector2(0.9f, 0.14f), 0.06f, "(keypad? what keypad?)").color = new Color(0.7f, 0.7f, 0.7f);
        var cl = Add<FirCodeLock>(lockGo.gameObject);
        cl.code = "6142"; cl.door = bookcase.gameObject; cl.display = disp;
        cl.sfx = Src(lockGo, Vector3.zero, null, false, 1f, 1f, 10f, false);
        cl.keyClip = Clip("switch"); cl.openClip = Clip("creak"); cl.wrongClip = Clip("wrong");
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "C", "0", "OK" };
        int[] vals = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 0, 11 };
        for (int i = 0; i < 12; i++)
        {
            var kb = HB(lockGo, "Key " + keys[i], new Vector3(-0.2f + (i % 3) * 0.2f, 0.22f - (i / 3) * 0.2f, 0f), Vector3.back, new Vector3(0.16f, 0.16f, 0.05f), M(i == 11 ? "ButtonGold" : "Button"), 61, vals[i], "Press " + keys[i], "<b>" + keys[i] + "</b>", 0.08f, true);
            kb.hunt = UB(cl);
        }
        // the secret room
        var sr = Group(g, "Secret Room", Vector3.zero);
        Box(sr, "Floor", new Vector3(520f, 0.02f, 49.2f), new Vector3(10f, 0.04f, 6.4f), M("Floor2"), false);
        Box(sr, "WallW", new Vector3(515f, 1.7f, 49.2f), new Vector3(0.2f, 3.4f, 6.4f), M("WallDark"));
        Box(sr, "WallE", new Vector3(525f, 1.7f, 49.2f), new Vector3(0.2f, 3.4f, 6.4f), M("WallDark"));
        Box(sr, "WallN", new Vector3(520f, 1.7f, 52.4f), new Vector3(10.2f, 3.4f, 0.2f), M("WallDark"));
        Box(sr, "Ceiling", new Vector3(520f, 3.5f, 49.2f), new Vector3(10.2f, 0.2f, 6.6f), M("WallDark"));
        // furniture on the ceiling (gravity is optional in here)
        Box(sr, "UpsideTable", new Vector3(517.5f, 3.0f, 50.5f), new Vector3(1.2f, 0.06f, 0.8f), M("DeskWood"), false);
        foreach (float lx in new[] { -0.5f, 0.5f }) Box(sr, "TableLeg", new Vector3(517.5f + lx, 3.2f, 50.5f), new Vector3(0.06f, 0.4f, 0.06f), M("DeskWood"), false);
        Box(sr, "UpsideChair", new Vector3(519.2f, 3.15f, 50.5f), new Vector3(0.45f, 0.5f, 0.45f), M("Curtain"), false);
        Text("Gravity", sr, new Vector3(520f, 2.2f, 52.28f), Face(Vector3.back), new Vector2(5f, 0.3f), 0.14f, "gravity in this room is a suggestion").color = new Color(0.7f, 1f, 0.7f);
        var floaty = Group(sr, "Floating books", Vector3.zero);
        var books = new List<Transform>();
        for (int k = 0; k < 5; k++)
        {
            var bk = Group(floaty, "Book " + k, new Vector3(521.5f + (k % 3) * 0.7f, 1.4f + (k % 2) * 0.5f, 48.2f + k * 0.5f));
            Prim(PrimitiveType.Cube, "Cover", bk, Vector3.zero, new Vector3(0.3f, 0.06f, 0.4f), M("Book" + (1 + k % 3))).transform.localRotation = Quaternion.Euler(k * 20f, k * 40f, 0);
            books.Add(bk);
        }
        var fr = Add<FirRide>(floaty.gameObject);
        fr.kind = 0; fr.bobbers = books.ToArray(); fr.bobHeight = 0.25f; fr.bobSpeed = 1.3f;
        foreach (var it in new[] { new { name = "Journal", p = new Vector3(518f, 0, 51.6f), clip = "journal", line = "\"Day one. The trees are watching me. Day two. The trees are still watching me. Day three. I have started watching the trees back. It is a stalemate.\"" },
                                   new { name = "Weirdness Detector", p = new Vector3(522.5f, 0, 51.6f), clip = "detector", line = "BEEP. BEEP. Weirdness level: yes." } })
        {
            var go = Group(sr, it.name, it.p);
            Box(go, "Pedestal", new Vector3(0, 0.5f, 0), new Vector3(0.6f, 1.0f, 0.6f), M("TrimDark"));
            if (it.name == "Journal") Prim(PrimitiveType.Cube, "Book", go, new Vector3(0, 1.05f, 0), new Vector3(0.35f, 0.08f, 0.45f), M("Curtain"));
            else { Prim(PrimitiveType.Cube, "Box", go, new Vector3(0, 1.15f, 0), new Vector3(0.35f, 0.25f, 0.25f), M("Metal")); Prim(PrimitiveType.Sphere, "Light", go, new Vector3(0, 1.35f, 0), Vector3.one * 0.1f, M("GlowRedEye")); }
            go.gameObject.AddComponent<BoxCollider>().size = new Vector3(0.7f, 1.4f, 0.7f);
            go.gameObject.GetComponent<BoxCollider>().center = new Vector3(0, 0.7f, 0);
            var cap = Text("Caption", go, new Vector3(0, 2.0f, 0), Face(Vector3.back), new Vector2(3.2f, 0.6f), 0.1f, "");
            cap.color = new Color(1f, 1f, 0.75f);
            var say = Add<FirSay>(go.gameObject);
            say.line = it.line; say.caption = cap; say.showFor = it.name == "Journal" ? 12f : 5f;
            say.voice = Src(go, new Vector3(0, 1.1f, 0), Clip("Mystery/my_" + it.clip), false, 1f, 1f, 10f, false);
            SetInteract(say, it.name == "Journal" ? "Read the journal" : "Switch on the detector", 2.5f);
        }

        // ---- the people
        var folk = new[] { new { who = "Marrowby", hex = "#c9a227", act = 26, p = new Vector3(517.2f, 0, 36.4f), yaw = 180f, clip = "marrowby" },
                           new { who = "Pip", hex = "#80cbc4", act = 3, p = new Vector3(526.6f, 0, 35.6f), yaw = -90f, clip = "pip" },
                           new { who = "Dad", hex = "#bcaaa4", act = 5, p = new Vector3(523.6f, 0, 31.4f), yaw = 180f, clip = "dad" },
                           new { who = "Junior", hex = "#ffcc80", act = 16, p = new Vector3(522.4f, 0, 31.0f), yaw = 180f, clip = "kid" },
                           new { who = "Lumberjack", hex = "#a1887f", act = 10, p = new Vector3(510f, 0, 30f), yaw = 90f, clip = "lumberjack" } };
        foreach (var f in folk)
        {
            var face = Quaternion.Euler(0, f.yaw, 0) * Vector3.forward;
            GagSkeleton(g, f.who, f.p, f.yaw, f.who == "Junior" ? 0.65f : 1f, f.act, f.hex, "Mystery/my_" + f.clip, new[] { BoneMat(f.who, f.hex) },
                        f.p + new Vector3(0, f.who == "Junior" ? 1.5f : 2.25f, 0) + face * 0.1f, face);
        }
        if (rigs.TryGetValue("Marrowby", out var mb))
        {
            Prim(PrimitiveType.Cylinder, "Bowler", mb[2], new Vector3(0, 0.15f, 0), new Vector3(0.24f, 0.08f, 0.24f), M("Black"));
            Prim(PrimitiveType.Cylinder, "Brim", mb[2], new Vector3(0, 0.08f, 0), new Vector3(0.36f, 0.01f, 0.36f), M("Black"));
            Prim(PrimitiveType.Cylinder, "Cane", mb[6], new Vector3(0, -0.45f, 0.05f), new Vector3(0.04f, 0.45f, 0.04f), M("Black"));
        }
        if (rigs.TryGetValue("Pip", out var pp)) Prim(PrimitiveType.Cylinder, "Cap", pp[2], new Vector3(0, 0.13f, 0), new Vector3(0.26f, 0.06f, 0.26f), M("GiftGreen"));
        if (rigs.TryGetValue("Dad", out var dd)) Prim(PrimitiveType.Cube, "Camera", dd[1], new Vector3(0, 0.15f, 0.15f), new Vector3(0.18f, 0.12f, 0.08f), M("Black"));
        if (rigs.TryGetValue("Lumberjack", out var lj)) Prim(PrimitiveType.Cube, "Plaid", lj[1], new Vector3(0, 0.28f, 0.01f), new Vector3(0.32f, 0.34f, 0.2f), M("CaneRed"));

        // ---- the woods: cipher signs, a gnome in a bush, a lake monster
        var cipher = new[] { new { p = new Vector3(505f, 0, 27.5f), yaw = 90f, t = "1 OF 4\nWKH ILUVW GLJLW LV VLA" },
                             new { p = new Vector3(535.5f, 0, 29.5f), yaw = -90f, t = "2 OF 4\nVHFRQG: RQH" },
                             new { p = new Vector3(504.5f, 0, 55.5f), yaw = 90f, t = "3 OF 4\nWKLUG: IRXU" },
                             new { p = new Vector3(535.5f, 0, 54.5f), yaw = -90f, t = "4 OF 4\nIRXUWK: WZR" } };
        foreach (var c in cipher)
        {
            var cg = Group(g, "Cipher sign", c.p);
            cg.localRotation = Quaternion.Euler(0, c.yaw, 0);
            Box(cg, "Post", new Vector3(0, 0.7f, 0), new Vector3(0.1f, 1.4f, 0.1f), M("Trunk"));
            Box(cg, "Board", new Vector3(0, 1.5f, 0), new Vector3(1.6f, 0.7f, 0.06f), M("DeskWood"), false);
            Text("Cipher", cg, new Vector3(0, 1.5f, 0.035f), Face(Vector3.forward), new Vector2(1.5f, 0.6f), 0.13f, "<b>" + c.t + "</b>").color = new Color(0.15f, 0.1f, 0.05f);
        }
        var gn = MovingGag(g, "The Gnome", new Vector3(532.5f, 0, 47f), new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.2f, 1.4f), "Mystery/my_gnome", "You didn't see me. I'm a garden ornament. Garden ornaments don't talk. Bye.", new Vector3(0, 2f, 0));
        gn.transform.localRotation = Quaternion.Euler(0, -90, 0);
        foreach (var bp in new[] { new Vector3(-0.4f, 0.4f, 0), new Vector3(0.4f, 0.45f, 0.1f), new Vector3(0, 0.6f, -0.3f) }) Prim(PrimitiveType.Sphere, "Bush", gn.transform, bp, Vector3.one * 0.9f, M("Hedge"));
        var gnome = Group(gn.transform, "Gnome", new Vector3(0, 0.2f, 0.45f));
        Prim(PrimitiveType.Cube, "Body", gnome, new Vector3(0, 0.15f, 0), new Vector3(0.2f, 0.3f, 0.2f), M("GiftBlue"));
        Prim(PrimitiveType.Sphere, "Face", gnome, new Vector3(0, 0.37f, 0.02f), Vector3.one * 0.16f, M("PixelSand"));
        Prim(PrimitiveType.Cube, "Beard", gnome, new Vector3(0, 0.3f, 0.08f), new Vector3(0.14f, 0.12f, 0.04f), M("CaneWhite"));
        Prim(PrimitiveType.Cylinder, "Hat", gnome, new Vector3(0, 0.52f, 0), new Vector3(0.18f, 0.12f, 0.18f), M("CaneRed"));
        gn.target = gnome; gn.hideWhenIdle = true; gn.moveBy = new Vector3(0, 0.55f, 0.1f); gn.wobble = 8f;
        var lk = MovingGag(g, "The Lake Monster", new Vector3(507.5f, 0, 48f), new Vector3(0, 0.3f, 2.8f), new Vector3(2f, 0.6f, 0.6f), "Mystery/my_lake", "The lake monster! It's a cardboard cutout. Or is it? (It is.)", new Vector3(0, 3.6f, 0));
        var monster = Group(lk.transform, "Cutout", new Vector3(0, -2.2f, 0));
        Prim(PrimitiveType.Cube, "Neck", monster, new Vector3(0, 1.2f, 0), new Vector3(0.6f, 2.4f, 0.04f), M("Cardboard"));
        Prim(PrimitiveType.Cube, "Head", monster, new Vector3(0.35f, 2.5f, 0), new Vector3(1.0f, 0.5f, 0.04f), M("Cardboard"));
        Prim(PrimitiveType.Sphere, "Eye", monster, new Vector3(0.6f, 2.6f, 0.03f), Vector3.one * 0.12f, M("GlowRedEye"));
        Prim(PrimitiveType.Cube, "Stick", monster, new Vector3(0, 0.6f, -0.05f), new Vector3(0.06f, 1.2f, 0.06f), M("DeskWood"));
        lk.target = monster; lk.moveBy = new Vector3(0, 2.2f, 0); lk.wobble = 5f; lk.inTime = 1.2f; lk.outTime = 1.0f;
        report.Add("The Mystery Hut: 8 exhibits, 4 cipher signs (code 6142), a secret room, 5 voiced skeletons, a gnome and a lake monster");
    }

    // =================================================================== The Cold North (meme parodies), reached from the Station

    /// <summary>A gag whose prop (built under the returned object by the caller) moves, turns and comes back, with a voiced, captioned line.</summary>
    static FirGag MovingGag(Transform parent, string name, Vector3 pos, Vector3 colCenter, Vector3 colSize, string clip, string line, Vector3 bubblePos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var col = go.AddComponent<BoxCollider>(); col.center = colCenter; col.size = colSize;
        var gag = Add<FirGag>(go);
        var c = Clip(clip);
        gag.line = line;
        gag.voice = Src(go.transform, colCenter, c, false, 1f, 1f, 25f, false);
        gag.holdTime = Mathf.Max(1.5f, c != null ? c.length : 2f);
        gag.bubble = Bubble(go.transform, bubblePos);
        gag.bubbleObject = gag.bubble.gameObject; gag.bubbleTransform = gag.bubble.transform;
        return gag;
    }

    static void NorthSkeleton(Transform parent, string who, string hex, int act, Vector3 pos, float yaw, string clip, string hat)
    {
        var face = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        GagSkeleton(parent, who, pos, yaw, 1f, act, hex, "North/nt_" + clip, new[] { BoneMat(who, hex) }, pos + new Vector3(0, 2.25f, 0) + face * 0.1f, face);
        if (!rigs.TryGetValue(who, out var r)) return;
        if (hat == "helmet")
        {
            Prim(PrimitiveType.Sphere, "Helmet", r[2], new Vector3(0, 0.1f, 0), new Vector3(0.3f, 0.2f, 0.3f), M("Metal"));
            foreach (float hx in new[] { -0.17f, 0.17f }) Prim(PrimitiveType.Cylinder, "Horn", r[2], new Vector3(hx, 0.17f, 0), new Vector3(0.05f, 0.1f, 0.05f), M("CaneWhite")).transform.localRotation = Quaternion.Euler(0, 0, hx > 0 ? -35 : 35);
        }
        else if (hat == "bucket")
            Prim(PrimitiveType.Cylinder, "Bucket", r[2], new Vector3(0, 0.02f, 0.01f), new Vector3(0.3f, 0.16f, 0.3f), M("Metal"));
        else if (hat == "hood")
            Prim(PrimitiveType.Sphere, "Hood", r[2], new Vector3(0, 0.06f, -0.03f), new Vector3(0.3f, 0.28f, 0.3f), M("Trunk"));
    }

    static void BuildNorth(Transform root)
    {
        var g = Group(root, "The Cold North", Vector3.zero);
        float h = 16f;
        Box(g, "Snow", new Vector3(458f, -0.1f, 40f), new Vector3(36f, 0.2f, 40f), M("Snow"));
        Box(g, "Sky", new Vector3(458f, h + 0.1f, 40f), new Vector3(36f, 0.2f, 40f), M("Sky"));
        foreach (var w in new[] { new { p = new Vector3(439.9f, h / 2, 40f), s = new Vector3(0.2f, h, 40.4f) }, new { p = new Vector3(476.1f, h / 2, 40f), s = new Vector3(0.2f, h, 40.4f) },
                                  new { p = new Vector3(458f, h / 2, 19.9f), s = new Vector3(36.4f, h, 0.2f) }, new { p = new Vector3(458f, h / 2, 60.1f), s = new Vector3(36.4f, h, 0.2f) } })
            Box(g, "Wall", w.p, w.s, M("PixelSky"));
        // the mountain along the north, with a snowy top; the ledge the cheese sits on
        for (int k = 0; k < 6; k++)
        {
            float mx = 443f + k * 6f, mh = 7f + (k * 5) % 6;
            Box(g, "Mountain", new Vector3(mx, mh / 2f, 55.5f), new Vector3(6.2f, mh, 9f), M("Stone"));
            Box(g, "Snowcap", new Vector3(mx, mh + 0.3f, 55.5f), new Vector3(5f, 0.6f, 7f), M("Snow"), false);
        }
        Box(g, "Ledge", new Vector3(447f, 1.5f, 50.5f), new Vector3(4f, 3f, 1.4f), M("Stone"));
        // the road loop, and pine trees along it
        foreach (var r in new[] { new { p = new Vector3(458f, 0.005f, 24f), s = new Vector3(30f, 0.01f, 2.4f) }, new { p = new Vector3(458f, 0.005f, 40f), s = new Vector3(30f, 0.01f, 2.4f) },
                                  new { p = new Vector3(444f, 0.005f, 32f), s = new Vector3(2.4f, 0.01f, 18.4f) }, new { p = new Vector3(472f, 0.005f, 32f), s = new Vector3(2.4f, 0.01f, 18.4f) } })
            Box(g, "Road", r.p, r.s, M("Dirt"), false);
        foreach (var tp in new[] { new Vector3(441.5f, 0, 30f), new Vector3(441.5f, 0, 38f), new Vector3(474.8f, 0, 26f), new Vector3(474.8f, 0, 44f), new Vector3(452f, 0, 46.5f) })
            TreeAt(g, "Pine", tp, (tp.x * 37f + tp.z * 11f) % 360f, 2.6f, false);
        var arrive = Marker(g, "Arrive", new Vector3(447f, 0.05f, 21.8f), 0f);
        StationStop("THE COLD NORTH", arrive, g, new Vector3(442.6f, 1.3f, 20.15f), Vector3.forward);
        Text("Title", g, new Vector3(458f, 7f, 20.05f), Face(Vector3.forward), new Vector2(14f, 1.1f), 0.8f, "<b>THE COLD NORTH</b>").color = new Color(0.85f, 0.9f, 1f);
        Text("Sub", g, new Vector3(458f, 6.1f, 20.05f), Face(Vector3.forward), new Vector2(14f, 0.4f), 0.22f, "the land of cold roads, cheese wheels and knees that have seen things").color = Color.white;

        // ---- the cart: round the road, with a passenger who has some news
        var cartRide = Group(g, "Cart Ride", Vector3.zero);
        var route = new[] { new Vector3(444f, 0, 24f), new Vector3(472f, 0, 24f), new Vector3(472f, 0, 40f), new Vector3(444f, 0, 40f) };
        var cart = Group(cartRide, "Cart", route[0]);
        Box(cart, "Bed", new Vector3(0, 0.6f, 0), new Vector3(1.6f, 0.2f, 2.6f), M("DeskWood"), false);
        foreach (float sx in new[] { -0.8f, 0.8f }) Box(cart, "Side", new Vector3(sx, 0.85f, 0), new Vector3(0.08f, 0.4f, 2.6f), M("DeskWood"), false);
        foreach (float wx in new[] { -0.85f, 0.85f }) foreach (float wz in new[] { -0.9f, 0.9f })
            Prim(PrimitiveType.Cylinder, "Wheel", cart, new Vector3(wx, 0.4f, wz), new Vector3(0.8f, 0.05f, 0.8f), M("Trunk")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        var cartExit = Marker(cartRide, "Cart exit", new Vector3(447f, 0.05f, 22.2f), 0f);
        RideSeat(cart, "Cart seat 0", new Vector3(-0.4f, 0.7f, 0.6f), 180f, cartExit);
        RideSeat(cart, "Cart seat 1", new Vector3(0.4f, 0.7f, 0.6f), 180f, cartExit);
        NorthSkeleton(cart, "Passenger", "#a1887f", 3, new Vector3(0, 0.7f, -0.7f), 0f, "passenger", "hood");
        var cr = Add<FirRide>(cartRide.gameObject);
        cr.kind = 1; cr.carts = new[] { cart }; cr.route = route; cr.pace = new[] { 1f, 1f, 1f, 1f }; cr.speed = 1.2f;

        // ---- the guards, the bucket shopkeeper
        NorthSkeleton(g, "Kneecap", "#b0bec5", 5, new Vector3(450f, 0, 21.6f), 0f, "guard1", "helmet");
        NorthSkeleton(g, "Rollguard", "#90a4ae", 20, new Vector3(466f, 0, 21.6f), 0f, "guard2", "helmet");
        NorthSkeleton(g, "Snoot", "#cfd8dc", 26, new Vector3(474.6f, 0, 33f), -90f, "guard3", "helmet");
        var stall = Stall(g, "GENERAL GOODS", new Vector3(458f, 0, 44.5f), 180f, "Stone", "Snow");
        NorthSkeleton(stall, "Bucket", "#8d6e63", 29, new Vector3(0, 0, -0.3f), 0f, "bucket", "bucket");

        // ---- the shout: a tree goes flying
        var shout = MovingGag(g, "The Shout", new Vector3(451f, 0, 32f), new Vector3(0, 0.95f, 0), new Vector3(0.7f, 1.9f, 0.6f), "North/nt_shout", "<b>FIR! ROH! DAH!</b>", new Vector3(0, 2.5f, 0));
        var shouter = Group(shout.transform, "Shouter", Vector3.zero);
        shouter.localRotation = Quaternion.Euler(0, 90, 0);
        SkeletonRig(shouter, "Rig", BoneMat("Shouty", "#ffccbc"));
        var flying = Group(shout.transform, "Flying tree", new Vector3(4f, 0, 0));
        Transform fm; TreeModel(flying, 2.4f, false, out fm);
        shout.target = flying; shout.moveBy = new Vector3(16f, 7f, 0); shout.rotateBy = new Vector3(0, 0, -300f);
        shout.inTime = 0.5f; shout.holdTime = 2.0f; shout.outTime = 1.2f;
        // ---- the giant: a skeleton goes into orbit
        var giantGag = MovingGag(g, "The Giant", new Vector3(464f, 0, 32f), new Vector3(0, 2.2f, 0), new Vector3(1.6f, 4.4f, 1.2f), "North/nt_launch", "Wheeeeeeee! I can see my house from here!", new Vector3(-1.5f, 3.2f, 0));
        Box(giantGag.transform, "GiantLegs", new Vector3(0, 1.2f, 0), new Vector3(1.2f, 2.4f, 0.8f), M("Trunk"), false);
        Box(giantGag.transform, "GiantBody", new Vector3(0, 3.1f, 0), new Vector3(1.6f, 1.6f, 1.0f), M("Stone"), false);
        Box(giantGag.transform, "GiantHead", new Vector3(0, 4.25f, 0), new Vector3(0.8f, 0.7f, 0.8f), M("PixelSand"), false);
        Box(giantGag.transform, "GiantClub", new Vector3(-1.1f, 2.6f, 0.3f), new Vector3(0.35f, 2.2f, 0.35f), M("Trunk"), false);
        var flyer = Group(giantGag.transform, "Flying skeleton", new Vector3(-2.2f, 0, 0));
        flyer.localRotation = Quaternion.Euler(0, 90, 0);
        SkeletonRig(flyer, "Rig", BoneMat("Launched", "#fff9c4"));
        giantGag.target = flyer; giantGag.moveBy = new Vector3(-2f, 24f, 0); giantGag.rotateBy = new Vector3(720f, 0, 0);
        giantGag.inTime = 0.6f; giantGag.holdTime = 2.5f; giantGag.outTime = 1.0f;
        // ---- the cheese
        var cheese = MovingGag(g, "The Cheese", new Vector3(447f, 3f, 50.2f), new Vector3(0, 0.3f, 0), new Vector3(3.6f, 0.8f, 1.2f), "North/nt_cheese", "And the cheese wheels roll down the mountain. As is tradition.", new Vector3(0, 1.8f, -1.5f));
        var wheels = Group(cheese.transform, "Wheels", Vector3.zero);
        for (int k = 0; k < 6; k++)
            Prim(PrimitiveType.Cylinder, "CheeseWheel", wheels, new Vector3(-1.4f + k * 0.56f, 0.25f, 0), new Vector3(0.5f, 0.12f, 0.5f), M("GlowYellow")).transform.localRotation = Quaternion.Euler(0, 0, 90);
        cheese.target = wheels; cheese.moveBy = new Vector3(0, -2.75f, -10f); cheese.rotateBy = new Vector3(-900f, 0, 0);
        cheese.inTime = 1.6f; cheese.outTime = 0.6f;
        // ---- the horse that climbs the cliff
        var horseGag = MovingGag(g, "The Horse", new Vector3(470f, 0, 49.4f), new Vector3(0, 0.8f, 0), new Vector3(0.8f, 1.6f, 1.6f), "North/nt_horse", "The horse has decided that the mountain is a road. Nobody can stop the horse.", new Vector3(0, 2.6f, -1.2f));
        var horse = Group(horseGag.transform, "Horse", Vector3.zero);
        Prim(PrimitiveType.Cube, "Body", horse, new Vector3(0, 1.0f, 0), new Vector3(0.5f, 0.6f, 1.4f), M("Trunk"));
        Prim(PrimitiveType.Cube, "Neck", horse, new Vector3(0, 1.45f, 0.6f), new Vector3(0.3f, 0.7f, 0.3f), M("Trunk")).transform.localRotation = Quaternion.Euler(25, 0, 0);
        Prim(PrimitiveType.Cube, "Head", horse, new Vector3(0, 1.8f, 0.85f), new Vector3(0.28f, 0.3f, 0.55f), M("Trunk"));
        foreach (float lx in new[] { -0.17f, 0.17f }) foreach (float lz in new[] { -0.5f, 0.5f })
            Prim(PrimitiveType.Cube, "Leg", horse, new Vector3(lx, 0.35f, lz), new Vector3(0.12f, 0.7f, 0.12f), M("Trunk"));
        horseGag.target = horse; horseGag.moveBy = new Vector3(0, 9f, 0.4f); horseGag.rotateBy = new Vector3(-85f, 0, 0); horseGag.bob = 0.12f;
        horseGag.inTime = 3.0f; horseGag.outTime = 2.0f;
        // ---- a sign
        var gg = Group(g, "Gags", Vector3.zero);
        Gag(gg, "N1", "sign", new Vector3(442.2f, 0, 33f), 90f, "Dragons not included. The trees, however, are.", "NORTHERN ROAD\n<size=70%>dragons not included</size>", "(the trees,\nhowever, are)");
        report.Add("The Cold North: a cart ride, 5 voiced skeletons, the shout, the giant, the cheese, the horse and a sign");
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
        Box(g, "Post", new Vector3(0, 0.2f, 0.05f), new Vector3(0.1f, 0.4f, 0.1f), M("Black"), false);
        Box(g, "Board", new Vector3(0, 1.55f, 0.05f), new Vector3(3.4f, 2.3f, 0.06f), M("Board"));
        Text("Title", g, new Vector3(0, 2.52f, -0.01f), Quaternion.identity, new Vector2(3.2f, 0.25f), 0.2f, "<b>THE STATION</b>").color = new Color(1f, 0.85f, 0.4f);
        Text("Sub", g, new Vector3(0, 2.36f, -0.01f), Quaternion.identity, new Vector2(3.2f, 0.15f), 0.09f, "press to go; each place has a way back").color = new Color(0.8f, 0.8f, 0.8f);
        stationBoard = g;
    }

    /// <summary>Adds a destination to the Station, and a way back to the lobby at the destination.</summary>
    static void StationStop(string title, Transform arrive, Transform backParent, Vector3 backPos, Vector3 backNormal)
    {
        int i = stationSlots++;
        var b = TeleportButton(stationBoard, title, new Vector3(-1.32f + (i % 5) * 0.66f, 2.17f - (i / 5) * 0.18f, -0.03f), Vector3.back, arrive, new Vector3(0.64f, 0.15f, 0.06f));   // 5 across, up to 50 stops
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
        // the town's sky and its north, south and west sides are built with the new districts (BuildTownDistricts);
        // the east wall, with the doorway in from the corridor, runs the whole length of the bigger town
        const float TS = -60f, TN = 70f, TH = 16f;
        Box(g, "WallE S", new Vector3(x0 + 0.1f, TH / 2, (TS + 2.8f) / 2f), new Vector3(0.2f, TH, 2.8f - TS), M("Sky"));
        Box(g, "WallE N", new Vector3(x0 + 0.1f, TH / 2, (5.2f + TN) / 2f), new Vector3(0.2f, TH, TN - 5.2f), M("Sky"));
        Box(g, "WallE Top", new Vector3(x0 + 0.1f, (3f + TH) / 2f, 4f), new Vector3(0.2f, TH - 3f, 2.4f), M("Sky"));
        foreach (var hw in new[] {
            new { p = new Vector3(x0 - 0.25f, 0.6f, (TS + 2.6f) / 2f), s = new Vector3(0.3f, 1.2f, 2.6f - TS) },
            new { p = new Vector3(x0 - 0.25f, 0.6f, (5.4f + TN) / 2f), s = new Vector3(0.3f, 1.2f, TN - 5.4f) } })
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
        TreeAt(g, "TownTree", new Vector3(-13.4f, 0, 26f), 135f, 2.2f, false);   // (moved out of the way of the north lane)

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
            Box(u, "BackWall", new Vector3(hm.x, 1.75f, back - s * 0.12f), new Vector3(w, 3.5f, 0.04f), M(hm.wall), true);
            Box(u, "Outside", new Vector3(hm.x, 1.75f, back + s * 0.02f), new Vector3(w + 0.2f, 3.5f, 0.04f), M("BrickWall"), false);
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
        var rds = root.GetComponentsInChildren<FirResident>(true);
        var me = UB(sh);
        for (int i = 0; i < gags.Length; i++) { gags[i].share = me; gags[i].shareIndex = i; }
        for (int i = 0; i < sgs.Length; i++) { sgs[i].share = me; sgs[i].shareIndex = i; }
        for (int i = 0; i < cls.Length; i++) { cls[i].share = me; cls[i].shareIndex = i; }
        for (int i = 0; i < rbs.Length; i++) { rbs[i].share = me; rbs[i].shareIndex = i; }
        for (int i = 0; i < cbs.Length; i++) { cbs[i].share = me; cbs[i].shareIndex = i; }
        for (int i = 0; i < wns.Length; i++) { wns[i].share = me; wns[i].shareIndex = i; }
        for (int i = 0; i < rds.Length; i++) { rds[i].share = me; rds[i].shareIndex = i; }
        sh.gags = gags.Select(x => UB(x)).ToArray();
        sh.skeletonGags = sgs.Select(x => UB(x)).ToArray();
        sh.closets = cls.Select(x => UB(x)).ToArray();
        sh.gagSkeletons = rbs.Select(x => UB(x)).ToArray();
        sh.combos = cbs.Select(x => UB(x)).ToArray();
        sh.wanderers = wns.Select(x => UB(x)).ToArray();
        sh.residents = rds.Select(x => UB(x)).ToArray();
        report.Add("shared with everyone: " + gags.Count(g => !g.walkIn) + " pressed gags, " + sgs.Length + " routines, " + cls.Length +
                   " closet, " + rbs.Length + " gag skeletons, " + cbs.Length + " combo acts, " + wns.Length + " wanderers, " + rds.Length + " residents");
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
