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
using UnityEngine.AI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

/// <summary>
/// Fir Warning > Build World. Builds the whole horror world into the open scene from code:
/// the lobby, six chapters, the escape room, every talking tree, the stalker and its NavMesh,
/// all the scares, and the VRChat scene descriptor. Re-run any time; it replaces the old
/// "FirWarning" root object.
///
/// Layout (metres): the lobby is off to the side at x = -60. The factory runs along +z:
///   Showroom 0-20 | Stockroom 20-56 | Voice Lab 56-80 | Offices 80-104 | Living Room 104-120 |
///   Recall Floor 120-150 | Exit 150-160
/// </summary>
public static class FirWorldBuilder
{
    const string Root = "Assets/FirHorror";
    const string Gen = Root + "/Generated";
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static Material fontMat;
    static FirGameManager game;
    static readonly List<GameObject> itemList = new List<GameObject>();
    static readonly List<int> itemChapterList = new List<int>();
    static readonly List<VRCObjectSync> carrySyncList = new List<VRCObjectSync>();
    static readonly List<UdonSharpBehaviour> allBehaviours = new List<UdonSharpBehaviour>();
    static readonly List<FirToilet> toilets = new List<FirToilet>();
    static FirStalker stalkerRef;

    class TreeParts
    {
        public GameObject root;
        public Transform head;
        public FirAnimatronic anim;
        public float faceHeight;   // above the root
        public float faceZ;        // forward of the trunk
        public float topHeight;    // canopy tip, above the head pivot
    }

    [MenuItem("Fir Warning/Build World", priority = 1)]
    public static void BuildMenu() { Build(false); }

    /// <summary>Builds the world into the open scene. Returns false if a prerequisite is missing.</summary>
    public static bool Build(bool quiet)
    {
        if (TMP_Settings.defaultFontAsset == null)
        {
            EditorUtility.DisplayDialog("Fir Warning", "Import TextMeshPro first:\nWindow > TextMeshPro > Import TMP Essential Resources", "OK");
            return false;
        }
        if (!EnsureProgramAssets()) return false;
        meshCache.Clear(); mats.Clear(); itemList.Clear(); itemChapterList.Clear(); carrySyncList.Clear(); allBehaviours.Clear(); toilets.Clear(); stalkerRef = null;
        Directory.CreateDirectory(Gen);
        ConfigureAssets();

        var old = GameObject.Find("FirWarning");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("FirWarning").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Fir Warning");
        fontMat = FontMaterial();
        MakeMaterials();

        // ---- the game manager and the head-locked HUD
        var mgrGo = new GameObject("GameManager");
        mgrGo.transform.SetParent(root, false);
        game = Add<FirGameManager>(mgrGo);
        var hud = new GameObject("HUD").transform;
        hud.SetParent(root, false);
        game.hud = hud;
        game.hudSubtitle = Text("Subtitle", hud, new Vector3(0, 0.06f, 0), Quaternion.identity, new Vector2(0.85f, 0.2f), 0.045f, "");
        game.hudObjective = Text("Objective", hud, new Vector3(0, -0.1f, 0), Quaternion.identity, new Vector2(0.7f, 0.06f), 0.03f, "");
        game.hudObjective.color = new Color(1f, 0.85f, 0.5f);
        game.sfx = Src(hud, Vector3.zero, null, false, 0f, 1f, 10f, false);
        game.music = Src(hud, Vector3.zero, null, true, 0f, 0.55f, 10f, false);
        game.chapterMusic = new[] { Clip("music_box"), Clip("amb_drone"), Clip("amb_warehouse"), Clip("amb_drone"),
                                    Clip("amb_warehouse"), Clip("music_box_broken"), Clip("amb_furnace"), Clip("ending") };
        game.screams = new[] { Clip("scream1"), Clip("scream2"), Clip("scream3") };
        game.clipPickup = Clip("pickup"); game.clipUnlock = Clip("unlock"); game.clipWrong = Clip("wrong");
        game.clipTalk = Clip("tree_talk"); game.clipLaugh = Clip("tree_laugh"); game.clipStatic = Clip("static");
        game.clipHit = Clip("scare_hit"); game.clipWhoosh = Clip("whoosh"); game.clipHeartbeat = Clip("heartbeat");
        game.clipWhisper = Clip("whisper");

        // ---- face-scare rig: a tree whose face sits exactly at the rig's origin
        var rig = new GameObject("ScareRig").transform;
        rig.SetParent(root, false);
        var scareTree = BuildTree(rig, Vector3.zero, 0f, 1.5f, 0, false, "ScareTree");
        scareTree.root.transform.localPosition = new Vector3(0, -scareTree.faceHeight, -scareTree.faceZ);
        scareTree.anim.watchRange = 0; scareTree.anim.greetChance = 0;
        game.scareRig = rig;
        game.scareAnim = scareTree.anim;

        // ---- places
        var spawn = BuildLobby(root);
        game.lobbySpawn = spawn;
        BuildFactory(root);

        // ---- ambience
        var amb = new GameObject("Ambience");
        amb.transform.SetParent(root, false);
        var fa = Add<FirAmbience>(amb);
        fa.game = game;
        fa.emitter = Src(amb.transform, Vector3.zero, null, false, 1f, 0.9f, 12f, false);
        fa.clips = new[] { Clip("whisper"), Clip("rustle"), Clip("creak"), Clip("tree_laugh"), Clip("servo"), Clip("tree_talk") };

        // ---- wire up the collected lists
        game.items = itemList.ToArray();
        game.itemChapter = itemChapterList.ToArray();
        foreach (var t in toilets) t.stalker = stalkerRef;
        game.carrySyncs = carrySyncList.ToArray();
        game.animatronics = root.GetComponentsInChildren<FirAnimatronic>(true).Where(a => a != scareTree.anim).ToArray();
        game.jumpscares = root.GetComponentsInChildren<FirJumpscare>(true);
        foreach (var b in root.GetComponentsInChildren<UdonSharpBehaviour>(true)) Sync(b);

        // ---- scene settings, NavMesh, descriptor
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.fog = false;
        foreach (var l in Object.FindObjectsOfType<Light>()) Undo.DestroyObjectImmediate(l.gameObject);
        SetupDescriptor(spawn);
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root.gameObject;
        if (!quiet) EditorUtility.DisplayDialog("Fir Warning", "Built the world: " + game.animatronics.Length + " talking trees, " +
                                                                game.jumpscares.Length + " scripted scares, " + itemList.Count + " items.", "OK");
        return true;
    }

    // =================================================================== lobby

    static Transform BuildLobby(Transform root)
    {
        var lobby = Group(root, "Lobby", new Vector3(-60, 0, 0));
        // 14 x 12 room, fullbright
        Box(lobby, "Floor", new Vector3(0, -0.1f, 0), new Vector3(14, 0.2f, 12), M("LobbyFloor"), true);
        Box(lobby, "Ceiling", new Vector3(0, 3.6f, 0), new Vector3(14, 0.2f, 12), M("LobbyCeiling"), true);
        Box(lobby, "WallN", new Vector3(0, 1.75f, 6.1f), new Vector3(14.4f, 3.5f, 0.2f), M("LobbyWall"), true);
        Box(lobby, "WallS", new Vector3(0, 1.75f, -6.1f), new Vector3(14.4f, 3.5f, 0.2f), M("LobbyWall"), true);
        Box(lobby, "WallW", new Vector3(-7.1f, 1.75f, 0), new Vector3(0.2f, 3.5f, 12.4f), M("LobbyWall"), true);
        Box(lobby, "WallE", new Vector3(7.1f, 1.75f, 0), new Vector3(0.2f, 3.5f, 12.4f), M("LobbyWall"), true);

        var spawn = new GameObject("Spawn").transform;
        spawn.SetParent(lobby, false);
        spawn.localPosition = new Vector3(0, 0, -4);

        var n = Vector3.back;  // north wall faces the players
        var title = Text("Title", lobby, new Vector3(0, 2.9f, 5.98f), Face(n), new Vector2(8, 1), 0.9f, "<b>FIR WARNING</b>");
        title.color = new Color(0.85f, 0.1f, 0.1f);
        Text("Subtitle", lobby, new Vector3(0, 2.35f, 5.98f), Face(n), new Vector2(9, 0.4f), 0.22f,
             "<i>Don't get fooled by imitations.</i>  A co-op horror world for 1-8 players, about 45 minutes.").color = new Color(0.2f, 0.55f, 0.25f);
        game.lobbyStatus = Text("Status", lobby, new Vector3(0, 1.85f, 5.98f), Face(n), new Vector2(6, 0.5f), 0.18f, "");
        game.lobbyStatus.color = Color.white;
        Button("START", lobby, new Vector3(-2.2f, 1.1f, 5.85f), n, new Vector3(1.4f, 0.5f, 0.2f), M("BtnGo"), game, "StartGame");
        Button("Join game", lobby, new Vector3(0, 1.1f, 5.85f), n, new Vector3(1.4f, 0.5f, 0.2f), M("Btn"), game, "JoinGame");
        Button("Reset (press twice)", lobby, new Vector3(2.2f, 1.1f, 5.85f), n, new Vector3(1.4f, 0.5f, 0.2f), M("BtnWarn"), game, "ResetGame");
        Button("Brightness", lobby, new Vector3(-2.2f, 0.5f, 5.85f), n, new Vector3(1.4f, 0.4f, 0.2f), M("Btn"), game, "ToggleBrightness");
        Button("Flashing lights", lobby, new Vector3(0, 0.5f, 5.85f), n, new Vector3(1.4f, 0.4f, 0.2f), M("Btn"), game, "ToggleFlashing");
        Button("Jumpscares", lobby, new Vector3(2.2f, 0.5f, 5.85f), n, new Vector3(1.4f, 0.4f, 0.2f), M("Btn"), game, "ToggleReducedScares");
        game.optionsText = Text("Options", lobby, new Vector3(4.9f, 0.9f, 5.98f), Face(n), new Vector2(2.2f, 1.2f), 0.16f, "");
        game.optionsText.color = Color.white;

        // story on the west wall
        var w = Vector3.right;
        Text("Story", lobby, new Vector3(-6.98f, 1.9f, 0.5f), Face(w), new Vector2(6.5f, 2.6f), 0.2f,
            "<b>Christmas Eve, 1996.</b>\n\nEvergreen Novelty Corp. made the talking tree every family wanted: he sang, he told jokes, " +
            "his eyes followed you round the room. Then came the cheap <b>imitations</b>, and the company's ads got strange.\n\n" +
            "The factory closed that night. The trees never switched off.\n\nYou are the clean-up crew. Get through the building, burn the " +
            "recalled imitations on the Recall Floor, and get out.\n\n<b>Never turn your back on a tree that is facing you.</b>").color = Color.white;
        // how to play on the east wall
        var e = Vector3.left;
        Text("HowTo", lobby, new Vector3(6.98f, 1.9f, -1.5f), Face(e), new Vector2(5.5f, 2.6f), 0.2f,
            "<b>HOW TO PLAY</b>\n\n- Press <b>START</b>. Everyone here is taken in together.\n- Each room has a goal, shown under your view.\n" +
            "- Press (use) things to pick them up. Carry hats and little trees in your hands.\n- One tree <b>only moves when nobody looks at it</b>. " +
            "Watch it while your friends search. If it reaches you, you're sent back to the start of the room.\n" +
            "- Toilets can be picked up and thrown. A good hit stuns the hunting tree for a few seconds.\n" +
            "- Stuck? A hint appears after a few minutes.\n- Late? Press <b>Join game</b>.\n\nBest with headphones. Lights off.").color = Color.white;
        Poster(lobby, "poster_warning", new Vector3(6.98f, 1.7f, 3.6f), e, 1.1f, true);
        Poster(lobby, "poster_original", new Vector3(-6.98f, 1.7f, -4.4f), w, 1.1f, true);

        // two toilets to throw about while you wait
        Toilet(lobby, new Vector3(4.6f, 0, -5.4f), 0);
        Toilet(lobby, new Vector3(5.8f, 0, -5.4f), 0);
        Text("ToiletSign", lobby, new Vector3(5.2f, 1.5f, -5.98f), Face(Vector3.forward), new Vector2(1.6f, 0.4f), 0.14f,
             "<b>Free toilets!</b>\nPick up, throw. Use to flush.").color = Color.white;

        // a little family of "friendly" trees by the door
        for (int i = 0; i < 3; i++)
        {
            var t = BuildTree(lobby, new Vector3(-5.5f + i * 0.9f, 0, -5.2f + (i % 2) * 0.3f), 0f, 0.8f + i * 0.35f, 0, i == 1, "LobbyTree" + i);
            t.anim.greetChance = 0.6f;
        }
        return spawn;
    }

    // =================================================================== factory

    struct Room { public string name; public float z0, z1, w, h; public Material floor, wall, ceil; }

    static void BuildFactory(Transform root)
    {
        var f = Group(root, "Factory", Vector3.zero);
        var rooms = new[]
        {
            new Room { name = "Showroom",    z0 = 0,   z1 = 20,  w = 16, h = 4.0f, floor = M("Wood"),         wall = M("Wallpaper"),    ceil = M("Ceiling") },
            new Room { name = "Stockroom",   z0 = 20,  z1 = 56,  w = 24, h = 5.0f, floor = M("Concrete"),     wall = M("Brick"),        ceil = M("Metal") },
            new Room { name = "VoiceLab",    z0 = 56,  z1 = 80,  w = 16, h = 3.6f, floor = M("OfficeCarpet"), wall = M("Wallpaper"),    ceil = M("Ceiling") },
            new Room { name = "Offices",     z0 = 80,  z1 = 104, w = 20, h = 3.2f, floor = M("OfficeCarpet"), wall = M("OfficeWall"),   ceil = M("Ceiling") },
            new Room { name = "LivingRoom",  z0 = 104, z1 = 120, w = 14, h = 3.4f, floor = M("Wood"),         wall = M("Wallpaper"),    ceil = M("LivingCeiling") },
            new Room { name = "RecallFloor", z0 = 120, z1 = 150, w = 22, h = 6.0f, floor = M("Concrete"),     wall = M("Metal"),        ceil = M("Metal") },
            new Room { name = "Exit",        z0 = 150, z1 = 160, w = 10, h = 3.5f, floor = M("Wood"),         wall = M("Wallpaper"),    ceil = M("Ceiling") },
        };
        var shell = Group(f, "Shell", Vector3.zero);
        var doors = new List<GameObject>();
        var checkpoints = new List<Transform>();
        for (int i = 0; i < rooms.Length; i++)
        {
            var r = rooms[i];
            float len = r.z1 - r.z0, zc = (r.z0 + r.z1) / 2;
            Box(shell, r.name + " Floor", new Vector3(0, -0.1f, zc), new Vector3(r.w, 0.2f, len), r.floor, true);
            Box(shell, r.name + " Ceiling", new Vector3(0, r.h + 0.1f, zc), new Vector3(r.w, 0.2f, len), r.ceil, true);
            Box(shell, r.name + " WallL", new Vector3(-r.w / 2 - 0.1f, r.h / 2, zc), new Vector3(0.2f, r.h, len), r.wall, true);
            Box(shell, r.name + " WallR", new Vector3(r.w / 2 + 0.1f, r.h / 2, zc), new Vector3(0.2f, r.h, len), r.wall, true);
            if (i == 0) DoorWall(shell, r.name + " Back", r.z0, r.w, r.h, r.wall, false);
            if (i == rooms.Length - 1) DoorWall(shell, r.name + " Front", r.z1, r.w, r.h, r.wall, false);
            else
            {
                var next = rooms[i + 1];
                DoorWall(shell, r.name + " Front", r.z1, Mathf.Max(r.w, next.w) + 0.4f, Mathf.Max(r.h, next.h), r.wall, true);
                doors.Add(Door(shell, r.z1, next.name));
            }
            var cp = new GameObject("Checkpoint " + r.name).transform;
            cp.SetParent(f, false);
            cp.localPosition = new Vector3(0, 0.05f, r.z0 + 2.2f);
            checkpoints.Add(cp);
            // a few dim ceiling fixtures so rooms have shape in the dark
            for (float z = r.z0 + 4; z < r.z1 - 2; z += 8)
                Box(shell, "Lamp", new Vector3(0, r.h - 0.02f, z), new Vector3(1.2f, 0.04f, 0.3f), M("LampDim"), false, false);
        }
        game.doors = doors.ToArray();
        game.checkpoints = checkpoints.ToArray();

        // the stalker's parking spot: a closed box far below, with NavMesh so Warp works
        var park = Group(f, "StalkerParking", new Vector3(0, -40, -30));
        Box(park, "Floor", new Vector3(0, -0.1f, 0), new Vector3(4, 0.2f, 4), M("Concrete"), true);

        var stalker = BuildStalker(f, park);
        stalkerRef = stalker;
        BuildShowroom(Group(f, "1 Showroom", Vector3.zero));
        BuildStockroom(Group(f, "2 Stockroom", Vector3.zero), stalker);
        BuildVoiceLab(Group(f, "3 VoiceLab", Vector3.zero));
        BuildOffices(Group(f, "4 Offices", Vector3.zero), stalker);
        BuildLivingRoom(Group(f, "5 LivingRoom", Vector3.zero));
        BuildRecallFloor(Group(f, "6 RecallFloor", Vector3.zero), stalker);
        BuildExit(Group(f, "7 Exit", Vector3.zero));
    }

    // ------------------------------------------------------------------- chapter 1

    static void BuildShowroom(Transform g)
    {
        // the long red table on the left, like the catalogue photo
        Table(g, new Vector3(-5.6f, 0, 9), new Vector3(1.6f, 0.8f, 9f), M("ClothRed"));
        var t1 = BuildTree(g, new Vector3(-5.6f, 0.82f, 6f), 90, 0.7f, 1, true, "TableTree1");
        var t2 = BuildTree(g, new Vector3(-5.6f, 0.82f, 8.5f), 90, 1.0f, 1, false, "TableTree2");
        var t3 = BuildTree(g, new Vector3(-5.6f, 0.82f, 11f), 90, 0.8f, 1, true, "TableTree3");
        BuildTree(g, new Vector3(-5.6f, 0.82f, 13.3f), 90, 0.6f, 1, false, "TableTree4");
        // the big one in the corner
        var big = BuildTree(g, new Vector3(5.6f, 0, 5.5f), -60, 2.6f, 1, false, "TheOriginal");
        big.anim.watchRange = 5f; big.anim.greetChance = 1f;
        // centre display: three singers on a stage
        Box(g, "Stage", new Vector3(0, 0.15f, 11), new Vector3(3.6f, 0.3f, 1.6f), M("ClothRed"), true);
        for (int i = 0; i < 3; i++) BuildTree(g, new Vector3(-1.1f + i * 1.1f, 0.3f, 11), 180, 0.9f + (i == 1 ? 0.3f : 0), 1, i != 1, "Singer" + i);
        // presents round the big tree
        Gifts(g, new Vector3(5.2f, 0, 3.2f), 7);
        Gifts(g, new Vector3(-2f, 0, 17.5f), 4);
        // window with curtains on the right wall
        Quad(g, "Window", new Vector3(7.98f, 1.7f, 14), Face(Vector3.left), new Vector2(2.2f, 1.6f), M("Window"));
        Box(g, "Sill", new Vector3(7.75f, 0.85f, 14), new Vector3(0.5f, 0.1f, 2.6f), M("Trim"), true);
        Box(g, "CurtainA", new Vector3(7.7f, 1.8f, 12.6f), new Vector3(0.12f, 2.6f, 0.7f), M("Curtain"), true);
        Box(g, "CurtainB", new Vector3(7.7f, 1.8f, 15.4f), new Vector3(0.12f, 2.6f, 0.7f), M("Curtain"), true);
        Poster(g, "poster_warning", new Vector3(-7.98f, 1.9f, 16.5f), Vector3.right, 1.2f, false);
        Poster(g, "poster_original", new Vector3(-3.5f, 1.9f, 0.12f), Vector3.forward, 1.2f, false);
        Sign(g, "SHOWROOM", new Vector3(0, 3.4f, 0.2f), Vector3.forward);

        // batteries: under the table (with a face scare), behind the curtain, in the presents
        var under = Scare(g, "UnderTableScare", Vector3.zero, Vector3.zero, 0, 1, 1);
        under.screamIndex = 0; under.duration = 1.1f; under.once = true;
        Item(g, "Battery", 1, new Vector3(-5.6f, 0.06f, 10.4f), M("Battery"), new Vector3(0.07f, 0.06f, 0.07f), PrimitiveType.Cylinder, "Take battery", under);
        Item(g, "Battery", 1, new Vector3(7.62f, 0.95f, 15.0f), M("Battery"), new Vector3(0.07f, 0.06f, 0.07f), PrimitiveType.Cylinder, "Take battery", null);
        Item(g, "Battery", 1, new Vector3(6.4f, 0.72f, 2.2f), M("Battery"), new Vector3(0.07f, 0.06f, 0.07f), PrimitiveType.Cylinder, "Take battery", null);

        // power panel by the door
        var panel = Box(g, "PowerPanel", new Vector3(3.4f, 1.4f, 19.85f), new Vector3(0.9f, 1.1f, 0.2f), M("Metal"), true);
        Station(panel, "Use power panel", "StationPower");
        Text("PanelLabel", g, new Vector3(3.4f, 2.1f, 19.74f), Face(Vector3.back), new Vector2(1.2f, 0.2f), 0.12f, "POWER PANEL");

        // scares
        var hello = Scare(g, "Hello", new Vector3(0, 1.5f, 5.5f), new Vector3(16, 3, 0.4f), 7, 1, 1);
        hello.anim = big.anim; hello.animMode = 2; hello.duration = 1.5f;
        var peek = Scare(g, "CornerPeek", new Vector3(3, 1.5f, 13f), new Vector3(8, 3, 0.4f), 6, 1, 1);
        peek.target = BuildTree(g, new Vector3(-7.2f, 0, 19.2f), 135, 1.5f, 0, false, "PeekTree").root;
        var singAll = Scare(g, "SingAlong", new Vector3(0, 1.5f, 9.5f), new Vector3(6, 3, 0.4f), 2, 1, 1);
        singAll.sound = Src(g, new Vector3(0, 1, 11), Clip("tree_sing"), false, 1f, 0.9f, 20f, false);
        singAll.anim = t2.anim; singAll.animMode = 3; singAll.duration = 18f;
        var glance = Scare(g, "TableGlance", new Vector3(-4, 1.5f, 8.5f), new Vector3(0.4f, 3, 6), 2, 1, 1);
        glance.sound = Src(g, new Vector3(-5.6f, 1.2f, 6f), Clip("servo"), false, 1f, 1f, 8f, false);
        glance.anim = t1.anim; glance.animMode = 1; glance.duration = 6f;
        var behind = Scare(g, "PanelBehind", new Vector3(2.5f, 1.5f, 18.4f), new Vector3(5, 3, 0.4f), 4, 1, 1);
        behind.delay = 1.5f;
        t3.anim.greetChance = 0.8f;
    }

    // ------------------------------------------------------------------- chapter 2

    static void BuildStockroom(Transform g, FirStalker stalker)
    {
        float[] rowsX = { -9f, -4.5f, 4.5f, 9f };
        foreach (float x in rowsX)
        {
            Shelf(g, new Vector3(x, 0, 30f), 11f);
            Shelf(g, new Vector3(x, 0, 45.5f), 11f);
        }
        // pallets of boxed trees in the main aisle for cover
        BoxStack(g, new Vector3(-1.5f, 0, 27f), 2, 3);
        BoxStack(g, new Vector3(1.8f, 0, 41f), 3, 2);
        BoxStack(g, new Vector3(-1.2f, 0, 49f), 2, 2);
        Poster(g, "poster_safety", new Vector3(-3.0f, 2.0f, 20.25f), Vector3.forward, 1.3f, false);
        Poster(g, "poster_recall", new Vector3(11.98f, 2.0f, 37.5f), Vector3.left, 1.3f, false);
        Sign(g, "STOCKROOM", new Vector3(0, 3.5f, 20.25f), Vector3.forward);

        Item(g, "Fuse", 2, new Vector3(-10.8f, 0.1f, 50.6f), M("Fuse"), new Vector3(0.06f, 0.07f, 0.06f), PrimitiveType.Cylinder, "Take fuse", null);
        Item(g, "Fuse", 2, new Vector3(-6.75f, 0.75f, 37.5f), M("Fuse"), new Vector3(0.06f, 0.07f, 0.06f), PrimitiveType.Cylinder, "Take fuse", null);
        Box(g, "Crate", new Vector3(-6.75f, 0.35f, 37.5f), new Vector3(0.7f, 0.7f, 0.7f), M("Cardboard"), true, true, true);
        var deep = Scare(g, "DeepAisleFace", Vector3.zero, Vector3.zero, 0, 2, 2);
        deep.screamIndex = 1; deep.duration = 1.0f;
        Item(g, "Fuse", 2, new Vector3(6.75f, 0.1f, 24.5f), M("Fuse"), new Vector3(0.06f, 0.07f, 0.06f), PrimitiveType.Cylinder, "Take fuse", deep);
        Item(g, "Fuse", 2, new Vector3(10.8f, 0.85f, 44f), M("Fuse"), new Vector3(0.06f, 0.07f, 0.06f), PrimitiveType.Cylinder, "Take fuse", null);
        Box(g, "Crate", new Vector3(10.8f, 0.4f, 44f), new Vector3(0.8f, 0.8f, 0.8f), M("Cardboard"), true, true, true);

        var fuseBox = Box(g, "FuseBox", new Vector3(-6f, 1.4f, 55.85f), new Vector3(0.9f, 1.1f, 0.2f), M("Metal"), true);
        Station(fuseBox, "Use fuse box", "StationFuses");
        Text("FuseLabel", g, new Vector3(-6f, 2.1f, 55.74f), Face(Vector3.back), new Vector2(1.2f, 0.2f), 0.12f, "FUSE BOX");

        stalker.spawnByPhase[2] = Marker(g, "StalkerSpawn2", new Vector3(8.5f, 0, 52f), 180);

        // scares
        var drop = Scare(g, "BoxDrop", new Vector3(-6.75f, 1.5f, 36.2f), new Vector3(2.5f, 3, 0.4f), 3, 2, 2);
        drop.target = Box(g, "FallingBox", new Vector3(-4.5f, 3.35f, 40.6f), new Vector3(0.7f, 0.6f, 0.7f), M("Cardboard"), false, false, true);
        drop.dropTo = Marker(g, "FallingBoxEnd", new Vector3(-5.6f, 0.3f, 38.6f), 20);
        drop.sound = Src(g, new Vector3(-6.4f, 1f, 37f), Clip("scare_hit"), false, 1f, 1f, 15f, false);
        var song = Scare(g, "DistantSong", new Vector3(0, 1.5f, 33f), new Vector3(8, 3, 0.4f), 2, 2, 2);
        song.sound = Src(g, new Vector3(-11f, 1f, 54f), Clip("tree_sing_wrong"), false, 1f, 0.8f, 30f, false);
        var peek = Scare(g, "AislePeek", new Vector3(6.75f, 1.5f, 27f), new Vector3(2.4f, 3, 0.4f), 6, 2, 2);
        peek.target = BuildTree(g, new Vector3(6.75f, 0, 51.5f), 180, 1.7f, 0, false, "AislePeekTree").root;
        var crate = BuildTree(g, new Vector3(10.8f, 0, 31f), -90, 1.4f, 2, false, "CrateTree");
        var pop = Scare(g, "CratePop", new Vector3(10.8f, 1.5f, 33.5f), new Vector3(2.4f, 3, 0.4f), 1, 2, 2);
        pop.target = crate.root; pop.anim = crate.anim; pop.animMode = 4; pop.duration = 1.6f;
        pop.sound = Src(g, new Vector3(10.8f, 1, 31f), Clip("scream2"), false, 1f, 1f, 12f, false);
        var behind = Scare(g, "FuseBehind", new Vector3(-6f, 1.5f, 53.5f), new Vector3(4, 3, 0.4f), 4, 2, 2);
        behind.delay = 2f;
        var crossFace = Scare(g, "CrossAisleFace", new Vector3(9f, 1.5f, 38.2f), new Vector3(0.4f, 3, 2.6f), 0, 2, 2);
        crossFace.screamIndex = 2; crossFace.duration = 0.9f;
    }

    // ------------------------------------------------------------------- chapter 3

    static void BuildVoiceLab(Transform g)
    {
        Sign(g, "VOICE LAB", new Vector3(0, 3.1f, 56.25f), Vector3.forward);
        Poster(g, "poster_warning_scrawl", new Vector3(7.98f, 1.8f, 60f), Vector3.left, 1.2f, false);

        // conveyor carrying tree heads across the room (one carries a tape)
        Box(g, "ConveyorBelt", new Vector3(0, 0.75f, 64f), new Vector3(12f, 0.12f, 1.0f), M("Rubber"), true);
        Box(g, "ConveyorBase", new Vector3(0, 0.35f, 64f), new Vector3(12f, 0.7f, 0.9f), M("Metal"), true);
        var conv = Group(g, "ConveyorItems", new Vector3(0, 0.81f, 64f));
        var cv = Add<FirConveyor>(conv.gameObject);
        cv.length = 12f; cv.speed = 0.35f;
        cv.hum = Src(conv, Vector3.zero, Clip("servo"), true, 1f, 0.25f, 10f, true);
        cv.hum.transform.SetParent(g, true);
        var heads = new List<TreeParts>();
        for (int i = 0; i < 5; i++)
        {
            var h = BuildTree(conv, new Vector3(-5.5f + i * 2.4f, 0, 0), 180, 0.55f, 3, i % 2 == 0, "BeltHead" + i);
            h.anim.watchRange = 2.5f;
            heads.Add(h);
        }
        var tapeOnBelt = Item(conv, "Tape", 3, new Vector3(-4.3f, 0.04f, 0), M("TapeRed"), new Vector3(0.12f, 0.03f, 0.08f), PrimitiveType.Cube, "Take tape", null);
        tapeOnBelt.transform.SetParent(conv, true);

        // recording booth on the right
        var booth = Group(g, "Booth", new Vector3(5.5f, 0, 72f));
        Box(booth, "BackWall", new Vector3(0, 1.5f, 3f), new Vector3(4.6f, 3f, 0.15f), M("Foam"), true);
        Box(booth, "SideWall", new Vector3(-2.3f, 1.5f, 0.6f), new Vector3(0.15f, 3f, 4.8f), M("Foam"), true);
        Box(booth, "FrontLow", new Vector3(0.6f, 0.5f, -1.8f), new Vector3(3.2f, 1f, 0.15f), M("Foam"), true);
        Box(booth, "FrontTop", new Vector3(0.6f, 2.6f, -1.8f), new Vector3(3.2f, 0.8f, 0.15f), M("Foam"), true);
        Box(booth, "Stool", new Vector3(0.6f, 0.3f, 1.5f), new Vector3(0.5f, 0.6f, 0.5f), M("Metal"), true);
        var singer = BuildTree(booth, new Vector3(0.6f, 0.6f, 1.5f), 180, 1.3f, 3, false, "BoothSinger");
        Box(booth, "MicStand", new Vector3(0.6f, 0.8f, 0.7f), new Vector3(0.04f, 1.6f, 0.04f), M("Metal"), false);
        Box(booth, "Mic", new Vector3(0.6f, 1.6f, 0.65f), new Vector3(0.08f, 0.14f, 0.08f), M("Black"), false);
        Item(booth, "Tape", 3, new Vector3(-1.6f, 0.03f, 2.4f), M("TapeBlue"), new Vector3(0.12f, 0.03f, 0.08f), PrimitiveType.Cube, "Take tape", null);

        // desks with the other two tapes
        Desk(g, new Vector3(-5f, 0, 70f), 90);
        Desk(g, new Vector3(-5f, 0, 75.5f), 90);
        var deskScare = Scare(g, "DeskTapeFace", Vector3.zero, Vector3.zero, 0, 3, 3);
        deskScare.screamIndex = 0; deskScare.duration = 1.0f;
        Item(g, "Tape", 3, new Vector3(-5f, 0.78f, 70.2f), M("TapeGreen"), new Vector3(0.12f, 0.03f, 0.08f), PrimitiveType.Cube, "Take tape", null);
        Item(g, "Tape", 3, new Vector3(-4.8f, 0.78f, 75.3f), M("TapeGold"), new Vector3(0.12f, 0.03f, 0.08f), PrimitiveType.Cube, "Take tape", deskScare);
        Box(g, "ClerkChair", new Vector3(-6.2f, 0.22f, 75.5f), new Vector3(0.5f, 0.45f, 0.5f), M("Black"), true);
        var clerk = BuildTree(g, new Vector3(-6.2f, 0.45f, 75.5f), 90, 1.1f, 3, false, "Clerk");
        clerk.anim.watchRange = 2.5f;

        // the whiteboard and the tape deck
        Box(g, "Whiteboard", new Vector3(-7.95f, 1.7f, 64f), new Vector3(0.05f, 1.2f, 2.2f), M("Whiteboard"), true);
        game.tapeBoard = Text("RecordingLog", g, new Vector3(-7.9f, 1.7f, 64f), Face(Vector3.right), new Vector2(2.0f, 1.1f), 0.14f, "");
        game.tapeBoard.color = new Color(0.1f, 0.1f, 0.15f);
        Desk(g, new Vector3(0, 0, 77.6f), 0);
        Box(g, "TapeDeck", new Vector3(0, 0.84f, 77.5f), new Vector3(0.9f, 0.14f, 0.45f), M("Black"), true);
        var lk = Add<FirSequenceLock>(Group(g, "TapeLock", new Vector3(0, 0.84f, 77.5f)).gameObject);
        lk.game = game; lk.chapter = 3; lk.length = 4; lk.needsItems = true;
        lk.valueNames = new[] { "R", "B", "G", "Y" };
        lk.valueClips = new[] { Clip("tree_talk"), Clip("whisper"), Clip("tree_laugh"), Clip("static") };
        lk.beep = Src(lk.transform, Vector3.zero, Clip("servo"), false, 1f, 1f, 8f, false);
        lk.display = Text("DeckDisplay", lk.transform, new Vector3(0, 0.45f, 0.1f), Face(Vector3.back), new Vector2(0.8f, 0.2f), 0.12f, "_ _ _ _");
        lk.display.color = new Color(1f, 0.3f, 0.2f);
        string[] tn = { "RED", "BLUE", "GREEN", "GOLD" };
        string[] tm = { "TapeRed", "TapeBlue", "TapeGreen", "TapeGold" };
        for (int i = 0; i < 4; i++) Key(lk, tn[i], i, new Vector3(-0.33f + i * 0.22f, 0.1f, -0.05f), new Vector3(0.18f, 0.06f, 0.18f), M(tm[i]), true);

        // scares
        var intro = Scare(g, "LabIntro", new Vector3(0, 1.5f, 59.5f), new Vector3(16, 3, 0.4f), 5, 3, 3);
        intro.groupChapter = 3; intro.duration = 2f; intro.screamIndex = 1;
        var boothScare = Scare(g, "BoothScream", new Vector3(5.5f, 1.5f, 69.5f), new Vector3(3.2f, 3, 0.4f), 1, 3, 3);
        boothScare.anim = singer.anim; boothScare.animMode = 4; boothScare.duration = 2f;
        boothScare.sound = Src(booth, new Vector3(0.6f, 1.5f, 1.5f), Clip("scream3"), false, 1f, 1f, 15f, false);
        var jam = Scare(g, "BeltStops", new Vector3(0, 1.5f, 66.5f), new Vector3(16, 3, 0.4f), 7, 3, 3);
        jam.anim = heads[2].anim; jam.animMode = 2; jam.duration = 2.5f;
        jam.sound = Src(g, new Vector3(0, 1, 64), Clip("tree_talk"), false, 1f, 1f, 15f, false);
        var deckBehind = Scare(g, "DeckBehind", new Vector3(0, 1.5f, 76f), new Vector3(4, 3, 0.4f), 4, 3, 3);
        deckBehind.delay = 3f;
    }

    // ------------------------------------------------------------------- chapter 4

    static void BuildOffices(Transform g, FirStalker stalker)
    {
        Sign(g, "OFFICES", new Vector3(0, 2.8f, 80.25f), Vector3.forward);
        float[] cx = { -7.2f, -3.6f, 3.6f, 7.2f };
        float[] cz = { 85.5f, 91.5f, 97.5f };
        int[,] memoAt = { { 0, 0 }, { 3, 1 }, { 1, 2 }, { 2, 0 } };  // (col, row) of the four memos
        var memos = new TextMeshPro[4];
        int n = 0;
        for (int c = 0; c < 4; c++)
            for (int r = 0; r < 3; r++)
            {
                bool leftSide = c < 2;
                var cub = Group(g, "Cubicle " + c + "," + r, new Vector3(cx[c], 0, cz[r]));
                Box(cub, "Back", new Vector3(leftSide ? -1.4f : 1.4f, 0.75f, 0), new Vector3(0.08f, 1.5f, 2.8f), M("Partition"), true);
                Box(cub, "SideA", new Vector3(0, 0.75f, -1.4f), new Vector3(2.8f, 1.5f, 0.08f), M("Partition"), true);
                Box(cub, "SideB", new Vector3(0, 0.75f, 1.4f), new Vector3(2.8f, 1.5f, 0.08f), M("Partition"), true);
                float deskX = leftSide ? -0.9f : 0.9f;
                Box(cub, "Desk", new Vector3(deskX, 0.72f, 0), new Vector3(0.8f, 0.05f, 1.8f), M("DeskWood"), true);
                Box(cub, "Monitor", new Vector3(deskX + (leftSide ? -0.15f : 0.15f), 0.97f, 0.3f), new Vector3(0.12f, 0.4f, 0.5f), M("Black"), true);
                // a tree employee in about half the chairs
                if ((c + r) % 2 == 0)
                {
                    Box(cub, "Chair", new Vector3(deskX + (leftSide ? 0.75f : -0.75f), 0.25f, 0), new Vector3(0.5f, 0.5f, 0.5f), M("Black"), true);
                    var emp = BuildTree(cub, new Vector3(deskX + (leftSide ? 0.75f : -0.75f), 0.5f, 0), leftSide ? -90 : 90, 1.2f, 4, n % 3 == 0, "Employee");
                    emp.anim.watchRange = 3f; emp.anim.greetChance = 0.25f;
                }
                n++;
                for (int k = 0; k < 4; k++)
                    if (memoAt[k, 0] == c && memoAt[k, 1] == r)
                    {
                        var paperPos = new Vector3(deskX, 0.752f, -0.45f);
                        Quad(cub, "Memo", paperPos, Quaternion.Euler(90, 0, 0), new Vector2(0.3f, 0.4f), M("Paper"));
                        memos[k] = Text("MemoText", cub, paperPos + new Vector3(0, 0.002f, 0), Quaternion.Euler(90, 0, 0), new Vector2(0.27f, 0.37f), 0.025f, "");
                        memos[k].color = new Color(0.1f, 0.1f, 0.12f);
                    }
            }
        game.memoTexts = memos;
        Poster(g, "poster_recall", new Vector3(-9.98f, 1.7f, 102f), Vector3.right, 1.1f, false);

        // staff restroom by the entrance: three stalls, three throwable toilets
        Sign(g, "RESTROOM", new Vector3(-8f, 2.5f, 80.25f), Vector3.forward);
        for (int i = 0; i < 3; i++)
        {
            float z = 81.0f + i * 1.15f;
            Toilet(g, new Vector3(-9.55f, 0, z), 90);
            Box(g, "StallWall", new Vector3(-9.4f, 0.75f, z + 0.575f), new Vector3(1.2f, 1.5f, 0.05f), M("Partition"), true);
        }
        var flushScare = Scare(g, "StallFlush", new Vector3(-7.5f, 1.5f, 82.2f), new Vector3(0.4f, 3, 3.4f), 2, 4, 4);
        flushScare.sound = Src(g, new Vector3(-9.6f, 0.6f, 82.15f), Clip("toilet_flush"), false, 1f, 1f, 12f, false);

        // keypad beside the door
        var kp = Add<FirSequenceLock>(Group(g, "Keypad", new Vector3(2.8f, 1.35f, 103.85f)).gameObject);
        kp.game = game; kp.chapter = 4; kp.length = 4;
        Box(kp.transform, "Plate", new Vector3(0, 0, 0.05f), new Vector3(0.5f, 0.75f, 0.06f), M("Metal"), true);
        kp.display = Text("Display", kp.transform, new Vector3(0, 0.27f, -0.01f), Face(Vector3.back), new Vector2(0.42f, 0.1f), 0.07f, "_ _ _ _");
        kp.display.color = new Color(0.3f, 1f, 0.4f);
        kp.beep = Src(kp.transform, Vector3.zero, Clip("pickup"), false, 1f, 0.6f, 8f, false);
        string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "" };
        int[] values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, -1, 0, -1 };
        for (int i = 0; i < 12; i++)
        {
            if (labels[i] == "") continue;
            Key(kp, labels[i], values[i], new Vector3(-0.14f + (i % 3) * 0.14f, 0.13f - (i / 3) * 0.12f, 0), new Vector3(0.11f, 0.09f, 0.04f), M("Btn"), false);
        }
        Text("KeypadLabel", g, new Vector3(2.8f, 1.9f, 103.75f), Face(Vector3.back), new Vector2(0.8f, 0.15f), 0.08f, "DOOR CODE");

        stalker.spawnByPhase[4] = Marker(g, "StalkerSpawn4", new Vector3(-8.5f, 0, 102.5f), 150);

        // scares
        var phone = Scare(g, "Phone", new Vector3(0, 1.5f, 88f), new Vector3(3, 3, 0.4f), 2, 4, 4);
        phone.sound = Src(g, new Vector3(7.2f, 0.9f, 91.5f), Clip("phone"), false, 1f, 1f, 20f, false);
        var stare = Scare(g, "AllStare", new Vector3(0, 1.5f, 94f), new Vector3(3, 3, 0.4f), 7, 4, 4);
        stare.duration = 1.5f; stare.sound = Src(g, new Vector3(0, 2.5f, 94f), Clip("static"), false, 1f, 0.7f, 20f, false);
        var cubFace = Scare(g, "CubicleFace", new Vector3(-7.2f, 1.5f, 97.5f), new Vector3(2, 3, 2), 0, 4, 4);
        cubFace.screamIndex = 2; cubFace.duration = 1.0f; cubFace.delay = 0.6f;
        var peek = Scare(g, "OfficePeek", new Vector3(0, 1.5f, 83f), new Vector3(3, 3, 0.4f), 6, 4, 4);
        peek.target = BuildTree(g, new Vector3(0.5f, 0, 103f), 180, 1.8f, 0, false, "OfficePeekTree").root;
        var group = Scare(g, "KeypadGroup", new Vector3(2.8f, 1.5f, 102.4f), new Vector3(3, 3, 0.4f), 5, 4, 4);
        group.groupChapter = 4; group.duration = 2.2f; group.screamIndex = 0; group.delay = 5f;
        var behind = Scare(g, "BackCorner", new Vector3(8.5f, 1.5f, 101f), new Vector3(2.5f, 3, 0.4f), 4, 4, 4);
    }

    // ------------------------------------------------------------------- chapter 5

    static void BuildLivingRoom(Transform g)
    {
        Sign(g, "HOME", new Vector3(0, 3.0f, 104.25f), Vector3.forward);
        // wainscot, window, curtains, fireplace, table: the catalogue photo, wrong
        Box(g, "WainscotL", new Vector3(-6.95f, 0.5f, 112), new Vector3(0.1f, 1f, 16), M("Trim"), true);
        Box(g, "WainscotR", new Vector3(6.95f, 0.5f, 112), new Vector3(0.1f, 1f, 16), M("Trim"), true);
        Quad(g, "Window", new Vector3(-3.5f, 1.8f, 119.88f), Face(Vector3.back), new Vector2(2.4f, 1.8f), M("Window"));
        Box(g, "CurtainA", new Vector3(-5.0f, 1.9f, 119.75f), new Vector3(0.6f, 2.6f, 0.12f), M("Curtain"), true);
        Box(g, "CurtainB", new Vector3(-2.0f, 1.9f, 119.75f), new Vector3(0.6f, 2.6f, 0.12f), M("Curtain"), true);
        Table(g, new Vector3(-4.6f, 0, 114f), new Vector3(1.8f, 0.75f, 2.4f), M("ClothRed"));
        Box(g, "Fireplace", new Vector3(-6.5f, 0.7f, 108f), new Vector3(0.9f, 1.4f, 2.0f), M("Brick"), true);
        Box(g, "Fire", new Vector3(-6.03f, 0.4f, 108f), new Vector3(0.05f, 0.5f, 1.0f), M("GlowOrange"), false);
        Box(g, "Mantle", new Vector3(-6.4f, 1.45f, 108f), new Vector3(1.1f, 0.1f, 2.3f), M("DeskWood"), true);
        Box(g, "Armchair", new Vector3(5.2f, 0.3f, 108f), new Vector3(1.0f, 0.6f, 1.0f), M("Curtain"), true);
        Box(g, "ArmchairBack", new Vector3(5.65f, 0.8f, 108f), new Vector3(0.15f, 1.0f, 1.0f), M("Curtain"), true);
        Gifts(g, new Vector3(4.5f, 0, 117.4f), 6);
        Poster(g, "poster_original", new Vector3(6.98f, 1.9f, 112f), Vector3.left, 1.1f, false);

        // the family: small and medium on the table, large on the floor, and the Original
        var small = BuildTree(g, new Vector3(-4.9f, 0.77f, 113.3f), 140, 0.65f, 5, false, "FamilySmall");
        var medium = BuildTree(g, new Vector3(-4.4f, 0.77f, 114.8f), 140, 1.0f, 5, false, "FamilyMedium");
        var large = BuildTree(g, new Vector3(-1.4f, 0, 116.6f), 170, 1.5f, 5, false, "FamilyLarge");
        var orig = BuildTree(g, new Vector3(4.6f, 0, 115.8f), -150, 2.7f, 5, false, "TheOriginalAtHome");
        orig.anim.watchRange = 6f; orig.anim.greetChance = 1f;
        var family = new[] { small, medium, large };
        var hatsOn = new GameObject[3];
        var hatPicks = new GameObject[3];
        string[] sizeName = { "small", "medium", "large" };
        Vector3[] hatSpots = { new Vector3(-6.4f, 1.58f, 107.6f), new Vector3(5.1f, 0.65f, 108f), new Vector3(3.0f, 0.05f, 118.6f) };
        for (int s = 0; s < 3; s++)
        {
            var tr = family[s];
            float scale = 0.6f + 0.35f * s;
            // the hat that appears on the tree when it's filled
            hatsOn[s] = Hat(tr.head, new Vector3(0, tr.topHeight - 0.08f * scale, 0), scale * 0.9f, "HatOnTree");
            hatsOn[s].SetActive(false);
            // the socket on the tree's head
            var sock = new GameObject("HatSocket");
            sock.transform.SetParent(tr.head, false);
            sock.transform.localPosition = new Vector3(0, tr.topHeight - 0.05f, 0);
            var sc = sock.AddComponent<BoxCollider>();
            sc.isTrigger = true; sc.size = Vector3.one * (0.35f + 0.2f * s);
            var fs = Add<FirSocket>(sock);
            fs.game = game; fs.acceptKind = 0; fs.socketId = s; fs.tree = tr.anim;
            // the pickup somewhere in the room
            hatPicks[s] = CarryHat(g, hatSpots[s], scale, s, "Santa hat (" + sizeName[s] + ")");
        }
        game.hatsOnTrees = hatsOn;
        game.hatPickups = hatPicks;

        // scares
        var welcome = Scare(g, "FamilySong", new Vector3(0, 1.5f, 106.5f), new Vector3(14, 3, 0.4f), 2, 5, 5);
        welcome.sound = Src(g, new Vector3(-3f, 1.2f, 115f), Clip("tree_sing_wrong"), false, 1f, 0.8f, 20f, false);
        welcome.anim = orig.anim; welcome.animMode = 3; welcome.duration = 25f;
        var winPeek = Scare(g, "WindowPeek", new Vector3(-3.5f, 1.5f, 112f), new Vector3(4, 3, 0.4f), 6, 5, 5);
        winPeek.target = BuildTree(g, new Vector3(1.0f, 0, 119.2f), 180, 2.0f, 0, false, "CornerWatcher").root;
        var fire = Scare(g, "FireplaceBehind", new Vector3(-5f, 1.5f, 108f), new Vector3(0.4f, 3, 2.5f), 4, 5, 5);
        var chair = Scare(g, "ArmchairFace", new Vector3(4.3f, 1.5f, 108f), new Vector3(0.4f, 3, 2.5f), 0, 5, 5);
        chair.screamIndex = 1; chair.duration = 1.0f; chair.delay = 0.8f;
    }

    // ------------------------------------------------------------------- chapter 6

    static void BuildRecallFloor(Transform g, FirStalker stalker)
    {
        Sign(g, "RECALL FLOOR", new Vector3(0, 3.2f, 120.25f), Vector3.forward);
        Poster(g, "poster_recall", new Vector3(-3f, 2.0f, 120.25f), Vector3.forward, 1.3f, false);
        // the furnace, front left
        Box(g, "Furnace", new Vector3(-7.5f, 2.2f, 147.6f), new Vector3(4.5f, 4.4f, 3.6f), M("Brick"), true);
        Box(g, "FurnaceMouth", new Vector3(-7.5f, 1.1f, 145.78f), new Vector3(2.0f, 1.3f, 0.05f), M("GlowOrange"), false);
        Box(g, "Chimney", new Vector3(-7.5f, 5.0f, 148f), new Vector3(1.2f, 2f, 1.2f), M("Brick"), true);
        Src(g, new Vector3(-7.5f, 1.2f, 145.5f), Clip("amb_furnace"), true, 1f, 0.7f, 14f, true);
        var mouth = new GameObject("FurnaceSocket");
        mouth.transform.SetParent(g, false);
        mouth.transform.localPosition = new Vector3(-7.5f, 1.1f, 145.4f);
        var mc = mouth.AddComponent<BoxCollider>();
        mc.isTrigger = true; mc.size = new Vector3(2.2f, 1.5f, 1.0f);
        var fs = Add<FirSocket>(mouth);
        fs.game = game; fs.acceptKind = 1;
        Text("FurnaceLabel", g, new Vector3(-7.5f, 2.3f, 145.7f), Face(Vector3.back), new Vector2(3f, 0.4f), 0.25f, "<color=#ffaa55>DISPOSAL</color>");

        // pallets, cages and dead conveyors for cover
        BoxStack(g, new Vector3(-4f, 0, 126f), 3, 2);
        BoxStack(g, new Vector3(5f, 0, 130f), 2, 3);
        BoxStack(g, new Vector3(-2f, 0, 136f), 3, 3);
        BoxStack(g, new Vector3(6.5f, 0, 140f), 2, 2);
        BoxStack(g, new Vector3(-8.5f, 0, 133f), 2, 3);
        Box(g, "DeadConveyor", new Vector3(3f, 0.45f, 135f), new Vector3(0.9f, 0.9f, 8f), M("Metal"), true);

        // five imitations to burn
        Vector3[] spots = { new Vector3(-9.5f, 0.2f, 123.5f), new Vector3(9.8f, 0.2f, 127f), new Vector3(3f, 1.05f, 133.5f),
                            new Vector3(-9f, 0.2f, 141f), new Vector3(9.5f, 0.2f, 146f) };
        for (int i = 0; i < spots.Length; i++) CarryImitation(g, spots[i], i);

        // hanging tree that drops
        var hangDrop = Scare(g, "CeilingDrop", new Vector3(0, 1.5f, 138.5f), new Vector3(8, 3, 0.4f), 3, 6, 6);
        var hanger = BuildTree(g, new Vector3(0.5f, 4.6f, 140.5f), 180, 1.3f, 6, false, "HangingTree");
        hangDrop.target = hanger.root; hangDrop.anim = hanger.anim; hangDrop.animMode = 4; hangDrop.duration = 2f;
        hangDrop.dropTo = Marker(g, "HangDropEnd", new Vector3(0.5f, 0, 140.5f), 180);
        hangDrop.sound = Src(g, new Vector3(0.5f, 2f, 140.5f), Clip("scream3"), false, 1f, 1f, 18f, false);
        var face1 = Scare(g, "PalletFace", new Vector3(-6.5f, 1.5f, 128f), new Vector3(0.4f, 3, 3f), 0, 6, 6);
        face1.screamIndex = 2; face1.duration = 1.0f;
        var peek = Scare(g, "FloorPeek", new Vector3(0, 1.5f, 124f), new Vector3(10, 3, 0.4f), 6, 6, 6);
        peek.target = BuildTree(g, new Vector3(-10f, 0, 148.5f), 135, 2.0f, 0, false, "FurnacePeek").root;
        var behind = Scare(g, "FurnaceBehind", new Vector3(-7.5f, 1.5f, 143.5f), new Vector3(3, 3, 0.4f), 4, 6, 6);
        behind.delay = 1f;

        stalker.spawnByPhase[6] = Marker(g, "StalkerSpawn6", new Vector3(9f, 0, 148f), 200);
    }

    // ------------------------------------------------------------------- escape room

    static void BuildExit(Transform g)
    {
        Sign(g, "EXIT", new Vector3(0, 3.0f, 150.25f), Vector3.forward, "GlowGreen");
        game.endingBoard = Text("EndingBoard", g, new Vector3(0, 1.9f, 159.85f), Face(Vector3.back), new Vector2(5f, 2.2f), 0.3f, "");
        game.endingBoard.color = Color.white;
        var back = Button("Back to the lobby", g, new Vector3(-3f, 1.0f, 159.8f), Vector3.back, new Vector3(1.6f, 0.5f, 0.2f), M("BtnGo"), game, "ReturnToLobby");
        var orig = BuildTree(g, new Vector3(3.6f, 0, 158.4f), -150, 2.4f, 7, false, "TheOriginalWaits");
        orig.anim.watchRange = 6f;
        var last = Scare(g, "OneLastScare", new Vector3(0, 1.5f, 154f), new Vector3(10, 3, 0.4f), 0, 7, 7);
        last.delay = 6f; last.screamIndex = 2; last.duration = 1.3f;
        var song = Scare(g, "Credits", new Vector3(0, 1.5f, 152f), new Vector3(10, 3, 0.4f), 2, 7, 7);
        song.sound = Src(g, new Vector3(3.6f, 1.5f, 158f), Clip("tree_sing"), false, 1f, 0.9f, 20f, false);
        song.anim = orig.anim; song.animMode = 3; song.duration = 19f;
    }

    // =================================================================== stalker

    static FirStalker BuildStalker(Transform f, Transform park)
    {
        var go = new GameObject("Stalker");
        go.transform.SetParent(f, false);
        go.transform.position = park.position;
        var agent = go.AddComponent<NavMeshAgent>();
        agent.radius = 0.4f; agent.height = 2.2f; agent.speed = 2.2f; agent.acceleration = 30f; agent.angularSpeed = 240f;
        agent.stoppingDistance = 0.3f; agent.autoBraking = false;
        var visualRoot = new GameObject("Visual").transform;
        visualRoot.SetParent(go.transform, false);
        var t = BuildTree(visualRoot, Vector3.zero, 0, 2.3f, 0, false, "Stalker Tree");
        t.anim.watchRange = 8f; t.anim.greetChance = 0f;
        var st = Add<FirStalker>(go);
        st.game = game; st.agent = agent; st.visual = visualRoot.gameObject; st.anim = t.anim;
        var eyes = new GameObject("Eyes").transform;
        eyes.SetParent(go.transform, false);
        eyes.localPosition = new Vector3(0, t.faceHeight, t.faceZ * 0.5f);
        st.eyes = eyes;
        st.spawnByPhase = new Transform[8];
        st.parking = park;
        st.moveAudio = Src(go.transform, new Vector3(0, 1f, 0), null, false, 1f, 1f, 16f, false);
        st.moveClips = new[] { Clip("rustle"), Clip("creak"), Clip("servo") };
        visualRoot.gameObject.SetActive(false);
        return st;
    }

    // =================================================================== props

    /// <summary>A procedural talking tree. Its face looks along the root's +Z.</summary>
    static TreeParts BuildTree(Transform parent, Vector3 pos, float yaw, float height, int chapter, bool hat, string name)
    {
        var tr = new TreeParts();
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = pos;
        root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        tr.root = root;
        float potH = Mathf.Max(0.08f, height * 0.09f);
        float trunkH = height * 0.08f;
        float canopyH = height - potH - trunkH;
        float radius = height * 0.36f;
        // pot (big trees get a stand)
        Prim(PrimitiveType.Cylinder, "Pot", root.transform, new Vector3(0, potH / 2, 0), new Vector3(radius * 0.55f, potH / 2, radius * 0.55f), M(height > 1.8f ? "Stand" : "Pot"));
        Prim(PrimitiveType.Cylinder, "Trunk", root.transform, new Vector3(0, potH + trunkH / 2, 0), new Vector3(radius * 0.18f, trunkH / 2 + 0.02f, radius * 0.18f), M("Trunk"));
        var head = new GameObject("Head").transform;
        head.SetParent(root.transform, false);
        head.localPosition = new Vector3(0, potH + trunkH, 0);
        tr.head = head;
        tr.topHeight = canopyH;
        var canopy = new GameObject("Canopy");
        canopy.transform.SetParent(head, false);
        canopy.AddComponent<MeshFilter>().sharedMesh = FirMesh(Mathf.Clamp(Mathf.RoundToInt(height * 2.2f), 3, 6), canopyH, radius, 14, 0.7f);
        NoShadows(canopy.AddComponent<MeshRenderer>(), M("Needles"));

        // face
        float faceY = canopyH * 0.5f;
        float rAt = radius * (1f - faceY / canopyH) * 1.05f;
        float eye = Mathf.Max(0.035f, canopyH * 0.055f);
        float faceZ = rAt * 0.92f;
        tr.faceHeight = potH + trunkH + faceY;
        tr.faceZ = faceZ;
        var eyeL = Prim(PrimitiveType.Sphere, "EyeL", head, new Vector3(-eye * 1.25f, faceY + eye * 1.1f, faceZ), Vector3.one * eye * 2f, M("Eye"));
        var eyeR = Prim(PrimitiveType.Sphere, "EyeR", head, new Vector3(eye * 1.25f, faceY + eye * 1.1f, faceZ), Vector3.one * eye * 2f, M("Eye"));
        var pupL = Prim(PrimitiveType.Sphere, "PupilL", head, eyeL.transform.localPosition + new Vector3(0, 0, eye * 0.85f), Vector3.one * eye * 0.8f, M("Pupil"));
        var pupR = Prim(PrimitiveType.Sphere, "PupilR", head, eyeR.transform.localPosition + new Vector3(0, 0, eye * 0.85f), Vector3.one * eye * 0.8f, M("Pupil"));
        var mouthPivot = new GameObject("Mouth").transform;
        mouthPivot.SetParent(head, false);
        mouthPivot.localPosition = new Vector3(0, faceY - eye * 1.4f, faceZ - eye * 0.2f);
        mouthPivot.localScale = new Vector3(1, 1, 1);
        Prim(PrimitiveType.Sphere, "Lips", mouthPivot, Vector3.zero, new Vector3(eye * 3.2f, eye * 1.6f, eye * 1.2f), M("Mouth"));
        Prim(PrimitiveType.Sphere, "Throat", mouthPivot, new Vector3(0, 0, eye * 0.25f), new Vector3(eye * 2.4f, eye * 1.1f, eye * 0.9f), M("Black"));
        if (hat) Hat(head, new Vector3(0, canopyH - 0.08f * height, 0), height * 0.55f, "Hat");

        var voice = Src(head, new Vector3(0, faceY, faceZ), null, false, 1f, 1f, Mathf.Lerp(8f, 18f, height / 2.6f), false);
        voice.pitch = Mathf.Lerp(1.25f, 0.8f, height / 2.6f);
        var a = Add<FirAnimatronic>(root);
        a.head = head; a.mouth = mouthPivot; a.pupilL = pupL.transform; a.pupilR = pupR.transform;
        a.voice = voice; a.chapter = chapter;
        a.talkClip = Clip("tree_talk"); a.singClip = Clip("tree_sing"); a.screamClip = Clip(height > 1.8f ? "scream2" : "scream1");
        tr.anim = a;
        return tr;
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

    static GameObject CarryHat(Transform g, Vector3 pos, float scale, int sizeId, string label)
    {
        var go = new GameObject("SantaHat " + sizeId);
        go.transform.SetParent(g, false);
        go.transform.localPosition = pos;
        Hat(go.transform, Vector3.zero, scale * 0.9f, "Visual");
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0, scale * 0.22f, 0); col.size = new Vector3(scale * 0.35f, scale * 0.45f, scale * 0.35f);
        var c = MakePickup(go, label);
        c.kind = 0; c.sizeId = sizeId;
        return go;
    }

    static void CarryImitation(Transform g, Vector3 pos, int i)
    {
        var holder = new GameObject("Imitation " + i);
        holder.transform.SetParent(g, false);
        holder.transform.localPosition = pos;
        holder.transform.localRotation = Quaternion.Euler(0, i * 73f, 0);
        var t = BuildTree(holder.transform, Vector3.zero, 0, 0.55f, 6, false, "ImitationTree");
        t.anim.watchRange = 3f; t.anim.greetChance = 0.5f;
        t.anim.talkClip = Clip("whisper");
        var col = holder.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.28f, 0); col.size = new Vector3(0.36f, 0.56f, 0.36f);
        var c = MakePickup(holder, "Imitation tree");
        c.kind = 1; c.index = itemList.Count; c.anim = t.anim;
        c.cry = Src(holder.transform, new Vector3(0, 0.4f, 0), Clip("tree_talk"), false, 1f, 1f, 8f, false);
        c.cry.pitch = 1.6f;
        itemList.Add(holder);
        itemChapterList.Add(6);
    }

    static FirCarryItem MakePickup(GameObject go, string label)
    {
        go.layer = 13;  // VRChat "Pickup" layer
        foreach (Transform ch in go.GetComponentsInChildren<Transform>(true)) ch.gameObject.layer = 13;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.5f; rb.useGravity = true; rb.isKinematic = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
        var p = go.AddComponent<VRCPickup>();
        p.InteractionText = label;
        p.AutoHold = VRC_Pickup.AutoHoldMode.Yes;
        p.proximity = 2f;
        var sync = go.AddComponent<VRCObjectSync>();
        carrySyncList.Add(sync);
        var c = Add<FirCarryItem>(go);
        c.pickup = p;
        return c;
    }

    /// <summary>A throwable porcelain toilet. Seat faces the root's +Z.</summary>
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
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.41f, -0.02f);
        col.size = new Vector3(0.44f, 0.82f, 0.62f);
        go.layer = 13;
        foreach (Transform ch in go.GetComponentsInChildren<Transform>(true)) ch.gameObject.layer = 13;
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 3f; rb.useGravity = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var p = go.AddComponent<VRCPickup>();
        p.InteractionText = "Toilet";
        p.UseText = "Flush";
        p.AutoHold = VRC_Pickup.AutoHoldMode.Yes;
        p.ThrowVelocityBoostScale = 1.4f;
        p.proximity = 2f;
        carrySyncList.Add(go.AddComponent<VRCObjectSync>());
        var t = Add<FirToilet>(go);
        t.pickup = p; t.body = rb;
        t.audioSrc = Src(go.transform, new Vector3(0, 0.5f, 0), null, false, 1f, 1f, 14f, false);
        t.flushClip = Clip("toilet_flush"); t.clunkClip = Clip("toilet_clunk");
        toilets.Add(t);
    }

    /// <summary>A pressable collectible with a global item index.</summary>
    static GameObject Item(Transform g, string name, int chapter, Vector3 pos, Material mat, Vector3 size, PrimitiveType shape, string label, FirJumpscare onTake)
    {
        var go = new GameObject(name + " " + itemList.Count);
        go.transform.SetParent(g, false);
        go.transform.localPosition = pos;
        Prim(shape, "Visual", go.transform, Vector3.zero, size, mat);
        Prim(PrimitiveType.Sphere, "Glint", go.transform, new Vector3(0, size.y * 0.6f, 0), Vector3.one * 0.02f, M("Glint"));
        go.AddComponent<BoxCollider>().size = Vector3.one * 0.28f;
        var it = Add<FirItem>(go);
        it.game = game; it.index = itemList.Count; it.scareOnTake = onTake;
        SetInteract(it, label, 2.5f);
        itemList.Add(go);
        itemChapterList.Add(chapter);
        return go;
    }

    static void Station(GameObject go, string label, string evt)
    {
        var b = Add<FirButton>(go);
        b.target = game; b.eventName = evt;
        SetInteract(b, label, 2.5f);
        Prim(PrimitiveType.Cube, "Light", go.transform, new Vector3(0, 0.35f, -0.12f), new Vector3(0.1f, 0.1f, 0.02f), M("GlowRed"));
    }

    /// <summary>A scare. A zero size means "no trigger" (fired by an item instead).</summary>
    static FirJumpscare Scare(Transform g, string name, Vector3 pos, Vector3 size, int kind, int minPhase, int maxPhase)
    {
        var go = new GameObject("Scare " + name);
        go.transform.SetParent(g, false);
        go.transform.localPosition = pos;
        if (size != Vector3.zero)
        {
            var c = go.AddComponent<BoxCollider>();
            c.isTrigger = true; c.size = size;
        }
        var s = Add<FirJumpscare>(go);
        s.game = game; s.kind = kind; s.minPhase = minPhase; s.maxPhase = maxPhase;
        return s;
    }

    static GameObject Door(Transform g, float z, string nextName)
    {
        var d = Box(g, "Door to " + nextName, new Vector3(0, 1.3f, z), new Vector3(2.4f, 2.6f, 0.15f), M("Door"), true, false, false);
        var obs = d.AddComponent<NavMeshObstacle>();
        obs.shape = NavMeshObstacleShape.Box; obs.size = new Vector3(2.4f, 2.6f, 0.5f); obs.carving = true;
        Prim(PrimitiveType.Cube, "Lock", d.transform, new Vector3(0.95f, 0, -0.09f), new Vector3(0.08f, 0.08f, 0.03f), M("GlowRed"));
        Prim(PrimitiveType.Cube, "LockBack", d.transform, new Vector3(0.95f, 0, 0.09f), new Vector3(0.08f, 0.08f, 0.03f), M("GlowRed"));
        return d;
    }

    static void DoorWall(Transform g, string name, float z, float w, float h, Material m, bool gap)
    {
        if (!gap) { Box(g, name, new Vector3(0, h / 2, z), new Vector3(w, h, 0.2f), m, true); return; }
        float gw = 2.4f, gh = 2.6f, side = (w - gw) / 2;
        Box(g, name + " L", new Vector3(-gw / 2 - side / 2, h / 2, z), new Vector3(side, h, 0.2f), m, true);
        Box(g, name + " R", new Vector3(gw / 2 + side / 2, h / 2, z), new Vector3(side, h, 0.2f), m, true);
        Box(g, name + " Top", new Vector3(0, (gh + h) / 2, z), new Vector3(gw, h - gh, 0.2f), m, true);
    }

    static void Table(Transform g, Vector3 at, Vector3 size, Material cloth)
    {
        Box(g, "TableTop", at + new Vector3(0, size.y - 0.03f, 0), new Vector3(size.x, 0.06f, size.z), cloth, true);
        Box(g, "ClothBack", at + new Vector3(-size.x / 2 + 0.02f, size.y / 2, 0), new Vector3(0.03f, size.y, size.z), cloth, true);
        Box(g, "ClothEndA", at + new Vector3(0, size.y / 2, -size.z / 2), new Vector3(size.x, size.y, 0.03f), cloth, true);
        Box(g, "ClothEndB", at + new Vector3(0, size.y / 2, size.z / 2), new Vector3(size.x, size.y, 0.03f), cloth, true);
        Box(g, "ClothFrontHalf", at + new Vector3(size.x / 2, size.y * 0.75f, 0), new Vector3(0.03f, size.y * 0.5f, size.z), cloth, true);
    }

    static void Desk(Transform g, Vector3 at, float yaw)
    {
        var d = Group(g, "Desk", at);
        d.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(d, "Top", new Vector3(0, 0.74f, 0), new Vector3(1.6f, 0.05f, 0.8f), M("DeskWood"), true);
        Box(d, "LegA", new Vector3(-0.75f, 0.36f, 0), new Vector3(0.06f, 0.72f, 0.7f), M("Metal"), true);
        Box(d, "LegB", new Vector3(0.75f, 0.36f, 0), new Vector3(0.06f, 0.72f, 0.7f), M("Metal"), true);
    }

    static void Shelf(Transform g, Vector3 center, float length)
    {
        var s = Group(g, "Shelf", center);
        Box(s, "BackPanel", new Vector3(0, 1.6f, 0), new Vector3(0.06f, 3.2f, length), M("Metal"), true);
        for (int k = 0; k < 4; k++)
            Box(s, "Board", new Vector3(0, 0.15f + k * 0.95f, 0), new Vector3(1.0f, 0.05f, length), M("Metal"), true);
        for (float z = -length / 2 + 0.6f; z < length / 2 - 0.4f; z += 0.9f)
            for (int k = 0; k < 3; k++)
            {
                if (UnityEngine.Random.value < 0.25f) continue;
                float side = UnityEngine.Random.value < 0.5f ? -0.25f : 0.25f;
                Box(s, "TreeBox", new Vector3(side, 0.45f + k * 0.95f, z), new Vector3(0.45f, 0.55f, 0.75f), M("Cardboard"), false, true, true);
            }
    }

    static void BoxStack(Transform g, Vector3 at, int nx, int ny)
    {
        var s = Group(g, "BoxStack", at);
        Box(s, "Pallet", new Vector3(0, 0.07f, 0), new Vector3(nx * 0.8f + 0.1f, 0.14f, 1.3f), M("DeskWood"), true);
        for (int x = 0; x < nx; x++)
            for (int y = 0; y < ny; y++)
                Box(s, "TreeBox", new Vector3((x - (nx - 1) / 2f) * 0.8f, 0.14f + 0.35f + y * 0.7f, 0), new Vector3(0.75f, 0.7f, 1.2f), M("Cardboard"), true, true, true);
    }

    static void Gifts(Transform g, Vector3 at, int count)
    {
        string[] gm = { "GiftRed", "GiftGreen", "GiftGold", "GiftBlue" };
        for (int i = 0; i < count; i++)
        {
            float s = UnityEngine.Random.Range(0.25f, 0.55f);
            var p = at + new Vector3(UnityEngine.Random.Range(-0.9f, 0.9f), s / 2, UnityEngine.Random.Range(-0.7f, 0.7f));
            var b = Box(g, "Gift", p, new Vector3(s, s, s * 1.2f), M(gm[i % gm.Length]), true, true, true);
            b.transform.localRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 90), 0);
            Prim(PrimitiveType.Cube, "Ribbon", b.transform, Vector3.zero, new Vector3(1.02f, 1.02f, 0.15f), M("GiftGold"));
        }
    }

    static void Poster(Transform g, string tex, Vector3 pos, Vector3 normal, float height, bool fullbright)
    {
        string name = "Poster_" + tex + (fullbright ? "_bright" : "");
        if (!mats.ContainsKey(name)) mats[name] = Mat(name, Tex(tex), Color.white, fullbright ? 1 : 0, Color.black);
        Quad(g, "Poster " + tex, pos, Face(normal), new Vector2(height * 512f / 720f, height), mats[name]);
    }

    static void Sign(Transform g, string text, Vector3 pos, Vector3 normal, string mat = "GlowRed")
    {
        Box(g, "Sign " + text, pos - normal * 0.0f, new Vector3(Mathf.Max(1.2f, text.Length * 0.22f), 0.35f, 0.06f), M("Black"), false, false);
        var t = Text("SignText", g, pos + normal * 0.04f, Face(normal), new Vector2(Mathf.Max(1.2f, text.Length * 0.22f), 0.3f), 0.25f, "<b>" + text + "</b>");
        t.color = mat == "GlowGreen" ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.25f, 0.2f);
    }

    static GameObject Button(string label, Transform parent, Vector3 pos, Vector3 normal, Vector3 size, Material mat, UdonSharpBehaviour target, string evt)
    {
        var go = new GameObject("Button " + label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Face(normal);
        go.AddComponent<BoxCollider>().size = size;
        Prim(PrimitiveType.Cube, "Visual", go.transform, Vector3.zero, size, mat);
        var t = Text("Label", go.transform, new Vector3(0, 0, -size.z / 2 - 0.003f), Quaternion.identity, new Vector2(size.x * 0.92f, size.y * 0.85f), 0.2f, label);
        t.color = Color.white;
        var b = Add<FirButton>(go);
        b.target = target; b.eventName = evt;
        SetInteract(b, label, 4f);
        return go;
    }

    static void Key(FirSequenceLock lk, string label, int value, Vector3 pos, Vector3 size, Material mat, bool flatOnTop)
    {
        var go = new GameObject("Key " + label);
        go.transform.SetParent(lk.transform, false);
        go.transform.localPosition = pos;
        go.AddComponent<BoxCollider>().size = size;
        Prim(PrimitiveType.Cube, "Visual", go.transform, Vector3.zero, size, mat);
        var rot = flatOnTop ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
        var off = flatOnTop ? new Vector3(0, size.y / 2 + 0.003f, 0) : new Vector3(0, 0, -size.z / 2 - 0.003f);
        var t = Text("Label", go.transform, off, rot, new Vector2(size.x * 0.9f, flatOnTop ? size.z * 0.6f : size.y * 0.8f), 0.06f, label);
        t.color = Color.white;
        var k = Add<FirKey>(go);
        k.lockTarget = lk; k.value = value;
        SetInteract(k, label, 2f);
    }

    static Transform Marker(Transform g, string name, Vector3 pos, float yaw)
    {
        var m = new GameObject(name).transform;
        m.SetParent(g, false);
        m.localPosition = pos;
        m.localRotation = Quaternion.Euler(0, yaw, 0);
        return m;
    }

    // =================================================================== meshes

    /// <summary>Stacked jagged cones: a fir tree canopy, base at y=0, tip at y=height.</summary>
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

    /// <summary>A box mesh whose UVs are in world metres (2 m per texture repeat), so walls don't stretch.</summary>
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

    // =================================================================== scene helpers

    static Transform Group(Transform parent, string name, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    /// <summary>A static box (walls, floors, furniture). unitUV = the texture covers each face once (crates, gifts).</summary>
    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider, bool isStatic = true, bool unitUV = false)
    {
        GameObject go;
        if (unitUV)
        {
            go = Prim(PrimitiveType.Cube, name, parent, pos, size, mat);
            if (collider) go.AddComponent<BoxCollider>();
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            NoShadows(go.AddComponent<MeshRenderer>(), mat);
            if (collider) go.AddComponent<BoxCollider>().size = size;
        }
        if (isStatic)
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                        StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic);
        return go;
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        NoShadows(go.GetComponent<MeshRenderer>(), mat);
        return go;
    }

    static GameObject Quad(Transform parent, string name, Vector3 pos, Quaternion rot, Vector2 size, Material mat)
    {
        var q = Prim(PrimitiveType.Quad, name, parent, pos, new Vector3(size.x, size.y, 1), mat);
        q.transform.localRotation = rot;
        return q;
    }

    static void NoShadows(MeshRenderer r, Material mat)
    {
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    /// <summary>Rotation for a flat thing (quad, text) on a surface whose visible side faces `normal`.</summary>
    static Quaternion Face(Vector3 normal) { return Quaternion.LookRotation(-normal, Vector3.up); }

    static TextMeshPro Text(string name, Transform parent, Vector3 pos, Quaternion rot, Vector2 size, float fontMax, string text)
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
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.01f;
        t.fontSizeMax = fontMax;
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

    static T Add<T>(GameObject go) where T : UdonSharpBehaviour
    {
        var b = go.AddComponent<T>();
        Setup(b);
        allBehaviours.Add(b);
        return b;
    }

    static void SetInteract(UdonSharpBehaviour b, string text, float proximity)
    {
        var u = Backing(b);
        if (u == null) return;
        u.interactText = text;
        u.proximity = proximity;
    }

    // =================================================================== assets

    static AudioClip Clip(string name)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + name + ".ogg");
        if (c == null) Debug.LogWarning("Fir Warning: missing sound " + name);
        return c;
    }

    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + name + ".png");

    static void ConfigureAssets()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            bool poster = Path.GetFileName(path).StartsWith("poster");
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = true;
            imp.wrapMode = poster ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.maxTextureSize = 512;
            imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            { name = "Android", overridden = true, maxTextureSize = 512, format = TextureImporterFormat.ASTC_6x6 });
            imp.SaveAndReimport();
        }
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Audio" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) continue;
            imp.forceToMono = true;
            imp.loadInBackground = true;
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.45f;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
    }

    static Shader Lantern() => Shader.Find("FirHorror/Lantern");

    static Material Mat(string name, Texture tex, Color c, float fullbright, Color glow)
    {
        string path = Gen + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(Lantern());
        m.shader = Lantern();
        m.SetColor("_Color", c);
        m.mainTexture = tex;
        m.SetFloat("_Fullbright", fullbright);
        m.SetColor("_Glow", glow);
        m.enableInstancing = true;
        if (isNew) AssetDatabase.CreateAsset(m, path);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material M(string name)
    {
        if (mats.TryGetValue(name, out var m)) return m;
        Debug.LogWarning("Fir Warning: unknown material " + name);
        return mats["Black"];
    }

    static void MakeMaterials()
    {
        void A(string n, string tex, Color c, float full = 0, Color? glow = null) => mats[n] = Mat(n, tex != null ? Tex(tex) : null, c, full, glow ?? Color.black);
        var white = Color.white;
        A("Black", null, new Color(0.03f, 0.03f, 0.03f));
        A("Wood", "wood_floor", white);
        A("Wallpaper", "wallpaper", white);
        A("Ceiling", "ceiling", white);
        A("LivingCeiling", "paper", new Color(0.85f, 0.82f, 0.75f));
        A("Concrete", "concrete", white);
        A("Brick", "brick", white);
        A("Metal", "metal", white);
        A("OfficeCarpet", "office_carpet", white);
        A("OfficeWall", "paper", new Color(0.75f, 0.72f, 0.62f));
        A("Partition", "office_carpet", new Color(0.8f, 0.8f, 0.85f));
        A("Cardboard", "cardboard", white);
        A("Paper", "paper", white, 0.6f);
        A("Whiteboard", null, new Color(0.92f, 0.92f, 0.9f), 0.5f);
        A("Needles", "needles", white);
        mats["Needles"].SetFloat("_VertexColor", 1f);
        A("Pot", null, new Color(0.55f, 0.12f, 0.1f));
        A("Stand", null, new Color(0.25f, 0.2f, 0.15f));
        A("Trunk", null, new Color(0.3f, 0.2f, 0.12f));
        A("ClothRed", "carpet_red", white);
        A("Curtain", null, new Color(0.8f, 0.78f, 0.7f));
        A("Trim", null, new Color(0.85f, 0.83f, 0.78f));
        A("DeskWood", "wood_floor", new Color(0.8f, 0.7f, 0.6f));
        A("Rubber", null, new Color(0.08f, 0.08f, 0.08f));
        A("Foam", "office_carpet", new Color(0.35f, 0.35f, 0.38f));
        A("Door", "metal", new Color(0.6f, 0.15f, 0.12f));
        A("Window", "window_snow", new Color(0.55f, 0.6f, 0.7f), 1f);
        A("GiftRed", null, new Color(0.7f, 0.08f, 0.1f));
        A("GiftGreen", null, new Color(0.1f, 0.45f, 0.15f));
        A("GiftGold", null, new Color(0.85f, 0.65f, 0.15f));
        A("GiftBlue", null, new Color(0.15f, 0.25f, 0.6f));
        A("HatRed", null, new Color(0.75f, 0.05f, 0.08f));
        A("HatWhite", null, new Color(0.95f, 0.95f, 0.95f));
        A("Porcelain", null, new Color(0.93f, 0.93f, 0.9f));
        A("ToiletWater", null, new Color(0.45f, 0.6f, 0.65f));
        // tree faces: eyes stay faintly visible in the dark, which is the point
        A("Eye", null, new Color(0.9f, 0.9f, 0.85f), 0f, new Color(0.22f, 0.22f, 0.2f));
        A("Pupil", null, new Color(0.01f, 0.01f, 0.01f), 1f);
        A("Mouth", null, new Color(0.7f, 0.05f, 0.08f), 0f, new Color(0.08f, 0f, 0f));
        // items glint so they can be found with the lantern
        A("Battery", null, new Color(0.85f, 0.7f, 0.1f), 0f, new Color(0.15f, 0.12f, 0.02f));
        A("Fuse", null, new Color(0.7f, 0.85f, 0.9f), 0f, new Color(0.1f, 0.14f, 0.16f));
        A("TapeRed", null, new Color(0.9f, 0.15f, 0.15f), 0f, new Color(0.15f, 0.02f, 0.02f));
        A("TapeBlue", null, new Color(0.2f, 0.6f, 1f), 0f, new Color(0.03f, 0.08f, 0.15f));
        A("TapeGreen", null, new Color(0.25f, 0.9f, 0.3f), 0f, new Color(0.03f, 0.13f, 0.04f));
        A("TapeGold", null, new Color(1f, 0.8f, 0.2f), 0f, new Color(0.15f, 0.12f, 0.03f));
        A("Glint", null, white, 1f, new Color(0.6f, 0.6f, 0.5f));
        A("GlowRed", null, new Color(0.3f, 0.02f, 0.02f), 1f, new Color(0.5f, 0.05f, 0.05f));
        A("GlowGreen", null, new Color(0.05f, 0.4f, 0.1f), 1f, new Color(0.1f, 0.5f, 0.15f));
        A("GlowOrange", null, new Color(0.8f, 0.35f, 0.05f), 1f, new Color(0.6f, 0.25f, 0.02f));
        A("LampDim", null, new Color(0.2f, 0.19f, 0.15f), 1f);
        // lobby: fullbright
        A("LobbyFloor", "carpet_red", white, 1f);
        A("LobbyWall", "wallpaper", new Color(0.8f, 0.8f, 0.8f), 1f);
        A("LobbyCeiling", "ceiling", new Color(0.7f, 0.7f, 0.7f), 1f);
        A("Btn", null, new Color(0.22f, 0.28f, 0.38f), 1f);
        A("BtnGo", null, new Color(0.15f, 0.55f, 0.22f), 1f);
        A("BtnWarn", null, new Color(0.6f, 0.22f, 0.12f), 1f);
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
        var camGo = new GameObject("ReferenceCamera");
        camGo.transform.SetParent(spawn.root, false);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.enabled = false;
        desc.ReferenceCamera = camGo;
        EditorUtility.SetDirty(desc);
    }

    // =================================================================== UdonSharp glue (reflection keeps this working across U# versions)

    static readonly string[] Scripts = { "FirGameManager", "FirAnimatronic", "FirStalker", "FirJumpscare", "FirItem", "FirCarryItem",
                                         "FirSocket", "FirSequenceLock", "FirKey", "FirButton", "FirConveyor", "FirAmbience", "FirToilet" };

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

    static Type EditorUtil => FindAnyType("UdonSharpEditor.UdonSharpEditorUtility");

    static Type FindAnyType(string full) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(full)).FirstOrDefault(t => t != null);

    static void Setup(UdonSharpBehaviour b)
    {
        var m = EditorUtil?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => x.Name == "RunBehaviourSetup");
        if (m == null) return;
        var ps = m.GetParameters();
        var args = new object[ps.Length];
        args[0] = b;
        for (int i = 1; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
        m.Invoke(null, args);
    }

    static VRC.Udon.UdonBehaviour Backing(UdonSharpBehaviour b)
    {
        var m = EditorUtil?.GetMethod("GetBackingUdonBehaviour", BindingFlags.Public | BindingFlags.Static);
        return m?.Invoke(null, new object[] { b }) as VRC.Udon.UdonBehaviour;
    }

    static void Sync(UdonSharpBehaviour b)
    {
        var m = EditorUtil?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(x => x.Name == "CopyProxyToUdon" && x.GetParameters().Length >= 1);
        if (m != null)
        {
            var ps = m.GetParameters();
            var args = new object[ps.Length];
            args[0] = b;
            for (int i = 1; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
            m.Invoke(null, args);
        }
        EditorUtility.SetDirty(b);
        var u = Backing(b);
        if (u != null) EditorUtility.SetDirty(u);
    }
}
#endif
